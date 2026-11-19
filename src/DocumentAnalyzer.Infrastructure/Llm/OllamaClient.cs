using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentAnalyzer.Core.Abstractions;
using DocumentAnalyzer.Core.Models.Reports;
using Microsoft.Extensions.Options;

namespace DocumentAnalyzer.Infrastructure.Llm;

/// <summary>ILlmClient for a local Ollama server, using its /api/chat endpoint with a JSON schema in "format".</summary>
public sealed class OllamaClient(HttpClient httpClient, IOptions<LlmOptions> options) : ILlmClient
{
    private static readonly JsonSerializerOptions ResponseOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    private readonly LlmOptions llm = options.Value;

    public string ProviderName => LlmProviders.Ollama;

    public string ModelName => llm.Ollama.Model;

    public async Task<LlmResponse<T>> GenerateAsync<T>(LlmRequest request, CancellationToken cancellationToken = default) where T : class
    {
        var body = new
        {
            model = llm.Ollama.Model,
            stream = false,
            think = false,
            keep_alive = llm.Ollama.KeepAlive,
            format = request.JsonSchema,
            options = BuildOptions(),
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            }
        };

        var stopwatch = Stopwatch.StartNew();

        using var response = await httpClient.PostAsJsonAsync("/api/chat", body, cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        stopwatch.Stop();

        var chat = JsonSerializer.Deserialize<OllamaChatResponse>(responseBody, ResponseOptions);
        var content = chat?.Message?.Content;

        if (chat is null || string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"Ollama response content was empty. Raw response: {responseBody}");
        }

        if (string.Equals(chat.DoneReason, "length", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(LlmJson.TruncatedMessage);
        }

        var metrics = new ProcessingMetrics
        {
            Provider = ProviderName,
            Model = ModelName,
            TotalDurationSeconds = stopwatch.Elapsed.TotalSeconds,
            PromptTokens = chat.PromptEvalCount,
            GeneratedTokens = chat.EvalCount,
            PromptCharacters = request.SystemPrompt.Length + request.UserPrompt.Length,
            ModelLoadSeconds = Seconds(chat.LoadDuration),
            PromptEvaluationSeconds = Seconds(chat.PromptEvalDuration),
            GenerationSeconds = Seconds(chat.EvalDuration)
        };

        return new LlmResponse<T>(LlmJson.Deserialize<T>(content), metrics);
    }

    private Dictionary<string, object> BuildOptions()
    {
        var result = new Dictionary<string, object>
        {
            ["temperature"] = llm.Temperature,
            ["num_predict"] = llm.MaxOutputTokens
        };

        // The context window must hold the prompt AND the generated output; when it is too small Ollama can silently
        // truncate or shift earlier tokens, including the instructions. 0 keeps Ollama's default.
        if (llm.Ollama.NumCtx > 0)
        {
            result["num_ctx"] = llm.Ollama.NumCtx;
        }

        return result;
    }

    private static double Seconds(long nanoseconds) => nanoseconds / 1_000_000_000d;

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")] public OllamaMessage? Message { get; set; }
        [JsonPropertyName("done_reason")] public string? DoneReason { get; set; }
        [JsonPropertyName("load_duration")] public long LoadDuration { get; set; }
        [JsonPropertyName("prompt_eval_duration")] public long PromptEvalDuration { get; set; }
        [JsonPropertyName("eval_duration")] public long EvalDuration { get; set; }
        [JsonPropertyName("prompt_eval_count")] public int PromptEvalCount { get; set; }
        [JsonPropertyName("eval_count")] public int EvalCount { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }
}
