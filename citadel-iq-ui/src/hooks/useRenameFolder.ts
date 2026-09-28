import { useState } from 'react';
import { foldersApi } from '../api/foldersApi';
import { ApiError } from '../api/apiClient';

export function useRenameFolder() {
  const [isRenaming, setIsRenaming] = useState(false);

  const renameFolder = async (folderId: string, name: string) => {
    setIsRenaming(true);
    try {
      return await foldersApi.rename(folderId, name);
    } catch (err) {
      throw new Error(err instanceof ApiError ? err.message : 'Unable to rename this folder right now.');
    } finally {
      setIsRenaming(false);
    }
  };

  return { renameFolder, isRenaming };
}
