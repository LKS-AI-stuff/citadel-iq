using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Application.Interfaces;

public interface IFolderRepository
{
    Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Folder>> GetChildrenAsync(Guid parentFolderId, CancellationToken cancellationToken = default);

    /// <summary>Returns all descendant folder ids of <paramref name="folderId"/>, not including itself.</summary>
    Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid folderId, CancellationToken cancellationToken = default);

    Task<bool> ExistsWithNameAsync(Guid parentFolderId, string name, CancellationToken cancellationToken = default);

    Task AddAsync(Folder folder, CancellationToken cancellationToken = default);
}
