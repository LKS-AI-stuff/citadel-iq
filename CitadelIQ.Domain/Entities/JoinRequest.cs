using CitadelIQ.Domain.Enums;
using CitadelIQ.Domain.Exceptions;

namespace CitadelIQ.Domain.Entities;

/// <summary>A newly signed-up user's request to join an organization, decided by one of its Owners or Admins.</summary>
public class JoinRequest
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public JoinRequestStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }
    public Guid? DecidedByUserId { get; private set; }

    private JoinRequest(Guid id, Guid userId, Guid workspaceId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId = userId;
        WorkspaceId = workspaceId;
        Status = JoinRequestStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public static JoinRequest Create(Guid userId, Guid workspaceId) =>
        new(Guid.NewGuid(), userId, workspaceId, DateTimeOffset.UtcNow);

    public void Approve(Guid decidedByUserId) => Decide(JoinRequestStatus.Approved, decidedByUserId);

    public void Reject(Guid decidedByUserId) => Decide(JoinRequestStatus.Rejected, decidedByUserId);

    public void Cancel() => Decide(JoinRequestStatus.Cancelled, null);

    private void Decide(JoinRequestStatus status, Guid? decidedByUserId)
    {
        if (Status != JoinRequestStatus.Pending)
        {
            throw new DomainException("This join request has already been decided.");
        }

        Status = status;
        DecidedAtUtc = DateTimeOffset.UtcNow;
        DecidedByUserId = decidedByUserId;
    }
}
