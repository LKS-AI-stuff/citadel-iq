using Npgsql;
using NpgsqlTypes;

namespace CitadelIQ.Infrastructure.Persistence;

/// <summary>Typed parameters for raw SQL where a value may be null (an untyped null gives Postgres nothing to infer from).</summary>
internal static class SqlParameters
{
    public static NpgsqlParameter Timestamp(DateTimeOffset? value) =>
        new() { NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = value is { } v ? v.ToUniversalTime() : DBNull.Value };

    public static NpgsqlParameter Uuid(Guid? value) =>
        new() { NpgsqlDbType = NpgsqlDbType.Uuid, Value = value is { } v ? v : DBNull.Value };

    public static NpgsqlParameter Text(string? value) =>
        new() { NpgsqlDbType = NpgsqlDbType.Varchar, Value = value is null ? DBNull.Value : value };

    public static string? Truncate(string? value, int maxLength) =>
        value is null ? null : value.Length > maxLength ? value[..maxLength] : value;
}
