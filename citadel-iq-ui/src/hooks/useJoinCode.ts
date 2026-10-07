import { useCallback, useEffect, useState } from 'react';
import { organizationApi } from '../api/organizationApi';
import { ApiError } from '../api/apiClient';

export function useJoinCode() {
  const [code, setCode] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isRegenerating, setIsRegenerating] = useState(false);

  const load = useCallback(async () => {
    try {
      setCode((await organizationApi.joinCode()).code);
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to load the join code right now.');
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const regenerate = async () => {
    setIsRegenerating(true);
    try {
      setCode((await organizationApi.regenerateJoinCode()).code);
    } catch (err) {
      throw new Error(err instanceof ApiError ? err.message : 'Unable to regenerate the join code right now.');
    } finally {
      setIsRegenerating(false);
    }
  };

  return { code, error, isRegenerating, regenerate };
}
