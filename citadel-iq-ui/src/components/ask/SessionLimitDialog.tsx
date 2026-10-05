import { Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle } from '@mui/material';

interface SessionLimitDialogProps {
  open: boolean;
  onConfirm: () => void;
}

export function SessionLimitDialog({ open, onConfirm }: SessionLimitDialogProps) {
  return (
    <Dialog open={open} aria-labelledby="session-limit-title">
      <DialogTitle id="session-limit-title">Session limit reached</DialogTitle>
      <DialogContent>
        <DialogContentText>
          This conversation has reached its length limit, so this session will be reloaded. Your documents are not affected.
        </DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button variant="contained" onClick={onConfirm} autoFocus>
          OK
        </Button>
      </DialogActions>
    </Dialog>
  );
}
