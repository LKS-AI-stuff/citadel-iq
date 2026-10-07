import { apiClient } from './apiClient';
import type { SessionDto } from '../types/session';

export const sessionApi = {
  me: () => apiClient.get<SessionDto>('/api/me'),
  createIndividual: () => apiClient.post<SessionDto>('/api/onboarding/individual'),
  createOrganization: (name: string) => apiClient.post<SessionDto>('/api/onboarding/organization', { name }),
  requestToJoin: (joinCode: string) => apiClient.post<SessionDto>('/api/onboarding/join-requests', { joinCode }),
  cancelJoinRequest: () => apiClient.delete<SessionDto>('/api/onboarding/join-requests/current'),
  /** Browser navigations (not fetch): the server redirects to the identity provider and back. */
  signInUrl: (returnUrl: string) => `/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`,
  signOutAction: '/auth/logout',
};
