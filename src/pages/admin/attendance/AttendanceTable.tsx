import React, { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { AttendanceToggle } from './AttendanceToggle';

interface Member {
  id: string;
  names: string[];
  surname: string;
  cardNumber: string;
  mainBranch: string;
  status: string;
  raCount?: number;
  attendance?: { branch: string; dateOffsetDays: number }[];
  deceased?: boolean;
  dateOfDeath?: string | null;
  dob: Date;
}

interface GuestMember extends Member {
  visitedBranch: string;
  timestamp: number;
}

interface AttendanceTableProps {
  currentMembers?: Member[];
  setMembers?: React.Dispatch<React.SetStateAction<Member[]>>;
  setPresentMembers?: React.Dispatch<React.SetStateAction<string[]>>;
  presentMembers?: string[];
  isCeremonyMode?: boolean;
  isBranchFilterMode?: boolean;
  branchStats?: Record<string, { total: number; present: number }>;
  selectedCeremonies?: string[];
  currentBranch?: string;
  guestMembers?: GuestMember[];
}

export const AttendanceTable = ({
  currentMembers = [],
  setMembers = () => {},
  setPresentMembers = () => {},
  presentMembers = [],
  isCeremonyMode = false,
  isBranchFilterMode = false,
  branchStats = {},
  selectedCeremonies = [],
  currentBranch = '',
  guestMembers = [],
}: AttendanceTableProps) => {
  const [previousRaCounts, setPreviousRaCounts] = useState<Record<string, number>>({});
  const [previousStatus, setPreviousStatus] = useState<Record<string, string>>({});
  const [notification, setNotification] = useState<{ x: number; y: number; message: string } | null>(null);

  // Reset notification when switching modes
  useEffect(() => {
    if (isBranchFilterMode || isCeremonyMode) {
      setNotification(null);
    }
  }, [isBranchFilterMode, isCeremonyMode]);

  const getStatusStyles = (isPresent: boolean, isGuestElsewhere: boolean) => {
    // Only apply special styling if member is a guest elsewhere AND we're in their home branch
    if (isGuestElsewhere && isBranchFilterMode) return 'bg-gray-100 opacity-60 cursor-not-allowed';
    return isPresent ? 'bg-green-100' : '';
  };

  // Check if a member is currently a guest at another branch
  const isMemberGuestElsewhere = (member: Member) => {
    return guestMembers.some(guest => 
      guest.id === member.id && guest.visitedBranch !== currentBranch
    );
  };

  const sortedMembers = [...currentMembers].sort((a, b) => {
    const firstNameA = a.names[0]?.toLowerCase() || '';
    const firstNameB = b.names[0]?.toLowerCase() || '';
    return firstNameA.localeCompare(firstNameB);
  });

  const toggleMemberAttendance = (memberId: string, event?: React.MouseEvent) => {
    if (event) {
      event.stopPropagation();
    }

    const isAllFilter = !isBranchFilterMode && !isCeremonyMode;

    // Check if we're in the main table (not branch or ceremony mode)
    if (isAllFilter) {
      setNotification({
        x: event ? event.clientX : window.innerWidth / 2,
        y: event ? event.clientY + 10 : window.innerHeight / 2,
        message: "This register cannot be interacted with. Please use branch filtering to mark attendance.",
      });
      setTimeout(() => {
        setNotification(null);
      }, 5000);
      return;
    }

    // Check if we're in a branch view and if the member belongs to the current branch
    const member = currentMembers.find(m => m.id === memberId);
    if (isBranchFilterMode && currentBranch && member && member.mainBranch !== currentBranch) {
      setNotification({
        x: event ? event.clientX : window.innerWidth / 2,
        y: event ? event.clientY + 10 : window.innerHeight / 2,
        message: `Cannot edit ${member.names.join(' ')} ${member.surname} from ${member.mainBranch} in ${currentBranch} view.`,
      });
      setTimeout(() => {
        setNotification(null);
      }, 5000);
      return;
    }

    if (!member) return;

    // Check if member is currently a guest at another branch (only in branch filter mode)
    if (isBranchFilterMode && currentBranch && member.mainBranch === currentBranch && isMemberGuestElsewhere(member)) {
      setNotification({
        x: event ? event.clientX : window.innerWidth / 2,
        y: event ? event.clientY + 10 : window.innerHeight / 2,
        message: `Cannot edit ${member.names.join(' ')} ${member.surname} - currently attending as guest at another branch.`,
      });
      setTimeout(() => {
        setNotification(null);
      }, 5000);
      return;
    }

    const isCurrentlyPresent = presentMembers.includes(memberId);

    if (isCurrentlyPresent) {
      // When removing attendance, restore previous status and RA count
      const previousRaCount = previousRaCounts[memberId] ?? member.raCount ?? 0;
      const previousStatusValue = previousStatus[memberId] ?? member.status;

      setMembers(prevMembers =>
        prevMembers.map(m =>
          m.id === memberId
            ? {
                ...m,
                raCount: previousRaCount,
                status: previousStatusValue,
                deceased: previousStatusValue === 'Pre-RA' ? false : m.deceased,
                dateOfDeath: previousStatusValue === 'Pre-RA' ? null : m.dateOfDeath,
              }
            : m
        )
      );

      // Remove from present members
      setPresentMembers(prev => prev.filter(id => id !== memberId));
    } else {
      // Store previous values before making changes
      setPreviousRaCounts(prev => ({
        ...prev,
        [memberId]: member.raCount ?? 0,
      }));
      setPreviousStatus(prev => ({
        ...prev,
        [memberId]: member.status,
      }));

      // STRICT RULE: If member is being marked present, RA count must be 0
      // Special handling for Pre-RA members - they cannot be present while being Pre-RA
      if (member.status.toLowerCase() === 'pre-ra') {
        setMembers(prevMembers =>
          prevMembers.map(m =>
            m.id === memberId
              ? {
                  ...m,
                  status: 'Active',
                  raCount: 0, // ENFORCE: RA count must be 0 when present
                  deceased: false,
                  dateOfDeath: null
                }
              : m
          )
        );
      } else {
        // For all other statuses, ensure RA count is 0 when present
        setMembers(prevMembers =>
          prevMembers.map(m =>
            m.id === memberId
              ? {
                  ...m,
                  raCount: 0 // ENFORCE: RA count must be 0 when present
                }
              : m
          )
        );
      }

      // Add to present members
      setPresentMembers(prev => [...prev, memberId]);
    }
  };

  const getLastAttendanceInfo = (member: Member) => {
    if (!member.attendance || member.attendance.length === 0) {
      return { branch: 'Never', date: null };
    }

    const sortedAttendance = [...member.attendance].sort((a, b) => a.dateOffsetDays - b.dateOffsetDays);
    const lastAttendance = sortedAttendance[0];

    return {
      branch: lastAttendance.branch,
      date: calculateActualDate(lastAttendance.dateOffsetDays),
    };
  };

  const formatDate = (date: Date | string | undefined | null) => {
    if (!date) return 'N/A';
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    return isNaN(dateObj.getTime()) ? 'N/A' : dateObj.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  const calculateActualDate = (offsetDays: number): Date => {
    const date = new Date();
    date.setDate(date.getDate() - offsetDays);
    return date;
  };

  const getCeremonyStats = () => {
    if (!isCeremonyMode || !branchStats) return null;

    try {
      const totalMembers = Object.values(branchStats).reduce((sum, stat) => sum + (stat.total || 0), 0);
      const totalPresent = Object.values(branchStats).reduce((sum, stat) => sum + (stat.present || 0), 0);
      const overallPercentage = totalMembers > 0 ? Math.round((totalPresent / totalMembers) * 100) : 0;

      const branchPercentages = Object.entries(branchStats)
        .filter(([_, stat]) => stat.total > 0)
        .map(([branch, stat]) => {
          const percentage = stat.total > 0 ? Math.round((stat.present / stat.total) * 100) : 0;
          return `${branch}: ${percentage}%`;
        })
        .join(' | ');

      return {
        ceremony: selectedCeremonies.join(', ') || 'Ceremony',
        overallPercentage,
        branchPercentages,
      };
    } catch (err) {
      return null;
    }
  };

  const ceremonyStats = getCeremonyStats();

  // Check if member can be edited in current view
  const canMemberBeEdited = (member: Member) => {
    // In ceremony mode, all members can be edited regardless of branch
    if (isCeremonyMode) return true;
    
    // In branch filter mode, check if member can be edited
    if (isBranchFilterMode) {
      // Members who are guests at other branches cannot be edited at their home branch
      if (member.mainBranch === currentBranch && isMemberGuestElsewhere(member)) {
        return false;
      }
      return true;
    }
    
    // In main table view, all members can be edited
    return true;
  };

  return (
    <div className="relative overflow-y-auto" style={{ maxHeight: 'calc(60vh - 60px)' }}>
      {isCeremonyMode && ceremonyStats && (
        <div className="mb-3 p-3 bg-blue-50 border border-blue-200 rounded-md w-full max-w-md text-left">
          <p className="text-xs text-blue-800">
            <strong>{ceremonyStats.ceremony} Attendance:</strong> {ceremonyStats.overallPercentage}% overall
            <br />
            {ceremonyStats.branchPercentages}
          </p>
        </div>
      )}

      <AnimatePresence>
        {notification && (
          <motion.div
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 1, scale: 1, transition: { duration: 0.3, type: 'spring', stiffness: 100 } }}
            exit={{ opacity: 0, y: -10, transition: { duration: 0.2 } }}
            className="fixed bg-red-500 text-white text-xs p-2 rounded-lg shadow-lg z-50 cursor-pointer"
            style={{ top: notification.y, left: Math.min(notification.x, window.innerWidth - 200) }}
            onClick={() => setNotification(null)}
          >
            {notification.message}
          </motion.div>
        )}
      </AnimatePresence>

      {sortedMembers.length === 0 && (
        <div className="text-left text-gray-500 text-[0.6rem] py-4">
          No members found
        </div>
      )}

      {/* Mobile Table */}
      <div className="md:hidden">
        <table className="min-w-full table-fixed bg-transparent">
          <thead className="bg-white sticky top-0 z-10">
            <tr>
              <th className="px-2 py-1.5 text-left text-[0.55rem] font-semibold text-gray-500 uppercase tracking-wider w-[30%]">
                Name
              </th>
              <th className="px-2 py-1.5 text-left text-[0.55rem] font-semibold text-gray-500 uppercase tracking-wider w-[30%]">
                Last Attended
              </th>
              <th className="px-2 py-1.5 text-left text-[0.55rem] font-semibold text-gray-500 uppercase tracking-wider w-[20%]">
                Status
              </th>
              <th className="px-2 py-1.5 text-left text-[0.55rem] font-semibold text-gray-500 uppercase tracking-wider w-[20%]">
                Present
              </th>
            </tr>
          </thead>
          <tbody className="bg-transparent">
            {sortedMembers.map(member => {
              const lastAttendance = getLastAttendanceInfo(member);
              const isPresent = presentMembers.includes(member.id);
              const isRAMember = member.status === 'RA';
              const canEdit = canMemberBeEdited(member);
              const isGuestElsewhere = isMemberGuestElsewhere(member);

              // Only show guest elsewhere styling in branch filter mode
              const showGuestElsewhereStyle = isBranchFilterMode && isGuestElsewhere;

              // STRICT RULE: If member is present, display Active status instead of Pre-RA
              const displayStatus = isPresent && member.status.toLowerCase() === 'pre-ra'
                ? 'Active'
                : member.status;

              return (
                <tr
                  key={member.id}
                  className={getStatusStyles(isPresent, isGuestElsewhere)}
                  onClick={e => !isRAMember && canEdit && toggleMemberAttendance(member.id, e)}
                >
                  <td className="px-2 py-1.5 whitespace-nowrap">
                    <div className={`text-[0.6rem] font-semibold text-left ${showGuestElsewhereStyle ? 'text-gray-500' : 'text-gray-700'}`}>
                      {member.names.join(' ')} {member.surname}
                    </div>
                    <div className={`text-[0.5rem] text-left ${showGuestElsewhereStyle ? 'text-gray-400' : 'text-gray-500'}`}>
                      Card: {member.cardNumber}
                    </div>
                    {showGuestElsewhereStyle && (
                      <div className="text-[0.4rem] text-orange-600 text-left mt-0.5">
                        Guest at another branch
                      </div>
                    )}
                  </td>
                  <td className="px-2 py-1.5 whitespace-nowrap">
                    <div className={`text-[0.6rem] text-left ${showGuestElsewhereStyle ? 'text-gray-500' : 'text-gray-700'}`}>
                      {lastAttendance.branch}
                    </div>
                    <div className={`text-[0.5rem] text-left ${showGuestElsewhereStyle ? 'text-gray-400' : 'text-gray-500'}`}>
                      {lastAttendance.date ? formatDate(lastAttendance.date) : 'Never'}
                    </div>
                  </td>
                  <td className="px-2 py-1.5 whitespace-nowrap">
                    <div
                      className={`text-[0.5rem] font-medium text-left ${
                        displayStatus.toLowerCase() === 'ra'
                          ? showGuestElsewhereStyle ? 'text-red-400' : 'text-red-600'
                        : displayStatus.toLowerCase() === 'pre-ra'
                          ? showGuestElsewhereStyle ? 'text-orange-400' : 'text-orange-500'
                        : displayStatus.toLowerCase() === 'inactive'
                          ? showGuestElsewhereStyle ? 'text-gray-400' : 'text-gray-500'
                        : showGuestElsewhereStyle ? 'text-green-400' : 'text-green-600'
                      }`}
                    >
                      {displayStatus}
                    </div>
                  </td>
                  <td className="px-2 py-1.5 whitespace-nowrap text-left">
                    <AttendanceToggle
                      isPresent={isPresent}
                      onToggle={e => !isRAMember && canEdit && toggleMemberAttendance(member.id, e)}
                      disabled={isRAMember || !canEdit || isGuestElsewhere}
                    />
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Desktop Table */}
      <div className="hidden md:block">
        <table className="min-w-full table-fixed bg-transparent">
          <thead className="bg-white sticky top-0 z-10">
            <tr>
              <th className="px-2 py-1.5 text-left text-[0.6rem] font-semibold text-gray-500 uppercase tracking-wider w-[60px]">
                Card
              </th>
              <th className="px-2 py-1.5 text-left text-[0.6rem] font-semibold text-gray-500 uppercase tracking-wider w-[100px]">
                Name
              </th>
              <th className="px-2 py-1.5 text-left text-[0.6rem] font-semibold text-gray-500 uppercase tracking-wider w-[100px]">
                Surname
              </th>
              <th className="px-2 py-1.5 text-left text-[0.6rem] font-semibold text-gray-500 uppercase tracking-wider w-[100px]">
                Branch
              </th>
              <th className="px-2 py-1.5 text-left text-[0.6rem] font-semibold text-gray-500 uppercase tracking-wider w-[100px]">
                Last Attended
              </th>
              <th className="px-2 py-1.5 text-left text-[0.6rem] font-semibold text-gray-500 uppercase tracking-wider w-[60px]">
                Status
              </th>
              <th className="px-2 py-1.5 text-left text-[0.6rem] font-semibold text-gray-500 uppercase tracking-wider w-[80px]">
                Present
              </th>
            </tr>
          </thead>
          <tbody className="bg-transparent">
            {sortedMembers.map(member => {
              const lastAttendance = getLastAttendanceInfo(member);
              const isPresent = presentMembers.includes(member.id);
              const isRAMember = member.status === 'RA';
              const canEdit = canMemberBeEdited(member);
              const isGuestElsewhere = isMemberGuestElsewhere(member);

              // Only show guest elsewhere styling in branch filter mode
              const showGuestElsewhereStyle = isBranchFilterMode && isGuestElsewhere;

              // STRICT RULE: If member is present, display Active status instead of Pre-RA
              const displayStatus = isPresent && member.status.toLowerCase() === 'pre-ra'
                ? 'Active'
                : member.status;

              return (
                <tr
                  key={member.id}
                  className={getStatusStyles(isPresent, isGuestElsewhere)}
                  onClick={e => !isRAMember && canEdit && toggleMemberAttendance(member.id, e)}
                >
                  <td className="px-2 py-1.5 whitespace-nowrap text-left">
                    <div className={`text-[0.6rem] ${showGuestElsewhereStyle ? 'text-gray-500' : 'text-gray-600'}`}>
                      {member.cardNumber}
                    </div>
                    {showGuestElsewhereStyle && (
                      <div className="text-[0.4rem] text-orange-600 text-left mt-0.5">
                        Guest elsewhere
                      </div>
                    )}
                  </td>
                  <td className="px-2 py-1.5 whitespace-nowrap text-left">
                    <div className={`text-[0.6rem] ${showGuestElsewhereStyle ? 'text-gray-500' : 'text-gray-700'}`}>
                      {member.names.join(' ')}
                    </div>
                  </td>
                  <td className="px-2 py-1.5 whitespace-nowrap text-left">
                    <div className={`text-[0.6rem] ${showGuestElsewhereStyle ? 'text-gray-500' : 'text-gray-700'}`}>
                      {member.surname}
                    </div>
                  </td>
                  <td className="px-2 py-1.5 whitespace-nowrap text-left">
                    <div className={`text-[0.6rem] ${showGuestElsewhereStyle ? 'text-gray-500' : 'text-gray-700'}`}>
                      {member.mainBranch}
                    </div>
                  </td>
                  <td className="px-2 py-1.5 whitespace-nowrap text-left">
                    <div className={`text-[0.6rem] ${showGuestElsewhereStyle ? 'text-gray-400' : 'text-gray-500'}`}>
                      {lastAttendance.date ? formatDate(lastAttendance.date) : 'Never'}
                    </div>
                  </td>
                  <td className="px-2 py-1.5 whitespace-nowrap text-left">
                    <span
                      className={`text-[0.45rem] font-medium ${
                        displayStatus.toLowerCase() === 'ra'
                          ? showGuestElsewhereStyle ? 'text-red-400' : 'text-red-600'
                        : displayStatus.toLowerCase() === 'pre-ra'
                          ? showGuestElsewhereStyle ? 'text-orange-400' : 'text-orange-500'
                        : displayStatus.toLowerCase() === 'inactive'
                          ? showGuestElsewhereStyle ? 'text-gray-400' : 'text-gray-500'
                        : showGuestElsewhereStyle ? 'text-green-400' : 'text-green-600'
                      }`}
                    >
                      {displayStatus}
                    </span>
                  </td>
                  <td className="px-2 py-1.5 whitespace-nowrap text-left">
                    <AttendanceToggle
                      isPresent={isPresent}
                      onToggle={e => !isRAMember && canEdit && toggleMemberAttendance(member.id, e)}
                      disabled={isRAMember || !canEdit || isGuestElsewhere}
                    />
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default AttendanceTable;
