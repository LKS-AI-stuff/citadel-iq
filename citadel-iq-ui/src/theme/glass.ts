import { alpha, type Theme } from '@mui/material/styles';

/** Tinted-circle treatment for inline card action icons (download/open/delete) — a softer
 * version of `IconBadge`'s colored background, sized for an `IconButton` rather than a large
 * file/folder glyph. Kept muted (not fully greyed out) while disabled so it still reads as the
 * same icon, just unavailable. */
export function actionIconButtonSx(color: string) {
  return {
    color,
    bgcolor: alpha(color, 0.12),
    transition: 'background-color 0.2s ease, transform 0.2s cubic-bezier(0.34, 1.56, 0.64, 1)',
    '&:hover': {
      bgcolor: alpha(color, 0.22),
      transform: 'scale(1.08)',
    },
    '&.Mui-disabled': {
      color: alpha(color, 0.35),
      bgcolor: alpha(color, 0.06),
    },
  } as const;
}

/** The folder accent used for folder icons/badges/headers — the theme's indigo primary, lightened
 * for dark mode (matches MuiButton's `outlined` override in theme.ts) since the flat `#4f46e5`
 * primary tone is too dark/low-contrast against the near-black glass surface. */
export function folderAccentColor(theme: Theme): string {
  return theme.palette.mode === 'light' ? '#4338ca' : '#a5b4fc';
}

/** Mirrors templatemo_592_glossy_touch's diagonal gradient body background.
 * Dark mode reuses the template's palette verbatim; light mode is a pastel
 * equivalent so glass text/cards stay legible without a dark backdrop. */
export function glossyBackground(mode: 'light' | 'dark'): string {
  return mode === 'dark'
    ? 'linear-gradient(135deg, #0c0c0c 0%, #1a1a2e 15%, #16213e 35%, #0f3460 50%, #533a7d 70%, #8b5a8c 85%, #a0616a 100%)'
    : 'linear-gradient(135deg, #eef2ff 0%, #e0e7ff 18%, #dbeafe 38%, #e0f2fe 58%, #ede9fe 78%, #fce7f3 100%)';
}

interface GlassOptions {
  hover?: boolean;
  radius?: number;
}

/** The template's `.glass` treatment: translucent surface + backdrop blur + soft border. */
export function glassSurfaceSx(theme: Theme, { hover = false, radius = 20 }: GlassOptions = {}) {
  const isLight = theme.palette.mode === 'light';

  return {
    position: 'relative',
    backgroundColor: isLight ? alpha('#ffffff', 0.55) : alpha('#ffffff', 0.08),
    backdropFilter: 'blur(20px) saturate(180%)',
    WebkitBackdropFilter: 'blur(20px) saturate(180%)',
    border: '1px solid',
    borderColor: isLight ? alpha('#ffffff', 0.7) : alpha('#ffffff', 0.16),
    borderRadius: `${radius}px`,
    boxShadow: isLight ? '0 8px 32px rgba(15, 23, 42, 0.08)' : '0 8px 32px rgba(0, 0, 0, 0.35)',
    transition: 'transform 0.3s ease, background-color 0.3s ease, box-shadow 0.3s ease, border-color 0.3s ease',
    ...(hover && {
      cursor: 'pointer',
      '&:hover': {
        backgroundColor: isLight ? alpha('#ffffff', 0.75) : alpha('#ffffff', 0.13),
        borderColor: isLight ? alpha('#4f46e5', 0.35) : alpha('#ffffff', 0.3),
        transform: 'translateY(-4px)',
        boxShadow: isLight ? '0 16px 40px rgba(15, 23, 42, 0.14)' : '0 16px 45px rgba(0, 0, 0, 0.45)',
      },
    }),
  } as const;
}
