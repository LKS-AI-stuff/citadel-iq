using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace CitadelIQ.Application.Accounts;

public class OnboardingService(
    CurrentUserContext context,
    IAccountService accountService,
    IWorkspaceRepository workspaceRepository,
    IFolderRepository folderRepository,
    IMembershipRepository membershipRepository,
    IJoinRequestRepository joinRequestRepository,
    IUnitOfWork unitOfWork,
    ILogger<OnboardingService> logger) : IOnboardingService
{
    /// <summary>Same message for an unknown code and an individual workspace's id, so codes can't be probed.</summary>
    public const string UnrecognisedCodeMessage = "That code wasn't recognised.";

    public async Task<SessionDto> CreateIndividualAsync(CancellationToken cancellationToken = default)
    {
        var user = await RequireNeedsOnboardingAsync(cancellationToken);
        var workspace = Workspace.CreateIndividual(user.DisplayName);
        await CreateWorkspaceAsync(user, workspace, cancellationToken);
        return await accountService.GetSessionAsync(cancellationToken);
    }

    public async Task<SessionDto> CreateOrganizationAsync(string name, CancellationToken cancellationToken = default)
    {
        var user = await RequireNeedsOnboardingAsync(cancellationToken);
        var workspace = Workspace.CreateOrganization(name, JoinCode.Generate());
        await CreateWorkspaceAsync(user, workspace, cancellationToken);
        return await accountService.GetSessionAsync(cancellationToken);
    }

    public async Task<SessionDto> RequestToJoinAsync(string joinCode, CancellationToken cancellationToken = default)
    {
        var user = await RequireNeedsOnboardingAsync(cancellationToken);

        var normalized = JoinCode.Normalize(joinCode) ?? throw new ValidationException(UnrecognisedCodeMessage);
        var workspace = await workspaceRepository.GetByJoinCodeAsync(normalized, cancellationToken);
        if (workspace is not { Kind: WorkspaceKind.Organization })
        {
            throw new ValidationException(UnrecognisedCodeMessage);
        }

        await joinRequestRepository.AddAsync(JoinRequest.Create(user.Id, workspace.Id), cancellationToken);
        logger.LogInformation("User {UserId} requested to join workspace {WorkspaceId}", user.Id, workspace.Id);
        return await accountService.GetSessionAsync(cancellationToken);
    }

    public async Task<SessionDto> CancelJoinRequestAsync(CancellationToken cancellationToken = default)
    {
        var userId = context.RequireUserId();
        var pending = await joinRequestRepository.GetPendingForUserAsync(userId, cancellationToken)
            ?? throw new NotFoundException("You have no pending join request.");

        // Under the workspace lock, so a cancel can't interleave with an approval of the same request.
        await membershipRepository.RunLockedAsync(pending.WorkspaceId, set =>
        {
            var request = set.PendingJoinRequests.FirstOrDefault(r => r.UserId == userId)
                ?? throw new NotFoundException("You have no pending join request.");
            request.Cancel();
            return Task.CompletedTask;
        }, cancellationToken);

        return await accountService.GetSessionAsync(cancellationToken);
    }

    private async Task<UserAccount> RequireNeedsOnboardingAsync(CancellationToken cancellationToken)
    {
        var user = context.User ?? throw new UnauthenticatedException();

        if (user.IsClosed)
        {
            throw new ForbiddenException("Your account has been closed.");
        }

        if (context.Membership is not null)
        {
            throw new ValidationException("You already belong to a workspace.");
        }

        if (await joinRequestRepository.GetPendingForUserAsync(user.Id, cancellationToken) is not null)
        {
            throw new ValidationException("You already have a pending request to join an organization. Cancel it first.");
        }

        return user;
    }

    private async Task CreateWorkspaceAsync(UserAccount user, Workspace workspace, CancellationToken cancellationToken)
    {
        // Order matters: enter the new workspace *before* the transaction opens its connection, so the connection is
        // stamped with it and the root folder insert passes row-level security's WITH CHECK.
        context.Enter(workspace.Id);
        var membership = Membership.Create(user.Id, workspace.Id, WorkspaceRole.Owner);

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await workspaceRepository.AddAsync(workspace, cancellationToken);
            await folderRepository.AddAsync(Folder.CreateRoot(workspace.Id), cancellationToken);
            await membershipRepository.AddAsync(membership, cancellationToken);
        }, cancellationToken);

        context.SetUser(user, membership, workspace);
        logger.LogInformation("User {UserId} created {Kind} workspace {WorkspaceId}", user.Id, workspace.Kind, workspace.Id);
    }
}
