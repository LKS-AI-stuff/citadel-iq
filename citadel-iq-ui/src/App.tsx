import { Route, Routes } from 'react-router-dom';
import { AppShell } from './app/AppShell';
import { FolderPage } from './app/FolderPage';

function App() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<FolderPage />} />
        <Route path="folders/:folderId" element={<FolderPage />} />
      </Route>
    </Routes>
  );
}

export default App;
