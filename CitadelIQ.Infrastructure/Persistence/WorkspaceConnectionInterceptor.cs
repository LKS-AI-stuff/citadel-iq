using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CitadelIQ.Infrastructure.Persistence;

/// <summary>
/// Stamps every connection EF opens with the scope's workspace (<c>app.workspace_id</c>), which the row-level
/// security policies compare against. The value is always overwritten — with '' when there is no workspace — so a
/// pooled connection can never carry another request's workspace. Also enables pgvector's iterative HNSW scan
/// (0.8+), so a workspace filter on a large shared table still yields up to LIMIT results.
/// </summary>
public sealed class WorkspaceConnectionInterceptor : DbConnectionInterceptor
{
    private const string Sql =
        "SELECT set_config('app.workspace_id', @workspace_id, false), set_config('hnsw.iterative_scan', 'strict_order', false)";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection, eventData);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection, eventData);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbCommand CreateCommand(DbConnection connection, ConnectionEndEventData eventData)
    {
        var workspaceId = (eventData.Context as CitadelIQDbContext)?.CurrentWorkspaceId;

        var command = connection.CreateCommand();
        command.CommandText = Sql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "workspace_id";
        parameter.Value = workspaceId?.ToString() ?? "";
        command.Parameters.Add(parameter);
        return command;
    }
}
