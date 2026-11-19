using DocumentAnalyzer.Core.Models.Reports;

namespace DocumentAnalyzer.Web.Runs;

/// <summary>What the results panel shows: the run that just finished, or a report opened from Recent runs.</summary>
/// <param name="WarningsKnown">False for saved reports, because validation warnings are not stored in the report file.</param>
public sealed record ReportView(string Title, AnalysisReport Report, IReadOnlyList<string> Warnings, string? FileName, bool WarningsKnown);
