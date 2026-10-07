using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Accounts;

public class AccountService(
    IUserAccountRepository userRepository,
    IMembershipRepository membershipRepository,
    IWorkspaceRepository workspaceRepository,
    IJoinRequestRepository joinRequestRepository,
    IFolderRepository folderRepository,
    ICurrentUser currentUser) : IAccountService
{
    public async Task<UserAccount> EnsureUserAsync(string issuer, string subject, string? email, string? displayName, CancellationToken cancellationToken = default)
    {
        var existing = await userRepository.GetByIdentityAsync(issuer, subject, cancellationToken);
        if (existing is null)
        {
            return await userRepository.AddOrGetAsync(UserAccount.Create(issuer, subject, email, displayName), cancellationToken);
        }

        existing.RecordSignIn(email, displayName);
        await userRepository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task<ResolvedUser?> ResolveAsync(string issuer, string subject, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdentityAsync(issuer, subject, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var membership = await membershipRepository.GetByUserIdAsync(user.Id, cancellationToken);
        var workspace = membership is null ? null : await workspaceRepository.GetByIdAsync(membership.WorkspaceId, cancellationToken);
        return new ResolvedUser(user, membership, workspace);
    }

    public async Task<SessionDto> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        var user = currentUser.User ?? throw new UnauthenticatedException();
        var userDto = new SessionUserDto(user.Id, user.DisplayName, user.Email);

        if (user.IsClosed)
        {
            return new SessionDto(SessionStatus.Closed, userDto, null, null, null, null);
        }

        if (currentUser is { Membership: { } membership, Workspace: { } workspace })
        {
            var root = await folderRepository.GetRootAsync(cancellationToken)
                ?? throw new InvalidOperationException("The workspace has no root folder.");
            return new SessionDto(
                SessionStatus.Active,
                userDto,
                new SessionWorkspaceDto(workspace.Id, workspace.Name, workspace.Kind, root.Id),
                membership.Role,
                null,
                null);
        }

        var latest = await joinRequestRepository.GetLatestForUserAsync(user.Id, cancellationToken);
        var requestedWorkspace = latest is null ? null : await workspaceRepository.GetByIdAsync(latest.WorkspaceId, cancellationToken);

        if (latest is { Status: JoinRequestStatus.Pending } && requestedWorkspace is not null)
        {
            return new SessionDto(
                SessionStatus.PendingApproval, userDto, null, null,
                new PendingJoinRequestDto(requestedWorkspace.Name, latest.CreatedAtUtc), null);
        }

        var rejectedName = latest is { Status: JoinRequestStatus.Rejected } ? requestedWorkspace?.Name : null;
        return new SessionDto(SessionStatus.NeedsOnboarding, userDto, null, null, null, rejectedName);
    }
}
