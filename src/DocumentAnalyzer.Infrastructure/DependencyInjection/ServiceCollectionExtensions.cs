using DocumentAnalyzer.Core.Abstractions;
using DocumentAnalyzer.Core.Analysis;
using DocumentAnalyzer.Infrastructure.Llm;
using DocumentAnalyzer.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocumentAnalyzer.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the analysis services and the implementations selected in configuration.
    /// The host calls this once; swapping the model provider or the storage is a configuration or registration change only.
    /// </summary>
    public static IServiceCollection AddDocumentAnalyzer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        var llm = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
        AddLlmClient(services, llm);

        services.AddSingleton<IDocumentCatalog, FileDocumentCatalog>();
        services.AddSingleton<IReportStore, FileReportStore>();

        services.AddScoped<AnalysisPipeline>();
        services.AddScoped<AnalysisService>();

        return services;
    }

    /// <summary>The one place that maps the Llm:Provider setting to a client. A new provider adds one case here.</summary>
    private static void AddLlmClient(IServiceCollection services, LlmOptions llm)
    {
        if (string.Equals(llm.Provider, LlmProviders.Ollama, StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<OllamaClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;

                client.BaseAddress = new Uri(options.Ollama.BaseAddress);
                client.Timeout = TimeSpan.FromMinutes(options.TimeoutMinutes);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseProxy = false });

            services.AddTransient<ILlmClient>(sp => sp.GetRequiredService<OllamaClient>());
        }
        else if (string.Equals(llm.Provider, LlmProviders.OpenAiCompatible, StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<OpenAiCompatibleClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;

                // The trailing slash matters: it makes "chat/completions" resolve under the base path (for example /v1/).
                client.BaseAddress = new Uri(options.OpenAiCompatible.BaseAddress.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromMinutes(options.TimeoutMinutes);
            });

            services.AddTransient<ILlmClient>(sp => sp.GetRequiredService<OpenAiCompatibleClient>());
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown Llm:Provider '{llm.Provider}'. Supported: {LlmProviders.Ollama}, {LlmProviders.OpenAiCompatible}.");
        }
    }
}
