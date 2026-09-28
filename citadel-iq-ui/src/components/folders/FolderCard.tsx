import { Stack, Tooltip, Typography } from '@mui/material';
import FolderIcon from '@mui/icons-material/Folder';
import OpenInNewOutlinedIcon from '@mui/icons-material/OpenInNewOutlined';
import { CardActionsMenu } from '../common/CardActionsMenu';
import { GlassSurface } from '../common/GlassSurface';
import { IconBadge } from '../common/IconBadge';
import type { FolderDto } from '../../types/folder';

interface FolderCardProps {
  folder: FolderDto;
  onOpen: (folderId: string) => void;
  animationDelayMs?: number;
}

export function FolderCard({ folder, onOpen, animationDelayMs = 0 }: FolderCardProps) {
  return (
    <GlassSurface
      hover
      radius={16}
      onClick={() => onOpen(folder.id)}
      className="animate-fade-in-up"
      sx={{
        p: 2,
        display: 'flex',
        alignItems: 'center',
        gap: 1.75,
        animationDelay: `${animationDelayMs}ms`,
        '&:hover .folder-icon-badge': { transform: 'scale(1.08)' },
      }}
    >
      <IconBadge className="folder-icon-badge" color="#4f46e5">
        <FolderIcon sx={{ color: '#4f46e5', fontSize: 24 }} />
      </IconBadge>
      <Stack sx={{ flex: 1, minWidth: 0 }}>
        <Tooltip title={folder.name}>
          <Typography variant="body1" noWrap sx={{ fontWeight: 600 }}>
            {folder.name}
          </Typography>
        </Tooltip>
      </Stack>
      <CardActionsMenu
        actions={[{ label: 'Open', icon: <OpenInNewOutlinedIcon fontSize="small" />, onClick: () => onOpen(folder.id) }]}
      />
    </GlassSurface>
  );
}
