namespace CitadelIQ.Application.Accounts;

/// <summary>
/// The workspace the current scope works in. Infrastructure reads it to filter every content query (EF query
/// filters) and to stamp each database connection for row-level security. No workspace ⇒ no content rows at all.
/// </summary>
public interface IWorkspaceContext
{
    Guid? WorkspaceId { get; }

    /// <summary>Enters a workspace for the rest of the scope. Entering the same one again is a no-op; entering a
    /// different one throws, so a scope can never switch tenants half-way through.</summary>
    void Enter(Guid workspaceId);
}
