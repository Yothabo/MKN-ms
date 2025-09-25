import { Routes, Route } from 'react-router-dom';
import Entry from './pages/entry/index.tsx';
import AdminLayout from './pages/admin/index.tsx';
import NotFound from './components/NotFound';

function App() {
  return (
    <Routes>
      <Route path="/" element={<Entry />} />
      <Route path="/admin/*" element={<AdminLayout />} />
      <Route path="*" element={<NotFound />} /> {/* Catch-all */}
    </Routes>
  );
}

export default App;
