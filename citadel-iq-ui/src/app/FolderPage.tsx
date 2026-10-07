import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, Divider, Stack, alpha, useTheme } from '@mui/material';
import ArrowBackOutlinedIcon from '@mui/icons-material/ArrowBackOutlined';
import CreateNewFolderOutlinedIcon from '@mui/icons-material/CreateNewFolderOutlined';
import UploadOutlinedIcon from '@mui/icons-material/UploadOutlined';
import InsertDriveFileOutlinedIcon from '@mui/icons-material/InsertDriveFileOutlined';
import FolderIcon from '@mui/icons-material/Folder';
import { useFolderContents } from '../hooks/useFolderContents';
import { Breadcrumbs } from '../components/folders/Breadcrumbs';
import { FolderCard } from '../components/folders/FolderCard';
import { CreateFolderDialog } from '../components/folders/CreateFolderDialog';
import { FileCard } from '../components/documents/FileCard';
import { UploadDialog } from '../components/upload/UploadDialog';
import { EmptyState } from '../components/common/EmptyState';
import { LoadingState } from '../components/common/LoadingState';
import { GlassSurface } from '../components/common/GlassSurface';
import { SectionHeader } from '../components/common/SectionHeader';
import { useToast } from '../components/common/ToastProvider';
import { folderAccentColor as getFolderAccentColor } from '../theme/glass';
import { useActiveWorkspace } from '../session/useSession';
import type { ProcessingStatus } from '../types/folder';

const UNSETTLED_STATUSES = new Set<ProcessingStatus>(['Uploaded', 'ExtractingText', 'Chunking', 'GeneratingEmbeddings']);

export function FolderPage() {
  const { folderId } = useParams<{ folderId?: string }>();
  // Each workspace has its own Home folder; "/" always means it.
  const { workspace } = useActiveWorkspace();
  const rootFolderId = workspace.rootFolderId;
  const currentFolderId = folderId ?? rootFolderId;
  const navigate = useNavigate();
  const { contents, isLoading, error, refetch } = useFolderContents(currentFolderId);
  const theme = useTheme();
  const documentsAccentColor = theme.palette.success.main;
  const subfoldersAccentColor = getFolderAccentColor(theme);
  const [isCreateFolderOpen, setCreateFolderOpen] = useState(false);
  const [isUploadOpen, setUploadOpen] = useState(false);
  const { showToast } = useToast();
  const previousStatusesRef = useRef<Record<string, ProcessingStatus>>({});

  const goToFolder = (id: string) => {
    navigate(id === rootFolderId ? '/' : `/folders/${id}`);
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
          {currentFolderId !== rootFolderId && (
            <Button
              variant="outlined"
              startIcon={<ArrowBackOutlinedIcon />}
              onClick={() => goToFolder(contents?.folder.parentFolderId ?? rootFolderId)}
            >
              Back
            </Button>
          )}
          <Button
            variant="outlined"
            startIcon={<CreateNewFolderOutlinedIcon />}
            onClick={() => setCreateFolderOpen(true)}
          >
            New folder
          </Button>
          <Button variant="contained" startIcon={<UploadOutlinedIcon />} onClick={() => setUploadOpen(true)}>
            Upload Document
          </Button>
        </Stack>
      </Stack>

        {isLoading || !contents ? (
          <GlassSurface sx={{ p: { xs: 2, sm: 3 } }}>
            <LoadingState />
          </GlassSurface>
        ) : (
          <GlassSurface component="section" sx={{ p: { xs: 2, sm: 3 } }}>
            <Box component="section">
              <SectionHeader
                icon={<InsertDriveFileOutlinedIcon sx={{ color: documentsAccentColor, fontSize: 20 }} />}
                color={documentsAccentColor}
                title="Documents"
                count={contents.documents.length}
              />
              {contents.documents.length === 0 ? (
                <EmptyState
                  dense
                  title="No documents in this folder yet"
                  description="Upload a PDF, DOCX, TXT, CSV, or XLSX file to make it searchable."
                  icon={<InsertDriveFileOutlinedIcon sx={{ fontSize: 22, color: 'primary.main' }} />}
                />
              ) : (
                <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(min(280px, 100%), 1fr))', gap: 2 }}>
                  {contents.documents.map((document, index) => (
                    <FileCard
                      key={document.id}
                      document={document}
                      animationDelayMs={index * 35}
                      onDeleted={refetch}
                    />
                  ))}
                </Box>
              )}
            </Box>

            <Divider sx={{ my: { xs: 2.5, sm: 3 }, borderColor: (t) => alpha(t.palette.text.primary, 0.1) }} />

            <Box component="section">
              <SectionHeader
                icon={<FolderIcon sx={{ color: subfoldersAccentColor, fontSize: 20 }} />}
                color={subfoldersAccentColor}
                title="Subfolders"
                count={contents.subfolders.length}
              />
              {contents.subfolders.length === 0 ? (
                <EmptyState
                  dense
                  title="No subfolders yet"
                  description="Create a folder to organize documents by topic, department, or year."
                  icon={<FolderIcon sx={{ fontSize: 22, color: 'primary.main' }} />}
                />
              ) : (
                <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(min(280px, 100%), 1fr))', gap: 2 }}>
                  {contents.subfolders.map((folder, index) => (
                    <FolderCard
                      key={folder.id}
                      folder={folder}
                      onOpen={goToFolder}
                      onDeleted={refetch}
                      onRenamed={refetch}
                      animationDelayMs={index * 35}
                    />
                  ))}
                </Box>
              )}
            </Box>
          </GlassSurface>
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
