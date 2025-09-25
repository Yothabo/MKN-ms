import { Routes, Route, useLocation, useNavigate } from 'react-router-dom';
import { useEffect } from 'react';
import Entry from './pages/entry';
import AdminLayout from './pages/admin';
import NotFound from './components/NotFound';
import InstallBanner from './components/InstallBanner';

function RoutePersistence({ children }: { children: React.ReactNode }) {
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    localStorage.setItem('lastRoute', location.pathname);
  }, [location]);

  useEffect(() => {
    const last = localStorage.getItem('lastRoute');
    if (last && last !== location.pathname) {
      navigate(last, { replace: true });
    }
  }, [location.pathname, navigate]);

  return <>{children}</>;
}

export default function App() {
  return (
    <>
      <RoutePersistence>
        <Routes>
          <Route path="/" element={<Entry />} />
          <Route path="/admin/*" element={<AdminLayout />} />
          <Route path="*" element={<NotFound />} />
        </Routes>
      </RoutePersistence>
      <InstallBanner />
    </>
  );
}
