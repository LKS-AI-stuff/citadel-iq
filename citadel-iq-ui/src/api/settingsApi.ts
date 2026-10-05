import { apiClient } from './apiClient';
import type { AppSettings } from '../types/ask';

export const settingsApi = {
  get: () => apiClient.get<AppSettings>('/api/settings'),
};
