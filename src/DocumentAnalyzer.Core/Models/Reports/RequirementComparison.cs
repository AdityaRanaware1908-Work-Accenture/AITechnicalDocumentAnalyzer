using System.Text.Json.Serialization;

namespace DocumentAnalyzer.Core.Models.Reports;

/// <summary>
/// One requirement compared against the technical design: a single flat record that holds the comparison working, the verdict and, when there is a finding, the finding details.
/// Fields marked nullable are null for rows without a finding (for example Status = satisfied) and are omitted from the saved JSON.
/// </summary>
public sealed class RequirementComparison
{
    // --- Which requirement and which design statement were compared ---------------------------------

    public string RequirementId { get; set; } = string.Empty;

    /// <summary>Exact requirement statement, looked up from the requirements document by code.</summary>
    public string RequirementStatement { get; set; } = string.Empty;

    public string MatchedDesignId { get; set; } = string.Empty;

    /// <summary>Exact design statement, looked up from the technical design document by code.</summary>
    public string DesignStatement { get; set; } = string.Empty;

    // --- Comparison working -------------------------------------------------------------------------

    public string ConstraintType { get; set; } = string.Empty;
    public string RequirementValue { get; set; } = string.Empty;
    public string DesignValue { get; set; } = string.Empty;
    public string ComparisonReasoning { get; set; } = string.Empty;
    public string ValueComparison { get; set; } = string.Empty;

    // --- Verdict ------------------------------------------------------------------------------------

    /// <summary>satisfied, contradicted, partially_covered, not_addressed, design_ambiguous or not_assessable.</summary>
    public string Status { get; set; } = string.Empty;

    public string Confidence { get; set; } = string.Empty;

    // --- Finding (null when the row has no finding) -------------------------------------------------

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IssueType { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Severity { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Explanation { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Recommendation { get; set; }
}
