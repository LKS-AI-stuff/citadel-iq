namespace CitadelIQ.Application.Dtos;

/// <summary>One numbered source: a search result plus the citation number the model refers to it by.</summary>
public record AnswerSourceDto(
    int Number,
    Guid DocumentId,
    string FileName,
    string FolderPath,
    string ContentType,
    string ChunkText,
    int ChunkIndex,
    int? PageNumber,
    string? SheetName,
    double SimilarityScore);

public record AnswerUsageDto(int InputTokens, int OutputTokens);
