import { Link as RouterLink } from 'react-router-dom';
import { Alert, Box, Button, Chip, Link, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import { EmptyState } from '../common/EmptyState';
import { LoadingState } from '../common/LoadingState';
import { DELIVERY_LABELS, OUTCOME_LABELS, REASON_LABELS, documentLink, formatDateTime } from './emailLabels';
import type { EmailActivityDto } from '../../types/email';

interface EmailActivityListProps {
  items: EmailActivityDto[] | null;
  error: string | null;
  hasMore: boolean;
  isLoadingMore: boolean;
  onLoadMore: () => void;
  /** Admins see everyone's emails: show the sender, confidence and documents columns. */
  detailed: boolean;
}

export function EmailActivityList({ items, error, hasMore, isLoadingMore, onLoadMore, detailed }: EmailActivityListProps) {
  if (error && !items) return <Alert severity="error">{error}</Alert>;
  if (!items) return <LoadingState />;
  if (items.length === 0) {
    return <EmptyState dense title="No emails yet" description="Questions emailed to the support address will be listed here." />;
  }

  return (
    <Box>
      {error && (
        <Alert severity="error" sx={{ mb: 1 }}>
          {error}
        </Alert>
      )}
      <Box sx={{ overflowX: 'auto' }}>
        <Table size="small" aria-label="Email activity">
          <TableHead>
            <TableRow>
              <TableCell>Received</TableCell>
              {detailed && <TableCell>Sender</TableCell>}
              <TableCell>Subject</TableCell>
              <TableCell>Outcome</TableCell>
              {detailed && <TableCell>Confidence</TableCell>}
              {detailed && <TableCell>Documents</TableCell>}
              <TableCell>Reply</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {items.map((item) => {
              const outcome = OUTCOME_LABELS[item.outcome];
              return (
                <TableRow key={item.id}>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDateTime(item.receivedAtUtc)}</TableCell>
                  {detailed && <TableCell>{item.sender.displayName}</TableCell>}
                  <TableCell sx={{ maxWidth: 280, overflowWrap: 'anywhere' }}>{item.subject || '(no subject)'}</TableCell>
                  <TableCell>
                    <Stack direction="row" sx={{ gap: 0.5, flexWrap: 'wrap' }}>
                      <Chip size="small" color={outcome.color} label={outcome.label} />
                      {item.reason && <Chip size="small" variant="outlined" label={REASON_LABELS[item.reason]} />}
                    </Stack>
                  </TableCell>
                  {detailed && <TableCell>{item.confidence != null ? `${Math.round(item.confidence * 100)}%` : '—'}</TableCell>}
                  {detailed && (
                    <TableCell sx={{ maxWidth: 260 }}>
                      {item.documents.length === 0
                        ? '—'
                        : item.documents.map((d) => (
                            <Link key={d.documentId} component={RouterLink} to={documentLink(d.folderId, d.documentId)} variant="body2" sx={{ display: 'block' }} noWrap>
                              {d.fileName}
                            </Link>
                          ))}
                    </TableCell>
                  )}
                  <TableCell>
                    {item.delivery ? (
                      <Typography variant="body2" color={item.delivery === 'Failed' ? 'error' : 'text.secondary'}>
                        {DELIVERY_LABELS[item.delivery]}
                      </Typography>
                    ) : (
                      '—'
                    )}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </Box>
      {hasMore && (
        <Stack sx={{ alignItems: 'center', mt: 2 }}>
          <Button variant="outlined" onClick={onLoadMore} disabled={isLoadingMore}>
            Load more
          </Button>
        </Stack>
      )}
    </Box>
  );
}
