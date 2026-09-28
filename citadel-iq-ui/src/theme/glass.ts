import { alpha, type Theme } from '@mui/material/styles';

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
