namespace CitadelIQ.Application.Dtos;

public record SearchResultDto(
    Guid DocumentId,
    Guid FolderId,
    string FileName,
    string FolderPath,
    string ContentType,
    string ChunkText,
    int ChunkIndex,
    int? PageNumber,
    string? SheetName,
    double SimilarityScore,
    UploaderDto? UploadedBy = null);
