import { AppBar, IconButton, Toolbar, Tooltip, Typography } from '@mui/material';
import CorporateFareIcon from '@mui/icons-material/CorporateFare';
import SearchIcon from '@mui/icons-material/Search';
import DarkModeOutlinedIcon from '@mui/icons-material/DarkModeOutlined';
import LightModeOutlinedIcon from '@mui/icons-material/LightModeOutlined';
import { useColorMode } from '../theme/ColorModeProvider';

interface TopNavigationProps {
  onSearchClick: () => void;
}

export function TopNavigation({ onSearchClick }: TopNavigationProps) {
  const { mode, toggleColorMode } = useColorMode();

  return (
    <AppBar
      position="static"
      color="inherit"
      elevation={0}
      sx={{ borderBottom: '1px solid', borderColor: 'divider' }}
    >
      <Toolbar>
        <CorporateFareIcon color="primary" sx={{ mr: 1.5 }} />
        <Typography variant="h6" color="text.primary" sx={{ fontWeight: 700, flex: 1 }}>
          CitadelIQ
        </Typography>
        <Tooltip title={mode === 'light' ? 'Switch to dark mode' : 'Switch to light mode'}>
          <IconButton onClick={toggleColorMode} aria-label="Toggle color mode">
            {mode === 'light' ? <DarkModeOutlinedIcon /> : <LightModeOutlinedIcon />}
          </IconButton>
        </Tooltip>
        <Tooltip title="Search documents">
          <IconButton onClick={onSearchClick} aria-label="Search documents">
            <SearchIcon />
          </IconButton>
        </Tooltip>
      </Toolbar>
    </AppBar>
  );
}
