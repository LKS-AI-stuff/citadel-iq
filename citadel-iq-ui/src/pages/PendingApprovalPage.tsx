import { useState } from 'react';
import { Alert, Button, Stack } from '@mui/material';
import { StandalonePage } from '../components/account/StandalonePage';
import { SignOutButton } from '../components/account/SignOutButton';
import { ApiError } from '../api/apiClient';
import { sessionApi } from '../api/sessionApi';
import { useSession } from '../session/useSession';

export function PendingApprovalPage() {
  const { session, setSession, refresh } = useSession();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const organization = session?.pendingJoinRequest?.organizationName ?? 'the organization';

  const cancel = async () => {
    setBusy(true);
    setError(null);
    try {
      setSession(await sessionApi.cancelJoinRequest());
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to cancel the request right now.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <StandalonePage
      title="Waiting for approval"
      subtitle={`Your request to join ${organization} has been sent. An Owner or Admin of ${organization} needs to approve it before you can see its documents.`}
    >
      <Stack spacing={2}>
        {error && <Alert severity="error">{error}</Alert>}
        <Stack direction="row" sx={{ gap: 1, flexWrap: 'wrap' }}>
          <Button variant="contained" onClick={() => refresh()} disabled={busy}>
            Check again
          </Button>
          <Button variant="outlined" color="error" onClick={cancel} disabled={busy}>
            Cancel request
          </Button>
          <SignOutButton />
        </Stack>
      </Stack>
    </StandalonePage>
  );
}
