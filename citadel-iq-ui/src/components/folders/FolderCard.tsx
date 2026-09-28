import { useState } from 'react';
import { IconButton, Stack, TextField, Tooltip, Typography, useTheme } from '@mui/material';
import FolderIcon from '@mui/icons-material/Folder';
import ArrowForwardIcon from '@mui/icons-material/ArrowForward';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import CheckOutlinedIcon from '@mui/icons-material/CheckOutlined';
import CloseOutlinedIcon from '@mui/icons-material/CloseOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import { ConfirmDialog } from '../common/ConfirmDialog';
import { GlassSurface } from '../common/GlassSurface';
import { IconBadge } from '../common/IconBadge';
import { useToast } from '../common/ToastProvider';
import { useDeleteFolder } from '../../hooks/useDeleteFolder';
import { useRenameFolder } from '../../hooks/useRenameFolder';
import { actionIconButtonSx, folderAccentColor as getFolderAccentColor } from '../../theme/glass';
import type { FolderDto } from '../../types/folder';

interface FolderCardProps {
  folder: FolderDto;
  onOpen: (folderId: string) => void;
  onDeleted?: () => void;
  onRenamed?: () => void;
  animationDelayMs?: number;
}

export function FolderCard({ folder, onOpen, onDeleted, onRenamed, animationDelayMs = 0 }: FolderCardProps) {
  const theme = useTheme();
  const folderAccentColor = getFolderAccentColor(theme);
  const [isConfirmOpen, setConfirmOpen] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [editedName, setEditedName] = useState(folder.name);
  const { deleteFolder, isDeleting } = useDeleteFolder();
  const { renameFolder, isRenaming } = useRenameFolder();
  const { showToast } = useToast();

  const handleDelete = async () => {
    try {
      await deleteFolder(folder.id);
      showToast(`"${folder.name}" was deleted.`, 'success');
      setConfirmOpen(false);
      onDeleted?.();
    } catch (err) {
      showToast(err instanceof Error ? err.message : 'Unable to delete this folder.', 'error');
    }
  };

  const startEditing = () => {
    setEditedName(folder.name);
    setIsEditing(true);
  };

  const cancelEditing = () => {
    setIsEditing(false);
    setEditedName(folder.name);
  };

  const handleSave = async () => {
    const trimmed = editedName.trim();

    if (!trimmed) {
      showToast('Folder name cannot be empty.', 'error');
      return;
    }

    if (trimmed === folder.name) {
      setIsEditing(false);
      return;
    }

    try {
      await renameFolder(folder.id, trimmed);
      showToast(`Folder renamed to "${trimmed}".`, 'success');
      setIsEditing(false);
      onRenamed?.();
    } catch (err) {
      showToast(err instanceof Error ? err.message : 'Unable to rename this folder.', 'error');
    }
  };

  return (
    <>
      <GlassSurface
        hover={!isEditing}
        radius={16}
        onClick={isEditing ? undefined : () => onOpen(folder.id)}
        className="animate-fade-in-up"
        sx={{
          p: 2,
          display: 'flex',
          alignItems: 'center',
          gap: 1.75,
          animationDelay: `${animationDelayMs}ms`,
          '&:hover .folder-icon-badge': { transform: isEditing ? 'none' : 'scale(1.08)' },
        }}
      >
        <IconBadge className="folder-icon-badge" color={folderAccentColor}>
          <FolderIcon sx={{ color: folderAccentColor, fontSize: 24 }} />
        </IconBadge>
        <Stack sx={{ flex: 1, minWidth: 0 }}>
          {isEditing ? (
            <TextField
              autoFocus
              size="small"
              variant="standard"
              value={editedName}
              onChange={(e) => setEditedName(e.target.value)}
              onClick={(e) => e.stopPropagation()}
              onKeyDown={(e) => {
                e.stopPropagation();
                if (e.key === 'Enter') {
                  handleSave();
                } else if (e.key === 'Escape') {
                  cancelEditing();
                }
              }}
              disabled={isRenaming}
            />
          ) : (
            <Tooltip title={folder.name}>
              <Typography variant="body1" noWrap sx={{ fontWeight: 600 }}>
                {folder.name}
              </Typography>
            </Tooltip>
          )}
        </Stack>
        <Stack direction="row" sx={{ gap: 0.5 }}>
          {isEditing ? (
            <>
              <Tooltip title="Save">
                <IconButton
                  size="small"
                  disabled={isRenaming}
                  onClick={(e) => {
                    e.stopPropagation();
                    handleSave();
                  }}
                  aria-label="Save"
                  sx={actionIconButtonSx(theme.palette.success.main)}
                >
                  <CheckOutlinedIcon fontSize="small" />
                </IconButton>
              </Tooltip>
              <Tooltip title="Cancel">
                <IconButton
                  size="small"
                  disabled={isRenaming}
                  onClick={(e) => {
                    e.stopPropagation();
                    cancelEditing();
                  }}
                  aria-label="Cancel"
                  sx={actionIconButtonSx(theme.palette.text.secondary)}
                >
                  <CloseOutlinedIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            </>
          ) : (
            <>
              <Tooltip title="Open">
                <IconButton
                  size="small"
                  onClick={(e) => {
                    e.stopPropagation();
                    onOpen(folder.id);
                  }}
                  aria-label="Open"
                  sx={actionIconButtonSx(folderAccentColor)}
                >
                  <ArrowForwardIcon fontSize="small" />
                </IconButton>
              </Tooltip>
              <Tooltip title="Rename">
                <IconButton
                  size="small"
                  onClick={(e) => {
                    e.stopPropagation();
                    startEditing();
                  }}
                  aria-label="Rename"
                  sx={actionIconButtonSx(folderAccentColor)}
                >
                  <EditOutlinedIcon fontSize="small" />
                </IconButton>
              </Tooltip>
              <Tooltip title="Delete">
                <IconButton
                  size="small"
                  onClick={(e) => {
                    e.stopPropagation();
                    setConfirmOpen(true);
                  }}
                  aria-label="Delete"
                  sx={actionIconButtonSx(theme.palette.error.main)}
                >
                  <DeleteOutlineIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            </>
          )}
        </Stack>
      </GlassSurface>

      <ConfirmDialog
        open={isConfirmOpen}
        title="Delete this folder?"
        description={
          <Typography variant="body2" color="text.secondary">
            "{folder.name}" and all its subfolders will be permanently deleted, along with every
            document inside them. Are you sure you want to proceed?
          </Typography>
        }
        isConfirming={isDeleting}
        onConfirm={handleDelete}
        onClose={() => setConfirmOpen(false)}
      />
    </>
  );
}
