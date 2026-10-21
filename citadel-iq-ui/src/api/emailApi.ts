import { apiClient } from './apiClient';
import type { EmailActivityDto, EmailDraftDto, EmailDraftSummaryDto, EmailStatusDto } from '../types/email';

export const emailApi = {
  status: () => apiClient.get<EmailStatusDto>('/api/email/status'),
  /** Next page: `before` = the last row's time, `skip` = rows already loaded with exactly that time. */
  activity: (params: { before?: string; skip?: number; limit?: number; mine?: boolean }) => {
    const query = new URLSearchParams();
    if (params.before) query.set('before', params.before);
    if (params.skip) query.set('skip', String(params.skip));
    if (params.limit) query.set('limit', String(params.limit));
    if (params.mine) query.set('mine', 'true');
    const suffix = query.toString();
    return apiClient.get<EmailActivityDto[]>(`/api/email/activity${suffix ? `?${suffix}` : ''}`);
  },
  pendingDrafts: () => apiClient.get<EmailDraftSummaryDto[]>('/api/email/drafts?status=Pending'),
  pendingCount: () => apiClient.get<{ pending: number }>('/api/email/drafts/count'),
  draft: (id: string) => apiClient.get<EmailDraftDto>(`/api/email/drafts/${id}`),
  sendDraft: (id: string, text: string) => apiClient.post<void>(`/api/email/drafts/${id}/send`, { text }),
  dismissDraft: (id: string) => apiClient.post<void>(`/api/email/drafts/${id}/dismiss`),
  notificationPreference: () => apiClient.get<{ emailOnNewDrafts: boolean }>('/api/email/notification-preference'),
  setNotificationPreference: (emailOnNewDrafts: boolean) =>
    apiClient.put<void>('/api/email/notification-preference', { emailOnNewDrafts }),
};
