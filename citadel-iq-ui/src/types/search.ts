export type SearchScope = 'EntirePortal' | 'CurrentFolder' | 'CurrentFolderAndSubfolders';

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
  similarityScore: number;
}
