using CitadelIQ.Domain.Enums;
using CitadelIQ.Domain.Exceptions;

namespace CitadelIQ.Domain.Entities;

/// <summary>
/// The unit of data ownership and isolation: every folder, document and chunk belongs to exactly one workspace.
/// An Individual workspace has one member; an Organization workspace has many and a join code.
/// </summary>
public class Workspace
{
    public const int MaxNameLength = 100;

    public Guid Id { get; private set; }
    public WorkspaceKind Kind { get; private set; }
    public string Name { get; private set; }
    /// <summary>Normalized (no separators) join code; organizations only.</summary>
    public string? JoinCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Workspace(Guid id, WorkspaceKind kind, string name, string? joinCode, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Kind = kind;
        Name = name;
        JoinCode = joinCode;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Named after the user; a name too short for an organization falls back rather than failing onboarding.</summary>
    public static Workspace CreateIndividual(string name) =>
        new(Guid.NewGuid(), WorkspaceKind.Individual,
            (name?.Trim().Length ?? 0) < 2 ? "My workspace" : ValidateName(name!, "Workspace name"), null, DateTimeOffset.UtcNow);

    public static Workspace CreateOrganization(string name, string joinCode) =>
        new(Guid.NewGuid(), WorkspaceKind.Organization, ValidateName(name, "Organization name"), joinCode, DateTimeOffset.UtcNow);

    public bool IsOrganization => Kind == WorkspaceKind.Organization;

    public void RegenerateJoinCode(string joinCode)
    {
        if (!IsOrganization)
        {
            throw new DomainException("Only organizations have a join code.");
        }

        JoinCode = joinCode;
    }

    private static string ValidateName(string name, string label)
    {
        var trimmed = name?.Trim() ?? "";

        if (trimmed.Length < 2)
        {
            throw new DomainException($"{label} must be at least 2 characters.");
        }

        // Individual workspaces are named after the user's display name, which may be longer; cut rather than fail.
        return trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
    }
}
