using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Dtos;

public record SearchRequestDto(string Query, Guid CurrentFolderId, SearchScope SearchScope, int? TopK);
