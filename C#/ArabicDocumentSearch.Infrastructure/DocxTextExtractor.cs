using ArabicDocumentSearch.Core;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ArabicDocumentSearch.Infrastructure;

public sealed class DocxTextExtractor(IArabicTextNormalizer normalizer) : IDocumentExtractor
{
    public bool CanExtract(string extension) => extension.Equals(".docx", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<PageContent> Extract(DiscoveredDocument document, CancellationToken cancellationToken)
    {
        using var package = WordprocessingDocument.Open(document.FullPath, false);
        var body = package.MainDocumentPart?.Document.Body;
        if (body is null) return [];
        var sections = new List<string>();
        foreach (var element in body.Elements())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = element.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(text)) sections.Add(text);
        }
        var original = string.Join(Environment.NewLine, sections);
        return [new PageContent(0, original, normalizer.Normalize(original), "Document")];
    }
}
