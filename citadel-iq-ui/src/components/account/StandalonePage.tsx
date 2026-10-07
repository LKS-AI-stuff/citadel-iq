import { Box, Stack, Typography, alpha } from '@mui/material';
import ShieldOutlinedIcon from '@mui/icons-material/ShieldOutlined';
import type { ReactNode } from 'react';
import { FloatingShapes } from '../../app/FloatingShapes';
import { GlassSurface } from '../common/GlassSurface';

interface StandalonePageProps {
  title: string;
  subtitle?: ReactNode;
  children?: ReactNode;
  /** Wider layout for pages with several choices (onboarding). */
  wide?: boolean;
}

/** Glass layout for the pages shown before a user is in a workspace: sign-in, onboarding, waiting, closed. */
export function StandalonePage({ title, subtitle, children, wide = false }: StandalonePageProps) {
  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', px: 2, py: 6, position: 'relative' }}>
      <FloatingShapes />
      <GlassSurface component="main" radius={24} sx={{ width: '100%', maxWidth: wide ? 920 : 520, p: { xs: 3, sm: 5 } }}>
        <Stack direction="row" sx={{ alignItems: 'center', gap: 1.25, mb: 3 }}>
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
            }}
          >
            <ShieldOutlinedIcon sx={{ color: '#fff', fontSize: 22 }} />
          </Box>
          <Typography variant="h6" sx={{ fontWeight: 800 }}>
            CitadelIQ
          </Typography>
        </Stack>
        <Typography variant="h4" component="h1" sx={{ fontWeight: 800, mb: 1 }}>
          {title}
        </Typography>
        {subtitle ? (
          <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>
            {subtitle}
          </Typography>
        ) : null}
        {children}
      </GlassSurface>
    </Box>
  );
}
