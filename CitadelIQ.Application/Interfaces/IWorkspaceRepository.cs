using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Application.Interfaces;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Looks up by the normalized join code (see <c>JoinCode.Normalize</c>).</summary>
    Task<Workspace?> GetByJoinCodeAsync(string normalizedJoinCode, CancellationToken cancellationToken = default);

    Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default);

    Task UpdateAsync(Workspace workspace, CancellationToken cancellationToken = default);
}
