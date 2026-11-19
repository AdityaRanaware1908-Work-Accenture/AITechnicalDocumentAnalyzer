namespace DocumentAnalyzer.Infrastructure.Llm;

/// <summary>Names accepted in the Llm:Provider setting. To add a provider, add a name here, a client class and a case in AddLlmClient.</summary>
public static class LlmProviders
{
    public const string Ollama = "Ollama";
    public const string OpenAiCompatible = "OpenAiCompatible";
}

/// <summary>Settings shared by all providers, plus one block per provider. Bound from the "Llm" configuration section.</summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>Which provider the app uses, see LlmProviders.</summary>
    public string Provider { get; set; } = LlmProviders.Ollama;

    public double Temperature { get; set; }

    public int MaxOutputTokens { get; set; } = 5000;

    public int TimeoutMinutes { get; set; } = 30;

    public OllamaOptions Ollama { get; set; } = new();

    public OpenAiCompatibleOptions OpenAiCompatible { get; set; } = new();
}

public sealed class OllamaOptions
{
    public string BaseAddress { get; set; } = "http://localhost:11434";

    public string Model { get; set; } = "qwen3:4b";

    public string KeepAlive { get; set; } = "30m";

    /// <summary>Context window in tokens; it must hold the prompt and the generated output. 0 keeps Ollama's default.</summary>
    public int NumCtx { get; set; }
}

/// <summary>
/// Any service that speaks the OpenAI chat completions API with JSON-schema structured output
/// (OpenAI, many gateways, vLLM, LM Studio). Keep the API key out of appsettings: use user-secrets or the
/// environment variable Llm__OpenAiCompatible__ApiKey.
/// </summary>
public sealed class OpenAiCompatibleOptions
{
    public string BaseAddress { get; set; } = "https://api.openai.com/v1/";

    public string Model { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>"max_tokens" for most services, "max_completion_tokens" for newer OpenAI models.</summary>
    public string MaxTokensField { get; set; } = "max_tokens";

    /// <summary>Set to false for models that only accept the default temperature.</summary>
    public bool SendTemperature { get; set; } = true;
}
