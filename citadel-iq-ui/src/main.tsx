import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import './index.css';
import { ColorModeProvider } from './theme/ColorModeProvider';
import { ToastProvider } from './components/common/ToastProvider';
import { SettingsProvider } from './settings/SettingsProvider';
import App from './App.tsx';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ColorModeProvider>
      <ToastProvider>
        <SettingsProvider>
          <BrowserRouter>
            <App />
          </BrowserRouter>
        </SettingsProvider>
      </ToastProvider>
    </ColorModeProvider>
  </StrictMode>,
);
