using DocumentAnalyzer.Core.Models.ModelResponses;
using DocumentAnalyzer.Core.Models.Reports;
using System.Text.RegularExpressions;

namespace DocumentAnalyzer.Core.Analysis;

/// <summary>
/// Builds the saved AnalysisReport from the raw model responses. Code supplies what the model should not
/// generate: document names, verbatim statements (looked up by identifier), the issue type (derived from the
/// status) and the summary (built from counts), so these can never disagree with the rows.
/// Assumes one statement per line with identifiers such as REQ-001 / DESIGN-001.
/// </summary>
public static class AnalysisReportMapper
{
    // ------------------------------------------------------------------
    // Requirements vs. technical design comparison
    // ------------------------------------------------------------------

    /// <summary>Maps the model's flat rows one-to-one onto RequirementComparison rows.</summary>
    public static AnalysisReport BuildCompareReport(DocumentComparisonModelResponse model, string requirementsDocName, string requirementsText, string designDocName, string designText)
    {
        var requirementIndex = new StatementIndex(requirementsText);
        var designIndex = new StatementIndex(designText);

        var comparisons = model.RequirementResults
            .Select(row => ToRequirementComparison(row, requirementIndex, designIndex))
            .ToList();

        return new AnalysisReport
        {
            Summary = BuildCompareSummary(comparisons),
            RequirementsDocument = requirementsDocName,
            TechnicalDesignDocument = designDocName,
            RequirementComparisons = comparisons
        };
    }

    private static RequirementComparison ToRequirementComparison(RequirementComparisonModelRow row, StatementIndex requirementIndex, StatementIndex designIndex)
    {
        // The gate is authoritative: a requirement the model judged purely subjective cannot have a design match
        // or a finding, whatever else the row says. It is covered by the single-document analysis instead.
        if (string.Equals(row.ObjectivelyComparable, Constants.YesNo.No, StringComparison.OrdinalIgnoreCase))
        {
            return new RequirementComparison
            {
                RequirementId = row.RequirementId,
                RequirementStatement = requirementIndex.Find(row.RequirementId),
                ConstraintType = Constants.ConstraintTypes.None,
                ComparisonReasoning = row.ComparisonReasoning,
                ValueComparison = Constants.ValueComparisons.NotApplicable,
                Status = Constants.ComparisonStatuses.NotAssessable,
                Confidence = row.Confidence
            };
        }

        return new RequirementComparison
        {
            RequirementId = row.RequirementId,
            RequirementStatement = requirementIndex.Find(row.RequirementId),
            MatchedDesignId = row.MatchedDesignId,
            DesignStatement = designIndex.Find(row.MatchedDesignId),
            ConstraintType = row.ConstraintType,
            RequirementValue = row.RequirementNumbers,
            DesignValue = row.DesignNumbers,
            ComparisonReasoning = row.ComparisonReasoning,
            ValueComparison = row.ValueComparison,
            Status = row.Status,
            Confidence = row.Confidence,

            // Null for statuses without a finding (satisfied, not_assessable) and for unknown statuses.
            IssueType = Constants.ComparisonStatuses.IssueTypeByStatus.GetValueOrDefault(row.Status),

            // Model convention for "no finding" is severity None and empty text; the report uses null instead.
            // A value the model did write is kept as is, so EvidenceValidator can flag an inconsistent row.
            Severity = NullIfBlankOrNone(row.Severity),
            Explanation = NullIfBlank(row.Explanation),

            // Conflict recommendations are deterministic, so code supplies them (consistent wording, fewer generated tokens).
            Recommendation = row.Status == Constants.ComparisonStatuses.Contradicted
                ? BuildConflictRecommendation(row)
                : NullIfBlank(row.Recommendation)
        };
    }

    private static string BuildConflictRecommendation(RequirementComparisonModelRow row)
    {
        var values = !string.IsNullOrWhiteSpace(row.RequirementNumbers) && !string.IsNullOrWhiteSpace(row.DesignNumbers)
            ? $" ({row.RequirementNumbers} vs {row.DesignNumbers})"
            : string.Empty;

        return $"Resolve the conflict between {row.RequirementId} and {row.MatchedDesignId}{values} with the responsible stakeholders " +
               "and align the requirement and design statements.";
    }

    private static string BuildCompareSummary(IReadOnlyList<RequirementComparison> comparisons)
    {
        int Count(string status) => comparisons.Count(c => c.Status == status);

        var satisfied = Count(Constants.ComparisonStatuses.Satisfied);
        var conflicts = Count(Constants.ComparisonStatuses.Contradicted);
        var partial = Count(Constants.ComparisonStatuses.PartiallyCovered);
        var missing = Count(Constants.ComparisonStatuses.NotAddressed);
        var unclear = Count(Constants.ComparisonStatuses.DesignAmbiguous);
        var notAssessable = Count(Constants.ComparisonStatuses.NotAssessable);

        var summary =
            $"{comparisons.Count} requirements assessed: {satisfied} satisfied, {conflicts} in conflict with the design, " +
            $"{partial} partially covered, {missing} not addressed, {unclear} with unclear design wording, " +
            $"{notAssessable} not assessable.";

        var conflictIds = string.Join(", ", comparisons
            .Where(c => c.Status == Constants.ComparisonStatuses.Contradicted)
            .Select(c => c.RequirementId));

        return conflictIds.Length > 0 ? $"{summary} Conflicts: {conflictIds}." : summary;
    }

    // ------------------------------------------------------------------
    // Single-document analysis
    // ------------------------------------------------------------------

