using CitadelIQ.Application.Dtos;

namespace CitadelIQ.Application.Accounts;

/// <summary>First-sign-in choices. Every method requires a signed-in user who has no workspace and no pending
/// join request.</summary>
public interface IOnboardingService
{
    Task<SessionDto> CreateIndividualAsync(CancellationToken cancellationToken = default);

    Task<SessionDto> CreateOrganizationAsync(string name, CancellationToken cancellationToken = default);

    Task<SessionDto> RequestToJoinAsync(string joinCode, CancellationToken cancellationToken = default);

    Task<SessionDto> CancelJoinRequestAsync(CancellationToken cancellationToken = default);
}
