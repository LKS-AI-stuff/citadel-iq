using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Email;

public class EmailStatusService(
    ICurrentUser currentUser,
    IUserAccountRepository users,
    IEmailAgentMonitor monitor,
    IOptionsMonitor<EmailAgentOptions> options) : IEmailStatusService
{
    public const string NoEmail = "NoEmail";
    public const string SharedAddress = "SharedAddress";

    public async Task<EmailStatusDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var user = currentUser.User ?? throw new UnauthenticatedException();
        var o = options.CurrentValue;
        var address = user.Email;

        string? reason = null;
        if (string.IsNullOrWhiteSpace(address)
            || (!o.AllowDevelopmentIdentities && user.Issuer == AuthenticationOptions.DevelopmentIssuer))
        {
            reason = NoEmail;
        }
        else if ((await users.FindActiveByEmailAsync(address, o.AllowDevelopmentIdentities, cancellationToken)).Count > 1)
        {
            reason = SharedAddress;
        }

        return new EmailStatusDto(
            // Not advertised while off, or while on without a configured mailbox (nothing would read the mail).
            o.Mode == EmailAgentMode.Off || !monitor.MailboxConfigured ? null : o.SupportAddress,
            o.Mode,
            address,
            reason is null,
            reason);
    }
}
