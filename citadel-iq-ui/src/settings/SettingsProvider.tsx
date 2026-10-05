import { useEffect, useState, type ReactNode } from 'react';
import { settingsApi } from '../api/settingsApi';
import { DEFAULT_SETTINGS, SettingsContext } from './settingsContext';
import type { AppSettings } from '../types/ask';

/** Fetches the client-safe settings once at load and caches them for the session. */
export function SettingsProvider({ children }: { children: ReactNode }) {
  const [settings, setSettings] = useState<AppSettings>(DEFAULT_SETTINGS);

  useEffect(() => {
    let cancelled = false;
    settingsApi
      .get()
      .then((loaded) => {
        if (!cancelled) setSettings(loaded);
      })
      .catch(() => {
        // Keep the defaults; the server enforces the real limits anyway.
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return <SettingsContext.Provider value={settings}>{children}</SettingsContext.Provider>;
}
