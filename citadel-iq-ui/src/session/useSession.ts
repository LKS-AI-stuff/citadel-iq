import { useContext } from 'react';
import { SessionContext } from './sessionContext';

export function useSession() {
  return useContext(SessionContext);
}

/** For components that only render inside an active workspace (behind SessionGate). */
export function useActiveWorkspace() {
  const { session, can } = useContext(SessionContext);
  if (!session?.workspace || !session.role) {
    throw new Error('useActiveWorkspace used outside an active session.');
  }

  return { workspace: session.workspace, role: session.role, user: session.user, can };
}
