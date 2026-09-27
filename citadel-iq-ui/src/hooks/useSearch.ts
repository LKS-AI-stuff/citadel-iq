import { useState } from 'react';
import { searchApi } from '../api/searchApi';
import { ApiError } from '../api/apiClient';
import type { SearchResultDto, SearchScope } from '../types/search';

export function useSearch(currentFolderId: string) {
  const [query, setQuery] = useState('');
  const [scope, setScope] = useState<SearchScope>('EntirePortal');
  const [results, setResults] = useState<SearchResultDto[] | null>(null);
  const [searchedQuery, setSearchedQuery] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const search = async () => {
    if (!query.trim()) {
      setError('Enter a search term.');
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const trimmedQuery = query.trim();
      const data = await searchApi.search({
        query: trimmedQuery,
        currentFolderId,
        searchScope: scope,
      });
      setResults(data);
      setSearchedQuery(trimmedQuery);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Search failed. Please try again.');
      setResults(null);
    } finally {
      setIsLoading(false);
    }
  };

  const clear = () => {
    setQuery('');
    setResults(null);
    setError(null);
  };

  return { query, setQuery, scope, setScope, results, searchedQuery, isLoading, error, search, clear };
}
