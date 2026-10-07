namespace CitadelIQ.Domain.Enums;

/// <summary>Ordered so that <c>role &gt;= WorkspaceRole.Admin</c> reads naturally: each role includes the ones below it.</summary>
public enum WorkspaceRole
{
    Member = 0,
    Admin = 1,
    Owner = 2
}
