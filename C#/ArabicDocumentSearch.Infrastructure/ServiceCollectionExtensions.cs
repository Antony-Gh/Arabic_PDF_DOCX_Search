using ArabicDocumentSearch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArabicDocumentSearch.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArabicSearchInfrastructure(this IServiceCollection services, string dataRoot)
    {
        var database = Path.Combine(dataRoot, "database", "app.db");
        var index = Path.Combine(dataRoot, "index");
        var searchLog = Path.Combine(dataRoot, "logs", "search-diagnostics.jsonl");
        services.AddSingleton<IArabicTextNormalizer, ArabicTextNormalizer>();
        services.AddSingleton<FileDiscovery>();
        services.AddSingleton<IDocumentExtractor, PdfTextExtractor>();
        services.AddSingleton<IDocumentExtractor, DocxTextExtractor>();
        services.AddSingleton<IMetadataStore>(_ => new SqliteMetadataStore(database));
        services.AddSingleton(new SearchDiagnostics(searchLog));
        services.AddSingleton<ITextIndex>(provider => new LuceneTextIndex(
            provider.GetRequiredService<IArabicTextNormalizer>(),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LuceneTextIndex>>(),
            provider.GetRequiredService<SearchDiagnostics>(),
            index));
        services.AddSingleton<IDocumentIndexer, DocumentIndexer>();
        services.AddLogging();
        return services;
    }
}
