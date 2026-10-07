import { Button, type ButtonProps } from '@mui/material';
import { sessionApi } from '../../api/sessionApi';

/**
 * Sign-out is a real form post (not fetch) so the browser can follow the server's redirect to the identity provider's
 * end-session page. A form cannot send the CSRF header; the server accepts it because the post is same-origin.
 */
export function SignOutButton(props: ButtonProps) {
  return (
    <form method="post" action={sessionApi.signOutAction} style={{ display: 'inline' }}>
      <Button type="submit" variant="outlined" {...props}>
        {props.children ?? 'Sign out'}
      </Button>
    </form>
  );
}
