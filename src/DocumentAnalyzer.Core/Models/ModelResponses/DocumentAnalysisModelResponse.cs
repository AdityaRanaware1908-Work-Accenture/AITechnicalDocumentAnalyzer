using DocumentAnalyzer.Core.Models.Reports;

namespace DocumentAnalyzer.Core.Models.ModelResponses;

/// <summary>
/// Shape of the JSON the model returns for a single-document analysis (matches PromptBuilder.AnalyzeOutputSchema).
/// This is the raw model response, not the saved report: the report mapper turns it into an AnalysisReport
/// and fills in what the model should not generate (document names and the summary).
/// Properties the model may still send but the report does not need (for example "summary" or "requirementId")
/// are ignored during deserialization.
/// </summary>
public sealed class DocumentAnalysisModelResponse
{
    public List<DocumentIssue> Issues { get; set; } = [];
}
