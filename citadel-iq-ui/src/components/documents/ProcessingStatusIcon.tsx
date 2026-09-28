import { CircularProgress, Tooltip } from '@mui/material';
import CheckCircleRoundedIcon from '@mui/icons-material/CheckCircleRounded';
import ErrorRoundedIcon from '@mui/icons-material/ErrorRounded';
import type { ProcessingStatus } from '../../types/folder';

const LABELS: Record<ProcessingStatus, string> = {
  Uploaded: 'Uploading…',
  ExtractingText: 'Extracting text…',
  Chunking: 'Creating searchable sections…',
  GeneratingEmbeddings: 'Generating embeddings…',
  Ready: 'Ready to search',
  Failed: 'Processing failed',
};

interface ProcessingStatusIconProps {
  status: ProcessingStatus;
  failureReason?: string | null;
}

/** Compact, icon-only processing indicator — a spinner while a document is being processed,
 * swapped for a checkmark once embeddings are ready (or an error icon on failure). Replaces a
 * full text chip so file cards stay as simple as folder cards. */
export function ProcessingStatusIcon({ status, failureReason }: ProcessingStatusIconProps) {
  if (status === 'Ready') {
    return (
      <Tooltip title={LABELS.Ready}>
        <CheckCircleRoundedIcon sx={{ fontSize: 15, color: 'success.main' }} />
      </Tooltip>
    );
  }

  if (status === 'Failed') {
    return (
      <Tooltip title={failureReason ?? LABELS.Failed}>
        <ErrorRoundedIcon sx={{ fontSize: 15, color: 'error.main' }} />
      </Tooltip>
    );
  }

  return (
    <Tooltip title={LABELS[status]}>
      <CircularProgress size={12} thickness={6} />
    </Tooltip>
  );
}
