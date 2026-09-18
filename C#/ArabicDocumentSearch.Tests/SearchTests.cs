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

        var outcome = await index.SearchAsync("6871 لسنة 2025", SearchScope.All, 50);

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

        var empty = await index.SearchAsync("   ", SearchScope.All, 50);
        var missing = await index.SearchAsync("6871", SearchScope.All, 50);

        Assert.False(empty.Succeeded);
        Assert.Equal("EmptyQuery", empty.ErrorType);
        Assert.True(missing.Succeeded);
        Assert.Empty(missing.Results);
    }

    [Fact]
    public async Task Search_FindsEnglishMixedTextAndFilename()
    {
        using var temp = new TemporaryDirectory();
        var normalizer = new ArabicTextNormalizer();
        using var index = new LuceneTextIndex(normalizer, NullLogger<LuceneTextIndex>.Instance, new SearchDiagnostics(Path.Combine(temp.Path, "search.jsonl")), Path.Combine(temp.Path, "index"));
        var metadata = new DocumentMetadata("doc-2", Path.Combine(temp.Path, "6871_2025_Economic_Court.pdf"), "6871_2025_Economic_Court.pdf", ".pdf", 10, DateTime.UtcNow, "Indexed");
        var text = "This judgment was issued by the Economic Court. القضية ٦٨٧١";
        await index.ReplaceAsync(metadata, [new PageContent(1, text, normalizer.Normalize(text), "Page 1")]);
        index.Commit();

        var english = await index.SearchAsync("economic", SearchScope.DocumentText, 50);
        var arabic = await index.SearchAsync("القضية", SearchScope.DocumentText, 50);
        var number = await index.SearchAsync("6871", SearchScope.DocumentText, 50);
        var mixed = await index.SearchAsync("القضية 6871", SearchScope.DocumentText, 50);
        var filename = await index.SearchAsync("6871", SearchScope.FileName, 50);

        Assert.Single(english.Results);
        Assert.Single(arabic.Results);
        Assert.Single(number.Results);
        Assert.True(mixed.Results.Count == 1, $"Mixed result count: {mixed.Results.Count}, error: {mixed.ErrorMessage}");
        Assert.True(filename.Results.Count == 1, $"Filename result count: {filename.Results.Count}, error: {filename.ErrorMessage}");
        Assert.Equal("Filename", filename.Results[0].MatchKind);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}