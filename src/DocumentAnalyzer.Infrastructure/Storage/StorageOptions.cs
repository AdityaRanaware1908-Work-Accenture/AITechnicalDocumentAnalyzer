namespace DocumentAnalyzer.Infrastructure.Storage;

/// <summary>Bound from the "Storage" configuration section. Relative folders are resolved against the web project folder at startup.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Folder that holds the requirement and design documents, split into the two subfolders below.</summary>
    public string InputFolder { get; set; } = "../../samples";

    public string RequirementsSubfolder { get; set; } = "requirements";

    public string DesignsSubfolder { get; set; } = "designs";

    /// <summary>Folder where finished reports are written as JSON.</summary>
    public string OutputFolder { get; set; } = "../../output/reports";
}
