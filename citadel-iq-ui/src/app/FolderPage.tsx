import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, Stack } from '@mui/material';
import CreateNewFolderOutlinedIcon from '@mui/icons-material/CreateNewFolderOutlined';
import UploadOutlinedIcon from '@mui/icons-material/UploadOutlined';
import { useFolderContents } from '../hooks/useFolderContents';
import { Breadcrumbs } from '../components/folders/Breadcrumbs';
import { FolderCard } from '../components/folders/FolderCard';
import { CreateFolderDialog } from '../components/folders/CreateFolderDialog';
import { FileCard } from '../components/documents/FileCard';
import { UploadDialog } from '../components/upload/UploadDialog';
import { EmptyState } from '../components/common/EmptyState';
import { LoadingState } from '../components/common/LoadingState';
import { useToast } from '../components/common/ToastProvider';
import { ROOT_FOLDER_ID } from '../constants';
import type { ProcessingStatus } from '../types/folder';

const UNSETTLED_STATUSES = new Set<ProcessingStatus>(['Uploaded', 'ExtractingText', 'Chunking', 'GeneratingEmbeddings']);

export function FolderPage() {
  const { folderId } = useParams<{ folderId?: string }>();
  const currentFolderId = folderId ?? ROOT_FOLDER_ID;
  const navigate = useNavigate();
  const { contents, isLoading, error, refetch } = useFolderContents(currentFolderId);
  const [isCreateFolderOpen, setCreateFolderOpen] = useState(false);
  const [isUploadOpen, setUploadOpen] = useState(false);
  const { showToast } = useToast();
  const previousStatusesRef = useRef<Record<string, ProcessingStatus>>({});

  const goToFolder = (id: string) => {
    navigate(id === ROOT_FOLDER_ID ? '/' : `/folders/${id}`);
  };

  useEffect(() => {
    const hasUnsettledDocument = contents?.documents.some((d) => UNSETTLED_STATUSES.has(d.status)) ?? false;
    if (!hasUnsettledDocument) {
      return;
    }

    const interval = setInterval(refetch, 2000);
    return () => clearInterval(interval);
  }, [contents, refetch]);

  useEffect(() => {
    if (!contents) {
      return;
    }

    const previousStatuses = previousStatusesRef.current;

    for (const document of contents.documents) {
      const previousStatus = previousStatuses[document.id];
      const justSettled = previousStatus && UNSETTLED_STATUSES.has(previousStatus) && !UNSETTLED_STATUSES.has(document.status);

      if (justSettled) {
        if (document.status === 'Ready') {
          showToast(`${document.fileName} is ready to search.`, 'success');
        } else if (document.status === 'Failed') {
          showToast(`${document.fileName} failed to process${document.failureReason ? `: ${document.failureReason}` : '.'}`, 'error');
        }
      }
    }

    previousStatusesRef.current = Object.fromEntries(contents.documents.map((d) => [d.id, d.status]));
  }, [contents, showToast]);

  if (error) {
    return <Alert severity="error">{error}</Alert>;
  }

  return (
    <Stack spacing={3}>
      <Stack
        direction="row"
        sx={{ alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 2 }}
      >
        {contents ? <Breadcrumbs items={contents.folderPath} onNavigate={goToFolder} /> : <Box />}
        <Stack direction="row" sx={{ gap: 1 }}>
          <Button
            variant="outlined"
            startIcon={<CreateNewFolderOutlinedIcon />}
            onClick={() => setCreateFolderOpen(true)}
          >
            New folder
          </Button>
          <Button variant="contained" startIcon={<UploadOutlinedIcon />} onClick={() => setUploadOpen(true)}>
            Upload
          </Button>
        </Stack>
      </Stack>

      {isLoading || !contents ? (
        <LoadingState />
      ) : contents.subfolders.length === 0 && contents.documents.length === 0 ? (
        <EmptyState
          title="This folder is empty"
          description="Create a folder or upload a document to get started."
        />
      ) : (
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))', gap: 2 }}>
          {contents.subfolders.map((folder) => (
            <FolderCard key={folder.id} folder={folder} onOpen={goToFolder} />
          ))}
          {contents.documents.map((document) => (
            <FileCard key={document.id} document={document} />
          ))}
        </Box>
      )}

      <CreateFolderDialog
        open={isCreateFolderOpen}
        parentFolderId={currentFolderId}
        onClose={() => setCreateFolderOpen(false)}
        onCreated={refetch}
      />

      <UploadDialog
        open={isUploadOpen}
        folderId={currentFolderId}
        onClose={() => setUploadOpen(false)}
        onUploaded={refetch}
      />
    </Stack>
  );
}
