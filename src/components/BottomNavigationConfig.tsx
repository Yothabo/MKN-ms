import { motion, AnimatePresence, type Variants } from 'framer-motion';
import {
  MdSpaceDashboard,
  MdGroups,
  MdCalendarViewMonth,
  MdNotifications,
  MdBusiness,
  MdDesignServices,
  MdAddCircleOutline,
  MdAssessment,
  MdSchool,
  MdSummarize,
  MdLocalLibrary,
  MdSettings,
  MdPerson,
} from 'react-icons/md';
import { useState } from 'react';

export const primaryItems = [
  { to: '/admin/dashboard', icon: MdSpaceDashboard, label: 'Dashboard' },
  { to: '/admin/members', icon: MdGroups, label: 'Members' },
  { to: '/admin/attendance', icon: MdCalendarViewMonth, label: 'Attendance' },
  { to: '/admin/congregation', icon: MdNotifications, label: 'Congregation' },
];

export const navItemVariants: Variants = {
  inactive: {
    scale: 1,
    y: 0,
    transition: { type: 'spring', stiffness: 400, damping: 17 },
  },
  active: {
    scale: 1.1,
    y: -5,
    transition: { type: 'spring', stiffness: 400, damping: 17 },
  },
  hover: {
    scale: 1.1,
    transition: { type: 'spring', stiffness: 400, damping: 10 },
  },
  tap: {
    scale: 0.95,
    transition: { type: 'spring', stiffness: 400, damping: 10 },
  },
};

export const textVariants: Variants = {
  hidden: { opacity: 0, y: 5, scale: 0.9 },
  visible: {
    opacity: 1,
    y: 0,
    scale: 1,
    transition: { type: 'spring', stiffness: 300, damping: 15 },
  },
  exit: {
    opacity: 0,
    y: 5,
    scale: 0.9,
    transition: { duration: 0.15 },
  },
};

export const dropdownVariants: Variants = {
  hidden: { opacity: 0, y: 20 },
  visible: {
    opacity: 1,
    y: 0,
    transition: { type: 'spring', stiffness: 300, damping: 20 },
  },
  exit: {
    opacity: 0,
    y: 20,
    transition: { duration: 0.2 },
  },
};

export const nestedContainerVariants: Variants = {
  hidden: { opacity: 0, height: 0 },
  visible: {
    opacity: 1,
    height: 'auto',
    transition: { duration: 0.2 },
  },
  exit: {
    opacity: 0,
    height: 0,
    transition: { duration: 0.2 },
  },
};

export const expandableSections = [
  { key: 'create', icon: MdAddCircleOutline, label: 'Create' },
  { key: 'raManagement', icon: MdAssessment, label: 'RA Management' },
  { key: 'youth', icon: MdSchool, label: 'Youth' },
  { key: 'congregation', icon: MdLocalLibrary, label: 'Congregation' },
  { key: 'reports', icon: MdSummarize, label: 'Reports' },
];

export const sectionItems = {
  create: [
    { to: '/admin/create/member', label: 'New Member' },
    { to: '/admin/create/branch', label: 'New Branch' },
    { to: '/admin/create/service', label: 'New Service' },
    { to: '/admin/create/announcement', label: 'New Announcement' },
    { to: '/admin/create/roster', label: 'New Roster' },
  ],
  raManagement: [
    { to: '/admin/ra/current', label: 'Current RA' },
    { to: '/admin/ra/pre-ra', label: 'Pre-RA' },
    { to: '/admin/ra/summary', label: 'RA Summary' },
    { to: '/admin/ra/history', label: 'RA History' },
  ],
  youth: [
    { to: '/admin/youth/activities', label: 'Activities' },
    { to: '/admin/youth/projects', label: 'Projects' },
    { to: '/admin/youth/events', label: 'Events' },
  ],
  congregation: [
    { to: '/admin/congregation/projects', label: 'Projects' },
    { to: '/admin/congregation/calendar', label: 'Year Calendar' },
  ],
  reports: [
    { to: '/admin/reports/members', label: 'Member Reports' },
    { to: '/admin/reports/attendance', label: 'Attendance Reports' },
    { to: '/admin/reports/service', label: 'Service Reports' },
    { to: '/admin/reports/financial', label: 'Financial Reports' },
  ],
};

export const appManagementItems = [
  { to: '/admin/app/settings', icon: MdSettings, label: 'Settings' },
  { to: '/admin/app/profile', icon: MdPerson, label: 'Profile' },
];

export const coreManagementItems = [
  { to: '/admin/branches', icon: MdBusiness, label: 'Branch Management' },
  { to: '/admin/services', icon: MdDesignServices, label: 'Service Management' },
];

export const useDevelopmentModal = () => {
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [modalSection, setModalSection] = useState('');

  const showDevelopmentModal = (sectionName: string) => {
    setModalSection(sectionName);
    setIsModalOpen(true);
  };

  const hideDevelopmentModal = () => {
    setIsModalOpen(false);
    setModalSection('');
  };

  return {
    isModalOpen,
    modalSection,
    showDevelopmentModal,
    hideDevelopmentModal,
  };
};

export const DevelopmentModal = ({
  isOpen,
  onClose,
  sectionName,
}: {
  isOpen: boolean;
  onClose: () => void;
  sectionName: string;
}) => {
  if (!isOpen) return null;

  return (
    <AnimatePresence>
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        transition={{ duration: 0.2 }}
        style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          zIndex: 1000,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
        onClick={onClose}
      >
        <motion.div
          initial={{ scale: 0.8, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
          exit={{ scale: 0.8, opacity: 0 }}
          transition={{ duration: 0.2 }}
          style={{
            backgroundColor: '#dc2626',
            color: 'white',
            padding: '24px',
            borderRadius: '12px',
            boxShadow: '0 10px 25px rgba(0, 0, 0, 0.2)',
            maxWidth: '320px',
            width: '90%',
            textAlign: 'center',
            zIndex: 1001,
          }}
          onClick={(e) => e.stopPropagation()}
        >
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              marginBottom: '16px',
              fontSize: '24px',
            }}
          >
            <MdNotifications />
          </div>
          <h3
            style={{
              fontSize: '18px',
              fontWeight: 'bold',
              marginBottom: '8px',
            }}
          >
            Section Under Development
          </h3>
          <p
            style={{
              fontSize: '14px',
              marginBottom: '20px',
              opacity: 0.9,
            }}
          >
            The {sectionName} section is currently under development and not available yet.
          </p>
          <button
            onClick={onClose}
            style={{
              backgroundColor: 'white',
              color: '#dc2626',
              border: 'none',
              padding: '10px 20px',
              borderRadius: '6px',
              fontWeight: 'bold',
              cursor: 'pointer',
              width: '100%',
              transition: 'background-color 0.2s',
            }}
            onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = '#f3f4f6')}
            onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'white')}
          >
            OK
          </button>
        </motion.div>
      </motion.div>
    </AnimatePresence>
  );
};
