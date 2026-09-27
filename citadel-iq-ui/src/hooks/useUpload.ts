import { useCallback, useState } from 'react';
import { documentsApi, type UploadHandle } from '../api/documentsApi';
import type { DocumentSummaryDto } from '../types/folder';

export interface UploadItem {
  id: string;
  fileName: string;
  progress: number;
  status: 'uploading' | 'done' | 'error';
  errorMessage?: string;
}

export function useUpload(folderId: string, onUploaded: (document: DocumentSummaryDto) => void) {
  const [uploads, setUploads] = useState<UploadItem[]>([]);
  const [handles] = useState(() => new Map<string, UploadHandle>());

  const updateUpload = (id: string, patch: Partial<UploadItem>) => {
    setUploads((prev) => prev.map((u) => (u.id === id ? { ...u, ...patch } : u)));
  };

  const uploadFiles = useCallback(
    (files: FileList | File[]) => {
      Array.from(files).forEach((file) => {
        const id = `${file.name}-${Date.now()}-${Math.random()}`;

        setUploads((prev) => [...prev, { id, fileName: file.name, progress: 0, status: 'uploading' }]);

        const handle = documentsApi.upload(folderId, file, (progress) => updateUpload(id, { progress }));
        handles.set(id, handle);

        handle.promise
          .then((document) => {
            updateUpload(id, { progress: 100, status: 'done' });
            onUploaded(document);
          })
          .catch((err: Error) => {
            updateUpload(id, { status: 'error', errorMessage: err.message });
          })
          .finally(() => {
            handles.delete(id);
          });
      });
    },
    [folderId, handles, onUploaded],
  );

  const cancelUpload = useCallback(
    (id: string) => {
      handles.get(id)?.abort();
    },
    [handles],
  );

  const clear = useCallback(() => setUploads([]), []);

  return { uploads, uploadFiles, cancelUpload, clear };
}
