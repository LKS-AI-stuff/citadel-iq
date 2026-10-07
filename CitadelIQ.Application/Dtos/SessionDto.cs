using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Dtos;

public enum SessionStatus
{
    NeedsOnboarding,
    PendingApproval,
    Active,
    Closed
}

public record SessionUserDto(Guid Id, string DisplayName, string Email);

public record SessionWorkspaceDto(Guid Id, string Name, WorkspaceKind Kind, Guid RootFolderId);

public record PendingJoinRequestDto(string OrganizationName, DateTimeOffset CreatedAtUtc);

/// <summary>Everything the UI needs to decide what to show a signed-in user (<c>GET /api/me</c>).</summary>
public record SessionDto(
    SessionStatus Status,
    SessionUserDto User,
    SessionWorkspaceDto? Workspace,
    WorkspaceRole? Role,
    PendingJoinRequestDto? PendingJoinRequest,
    string? LastRejectedOrganizationName);
