using System.Text.Encodings.Web;
using System.Text.Json;
using DocumentAnalyzer.Core;
using DocumentAnalyzer.Core.Abstractions;
using DocumentAnalyzer.Core.Models.Reports;
using Microsoft.Extensions.Options;

namespace DocumentAnalyzer.Infrastructure.Storage;

/// <summary>IReportStore that keeps one JSON file per report.</summary>
public sealed class FileReportStore(IOptions<StorageOptions> options) : IReportStore
{
    private const string AnalysisPrefix = "Analysis_";
    private const string ComparisonPrefix = "Comparison_";
    private const string TimestampFormat = "yyyyMMdd_HHmmssfff";

    // "report_" covers files saved by earlier versions, so existing reports still appear under Recent runs.
    private static readonly string[] KnownPrefixes = [AnalysisPrefix, ComparisonPrefix, "report_"];

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    private readonly StorageOptions storage = options.Value;

    public async Task<string> SaveAsync(AnalysisReport report, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(storage.OutputFolder);

        var prefix = report.RequirementComparisons is not null ? ComparisonPrefix : AnalysisPrefix;
        var fileName = $"{prefix}{DateTime.Now.ToString(TimestampFormat)}.json";

        await File.WriteAllTextAsync(Path.Combine(storage.OutputFolder, fileName), JsonSerializer.Serialize(report, WriteOptions), cancellationToken);

        return fileName;
    }

    public async Task<IReadOnlyList<ReportSummary>> ListAsync(int max = 10, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(storage.OutputFolder))
        {
            return [];
        }

        var result = new List<ReportSummary>();

        // The prefixes differ, so sort by the timestamp after the first underscore to keep newest first.
        var files = Directory.EnumerateFiles(storage.OutputFolder, "*.json")
            .Where(file => KnownPrefixes.Any(p => Path.GetFileName(file).StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(Stamp, StringComparer.Ordinal)
            .Take(max);

        foreach (var file in files)
        {
            var report = await TryReadAsync(file, cancellationToken);

            if (report is not null)
            {
                result.Add(Describe(file, report));
            }
        }

        return result;
    }

    public Task<AnalysisReport?> LoadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var path = ResolveExisting(fileName);

        return path is null ? Task.FromResult<AnalysisReport?>(null) : TryReadAsync(path, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var path = ResolveExisting(fileName);

        return Task.FromResult<Stream?>(path is null ? null : File.OpenRead(path));
    }

    /// <summary>Only plain file names inside the output folder are accepted, so a name can never escape it.</summary>
    private string? ResolveExisting(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
        {
            return null;
        }

        var path = Path.Combine(storage.OutputFolder, fileName);

        return File.Exists(path) ? path : null;
    }

    private static async Task<AnalysisReport?> TryReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return JsonSerializer.Deserialize<AnalysisReport>(await File.ReadAllTextAsync(path, cancellationToken), ReadOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    private static ReportSummary Describe(string file, AnalysisReport report)
    {
        var isComparison = report.RequirementComparisons is not null;

        var title = isComparison
            ? $"{report.RequirementsDocument} vs {report.TechnicalDesignDocument}"
            : report.IssuesIdentified?.FirstOrDefault()?.SourceDocument is { Length: > 0 } source
                ? source
                : "Requirement analysis";

        return new ReportSummary(
            Path.GetFileName(file)!,
            File.GetLastWriteTime(file),
            title,
            isComparison ? Constants.AnalysisMode.Comparison : Constants.AnalysisMode.RequirementAnalysis);
    }

    private static string Stamp(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);

        return name[(name.IndexOf('_') + 1)..];
    }
}
