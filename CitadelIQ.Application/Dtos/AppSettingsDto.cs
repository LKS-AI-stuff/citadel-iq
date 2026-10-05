namespace CitadelIQ.Application.Dtos;

/// <summary>Client-safe settings only — never model names, keys or connection strings.</summary>
public record AppSettingsDto(bool AskEnabled, int MaxQuestionLength, int MaxHistoryTurns, int MaxHistoryChars);
