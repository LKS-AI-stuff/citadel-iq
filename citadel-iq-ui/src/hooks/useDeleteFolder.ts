import { useState } from 'react';
import { foldersApi } from '../api/foldersApi';
import { ApiError } from '../api/apiClient';

export function useDeleteFolder() {
  const [isDeleting, setIsDeleting] = useState(false);

  const deleteFolder = async (folderId: string) => {
    setIsDeleting(true);
    try {
      await foldersApi.delete(folderId);
    } catch (err) {
      throw new Error(err instanceof ApiError ? err.message : 'Unable to delete this folder right now.');
    } finally {
      setIsDeleting(false);
    }
  };

  return { deleteFolder, isDeleting };
}
