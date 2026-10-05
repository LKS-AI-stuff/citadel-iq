import { createContext } from 'react';
import type { AppSettings } from '../types/ask';

/** Used until the server answers (and if it never does) — mirrors the documented backend defaults. */
export const DEFAULT_SETTINGS: AppSettings = {
  askEnabled: true,
  maxQuestionLength: 1000,
  maxHistoryTurns: 10,
  maxHistoryChars: 12000,
};

export const SettingsContext = createContext<AppSettings>(DEFAULT_SETTINGS);
