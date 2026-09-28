import { Stack, Tooltip, Typography } from '@mui/material';
import DownloadOutlinedIcon from '@mui/icons-material/DownloadOutlined';
import { FileIcon, getFileAccentColor } from './FileIcon';
import { ProcessingStatusIcon } from './ProcessingStatusIcon';
import { CardActionsMenu } from '../common/CardActionsMenu';
import { GlassSurface } from '../common/GlassSurface';
import { IconBadge } from '../common/IconBadge';
import { documentsApi } from '../../api/documentsApi';
import type { DocumentSummaryDto } from '../../types/folder';

interface FileCardProps {
  document: DocumentSummaryDto;
  animationDelayMs?: number;
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

export function FileCard({ document, animationDelayMs = 0 }: FileCardProps) {
  const isReady = document.status === 'Ready';
  const accentColor = getFileAccentColor(document.fileName);

  const handleDownload = () => {
    window.open(documentsApi.getDownloadUrl(document.id), '_blank');
  };

  return (
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
      <IconBadge className="file-icon-badge" color={accentColor}>
        <FileIcon fileName={document.fileName} sx={{ fontSize: 24 }} />
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
      <CardActionsMenu
        actions={[
          {
            label: 'Download',
            icon: <DownloadOutlinedIcon fontSize="small" />,
            onClick: handleDownload,
            disabled: !isReady,
            disabledReason: 'Available once processing completes',
          },
        ]}
      />
    </GlassSurface>
  );
}
