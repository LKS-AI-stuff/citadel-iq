using AutoMapper;
using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Common;
using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Folders;

public class FolderService(
    IFolderRepository folderRepository,
    IDocumentRepository documentRepository,
    IDocumentService documentService,
    FolderPathBuilder folderPathBuilder,
    UploaderLookup uploaderLookup,
    ICurrentUser currentUser,
    IMapper mapper) : IFolderService
{
    // Every lookup below goes through the repositories, which only ever see the current workspace's rows (EF
    // query filter + row-level security): another workspace's folder id is simply "not found".

    public async Task<FolderDto> GetRootAsync(CancellationToken cancellationToken = default)
    {
        var root = await folderRepository.GetRootAsync(cancellationToken)
            ?? throw new NotFoundException("Folder not found.");

        return mapper.Map<FolderDto>(root);
    }

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
        var uploaders = await uploaderLookup.GetAsync(documents.Select(d => d.UploadedByUserId), cancellationToken);

        return new FolderContentsDto(
            mapper.Map<FolderDto>(folder),
            folderPath,
            mapper.Map<List<FolderDto>>(subfolders),
            documents
                .Select(d => mapper.Map<DocumentSummaryDto>(d) with { UploadedBy = UploaderLookup.Find(uploaders, d.UploadedByUserId) })
                .ToList());
    }

    public async Task<FolderDto> CreateFolderAsync(Guid parentFolderId, string name, CancellationToken cancellationToken = default)
    {
        var parent = await folderRepository.GetByIdAsync(parentFolderId, cancellationToken)
            ?? throw new NotFoundException("Parent folder not found.");

        if (await folderRepository.ExistsWithNameAsync(parentFolderId, name, cancellationToken))
        {
            throw new ValidationException($"A folder named \"{name.Trim()}\" already exists here.");
        }

        var folder = Folder.CreateChild(parent, name);
        await folderRepository.AddAsync(folder, cancellationToken);
        return mapper.Map<FolderDto>(folder);
    }

    public async Task<FolderDto> RenameFolderAsync(Guid folderId, string name, CancellationToken cancellationToken = default)
    {
        var folder = await folderRepository.GetByIdAsync(folderId, cancellationToken)
            ?? throw new NotFoundException("Folder not found.");

        currentUser.EnsureRole(WorkspaceRole.Admin, "rename folders");

        if (folder.IsRoot)
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

        currentUser.EnsureRole(WorkspaceRole.Admin, "delete folders");

        if (folder.IsRoot)
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
