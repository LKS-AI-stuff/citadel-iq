using CitadelIQ.Application.Dtos;

namespace CitadelIQ.Application.Folders;

public interface IFolderService
{
    Task<FolderDto> GetByIdAsync(Guid folderId, CancellationToken cancellationToken = default);

    Task<FolderContentsDto> GetContentsAsync(Guid folderId, CancellationToken cancellationToken = default);

    Task<FolderDto> CreateFolderAsync(Guid parentFolderId, string name, CancellationToken cancellationToken = default);
}
