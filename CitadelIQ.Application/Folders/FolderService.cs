using AutoMapper;
using CitadelIQ.Application.Common;
using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Application.Folders;

public class FolderService(
    IFolderRepository folderRepository,
    IDocumentRepository documentRepository,
    IDocumentService documentService,
    FolderPathBuilder folderPathBuilder,
    IMapper mapper) : IFolderService
{
    public async Task<FolderDto> GetByIdAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        var folder = await folderRepository.GetByIdAsync(folderId, cancellationToken)
            ?? throw new NotFoundException("Folder not found.");

        return mapper.Map<FolderDto>(folder);
    }

    public async Task<FolderContentsDto> GetContentsAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        var folder = await folderRepository.GetByIdAsync(folderId, cancellationToken)
            ?? throw new NotFoundException("Folder not found.");

        var folderPath = await folderPathBuilder.BuildAsync(folder, cancellationToken);
        var subfolders = await folderRepository.GetChildrenAsync(folder.Id, cancellationToken);
        var documents = await documentRepository.GetByFolderIdAsync(folder.Id, cancellationToken);

        return new FolderContentsDto(
            mapper.Map<FolderDto>(folder),
            folderPath,
            mapper.Map<List<FolderDto>>(subfolders),
            mapper.Map<List<DocumentSummaryDto>>(documents));
    }

    public async Task<FolderDto> CreateFolderAsync(Guid parentFolderId, string name, CancellationToken cancellationToken = default)
    {
        _ = await folderRepository.GetByIdAsync(parentFolderId, cancellationToken)
            ?? throw new NotFoundException("Parent folder not found.");

        if (await folderRepository.ExistsWithNameAsync(parentFolderId, name, cancellationToken))
        {
            throw new ValidationException($"A folder named \"{name.Trim()}\" already exists here.");
        }

        var folder = Folder.Create(name, parentFolderId);
        await folderRepository.AddAsync(folder, cancellationToken);
        return mapper.Map<FolderDto>(folder);
    }

    public async Task<FolderDto> RenameFolderAsync(Guid folderId, string name, CancellationToken cancellationToken = default)
    {
        var folder = await folderRepository.GetByIdAsync(folderId, cancellationToken)
            ?? throw new NotFoundException("Folder not found.");

        if (folder.Id == Folder.RootId)
        {
            throw new ValidationException("The Home folder cannot be renamed.");
        }

        var trimmed = name.Trim();
        var isUnchanged = string.Equals(trimmed, folder.Name, StringComparison.OrdinalIgnoreCase);

        if (!isUnchanged && await folderRepository.ExistsWithNameAsync(folder.ParentFolderId!.Value, trimmed, cancellationToken))
        {
            throw new ValidationException($"A folder named \"{trimmed}\" already exists here.");
        }

        folder.Rename(name);
        await folderRepository.UpdateAsync(folder, cancellationToken);
        return mapper.Map<FolderDto>(folder);
    }

    public async Task DeleteFolderAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        var folder = await folderRepository.GetByIdAsync(folderId, cancellationToken)
            ?? throw new NotFoundException("Folder not found.");

        if (folder.Id == Folder.RootId)
        {
            throw new ValidationException("The Home folder cannot be deleted.");
        }

        var descendantFolderIds = await folderRepository.GetDescendantIdsAsync(folder.Id, cancellationToken);
        var allFolderIds = new List<Guid>(descendantFolderIds) { folder.Id };

        var documents = await documentRepository.GetByFolderIdsAsync(allFolderIds, cancellationToken);
        await documentService.DeleteDocumentsAsync(documents, cancellationToken);

        await folderRepository.DeleteManyAsync(allFolderIds, cancellationToken);
    }
}
