import { Stack, Typography } from '@mui/material';
import { SearchResultCard } from './SearchResultCard';
import { EmptyState } from '../common/EmptyState';
import SearchOffRoundedIcon from '@mui/icons-material/SearchOffRounded';
import type { SearchResultDto } from '../../types/search';

interface SearchResultsProps {
  results: SearchResultDto[];
  query: string;
}

export function SearchResults({ results, query }: SearchResultsProps) {
  if (results.length === 0) {
    return (
      <EmptyState
        title="No matching results"
        description="Try a different phrasing, or widen the search scope."
        icon={<SearchOffRoundedIcon sx={{ fontSize: 32, color: 'primary.main' }} />}
      />
    );
  }

  return (
    <Stack spacing={1.5}>
      <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
        {results.length} result{results.length === 1 ? '' : 's'}, ranked by similarity
      </Typography>
      {results.map((result, index) => (
        <SearchResultCard
          key={`${result.documentId}-${result.chunkIndex}`}
          result={result}
          query={query}
          rank={index + 1}
          animationDelayMs={index * 40}
        />
      ))}
    </Stack>
  );
}
