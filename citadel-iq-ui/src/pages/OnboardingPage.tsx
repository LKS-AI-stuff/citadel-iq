import { useState, type ReactNode } from 'react';
import { Alert, Box, Button, Stack, TextField, Typography, alpha } from '@mui/material';
import PersonOutlineRoundedIcon from '@mui/icons-material/PersonOutlineRounded';
import ApartmentRoundedIcon from '@mui/icons-material/ApartmentRounded';
import GroupAddOutlinedIcon from '@mui/icons-material/GroupAddOutlined';
import { StandalonePage } from '../components/account/StandalonePage';
import { SignOutButton } from '../components/account/SignOutButton';
import { GlassSurface } from '../components/common/GlassSurface';
import { IconBadge } from '../components/common/IconBadge';
import { ApiError } from '../api/apiClient';
import { sessionApi } from '../api/sessionApi';
import { useSession } from '../session/useSession';
import type { SessionDto } from '../types/session';

type Choice = 'individual' | 'organization' | 'join';

interface ChoiceCardProps {
  icon: ReactNode;
  title: string;
  description: string;
  children: ReactNode;
}

function ChoiceCard({ icon, title, description, children }: ChoiceCardProps) {
  return (
    <GlassSurface radius={18} sx={{ p: 2.5, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <IconBadge color="#6366f1">{icon}</IconBadge>
      <Typography variant="h6" sx={{ fontWeight: 700 }}>
        {title}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ flex: 1 }}>
        {description}
      </Typography>
      {children}
    </GlassSurface>
  );
}

/** First sign-in: choose an individual workspace, create an organization, or ask to join one with its code. */
export function OnboardingPage() {
  const { session, setSession } = useSession();
  const [organizationName, setOrganizationName] = useState('');
  const [joinCode, setJoinCode] = useState('');
  const [busy, setBusy] = useState<Choice | null>(null);
  const [error, setError] = useState<string | null>(null);

  const run = async (choice: Choice, action: () => Promise<SessionDto>) => {
    setBusy(choice);
    setError(null);
    try {
      setSession(await action());
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.');
    } finally {
      setBusy(null);
    }
  };

  return (
    <StandalonePage
      wide
      title={`Welcome, ${session?.user.displayName ?? ''}`}
      subtitle="How will you use CitadelIQ? You can belong to one workspace."
    >
      <Stack spacing={2.5}>
        {session?.lastRejectedOrganizationName && (
          <Alert severity="info">
            Your request to join {session.lastRejectedOrganizationName} was declined. You can choose again below.
          </Alert>
        )}
        {error && <Alert severity="error">{error}</Alert>}

        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' }, gap: 2 }}>
          <ChoiceCard
            icon={<PersonOutlineRoundedIcon sx={{ color: '#6366f1' }} />}
            title="Just me"
            description="A private workspace with your own folders. Nobody else can see it."
          >
            <Button
              variant="contained"
              disabled={busy !== null}
              onClick={() => run('individual', sessionApi.createIndividual)}
            >
              {busy === 'individual' ? 'Creating…' : 'Create my workspace'}
            </Button>
          </ChoiceCard>

          <ChoiceCard
            icon={<ApartmentRoundedIcon sx={{ color: '#6366f1' }} />}
            title="New organization"
            description="A shared workspace for your team. You become its Owner and approve who joins."
          >
            <TextField
              size="small"
              label="Organization name"
              value={organizationName}
              onChange={(e) => setOrganizationName(e.target.value)}
              slotProps={{ htmlInput: { maxLength: 100 } }}
            />
            <Button
              variant="contained"
              disabled={busy !== null || organizationName.trim().length < 2}
              onClick={() => run('organization', () => sessionApi.createOrganization(organizationName.trim()))}
            >
              {busy === 'organization' ? 'Creating…' : 'Create organization'}
            </Button>
          </ChoiceCard>

          <ChoiceCard
            icon={<GroupAddOutlinedIcon sx={{ color: '#6366f1' }} />}
            title="Join an organization"
            description="Enter the join code an Owner or Admin gave you. They approve your request."
          >
            <TextField
              size="small"
              label="Join code"
              placeholder="XXXX-XXXX-XXXX"
              value={joinCode}
              onChange={(e) => setJoinCode(e.target.value)}
              slotProps={{ htmlInput: { maxLength: 20, style: { fontFamily: 'monospace', letterSpacing: '0.08em' } } }}
            />
            <Button
              variant="contained"
              disabled={busy !== null || joinCode.trim().length === 0}
              onClick={() => run('join', () => sessionApi.requestToJoin(joinCode.trim()))}
            >
              {busy === 'join' ? 'Sending…' : 'Request to join'}
            </Button>
          </ChoiceCard>
        </Box>

        <Box sx={{ borderTop: '1px solid', borderColor: (t) => alpha(t.palette.text.primary, 0.1), pt: 2 }}>
          <SignOutButton size="small" />
        </Box>
      </Stack>
    </StandalonePage>
  );
}
