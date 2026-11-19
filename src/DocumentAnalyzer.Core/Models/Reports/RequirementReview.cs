using System.Text.Json.Serialization;

namespace DocumentAnalyzer.Core.Models.Reports;

/// <summary>One requirement of the reviewed document, with or without issues. The issues themselves stay in IssuesIdentified.</summary>
public sealed class RequirementReview
{
    public string RequirementId { get; set; } = string.Empty;
    public string RequirementStatement { get; set; } = string.Empty;
    public int IssueCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HighestSeverity { get; set; }
}