namespace DocumentAnalyzer.Core.Models.ModelResponses;

/// <summary>
/// Shape of the JSON the model returns for a requirements-vs-design comparison
/// (matches PromptBuilder.CompareOutputSchema). This is the raw model response, not the saved report:
/// AnalysisReportMapper.BuildCompareReport turns it into RequirementComparison rows.
/// </summary>
public sealed class DocumentComparisonModelResponse
{
    public List<RequirementComparisonModelRow> RequirementResults { get; set; } = [];
}

/// <summary>
/// One flat row per requirement, exactly the twelve fields in the schema, in generation order: the numbers are
/// copied out first, then the constraint is classified, compared and decided. Rows without a finding use
/// Severity = "None" and empty Explanation/Recommendation; the mapper converts those to null.
/// </summary>
public sealed class RequirementComparisonModelRow
{
    public string RequirementId { get; set; } = string.Empty;

    /// <summary>"yes" or "no". "no" means the requirement is purely subjective, so nothing can be compared; the mapper then forces not_assessable.</summary>
    public string ObjectivelyComparable { get; set; } = Constants.YesNo.Yes;

    public string RequirementNumbers { get; set; } = string.Empty;
    public string MatchedDesignId { get; set; } = string.Empty;
    public string DesignNumbers { get; set; } = string.Empty;
    public string ConstraintType { get; set; } = string.Empty;
    public string ComparisonReasoning { get; set; } = string.Empty;
    public string ValueComparison { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = Constants.Severities.None;
    public string Confidence { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}
