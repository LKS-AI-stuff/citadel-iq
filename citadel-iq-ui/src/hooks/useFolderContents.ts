import { useCallback, useEffect, useState } from 'react';
import { foldersApi } from '../api/foldersApi';
import { ApiError } from '../api/apiClient';
import type { FolderContentsDto } from '../types/folder';

export function useFolderContents(folderId: string | null) {
  const [contents, setContents] = useState<FolderContentsDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refetch = useCallback(async () => {
    if (!folderId) {
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const data = await foldersApi.getContents(folderId);
      setContents(data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to load this folder right now.');
    } finally {
      setIsLoading(false);
    }
  }, [folderId]);

  useEffect(() => {
    refetch();
  }, [refetch]);

  return { contents, isLoading, error, refetch };
}
