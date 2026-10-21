import { useCallback, useEffect, useState } from 'react';
import { emailApi } from '../api/emailApi';

const REFRESH_MS = 60_000;

/** Pending drafts for the review badge: fetched on demand and every 60 s while the page is visible. Owners/Admins
 * of organizations only (`enabled`); 0 otherwise. */
export function usePendingDraftCount(enabled: boolean) {
  const [count, setCount] = useState(0);

  const refetch = useCallback(async () => {
    if (!enabled) return;
    try {
      setCount((await emailApi.pendingCount()).pending);
    } catch {
      // keep the last value; the badge is a hint, not data
    }
  }, [enabled]);

  useEffect(() => {
    if (!enabled) {
      return;
    }

    refetch();
    const interval = setInterval(() => {
      if (document.visibilityState === 'visible') refetch();
    }, REFRESH_MS);
    const onVisible = () => {
      if (document.visibilityState === 'visible') refetch();
    };
    document.addEventListener('visibilitychange', onVisible);
    return () => {
      clearInterval(interval);
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, [enabled, refetch]);

  return { count: enabled ? count : 0, refetch };
}
