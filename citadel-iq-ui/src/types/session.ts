export type SessionStatus = 'NeedsOnboarding' | 'PendingApproval' | 'Active' | 'Closed';
export type WorkspaceKind = 'Individual' | 'Organization';
export type WorkspaceRole = 'Member' | 'Admin' | 'Owner';

export interface SessionDto {
  status: SessionStatus;
  user: { id: string; displayName: string; email: string };
  workspace: { id: string; name: string; kind: WorkspaceKind; rootFolderId: string } | null;
  role: WorkspaceRole | null;
  pendingJoinRequest: { organizationName: string; createdAtUtc: string } | null;
  lastRejectedOrganizationName: string | null;
}

/** Each role includes the ones below it. */
export const ROLE_RANK: Record<WorkspaceRole, number> = { Member: 0, Admin: 1, Owner: 2 };
