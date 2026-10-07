import type { UploaderDto } from './folder';

/** EntireWorkspace = the caller's whole workspace. */
export type SearchScope = 'EntireWorkspace' | 'CurrentFolder' | 'CurrentFolderAndSubfolders';

export interface SearchRequestDto {
  query: string;
  currentFolderId: string;
  searchScope: SearchScope;
  topK?: number;
}

export interface SearchResultDto {
  documentId: string;
  fileName: string;
  folderPath: string;
  contentType: string;
  chunkText: string;
  chunkIndex: number;
  pageNumber: number | null;
  sheetName: string | null;
  similarityScore: number;
  uploadedBy: UploaderDto | null;
}
