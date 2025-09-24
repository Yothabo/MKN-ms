import { useState, useRef, useEffect } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { MdMoreHoriz, MdChevronRight } from 'react-icons/md';
import {
  primaryItems,
  navItemVariants,
  textVariants,
  dropdownVariants,
  expandableSections,
  appManagementItems,
  coreManagementItems,
  useDevelopmentModal,
  DevelopmentModal,
} from './BottomNavigationConfig';

const BottomNavigation = () => {
  const [showMenu, setShowMenu] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const location = useLocation();
  const [activeTab, setActiveTab] = useState('');
  const { isModalOpen, modalSection, hideDevelopmentModal, showDevelopmentModal } = useDevelopmentModal();

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setShowMenu(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  useEffect(() => {
    const currentTab = primaryItems.find((item) =>
      location.pathname.startsWith(item.to)
    )?.to || '';
    setActiveTab(currentTab);
    setShowMenu(false);
  }, [location.pathname]);

  const handleMenuItemClick = (label: string) => {
    showDevelopmentModal(label);
    setShowMenu(false);
  };

  return (
    <>
      <motion.nav
        style={{
          position: 'fixed',
          bottom: 0,
          left: 0,
          right: 0,
          height: '80px',
          backgroundColor: '#ffffff',
          boxShadow: '0 -2px 20px rgba(0, 0, 0, 0.1)',
          display: 'flex',
          justifyContent: 'space-around',
          alignItems: 'center',
          padding: '0 16px',
          zIndex: 100,
          borderTopLeftRadius: '16px',
          borderTopRightRadius: '16px',
          maxWidth: '800px',
          margin: '0 auto',
        }}
        initial={{ y: 100, opacity: 0 }}
        animate={{ y: 0, opacity: 1 }}
        transition={{ type: 'spring', stiffness: 300, damping: 25 }}
      >
        {primaryItems.map(({ to, icon: Icon, label }) => (
          <NavLink
            key={to}
            to={to}
            style={{
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              textDecoration: 'none',
              color: '#6B7280',
              flex: 1,
              height: '100%',
            }}
          >
            <motion.div
              variants={navItemVariants}
              initial="inactive"
              animate={activeTab === to ? 'active' : 'inactive'}
              whileHover="hover"
              whileTap="tap"
              style={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                height: '100%',
                padding: '8px 0',
              }}
            >
              <Icon size={activeTab === to ? 20 : 18} color="#6B7280" />
              <AnimatePresence>
                {activeTab === to && (
                  <motion.span
                    style={{
                      marginTop: '4px',
                      fontSize: '10px',
                      fontWeight: 600,
                      color: '#6B7280',
                    }}
                    variants={textVariants}
                    initial="hidden"
                    animate="visible"
                    exit="exit"
                  >
                    {label}
                  </motion.span>
                )}
              </AnimatePresence>
            </motion.div>
          </NavLink>
        ))}

        <div
          ref={menuRef}
          style={{
            position: 'relative',
            height: '100%',
            display: 'flex',
            alignItems: 'center',
            flex: 1,
          }}
        >
          <motion.button
            onClick={() => setShowMenu((prev) => !prev)}
            style={{
              background: 'none',
              border: 'none',
              color: '#6B7280',
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              cursor: 'pointer',
              height: '100%',
              width: '100%',
              padding: 0,
            }}
            variants={navItemVariants}
            initial="inactive"
            animate={showMenu ? 'active' : 'inactive'}
            whileHover="hover"
            whileTap="tap"
          >
            <MdMoreHoriz size={18} color={showMenu ? '#16a34a' : '#6B7280'} />
            <AnimatePresence>
              {showMenu && (
                <motion.span
                  style={{
                    marginTop: '4px',
                    fontSize: '10px',
                    fontWeight: 600,
                    color: '#6B7280',
                  }}
                  variants={textVariants}
                  initial="hidden"
                  animate="visible"
                  exit="exit"
                >
                  More
                </motion.span>
              )}
            </AnimatePresence>
          </motion.button>

          <AnimatePresence>
            {showMenu && (
              <>
                <motion.div
                  style={{
                    position: 'fixed',
                    top: 0,
                    left: 0,
                    right: 0,
                    bottom: '80px',
                    background: 'rgba(255, 255, 255, 0.5)',
                    backdropFilter: 'blur(5px)',
                    zIndex: 150,
                    cursor: 'pointer',
                  }}
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  exit={{ opacity: 0 }}
                  transition={{ duration: 0.2 }}
                  onClick={() => setShowMenu(false)}
                />

                <motion.div
                  style={{
                    position: 'absolute',
                    bottom: '84px',
                    right: '16px',
                    backgroundColor: '#ffffff',
                    boxShadow: '0 8px 24px rgba(0, 0, 0, 0.15)',
                    borderRadius: '12px',
                    padding: '8px 0',
                    display: 'flex',
                    flexDirection: 'column',
                    zIndex: 200,
                    minWidth: '280px',
                    maxHeight: '70vh',
                    overflowY: 'auto',
                  }}
                  variants={dropdownVariants}
                  initial="hidden"
                  animate="visible"
                  exit="exit"
                >
                  <div
                    style={{
                      padding: '8px 16px',
                      fontSize: '10px',
                      fontWeight: 600,
                      color: '#6B7280',
                      textTransform: 'uppercase',
                      letterSpacing: '0.5px',
                      borderBottom: '1px solid #f3f4f6',
                      backgroundColor: '#f9fafb',
                    }}
                  >
                    Core Management
                  </div>

                  {coreManagementItems.map(({ icon: Icon, label }) => (
                    <div
                      key={label}
                      onClick={() => handleMenuItemClick(label)}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        fontSize: '12px',
                        fontWeight: 500,
                        padding: '8px 16px',
                        transition: 'all 0.2s ease',
                        color: '#374151',
                        cursor: 'pointer',
                      }}
                    >
                      <Icon size={16} style={{ marginRight: '8px' }} />
                      <span>{label}</span>
                    </div>
                  ))}

                  {expandableSections.map(({ key, icon: Icon, label }) => (
                    <div key={key}>
                      <div
                        style={{
                          padding: '8px 16px',
                          fontSize: '10px',
                          fontWeight: 600,
                          color: '#6B7280',
                          textTransform: 'uppercase',
                          letterSpacing: '0.5px',
                          borderBottom: '1px solid #f3f4f6',
                          backgroundColor: '#f9fafb',
                          cursor: 'pointer',
                        }}
                        onClick={() => handleMenuItemClick(label)}
                      >
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                          <Icon size={14} style={{ marginRight: '8px' }} />
                          <span>{label}</span>
                          <MdChevronRight size={14} />
                        </div>
                      </div>
                    </div>
                  ))}

                  <div
                    style={{
                      padding: '8px 16px',
                      fontSize: '10px',
                      fontWeight: 600,
                      color: '#6B7280',
                      textTransform: 'uppercase',
                      letterSpacing: '0.5px',
                      borderBottom: '1px solid #f3f4f6',
                      backgroundColor: '#f9fafb',
                    }}
                  >
                    App Management
                  </div>

                  {appManagementItems.map(({ icon: Icon, label }) => (
                    <div
                      key={label}
                      onClick={() => handleMenuItemClick(label)}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        fontSize: '12px',
                        fontWeight: 500,
                        padding: '8px 16px',
                        transition: 'all 0.2s ease',
                        color: '#374151',
                        cursor: 'pointer',
                      }}
                    >
                      <Icon size={16} style={{ marginRight: '8px' }} />
                      <span>{label}</span>
                    </div>
                  ))}
                </motion.div>
              </>
            )}
          </AnimatePresence>
        </div>
      </motion.nav>

      <DevelopmentModal
        isOpen={isModalOpen}
        onClose={hideDevelopmentModal}
        sectionName={modalSection}
      />
    </>
  );
};

export default BottomNavigation;
