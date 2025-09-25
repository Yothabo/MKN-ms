// src/App.tsx
import { Routes, Route } from 'react-router-dom';
import { useRegisterSW } from 'virtual:pwa-register/react';
import Entry from './pages/entry/index.tsx';
import AdminLayout from './pages/admin/index.tsx';
import NotFound from './components/NotFound';

function App() {
  const { needRefresh, offlineReady, updateServiceWorker } = useRegisterSW();

  return (
    <>
      <Routes>
        <Route path="/" element={<Entry />} />
        <Route path="/admin/*" element={<AdminLayout />} />
        <Route path="*" element={<NotFound />} />
      </Routes>
      {needRefresh && (
        <button onClick={() => updateServiceWorker(true)}>Update</button>
      )}
      {offlineReady && <p>App ready offline</p>}
    </>
  );
}

export default App;
