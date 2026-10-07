using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Dtos;

public record MemberDto(Guid UserId, string DisplayName, string Email, WorkspaceRole Role, DateTimeOffset JoinedAtUtc, bool IsCurrentUser);

public record JoinRequestDto(Guid Id, string DisplayName, string Email, DateTimeOffset CreatedAtUtc);

/// <summary>Formatted for display, e.g. <c>7K3M-Q9TX-2HDB</c>.</summary>
public record JoinCodeDto(string Code);
