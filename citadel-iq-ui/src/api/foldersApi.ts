import { apiClient } from './apiClient';
import type { FolderContentsDto, FolderDto } from '../types/folder';

export const foldersApi = {
  getRootId: () => apiClient.get<{ folderId: string }>('/api/folders/root'),
  getContents: (folderId: string) => apiClient.get<FolderContentsDto>(`/api/folders/${folderId}/contents`),
  create: (parentFolderId: string, name: string) =>
    apiClient.post<FolderDto>('/api/folders', { parentFolderId, name }),
};
