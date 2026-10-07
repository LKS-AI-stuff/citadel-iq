using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Domain.Entities;

/// <summary>A user's place in a workspace. One per user (the table's primary key is <see cref="UserId"/>).</summary>
public class Membership
{
    public Guid UserId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public WorkspaceRole Role { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }

    private Membership(Guid userId, Guid workspaceId, WorkspaceRole role, DateTimeOffset joinedAtUtc)
    {
        UserId = userId;
        WorkspaceId = workspaceId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    public static Membership Create(Guid userId, Guid workspaceId, WorkspaceRole role) =>
        new(userId, workspaceId, role, DateTimeOffset.UtcNow);

    public void ChangeRole(WorkspaceRole role)
    {
        Role = role;
    }
}
