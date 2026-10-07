using CitadelIQ.Application.Dtos;
using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Organizations;

/// <summary>Member management for organization workspaces. Everything except <see cref="LeaveAsync"/> needs Admin
/// or Owner; every membership change runs under the workspace lock and keeps at least one Owner.</summary>
public interface IOrganizationAdminService
{
    Task<IReadOnlyList<MemberDto>> ListMembersAsync(CancellationToken cancellationToken = default);

    Task<MemberDto> ChangeRoleAsync(Guid userId, WorkspaceRole role, CancellationToken cancellationToken = default);

    /// <summary>Removes the member and permanently closes their account.</summary>
    Task RemoveMemberAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The caller leaves; their account is closed. Not available in individual workspaces.</summary>
    Task LeaveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JoinRequestDto>> ListJoinRequestsAsync(CancellationToken cancellationToken = default);

    Task<MemberDto> ApproveJoinRequestAsync(Guid requestId, WorkspaceRole role, CancellationToken cancellationToken = default);

    Task RejectJoinRequestAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task<JoinCodeDto> GetJoinCodeAsync(CancellationToken cancellationToken = default);

    Task<JoinCodeDto> RegenerateJoinCodeAsync(CancellationToken cancellationToken = default);
}
