using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace CitadelIQ.Application.Organizations;

public class OrganizationAdminService(
    ICurrentUser currentUser,
    IMembershipRepository membershipRepository,
    IJoinRequestRepository joinRequestRepository,
    IUserAccountRepository userRepository,
    IWorkspaceRepository workspaceRepository,
    ILogger<OrganizationAdminService> logger) : IOrganizationAdminService
{
    private const string ManageMembers = "manage members";

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(CancellationToken cancellationToken = default)
    {
        var workspace = RequireOrganization(WorkspaceRole.Admin, ManageMembers);
        var members = await membershipRepository.ListByWorkspaceAsync(workspace.Id, cancellationToken);
        return members.Select(ToDto).ToList();
    }

    public async Task<MemberDto> ChangeRoleAsync(Guid userId, WorkspaceRole role, CancellationToken cancellationToken = default)
    {
        var workspace = RequireOrganization(WorkspaceRole.Admin, ManageMembers);
        RequireDefined(role);
        var actorId = currentUser.RequireUserId();

        await membershipRepository.RunLockedAsync(workspace.Id, set =>
        {
            var actor = RequireLockedActor(set, actorId);
            var target = set.Find(userId) ?? throw new NotFoundException("Member not found.");

            if (!MembershipRules.CanManage(actor.Role, target.Role, role))
            {
                throw new ForbiddenException("You don't have permission to change this member's role.");
            }

            target.ChangeRole(role);
            MembershipRules.EnsureOwnerRemains(set.Memberships);
            return Task.CompletedTask;
        }, cancellationToken);

        logger.LogInformation("{ActorId} changed {TargetId} to {Role}", actorId, userId, role);
        return await GetMemberAsync(workspace.Id, userId, cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var workspace = RequireOrganization(WorkspaceRole.Admin, ManageMembers);
        var actorId = currentUser.RequireUserId();

        await membershipRepository.RunLockedAsync(workspace.Id, async set =>
        {
            var actor = RequireLockedActor(set, actorId);
            var target = set.Find(userId) ?? throw new NotFoundException("Member not found.");

            if (!MembershipRules.CanManage(actor.Role, target.Role, targetNewRole: null))
            {
                throw new ForbiddenException("You don't have permission to remove this member.");
            }

            await RemoveAndCloseAsync(set, target, cancellationToken);
        }, cancellationToken);

        logger.LogInformation("{ActorId} removed {TargetId}", actorId, userId);
    }

    public async Task LeaveAsync(CancellationToken cancellationToken = default)
    {
        var workspace = RequireOrganization(WorkspaceRole.Member, "leave this organization");
        var actorId = currentUser.RequireUserId();

        await membershipRepository.RunLockedAsync(workspace.Id, async set =>
        {
            var actor = RequireLockedActor(set, actorId);
            await RemoveAndCloseAsync(set, actor, cancellationToken);
        }, cancellationToken);

        logger.LogInformation("{ActorId} left workspace {WorkspaceId}", actorId, workspace.Id);
    }

    public async Task<IReadOnlyList<JoinRequestDto>> ListJoinRequestsAsync(CancellationToken cancellationToken = default)
    {
        var workspace = RequireOrganization(WorkspaceRole.Admin, ManageMembers);
        var requests = await joinRequestRepository.ListPendingAsync(workspace.Id, cancellationToken);
        return requests
            .Select(r => new JoinRequestDto(r.Request.Id, r.User.DisplayName, r.User.Email, r.Request.CreatedAtUtc))
            .ToList();
    }

    public async Task<MemberDto> ApproveJoinRequestAsync(Guid requestId, WorkspaceRole role, CancellationToken cancellationToken = default)
    {
        var workspace = RequireOrganization(WorkspaceRole.Admin, ManageMembers);
        RequireDefined(role);
        var actorId = currentUser.RequireUserId();
        Guid newMemberId = default;

        await membershipRepository.RunLockedAsync(workspace.Id, set =>
        {
            var actor = RequireLockedActor(set, actorId);
            var request = set.PendingJoinRequests.FirstOrDefault(r => r.Id == requestId)
                ?? throw new NotFoundException("Join request not found.");

            if (!MembershipRules.CanManage(actor.Role, WorkspaceRole.Member, role))
            {
                throw new ForbiddenException("Only an Owner can approve someone as an Owner.");
            }

            request.Approve(actorId);
            set.Add(Membership.Create(request.UserId, workspace.Id, role));
            newMemberId = request.UserId;
            return Task.CompletedTask;
        }, cancellationToken);

        logger.LogInformation("{ActorId} approved join request {RequestId} as {Role}", actorId, requestId, role);
        return await GetMemberAsync(workspace.Id, newMemberId, cancellationToken);
    }

    public async Task RejectJoinRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var workspace = RequireOrganization(WorkspaceRole.Admin, ManageMembers);
        var actorId = currentUser.RequireUserId();

        await membershipRepository.RunLockedAsync(workspace.Id, set =>
        {
            var actor = RequireLockedActor(set, actorId);
            if (actor.Role < WorkspaceRole.Admin)
            {
                throw new ForbiddenException("You don't have permission to manage members.");
            }

            var request = set.PendingJoinRequests.FirstOrDefault(r => r.Id == requestId)
                ?? throw new NotFoundException("Join request not found.");
            request.Reject(actorId);
            return Task.CompletedTask;
        }, cancellationToken);

        logger.LogInformation("{ActorId} rejected join request {RequestId}", actorId, requestId);
    }

    public async Task<JoinCodeDto> GetJoinCodeAsync(CancellationToken cancellationToken = default)
    {
        var workspace = RequireOrganization(WorkspaceRole.Admin, ManageMembers);
        var current = await workspaceRepository.GetByIdAsync(workspace.Id, cancellationToken)
            ?? throw new NotFoundException("Organization not found.");
        return new JoinCodeDto(JoinCode.Format(current.JoinCode!));
    }

    public async Task<JoinCodeDto> RegenerateJoinCodeAsync(CancellationToken cancellationToken = default)
    {
        var workspace = RequireOrganization(WorkspaceRole.Admin, ManageMembers);
        var current = await workspaceRepository.GetByIdAsync(workspace.Id, cancellationToken)
            ?? throw new NotFoundException("Organization not found.");

        current.RegenerateJoinCode(JoinCode.Generate());
        await workspaceRepository.UpdateAsync(current, cancellationToken);
        logger.LogInformation("{ActorId} regenerated the join code of {WorkspaceId}", currentUser.UserId, workspace.Id);
        return new JoinCodeDto(JoinCode.Format(current.JoinCode!));
    }

    /// <summary>Individual workspaces have no organization endpoints: they are reported as not found.</summary>
    private Workspace RequireOrganization(WorkspaceRole minimum, string action)
    {
        if (currentUser.Workspace is not { Kind: WorkspaceKind.Organization } workspace || !currentUser.IsActiveMember)
        {
            throw new NotFoundException("Organization not found.");
        }

        currentUser.EnsureRole(minimum, action);
        return workspace;
    }

    /// <summary>The actor's role is re-read under the lock: a concurrent demotion of the actor must win.</summary>
    private static Membership RequireLockedActor(WorkspaceMembershipSet set, Guid actorId) =>
        set.Find(actorId) ?? throw new ForbiddenException("You are no longer a member of this organization.");

    private async Task RemoveAndCloseAsync(WorkspaceMembershipSet set, Membership target, CancellationToken cancellationToken)
    {
        set.Remove(target);
        MembershipRules.EnsureOwnerRemains(set.Memberships);

        // Tracked by the same unit of work, so the closure is saved (or rolled back) with the removal.
        var user = await userRepository.GetByIdAsync(target.UserId, cancellationToken);
        user?.Close();
    }

    private async Task<MemberDto> GetMemberAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken)
    {
        var members = await membershipRepository.ListByWorkspaceAsync(workspaceId, cancellationToken);
        var member = members.FirstOrDefault(m => m.Membership.UserId == userId) ?? throw new NotFoundException("Member not found.");
        return ToDto(member);
    }

    private MemberDto ToDto(MemberRecord record) =>
        new(record.User.Id, record.User.DisplayName, record.User.Email, record.Membership.Role, record.Membership.JoinedAtUtc,
            record.User.Id == currentUser.UserId);

    private static void RequireDefined(WorkspaceRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ValidationException("Unrecognized role.");
        }
    }
}
