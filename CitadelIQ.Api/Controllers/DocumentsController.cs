using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace CitadelIQ.Api.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController(IDocumentService documentService) : ControllerBase
{
    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<DocumentSummaryDto>> Upload(
        [FromForm] Guid folderId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await documentService.UploadDocumentAsync(
            folderId,
            file.FileName,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            stream,
            file.Length,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{documentId:guid}/status")]
    public async Task<ActionResult<DocumentStatusDto>> GetStatus(Guid documentId, CancellationToken cancellationToken)
    {
        var status = await documentService.GetStatusAsync(documentId, cancellationToken);
        return Ok(status);
    }

    [HttpGet("{documentId:guid}/download")]
    public async Task<IActionResult> Download(Guid documentId, CancellationToken cancellationToken)
    {
        var (content, fileName, contentType) = await documentService.DownloadAsync(documentId, cancellationToken);
        return File(content, contentType, fileName);
    }
}
