import { Route, Routes } from 'react-router-dom';
import { AppShell } from './app/AppShell';
import { FolderPage } from './app/FolderPage';
import { SessionGate } from './app/SessionGate';
import { AdminPage } from './pages/AdminPage';

function App() {
  return (
    <SessionGate>
      <Routes>
        <Route element={<AppShell />}>
          <Route index element={<FolderPage />} />
          <Route path="folders/:folderId" element={<FolderPage />} />
          <Route path="admin" element={<AdminPage />} />
        </Route>
      </Routes>
    </SessionGate>
  );
}

export default App;
