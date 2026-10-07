using CitadelIQ.Application.Accounts;
using Microsoft.AspNetCore.Authorization;

namespace CitadelIQ.Api.Authentication;

/// <summary>Signed in, not closed, and onboarded into a workspace. The fallback policy for every endpoint that does
/// not say otherwise.</summary>
public sealed class ActiveMemberRequirement : IAuthorizationRequirement;

public sealed class ActiveMemberHandler(IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<ActiveMemberRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveMemberRequirement requirement)
    {
        var currentUser = httpContextAccessor.HttpContext?.RequestServices.GetService<ICurrentUser>();
        if (currentUser is { IsActiveMember: true })
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
