import { Chip, Paper, Stack, Typography } from '@mui/material';
import { FileIcon } from '../documents/FileIcon';
import { highlightMatches } from '../../utils/highlightMatches';
import type { SearchResultDto } from '../../types/search';

interface SearchResultCardProps {
  result: SearchResultDto;
  query: string;
}

export function SearchResultCard({ result, query }: SearchResultCardProps) {
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Stack direction="row" sx={{ alignItems: 'center', gap: 1, mb: 0.5 }}>
        <FileIcon fileName={result.fileName} sx={{ fontSize: 20 }} />
        <Typography variant="subtitle2" noWrap sx={{ flex: 1 }}>
          {result.fileName}
        </Typography>
      </Stack>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
        {result.folderPath}
      </Typography>
      <Typography variant="body2" sx={{ mb: 1.5, color: 'text.primary' }}>
        "{highlightMatches(result.chunkText, query)}"
      </Typography>
      <Stack direction="row" sx={{ gap: 1, flexWrap: 'wrap' }}>
        <Chip size="small" variant="outlined" label={`Similarity: ${result.similarityScore.toFixed(2)}`} />
        <Chip size="small" variant="outlined" label={`Chunk ${result.chunkIndex + 1}`} />
        {result.pageNumber != null && <Chip size="small" variant="outlined" label={`Page ${result.pageNumber}`} />}
      </Stack>
    </Paper>
  );
}
