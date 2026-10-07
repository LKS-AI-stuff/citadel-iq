using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Application.Interfaces;

public record MemberRecord(Membership Membership, UserAccount User);

/// <summary>
/// A workspace's memberships and pending join requests, loaded under a row lock on the workspace. Changes made to
/// these (tracked) entities, plus <see cref="Add"/>/<see cref="Remove"/>, are saved together when the locked
/// callback returns.
/// </summary>
public sealed class WorkspaceMembershipSet(Workspace workspace, List<Membership> memberships, List<JoinRequest> pendingJoinRequests)
{
    private readonly List<Membership> _added = [];
    private readonly List<Membership> _removed = [];

    public Workspace Workspace { get; } = workspace;
    public IReadOnlyList<Membership> Memberships => memberships;
    public IReadOnlyList<JoinRequest> PendingJoinRequests { get; } = pendingJoinRequests;

    public IReadOnlyList<Membership> Added => _added;
    public IReadOnlyList<Membership> Removed => _removed;

    public Membership? Find(Guid userId) => memberships.FirstOrDefault(m => m.UserId == userId);

    public void Add(Membership membership)
    {
        memberships.Add(membership);
        _added.Add(membership);
    }

    public void Remove(Membership membership)
    {
        memberships.Remove(membership);
        _removed.Add(membership);
    }
}

public interface IMembershipRepository
{
    Task<Membership?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemberRecord>> ListByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>Adds a membership; a user who already has one gets a <c>ValidationException</c>.</summary>
    Task AddAsync(Membership membership, CancellationToken cancellationToken = default);

    /// <summary>
    /// The only way to change an existing workspace's memberships: opens a transaction, locks the workspace row
    /// (<c>SELECT … FOR UPDATE</c>) so concurrent changes are serialized, loads the set, runs
    /// <paramref name="change"/>, saves everything tracked and commits. Throwing from <paramref name="change"/> rolls
    /// it all back. Throws <c>NotFoundException</c> if the workspace does not exist.
    /// </summary>
    Task RunLockedAsync(Guid workspaceId, Func<WorkspaceMembershipSet, Task> change, CancellationToken cancellationToken = default);
}
