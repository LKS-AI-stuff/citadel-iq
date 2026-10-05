import { useState } from 'react';
import { Box, Button, CircularProgress, Divider, Drawer, IconButton, Stack, ToggleButton, ToggleButtonGroup, Typography, Alert, alpha } from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import SearchIcon from '@mui/icons-material/Search';
import { SearchInput } from './SearchInput';
import { SearchScopeSelector } from './SearchScopeSelector';
import { SearchResults } from './SearchResults';
import { AskPanel } from '../ask/AskPanel';
import { useSearch } from '../../hooks/useSearch';
import { useAsk } from '../../hooks/useAsk';
import { useAppSettings } from '../../settings/useAppSettings';
import { glossyBackground } from '../../theme/glass';

interface SearchPanelProps {
  open: boolean;
  onClose: () => void;
  currentFolderId: string;
}

export function SearchPanel({ open, onClose, currentFolderId }: SearchPanelProps) {
  const { query, setQuery, scope, setScope, results, searchedQuery, isLoading, error, search, clear } =
    useSearch(currentFolderId);
  const ask = useAsk(currentFolderId);
  const { askEnabled } = useAppSettings();
  const [chosenMode, setChosenMode] = useState<'ask' | 'passages'>('ask');
  // When Ask is switched off on the server, only the passages view is available.
  const mode = askEnabled ? chosenMode : 'passages';

  // Closing the drawer stops any in-flight answer, so the server cancels generation.
  const handleClose = () => {
    ask.abort();
    onClose();
  };

  const handleSearchInstead = (question: string) => {
    setQuery(question);
    setChosenMode('passages');
  };

  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={handleClose}
      slotProps={{
        paper: {
          sx: {
            width: { xs: '100%', sm: '50%' },
            maxWidth: 640,
            borderRadius: { sm: '20px 0 0 20px' },
            // Same diagonal gradient as the page body, tinted with a translucent overlay so
            // the drawer reads as glass over it rather than a flat, disconnected panel.
            backgroundImage: (t) =>
              `linear-gradient(${alpha(t.palette.background.paper, t.palette.mode === 'light' ? 0.55 : 0.35)}, ${alpha(t.palette.background.paper, t.palette.mode === 'light' ? 0.75 : 0.55)}), ${glossyBackground(t.palette.mode)}`,
            backdropFilter: 'blur(24px) saturate(180%)',
            WebkitBackdropFilter: 'blur(24px) saturate(180%)',
            borderLeft: '1px solid',
            borderColor: (t) => alpha('#ffffff', t.palette.mode === 'light' ? 0.6 : 0.14),
          },
        },
      }}
    >
      <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
        <Box
          sx={{
            px: 3,
            py: 2.5,
            background: (t) =>
              `linear-gradient(135deg, ${alpha(t.palette.primary.main, t.palette.mode === 'light' ? 0.08 : 0.16)}, transparent 70%)`,
            borderBottom: '1px solid',
            borderColor: 'divider',
          }}
        >
          <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
            <Stack direction="row" sx={{ alignItems: 'center', gap: 1.25 }}>
              <Box
                sx={{
                  width: 36,
                  height: 36,
                  borderRadius: '10px',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  background: 'linear-gradient(135deg, #4f46e5 0%, #0ea5e9 100%)',
                }}
              >
                <SearchIcon sx={{ color: '#fff', fontSize: 20 }} />
              </Box>
              <Box>
                <Typography variant="h6" sx={{ lineHeight: 1.15 }}>
                  {mode === 'ask' ? 'Ask your documents' : 'Semantic search'}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {mode === 'ask' ? 'Answers are AI-generated from your documents' : 'Ranked by meaning, not keywords'}
                </Typography>
              </Box>
            </Stack>
            <IconButton onClick={handleClose} aria-label="Close search">
              <CloseIcon />
            </IconButton>
          </Stack>
        </Box>

        {askEnabled && (
          <Box sx={{ px: 3, pt: 2 }}>
            <ToggleButtonGroup
              value={mode}
              exclusive
              fullWidth
              size="small"
              aria-label="Mode"
              onChange={(_, next) => {
                if (next) setChosenMode(next);
              }}
              sx={{ '& .MuiToggleButton-root': { textTransform: 'none', fontWeight: 600 } }}
            >
              <ToggleButton value="ask">Ask</ToggleButton>
              <ToggleButton value="passages">Passages only</ToggleButton>
            </ToggleButtonGroup>
          </Box>
        )}

        {mode === 'ask' ? (
          <AskPanel ask={ask} onSearchInstead={handleSearchInstead} />
        ) : (
          <>
            <Box sx={{ p: 3, pb: 2 }}>
              <Stack spacing={2}>
                <SearchInput value={query} onChange={setQuery} onSearch={search} onClear={clear} isLoading={isLoading} />
                <SearchScopeSelector value={scope} onChange={setScope} />
                <Button
                  variant="contained"
                  size="large"
                  onClick={search}
                  disabled={isLoading}
                  sx={{ background: 'linear-gradient(135deg, #4f46e5 0%, #4338ca 100%)' }}
                >
                  {isLoading ? <CircularProgress size={20} color="inherit" /> : 'Search'}
                </Button>
              </Stack>
            </Box>

            <Divider />

            <Box sx={{ flex: 1, overflowY: 'auto', p: 3, pt: 2 }}>
              {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
              {results && <SearchResults results={results} query={searchedQuery} />}
            </Box>
          </>
        )}
      </Box>
    </Drawer>
  );
}
