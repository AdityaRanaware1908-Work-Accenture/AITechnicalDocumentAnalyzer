using DocumentAnalyzer.Core.Abstractions;
using DocumentAnalyzer.Core.Models.Reports;
using DocumentAnalyzer.Core.Validation;

namespace DocumentAnalyzer.Core.Analysis;

public sealed record DocumentInput(string Name, string Text);

public sealed record AnalysisRunResult(AnalysisReport Report, IReadOnlyList<string> Warnings, string ReportFileName);

/// <summary>
/// The application service the UI talks to: documents in, validated and saved report out.
/// Everything it needs comes through interfaces, so it runs unchanged against any provider or storage.
/// </summary>
public sealed class AnalysisService(AnalysisPipeline pipeline, IDocumentCatalog catalog, IReportStore reports)
{
    // ------------------------------------------------------------------
    // Documents
    // ------------------------------------------------------------------

    public Task<IReadOnlyList<DocumentInfo>> ListDocumentsAsync(Constants.DocumentKind kind, CancellationToken cancellationToken = default) =>
        catalog.ListAsync(kind, cancellationToken);

    public Task<string> ReadDocumentAsync(Constants.DocumentKind kind, string name, CancellationToken cancellationToken = default) =>
        catalog.ReadAsync(kind, name, cancellationToken);

    // ------------------------------------------------------------------
    // Runs. The model call is one blocking step, so progress is reported in coarse stages.
    // ------------------------------------------------------------------

    public async Task<AnalysisRunResult> AnalyzeAsync(DocumentInput requirements, IProgress<Constants.RunStage>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(Constants.RunStage.WaitingForModel);

        var report = await pipeline.AnalyzeAsync(requirements.Name, requirements.Text, cancellationToken);

        return await FinishAsync(report, [requirements], progress, cancellationToken);
    }

    public async Task<AnalysisRunResult> CompareAsync(DocumentInput requirements, DocumentInput design, IProgress<Constants.RunStage>? progress = null, CancellationToken cancellationToken = default)
    {
        // The validator and the report identify documents by name, so the two names must differ.
        if (string.Equals(requirements.Name, design.Name, StringComparison.OrdinalIgnoreCase))
        {
            design = design with { Name = $"{design.Name} (design)" };
        }

        progress?.Report(Constants.RunStage.WaitingForModel);

        var report = await pipeline.CompareAsync(requirements.Name, requirements.Text, design.Name, design.Text, cancellationToken);

        return await FinishAsync(report, [requirements, design], progress, cancellationToken);
    }

    private async Task<AnalysisRunResult> FinishAsync(AnalysisReport report, IReadOnlyList<DocumentInput> documents, IProgress<Constants.RunStage>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(Constants.RunStage.Validating);

        var byName = documents.ToDictionary(d => d.Name, d => d.Text, StringComparer.OrdinalIgnoreCase);
        var warnings = EvidenceValidator.Validate(report, byName);

        progress?.Report(Constants.RunStage.Saving);

        var fileName = await reports.SaveAsync(report, cancellationToken);

        return new AnalysisRunResult(report, warnings, fileName);
    }

    // ------------------------------------------------------------------
    // Saved reports
    // ------------------------------------------------------------------

    public Task<IReadOnlyList<ReportSummary>> ListReportsAsync(int max = 100, CancellationToken cancellationToken = default) =>
        reports.ListAsync(max, cancellationToken);

    public Task<AnalysisReport?> LoadReportAsync(string fileName, CancellationToken cancellationToken = default) =>
        reports.LoadAsync(fileName, cancellationToken);
}
