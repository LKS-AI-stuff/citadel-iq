using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Folders;
using CitadelIQ.Application.Mapping;
using CitadelIQ.Application.Options;
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
        return services;
    }
}
