import { AppBar, Toolbar, Typography } from '@mui/material';
import CorporateFareIcon from '@mui/icons-material/CorporateFare';

export function TopNavigation() {
  return (
    <AppBar
      position="static"
      color="inherit"
      elevation={0}
      sx={{ borderBottom: '1px solid', borderColor: 'divider' }}
    >
      <Toolbar>
        <CorporateFareIcon color="primary" sx={{ mr: 1.5 }} />
        <Typography variant="h6" color="text.primary" sx={{ fontWeight: 700 }}>
          CitadelIQ
        </Typography>
      </Toolbar>
    </AppBar>
  );
}
