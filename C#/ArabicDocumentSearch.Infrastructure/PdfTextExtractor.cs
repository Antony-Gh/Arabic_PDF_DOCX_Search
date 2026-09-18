using ArabicDocumentSearch.Core;
using UglyToad.PdfPig;

namespace ArabicDocumentSearch.Infrastructure;

public sealed class PdfTextExtractor(IArabicTextNormalizer normalizer) : IDocumentExtractor
{
    public bool CanExtract(string extension) => extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<PageContent> Extract(DiscoveredDocument document, CancellationToken cancellationToken)
    {
        var pages = new List<PageContent>();
        using var pdf = PdfDocument.Open(document.FullPath);
        var pageNumber = 0;
        foreach (var page in pdf.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            pageNumber++;
            var text = page.Text ?? string.Empty;
            pages.Add(new PageContent(pageNumber, text, normalizer.Normalize(text), $"Page {pageNumber}"));
        }
        return pages;
    }
}
