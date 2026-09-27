import { Box } from '@mui/material';
import { Outlet } from 'react-router-dom';
import { TopNavigation } from './TopNavigation';

export function AppShell() {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh', bgcolor: 'background.default' }}>
      <TopNavigation />
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
    </Box>
  );
}
