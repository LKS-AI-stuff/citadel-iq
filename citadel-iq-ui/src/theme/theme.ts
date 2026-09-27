import { createTheme, type PaletteMode } from '@mui/material/styles';

export function getTheme(mode: PaletteMode) {
  return createTheme({
    palette: {
      mode,
      primary: {
        main: '#2563eb',
      },
      background:
        mode === 'light'
          ? { default: '#f8fafc', paper: '#ffffff' }
          : { default: '#0f172a', paper: '#1e293b' },
    },
    shape: {
      borderRadius: 8,
    },
    typography: {
      fontFamily: [
        'Inter',
        'system-ui',
        '-apple-system',
        'Segoe UI',
        'Roboto',
        'sans-serif',
      ].join(','),
    },
    components: {
      MuiPaper: {
        styleOverrides: {
          root: {
            backgroundImage: 'none',
          },
        },
      },
    },
  });
}
