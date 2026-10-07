import type { WorkspaceRole } from './session';

export interface MemberDto {
  userId: string;
  displayName: string;
  email: string;
  role: WorkspaceRole;
  joinedAtUtc: string;
  isCurrentUser: boolean;
}

export interface JoinRequestDto {
  id: string;
  displayName: string;
  email: string;
  createdAtUtc: string;
}

export interface JoinCodeDto {
  code: string;
}
