import { Paper, Stack, Typography } from '@mui/material';
import FolderIcon from '@mui/icons-material/Folder';
import OpenInNewOutlinedIcon from '@mui/icons-material/OpenInNewOutlined';
import { CardActionsMenu } from '../common/CardActionsMenu';
import type { FolderDto } from '../../types/folder';

interface FolderCardProps {
  folder: FolderDto;
  onOpen: (folderId: string) => void;
}

export function FolderCard({ folder, onOpen }: FolderCardProps) {
  return (
    <Paper
      variant="outlined"
      onClick={() => onOpen(folder.id)}
      sx={{
        p: 2,
        display: 'flex',
        alignItems: 'center',
        gap: 1.5,
        cursor: 'pointer',
        transition: 'border-color 0.15s ease, background-color 0.15s ease',
        '&:hover': { borderColor: 'primary.main', bgcolor: 'action.hover' },
      }}
    >
      <FolderIcon color="primary" sx={{ fontSize: 32 }} />
      <Stack sx={{ flex: 1, minWidth: 0 }}>
        <Typography variant="body1" noWrap sx={{ fontWeight: 500 }}>
          {folder.name}
        </Typography>
      </Stack>
      <CardActionsMenu
        actions={[{ label: 'Open', icon: <OpenInNewOutlinedIcon fontSize="small" />, onClick: () => onOpen(folder.id) }]}
      />
    </Paper>
  );
}
