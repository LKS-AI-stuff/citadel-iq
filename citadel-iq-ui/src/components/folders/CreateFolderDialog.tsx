import { useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
} from '@mui/material';
import CreateNewFolderOutlinedIcon from '@mui/icons-material/CreateNewFolderOutlined';
import { useCreateFolder } from '../../hooks/useCreateFolder';
import { useToast } from '../common/ToastProvider';

interface CreateFolderDialogProps {
  open: boolean;
  parentFolderId: string;
  onClose: () => void;
  onCreated: () => void;
}

export function CreateFolderDialog({ open, parentFolderId, onClose, onCreated }: CreateFolderDialogProps) {
  const [name, setName] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);
  const { createFolder, isCreating } = useCreateFolder();
  const { showToast } = useToast();

  const handleClose = () => {
    setName('');
    setValidationError(null);
    onClose();
  };

  const handleSubmit = async () => {
    if (!name.trim()) {
      setValidationError('Folder name cannot be empty.');
      return;
    }

    try {
      await createFolder(parentFolderId, name.trim());
      showToast(`Folder "${name.trim()}" created.`, 'success');
      handleClose();
      onCreated();
    } catch (err) {
      showToast(err instanceof Error ? err.message : 'Unable to create the folder.', 'error');
    }
  };

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="xs">
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <CreateNewFolderOutlinedIcon color="primary" fontSize="small" />
        New folder
      </DialogTitle>
      <DialogContent>
        <TextField
          autoFocus
          fullWidth
          margin="dense"
          label="Folder name"
          value={name}
          error={Boolean(validationError)}
          helperText={validationError}
          onChange={(e) => {
            setName(e.target.value);
            setValidationError(null);
          }}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              handleSubmit();
            }
          }}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>Cancel</Button>
        <Button onClick={handleSubmit} variant="contained" disabled={isCreating}>
          Create
        </Button>
      </DialogActions>
    </Dialog>
  );
}
