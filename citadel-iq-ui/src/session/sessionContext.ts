import { createContext } from 'react';
import type { SessionDto } from '../types/session';

export interface SessionCapabilities {
  /** Delete documents and folders (Admin and Owner). */
  deleteContent: boolean;
  /** Rename folders (Admin and Owner). */
  renameFolders: boolean;
  /** The organization admin page (Admin and Owner of an organization). */
  administer: boolean;
}

export interface SessionState {
  /** `loading` until /api/me answers; `signedOut` on 401; `error` if the API is unreachable. */
  state: 'loading' | 'signedOut' | 'error' | 'ready';
  session: SessionDto | null;
  can: SessionCapabilities;
  refresh: () => Promise<void>;
  /** Replaces the session with one the server just returned (onboarding calls return the new session). */
  setSession: (session: SessionDto) => void;
}

export const NO_CAPABILITIES: SessionCapabilities = { deleteContent: false, renameFolders: false, administer: false };

export const SessionContext = createContext<SessionState>({
  state: 'loading',
  session: null,
  can: NO_CAPABILITIES,
  refresh: async () => {},
  setSession: () => {},
});
