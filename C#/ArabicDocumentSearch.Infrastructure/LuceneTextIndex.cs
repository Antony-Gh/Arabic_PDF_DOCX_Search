using ArabicDocumentSearch.Core;
using Microsoft.Extensions.Logging;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace ArabicDocumentSearch.Infrastructure;

public sealed class LuceneTextIndex : ITextIndex, IDisposable
{
    private static readonly LuceneVersion Version = LuceneVersion.LUCENE_48;
    private readonly FSDirectory _directory;
    private readonly StandardAnalyzer _analyzer;
    private readonly IndexWriter _writer;
    private readonly object _gate = new();
    private readonly IArabicTextNormalizer _normalizer;
    private readonly ILogger<LuceneTextIndex> _logger;
    private readonly SearchDiagnostics _diagnostics;
    private readonly string _indexPath;

    public LuceneTextIndex(IArabicTextNormalizer normalizer, ILogger<LuceneTextIndex> logger, SearchDiagnostics diagnostics, string indexPath)
    {
        _normalizer = normalizer;
        _logger = logger;
        _diagnostics = diagnostics;
        _indexPath = indexPath;
        _directory = OpenDirectory(indexPath);
        _analyzer = new StandardAnalyzer(Version);
        _writer = new IndexWriter(_directory, new IndexWriterConfig(Version, _analyzer));
    }

    private static FSDirectory OpenDirectory(string path)
    {
        System.IO.Directory.CreateDirectory(path);
        return FSDirectory.Open(path);
    }

    public Task ReplaceAsync(DocumentMetadata document, IReadOnlyList<PageContent> pages, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = pages.Select(page => new Document
        {
            new StringField("documentId", document.Id, Field.Store.YES),
            new StringField("path", document.FullPath, Field.Store.YES),
            new StringField("extension", document.Extension, Field.Store.YES),
            new StringField("page", page.PageNumber.ToString(), Field.Store.YES),
            new StringField("location", page.Location, Field.Store.YES),
            new TextField("fileName", document.FileName, Field.Store.YES),
            new TextField("text", page.NormalizedText, Field.Store.NO),
            new StoredField("originalText", page.OriginalText)
        }).ToList();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _writer.DeleteDocuments(new Term("documentId", document.Id));
            foreach (var item in items) _writer.AddDocument(item);
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string documentId, CancellationToken cancellationToken = default)
    {
        lock (_gate) _writer.DeleteDocuments(new Term("documentId", documentId));
        return Task.CompletedTask;
    }

    public Task<SearchOutcome> SearchAsync(string query, int maxResults, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => SearchCore(query, maxResults, cancellationToken), cancellationToken);
    }

    private SearchOutcome SearchCore(string query, int maxResults, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var normalizedQuery = _normalizer.Normalize(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var emptyOutcome = new SearchOutcome(query, false, false, [], DateTime.UtcNow - started, "Enter a search term.", "EmptyQuery");
            _diagnostics.Write(emptyOutcome, _indexPath);
            return emptyOutcome;
        }

        maxResults = Math.Clamp(maxResults, 1, 5000);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!DirectoryReader.IndexExists(_directory))
            {
                var noIndexOutcome = new SearchOutcome(query, true, false, [], DateTime.UtcNow - started);
                _diagnostics.Write(noIndexOutcome, _indexPath);
                return noIndexOutcome;
            }
            using var reader = DirectoryReader.Open(_directory);
            var searcher = new IndexSearcher(reader);
            var parser = new MultiFieldQueryParser(Version, ["text", "fileName"], _analyzer);
            var parsed = parser.Parse(QueryParserBase.Escape(normalizedQuery));
            var hits = searcher.Search(parsed, maxResults).ScoreDocs;
            var results = hits.Select(hit =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = searcher.Doc(hit.Doc);
                var original = document.Get("originalText") ?? string.Empty;
                return new SearchResult(document.Get("documentId") ?? string.Empty, document.Get("fileName") ?? string.Empty, document.Get("path") ?? string.Empty, document.Get("extension") ?? string.Empty, int.TryParse(document.Get("page"), out var page) && page > 0 ? page : null, document.Get("location") ?? "Document", MakeSnippet(original, normalizedQuery), hit.Score);
            }).ToList();
            var successOutcome = new SearchOutcome(query, true, false, results, DateTime.UtcNow - started);
            _diagnostics.Write(successOutcome, _indexPath);
            return successOutcome;
        }
        catch (OperationCanceledException)
        {
            var cancelledOutcome = new SearchOutcome(query, false, true, [], DateTime.UtcNow - started, "Search cancelled.", "OperationCanceledException");
            _diagnostics.Write(cancelledOutcome, _indexPath);
            return cancelledOutcome;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Search failed for query length {QueryLength}", query.Length);
            var failureOutcome = new SearchOutcome(query, false, false, [], DateTime.UtcNow - started, exception.Message, exception.GetType().Name, exception.ToString());
            _diagnostics.Write(failureOutcome, _indexPath);
            return failureOutcome;
        }
    }

    public void Commit() { lock (_gate) _writer.Commit(); }

    private static string MakeSnippet(string text, string query)
    {
        if (text.Length <= 360) return text;
        var position = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        var start = Math.Max(0, position < 0 ? 0 : position - 140);
        return (start > 0 ? "... " : string.Empty) + text.Substring(start, Math.Min(360, text.Length - start)).Trim() + (start + 360 < text.Length ? " ..." : string.Empty);
    }

    public void Dispose() { _writer.Dispose(); _analyzer.Dispose(); _directory.Dispose(); }
}
