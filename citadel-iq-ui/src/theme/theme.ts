import { alpha, createTheme, type PaletteMode, type Shadows } from '@mui/material/styles';
import { glossyBackground } from './glass';

function buildShadows(mode: PaletteMode): Shadows {
  const rgb = mode === 'light' ? '15, 23, 42' : '0, 0, 0';
  const shadows = ['none'] as unknown as string[];

  for (let i = 1; i <= 24; i += 1) {
    const y = Math.min(1 + i, 20);
    const blur = 4 + i * 3;
    const spread = i > 10 ? -2 : -1;
    const alphaValue = mode === 'light' ? 0.05 + Math.min(i, 10) * 0.011 : 0.45 + Math.min(i, 10) * 0.02;
    shadows.push(`0 ${y}px ${blur}px ${spread}px rgba(${rgb}, ${alphaValue.toFixed(3)})`);
  }

  return shadows as unknown as Shadows;
}

export function getTheme(mode: PaletteMode) {
  const isLight = mode === 'light';

  const theme = createTheme({
    palette: {
      mode,
      primary: {
        main: '#4f46e5',
        light: '#818cf8',
        dark: '#3730a3',
        contrastText: '#ffffff',
      },
      secondary: {
        main: '#0ea5e9',
      },
      success: { main: isLight ? '#16a34a' : '#4ade80' },
      error: { main: isLight ? '#dc2626' : '#f87171' },
      warning: { main: isLight ? '#d97706' : '#fbbf24' },
      background: isLight
        ? { default: '#f5f6fb', paper: '#ffffff' }
        : { default: '#0a0e1a', paper: '#111629' },
      divider: isLight ? alpha('#1e293b', 0.08) : alpha('#e2e8f0', 0.09),
      text: isLight
        ? { primary: '#0f172a', secondary: '#5b6478' }
        : { primary: '#f1f5f9', secondary: '#93a0b8' },
    },
    shape: {
      borderRadius: 12,
    },
    shadows: buildShadows(mode),
    typography: {
      fontFamily: ['Inter', 'system-ui', '-apple-system', 'Segoe UI', 'Roboto', 'sans-serif'].join(','),
      h4: { fontWeight: 800, letterSpacing: '-0.02em' },
      h5: { fontWeight: 800, letterSpacing: '-0.015em' },
      h6: { fontWeight: 700, letterSpacing: '-0.01em' },
      subtitle1: { fontWeight: 600 },
      subtitle2: { fontWeight: 600 },
      button: { fontWeight: 600, letterSpacing: 0 },
    },
    components: {
      MuiCssBaseline: {
        styleOverrides: {
          body: {
            background: glossyBackground(mode),
            backgroundAttachment: 'fixed',
            minHeight: '100vh',
            overflowX: 'hidden',
            transition: 'background 0.3s ease',
          },
        },
      },
      MuiPaper: {
        styleOverrides: {
          root: {
            backgroundImage: 'none',
          },
        },
      },
      MuiButton: {
        defaultProps: { disableElevation: true },
        styleOverrides: {
          root: {
            borderRadius: 10,
            textTransform: 'none',
            fontWeight: 600,
            paddingLeft: 16,
            paddingRight: 16,
            transition: 'transform 0.15s cubic-bezier(0.4, 0, 0.2, 1), box-shadow 0.15s ease, background-color 0.15s ease',
          },
          contained: {
            boxShadow: `0 1px 2px ${alpha('#4f46e5', 0.3)}`,
            '&:hover': {
              boxShadow: `0 8px 20px ${alpha('#4f46e5', 0.35)}`,
              transform: 'translateY(-1px)',
            },
            '&:active': { transform: 'translateY(0)' },
          },
          outlined: {
            color: isLight ? '#4338ca' : '#a5b4fc',
            borderColor: isLight ? alpha('#1e293b', 0.16) : alpha('#a5b4fc', 0.4),
            '&:hover': {
              transform: 'translateY(-1px)',
              backgroundColor: alpha('#4f46e5', isLight ? 0.05 : 0.16),
              borderColor: isLight ? '#4338ca' : '#a5b4fc',
            },
          },
        },
      },
      MuiIconButton: {
        styleOverrides: {
          root: {
            transition: 'transform 0.15s ease, background-color 0.15s ease',
            '&:hover': { transform: 'translateY(-1px)' },
          },
        },
      },
      MuiChip: {
        styleOverrides: {
          root: {
            borderRadius: 8,
            fontWeight: 600,
          },
        },
      },
      MuiDrawer: {
        styleOverrides: {
          paper: {
            backgroundImage: 'none',
          },
        },
      },
      MuiDialog: {
        styleOverrides: {
          paper: {
            borderRadius: 20,
          },
        },
      },
      MuiTooltip: {
        styleOverrides: {
          tooltip: {
            fontSize: 12,
            fontWeight: 500,
            borderRadius: 8,
            backgroundColor: isLight ? '#0f172a' : '#f1f5f9',
            color: isLight ? '#f8fafc' : '#0f172a',
          },
        },
      },
      MuiLinearProgress: {
        styleOverrides: {
          root: {
            borderRadius: 999,
            height: 6,
            backgroundColor: alpha('#4f46e5', 0.12),
          },
          bar: {
            borderRadius: 999,
          },
        },
      },
      MuiMenu: {
        styleOverrides: {
          paper: {
            borderRadius: 12,
            border: `1px solid ${isLight ? alpha('#1e293b', 0.08) : alpha('#e2e8f0', 0.08)}`,
          },
        },
      },
    },
  });

  return theme;
}
