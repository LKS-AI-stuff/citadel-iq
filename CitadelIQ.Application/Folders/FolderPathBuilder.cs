using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using AutoMapper;

namespace CitadelIQ.Application.Folders;

/// <summary>Walks a folder's ancestor chain up to the root. Shared by folder browsing (breadcrumb)
/// and search (result location display).</summary>
public class FolderPathBuilder(IFolderRepository folderRepository, IMapper mapper)
{
    public async Task<IReadOnlyList<FolderPathSegmentDto>> BuildAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        var path = new List<FolderPathSegmentDto> { mapper.Map<FolderPathSegmentDto>(folder) };
        var current = folder;

        while (current.ParentFolderId is { } parentId)
        {
            current = await folderRepository.GetByIdAsync(parentId, cancellationToken)
                ?? throw new NotFoundException("Folder not found.");
            path.Add(mapper.Map<FolderPathSegmentDto>(current));
        }

        path.Reverse();
        return path;
    }

    public async Task<IReadOnlyList<FolderPathSegmentDto>> BuildAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        var folder = await folderRepository.GetByIdAsync(folderId, cancellationToken)
            ?? throw new NotFoundException("Folder not found.");

        return await BuildAsync(folder, cancellationToken);
    }
}
