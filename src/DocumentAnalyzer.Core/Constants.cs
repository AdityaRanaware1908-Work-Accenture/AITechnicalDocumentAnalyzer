namespace DocumentAnalyzer.Core;

/// <summary>
/// Single home for the analyzer's static data: allowed values, issue types, statuses and lookups.
/// Prompts, schemas, mappers and validators all read from here, so every value is defined exactly once.
/// Prompt wording (rubric text, examples) stays in PromptBuilder because it is content, not data.
/// </summary>
public static class Constants
{
    /// <summary>Issue types. The model returns these for single-document analysis; for comparisons code derives them from the row status.</summary>
    public static class IssueTypes
    {
        public const string None = "None";

        // Single-document quality review
        public const string VagueLanguage = "Vague_Language";
        public const string UntestableRequirement = "Untestable_Requirement";
        public const string AmbiguousStatement = "Ambiguous_Statement";
        public const string IncompleteRequirement = "Incomplete_Requirement";
        public const string InternalContradiction = "Internal_Contradiction";

        // Requirements vs. technical design comparison
        public const string RequirementDesignConflict = "Requirement_Design_Conflict";
        public const string MissingDesignCoverage = "Missing_Design_Coverage";
        public const string PartialDesignCoverage = "Partial_Design_Coverage";
        public const string AmbiguousDesign = "Ambiguous_Design";

        /// <summary>Allowed values for single-document analysis, in the decision order used by the prompt.</summary>
        public static readonly string[] ForAnalysisRows =
        [
            None,
            InternalContradiction,
            VagueLanguage,
            UntestableRequirement,
            AmbiguousStatement,
            IncompleteRequirement
        ];

        public static readonly string[] ForComparison =
        [
            RequirementDesignConflict,
            MissingDesignCoverage,
            PartialDesignCoverage,
            AmbiguousDesign
        ];
    }

    public static class Severities
    {
        public const string None = "None";
        public const string Low = "Low";
        public const string Medium = "Medium";
        public const string High = "High";
        public const string Critical = "Critical";

        /// <summary>Allowed values for a reported issue.</summary>
        public static readonly string[] ForIssues = [Low, Medium, High, Critical];

        /// <summary>Allowed values for a comparison row, where None means the row has no finding.</summary>
        public static readonly string[] ForComparisonRows = [None, Low, Medium, High, Critical];

        /// <summary>Most to least severe, for sorting and grouping.</summary>
        public static readonly string[] MostToLeastSevere = [Critical, High, Medium, Low];
    }

    public static class Confidences
    {
        public const string Low = "Low";
        public const string Medium = "Medium";
        public const string High = "High";

        public static readonly string[] All = [Low, Medium, High];
    }

    public static class YesNo
    {
        public const string Yes = "yes";
        public const string No = "no";

        public static readonly string[] All = [Yes, No];
    }

    public static class ConstraintTypes
    {
        public const string Maximum = "maximum";
        public const string Minimum = "minimum";
        public const string Exact = "exact";
        public const string None = "none";

        public static readonly string[] All = [Maximum, Minimum, Exact, None];
    }

    public static class ValueComparisons
    {
        public const string DesignCompatible = "Design_Compatible";
        public const string DesignIncompatible = "Design_Incompatible";
        public const string NotComparable = "Not_Comparable";
        public const string NoDesignStatement = "No_Design_Statement";
        public const string NotApplicable = "Not_Applicable";

        public static readonly string[] All =
        [
            DesignCompatible,
            DesignIncompatible,
            NotComparable,
            NoDesignStatement,
            NotApplicable
        ];
    }

    public static class ComparisonStatuses
    {
        public const string Satisfied = "satisfied";
        public const string Contradicted = "contradicted";
        public const string PartiallyCovered = "partially_covered";
        public const string NotAddressed = "not_addressed";
        public const string DesignAmbiguous = "design_ambiguous";
        public const string NotAssessable = "not_assessable";

        public static readonly string[] All =
        [
            Satisfied,
            Contradicted,
            PartiallyCovered,
            NotAddressed,
            DesignAmbiguous,
            NotAssessable
        ];

        /// <summary>Statuses that carry a finding, mapped to the issue type code assigns to them.</summary>
        public static readonly IReadOnlyDictionary<string, string> IssueTypeByStatus = new Dictionary<string, string>
        {
            [Contradicted] = IssueTypes.RequirementDesignConflict,
            [NotAddressed] = IssueTypes.MissingDesignCoverage,
            [PartiallyCovered] = IssueTypes.PartialDesignCoverage,
            [DesignAmbiguous] = IssueTypes.AmbiguousDesign
        };

        /// <summary>Statuses that only make sense when a design statement was matched to the requirement.</summary>
        public static readonly string[] RequiringDesignStatement = [Satisfied, Contradicted, PartiallyCovered, DesignAmbiguous];

        /// <summary>Statuses that are consistent with each valueComparison value (used by EvidenceValidator).</summary>
        public static readonly IReadOnlyDictionary<string, string[]> AllowedByValueComparison = new Dictionary<string, string[]>
        {
            [ValueComparisons.DesignIncompatible] = [Contradicted],
            [ValueComparisons.NoDesignStatement] = [NotAddressed],
            [ValueComparisons.NotComparable] = [PartiallyCovered, DesignAmbiguous],
            [ValueComparisons.DesignCompatible] = [Satisfied, PartiallyCovered, DesignAmbiguous],
            [ValueComparisons.NotApplicable] = [NotAssessable]
        };
    }

    public enum DocumentKind
    {
        Requirements,
        Design
    }

    public enum AnalysisMode
    {
        RequirementAnalysis,
        Comparison
    }

    /// <summary>Order matters: the run progress screen compares stages with &lt; and ==.</summary>
    public enum RunStage
    {
        LoadingDocuments,
        WaitingForModel,
        Validating,
        Saving
    }

    public enum RunStatus
    {
        Running,
        Succeeded,
        Failed,
        Cancelled
    }

    public static class AnalysisGuards
    {
        /// <summary>A Vague_Language finding must quote at least one of these. Extend the list as needed.</summary>
        public static readonly string[] VagueTerms =
        [
            "quick", "quickly", "fast", "faster", "slow", "many", "few", "several", "large", "small", "big", "high", "low",
        "easy", "easily", "simple", "user-friendly", "intuitive", "robust", "appropriate", "adequate", "sufficient",
        "reasonable", "flexible", "scalable", "efficient", "efficiently", "responsive", "seamless", "smooth", "modern",
        "minimal", "optimal", "timely", "promptly", "acceptable", "good", "better", "best", "reliable", "secure", "etc"
        ];

        /// <summary>Words ignored when checking that two requirements are about the same subject.</summary>
        public static readonly string[] StopWords =
        [
            "shall", "system", "must", "should", "will", "would", "with", "that", "this", "from", "into", "been", "have",
        "than", "when", "each", "every", "same", "their", "them", "they", "there", "which", "where", "upon", "also", "only", "such", "other"
        ];
    }
}
