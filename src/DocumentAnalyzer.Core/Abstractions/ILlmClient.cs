using DocumentAnalyzer.Core.Models.Reports;

namespace DocumentAnalyzer.Core.Abstractions;

/// <summary>A provider-neutral request for schema-constrained JSON output.</summary>
/// <param name="SystemPrompt">General rules for the model.</param>
/// <param name="UserPrompt">The task prompt including the documents.</param>
/// <param name="JsonSchema">JSON schema object the response must follow. Providers map it to their own structured-output mechanism.</param>
public sealed record LlmRequest(string SystemPrompt, string UserPrompt, object JsonSchema);

/// <summary>The parsed model output together with what the call cost.</summary>
public sealed record LlmResponse<T>(T Content, ProcessingMetrics Metrics) where T : class;

/// <summary>
/// The single seam between the analysis logic and any model provider. The Core project only knows this interface;
/// local Ollama today, a cloud API tomorrow: implement it, register it, select it in configuration.
/// </summary>
public interface ILlmClient
{
    /// <summary>Provider label shown in the UI and stored in the report (for example "Ollama").</summary>
    string ProviderName { get; }

    /// <summary>Model label shown in the UI and stored in the report.</summary>
    string ModelName { get; }

    /// <summary>
    /// Sends the request and returns the model's JSON deserialized into T.
    /// Implementations throw InvalidOperationException for unusable output (invalid JSON, truncated output)
    /// and HttpRequestException for transport or authentication problems.
    /// </summary>
    Task<LlmResponse<T>> GenerateAsync<T>(LlmRequest request, CancellationToken cancellationToken = default) where T : class;
}
