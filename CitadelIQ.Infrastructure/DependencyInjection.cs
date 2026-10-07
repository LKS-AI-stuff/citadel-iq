using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
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

        // The interceptor is stateless (it reads the workspace from the DbContext it is called for), so one instance
        // serves every context.
        var workspaceInterceptor = new WorkspaceConnectionInterceptor();
        services.AddDbContext<CitadelIQDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector())
                .AddInterceptors(workspaceInterceptor));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IJoinRequestRepository, JoinRequestRepository>();
        services.AddScoped<IFolderRepository, FolderRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
        services.AddScoped<IEmbeddingRepository, EmbeddingRepository>();
        services.AddScoped<IVectorSearchRepository, VectorSearchRepository>();

        // Raw file bytes: local disk (App_Data/, not RAM and not bin/ — see PLAN.md §3) or Azure Blob Storage,
        // chosen by Storage:Provider. Unknown values fail fast at startup.
        var storageProvider = configuration["Storage:Provider"] ?? StorageOptions.LocalDiskProvider;
        switch (storageProvider)
        {
            case StorageOptions.LocalDiskProvider:
                services.AddSingleton<IDocumentStorage, LocalDiskDocumentStorage>();
                break;
            case StorageOptions.AzureBlobProvider:
                services.AddSingleton<AzureBlobDocumentStorage>();
                services.AddSingleton<IDocumentStorage>(sp => sp.GetRequiredService<AzureBlobDocumentStorage>());
                services.AddSingleton<IStorageProbe>(sp => sp.GetRequiredService<AzureBlobDocumentStorage>());
                break;
            default:
                throw new InvalidOperationException(
                    $"Storage:Provider '{storageProvider}' is not supported. Use '{StorageOptions.LocalDiskProvider}' or '{StorageOptions.AzureBlobProvider}'.");
        }

        services.AddSingleton<ITextExtractor, PdfTextExtractor>();
        services.AddSingleton<ITextExtractor, DocxTextExtractor>();
        services.AddSingleton<ITextExtractor, XlsxTextExtractor>();
        services.AddSingleton<ITextExtractor, PlainTextExtractor>();
        services.AddSingleton<ITextExtractionService, TextExtractionService>();

        services.AddSingleton<IFileValidator, FileValidator>();
        services.AddSingleton<IOpenAIEmbeddingService, OpenAIEmbeddingService>();
        services.AddSingleton<IChatCompletionService, OpenAIChatCompletionService>();
        services.AddSingleton<IDocumentProcessingDispatcher, BackgroundDocumentProcessingDispatcher>();

        return services;
    }
}
