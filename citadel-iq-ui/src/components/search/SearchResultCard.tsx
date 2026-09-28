import { Box, Chip, LinearProgress, Stack, Tooltip, Typography, alpha } from '@mui/material';
import { FileIcon, getFileAccentColor } from '../documents/FileIcon';
import { GlassSurface } from '../common/GlassSurface';
import { IconBadge } from '../common/IconBadge';
import { highlightMatches } from '../../utils/highlightMatches';
import type { SearchResultDto } from '../../types/search';

interface SearchResultCardProps {
  result: SearchResultDto;
  query: string;
  rank: number;
  animationDelayMs?: number;
}

function similarityColor(score: number): 'success' | 'warning' | 'inherit' {
  if (score >= 0.75) return 'success';
  if (score >= 0.5) return 'warning';
  return 'inherit';
}

export function SearchResultCard({ result, query, rank, animationDelayMs = 0 }: SearchResultCardProps) {
  const accentColor = getFileAccentColor(result.fileName);
  const scorePercent = Math.max(0, Math.min(1, result.similarityScore)) * 100;

  return (
    <GlassSurface
      hover
      radius={16}
      className="animate-fade-in-up"
      sx={{
        p: 2,
        animationDelay: `${animationDelayMs}ms`,
      }}
    >
      <Stack direction="row" sx={{ alignItems: 'flex-start', gap: 1.25, mb: 1 }}>
        <Typography
          variant="caption"
          sx={{
            fontWeight: 800,
            color: 'text.disabled',
            minWidth: 20,
            mt: 0.4,
          }}
        >
          #{rank}
        </Typography>
        <IconBadge color={accentColor} size={32}>
          <FileIcon fileName={result.fileName} sx={{ fontSize: 16 }} />
        </IconBadge>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography variant="subtitle2" noWrap sx={{ fontWeight: 700 }}>
            {result.fileName}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {result.folderPath}
          </Typography>
        </Box>
      </Stack>

      <Typography
        variant="body2"
        sx={{
          mb: 1.5,
          color: 'text.primary',
          borderLeft: '2px solid',
          borderColor: (t) => alpha(t.palette.primary.main, 0.25),
          ml: '19px',
          pl: 1.5,
          py: 0.25,
        }}
      >
        "{highlightMatches(result.chunkText, query)}"
      </Typography>

      <Stack direction="row" sx={{ alignItems: 'center', gap: 1.25, flexWrap: 'wrap', pl: '19px' }}>
        <Tooltip title="Cosine similarity between the query and this chunk — a relative ranking score, not a probability.">
          <Stack direction="row" sx={{ alignItems: 'center', gap: 0.75, minWidth: 130 }}>
            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, whiteSpace: 'nowrap' }}>
              Similarity {result.similarityScore.toFixed(2)}
            </Typography>
            <LinearProgress
              variant="determinate"
              value={scorePercent}
              color={similarityColor(result.similarityScore)}
              sx={{ width: 56, height: 5 }}
            />
          </Stack>
        </Tooltip>
        <Chip size="small" variant="outlined" label={`Chunk ${result.chunkIndex + 1}`} />
        {result.pageNumber != null && <Chip size="small" variant="outlined" label={`Page ${result.pageNumber}`} />}
      </Stack>
    </GlassSurface>
  );
}
