using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CitadelIQ.Infrastructure.Persistence;

internal static class PostgresErrors
{
    public static bool IsUniqueViolation(this DbUpdateException ex, string? constraintName = null) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && (constraintName is null || pg.ConstraintName == constraintName);
}
