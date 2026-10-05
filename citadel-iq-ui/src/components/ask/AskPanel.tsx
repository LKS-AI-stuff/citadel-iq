import { useEffect, useRef } from 'react';
import { Box, Button, Stack, Typography } from '@mui/material';
import AutoAwesomeRoundedIcon from '@mui/icons-material/AutoAwesomeRounded';
import RestartAltRoundedIcon from '@mui/icons-material/RestartAltRounded';
import { SearchInput } from '../search/SearchInput';
import { SearchScopeSelector } from '../search/SearchScopeSelector';
import { EmptyState } from '../common/EmptyState';
import { TurnBlock } from './TurnBlock';
import { SessionLimitDialog } from './SessionLimitDialog';
import type { useAsk } from '../../hooks/useAsk';

interface AskPanelProps {
  ask: ReturnType<typeof useAsk>;
  onSearchInstead: (query: string) => void;
}

export function AskPanel({ ask, onSearchInstead }: AskPanelProps) {
  const { input, setInput, scope, setScope, turns, isStreaming, limitReached, inputError, submit, newSession } = ask;
  const bottomRef = useRef<HTMLDivElement>(null);

  // Keep the newest turn in view as the conversation grows.
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ block: 'nearest' });
  }, [turns.length]);

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
      <Box sx={{ p: 3, pb: 2 }}>
        <Stack spacing={2}>
          <SearchInput value={input} onChange={setInput} onSearch={submit} onClear={() => setInput('')} isLoading={isStreaming} />
          {inputError && (
            <Typography variant="caption" color="error">
              {inputError}
            </Typography>
          )}
          <SearchScopeSelector value={scope} onChange={setScope} />
          <Stack direction="row" sx={{ gap: 1 }}>
            <Button
              variant="contained"
              size="large"
              onClick={submit}
              disabled={isStreaming || !input.trim()}
              startIcon={<AutoAwesomeRoundedIcon />}
              sx={{ flex: 1, background: 'linear-gradient(135deg, #4f46e5 0%, #4338ca 100%)' }}
            >
              Ask
            </Button>
            {turns.length > 0 && (
              <Button variant="outlined" size="large" onClick={newSession} startIcon={<RestartAltRoundedIcon />}>
                New session
              </Button>
            )}
          </Stack>
        </Stack>
      </Box>

      <Box sx={{ flex: 1, overflowY: 'auto', px: 3, pb: 2 }}>
        {turns.length === 0 ? (
          <EmptyState
            title="Ask a question"
            description="Answers are AI-generated from your documents and cite the passages they rely on."
            icon={<AutoAwesomeRoundedIcon sx={{ fontSize: 32, color: 'primary.main' }} />}
          />
        ) : (
          turns.map((turn) => <TurnBlock key={turn.id} turn={turn} onSearchInstead={onSearchInstead} />)
        )}
        <div ref={bottomRef} />
      </Box>

      <Typography variant="caption" color="text.secondary" sx={{ px: 3, py: 1.5, borderTop: '1px solid', borderColor: 'divider' }}>
        Your question and the most relevant passages are sent to OpenAI to write the answer. Nothing is stored on the server.
      </Typography>

      <SessionLimitDialog open={limitReached} onConfirm={newSession} />
    </Box>
  );
}
