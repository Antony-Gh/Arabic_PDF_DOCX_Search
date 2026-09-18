using ArabicDocumentSearch.Core;
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
    private readonly StandardAnalyzer _analyzer = new(Version);
    private readonly IndexWriter _writer;
    private readonly object _gate = new();

    public LuceneTextIndex(string indexPath)
    {
        System.IO.Directory.CreateDirectory(indexPath);
        _directory = FSDirectory.Open(indexPath);
        _writer = new IndexWriter(_directory, new IndexWriterConfig(Version, _analyzer));
    }

    public Task ReplaceAsync(DocumentMetadata document, IReadOnlyList<PageContent> pages, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _writer.DeleteDocuments(new Term("documentId", document.Id));
            foreach (var page in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = new Document
                {
                    new StringField("documentId", document.Id, Field.Store.YES),
                    new StringField("path", document.FullPath, Field.Store.YES),
                    new StringField("extension", document.Extension, Field.Store.YES),
                    new StringField("page", page.PageNumber.ToString(), Field.Store.YES),
                    new StringField("location", page.Location, Field.Store.YES),
                    new TextField("fileName", document.FileName, Field.Store.YES),
                    new TextField("text", page.NormalizedText, Field.Store.NO),
                    new StoredField("originalText", page.OriginalText)
                };
                _writer.AddDocument(item);
            }
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string documentId, CancellationToken cancellationToken = default)
    {
        lock (_gate) _writer.DeleteDocuments(new Term("documentId", documentId));
        return Task.CompletedTask;
    }

    public IReadOnlyList<SearchResult> Search(string query, int maxResults = 500)
    {
        lock (_gate)
        {
            _writer.Commit();
            using var reader = DirectoryReader.Open(_directory);
            var searcher = new IndexSearcher(reader);
            var parser = new MultiFieldQueryParser(Version, ["text", "fileName"], _analyzer);
            Query parsed;
            try { parsed = parser.Parse(QueryParserBase.Escape(query)); }
            catch (ParseException) { return []; }
            var hits = searcher.Search(parsed, maxResults).ScoreDocs;
            return hits.Select(hit =>
            {
                var document = searcher.Doc(hit.Doc);
                var original = document.Get("originalText") ?? string.Empty;
                return new SearchResult(document.Get("documentId") ?? string.Empty, document.Get("fileName") ?? string.Empty, document.Get("path") ?? string.Empty, document.Get("extension") ?? string.Empty, int.TryParse(document.Get("page"), out var page) && page > 0 ? page : null, document.Get("location") ?? "Document", MakeSnippet(original, query), hit.Score);
            }).ToList();
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
