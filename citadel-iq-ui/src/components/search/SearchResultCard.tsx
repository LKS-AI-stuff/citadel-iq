import { Box, ButtonBase, Chip, IconButton, LinearProgress, Stack, Tooltip, Typography, alpha, useTheme } from '@mui/material';
import DownloadOutlinedIcon from '@mui/icons-material/DownloadOutlined';
import { FileIcon } from '../documents/FileIcon';
import { UploaderCaption } from '../documents/UploaderCaption';
import { GlassSurface } from '../common/GlassSurface';
import { IconBadge } from '../common/IconBadge';
import { actionIconButtonSx } from '../../theme/glass';
import { highlightMatches } from '../../utils/highlightMatches';
import type { SearchResultDto } from '../../types/search';

interface SearchResultCardProps {
  result: SearchResultDto;
  query: string;
  /** Position in a ranked results list; ignored when `citationNumber` is given. */
  rank?: number;
  animationDelayMs?: number;
  /** Set when the card is an answer source: shows `[n]` instead of the rank. */
  citationNumber?: number;
  /** Briefly emphasises the card (e.g. after clicking its citation chip). */
  highlighted?: boolean;
  /** When given, the file badge and a Download button call it. */
  onDownload?: () => void;
}

function similarityColor(score: number): 'success' | 'warning' | 'inherit' {
  if (score >= 0.75) return 'success';
  if (score >= 0.5) return 'warning';
  return 'inherit';
}

export function SearchResultCard({
  result,
  query,
  rank,
  animationDelayMs = 0,
  citationNumber,
  highlighted = false,
  onDownload,
}: SearchResultCardProps) {
  const theme = useTheme();
  const accentColor = theme.palette.success.main;
  const scorePercent = Math.max(0, Math.min(1, result.similarityScore)) * 100;

  const badgeContent = (
    <IconBadge color={accentColor} size={32}>
      <FileIcon fileName={result.fileName} sx={{ fontSize: 16, color: accentColor }} />
    </IconBadge>
  );
  const badge = onDownload ? (
    <Tooltip title="Download original file">
      <ButtonBase onClick={onDownload} aria-label={`Download ${result.fileName}`} sx={{ borderRadius: '12px' }}>
        {badgeContent}
      </ButtonBase>
    </Tooltip>
  ) : (
    badgeContent
  );

  return (
    <GlassSurface
      hover
      radius={16}
      className="animate-fade-in-up"
      sx={{
        p: 2,
        animationDelay: `${animationDelayMs}ms`,
        transition: 'box-shadow 0.3s ease',
        boxShadow: highlighted ? (t) => `0 0 0 2px ${t.palette.primary.main}` : undefined,
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
          {citationNumber != null ? `[${citationNumber}]` : `#${rank}`}
        </Typography>
        {badge}
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography variant="subtitle2" noWrap sx={{ fontWeight: 700 }}>
            {result.fileName}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {result.folderPath}
          </Typography>
          <UploaderCaption uploadedBy={result.uploadedBy} />
        </Box>
        {onDownload && (
          <Tooltip title="Download">
            <IconButton size="small" onClick={onDownload} aria-label="Download" sx={actionIconButtonSx(accentColor)}>
              <DownloadOutlinedIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
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
        {result.sheetName != null && <Chip size="small" variant="outlined" label={`Sheet: ${result.sheetName}`} />}
      </Stack>
    </GlassSurface>
  );
}
