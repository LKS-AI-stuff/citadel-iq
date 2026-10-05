import { useEffect, useState } from 'react';
import { Box } from '@mui/material';
import { Outlet, useNavigate, useParams } from 'react-router-dom';
import { TopNavigation } from './TopNavigation';
import { Footer } from './Footer';
import { FloatingShapes } from './FloatingShapes';
import { SearchPanel } from '../components/search/SearchPanel';
import { AssistantInfoPanel } from '../components/common/AssistantInfoPanel';
import { ROOT_FOLDER_ID } from '../constants';

const CONTENT_MAX_WIDTH = 1320;
const SIDEBAR_WIDTH = 300;

export function AppShell() {
  const { folderId } = useParams<{ folderId?: string }>();
  const navigate = useNavigate();
  const [isSearchOpen, setSearchOpen] = useState(false);

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      const isShortcut = (event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k';
      if (isShortcut) {
        event.preventDefault();
        setSearchOpen(true);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  return (
    <Box
      sx={{
        display: 'grid',
        // Stacked on mobile/tablet (the sidebar sits above the page body); a real two-column
        // layout from `md` up, with the sidebar starting at row 1 — level with the header,
        // not pushed down to start beside <main>.
        gridTemplateColumns: { xs: '1fr', md: `${SIDEBAR_WIDTH}px 1fr` },
        minHeight: '100vh',
      }}
    >
      <Box
        component="aside"
        sx={{
          // Hidden below `md` entirely — there's no good place to stack it on a narrow screen
          // without it competing with the header for attention, so it's a desktop-only extra.
          display: { xs: 'none', md: 'block' },
          // No right padding: the content column's own left padding (matching
          // header/main/footer's gutter) is the only gap between sidebar and content, so the
          // two don't stack into a double gap.
          pl: 3,
          pt: 3,
          pb: 3,
          position: 'sticky',
          top: 0,
          alignSelf: 'start',
          height: '100vh',
          overflowY: 'auto',
        }}
      >
        <AssistantInfoPanel />
      </Box>

      <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh', position: 'relative' }}>
        <FloatingShapes />

        <TopNavigation onSearchClick={() => setSearchOpen(true)} onLogoClick={() => navigate('/')} />

        <Box
          component="main"
          sx={{
            flex: 1,
            px: { xs: 2, sm: 3, md: 4 },
            py: { xs: 3, md: 4 },
          }}
        >
          {/* Padding lives on this outer box, not the width-capped one below — matching how
              TopNavigation/Footer inset their glass bar, so the content column lines up at
              exactly the same width instead of being narrowed by an extra inset. No mx: 'auto'
              here either, for the same reason as those two: centering within a column that's
              wider than 1320px (once the sidebar eats the rest) would open a gap that grows
              with the window instead of just capping the content's own width. */}
          <Box sx={{ maxWidth: `${CONTENT_MAX_WIDTH}px`, width: '100%' }}>
            <Outlet />
          </Box>
        </Box>

        <Footer onHomeClick={() => navigate('/')} onSearchClick={() => setSearchOpen(true)} />
      </Box>

      <SearchPanel
        open={isSearchOpen}
        onClose={() => setSearchOpen(false)}
        currentFolderId={folderId ?? ROOT_FOLDER_ID}
      />
    </Box>
  );
}
