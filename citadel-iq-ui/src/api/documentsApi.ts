import { API_BASE_URL, CSRF_HEADERS, apiClient, reportAuthFailure } from './apiClient';
import type { DocumentSummaryDto } from '../types/folder';

export interface UploadHandle {
  promise: Promise<DocumentSummaryDto>;
  abort: () => void;
}

function uploadDocument(folderId: string, file: File, onProgress: (percent: number) => void): UploadHandle {
  const xhr = new XMLHttpRequest();

  const promise = new Promise<DocumentSummaryDto>((resolve, reject) => {
    const formData = new FormData();
    formData.append('folderId', folderId);
    formData.append('file', file);

    xhr.open('POST', `${API_BASE_URL}/api/documents/upload`);
    for (const [name, value] of Object.entries(CSRF_HEADERS)) {
      xhr.setRequestHeader(name, value);
    }

    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) {
        onProgress(Math.round((event.loaded / event.total) * 100));
      }
    };

    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve(JSON.parse(xhr.responseText) as DocumentSummaryDto);
        return;
      }

      const problem = safeParseJson(xhr.responseText);
      reportAuthFailure(xhr.status, problem?.title);
      reject(new Error(problem?.title ?? 'Upload failed. Please try again.'));
    };

    xhr.onerror = () => reject(new Error('Upload failed. Please check your connection and try again.'));
    xhr.onabort = () => reject(new Error('Upload cancelled.'));

    xhr.send(formData);
  });

  return { promise, abort: () => xhr.abort() };
}

function safeParseJson(text: string): { title?: string } | null {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

export const documentsApi = {
  upload: uploadDocument,
  getStatus: (documentId: string) =>
    apiClient.get<{ id: string; status: string; failureReason: string | null }>(`/api/documents/${documentId}/status`),
  getDownloadUrl: (documentId: string) => `${API_BASE_URL}/api/documents/${documentId}/download`,
  delete: (documentId: string) => apiClient.delete<void>(`/api/documents/${documentId}`),
};
