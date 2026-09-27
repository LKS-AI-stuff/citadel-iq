using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Dtos;

public record DocumentStatusDto(Guid Id, ProcessingStatus Status, string? FailureReason);
