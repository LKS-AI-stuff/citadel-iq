import { useContext } from 'react';
import { SettingsContext } from './settingsContext';

export function useAppSettings() {
  return useContext(SettingsContext);
}
