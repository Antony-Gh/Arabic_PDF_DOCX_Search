using ArabicDocumentSearch.Core;
using ArabicDocumentSearch.Infrastructure;

namespace ArabicDocumentSearch.Tests;

public sealed class CoreTests
{
    [Fact]
    public void ArabicNormalizer_NormalizesLettersDigitsMarksAndWhitespace()
    {
        var normalizer = new ArabicTextNormalizer();

        var actual = normalizer.Normalize("  أَلْحُكْمُ   ١٢٣   المقيدة  ");

        Assert.Equal("الحكم 123 المقيده", actual);
    }

    [Fact]
    public void FileDiscovery_SkipsExcludedFolders()
    {
        using var temp = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "included"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "excluded"));
        File.WriteAllText(Path.Combine(temp.Path, "included", "keep.docx"), "");
        File.WriteAllText(Path.Combine(temp.Path, "excluded", "skip.pdf"), "");

        var discovery = new FileDiscovery();
        var documents = discovery.Discover(temp.Path, [Path.Combine(temp.Path, "excluded")], CancellationToken.None).ToList();

        Assert.Single(documents);
        Assert.EndsWith("keep.docx", documents[0].FullPath, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}