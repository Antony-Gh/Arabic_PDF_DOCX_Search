using ArabicDocumentSearch.Core;
using Microsoft.Extensions.Logging;

namespace ArabicDocumentSearch.Infrastructure;

public sealed class DocumentIndexer(FileDiscovery discovery, IEnumerable<IDocumentExtractor> extractors, IMetadataStore metadata, ITextIndex index, ILogger<DocumentIndexer> logger) : IDocumentIndexer
{
    public async Task<IndexProgress> IndexAsync(string rootFolder, IndexOptions options, IProgress<IndexProgress>? progress, CancellationToken cancellationToken)
    {
        await metadata.InitializeAsync(cancellationToken);
        var started = DateTime.UtcNow;
        var documents = discovery.Discover(rootFolder, options.ExcludedFolders ?? [], cancellationToken).ToList();
        var result = new IndexProgress(documents.Count, 0, 0, 0, 0, 0, null, TimeSpan.Zero);
        progress?.Report(result);
        var processed = 0; var indexed = 0; var skipped = 0; var errors = 0;
        var recentSamples = new System.Collections.Concurrent.ConcurrentQueue<(DateTime Timestamp, int Processed)>();
        await Parallel.ForEachAsync(documents, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2), CancellationToken = cancellationToken }, async (document, token) =>
        {
            try
            {
                var existing = await metadata.GetAsync(document.Id, token);
                if (options.Incremental && existing is not null && existing.FileSize == document.FileSize && existing.LastWriteTimeUtc == document.LastWriteTimeUtc)
                {
                    Interlocked.Increment(ref skipped);
                }
                else
                {
                    var extractor = extractors.First(item => item.CanExtract(document.Extension));
                    var pages = extractor.Extract(document, token);
                    var metadataItem = new DocumentMetadata(document.Id, document.FullPath, document.FileName, document.Extension, document.FileSize, document.LastWriteTimeUtc, "Indexed", DateTime.UtcNow, pages.Count, pages.Any(page => !string.IsNullOrWhiteSpace(page.OriginalText)), pages.Any(page => string.IsNullOrWhiteSpace(page.OriginalText)), false);
                    await index.ReplaceAsync(metadataItem, pages, token);
                    await metadata.UpsertAsync(metadataItem, token);
                    Interlocked.Increment(ref indexed);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                Interlocked.Increment(ref errors);
                logger.LogError(exception, "Failed to index {Path}", document.FullPath);
                await metadata.UpsertAsync(new DocumentMetadata(document.Id, document.FullPath, document.FileName, document.Extension, document.FileSize, document.LastWriteTimeUtc, "Failed", DateTime.UtcNow, ErrorMessage: exception.Message), token);
            }
            var current = Interlocked.Increment(ref processed);
            var now = DateTime.UtcNow;
            recentSamples.Enqueue((now, current));
            while (recentSamples.TryPeek(out var sample) && (now - sample.Timestamp).TotalSeconds > 30) recentSamples.TryDequeue(out _);
            var hasRecentSample = recentSamples.TryPeek(out var recent);
            var recentTimestamp = hasRecentSample ? recent.Timestamp : now;
            var recentProcessed = hasRecentSample ? recent.Processed : current;
            var recentSeconds = Math.Max(0.001, (now - recentTimestamp).TotalSeconds);
            var recentSpeed = (current - recentProcessed) / recentSeconds;
            var overallSpeed = current / Math.Max(0.001, (now - started).TotalSeconds);
            var speed = recentSamples.Count >= 2 ? (recentSpeed * 0.7) + (overallSpeed * 0.3) : overallSpeed;
            var remaining = speed > 0 && documents.Count > current ? TimeSpan.FromSeconds((documents.Count - current) / speed) : (TimeSpan?)null;
            progress?.Report(new IndexProgress(documents.Count, current, indexed, skipped, errors, 0, document.FileName, now - started, started, remaining, remaining is null ? null : now + remaining.Value, speed));
        });
        var missing = await metadata.RemoveMissingAsync(documents.Select(document => document.Id).ToHashSet(), cancellationToken);
        foreach (var documentId in missing) await index.RemoveAsync(documentId, cancellationToken);
        index.Commit();
        return new IndexProgress(documents.Count, processed, indexed, skipped, errors, 0, null, DateTime.UtcNow - started);
    }
}
