namespace CitadelIQ.Application.Dtos;

public record FolderDto(Guid Id, string Name, Guid? ParentFolderId);
