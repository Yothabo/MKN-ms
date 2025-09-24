import { Routes, Route } from 'react-router-dom';

// Import the Entry page from its location
import Entry from './pages/entry/index.tsx';

// Import the main admin page from the root of its directory
import AdminLayout from './pages/admin/index.tsx';

function App() {
  return (
    <Routes>
      <Route path="/" element={<Entry />} />
      <Route path="/admin/*" element={<AdminLayout />} />
    </Routes>
  );
}

export default App;

