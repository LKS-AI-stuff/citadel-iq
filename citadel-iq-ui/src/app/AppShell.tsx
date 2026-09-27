import { useState } from 'react';
import { Box } from '@mui/material';
import { Outlet, useParams } from 'react-router-dom';
import { TopNavigation } from './TopNavigation';
import { SearchPanel } from '../components/search/SearchPanel';
import { ROOT_FOLDER_ID } from '../constants';

export function AppShell() {
  const { folderId } = useParams<{ folderId?: string }>();
  const [isSearchOpen, setSearchOpen] = useState(false);

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh', bgcolor: 'background.default' }}>
      <TopNavigation onSearchClick={() => setSearchOpen(true)} />
      <Box
        component="main"
        sx={{
          flex: 1,
          maxWidth: '1280px',
          width: '100%',
          mx: 'auto',
          px: { xs: 2, sm: 3, md: 4 },
          py: 3,
        }}
      >
        <Outlet />
      </Box>
      <SearchPanel
        open={isSearchOpen}
        onClose={() => setSearchOpen(false)}
        currentFolderId={folderId ?? ROOT_FOLDER_ID}
      />
    </Box>
  );
}
