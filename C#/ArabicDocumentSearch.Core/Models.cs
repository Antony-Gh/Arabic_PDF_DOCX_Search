namespace ArabicDocumentSearch.Core;

public sealed record DocumentMetadata(
    string Id,
    string FullPath,
    string FileName,
    string Extension,
    long FileSize,
    DateTime LastWriteTimeUtc,
    string Status,
    DateTime? IndexedAtUtc = null,
    int PageCount = 0,
    bool HasText = false,
    bool RequiresOcr = false,
    bool OcrCompleted = false,
    string? ErrorMessage = null);

public sealed record PageContent(int PageNumber, string OriginalText, string NormalizedText, string Location);

public sealed record DiscoveredDocument(
    string FullPath,
    string FileName,
    string Extension,
    long FileSize,
    DateTime LastWriteTimeUtc,
    string Id);

public sealed record SearchResult(
    string DocumentId,
    string FileName,
    string FullPath,
    string Extension,
    int? PageNumber,
    string Location,
    string Snippet,
    float Score);

public sealed record IndexProgress(
    int Total,
    int Processed,
    int Indexed,
    int Skipped,
    int Errors,
    int OcrPages,
    string? CurrentFile,
    TimeSpan Elapsed);

public sealed record IndexOptions(
    bool Incremental = true,
    IReadOnlyCollection<string>? ExcludedFolders = null,
    int MaxResults = 500);

public interface IArabicTextNormalizer
{
    string Normalize(string text);
}

public interface IDocumentExtractor
{
    bool CanExtract(string extension);
    IReadOnlyList<PageContent> Extract(DiscoveredDocument document, CancellationToken cancellationToken);
}

public interface IMetadataStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<DocumentMetadata?> GetAsync(string documentId, CancellationToken cancellationToken = default);
    Task UpsertAsync(DocumentMetadata metadata, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> RemoveMissingAsync(IReadOnlySet<string> discoveredIds, CancellationToken cancellationToken = default);
}

public interface ITextIndex
{
    Task ReplaceAsync(DocumentMetadata document, IReadOnlyList<PageContent> pages, CancellationToken cancellationToken = default);
    Task RemoveAsync(string documentId, CancellationToken cancellationToken = default);
    IReadOnlyList<SearchResult> Search(string query, int maxResults = 500);
    void Commit();
}

public interface IDocumentIndexer
{
    Task<IndexProgress> IndexAsync(string rootFolder, IndexOptions options, IProgress<IndexProgress>? progress, CancellationToken cancellationToken);
}
