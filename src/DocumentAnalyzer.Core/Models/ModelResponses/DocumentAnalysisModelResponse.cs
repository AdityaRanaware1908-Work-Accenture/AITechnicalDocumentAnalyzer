namespace DocumentAnalyzer.Core.Models.ModelResponses;

/// <summary>Shape of the JSON the model returns for a single-document analysis: one row per requirement.</summary>
public sealed class DocumentAnalysisModelResponse
{
    public List<RequirementCheckRow> RequirementChecks { get; set; } = [];
}

public sealed class RequirementCheckRow
{
    public string RequirementId { get; set; } = string.Empty;
    public string ConflictsWith { get; set; } = string.Empty;
    public string TestableAsWritten { get; set; } = Constants.YesNo.Yes;
    public string ProblemText { get; set; } = string.Empty;
    public string IssueType { get; set; } = Constants.IssueTypes.None;
    public string Severity { get; set; } = Constants.Severities.None;
    public string Confidence { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}