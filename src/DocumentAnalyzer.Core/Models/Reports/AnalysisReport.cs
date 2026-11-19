using System.Text.Json.Serialization;

namespace DocumentAnalyzer.Core.Models.Reports;

/// <summary>
/// The saved report. Each run fills exactly one of the two lists: IssuesIdentified for a single-document analysis,
/// RequirementComparisons for a requirements-vs-design comparison. The list that does not apply stays null and is omitted from the JSON.
/// </summary>
public sealed class AnalysisReport
{
    public string Summary { get; set; } = string.Empty;

    // Comparison runs only: the document names are constant for the whole run, so they live here once.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequirementsDocument { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TechnicalDesignDocument { get; set; }

    // Single-document analysis: every requirement of the document, with or without issues.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RequirementReview>? RequirementReviews { get; set; }

    // Single-document analysis.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DocumentIssue>? IssuesIdentified { get; set; }

    // Requirements vs. technical design comparison.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RequirementComparison>? RequirementComparisons { get; set; }

    public ProcessingMetrics? ProcessingMetrics { get; set; }
}
