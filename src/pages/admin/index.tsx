import { Routes, Route, Navigate } from 'react-router-dom';
import BottomNavigation from '../../components/bottom-navigation.tsx';
import AdminDashboard from './dashboard/index.tsx';
import AdminAnnouncements from './announcements/index.tsx';
import AdminAttendance from './attendance/index.tsx';
import AdminMembers from './members/index.tsx';
import AdminRoster from './roster/index.tsx';
import AdminCongregation from './congregation/index.tsx';

const isAuthenticated = () => {
  return !!localStorage.getItem('authToken');
};

const AdminPage = () => {
  if (!isAuthenticated()) {
    return <Navigate to="/" replace />;
  }

  return (
    <div>
      <Routes>
        <Route path="/" element={<AdminDashboard />} />
        <Route path="dashboard" element={<AdminDashboard />} />
        <Route path="attendance" element={<AdminAttendance />} />
        <Route path="announcements" element={<AdminAnnouncements />} />
        <Route path="members" element={<AdminMembers />} />
        <Route path="roster" element={<AdminRoster />} />
        <Route path="congregation" element={<AdminCongregation />} />
        <Route path="*" element={<Navigate to="/admin/dashboard" replace />} />
      </Routes>
      <BottomNavigation />
    </div>
  );
};

export default AdminPage;
