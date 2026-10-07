import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import './index.css';
import { ColorModeProvider } from './theme/ColorModeProvider';
import { ToastProvider } from './components/common/ToastProvider';
import { SettingsProvider } from './settings/SettingsProvider';
import { SessionProvider } from './session/SessionProvider';
import App from './App.tsx';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ColorModeProvider>
      <ToastProvider>
        <SettingsProvider>
          <SessionProvider>
            <BrowserRouter>
              <App />
            </BrowserRouter>
          </SessionProvider>
        </SettingsProvider>
      </ToastProvider>
    </ColorModeProvider>
  </StrictMode>,
);
