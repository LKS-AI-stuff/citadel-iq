import { useCallback, useEffect, useState } from 'react';
import { organizationApi } from '../api/organizationApi';
import { ApiError } from '../api/apiClient';
import type { MemberDto } from '../types/organization';
import type { WorkspaceRole } from '../types/session';

export function useMembers() {
  const [members, setMembers] = useState<MemberDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const refetch = useCallback(async () => {
    try {
      setMembers(await organizationApi.members());
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to load members right now.');
    }
  }, []);

  useEffect(() => {
    refetch();
  }, [refetch]);

  /** Throws an Error with the server's (safe) message; the caller shows it. Always refetches afterwards. */
  const changeRole = async (userId: string, role: WorkspaceRole) => {
    try {
      await organizationApi.changeRole(userId, role);
    } catch (err) {
      throw new Error(err instanceof ApiError ? err.message : 'Unable to change this role right now.');
    } finally {
      await refetch();
    }
  };

  const removeMember = async (userId: string) => {
    try {
      await organizationApi.removeMember(userId);
    } catch (err) {
      throw new Error(err instanceof ApiError ? err.message : 'Unable to remove this member right now.');
    } finally {
      await refetch();
    }
  };

  return { members, error, refetch, changeRole, removeMember };
}
