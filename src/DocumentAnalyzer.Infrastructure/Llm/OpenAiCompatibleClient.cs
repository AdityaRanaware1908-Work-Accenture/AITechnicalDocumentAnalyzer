using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentAnalyzer.Core.Abstractions;
using DocumentAnalyzer.Core.Models.Reports;
using Microsoft.Extensions.Options;

namespace DocumentAnalyzer.Infrastructure.Llm;

/// <summary>
/// ILlmClient for services that speak the OpenAI chat completions API with JSON-schema structured output.
/// NOT tested against a live endpoint yet: check it against your provider before relying on it.
/// </summary>
public sealed class OpenAiCompatibleClient(HttpClient httpClient, IOptions<LlmOptions> options) : ILlmClient
{
    private readonly LlmOptions llm = options.Value;

    private OpenAiCompatibleOptions Provider => llm.OpenAiCompatible;

    public string ProviderName => LlmProviders.OpenAiCompatible;

    public string ModelName => Provider.Model;

    public async Task<LlmResponse<T>> GenerateAsync<T>(LlmRequest request, CancellationToken cancellationToken = default) where T : class
    {
        if (string.IsNullOrWhiteSpace(Provider.ApiKey))
        {
            throw new InvalidOperationException(
                "No API key configured. Set Llm:OpenAiCompatible:ApiKey with user-secrets or the environment variable Llm__OpenAiCompatible__ApiKey.");
        }

        // A dictionary keeps the token-limit field name configurable (max_tokens or max_completion_tokens).
        var body = new Dictionary<string, object>
        {
            ["model"] = Provider.Model,
            [Provider.MaxTokensField] = llm.MaxOutputTokens,
            ["messages"] = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            ["response_format"] = new
            {
                type = "json_schema",
                json_schema = new { name = "analysis_result", strict = true, schema = request.JsonSchema }
            }
        };

        if (Provider.SendTemperature)
        {
            body["temperature"] = llm.Temperature;
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions") { Content = JsonContent.Create(body) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Provider.ApiKey);

        var stopwatch = Stopwatch.StartNew();

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            var detail = responseBody.Length > 500 ? responseBody[..500] : responseBody;

            throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}: {detail}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        var choice = root.GetProperty("choices")[0];

        if (choice.TryGetProperty("finish_reason", out var finish) && finish.GetString() == "length")
        {
            throw new InvalidOperationException(LlmJson.TruncatedMessage);
        }

        var content = choice.GetProperty("message").GetProperty("content").GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"The model returned no content. Raw response: {responseBody}");
        }

        var metrics = new ProcessingMetrics
        {
            Provider = ProviderName,
            Model = ModelName,
            TotalDurationSeconds = stopwatch.Elapsed.TotalSeconds,
            PromptCharacters = request.SystemPrompt.Length + request.UserPrompt.Length
        };

        if (root.TryGetProperty("usage", out var usage))
        {
            metrics.PromptTokens = usage.TryGetProperty("prompt_tokens", out var prompt) ? prompt.GetInt32() : 0;
            metrics.GeneratedTokens = usage.TryGetProperty("completion_tokens", out var completion) ? completion.GetInt32() : 0;
        }

        return new LlmResponse<T>(LlmJson.Deserialize<T>(content), metrics);
    }
}
