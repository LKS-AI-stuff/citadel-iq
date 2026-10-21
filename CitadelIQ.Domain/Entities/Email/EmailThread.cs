using CitadelIQ.Domain.Exceptions;

namespace CitadelIQ.Domain.Entities;

/// <summary>
/// One email conversation between a workspace member and the support address. Follow-ups are answered with the
/// thread's own records as history; the thread caps automatic replies and switches to drafts once an admin replied.
/// </summary>
public class EmailThread
{
    public const int MaxSubjectLength = 300;

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    /// <summary>The asker. Only their own later emails continue the thread.</summary>
    public Guid UserId { get; private set; }
    public string Subject { get; private set; }
    public int AutomaticReplyCount { get; private set; }
    public bool AdminReplied { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastActivityAtUtc { get; private set; }

    private EmailThread(Guid id, Guid workspaceId, Guid userId, string subject, DateTimeOffset createdAtUtc)
    {
        Id = id;
        WorkspaceId = workspaceId;
        UserId = userId;
        Subject = subject;
        CreatedAtUtc = createdAtUtc;
        LastActivityAtUtc = createdAtUtc;
    }

    public static EmailThread Create(Guid workspaceId, Guid userId, string subject) =>
        new(Guid.NewGuid(), workspaceId, userId, EmailText.Truncate(subject, MaxSubjectLength), DateTimeOffset.UtcNow);

    public bool HasReachedLimit(int maxAutomaticReplies) => AutomaticReplyCount >= maxAutomaticReplies;

    /// <summary>A holding, automatic or no-confident reply was queued in this thread.</summary>
    public void RecordAutomaticReply(int maxAutomaticReplies)
    {
        if (HasReachedLimit(maxAutomaticReplies))
        {
            throw new DomainException("This conversation has reached its automatic reply limit.");
        }

        AutomaticReplyCount++;
        Touch();
    }

    public void MarkAdminReplied()
    {
        AdminReplied = true;
        Touch();
    }

    public void Touch() => LastActivityAtUtc = DateTimeOffset.UtcNow;
}
