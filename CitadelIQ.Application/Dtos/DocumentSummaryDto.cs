using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Dtos;

public record DocumentSummaryDto(
    Guid Id,
    Guid FolderId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAtUtc,
    ProcessingStatus Status,
    string? FailureReason);
