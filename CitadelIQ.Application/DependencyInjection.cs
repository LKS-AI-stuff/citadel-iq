using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Folders;
using CitadelIQ.Application.Mapping;
using CitadelIQ.Application.Search;
using Microsoft.Extensions.DependencyInjection;

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
        return services;
    }
}
