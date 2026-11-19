namespace DocumentAnalyzer.Core.Abstractions;

/// <summary>A document the user can pick as input. Name is the file name, for example "ecommerce-orders.requirements.txt".</summary>
public sealed record DocumentInfo(string Name, Constants.DocumentKind Kind)
{
    /// <summary>
    /// Scenario the document belongs to: the part of the name before the first dot ("ecommerce-orders").
    /// A requirements document and a design with the same scenario are a matching pair.
    /// </summary>
    public string Scenario => Name.Split('.')[0];
}

/// <summary>Where input documents come from. The file-based implementation reads the samples folder; a database or blob store can replace it.</summary>
public interface IDocumentCatalog
{
    Task<IReadOnlyList<DocumentInfo>> ListAsync(Constants.DocumentKind kind, CancellationToken cancellationToken = default);

    Task<string> ReadAsync(Constants.DocumentKind kind, string name, CancellationToken cancellationToken = default);
}
