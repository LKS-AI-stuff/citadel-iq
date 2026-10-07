import { Alert, Box, Button, CircularProgress } from '@mui/material';
import type { ReactNode } from 'react';
import { useSession } from '../session/useSession';
import { SignInPage } from '../pages/SignInPage';
import { OnboardingPage } from '../pages/OnboardingPage';
import { PendingApprovalPage } from '../pages/PendingApprovalPage';
import { AccountClosedPage } from '../pages/AccountClosedPage';

/** Shows the page that matches the session; the app itself only renders for an active workspace member. */
export function SessionGate({ children }: { children: ReactNode }) {
  const { state, session, refresh } = useSession();

  if (state === 'loading') {
    return (
      <Box sx={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <CircularProgress aria-label="Loading" />
      </Box>
    );
  }

  if (state === 'error') {
    return (
      <Box sx={{ maxWidth: 480, mx: 'auto', mt: '20vh', px: 2 }}>
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => refresh()}>
              Retry
            </Button>
          }
        >
          CitadelIQ is unreachable right now.
        </Alert>
      </Box>
    );
  }

  if (state === 'signedOut' || !session) {
    return <SignInPage />;
  }

  switch (session.status) {
    case 'NeedsOnboarding':
      return <OnboardingPage />;
    case 'PendingApproval':
      return <PendingApprovalPage />;
    case 'Closed':
      return <AccountClosedPage />;
    default:
      return <>{children}</>;
  }
}
