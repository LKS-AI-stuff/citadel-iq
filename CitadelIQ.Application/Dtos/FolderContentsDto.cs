namespace CitadelIQ.Application.Dtos;

public record FolderContentsDto(
    FolderDto Folder,
    IReadOnlyList<FolderPathSegmentDto> FolderPath,
    IReadOnlyList<FolderDto> Subfolders,
    IReadOnlyList<DocumentSummaryDto> Documents);
