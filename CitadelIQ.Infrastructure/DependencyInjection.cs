using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using CitadelIQ.Infrastructure.AI;
using CitadelIQ.Infrastructure.Email;
using CitadelIQ.Infrastructure.Email.Hosting;
using CitadelIQ.Infrastructure.Persistence;
using CitadelIQ.Infrastructure.Processing;
using CitadelIQ.Infrastructure.Storage;
using CitadelIQ.Infrastructure.TextExtraction;
using CitadelIQ.Infrastructure.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

        services.AddEmailAgent(configuration, connectionString);

        return services;
    }

    /// <summary>
    /// The email questions agent. Repositories and the (testable) pipelines are always registered so the portal
    /// endpoints work; the MailKit mailbox/sender, the leader lock and the four hosted loops only when
    /// <c>EmailAgent:Mailbox:Host</c> is set — local development and tests run without a mailbox.
    /// </summary>
    public static IServiceCollection AddEmailAgent(this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        services.AddScoped<IInboundEmailRepository, InboundEmailRepository>();
        services.AddScoped<IOutboundEmailQueue, OutboundEmailQueue>();
        services.AddScoped<IAdminNotificationSchedule, AdminNotificationSchedule>();
        services.AddScoped<IEmailThreadRepository, EmailThreadRepository>();
        services.AddScoped<IEmailQuestionRepository, EmailQuestionRepository>();
        services.AddScoped<IEmailDraftRepository, EmailDraftRepository>();
        services.AddScoped<IOutboundEmailRepository, OutboundEmailRepository>();

        services.AddSingleton<MailboxSynchronizer>();
        services.AddSingleton<InboundEmailPipeline>();
        services.AddSingleton<OutboundEmailPipeline>();
        services.AddSingleton<EmailMaintenanceRunner>();

        services.AddOptions<MailboxOptions>().Bind(configuration.GetSection("EmailAgent:Mailbox")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<MailboxOptions>, MailboxOptionsValidator>();

        var mailbox = configuration.GetSection("EmailAgent:Mailbox").Get<MailboxOptions>() ?? new MailboxOptions();
        services.AddSingleton<IEmailAgentMonitor>(new EmailAgentMonitor(mailbox.IsConfigured));
        if (!mailbox.IsConfigured)
        {
            services.AddSingleton<DisabledMailbox>();
            services.AddSingleton<IMailbox>(sp => sp.GetRequiredService<DisabledMailbox>());
            services.AddSingleton<IMailSender>(sp => sp.GetRequiredService<DisabledMailbox>());
            return services;
        }

        if (mailbox.AuthMode == MailboxOptions.OAuth2Auth)
        {
            // The production token source depends on the mailbox host (design §13 Q1); register it before this call.
            services.TryAddSingleton<IMailAccessTokenSource, UnconfiguredMailAccessTokenSource>();
            services.AddSingleton<IMailCredentialProvider, OAuth2MailCredentialProvider>();
        }
        else
        {
            services.AddSingleton<IMailCredentialProvider, PasswordMailCredentialProvider>();
        }

        services.AddSingleton<ImapMailbox>();
        services.AddSingleton<IMailbox>(sp => sp.GetRequiredService<ImapMailbox>());
        services.AddSingleton<IMailSender, SmtpMailSender>();

        services.AddSingleton(new EmailAgentLeaderConnection(connectionString));
        services.AddSingleton<EmailAgentLeader>();
        services.AddHostedService(sp => sp.GetRequiredService<EmailAgentLeader>());
        services.AddHostedService<MailboxListenerService>();
        services.AddHostedService<InboundProcessorService>();
        services.AddHostedService<OutboundSenderService>();
        services.AddHostedService<EmailMaintenanceService>();
        return services;
    }
}
