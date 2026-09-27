namespace CitadelIQ.Application.Dtos;

public record SearchResultDto(
    Guid DocumentId,
    string FileName,
    string FolderPath,
    string ContentType,
    string ChunkText,
    int ChunkIndex,
    int? PageNumber,
    double SimilarityScore);
