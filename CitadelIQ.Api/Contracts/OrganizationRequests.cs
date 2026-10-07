using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Api.Contracts;

/// <summary>Role is nullable so a body without it is a 400, not a silent demotion to Member (enum value 0).</summary>
public record ChangeRoleRequest(WorkspaceRole? Role);

public record ApproveJoinRequestRequest(WorkspaceRole Role = WorkspaceRole.Member);
