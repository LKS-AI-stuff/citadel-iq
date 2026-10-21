import { useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  ButtonBase,
  Chip,
  CircularProgress,
  Link,
  Stack,
  TextField,
  Typography,
  alpha,
} from '@mui/material';
import SendRoundedIcon from '@mui/icons-material/SendRounded';
import { GlassSurface } from '../common/GlassSurface';
import { ConfirmDialog } from '../common/ConfirmDialog';
import { EmptyState } from '../common/EmptyState';
import { LoadingState } from '../common/LoadingState';
import { FileIcon } from '../documents/FileIcon';
import { useToast } from '../common/ToastProvider';
import { useDraft } from '../../hooks/useDrafts';
import { REASON_LABELS, documentLink, formatAge, formatDateTime } from './emailLabels';
import type { EmailDraftDto, EmailDraftSummaryDto } from '../../types/email';

interface DraftReviewProps {
  drafts: EmailDraftSummaryDto[] | null;
  error: string | null;
  onSend: (id: string, text: string) => Promise<void>;
  onDismiss: (id: string) => Promise<void>;
  /** Called after a send/dismiss so counts elsewhere refresh. */
  onHandled: () => void;
}

/** The review queue: pending drafts on the left, the selected draft's question, editable reply and sources on the right. */
export function DraftReview({ drafts, error, onSend, onDismiss, onHandled }: DraftReviewProps) {
  const [chosenId, setSelectedId] = useState<string | null>(null);
  // A valid selection: the chosen draft while it's still pending, otherwise the first one.
  const selectedId = drafts?.some((d) => d.id === chosenId) ? chosenId : (drafts?.[0]?.id ?? null);

  if (error) return <Alert severity="error">{error}</Alert>;
  if (!drafts) return <LoadingState />;
  if (drafts.length === 0) {
    return (
      <EmptyState
        dense
        title="No drafts waiting"
        description="When an emailed question can't be answered confidently, a draft reply appears here for review."
      />
    );
  }

  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'minmax(240px, 320px) 1fr' }, gap: 2 }}>
      <Stack spacing={1} component="ul" aria-label="Drafts awaiting review" sx={{ listStyle: 'none', p: 0, m: 0 }}>
        {drafts.map((d) => (
          <Box component="li" key={d.id}>
            <ButtonBase
              onClick={() => setSelectedId(d.id)}
              aria-current={d.id === selectedId ? 'true' : undefined}
              sx={{ width: '100%', textAlign: 'left', borderRadius: '14px', display: 'block' }}
            >
              <GlassSurface
                radius={14}
                hover
                sx={{
                  p: 1.5,
                  boxShadow: d.id === selectedId ? (t) => `0 0 0 2px ${t.palette.primary.main}` : undefined,
                }}
              >
                <Typography variant="body2" noWrap sx={{ fontWeight: 700 }}>
                  {d.subject || '(no subject)'}
                </Typography>
                <Typography variant="caption" color="text.secondary" noWrap component="p">
                  {d.asker.displayName} · {formatAge(d.createdAtUtc)}
                </Typography>
                <Chip size="small" label={REASON_LABELS[d.reason]} sx={{ mt: 0.75 }} />
              </GlassSurface>
            </ButtonBase>
          </Box>
        ))}
      </Stack>

      {selectedId && <DraftDetail key={selectedId} id={selectedId} onSend={onSend} onDismiss={onDismiss} onHandled={onHandled} />}
    </Box>
  );
}

function DraftDetail({
  id,
  onSend,
  onDismiss,
  onHandled,
}: {
  id: string;
  onSend: (id: string, text: string) => Promise<void>;
  onDismiss: (id: string) => Promise<void>;
  onHandled: () => void;
}) {
  const { draft, error } = useDraft(id);

  if (error) return <Alert severity="error">{error}</Alert>;
  if (!draft) return <LoadingState />;
  return <DraftEditor draft={draft} onSend={onSend} onDismiss={onDismiss} onHandled={onHandled} />;
}

