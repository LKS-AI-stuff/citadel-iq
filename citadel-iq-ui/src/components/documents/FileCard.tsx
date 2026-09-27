import { Paper, Stack, Typography } from '@mui/material';
import DownloadOutlinedIcon from '@mui/icons-material/DownloadOutlined';
import { FileIcon } from './FileIcon';
import { ProcessingStatusChip } from './ProcessingStatusChip';
import { CardActionsMenu } from '../common/CardActionsMenu';
import { documentsApi } from '../../api/documentsApi';
import type { DocumentSummaryDto } from '../../types/folder';

interface FileCardProps {
  document: DocumentSummaryDto;
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function FileCard({ document }: FileCardProps) {
  const isReady = document.status === 'Ready';

  const handleDownload = () => {
    window.open(documentsApi.getDownloadUrl(document.id), '_blank');
  };

  return (
    <Paper
      variant="outlined"
      onClick={isReady ? handleDownload : undefined}
      sx={{
        p: 2,
        display: 'flex',
        alignItems: 'center',
        gap: 1.5,
        opacity: isReady ? 1 : 0.85,
        cursor: isReady ? 'pointer' : 'default',
        transition: 'border-color 0.15s ease, background-color 0.15s ease',
        ...(isReady && { '&:hover': { borderColor: 'primary.main', bgcolor: 'action.hover' } }),
      }}
    >
      <FileIcon fileName={document.fileName} sx={{ fontSize: 32 }} />
      <Stack spacing={0.5} sx={{ flex: 1, minWidth: 0 }}>
        <Typography variant="body1" noWrap sx={{ fontWeight: 500 }}>
          {document.fileName}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {formatSize(document.sizeBytes)}
        </Typography>
      </Stack>
      <ProcessingStatusChip status={document.status} failureReason={document.failureReason} />
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
    </Paper>
  );
}
