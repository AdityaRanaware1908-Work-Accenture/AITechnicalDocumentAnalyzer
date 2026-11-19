using DocumentAnalyzer.Core.Models.Reports;

namespace DocumentAnalyzer.Core.Abstractions;

public sealed record ReportSummary(string FileName, DateTime CreatedLocal, string Title, Constants.AnalysisMode Mode);

/// <summary>Where finished reports are kept. The file-based implementation writes JSON files; the UI history and downloads use this interface only.</summary>
public interface IReportStore
{
    /// <summary>Saves the report and returns its identifier (the file name).</summary>
    Task<string> SaveAsync(AnalysisReport report, CancellationToken cancellationToken = default);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<ReportSummary>> ListAsync(int max = 10, CancellationToken cancellationToken = default);

    Task<AnalysisReport?> LoadAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>Opens the stored JSON for download. Returns null when the report does not exist.</summary>
    Task<Stream?> OpenReadAsync(string fileName, CancellationToken cancellationToken = default);
}
