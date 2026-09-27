import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  LinearProgress,
  Stack,
  Typography,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import { UploadDropzone } from './UploadDropzone';
import { useUpload } from '../../hooks/useUpload';
import type { DocumentSummaryDto } from '../../types/folder';

interface UploadDialogProps {
  open: boolean;
  folderId: string;
  onClose: () => void;
  onUploaded: (document: DocumentSummaryDto) => void;
}

export function UploadDialog({ open, folderId, onClose, onUploaded }: UploadDialogProps) {
  const { uploads, uploadFiles, cancelUpload, clear } = useUpload(folderId, onUploaded);

  const handleClose = () => {
    clear();
    onClose();
  };

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
      <DialogTitle>Upload documents</DialogTitle>
      <DialogContent>
        <Stack spacing={2}>
          <UploadDropzone onFilesSelected={uploadFiles} />

          {uploads.length > 0 && (
            <Stack spacing={1.5}>
              {uploads.map((upload) => (
                <Stack key={upload.id} spacing={0.5}>
                  <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
                    <Typography variant="body2" noWrap sx={{ maxWidth: 300 }}>
                      {upload.fileName}
                    </Typography>
                    {upload.status === 'uploading' && (
                      <IconButton size="small" onClick={() => cancelUpload(upload.id)} aria-label="Cancel upload">
                        <CloseIcon fontSize="small" />
                      </IconButton>
                    )}
                  </Stack>
                  {upload.status === 'uploading' && <LinearProgress variant="determinate" value={upload.progress} />}
                  {upload.status === 'done' && (
                    <Alert severity="success" sx={{ py: 0 }}>
                      Uploaded — processing in the background.
                    </Alert>
                  )}
                  {upload.status === 'error' && (
                    <Alert severity="error" sx={{ py: 0 }}>
                      {upload.errorMessage}
                    </Alert>
                  )}
                </Stack>
              ))}
            </Stack>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>Done</Button>
      </DialogActions>
    </Dialog>
  );
}
