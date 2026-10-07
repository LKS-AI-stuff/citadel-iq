import { useCallback, useEffect, useState } from 'react';
import { organizationApi } from '../api/organizationApi';
import { ApiError } from '../api/apiClient';
import type { JoinRequestDto } from '../types/organization';
import type { WorkspaceRole } from '../types/session';

export function useJoinRequests(onMembershipChanged: () => void) {
  const [requests, setRequests] = useState<JoinRequestDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const refetch = useCallback(async () => {
    try {
      setRequests(await organizationApi.joinRequests());
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to load join requests right now.');
    }
  }, []);

  useEffect(() => {
    refetch();
  }, [refetch]);

  const approve = async (requestId: string, role: WorkspaceRole) => {
    try {
      await organizationApi.approve(requestId, role);
      onMembershipChanged();
    } catch (err) {
      throw new Error(err instanceof ApiError ? err.message : 'Unable to approve this request right now.');
    } finally {
      await refetch();
    }
  };

  const reject = async (requestId: string) => {
    try {
      await organizationApi.reject(requestId);
    } catch (err) {
      throw new Error(err instanceof ApiError ? err.message : 'Unable to reject this request right now.');
    } finally {
      await refetch();
    }
  };

  return { requests, error, approve, reject };
}
