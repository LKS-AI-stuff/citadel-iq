using CitadelIQ.Api.Contracts;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Organizations;
using Microsoft.AspNetCore.Mvc;

namespace CitadelIQ.Api.Controllers;

/// <summary>Organization administration. Roles and the last-Owner rule are enforced by the service.</summary>
[ApiController]
[Route("api/organization")]
public class OrganizationController(IOrganizationAdminService adminService) : ControllerBase
{
    [HttpGet("members")]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> ListMembers(CancellationToken cancellationToken) =>
        Ok(await adminService.ListMembersAsync(cancellationToken));

    [HttpPut("members/{userId:guid}/role")]
    public async Task<ActionResult<MemberDto>> ChangeRole(Guid userId, [FromBody] ChangeRoleRequest request, CancellationToken cancellationToken) =>
        Ok(await adminService.ChangeRoleAsync(
            userId, request.Role ?? throw new Application.Common.ValidationException("A role is required."), cancellationToken));

    [HttpDelete("members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid userId, CancellationToken cancellationToken)
    {
        await adminService.RemoveMemberAsync(userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("leave")]
    public async Task<IActionResult> Leave(CancellationToken cancellationToken)
    {
        await adminService.LeaveAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("join-requests")]
    public async Task<ActionResult<IReadOnlyList<JoinRequestDto>>> ListJoinRequests(CancellationToken cancellationToken) =>
        Ok(await adminService.ListJoinRequestsAsync(cancellationToken));

    [HttpPost("join-requests/{requestId:guid}/approve")]
    public async Task<ActionResult<MemberDto>> Approve(Guid requestId, [FromBody] ApproveJoinRequestRequest? request, CancellationToken cancellationToken) =>
        Ok(await adminService.ApproveJoinRequestAsync(requestId, request?.Role ?? Domain.Enums.WorkspaceRole.Member, cancellationToken));

    [HttpPost("join-requests/{requestId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid requestId, CancellationToken cancellationToken)
    {
        await adminService.RejectJoinRequestAsync(requestId, cancellationToken);
        return NoContent();
    }

    [HttpGet("join-code")]
    public async Task<ActionResult<JoinCodeDto>> GetJoinCode(CancellationToken cancellationToken) =>
        Ok(await adminService.GetJoinCodeAsync(cancellationToken));

    [HttpPost("join-code/regenerate")]
    public async Task<ActionResult<JoinCodeDto>> RegenerateJoinCode(CancellationToken cancellationToken) =>
        Ok(await adminService.RegenerateJoinCodeAsync(cancellationToken));
}
