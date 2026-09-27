import { Paper, Typography } from '@mui/material';
import FolderIcon from '@mui/icons-material/Folder';
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
      className="cursor-pointer transition-colors hover:border-blue-400 hover:bg-blue-50"
      sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 1.5 }}
    >
      <FolderIcon color="primary" sx={{ fontSize: 32 }} />
      <Typography variant="body1" noWrap sx={{ fontWeight: 500 }}>
        {folder.name}
      </Typography>
    </Paper>
  );
}
