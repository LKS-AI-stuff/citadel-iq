import { Alert, Box, Typography } from '@mui/material';
import MailOutlineRoundedIcon from '@mui/icons-material/MailOutlineRounded';
import { useEmailStatus } from '../../hooks/useEmailStatus';

const REASONS: Record<'NoEmail' | 'SharedAddress', string> = {
  NoEmail: "Your account has no email address, so emailed questions can't be matched to you.",
  SharedAddress: "Another account uses your email address, so emailed questions can't be matched to you.",
};

/** "Email questions about your documents to … from …" — hidden while the email agent is off. */
export function EmailSupportLine({ dense = false }: { dense?: boolean }) {
  const status = useEmailStatus();
  if (!status?.supportAddress) {
    return null;
  }

  return (
    <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
      <MailOutlineRoundedIcon sx={{ fontSize: 18, color: 'primary.main', mt: '2px', flexShrink: 0 }} aria-hidden />
      <Box sx={{ minWidth: 0 }}>
        <Typography variant={dense ? 'caption' : 'body2'} component="p" sx={{ overflowWrap: 'anywhere' }}>
          Email questions about your documents to <strong>{status.supportAddress}</strong>
          {status.canUse && (
            <>
              {' '}
              from <strong>{status.yourAddress}</strong>
            </>
          )}
          .
        </Typography>
        <Typography variant="caption" color="text.secondary" component="p" sx={{ mt: 0.25 }}>
          Email content is processed by OpenAI. Mail must come from a properly configured mail domain.
        </Typography>
        {!status.canUse && status.reason && (
          <Alert severity="warning" sx={{ mt: 1, py: 0 }}>
            {REASONS[status.reason]}
          </Alert>
        )}
      </Box>
    </Box>
  );
}
