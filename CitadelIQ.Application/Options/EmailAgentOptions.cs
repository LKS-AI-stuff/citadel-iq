namespace CitadelIQ.Application.Options;

/// <summary>Kill switch for the email agent (<c>EmailAgent:Mode</c>), read on every loop iteration.</summary>
public enum EmailAgentMode
{
    /// <summary>Nothing is processed or sent; mail waits in the inbox and is handled in order once turned back on.</summary>
    Off,
    /// <summary>No answer-bearing mail is sent. Organizations still get drafts and holding replies; individual
    /// workspaces are recorded as paused.</summary>
    DraftsOnly,
    On
}

/// <summary>
/// The email questions agent (<c>EmailAgent</c> section). Thresholds are untuned starting points. Mailbox connection
/// settings (<c>EmailAgent:Mailbox</c>) are Infrastructure-only; the support address is here because the loop checks
/// and the portal need it.
/// </summary>
public class EmailAgentOptions
{
    /// <summary>Used when <see cref="RetryDelaysSeconds"/> is empty. (Not an initializer on the property: the
    /// configuration binder appends to an existing array instead of replacing it.)</summary>
    public static readonly int[] DefaultRetryDelaysSeconds = [60, 300, 900];

    public EmailAgentMode Mode { get; set; } = EmailAgentMode.Off;

    /// <summary>The one shared address every workspace emails (requirements decision 1).</summary>
    public string SupportAddress { get; set; } = "";

    public string SupportDisplayName { get; set; } = "CitadelIQ";

    /// <summary>Absolute portal origin for links in replies, e.g. <c>https://app.citadeliq.com</c>.</summary>
    public string PortalBaseUrl { get; set; } = "";

    /// <summary>Lets development-login identities be matched as senders. True only in appsettings.Development.json.</summary>
    public bool AllowDevelopmentIdentities { get; set; }

    /// <summary>The judge's "fully answered" score needed to auto-reply (0–1).</summary>
    public double ConfidenceThreshold { get; set; } = 0.8;

    /// <summary>The best cited source's cosine similarity must clear this (stricter than Rag:MinSimilarity).</summary>
    public double MinCitedSimilarity { get; set; } = 0.45;

    /// <summary>1 = no reworded-query retry.</summary>
    public int MaxSearchAttempts { get; set; } = 2;

    /// <summary>Cleaned body cap before triage.</summary>
    public int MaxBodyChars { get; set; } = 6000;

    public int MaxAutomaticRepliesPerThread { get; set; } = 5;

    /// <summary>Cap on an admin-edited reply.</summary>
    public int MaxDraftChars { get; set; } = 10000;

    public EmailLimitOptions Limits { get; set; } = new();

    /// <summary>Attempts per inbound email and per outbound email before it is recorded as failed.</summary>
    public int MaxAttempts { get; set; } = 3;

    public int[] RetryDelaysSeconds { get; set; } = [];

    public int ProcessingLeaseMinutes { get; set; } = 10;

    /// <summary>Fallback poll interval (IDLE usually reports new mail within seconds).</summary>
    public int PollIntervalSeconds { get; set; } = 120;

    public int IdleRefreshMinutes { get; set; } = 9;

    /// <summary>At most one draft-notification email per admin per interval.</summary>
    public int NotificationIntervalMinutes { get; set; } = 15;

    public int DraftExpiryDays { get; set; } = 30;

    /// <summary>Email activity, threads, outbound mail, processing rows and the mailbox archive folder.</summary>
    public int ActivityRetentionDays { get; set; } = 90;

    public int MaintenanceIntervalMinutes { get; set; } = 60;

    /// <summary><c>/health/email</c> reports 503 once the mailbox has been failing for longer than this.</summary>
    public int HealthFailureMinutes { get; set; } = 10;

    public EmailTextOptions Texts { get; set; } = new();

    public TimeSpan RetryDelay(int attempt)
    {
        var delays = RetryDelaysSeconds is { Length: > 0 } configured ? configured : DefaultRetryDelaysSeconds;
        return TimeSpan.FromSeconds(delays[Math.Clamp(attempt - 1, 0, delays.Length - 1)]);
    }
}

public class EmailLimitOptions
{
    /// <summary>Emails processed per sender per rolling hour.</summary>
    public int PerUserPerHour { get; set; } = 10;

    /// <summary>Emails processed per workspace per rolling 24 hours.</summary>
    public int PerOrganizationPerDay { get; set; } = 200;

    /// <summary>Emails processed across all workspaces per rolling 24 hours.</summary>
    public int TotalPerDay { get; set; } = 2000;

    /// <summary>Emails sent per rolling 24 hours, notifications included (consumer Gmail allows ~500/day — test only).</summary>
    public int MaxSentPerDay { get; set; } = 1500;
}

/// <summary>Fixed reply texts (configuration, so they can be reworded without a release).</summary>
public class EmailTextOptions
{
    public string HoldingReply { get; set; } =
        "We couldn't answer this from your documents automatically; your organization's admins have been notified.";

    public string NoConfidentReply { get; set; } = "We couldn't find a confident answer in your documents.";

    public string UnverifiedSuggestionIntro { get; set; } =
        "Unverified suggestion (may be incomplete or wrong; please check the sources):";

    public string OrganizationFooter { get; set; } =
        "This reply was generated automatically by CitadelIQ from your organization's documents. To reach a person, contact your organization's administrators.";

    public string IndividualFooter { get; set; } =
        "This reply was generated automatically by CitadelIQ from your documents.";

    public string DraftReplyFooter { get; set; } =
        "This reply was sent by your organization's administrators through CitadelIQ.";

    /// <summary>Prefixes notification subjects; a reply carrying it is recognised as a notification reply.</summary>
    public string NotificationMarker { get; set; } = "[CitadelIQ]";
}
