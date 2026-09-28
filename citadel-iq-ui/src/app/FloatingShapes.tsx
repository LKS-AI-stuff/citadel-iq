import { Box, alpha, useTheme } from '@mui/material';

const SHAPES = [
  { width: 140, height: 90, top: '18%', left: '8%', rotate: 15, delay: '0s', duration: '9s' },
  { width: 100, height: 150, top: '58%', right: '12%', rotate: -20, delay: '1.2s', duration: '10s' },
  { width: 110, height: 70, bottom: '18%', left: '18%', rotate: 25, delay: '2.4s', duration: '8s' },
  { width: 90, height: 130, top: '8%', right: '28%', rotate: -10, delay: '0.6s', duration: '11s' },
  { width: 120, height: 80, bottom: '35%', right: '20%', rotate: 30, delay: '3s', duration: '9.5s' },
  { width: 105, height: 105, top: '38%', left: '4%', rotate: -15, delay: '1.8s', duration: '10.5s' },
];

/** Decorative echo of templatemo_592_glossy_touch's `.bg-shapes` — soft translucent blocks
 * that gently drift behind the glass surfaces. Fixed + inert, so it never affects layout. */
export function FloatingShapes() {
  const theme = useTheme();
  const isLight = theme.palette.mode === 'light';

  return (
    <Box
      aria-hidden
      sx={{
        position: 'fixed',
        inset: 0,
        zIndex: -1,
        overflow: 'hidden',
        pointerEvents: 'none',
      }}
    >
      {SHAPES.map((shape, index) => (
        <Box
          key={index}
          className="animate-float-slow"
          sx={{
            position: 'absolute',
            width: shape.width,
            height: shape.height,
            top: shape.top,
            left: shape.left,
            right: shape.right,
            bottom: shape.bottom,
            borderRadius: '18px',
            transform: `rotate(${shape.rotate}deg)`,
            backgroundColor: alpha('#ffffff', isLight ? 0.35 : 0.08),
            boxShadow: `0 8px 32px ${alpha('#ffffff', isLight ? 0.25 : 0.1)}`,
            animationDelay: shape.delay,
            animationDuration: shape.duration,
          }}
        />
      ))}
    </Box>
  );
}
