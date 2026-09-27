import { Stack } from '@mui/material';
import { SearchResultCard } from './SearchResultCard';
import { EmptyState } from '../common/EmptyState';
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
      />
    );
  }

  return (
    <Stack spacing={1.5}>
      {results.map((result) => (
        <SearchResultCard key={`${result.documentId}-${result.chunkIndex}`} result={result} query={query} />
      ))}
    </Stack>
  );
}
