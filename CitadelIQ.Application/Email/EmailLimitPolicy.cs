using CitadelIQ.Application.Options;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Email;

/// <summary>Rolling-window counts of processed emails, excluding the one being decided.</summary>
public record EmailUsage(int SenderLastHour, int WorkspaceLastDay, int TotalLastDay);

/// <summary>Processing caps (checked before any model call) and the per-thread rules (requirements decisions 8, 19).</summary>
public static class EmailLimitPolicy
{
    public static bool IsOverLimit(EmailUsage usage, EmailLimitOptions limits) =>
        usage.SenderLastHour >= limits.PerUserPerHour
        || usage.WorkspaceLastDay >= limits.PerOrganizationPerDay
        || usage.TotalLastDay >= limits.TotalPerDay;

    /// <summary>ThreadLimit once the thread used up its automatic replies; AdminInThread once an admin replied in it
    /// (later messages become drafts); otherwise null.</summary>
    public static EmailReason? ForThread(EmailThread? thread, int maxAutomaticReplies)
    {
        if (thread is null)
        {
            return null;
        }

        if (thread.HasReachedLimit(maxAutomaticReplies))
        {
            return EmailReason.ThreadLimit;
        }

        return thread.AdminReplied ? EmailReason.AdminInThread : null;
    }
}
