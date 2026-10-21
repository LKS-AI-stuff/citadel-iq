import { useEffect, useState } from 'react';
import { Alert, Badge, Box, FormControlLabel, Stack, Switch, Tab, Tabs, Typography } from '@mui/material';
import { GlassSurface } from '../components/common/GlassSurface';
import { useToast } from '../components/common/ToastProvider';
import { DraftReview } from '../components/email/DraftReview';
import { EmailActivityList } from '../components/email/EmailActivityList';
import { EmailSupportLine } from '../components/email/EmailSupportLine';
import { emailApi } from '../api/emailApi';
import { ApiError } from '../api/apiClient';
import { useDrafts } from '../hooks/useDrafts';
import { useEmailActivity } from '../hooks/useEmailActivity';
import { useActiveWorkspace } from '../session/useSession';

type EmailTab = 'review' | 'activity';

/** Emailed questions: the review queue (Owners/Admins of organizations) and email activity (everyone — Members see
 * only their own emails). The server enforces both. */
export function EmailPage() {
  const { can } = useActiveWorkspace();
  const [tab, setTab] = useState<EmailTab>(can.administer ? 'review' : 'activity');

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h4" component="h1" sx={{ fontWeight: 800 }}>
          {can.administer ? 'Email review' : 'My emails'}
        </Typography>
        <Box sx={{ mt: 1 }}>
          <EmailSupportLine />
        </Box>
      </Box>

      {can.administer ? (
        <>
          <Tabs value={tab} onChange={(_, value: EmailTab) => setTab(value)} aria-label="Email sections">
            <Tab value="review" label="Review queue" id="email-tab-review" aria-controls="email-panel-review" />
            <Tab value="activity" label="Activity" id="email-tab-activity" aria-controls="email-panel-activity" />
          </Tabs>
          <Box role="tabpanel" id={`email-panel-${tab}`} aria-labelledby={`email-tab-${tab}`}>
            {tab === 'review' ? <ReviewQueue /> : <Activity detailed />}
          </Box>
        </>
      ) : (
        <Activity detailed={false} />
      )}
    </Stack>
  );
}

function ReviewQueue() {
  const { drafts, error, refetch, send, dismiss } = useDrafts();

  return (
    <Stack spacing={2}>
      <NotificationPreference />
      <GlassSurface component="section" aria-label="Drafts awaiting review" sx={{ p: { xs: 2, sm: 3 } }}>
        <Stack direction="row" sx={{ alignItems: 'center', gap: 1.5, mb: 2 }}>
          <Badge color="warning" badgeContent={drafts?.length ?? 0} max={99}>
            <Typography variant="h5">Drafts awaiting review</Typography>
          </Badge>
        </Stack>
        <DraftReview drafts={drafts} error={error} onSend={send} onDismiss={dismiss} onHandled={refetch} />
      </GlassSurface>
    </Stack>
  );
}

function NotificationPreference() {
  const { showToast } = useToast();
  const [enabled, setEnabled] = useState<boolean | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    emailApi
      .notificationPreference()
      .then((p) => setEnabled(p.emailOnNewDrafts))
      .catch(() => setEnabled(null));
  }, []);

  const toggle = async (value: boolean) => {
    setIsSaving(true);
    try {
      await emailApi.setNotificationPreference(value);
      setEnabled(value);
      showToast(value ? "You'll get an email when drafts are waiting." : "You won't get emails about drafts.", 'success');
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Unable to save this setting right now.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  if (enabled === null) return null;

  return (
    <FormControlLabel
      control={<Switch checked={enabled} onChange={(e) => toggle(e.target.checked)} disabled={isSaving} />}
      label="Email me when new drafts are waiting (at most one email every few minutes)"
    />
  );
}

function Activity({ detailed }: { detailed: boolean }) {
  const [mine, setMine] = useState(false);
  const { items, hasMore, isLoadingMore, error, loadMore } = useEmailActivity(detailed ? mine : true);

  return (
    <GlassSurface component="section" aria-label="Email activity" sx={{ p: { xs: 2, sm: 3 } }}>
      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1, mb: 2 }}>
        <Typography variant="h5">{detailed ? 'Email activity' : 'Your emailed questions'}</Typography>
        {detailed && (
          <FormControlLabel control={<Switch checked={mine} onChange={(e) => setMine(e.target.checked)} />} label="Only my emails" />
        )}
      </Stack>
      {!detailed && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Replies arrive in your inbox. This list shows what happened to each email you sent.
        </Alert>
      )}
      <EmailActivityList
        items={items}
        error={error}
        hasMore={hasMore}
        isLoadingMore={isLoadingMore}
        onLoadMore={loadMore}
        detailed={detailed}
      />
    </GlassSurface>
  );
}
