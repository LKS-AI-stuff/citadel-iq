import { Box, IconButton, Stack, Tooltip, Typography, alpha } from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import DarkModeOutlinedIcon from '@mui/icons-material/DarkModeOutlined';
import LightModeOutlinedIcon from '@mui/icons-material/LightModeOutlined';
import ShieldOutlinedIcon from '@mui/icons-material/ShieldOutlined';
import { GlassSurface } from '../components/common/GlassSurface';
import { AccountMenu } from '../components/account/AccountMenu';
import { useColorMode } from '../theme/ColorModeProvider';

interface TopNavigationProps {
  onSearchClick: () => void;
  onLogoClick: () => void;
}

const isMac = typeof navigator !== 'undefined' && /Mac/i.test(navigator.platform);

export function TopNavigation({ onSearchClick, onLogoClick }: TopNavigationProps) {
  const { mode, toggleColorMode } = useColorMode();

  return (
    <Box component="header" sx={{ px: { xs: 2, sm: 3, md: 4 }, pt: { xs: 2, md: 3 } }}>
      <GlassSurface
        component="nav"
        aria-label="Primary"
        radius={20}
        sx={{
          // No mx: 'auto' — the sidebar column is always present now, so centering here would
          // create a gap that grows with leftover space instead of just capping the width.
          maxWidth: '1320px',
          width: '100%',
          display: 'flex',
          alignItems: 'center',
          gap: 2,
          px: { xs: 2, sm: 3 },
          py: 1.5,
        }}
      >
        <Stack
          direction="row"
          onClick={onLogoClick}
          sx={{ alignItems: 'center', gap: 1.25, flexShrink: 0, cursor: 'pointer' }}
        >
          <Box
            sx={{
              width: 40,
              height: 40,
              borderRadius: '12px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              background: 'linear-gradient(135deg, #4f46e5 0%, #6366f1 55%, #0ea5e9 100%)',
              boxShadow: (t) => `0 6px 16px ${alpha(t.palette.primary.main, 0.4)}`,
              transition: 'transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1)',
              '&:hover': { transform: 'scale(1.08) translateY(-2px)' },
            }}
          >
            <ShieldOutlinedIcon sx={{ color: '#fff', fontSize: 22 }} />
          </Box>
          <Box sx={{ display: { xs: 'none', sm: 'block' } }}>
            <Typography
              variant="h6"
              sx={{ lineHeight: 1.1, fontWeight: 800, color: 'text.primary', textShadow: '0 2px 10px rgba(0,0,0,0.15)' }}
            >
              CitadelIQ
            </Typography>
            <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: '0.04em', fontWeight: 600 }}>
              DOCUMENT INTELLIGENCE
            </Typography>
          </Box>
        </Stack>

        <Box sx={{ flex: 1 }} />

        <Box
          component="button"
          onClick={onSearchClick}
          aria-label="Search documents"
          sx={{
            display: { xs: 'none', md: 'flex' },
            alignItems: 'center',
            gap: 1,
            width: 300,
            px: 1.5,
            py: 0.85,
            borderRadius: '999px',
            border: '1px solid',
            borderColor: (t) => alpha(t.palette.text.primary, 0.14),
            bgcolor: (t) => alpha(t.palette.text.primary, t.palette.mode === 'light' ? 0.04 : 0.08),
            color: 'text.secondary',
            cursor: 'pointer',
            transition: 'border-color 0.15s ease, background-color 0.15s ease, transform 0.15s ease',
            fontFamily: 'inherit',
            '&:hover': {
              borderColor: 'primary.main',
              bgcolor: (t) => alpha(t.palette.primary.main, 0.1),
              transform: 'translateY(-1px)',
            },
          }}
        >
          <SearchIcon fontSize="small" />
          <Typography variant="body2" sx={{ flex: 1, textAlign: 'left' }}>
            Search documents…
          </Typography>
          <Box
            sx={{
              px: 0.75,
              py: 0.25,
              borderRadius: '6px',
              fontSize: 11,
              fontWeight: 700,
              border: '1px solid',
              borderColor: 'divider',
            }}
          >
            {isMac ? '⌘K' : 'Ctrl K'}
          </Box>
        </Box>

        <Tooltip title="Search documents">
          <IconButton
            onClick={onSearchClick}
            aria-label="Search documents"
            sx={{ display: { md: 'none' }, bgcolor: (t) => alpha(t.palette.text.primary, 0.06) }}
          >
            <SearchIcon />
          </IconButton>
        </Tooltip>

        <Tooltip title={mode === 'light' ? 'Switch to dark mode' : 'Switch to light mode'}>
          <IconButton
            onClick={toggleColorMode}
            aria-label="Toggle color mode"
            sx={{ bgcolor: (t) => alpha(t.palette.text.primary, 0.06) }}
          >
            <Box
              sx={{
                display: 'flex',
                transition: 'transform 0.4s cubic-bezier(0.34, 1.56, 0.64, 1)',
                transform: mode === 'light' ? 'rotate(0deg)' : 'rotate(180deg)',
              }}
            >
              {mode === 'light' ? <DarkModeOutlinedIcon /> : <LightModeOutlinedIcon />}
            </Box>
          </IconButton>
        </Tooltip>

        <AccountMenu />
      </GlassSurface>
    </Box>
  );
}
