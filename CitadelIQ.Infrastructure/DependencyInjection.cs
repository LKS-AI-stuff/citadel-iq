using CitadelIQ.Application.Interfaces;
using CitadelIQ.Infrastructure.AI;
using CitadelIQ.Infrastructure.Persistence;
using CitadelIQ.Infrastructure.Processing;
using CitadelIQ.Infrastructure.Storage;
using CitadelIQ.Infrastructure.TextExtraction;
using CitadelIQ.Infrastructure.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace CitadelIQ.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Global, app-wide in-memory stores (singletons) — see PLAN.md §3 for the rationale and
        // the future swap-in path (Postgres/pgvector) behind these same interfaces.
        services.AddSingleton<IFolderRepository, InMemoryFolderRepository>();
        services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();
        services.AddSingleton<IDocumentChunkRepository, InMemoryDocumentChunkRepository>();
        services.AddSingleton<IEmbeddingRepository, InMemoryEmbeddingRepository>();

        // Raw file bytes on local disk (App_Data/), not RAM and not bin/ — see PLAN.md §3.
        services.AddSingleton<IDocumentStorage, LocalDiskDocumentStorage>();

        services.AddSingleton<ITextExtractor, PdfTextExtractor>();
        services.AddSingleton<ITextExtractor, DocxTextExtractor>();
        services.AddSingleton<ITextExtractor, XlsxTextExtractor>();
        services.AddSingleton<ITextExtractor, PlainTextExtractor>();
        services.AddSingleton<ITextExtractionService, TextExtractionService>();

        services.AddSingleton<IFileValidator, FileValidator>();
        services.AddSingleton<IOpenAIEmbeddingService, OpenAIEmbeddingService>();
        services.AddSingleton<IDocumentProcessingDispatcher, BackgroundDocumentProcessingDispatcher>();

        return services;
    }
}
