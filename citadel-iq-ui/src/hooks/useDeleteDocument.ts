import { useState } from 'react';
import { documentsApi } from '../api/documentsApi';
import { ApiError } from '../api/apiClient';

export function useDeleteDocument() {
  const [isDeleting, setIsDeleting] = useState(false);

  const deleteDocument = async (documentId: string) => {
    setIsDeleting(true);
    try {
      await documentsApi.delete(documentId);
    } catch (err) {
      throw new Error(err instanceof ApiError ? err.message : 'Unable to delete this document right now.');
    } finally {
      setIsDeleting(false);
    }
  };

  return { deleteDocument, isDeleting };
}
