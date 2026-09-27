import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, Stack } from '@mui/material';
import CreateNewFolderOutlinedIcon from '@mui/icons-material/CreateNewFolderOutlined';
import { useFolderContents } from '../hooks/useFolderContents';
import { Breadcrumbs } from '../components/folders/Breadcrumbs';
import { FolderCard } from '../components/folders/FolderCard';
import { CreateFolderDialog } from '../components/folders/CreateFolderDialog';
import { EmptyState } from '../components/common/EmptyState';
import { LoadingState } from '../components/common/LoadingState';
import { ROOT_FOLDER_ID } from '../constants';

export function FolderPage() {
  const { folderId } = useParams<{ folderId?: string }>();
  const currentFolderId = folderId ?? ROOT_FOLDER_ID;
  const navigate = useNavigate();
  const { contents, isLoading, error, refetch } = useFolderContents(currentFolderId);
  const [isCreateFolderOpen, setCreateFolderOpen] = useState(false);

  const goToFolder = (id: string) => {
    navigate(id === ROOT_FOLDER_ID ? '/' : `/folders/${id}`);
  };

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
        <Button
          variant="outlined"
          startIcon={<CreateNewFolderOutlinedIcon />}
          onClick={() => setCreateFolderOpen(true)}
        >
          New folder
        </Button>
      </Stack>

      {isLoading || !contents ? (
        <LoadingState />
      ) : contents.subfolders.length === 0 && contents.documents.length === 0 ? (
        <EmptyState
          title="This folder is empty"
          description="Create a folder to start organizing your documents."
        />
      ) : (
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))', gap: 2 }}>
          {contents.subfolders.map((folder) => (
            <FolderCard key={folder.id} folder={folder} onOpen={goToFolder} />
          ))}
        </Box>
      )}

      <CreateFolderDialog
        open={isCreateFolderOpen}
        parentFolderId={currentFolderId}
        onClose={() => setCreateFolderOpen(false)}
        onCreated={refetch}
      />
    </Stack>
  );
}
