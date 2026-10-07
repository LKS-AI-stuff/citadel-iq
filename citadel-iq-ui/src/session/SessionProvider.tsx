import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { ApiError, onAuthFailure } from '../api/apiClient';
import { sessionApi } from '../api/sessionApi';
import { NO_CAPABILITIES, SessionContext, type SessionCapabilities, type SessionState } from './sessionContext';
import { ROLE_RANK, type SessionDto } from '../types/session';

function capabilitiesOf(session: SessionDto | null): SessionCapabilities {
  if (session?.status !== 'Active' || !session.role || !session.workspace) {
    return NO_CAPABILITIES;
  }

  const isAdmin = ROLE_RANK[session.role] >= ROLE_RANK.Admin;
  return {
    deleteContent: isAdmin,
    renameFolders: isAdmin,
    administer: isAdmin && session.workspace.kind === 'Organization',
  };
}

/**
 * Loads `GET /api/me` once at start and whenever asked. Any API call answering 401 flips the app to "signed out";
 * a 403 "account closed" re-reads the session so the closed-account page shows. Roles are never trusted from here
 * for security — the server enforces them — this only decides what the UI offers.
 */
export function SessionProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<SessionState['state']>('loading');
  const [session, setSessionValue] = useState<SessionDto | null>(null);

  const refresh = useCallback(async () => {
    try {
      const me = await sessionApi.me();
      setSessionValue(me);
      setState('ready');
    } catch (err) {
      setSessionValue(null);
      setState(err instanceof ApiError && err.status === 401 ? 'signedOut' : 'error');
    }
  }, []);

  const setSession = useCallback((next: SessionDto) => {
    setSessionValue(next);
    setState('ready');
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  useEffect(() => {
    onAuthFailure((status) => {
      if (status === 401) {
        setSessionValue(null);
        setState('signedOut');
      } else {
        // Closed account, unfinished onboarding, or a role that changed elsewhere (e.g. demoted by an admin):
        // re-read the session so the UI stops offering what the server now refuses.
        refresh();
      }
    });
    return () => onAuthFailure(null);
  }, [refresh]);

  const value = useMemo<SessionState>(
    () => ({ state, session, can: capabilitiesOf(session), refresh, setSession }),
    [state, session, refresh, setSession],
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}
