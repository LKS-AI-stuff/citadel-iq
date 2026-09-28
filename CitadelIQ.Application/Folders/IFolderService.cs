using CitadelIQ.Application.Dtos;

namespace CitadelIQ.Application.Folders;

public interface IFolderService
{
    Task<FolderDto> GetByIdAsync(Guid folderId, CancellationToken cancellationToken = default);

    Task<FolderContentsDto> GetContentsAsync(Guid folderId, CancellationToken cancellationToken = default);

    Task<FolderDto> CreateFolderAsync(Guid parentFolderId, string name, CancellationToken cancellationToken = default);

    Task<FolderDto> RenameFolderAsync(Guid folderId, string name, CancellationToken cancellationToken = default);

    /// <summary>Deletes a folder along with every descendant subfolder and every document contained
    /// anywhere in that subtree (including each document's stored file, chunks, and embeddings).</summary>
    Task DeleteFolderAsync(Guid folderId, CancellationToken cancellationToken = default);
}
