import { useEffect, useRef, useState } from 'react';
import { Alert, Box, Button, CircularProgress, Divider, Stack, Typography } from '@mui/material';
import { AnswerText } from './AnswerText';
import { SourceList } from './SourceList';
import { sourceElementId, splitCitations } from '../../utils/citations';
import { NOT_FOUND_MESSAGE } from '../../hooks/useAsk';
import type { ConversationTurn } from '../../types/ask';

interface TurnBlockProps {
  turn: ConversationTurn;
  onSearchInstead: (query: string) => void;
}

export function TurnBlock({ turn, onSearchInstead }: TurnBlockProps) {
  const [highlighted, setHighlighted] = useState<number | null>(null);
  const timerRef = useRef<number | undefined>(undefined);
  useEffect(() => () => window.clearTimeout(timerRef.current), []);

  const handleCitationClick = (number: number) => {
    document.getElementById(sourceElementId(turn.id, number))?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    setHighlighted(number);
    window.clearTimeout(timerRef.current);
    timerRef.current = window.setTimeout(() => setHighlighted(null), 2000);
  };

  const searchedQuery = turn.standaloneQuestion ?? turn.question;
  const isStreaming = turn.status === 'streaming';
  // Error/aborted turns never receive the server's `done` event, so derive the cited sources from the text.
  const cited =
    turn.cited.length > 0
      ? turn.cited
      : [...new Set(splitCitations(turn.answer, turn.sources.length).flatMap((p) => (p.type === 'cite' ? p.numbers : [])))];

  return (
    <Box component="section" sx={{ py: 2.5 }}>
      <Typography variant="subtitle1" sx={{ fontWeight: 700, lineHeight: 1.35 }}>
        {turn.question}
      </Typography>
      {turn.rewritten && turn.standaloneQuestion && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.25 }}>
          Searched for: {turn.standaloneQuestion}
        </Typography>
      )}

      <Box aria-live="polite" sx={{ mt: 1.5 }}>
        {isStreaming && turn.answer === '' && (
          <Stack direction="row" sx={{ alignItems: 'center', gap: 1 }}>
            <CircularProgress size={16} />
            <Typography variant="body2" color="text.secondary">
              Searching your documents…
            </Typography>
          </Stack>
        )}

        {turn.answer !== '' && turn.status !== 'notfound' && (
          <AnswerText text={turn.answer} sources={turn.sources} onCitationClick={handleCitationClick} isStreaming={isStreaming} />
        )}

        {turn.status === 'notfound' && (
          <Typography variant="body2" color="text.secondary">
            {NOT_FOUND_MESSAGE}
          </Typography>
        )}

        {turn.status === 'done' && turn.verified === false && (
          <Alert severity="warning" sx={{ mt: 1.5 }}>
            This answer could not be verified against your documents. Review the sources.
          </Alert>
        )}

        {turn.status === 'error' && (
          <Alert
            severity="error"
            sx={{ mt: 1 }}
            action={
              turn.searchInstead ? (
                <Button color="inherit" size="small" onClick={() => onSearchInstead(turn.question)}>
                  Search passages instead
                </Button>
              ) : undefined
            }
          >
            {turn.error}
          </Alert>
        )}
      </Box>

      {turn.sources.length > 0 && turn.status !== 'streaming' && (
        <Box sx={{ mt: 2 }}>
          <SourceList turnId={turn.id} sources={turn.sources} cited={cited} query={searchedQuery} highlighted={highlighted} />
        </Box>
      )}

      <Divider sx={{ mt: 2.5 }} />
    </Box>
  );
}
