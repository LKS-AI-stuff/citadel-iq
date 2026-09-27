using CitadelIQ.Api.Contracts;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Folders;
using CitadelIQ.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace CitadelIQ.Api.Controllers;

[ApiController]
[Route("api/folders")]
public class FoldersController(IFolderService folderService) : ControllerBase
{
    /// <summary>The well-known id of the Home folder, so the frontend can bootstrap without a lookup.</summary>
    [HttpGet("root")]
    public ActionResult<object> GetRootId() => Ok(new { folderId = Folder.RootId });

    [HttpGet("{folderId:guid}")]
    public async Task<ActionResult<FolderDto>> GetById(Guid folderId, CancellationToken cancellationToken)
    {
        var folder = await folderService.GetByIdAsync(folderId, cancellationToken);
        return Ok(folder);
    }

    [HttpGet("{folderId:guid}/contents")]
    public async Task<ActionResult<FolderContentsDto>> GetContents(Guid folderId, CancellationToken cancellationToken)
    {
        var contents = await folderService.GetContentsAsync(folderId, cancellationToken);
        return Ok(contents);
    }

    [HttpPost]
    public async Task<ActionResult<FolderDto>> Create([FromBody] CreateFolderRequest request, CancellationToken cancellationToken)
    {
        var folder = await folderService.CreateFolderAsync(request.ParentFolderId, request.Name, cancellationToken);
        return Ok(folder);
    }
}
