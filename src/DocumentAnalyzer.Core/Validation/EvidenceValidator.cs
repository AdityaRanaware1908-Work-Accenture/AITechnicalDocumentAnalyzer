using DocumentAnalyzer.Core.Models.Reports;
using System.Text.RegularExpressions;

namespace DocumentAnalyzer.Core.Validation;

/// <summary>
/// Post-processing checks on a finished report. Returns warnings; it never changes the report.
/// A run fills exactly one list on the report, so the matching set of checks is applied:
///   IssuesIdentified        single-document findings (severity, source and related evidence)
///   RequirementComparisons  flat comparison rows (statements, allowed values, status consistency, coverage)
/// </summary>
public static class EvidenceValidator
{
    // First token of a line when it looks like an identifier, e.g. REQ-001 or DESIGN-002.
    private static readonly Regex RequirementIdPattern = new(
        @"^\s*([A-Za-z][A-Za-z0-9_]*-\d+)\b",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public static List<string> Validate(AnalysisReport analysisReport, IReadOnlyDictionary<string, string> documentDict)
    {
        var warnings = new List<string>();

        if (analysisReport.IssuesIdentified is not null)
        {
            ValidateIssues(analysisReport.IssuesIdentified, documentDict, warnings);
        }

        if (analysisReport.RequirementComparisons is not null)
        {
            ValidateRequirementComparisons(analysisReport, documentDict, warnings);
        }

        return warnings;
    }

    // ------------------------------------------------------------------
    // Single-document findings
    // ------------------------------------------------------------------

    private static void ValidateIssues(IReadOnlyList<DocumentIssue> issues, IReadOnlyDictionary<string, string> documentDict, List<string> warnings)
    {
        for (var i = 0; i < issues.Count; i++)
        {
            var issue = issues[i];
            var label = $"Finding {i + 1}";

            if (!Constants.Severities.ForIssues.Contains(issue.Severity, StringComparer.OrdinalIgnoreCase))
            {
                warnings.Add($"{label}: unrecognized severity.");
            }

            if (string.IsNullOrWhiteSpace(issue.SourceDocument) || !documentDict.TryGetValue(issue.SourceDocument, out var sourceText))
            {
                warnings.Add($"{label}: source document was not recognized.");
            }
            else if (string.IsNullOrWhiteSpace(issue.Evidence))
            {
                warnings.Add($"{label}: evidence is empty.");
            }
            else if (!ContainsNormalized(sourceText, issue.Evidence))
            {
                warnings.Add($"{label}: source evidence was not found even after whitespace normalization.");
            }

            if (!string.IsNullOrWhiteSpace(issue.RelatedDocument))
            {
                if (!documentDict.TryGetValue(issue.RelatedDocument, out var relatedText))
                {
                    warnings.Add($"{label}: related document was not recognized.");
                }
                else if (string.IsNullOrWhiteSpace(issue.RelatedEvidence))
                {
                    warnings.Add($"{label}: related evidence is empty.");
                }
                else if (!ContainsNormalized(relatedText, issue.RelatedEvidence))
                {
                    warnings.Add($"{label}: related evidence was not found even after whitespace normalization.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(issue.RelatedEvidence))
            {
                warnings.Add($"{label}: related evidence has no related document.");
            }
        }
    }

    // ------------------------------------------------------------------
    // Requirements vs. technical design comparison
    // ------------------------------------------------------------------

    private static void ValidateRequirementComparisons(AnalysisReport report, IReadOnlyDictionary<string, string> documentDict, List<string> warnings)
    {
        var comparisons = report.RequirementComparisons!;

        string? requirementsText = null;
        string? designText = null;

        if (string.IsNullOrWhiteSpace(report.RequirementsDocument) || !documentDict.TryGetValue(report.RequirementsDocument, out requirementsText))
        {
            warnings.Add("Requirements document was not recognized.");
        }

        if (string.IsNullOrWhiteSpace(report.TechnicalDesignDocument) || !documentDict.TryGetValue(report.TechnicalDesignDocument, out designText))
        {
            warnings.Add("Technical design document was not recognized.");
        }

        if (requirementsText is not null)
        {
            ValidateRequirementCoverage(comparisons, requirementsText, warnings);
        }

        foreach (var row in comparisons)
        {
            var label = $"Requirement {(string.IsNullOrWhiteSpace(row.RequirementId) ? "(no id)" : row.RequirementId)}";

            ValidateStatements(label, row, requirementsText, designText, warnings);
            ValidateAllowedValues(label, row, warnings);
            ValidateFindingFields(label, row, warnings);
        }
    }

    /// <summary>Every identifier found in the requirements document should have exactly one comparison row.</summary>
    private static void ValidateRequirementCoverage(IReadOnlyList<RequirementComparison> comparisons, string requirementsText, List<string> warnings)
    {
        var expectedIds = RequirementIdPattern.Matches(requirementsText)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (expectedIds.Count == 0)
        {
            return; // the document has no identifiers, so coverage cannot be checked
        }

        var rowCounts = comparisons
            .GroupBy(c => c.RequirementId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var id in expectedIds)
        {
            if (!rowCounts.TryGetValue(id, out var count))
            {
                warnings.Add($"Requirement {id}: no comparison row was returned for it.");
            }
            else if (count > 1)
            {
                warnings.Add($"Requirement {id}: {count} comparison rows were returned for it.");
            }
        }
    }

    private static void ValidateStatements(string label, RequirementComparison row, string? requirementsText, string? designText, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(row.RequirementStatement))
        {
            warnings.Add($"{label}: requirement statement was not found in the requirements document.");
        }
        else if (requirementsText is not null && !ContainsNormalized(requirementsText, row.RequirementStatement))
        {
            warnings.Add($"{label}: requirement statement was not found even after whitespace normalization.");
        }

        if (string.IsNullOrWhiteSpace(row.MatchedDesignId))
        {
            if (!string.IsNullOrWhiteSpace(row.DesignStatement))
            {
                warnings.Add($"{label}: a design statement is set but no design identifier was matched.");
            }

            if (Constants.ComparisonStatuses.RequiringDesignStatement.Contains(row.Status))
            {
                warnings.Add($"{label}: status '{row.Status}' requires a matched design statement but none was matched.");
            }
        }
        else if (string.IsNullOrWhiteSpace(row.DesignStatement))
        {
            warnings.Add($"{label}: design statement '{row.MatchedDesignId}' was not found in the technical design document.");
        }
        else if (designText is not null && !ContainsNormalized(designText, row.DesignStatement))
        {
            warnings.Add($"{label}: design statement was not found even after whitespace normalization.");
        }
    }

    private static void ValidateAllowedValues(string label, RequirementComparison row, List<string> warnings)
    {
        var statusKnown = Constants.ComparisonStatuses.All.Contains(row.Status);
        var comparisonKnown = Constants.ValueComparisons.All.Contains(row.ValueComparison);

        if (!statusKnown)
        {
            warnings.Add($"{label}: unrecognized status '{row.Status}'.");
        }

        if (!comparisonKnown)
        {
            warnings.Add($"{label}: unrecognized value comparison '{row.ValueComparison}'.");
        }

        if (!Constants.ConstraintTypes.All.Contains(row.ConstraintType))
        {
            warnings.Add($"{label}: unrecognized constraint type '{row.ConstraintType}'.");
        }

        if (!Constants.Confidences.All.Contains(row.Confidence))
        {
            warnings.Add($"{label}: unrecognized confidence '{row.Confidence}'.");
        }

        // Numbers in the requirement must have been extracted into the working fields (a common small-model slip).
        var noConstraint = row.ConstraintType == Constants.ConstraintTypes.None;

        if (noConstraint && string.IsNullOrWhiteSpace(row.RequirementValue) && StripLeadingId(row.RequirementStatement).Any(char.IsDigit))
        {
            warnings.Add($"{label}: the requirement contains numbers but constraint type is 'none' and no values were extracted.");
        }
        else if (!noConstraint && string.IsNullOrWhiteSpace(row.RequirementValue))
        {
            warnings.Add($"{label}: constraint type is '{row.ConstraintType}' but no requirement value was extracted.");
        }

        if (statusKnown && comparisonKnown &&
            Constants.ComparisonStatuses.AllowedByValueComparison.TryGetValue(row.ValueComparison, out var allowedStatuses) &&
            !allowedStatuses.Contains(row.Status))
        {
            warnings.Add($"{label}: status '{row.Status}' is inconsistent with value comparison '{row.ValueComparison}'.");
        }
    }

    /// <summary>Finding fields must be present exactly when the status carries a finding.</summary>
    private static void ValidateFindingFields(string label, RequirementComparison row, List<string> warnings)
    {
        if (Constants.ComparisonStatuses.IssueTypeByStatus.ContainsKey(row.Status))
        {
            if (string.IsNullOrWhiteSpace(row.IssueType))
            {
                warnings.Add($"{label}: status '{row.Status}' is a finding but has no issue type.");
            }

            if (string.IsNullOrWhiteSpace(row.Severity))
            {
                warnings.Add($"{label}: status '{row.Status}' is a finding but has no severity.");
            }
            else if (!Constants.Severities.ForIssues.Contains(row.Severity, StringComparer.OrdinalIgnoreCase))
            {
                warnings.Add($"{label}: unrecognized severity '{row.Severity}'.");
            }

            if (string.IsNullOrWhiteSpace(row.Explanation))
            {
                warnings.Add($"{label}: status '{row.Status}' is a finding but has no explanation.");
            }

            if (string.IsNullOrWhiteSpace(row.Recommendation))
            {
                warnings.Add($"{label}: status '{row.Status}' is a finding but has no recommendation.");
            }
        }
        else if (Constants.ComparisonStatuses.All.Contains(row.Status) &&
                 (row.IssueType is not null || row.Severity is not null || row.Explanation is not null || row.Recommendation is not null))
        {
            warnings.Add($"{label}: status '{row.Status}' has no finding but finding fields are set.");
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Removes a leading identifier such as "REQ-005:" so its digits are not mistaken for requirement numbers.</summary>
    private static string StripLeadingId(string statement) =>
        Regex.Replace(statement, @"^\s*[A-Za-z][A-Za-z0-9_]*-\d+\s*:?\s*", string.Empty);

    private static bool ContainsNormalized(string text, string evidence)
    {
        var normalizedText = NormalizeWhitespace(text);
        var normalizedEvidence = NormalizeWhitespace(evidence);

        return normalizedText.Contains(normalizedEvidence, StringComparison.Ordinal);
    }

    private static string NormalizeWhitespace(string value)
    {
        return Regex.Replace(value, @"\s+", " ").Trim();
    }
}
