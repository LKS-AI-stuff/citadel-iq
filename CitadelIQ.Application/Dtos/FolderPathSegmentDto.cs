namespace CitadelIQ.Application.Dtos;

/// <summary>One folder in the ancestor chain from the root down to (and including) a given folder.</summary>
public record FolderPathSegmentDto(Guid Id, string Name);
