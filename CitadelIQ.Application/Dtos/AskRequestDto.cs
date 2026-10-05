using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Dtos;

public record ConversationTurnDto(string Question, string Answer);

public record AskRequestDto(string Question, Guid CurrentFolderId, SearchScope SearchScope, IReadOnlyList<ConversationTurnDto>? History);
