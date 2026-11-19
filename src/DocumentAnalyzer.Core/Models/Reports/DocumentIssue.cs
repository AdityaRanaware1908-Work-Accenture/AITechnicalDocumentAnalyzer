namespace DocumentAnalyzer.Core.Models.Reports;

/// <summary>One quality issue found by a single-document analysis.</summary>
public sealed class DocumentIssue
{
    public string RequirementId { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string SourceDocument { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string RelatedDocument { get; set; } = string.Empty;
    public string RelatedEvidence { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string Confidence { get; set; } = string.Empty;
}
