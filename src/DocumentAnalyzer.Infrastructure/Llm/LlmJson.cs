using System.Text.Json;

namespace DocumentAnalyzer.Infrastructure.Llm;

/// <summary>JSON handling shared by every provider client.</summary>
internal static class LlmJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static T Deserialize<T>(string content) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(content, Options)
                ?? throw new InvalidOperationException("The model returned an empty JSON document.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"The model returned invalid JSON. Content: {content}", ex);
        }
    }

    public const string TruncatedMessage =
        "The model stopped because the output limit was reached, so the JSON is incomplete. " +
        "Raise Llm:MaxOutputTokens (and Ollama:NumCtx) or use a smaller document.";
}
