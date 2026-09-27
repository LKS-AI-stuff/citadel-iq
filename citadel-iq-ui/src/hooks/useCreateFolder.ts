import { useState } from 'react';
import { foldersApi } from '../api/foldersApi';
import { ApiError } from '../api/apiClient';

export function useCreateFolder() {
  const [isCreating, setIsCreating] = useState(false);

  const createFolder = async (parentFolderId: string, name: string) => {
    setIsCreating(true);
    try {
      return await foldersApi.create(parentFolderId, name);
    } catch (err) {
      throw new Error(err instanceof ApiError ? err.message : 'Unable to create the folder right now.');
    } finally {
      setIsCreating(false);
    }
  };

  return { createFolder, isCreating };
}
