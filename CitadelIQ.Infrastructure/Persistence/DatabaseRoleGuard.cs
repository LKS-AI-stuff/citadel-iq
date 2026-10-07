using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CitadelIQ.Infrastructure.Persistence;

/// <summary>
/// Superusers and BYPASSRLS roles silently ignore row-level security, which would void the database half of
/// workspace isolation. The API refuses to start when its runtime connection uses such a role.
/// </summary>
public static class DatabaseRoleGuard
{
    public static async Task EnsureRuntimeRoleEnforcesRowLevelSecurityAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CitadelIQDbContext>();

        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        string role;
        bool bypasses;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT current_user::text, (rolsuper OR rolbypassrls) FROM pg_roles WHERE rolname = current_user";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            role = reader.GetString(0);
            bypasses = reader.GetBoolean(1);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        if (bypasses)
        {
            throw new InvalidOperationException(
                $"The database role '{role}' used by ConnectionStrings:CitadelIQ is a superuser or has BYPASSRLS, so row-level " +
                "security would not apply. Connect the API as the restricted runtime role (citadeliq_app) and keep the owner " +
                "role for ConnectionStrings:CitadelIQMigrations only.");
        }
    }
}
