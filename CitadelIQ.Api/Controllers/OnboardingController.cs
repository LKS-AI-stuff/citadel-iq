using CitadelIQ.Api.Authentication;
using CitadelIQ.Api.Contracts;
using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CitadelIQ.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
[Authorize(Policy = AuthenticationSetup.SignedInPolicy)]
public class OnboardingController(IOnboardingService onboardingService) : ControllerBase
{
    public const string JoinRateLimitPolicy = "join-code";

    [HttpPost("individual")]
    public async Task<ActionResult<SessionDto>> CreateIndividual(CancellationToken cancellationToken) =>
        Ok(await onboardingService.CreateIndividualAsync(cancellationToken));

    [HttpPost("organization")]
    public async Task<ActionResult<SessionDto>> CreateOrganization([FromBody] CreateOrganizationRequest request, CancellationToken cancellationToken) =>
        Ok(await onboardingService.CreateOrganizationAsync(request.Name ?? "", cancellationToken));

    [HttpPost("join-requests")]
    [EnableRateLimiting(JoinRateLimitPolicy)]
    public async Task<ActionResult<SessionDto>> RequestToJoin([FromBody] JoinOrganizationRequest request, CancellationToken cancellationToken) =>
        Ok(await onboardingService.RequestToJoinAsync(request.JoinCode ?? "", cancellationToken));

    [HttpDelete("join-requests/current")]
    public async Task<ActionResult<SessionDto>> CancelJoinRequest(CancellationToken cancellationToken) =>
        Ok(await onboardingService.CancelJoinRequestAsync(cancellationToken));
}
