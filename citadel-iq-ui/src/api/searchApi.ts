import { apiClient } from './apiClient';
import type { SearchRequestDto, SearchResultDto } from '../types/search';

export const searchApi = {
  search: (request: SearchRequestDto) => apiClient.post<SearchResultDto[]>('/api/search', request),
};