/** Rendered once the draft has loaded, so the reply box starts from the suggestion without an effect. */
function DraftEditor({
  draft,
  onSend,
  onDismiss,
  onHandled,
}: {
  draft: EmailDraftDto;
  onSend: (id: string, text: string) => Promise<void>;
  onDismiss: (id: string) => Promise<void>;
  onHandled: () => void;
}) {
  const id = draft.id;
  const { showToast } = useToast();
  const [text, setText] = useState(draft.suggestedText ?? '');
  const [isSending, setIsSending] = useState(false);
  const [isDismissOpen, setDismissOpen] = useState(false);
  const [isDismissing, setIsDismissing] = useState(false);

  const send = async () => {
    setIsSending(true);
    try {
      await onSend(id, text);
      showToast(`Reply sent to ${draft.asker.displayName}.`, 'success');
      onHandled();
    } catch (err) {
      showToast(err instanceof Error ? err.message : 'Unable to send this reply.', 'error');
    } finally {
      setIsSending(false);
    }
  };

  const dismiss = async () => {
    setIsDismissing(true);
    try {
      await onDismiss(id);
      setDismissOpen(false);
      showToast('Draft dismissed.', 'success');
      onHandled();
    } catch (err) {
      showToast(err instanceof Error ? err.message : 'Unable to dismiss this draft.', 'error');
    } finally {
      setIsDismissing(false);
    }
  };

  return (
    <GlassSurface radius={16} sx={{ p: { xs: 2, sm: 2.5 } }} aria-label="Selected draft">
      <Typography variant="h6" component="h2" sx={{ overflowWrap: 'anywhere' }}>
        {draft.subject || '(no subject)'}
      </Typography>
      <Typography variant="caption" color="text.secondary" component="p" sx={{ mb: 2 }}>
        From {draft.asker.displayName} · received {formatDateTime(draft.receivedAtUtc)} · {REASON_LABELS[draft.reason]}
        {draft.confidence != null && ` · confidence ${Math.round(draft.confidence * 100)}%`}
      </Typography>

      <Typography variant="overline" color="text.secondary">
        Question
      </Typography>
      <Box
        sx={{
          borderLeft: '2px solid',
          borderColor: (t) => alpha(t.palette.primary.main, 0.35),
          pl: 1.5,
          mb: 2,
          whiteSpace: 'pre-wrap',
        }}
      >
        <Typography variant="body2">{draft.question ?? '(The question could not be read.)'}</Typography>
      </Box>

      {draft.missing && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Possibly missing: {draft.missing}
        </Alert>
      )}

      <TextField
        label="Reply"
        multiline
        minRows={6}
        fullWidth
        value={text}
        onChange={(e) => setText(e.target.value)}
        placeholder="Write the reply the asker will receive."
        helperText="Sent from the support address in the original email thread. [n] markers refer to the sources below."
      />

      {draft.sources.length > 0 && (
        <Box sx={{ mt: 2 }}>
          <Typography variant="overline" color="text.secondary">
            Documents used
          </Typography>
          <Stack spacing={0.75}>
            {draft.sources.map((s) => (
              <Stack key={s.number} direction="row" sx={{ alignItems: 'center', gap: 1 }}>
                <Typography variant="caption" sx={{ fontWeight: 800, color: 'text.disabled', minWidth: 24 }}>
                  [{s.number}]
                </Typography>
                <FileIcon fileName={s.fileName} sx={{ fontSize: 18, color: 'success.main' }} />
                <Link component={RouterLink} to={documentLink(s.folderId, s.documentId)} variant="body2" noWrap sx={{ minWidth: 0 }}>
                  {s.fileName}
                </Link>
                {s.page != null && <Chip size="small" variant="outlined" label={`Page ${s.page}`} />}
                {s.sheet != null && <Chip size="small" variant="outlined" label={`Sheet: ${s.sheet}`} />}
                {s.cited && <Chip size="small" color="success" label="Cited" />}
              </Stack>
            ))}
          </Stack>
        </Box>
      )}

      <Stack direction="row" sx={{ gap: 1, mt: 2.5, justifyContent: 'flex-end', flexWrap: 'wrap' }}>
        <Button variant="outlined" color="error" onClick={() => setDismissOpen(true)} disabled={isSending}>
          Dismiss
        </Button>
        <Button
          variant="contained"
          startIcon={isSending ? <CircularProgress size={16} color="inherit" /> : <SendRoundedIcon />}
          onClick={send}
          disabled={isSending || text.trim().length === 0}
        >
          Send reply
        </Button>
      </Stack>

      <ConfirmDialog
        open={isDismissOpen}
        title="Dismiss this draft?"
        description={
          <Typography variant="body2" color="text.secondary">
            No reply will be sent to {draft.asker.displayName}. The email stays in the activity list.
          </Typography>
        }
        confirmLabel="Dismiss"
        isConfirming={isDismissing}
        onConfirm={dismiss}
        onClose={() => setDismissOpen(false)}
      />
    </GlassSurface>
  );
}
