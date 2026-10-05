import { Box, Chip, Stack, Typography, alpha } from '@mui/material';
import AutoAwesomeOutlinedIcon from '@mui/icons-material/AutoAwesomeOutlined';
import { GlassSurface } from './GlassSurface';

/** A persistent "what the assistant does" blurb — lives outside the main content column so it
 * never mounts/unmounts on navigation (see AppShell), avoiding any layout shift. */
export function AssistantInfoPanel() {
  return (
    <GlassSurface component="aside" sx={{ p: { xs: 3, sm: 3.5 } }}>
      <Typography variant="overline" sx={{ color: 'primary.main', fontWeight: 700, letterSpacing: '0.08em' }}>
        AI document assistant
      </Typography>
      <Typography variant="h5" sx={{ mb: 1.5, mt: 0.5 }}>
        Ask your documents, get cited answers
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2.5 }}>
        Ask a question in plain English and CitadelIQ writes a short answer using only what's in your
        documents. Every statement points to the exact passage it came from, so you can check it
        yourself — and if your documents don't cover it, the assistant says so instead of guessing.
      </Typography>

      <GlassSurface
        radius={16}
        sx={{ p: 2.25, bgcolor: (t) => alpha(t.palette.background.paper, t.palette.mode === 'light' ? 0.4 : 0.2) }}
      >
        <Stack direction="row" sx={{ alignItems: 'center', gap: 1, mb: 1.5 }}>
          <AutoAwesomeOutlinedIcon sx={{ fontSize: 18, color: 'primary.main' }} />
          <Typography variant="caption" sx={{ fontWeight: 700, letterSpacing: '0.04em' }}>
            TRY ASKING
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
            "Employees are eligible for 15 days of annual vacation [1]."
          </Typography>
        </Box>
        <Stack direction="row" sx={{ gap: 1, flexWrap: 'wrap' }}>
          <Chip size="small" color="success" label="[1] HR Policy.pdf · page 3" />
          <Chip size="small" variant="outlined" label="Grounded in your files" />
        </Stack>
      </GlassSurface>

      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 2 }}>
        Prefer raw results? Switch to "Passages only" in the search panel (Ctrl/⌘ K) to see ranked passages by meaning.
      </Typography>
    </GlassSurface>
  );
}
