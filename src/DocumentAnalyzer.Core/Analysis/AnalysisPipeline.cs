using DocumentAnalyzer.Core.Abstractions;
using DocumentAnalyzer.Core.Models.ModelResponses;
using DocumentAnalyzer.Core.Models.Reports;
using DocumentAnalyzer.Core.Prompts;

namespace DocumentAnalyzer.Core.Analysis;

/// <summary>
/// One model call per analysis: build the prompt, ask the model through ILlmClient, map the response into a report.
/// It knows nothing about Ollama, HTTP, files or the UI.
/// </summary>
public sealed class AnalysisPipeline(ILlmClient llm)
{
    public async Task<AnalysisReport> AnalyzeAsync(string documentName, string documentText, CancellationToken cancellationToken = default)
    {
        var request = new LlmRequest(
            PromptBuilder.SystemPrompt,
            PromptBuilder.BuildAnalyzeDocumentPrompt(documentName, documentText),
            PromptBuilder.AnalyzeOutputSchema);

        var response = await llm.GenerateAsync<DocumentAnalysisModelResponse>(request, cancellationToken);

        var report = AnalysisReportMapper.BuildAnalyzeReport(response.Content, documentName, documentText);
        report.ProcessingMetrics = response.Metrics;

        return report;
    }

    public async Task<AnalysisReport> CompareAsync(string requirementsName, string requirementsText, string designName, string designText, CancellationToken cancellationToken = default)
    {
        var request = new LlmRequest(
            PromptBuilder.SystemPrompt,
            PromptBuilder.BuildCompareAndAnalyzeDocumentsPrompt(requirementsName, requirementsText, designName, designText),
            PromptBuilder.CompareOutputSchema);

        var response = await llm.GenerateAsync<DocumentComparisonModelResponse>(request, cancellationToken);

        var report = AnalysisReportMapper.BuildCompareReport(response.Content, requirementsName, requirementsText, designName, designText);
        report.ProcessingMetrics = response.Metrics;

        return report;
    }
}
