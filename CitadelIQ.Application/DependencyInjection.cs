using CitadelIQ.Application.Folders;
using CitadelIQ.Application.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace CitadelIQ.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());
        services.AddScoped<IFolderService, FolderService>();
        return services;
    }
}
