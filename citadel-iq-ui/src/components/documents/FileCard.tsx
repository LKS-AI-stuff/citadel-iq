import { useState } from 'react';
import { IconButton, Stack, Tooltip, Typography, useTheme } from '@mui/material';
import DownloadOutlinedIcon from '@mui/icons-material/DownloadOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import { FileIcon } from './FileIcon';
import { ProcessingStatusIcon } from './ProcessingStatusIcon';
import { ConfirmDialog } from '../common/ConfirmDialog';
import { GlassSurface } from '../common/GlassSurface';
import { IconBadge } from '../common/IconBadge';
import { useToast } from '../common/ToastProvider';
import { documentsApi } from '../../api/documentsApi';
import { useDeleteDocument } from '../../hooks/useDeleteDocument';
import { actionIconButtonSx } from '../../theme/glass';
import type { DocumentSummaryDto } from '../../types/folder';

interface FileCardProps {
  document: DocumentSummaryDto;
  animationDelayMs?: number;
  onDeleted?: () => void;
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

export function FileCard({ document, animationDelayMs = 0, onDeleted }: FileCardProps) {
  const isReady = document.status === 'Ready';
  const theme = useTheme();
  // One shared accent for every file type — matches the Download button's green — instead of a
  // color-per-extension palette.
  const fileAccentColor = theme.palette.success.main;
  const [isConfirmOpen, setConfirmOpen] = useState(false);
  const { deleteDocument, isDeleting } = useDeleteDocument();
  const { showToast } = useToast();

  const handleDownload = () => {
    window.open(documentsApi.getDownloadUrl(document.id), '_blank');
  };

  const handleDelete = async () => {
    try {
      await deleteDocument(document.id);
      showToast(`"${document.fileName}" was deleted.`, 'success');
      setConfirmOpen(false);
      onDeleted?.();
    } catch (err) {
      showToast(err instanceof Error ? err.message : 'Unable to delete this document.', 'error');
    }
  };

  return (
    <>
      <GlassSurface
        hover={isReady}
        radius={16}
        onClick={isReady ? handleDownload : undefined}
        className="animate-fade-in-up"
        sx={{
          p: 2,
          display: 'flex',
          alignItems: 'center',
          gap: 1.75,
          opacity: isReady ? 1 : 0.85,
          cursor: isReady ? 'pointer' : 'default',
          animationDelay: `${animationDelayMs}ms`,
          '&:hover .file-icon-badge': { transform: 'scale(1.08)' },
        }}
      >
        <IconBadge className="file-icon-badge" color={fileAccentColor}>
          <FileIcon fileName={document.fileName} sx={{ fontSize: 24, color: fileAccentColor }} />
        </IconBadge>
        <Stack sx={{ flex: 1, minWidth: 0 }}>
          <Tooltip title={document.fileName}>
            <Typography variant="body1" noWrap sx={{ fontWeight: 600 }}>
              {document.fileName}
            </Typography>
          </Tooltip>
          <Stack direction="row" sx={{ alignItems: 'center', gap: 0.6 }}>
            <ProcessingStatusIcon status={document.status} failureReason={document.failureReason} />
            <Typography variant="caption" color="text.secondary" noWrap>
              {formatSize(document.sizeBytes)} · {formatDate(document.uploadedAtUtc)}
            </Typography>
          </Stack>
        </Stack>
        <Stack direction="row" sx={{ gap: 0.5 }}>
          <Tooltip title={isReady ? 'Download' : 'Available once processing completes'}>
            <span>
              <IconButton
                size="small"
                disabled={!isReady}
                onClick={(e) => {
                  e.stopPropagation();
                  handleDownload();
                }}
                aria-label="Download"
                sx={actionIconButtonSx(fileAccentColor)}
              >
                <DownloadOutlinedIcon fontSize="small" />
              </IconButton>
            </span>
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
        </Stack>
      </GlassSurface>

      <ConfirmDialog
        open={isConfirmOpen}
        title="Delete this document?"
        description={
          <Typography variant="body2" color="text.secondary">
            "{document.fileName}" will be permanently deleted. Are you sure you want to proceed?
          </Typography>
        }
        isConfirming={isDeleting}
        onConfirm={handleDelete}
        onClose={() => setConfirmOpen(false)}
      />
    </>
  );
}
