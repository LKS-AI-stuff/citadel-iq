import { Box, Chip, Stack, Typography, alpha } from '@mui/material';
import AutoAwesomeOutlinedIcon from '@mui/icons-material/AutoAwesomeOutlined';
import { GlassSurface } from './GlassSurface';

/** A persistent "how search works" blurb — lives outside the main content column so it
 * never mounts/unmounts on navigation (see AppShell), avoiding any layout shift. */
export function SearchInfoPanel() {
  return (
    <GlassSurface component="aside" sx={{ p: { xs: 3, sm: 3.5 } }}>
      <Typography variant="overline" sx={{ color: 'primary.main', fontWeight: 700, letterSpacing: '0.08em' }}>
        AI-powered semantic search
      </Typography>
      <Typography variant="h5" sx={{ mb: 1.5, mt: 0.5 }}>
        Search by meaning, not keywords
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2.5 }}>
        Every document you upload here is chunked and embedded automatically, so a question phrased
        in plain English can surface the right passage even when the wording doesn't match.
      </Typography>

      <GlassSurface
        radius={16}
        sx={{ p: 2.25, bgcolor: (t) => alpha(t.palette.background.paper, t.palette.mode === 'light' ? 0.4 : 0.2) }}
      >
        <Stack direction="row" sx={{ alignItems: 'center', gap: 1, mb: 1.5 }}>
          <AutoAwesomeOutlinedIcon sx={{ fontSize: 18, color: 'primary.main' }} />
          <Typography variant="caption" sx={{ fontWeight: 700, letterSpacing: '0.04em' }}>
            TRY A SEARCH
          </Typography>
        </Stack>
        <Typography variant="body2" sx={{ fontStyle: 'italic', mb: 1.5 }}>
          "How many vacation days can an employee take?"
        </Typography>
        <Box
          sx={{
            borderLeft: '2px solid',
            borderColor: (t) => alpha(t.palette.primary.main, 0.4),
            pl: 1.5,
            py: 0.5,
            mb: 1.5,
          }}
        >
          <Typography variant="body2" color="text.secondary">
            "Employees are eligible for 15 days of annual vacation…"
          </Typography>
        </Box>
        <Stack direction="row" sx={{ gap: 1, flexWrap: 'wrap' }}>
          <Chip size="small" color="success" label="Similarity 0.91" />
          <Chip size="small" variant="outlined" label="No shared keywords" />
        </Stack>
      </GlassSurface>
    </GlassSurface>
  );
}
