using ArabicDocumentSearch.Core;
using ArabicDocumentSearch.Infrastructure;
using Lucene.Net.Documents;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArabicDocumentSearch.Tests;

public sealed class SearchTests
{
    [Fact]
    public async Task Search_NormalizesArabicDigitsAndReturnsIndexedPage()
    {
        using var temp = new TemporaryDirectory();
        var normalizer = new ArabicTextNormalizer();
        using var index = new LuceneTextIndex(normalizer, NullLogger<LuceneTextIndex>.Instance, new SearchDiagnostics(Path.Combine(temp.Path, "search.jsonl")), Path.Combine(temp.Path, "index"));
        var metadata = new DocumentMetadata("doc-1", Path.Combine(temp.Path, "judgment.pdf"), "judgment.pdf", ".pdf", 10, DateTime.UtcNow, "Indexed");
        await index.ReplaceAsync(metadata, [new PageContent(7, "القضية رقم ٦٨٧١ لسنة ٢٠٢٥", normalizer.Normalize("القضية رقم ٦٨٧١ لسنة ٢٠٢٥"), "Page 7")]);
        index.Commit();

        var outcome = await index.SearchAsync("6871 لسنة 2025", 50);

        Assert.True(outcome.Succeeded);
        Assert.Single(outcome.Results);
        Assert.Equal(7, outcome.Results[0].PageNumber);
    }

    [Fact]
    public async Task Search_EmptyQueryIsFailureAndMissingIndexIsSuccessfulNoResults()
    {
        using var temp = new TemporaryDirectory();
        var normalizer = new ArabicTextNormalizer();
        using var index = new LuceneTextIndex(normalizer, NullLogger<LuceneTextIndex>.Instance, new SearchDiagnostics(Path.Combine(temp.Path, "search.jsonl")), Path.Combine(temp.Path, "index"));

        var empty = await index.SearchAsync("   ", 50);
        var missing = await index.SearchAsync("6871", 50);

        Assert.False(empty.Succeeded);
        Assert.Equal("EmptyQuery", empty.ErrorType);
        Assert.True(missing.Succeeded);
        Assert.Empty(missing.Results);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}