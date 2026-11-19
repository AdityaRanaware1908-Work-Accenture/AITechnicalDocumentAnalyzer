using DocumentAnalyzer.Core;
using DocumentAnalyzer.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace DocumentAnalyzer.Infrastructure.Storage;

/// <summary>IDocumentCatalog over folders of .txt and .md files.</summary>
public sealed class FileDocumentCatalog(IOptions<StorageOptions> options) : IDocumentCatalog
{
    private static readonly string[] Extensions = [".txt", ".md"];

    private readonly StorageOptions storage = options.Value;

    public Task<IReadOnlyList<DocumentInfo>> ListAsync(Constants.DocumentKind kind, CancellationToken cancellationToken = default)
    {
        var folder = FolderFor(kind);

        IReadOnlyList<DocumentInfo> result = !Directory.Exists(folder)
            ? []
            : Directory.EnumerateFiles(folder)
                .Where(file => Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .Select(file => Path.GetFileName(file)!)
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(name => new DocumentInfo(name, kind))
                .ToList();

        return Task.FromResult(result);
    }

    public async Task<string> ReadAsync(Constants.DocumentKind kind, string name, CancellationToken cancellationToken = default)
    {
        // Only plain file names inside the folder are accepted, so a name can never escape it.
        if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name)
        {
            throw new ArgumentException("Invalid document name.", nameof(name));
        }

        var path = Path.Combine(FolderFor(kind), name);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Document '{name}' was not found.");
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    private string FolderFor(Constants.DocumentKind kind) =>
        Path.Combine(storage.InputFolder, kind == Constants.DocumentKind.Requirements ? storage.RequirementsSubfolder : storage.DesignsSubfolder);
}
