import { Box, Button, CircularProgress, Divider, Drawer, IconButton, Stack, Typography, Alert } from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import SearchIcon from '@mui/icons-material/Search';
import { SearchInput } from './SearchInput';
import { SearchScopeSelector } from './SearchScopeSelector';
import { SearchResults } from './SearchResults';
import { useSearch } from '../../hooks/useSearch';

interface SearchPanelProps {
  open: boolean;
  onClose: () => void;
  currentFolderId: string;
}

export function SearchPanel({ open, onClose, currentFolderId }: SearchPanelProps) {
  const { query, setQuery, scope, setScope, results, searchedQuery, isLoading, error, search, clear } =
    useSearch(currentFolderId);

  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
      slotProps={{ paper: { sx: { width: { xs: '100%', sm: '50%' }, maxWidth: 640 } } }}
    >
      <Box sx={{ p: 3, display: 'flex', flexDirection: 'column', height: '100%' }}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 2 }}>
          <Stack direction="row" sx={{ alignItems: 'center', gap: 1 }}>
            <SearchIcon color="primary" />
            <Typography variant="h6">Search documents</Typography>
          </Stack>
          <IconButton onClick={onClose} aria-label="Close search">
            <CloseIcon />
          </IconButton>
        </Stack>

        <Stack spacing={2}>
          <SearchInput value={query} onChange={setQuery} onSearch={search} onClear={clear} isLoading={isLoading} />
          <SearchScopeSelector value={scope} onChange={setScope} />
          <Button variant="contained" onClick={search} disabled={isLoading}>
            {isLoading ? <CircularProgress size={20} color="inherit" /> : 'Search'}
          </Button>
        </Stack>

        <Divider sx={{ my: 2 }} />

        <Box sx={{ flex: 1, overflowY: 'auto' }}>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          {results && <SearchResults results={results} query={searchedQuery} />}
        </Box>
      </Box>
    </Drawer>
  );
}
