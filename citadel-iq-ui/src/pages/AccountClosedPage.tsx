import { StandalonePage } from '../components/account/StandalonePage';
import { SignOutButton } from '../components/account/SignOutButton';

export function AccountClosedPage() {
  return (
    <StandalonePage
      title="This account has been closed"
      subtitle="You left your organization or were removed from it, so this account can no longer be used. Documents you uploaded stay with the organization. To use CitadelIQ again, sign up with a different email address."
    >
      <SignOutButton variant="contained" />
    </StandalonePage>
  );
}
