namespace CitadelIQ.Api.Contracts;

public record CreateFolderRequest(Guid ParentFolderId, string Name);
