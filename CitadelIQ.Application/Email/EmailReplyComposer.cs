using System.Text;
using CitadelIQ.Application.Options;
using CitadelIQ.Application.Rag;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Email;

/// <summary>One draft listed in a notification: no question or answer content, only who, what subject and why.</summary>
public record NotificationItem(string AskerDisplayName, string Subject, EmailReason Reason);

/// <summary>
/// Builds the plain-text bodies and threading headers of every email we send. Answers keep only valid <c>[n]</c>
/// markers and list only the cited documents, each linked to its folder in the portal (sign-in protected; never a
/// download link).
/// </summary>
public class EmailReplyComposer(IOptionsMonitor<EmailAgentOptions> options)
{
    private const int MaxReferences = 10;
    public const int MaxNotificationItems = 20;

    public string NewMessageId()
    {
        var domain = EmailAddresses.DomainOf(options.CurrentValue.SupportAddress) ?? "citadeliq.invalid";
        return $"{AutomatedMailClassifier.MessageIdPrefix}{Guid.NewGuid():N}@{domain}";
    }

    public static string ReplySubject(string? originalSubject)
    {
        var subject = string.IsNullOrWhiteSpace(originalSubject) ? "Your question" : originalSubject.Trim();
        return subject.StartsWith("re:", StringComparison.OrdinalIgnoreCase) ? subject : "Re: " + subject;
    }

    /// <summary>The original's References plus its own Message-ID (the last <see cref="MaxReferences"/> only).</summary>
    public static IReadOnlyList<string> ReplyReferences(string? inboundMessageId, string? inboundReferences)
    {
        var ids = (inboundReferences ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (!string.IsNullOrWhiteSpace(inboundMessageId))
        {
            ids.Add(inboundMessageId.Trim());
        }

        return ids.Distinct(StringComparer.OrdinalIgnoreCase).TakeLast(MaxReferences).ToList();
    }

    public string AutoReply(string answer, IReadOnlyList<EmailSourceRef> sources, WorkspaceKind kind)
    {
        var sb = new StringBuilder(CleanAnswer(answer, sources));
        AppendSources(sb, sources);
        AppendFooter(sb, Footer(kind));
        return sb.ToString();
    }

    public string HoldingReply()
    {
        var sb = new StringBuilder(options.CurrentValue.Texts.HoldingReply);
        AppendFooter(sb, Footer(WorkspaceKind.Organization));
        return sb.ToString();
    }

    /// <summary>Individual workspaces; a low-confidence suggestion is included only clearly labelled as unverified.</summary>
    public string NoConfidentReply(string? suggestion, IReadOnlyList<EmailSourceRef> sources)
    {
        var texts = options.CurrentValue.Texts;
        var sb = new StringBuilder(texts.NoConfidentReply);
        if (!string.IsNullOrWhiteSpace(suggestion))
        {
            sb.Append("\n\n").Append(texts.UnverifiedSuggestionIntro).Append("\n\n").Append(CleanAnswer(suggestion, sources));
            AppendSources(sb, sources);
        }

        AppendFooter(sb, Footer(WorkspaceKind.Individual));
        return sb.ToString();
    }

    /// <summary>An admin's reviewed reply. Citation markers they kept still point at the listed sources.</summary>
    public string DraftReply(string text, IReadOnlyList<EmailSourceRef> sources)
    {
        var cited = CitationParser.Extract(text, MaxNumber(sources));
        var listed = sources.Select(s => s with { Cited = cited.Contains(s.Number) }).ToList();
        var sb = new StringBuilder(CleanAnswer(text.Trim(), listed));
        AppendSources(sb, listed);
        AppendFooter(sb, options.CurrentValue.Texts.DraftReplyFooter);
        return sb.ToString();
    }

    public (string Subject, string Body) DraftNotification(int pendingCount, IReadOnlyList<NotificationItem> items)
    {
        var o = options.CurrentValue;
        var subject = $"{o.Texts.NotificationMarker} {pendingCount} draft{(pendingCount == 1 ? "" : "s")} awaiting review";
        var sb = new StringBuilder();
        sb.Append(pendingCount == 1 ? "A draft reply is" : $"{pendingCount} draft replies are")
          .Append(" waiting for review in CitadelIQ:\n\n");

        foreach (var item in items.Take(MaxNotificationItems))
        {
            sb.Append("- ").Append(item.AskerDisplayName).Append(": \"").Append(item.Subject).Append("\" (")
              .Append(DescribeReason(item.Reason)).Append(")\n");
        }

        var more = pendingCount - Math.Min(items.Count, MaxNotificationItems);
        if (more > 0)
        {
            sb.Append("- and ").Append(more).Append(" more\n");
        }

        sb.Append("\nReview them: ").Append(PortalUrl("/email")).Append('\n');
        AppendFooter(sb, "You can turn these emails off on the Email review page.");
        return (subject, sb.ToString());
    }

    public string DocumentLink(Guid folderId, Guid documentId) => PortalUrl($"/folders/{folderId}?document={documentId}");

    public static string DescribeReason(EmailReason reason) => reason switch
    {
        EmailReason.NoAnswer => "no answer found",
        EmailReason.LowConfidence => "low confidence",
        EmailReason.OverLimit => "over limit",
        EmailReason.ThreadLimit => "conversation limit reached",
        EmailReason.AdminInThread => "follow-up to an admin's reply",
        EmailReason.DraftsOnly => "automatic replies paused",
        EmailReason.AnswersDisabled => "answers turned off",
        EmailReason.Failed => "processing failed",
        _ => reason.ToString()
    };

    private static string CleanAnswer(string answer, IReadOnlyList<EmailSourceRef> sources) =>
        CitationParser.RemoveInvalid(answer.Trim(), MaxNumber(sources));

    private static int MaxNumber(IReadOnlyList<EmailSourceRef> sources) => sources.Count == 0 ? 0 : sources.Max(s => s.Number);

    private void AppendSources(StringBuilder sb, IReadOnlyList<EmailSourceRef> sources)
    {
        var cited = sources.Where(s => s.Cited).OrderBy(s => s.Number).ToList();
        if (cited.Count == 0)
        {
            return;
        }

        sb.Append("\n\nSources:\n");
        foreach (var s in cited)
        {
            sb.Append('[').Append(s.Number).Append("] ").Append(s.FileName);
            if (s.PageNumber is { } page) sb.Append(", page ").Append(page);
            else if (!string.IsNullOrWhiteSpace(s.SheetName)) sb.Append(", sheet ").Append(s.SheetName);
            sb.Append(" — ").Append(DocumentLink(s.FolderId, s.DocumentId)).Append('\n');
        }
    }

    private static void AppendFooter(StringBuilder sb, string footer)
    {
        var text = sb.ToString().TrimEnd();
        sb.Clear().Append(text).Append("\n\n-- \n").Append(footer).Append('\n');
    }

    private string Footer(WorkspaceKind kind) =>
        kind == WorkspaceKind.Organization ? options.CurrentValue.Texts.OrganizationFooter : options.CurrentValue.Texts.IndividualFooter;

    private string PortalUrl(string path) => options.CurrentValue.PortalBaseUrl.TrimEnd('/') + path;
}
