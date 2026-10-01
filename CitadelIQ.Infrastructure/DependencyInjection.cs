using CitadelIQ.Application.Interfaces;
using CitadelIQ.Infrastructure.AI;
using CitadelIQ.Infrastructure.Persistence;
using CitadelIQ.Infrastructure.Processing;
using CitadelIQ.Infrastructure.Storage;
using CitadelIQ.Infrastructure.TextExtraction;
using CitadelIQ.Infrastructure.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CitadelIQ.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // PostgreSQL + pgvector via EF Core. DbContext and repositories are scoped: every request —
        // and every background processing task, which gets its own DI scope — gets its own DbContext
        // (DbContext is not thread-safe).
        var connectionString = configuration.GetConnectionString("CitadelIQ");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ConnectionStrings:CitadelIQ' is not configured. " +
                "Set it via dotnet user-secrets or an environment variable.");
        }

        services.AddDbContext<CitadelIQDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

        services.AddScoped<IFolderRepository, FolderRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
        services.AddScoped<IEmbeddingRepository, EmbeddingRepository>();
        services.AddScoped<IVectorSearchRepository, VectorSearchRepository>();

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
