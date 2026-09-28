import { Box, Divider, Link, Stack, Typography, alpha } from '@mui/material';
import { GlassSurface } from '../components/common/GlassSurface';

interface FooterProps {
  onHomeClick: () => void;
  onSearchClick: () => void;
}

export function Footer({ onHomeClick, onSearchClick }: FooterProps) {
  const year = new Date().getFullYear();

  return (
    <Box component="footer" sx={{ px: { xs: 2, sm: 3, md: 4 }, pb: { xs: 3, md: 4 }, pt: 2, mt: 4 }}>
      {/* No mx: 'auto' — matches TopNavigation: capping width without centering keeps the gap
          to the sidebar constant instead of growing with leftover space. */}
      <GlassSurface sx={{ maxWidth: '1320px', width: '100%', p: { xs: 3, sm: 4 }, textAlign: 'center' }}>
        <Stack direction="row" sx={{ justifyContent: 'center', gap: { xs: 2, sm: 4 }, mb: 2.5, flexWrap: 'wrap' }}>
          <Link component="button" underline="hover" color="text.secondary" onClick={onHomeClick} sx={{ fontSize: 14, fontWeight: 600 }}>
            Home
          </Link>
          <Link component="button" underline="hover" color="text.secondary" onClick={onSearchClick} sx={{ fontSize: 14, fontWeight: 600 }}>
            Semantic search (⌘K)
          </Link>
          <Link
            underline="hover"
            color="text.secondary"
            href="https://platform.openai.com/docs/guides/embeddings"
            target="_blank"
            rel="noreferrer"
            sx={{ fontSize: 14, fontWeight: 600 }}
          >
            How embeddings work
          </Link>
        </Stack>
        <Divider sx={{ borderColor: (t) => alpha(t.palette.text.primary, 0.1), mb: 2 }} />
        <Typography variant="caption" color="text.secondary">
          © {year} CitadelIQ — AI-powered document intelligence.
        </Typography>
      </GlassSurface>
    </Box>
  );
}
