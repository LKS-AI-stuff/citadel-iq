using CitadelIQ.Api.Authentication;
using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CitadelIQ.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize(Policy = AuthenticationSetup.SignedInPolicy)]
public class MeController(IAccountService accountService) : ControllerBase
{
    /// <summary>The signed-in user's session state: onboarding, pending approval, active (with workspace and role)
    /// or closed. 401 when signed out.</summary>
    [HttpGet]
    public async Task<ActionResult<SessionDto>> Get(CancellationToken cancellationToken) =>
        Ok(await accountService.GetSessionAsync(cancellationToken));
}