    public static AnalysisReport BuildAnalyzeReport(DocumentAnalysisModelResponse model, string documentName, string documentText)
    {
        foreach (var issue in model.Issues)
        {
            // The document name is known to the caller; only an internal contradiction has a related statement.
            issue.SourceDocument = documentName;
            issue.RelatedDocument = string.IsNullOrWhiteSpace(issue.RelatedEvidence) ? string.Empty : documentName;
        }

        var reviews = new StatementIndex(documentText).All
            .Select(statement => ToReview(statement, model.Issues))
            .ToList();

        return new AnalysisReport
        {
            Summary = BuildAnalyzeSummary(model.Issues, reviews),
            RequirementReviews = reviews,
            IssuesIdentified = model.Issues
        };
    }


    private static string BuildAnalyzeSummary(IReadOnlyList<DocumentIssue> issues)
    {
        if (issues.Count == 0)
        {
            return "No material issues were found.";
        }

        var bySeverity = string.Join(", ", issues
            .GroupBy(i => i.Severity, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => SeverityRank(g.Key))
            .Select(g => $"{g.Count()} {g.Key}"));

        var byType = string.Join(", ", issues
            .GroupBy(i => i.IssueType)
            .Select(g => $"{g.Key} ({g.Count()})"));

        return $"{issues.Count} issues found ({bySeverity}). Types: {byType}.";
    }

    private static int SeverityRank(string severity)
    {
        var order = Constants.Severities.MostToLeastSevere;
        var index = Array.FindIndex(order, s => s.Equals(severity, StringComparison.OrdinalIgnoreCase));

        return index < 0 ? order.Length : index;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static RequirementReview ToReview((string Id, string Statement) statement, IReadOnlyList<DocumentIssue> issues)
    {
        var own = issues.Where(i => Concerns(i, statement.Id)).ToList();

        return new RequirementReview
        {
            RequirementId = statement.Id,
            RequirementStatement = statement.Statement,
            IssueCount = own.Count,
            HighestSeverity = own.Count == 0 ? null : own.OrderBy(i => SeverityRank(i.Severity)).First().Severity
        };
    }

    ///// <summary>True when the issue is about the requirement: its own id, or the id of the statement it conflicts with.</summary>
    //public static bool Concerns(DocumentIssue issue, string requirementId) =>
    //    string.Equals(issue.RequirementId.Trim(), requirementId, StringComparison.OrdinalIgnoreCase) ||
    //    string.Equals(StatementIndex.IdOf(issue.RelatedEvidence), requirementId, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the issue is about the requirement: its own id, or the id of the statement it conflicts with.</summary>
    public static bool Concerns(DocumentIssue issue, string requirementId)
    {
        if (string.Equals(issue.RequirementId.Trim(), requirementId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatementIndex.IdOf(issue.RelatedEvidence), requirementId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // A contradiction involves two requirements. A small model often leaves relatedEvidence empty and names the second
        // requirement only in the explanation, so for contradictions an id mentioned there counts as well.
        return string.Equals(issue.IssueType, Constants.IssueTypes.InternalContradiction, StringComparison.OrdinalIgnoreCase) &&
               Regex.IsMatch(issue.Explanation, $@"\b{Regex.Escape(requirementId)}\b", RegexOptions.IgnoreCase);
    }

    private static string BuildAnalyzeSummary(IReadOnlyList<DocumentIssue> issues, IReadOnlyList<RequirementReview> reviews)
    {
        var withIssues = reviews.Count(r => r.IssueCount > 0);
        var scope = reviews.Count > 0
            ? $"{reviews.Count} requirements reviewed: {withIssues} with issues, {reviews.Count - withIssues} without. "
            : string.Empty;

        if (issues.Count == 0)
        {
            return scope + "No material issues were found.";
        }

        var bySeverity = string.Join(", ", issues
            .GroupBy(i => i.Severity, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => SeverityRank(g.Key))
            .Select(g => $"{g.Count()} {g.Key}"));

        var byType = string.Join(", ", issues
            .GroupBy(i => i.IssueType)
            .Select(g => $"{g.Key} ({g.Count()})"));

        return $"{scope}{issues.Count} issues found ({bySeverity}). Types: {byType}.";
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NullIfBlankOrNone(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Equals(Constants.Severities.None, StringComparison.OrdinalIgnoreCase)
            ? null
            : value.Trim();

    /// <summary>Looks up the exact source line for an identifier such as REQ-003 or DESIGN-001.</summary>
    private sealed class StatementIndex
    {
        private static readonly Regex LeadingId = new(@"^([A-Za-z][A-Za-z0-9_]*-\d+)\b", RegexOptions.Compiled);

        private readonly Dictionary<string, string> byId = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<(string Id, string Statement)> ordered = [];

        public IReadOnlyList<(string Id, string Statement)> All => ordered;

        public static string IdOf(string? text)
        {
            var match = LeadingId.Match(Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim());

            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        public StatementIndex(string text)
        {
            foreach (var rawLine in text.Split('\n'))
            {
                var line = Regex.Replace(rawLine, @"\s+", " ").Trim();
                var match = LeadingId.Match(line);

                if (match.Success && byId.TryAdd(match.Groups[1].Value, line))
                {
                    ordered.Add((match.Groups[1].Value, line));
                }
            }
        }

        // An unknown identifier returns an empty string; EvidenceValidator should report it as a missing statement.
        public string Find(string id) => byId.TryGetValue(id.Trim(), out var line) ? line : string.Empty;
    }
}
