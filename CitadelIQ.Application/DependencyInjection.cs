using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Folders;
using CitadelIQ.Application.Mapping;
using CitadelIQ.Application.Options;
using CitadelIQ.Application.Organizations;
using CitadelIQ.Application.Rag;
using CitadelIQ.Application.Settings;
using CitadelIQ.Application.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

        // One scoped object is both the current user and the workspace context for the scope.
        services.AddScoped<CurrentUserContext>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUserContext>());
        services.AddScoped<IWorkspaceContext>(sp => sp.GetRequiredService<CurrentUserContext>());
        services.AddScoped<UploaderLookup>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IOrganizationAdminService, OrganizationAdminService>();

        services.AddScoped<FolderPathBuilder>();
        services.AddScoped<IFolderService, FolderService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddSingleton<ITextChunker, TextChunker>();
        services.AddScoped<IAnswerService, AnswerService>();
        services.AddSingleton<AskRequestValidator>();
        services.AddScoped<QuestionRewriter>();
        services.AddSingleton<PromptBuilder>();
        services.AddSingleton<ContextSelector>();
        services.AddSingleton<AnswerStreamProcessor>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IValidateOptions<RagOptions>, RagOptionsValidator>();
        services.AddSingleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>();
        services.AddSingleton<IValidateOptions<AuthenticationOptions>, AuthenticationOptionsValidator>();
        return services;
    }
}
