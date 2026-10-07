import { Alert, Button, Stack } from '@mui/material';
import LoginRoundedIcon from '@mui/icons-material/LoginRounded';
import { StandalonePage } from '../components/account/StandalonePage';
import { sessionApi } from '../api/sessionApi';

export function SignInPage() {
  const signInFailed = new URLSearchParams(window.location.search).get('signin') === 'failed';
  const returnUrl = window.location.pathname === '/' ? '/' : window.location.pathname + window.location.search;

  return (
    <StandalonePage
      title="Ask your documents"
      subtitle="Organize documents in folders, search them by meaning, and get short answers with citations — privately, or shared with your organization."
    >
      <Stack spacing={2}>
        {signInFailed && <Alert severity="error">Sign-in didn't complete. Please try again.</Alert>}
        <Button
          variant="contained"
          size="large"
          startIcon={<LoginRoundedIcon />}
          href={sessionApi.signInUrl(signInFailed ? '/' : returnUrl)}
        >
          Sign in or create an account
        </Button>
      </Stack>
    </StandalonePage>
  );
}
