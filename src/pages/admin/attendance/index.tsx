import { useState, useEffect } from 'react';
import { useAttendance } from './useAttendance';
import { SearchAndFilter } from './SearchAndFilter';
import { AttendanceTable } from './AttendanceTable';
import { RAStatusCard } from './RAStatusCard';
import { NotificationBanner } from './NotificationBanner';
import { useBranchStats } from './useBranchStats';
import { useGuestAttendance } from './useGuestAttendance';
import { useSearch } from './useSearch';
import MKNLogo from '../../../assets/MKN.png';

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

const AttendancePage = () => {
  const attendanceData = useAttendance();
  const [showBranchFilter, setShowBranchFilter] = useState(false);
  const [selectedStatus, setSelectedStatus] = useState<string[]>([]);
  const [presentMembers, setPresentMembers] = useState<string[]>([]);
  const [guestMembers, setGuestMembers] = useState<GuestMember[]>([]);

  const { searchTerm, setSearchTerm } = useSearch(attendanceData.allMembers as any);
  const { calculateBranchStats, formatBranchStats } = useBranchStats(
    attendanceData.currentMembers,
    presentMembers
  );
  const {
    notification,
    setNotification,
    handleAddGuestToBranch: handleGuestAttendance,
  } = useGuestAttendance(attendanceData.allMembers, presentMembers, attendanceData.setMembers as any, setPresentMembers);

  const branchStats = calculateBranchStats();
  const isBranchFilterMode = attendanceData.selectedBranches.length > 0 && !attendanceData.selectedBranches.includes('All');

  useEffect(() => {
    const updatedGuestMembers = guestMembers.filter(guest => presentMembers.includes(guest.id));
    if (updatedGuestMembers.length !== guestMembers.length) {
      setGuestMembers(updatedGuestMembers);
    }
  }, [presentMembers, guestMembers]);

  const toggleStatusFilter = (status: string) => {
    setSelectedStatus(prev => (prev.includes(status) ? prev.filter(s => s !== status) : [...prev, status]));
  };

  const handleAddGuestToBranch = (memberId: string, currentBranch: string) => {
    const member = attendanceData.allMembers.find(m => m.id === memberId);
    if (!member) return;

    const updatedMembers = attendanceData.allMembers.map(m => {
      if (m.id === memberId) {
        const newAttendance = {
          branch: currentBranch,
          dateOffsetDays: 0,
        };
        return {
          ...m,
          attendance: [...(m.attendance || []), newAttendance],
        };
      }
      return m;
    });

    attendanceData.setMembers(updatedMembers as any);
    setPresentMembers(prev => [...prev, memberId]);

    const guestMember: any = {
      ...member,
      visitedBranch: currentBranch,
      timestamp: Date.now(),
    };
    setGuestMembers(prev => [...prev, guestMember]);
    handleGuestAttendance(memberId, currentBranch);
  };

  const handleRemoveGuestFromBranch = (memberId: string) => {
    const member = attendanceData.allMembers.find(m => m.id === memberId);
    if (!member) return;

    setPresentMembers(prev => prev.filter(id => id !== memberId));
    setGuestMembers(prev => prev.filter(guest => guest.id !== memberId));

    const updatedMembers = attendanceData.allMembers.map(m => {
      if (m.id === memberId && m.attendance) {
        const currentBranch = attendanceData.selectedBranches[0];
        const filteredAttendance = m.attendance.filter(att => !(att.branch === currentBranch && att.dateOffsetDays === 0));
        return {
          ...m,
          attendance: filteredAttendance,
        };
      }
      return m;
    });

    attendanceData.setMembers(updatedMembers as any);
  };

  const filteredMembers = attendanceData.currentMembers.filter(member => {
    const statusMatch = selectedStatus.length === 0 || selectedStatus.includes(member.status);
    const branchMatch =
      attendanceData.selectedBranches.length === 0 ||
      attendanceData.selectedBranches.includes('All') ||
      attendanceData.selectedBranches.includes(member.mainBranch);

    const isGuestElsewhere = guestMembers.some(
      guest => guest.id === member.id && guest.visitedBranch !== member.mainBranch
    );
    const shouldHide = isBranchFilterMode && isGuestElsewhere;

    return statusMatch && branchMatch && !shouldHide;
  });

  return (
    <div className="flex flex-col min-h-screen pb-[80px]">
      <header className="sticky top-0 z-50 w-full bg-white shadow-sm">
        <div className="flex items-center px-4 py-3">
          <img src={MKNLogo} alt="MKN Logo" className="h-8 w-auto" />
          <div className="ml-2 flex flex-col">
            <span className="text-lg font-semibold text-gray-800 tracking-wider">Muzi Ka Nkulunkulu</span>
            <p className="text-[0.65rem] text-gray-400 -mt-0.5 tracking-tight">where illnesses and troubles are cured</p>
          </div>
        </div>
      </header>

      {notification && (
        <NotificationBanner
          message={notification.message}
          type={notification.type}
          onClose={() => setNotification(null)}
        />
      )}

      <div className="p-4">
        <div className="mb-3 w-full">
          <SearchAndFilter
            searchTerm={searchTerm}
            setSearchTerm={setSearchTerm}
            selectedBranches={attendanceData.selectedBranches}
            toggleBranchFilter={attendanceData.toggleBranchFilter}
            setSelectedBranches={attendanceData.setSelectedBranches}
            selectedStatus={selectedStatus}
            toggleStatusFilter={toggleStatusFilter}
            setSelectedStatus={setSelectedStatus}
            showBranchFilter={showBranchFilter}
            setShowBranchFilter={setShowBranchFilter}
            totalMembers={filteredMembers.length}
            isBranchFilterMode={isBranchFilterMode}
            branchStats={formatBranchStats(branchStats)}
            presentMembers={presentMembers}
            setPresentMembers={setPresentMembers}
            currentBranch={attendanceData.selectedBranches[0] || ''}
            onAddGuestToBranch={handleAddGuestToBranch}
            onRemoveGuestFromBranch={handleRemoveGuestFromBranch}
            guestMembers={guestMembers}
            allMembers={attendanceData.allMembers as any}
          />
        </div>

        <div className="flex-1 overflow-hidden">
          <AttendanceTable
            currentMembers={filteredMembers as any}
            setMembers={attendanceData.setMembers as any}
            setPresentMembers={setPresentMembers}
            presentMembers={presentMembers}
            isBranchFilterMode={isBranchFilterMode}
            branchStats={branchStats}
            currentBranch={attendanceData.selectedBranches[0]}
            guestMembers={guestMembers}
          />

          <div className="mt-4">
            <RAStatusCard
              members={attendanceData.allMembers as any}
              setMembers={attendanceData.setMembers as any}
              selectedBranch={attendanceData.selectedBranches?.[0] || 'All'}
              presentMembers={presentMembers}
              setPresentMembers={setPresentMembers}
            />
          </div>
        </div>
      </div>
    </div>
  );
};

export default AttendancePage;

