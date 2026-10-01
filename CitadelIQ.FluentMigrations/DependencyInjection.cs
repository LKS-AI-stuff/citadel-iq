using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;

namespace CitadelIQ.FluentMigrations;

public static class DependencyInjection
{
    public static IServiceCollection AddFluentMigrations(this IServiceCollection services, string connectionString) =>
        services.AddFluentMigratorCore()
            .ConfigureRunner(runner => runner
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(DependencyInjection).Assembly).For.Migrations())
            .AddLogging(logging => logging.AddFluentMigratorConsole());

    /// <summary>Applies all pending migrations (idempotent; tracked in the VersionInfo table).</summary>
    public static void ApplyDatabaseMigrations(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
    }
}
