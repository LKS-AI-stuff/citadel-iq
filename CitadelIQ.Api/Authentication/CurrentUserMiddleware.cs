using CitadelIQ.Application.Accounts;

namespace CitadelIQ.Api.Authentication;

/// <summary>
/// Resolves the signed-in identity to its user, membership and workspace on every request (so removals and role
/// changes apply immediately) and fills the scoped <see cref="CurrentUserContext"/>. A closed account is rejected for
/// every API call except <c>/api/me</c>, which tells the UI to show the "account closed" page.
/// </summary>
public class CurrentUserMiddleware(RequestDelegate next, ILogger<CurrentUserMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IAccountService accounts, CurrentUserContext currentUser)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !SessionClaims.TryGetIdentity(context.User, out var issuer, out var subject))
        {
            await next(context);
            return;
        }

        var resolved = await accounts.ResolveAsync(issuer, subject, context.RequestAborted);
        if (resolved is null)
        {
            // A valid session for a user row that no longer exists (e.g. a reset database): recreate it.
            var user = await accounts.EnsureUserAsync(
                issuer, subject, context.User.FindFirst(SessionClaims.Email)?.Value, context.User.FindFirst(SessionClaims.Name)?.Value,
                context.RequestAborted);
            resolved = new ResolvedUser(user, null, null);
        }

        currentUser.SetUser(resolved.User, resolved.Membership, resolved.Workspace);

        if (resolved.User.IsClosed
            && context.Request.Path.StartsWithSegments("/api")
            && !context.Request.Path.StartsWithSegments("/api/me"))
        {
            await ProblemResponses.WriteAsync(context, StatusCodes.Status403Forbidden, "Your account has been closed.");
            return;
        }

        // Ids only — never email addresses — in the log scope.
        using (logger.BeginScope(new Dictionary<string, object?>
               {
                   ["UserId"] = resolved.User.Id,
                   ["WorkspaceId"] = resolved.Workspace?.Id
               }))
        {
            await next(context);
        }
    }
}
