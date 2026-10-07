export type ProcessingStatus =
  | 'Uploaded'
  | 'ExtractingText'
  | 'Chunking'
  | 'GeneratingEmbeddings'
  | 'Ready'
  | 'Failed';

export interface FolderDto {
  id: string;
  name: string;
  parentFolderId: string | null;
}

/** Who uploaded a document; `isFormerMember` once they have left the organization. */
export interface UploaderDto {
  displayName: string;
  isFormerMember: boolean;
}

export interface DocumentSummaryDto {
  id: string;
  folderId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAtUtc: string;
  status: ProcessingStatus;
  failureReason: string | null;
  uploadedBy: UploaderDto | null;
}

export interface FolderPathSegmentDto {
  id: string;
  name: string;
}

export interface FolderContentsDto {
  folder: FolderDto;
  folderPath: FolderPathSegmentDto[];
  subfolders: FolderDto[];
  documents: DocumentSummaryDto[];
}
