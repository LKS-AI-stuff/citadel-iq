using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Email;

/// <summary>Where an incoming email belongs: a reply to a draft notification (ignored), a follow-up in one of the
/// sender's threads, or a new conversation.</summary>
public record ThreadMatch(bool IsNotificationReply, Guid? ThreadId);

public static class ThreadMatcher
{
    /// <summary>Message-IDs the email refers to (In-Reply-To first, then References), de-duplicated.</summary>
    public static IReadOnlyList<string> ReferencedIds(string? inReplyTo, IEnumerable<string> references) =>
        new[] { inReplyTo }.Concat(references)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim().Trim('<', '>'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <param name="referenced">Our own outbound messages (in the sender's workspace) whose Message-IDs the email refers to.</param>
    public static ThreadMatch Match(string subject, IReadOnlyList<OutboundEmail> referenced, Guid senderUserId, string notificationMarker)
    {
        if (referenced.Any(o => o.Kind == OutboundEmailKind.DraftNotification)
            || (!string.IsNullOrEmpty(notificationMarker) && subject.Contains(notificationMarker, StringComparison.OrdinalIgnoreCase)))
        {
            return new ThreadMatch(true, null);
        }

        // Only the asker's own thread continues: a forwarded reply from a colleague starts a new conversation.
        var thread = referenced
            .Where(o => o.ThreadId is not null && o.ToUserId == senderUserId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(o => o.ThreadId)
            .FirstOrDefault();

        return new ThreadMatch(false, thread);
    }
}
