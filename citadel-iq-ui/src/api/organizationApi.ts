import { apiClient } from './apiClient';
import type { JoinCodeDto, JoinRequestDto, MemberDto } from '../types/organization';
import type { WorkspaceRole } from '../types/session';

export const organizationApi = {
  members: () => apiClient.get<MemberDto[]>('/api/organization/members'),
  changeRole: (userId: string, role: WorkspaceRole) =>
    apiClient.put<MemberDto>(`/api/organization/members/${userId}/role`, { role }),
  removeMember: (userId: string) => apiClient.delete<void>(`/api/organization/members/${userId}`),
  leave: () => apiClient.post<void>('/api/organization/leave'),
  joinRequests: () => apiClient.get<JoinRequestDto[]>('/api/organization/join-requests'),
  approve: (requestId: string, role: WorkspaceRole) =>
    apiClient.post<MemberDto>(`/api/organization/join-requests/${requestId}/approve`, { role }),
  reject: (requestId: string) => apiClient.post<void>(`/api/organization/join-requests/${requestId}/reject`),
  joinCode: () => apiClient.get<JoinCodeDto>('/api/organization/join-code'),
  regenerateJoinCode: () => apiClient.post<JoinCodeDto>('/api/organization/join-code/regenerate'),
};
