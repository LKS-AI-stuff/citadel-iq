import { useCallback, useEffect, useRef, useState } from 'react';
import { emailApi } from '../api/emailApi';
import { ApiError } from '../api/apiClient';
import type { EmailActivityDto } from '../types/email';

const PAGE_SIZE = 50;

/** Email activity, newest first, paged by arrival time. Members always get only their own emails (server-enforced);
 * `mine` narrows an admin's view the same way. */
export function useEmailActivity(mine: boolean) {
  const [items, setItems] = useState<EmailActivityDto[] | null>(null);
  const [hasMore, setHasMore] = useState(false);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const generation = useRef(0);

  const refetch = useCallback(async () => {
    const current = ++generation.current;
    try {
      const page = await emailApi.activity({ limit: PAGE_SIZE, mine });
      if (current !== generation.current) return;
      setItems(page);
      setHasMore(page.length === PAGE_SIZE);
      setError(null);
    } catch (err) {
      if (current !== generation.current) return;
      setError(err instanceof ApiError ? err.message : 'Unable to load email activity right now.');
    }
  }, [mine]);

  useEffect(() => {
    setItems(null);
    refetch();
  }, [refetch]);

  const loadMore = async () => {
    const last = items?.[items.length - 1];
    if (!last || !items) return;
    // Rows sharing the boundary timestamp: the server skips those already loaded instead of losing the rest.
    const skip = items.filter((i) => i.receivedAtUtc === last.receivedAtUtc).length;
    const current = generation.current;
    setIsLoadingMore(true);
    try {
      const page = await emailApi.activity({ before: last.receivedAtUtc, skip, limit: PAGE_SIZE, mine });
      if (current !== generation.current) return;
      setItems((prev) => [...(prev ?? []), ...page]);
      setHasMore(page.length === PAGE_SIZE);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to load more activity right now.');
    } finally {
      setIsLoadingMore(false);
    }
  };

  return { items, hasMore, isLoadingMore, error, refetch, loadMore };
}
