import { Chip, CircularProgress, Tooltip } from '@mui/material';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutlineOutlined';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutlineOutlined';
import type { ProcessingStatus } from '../../types/folder';

const LABELS: Record<ProcessingStatus, string> = {
  Uploaded: 'Uploading…',
  ExtractingText: 'Extracting text…',
  Chunking: 'Creating searchable sections…',
  GeneratingEmbeddings: 'Generating embeddings…',
  Ready: 'Ready to search',
  Failed: 'Processing failed',
};

interface ProcessingStatusChipProps {
  status: ProcessingStatus;
  failureReason?: string | null;
}

export function ProcessingStatusChip({ status, failureReason }: ProcessingStatusChipProps) {
  if (status === 'Ready') {
    return <Chip size="small" color="success" icon={<CheckCircleOutlineIcon />} label={LABELS.Ready} />;
  }

  if (status === 'Failed') {
    return (
      <Tooltip title={failureReason ?? 'Something went wrong while processing this document.'}>
        <Chip size="small" color="error" icon={<ErrorOutlineIcon />} label={LABELS.Failed} />
      </Tooltip>
    );
  }

  return <Chip size="small" icon={<CircularProgress size={14} />} label={LABELS[status]} />;
}
