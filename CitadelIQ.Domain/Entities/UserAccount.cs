namespace CitadelIQ.Domain.Entities;

/// <summary>
/// A person who signs in, identified by the identity provider's (issuer, subject) pair — never by email.
/// A closed account is kept (so documents it uploaded can still show "former member") but can never be used again.
/// </summary>
public class UserAccount
{
    public const int MaxDisplayNameLength = 200;
    public const int MaxEmailLength = 320;

    public Guid Id { get; private set; }
    public string Issuer { get; private set; }
    public string Subject { get; private set; }
    public string Email { get; private set; }
    public string DisplayName { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastSignInAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }

    private UserAccount(Guid id, string issuer, string subject, string email, string displayName, DateTimeOffset createdAtUtc, DateTimeOffset lastSignInAtUtc)
    {
        Id = id;
        Issuer = issuer;
        Subject = subject;
        Email = email;
        DisplayName = displayName;
        CreatedAtUtc = createdAtUtc;
        LastSignInAtUtc = lastSignInAtUtc;
    }

    public static UserAccount Create(string issuer, string subject, string? email, string? displayName)
    {
        var normalizedEmail = NormalizeEmail(email);
        var now = DateTimeOffset.UtcNow;
        return new UserAccount(Guid.NewGuid(), issuer, subject, normalizedEmail, NormalizeDisplayName(displayName, normalizedEmail), now, now);
    }

    public bool IsClosed => ClosedAtUtc is not null;

    /// <summary>Refreshes the profile from the identity provider's claims; they are the source of truth.</summary>
    public void RecordSignIn(string? email, string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            Email = NormalizeEmail(email);
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = NormalizeDisplayName(displayName, Email);
        }

        LastSignInAtUtc = DateTimeOffset.UtcNow;
    }

    public void Close()
    {
        ClosedAtUtc ??= DateTimeOffset.UtcNow;
    }

    private static string NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim() ?? "";
        return trimmed.Length > MaxEmailLength ? trimmed[..MaxEmailLength] : trimmed;
    }

    private static string NormalizeDisplayName(string? displayName, string email)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? email : displayName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "User";
        }

        return name.Length > MaxDisplayNameLength ? name[..MaxDisplayNameLength] : name;
    }
}
