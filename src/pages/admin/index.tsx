import { Routes, Route, useNavigate } from 'react-router-dom';
import { useEffect } from 'react';

// Import shared components
import BottomNavigation from '../../components/bottom-navigation.tsx';

// Import all the individual admin pages
import AdminDashboard from './dashboard/index.tsx';
import AdminAnnouncements from './announcements/index.tsx';
import AdminAttendance from './attendance/index.tsx';
import AdminMembers from './members/index.tsx';
import AdminRoster from './roster/index.tsx';
import AdminCongregation from './congregation/index.tsx';

const AdminPage = () => {
  const navigate = useNavigate();

  useEffect(() => {
    // Redirects to the dashboard when the path is exactly /admin
    if (window.location.pathname === '/admin') {
      navigate('/admin/dashboard', { replace: true });
    }
  }, [navigate]);

  return (
    <div>
      <Routes>
        <Route path="dashboard" element={<AdminDashboard />} />
        <Route path="announcements" element={<AdminAnnouncements />} />
        <Route path="attendance" element={<AdminAttendance />} />
        <Route path="members" element={<AdminMembers />} />
        <Route path="roster" element={<AdminRoster />} />
        <Route path="congregation" element={<AdminCongregation />} /> {/* ADD THIS ROUTE */}
      </Routes>
      <BottomNavigation />
    </div>
  );
};

export default AdminPage;
