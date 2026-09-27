using CitadelIQ.Application.Interfaces;
using CitadelIQ.Infrastructure.Persistence;
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
        return services;
    }
}
