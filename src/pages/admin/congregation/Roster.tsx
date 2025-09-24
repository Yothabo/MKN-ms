import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { FiEdit, FiSave, FiX, FiChevronDown, FiChevronLeft, FiChevronRight, FiDownload, FiInfo } from 'react-icons/fi';
import { mockDataTest } from '../../../types/mockDataTest';
import { autoAssignDuties, populatePastWeek } from './autoAssignDuties';

// Member interface aligned with mockDataTest.ts
interface Member {
  id: string;
  names: string[];
  surname: string;
  cardNumber: number | string;
  dob: Date;
  virginityStatus: 'unset' | 'virgin' | 'non-virgin' | 'inapplicable';
  dateOfEntry: Date;
  reasonOfEntry: string;
  gender: 'unset' | 'male' | 'female' | 'other';
  position: string;
  mainBranch: 'unset' | 'Johannesburg' | 'Bulawayo' | 'Gaborone' | 'Harare' | 'Ireland';
  phone: string;
  email: string;
  otpPreference: 'unset' | 'phone' | 'email';
  password: string;
  role: 'member' | 'admin' | 'super_admin';
  lastSeenOffsetDays: number;
  raCount: number;
  status: 'Active' | 'Pre-RA' | 'RA' | 'Inactive';
  reAdmissionOffsetDays: number | null;
  raHistory: {
    dateOffsetDays: number;
    reason: string;
    removedDate: string | null;
    removedReason: string | null;
  }[];
  trackingActive: boolean;
  preRaWarningSentOffsetDays: number | null;
  globalLock: boolean;
  lockStatus: 'Unlocked' | 'RA Lock' | 'Warning' | 'Deceased' | 'Untracked';
  attendance: {
    dateOffsetDays: number;
    branch: 'unset' | 'Johannesburg' | 'Bulawayo' | 'Gaborone' | 'Harare' | 'Ireland';
    type: 'Regular' | 'Guest';
    timestampOffsetHours: number;
    markedBy: string;
  }[];
  selfManagementData: {
    messages: string[];
    attendanceSummary: {
      totalAttendances: number;
      last90Days: number;
      raCount: number;
    };
  };
  deceasedInfo?: {
    dateOfDeath: Date;
    reason: string;
    memorialServiceDate: Date;
    deathCertificateNumber?: string;
  };
  lastLogin?: Date;
  lastPasswordChange?: Date;
  failedLoginAttempts?: number;
  accountActivated?: boolean;
  profilePicture?: string;
  emergencyContact?: {
    name: string;
    relationship: string;
    phone: string;
  };
  notes?: string[];
  permissions?: {
    canManageAttendance?: boolean;
    canManageMembers?: boolean;
    canManageFinance?: boolean;
  };
}

// Minimal Member interface for autoAssignDuties compatibility
interface AssignableMember {
  id: string;
  name: string;
  mainBranch: string;
  position: string;
  cardNumber: string | number;
  status: string;
  phone: string;
  email: string;
  virginityStatus?: boolean;
  age?: number;
}

interface DutyAssignment {
  [duty: string]: string[];
}

interface DutyHistory {
  totalPoints: number;
  chair?: { count: number; points: number };
  reader?: { count: number; points: number };
  'word reader'?: { count: number; points: number };
  messenger?: { count: number; points: number };
  facilitator?: { count: number; points: number };
  evangelist?: { count: number; points: number };
  announcements?: { count: number; points: number };
}

type DutyKey = 'chair' | 'reader' | 'word reader' | 'messenger' | 'facilitator' | 'evangelist' | 'announcements';

interface RosterProps {
  searchTerm: string;
}

const dutyPoints: { [key: string]: number } = {
  chair: 10,
  reader: 8,
  'word reader': 8,
  messenger: 6,
  facilitator: 6,
  evangelist: 7,
  announcements: 5,
};

const Roster: React.FC<RosterProps> = ({ searchTerm }) => {
  const duties = useMemo(() => ['chair', 'reader', 'word reader', 'messenger', 'facilitator', 'evangelist', 'announcements'] as DutyKey[], []);
  const dutyTranslations: { [key: string]: string } = {
    chair: 'Umgcini sihlalo',
    reader: 'Inhloko zendima',
    'word reader': 'Imfundiso',
    messenger: 'Izithunywa',
    facilitator: 'Abakhokheli',
    evangelist: 'Omele Abavangeli',
    announcements: 'Izaziso',
  };

  const [members, setMembers] = useState<Member[]>([]);
  const [dutyAssignments, setDutyAssignments] = useState<DutyAssignment>({});
  const [isEditing, setIsEditing] = useState(false);
  const [editingDuty, setEditingDuty] = useState<string | null>(null);
  const [selectedServiceType, setSelectedServiceType] = useState('Evening');
  const [selectedBranch, setSelectedBranch] = useState('Select Branch');
  const [showMemberDetails, setShowMemberDetails] = useState<{ [key: string]: boolean }>({});
  const [showBranchDropdown, setShowBranchDropdown] = useState(false);
  const [showServiceDropdown, setShowServiceDropdown] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const branches = useMemo(() => ['Select Branch', 'Johannesburg', 'Bulawayo', 'Gaborone', 'Harare', 'Ireland'], []);
  const daysOfWeek = useMemo(() => ['MONDAY', 'TUESDAY', 'WEDNESDAY', 'THURSDAY', 'FRIDAY', 'SATURDAY', 'SUNDAY'], []);
  const [currentWeekOffset, setCurrentWeekOffset] = useState(0);

  const today = useMemo(() => {
    const date = new Date();
    date.setHours(0, 0, 0, 0);
    return date;
  }, []);

  const currentDayIndex = useMemo(() => (today.getDay() === 0 ? 6 : today.getDay() - 1), [today]);
  const [selectedDate, setSelectedDate] = useState<Date>(today);
  const [selectedDay, setSelectedDay] = useState<string>(daysOfWeek[currentDayIndex]);

  const getServiceTypeForDay = useCallback((day: string) => {
    switch (day) {
      case 'MONDAY':
      case 'TUESDAY':
      case 'WEDNESDAY':
      case 'FRIDAY':
        return 'Evening';
      case 'THURSDAY':
        return 'Afternoon';
      case 'SATURDAY':
      case 'SUNDAY':
        return selectedServiceType;
      default:
        return 'Evening';
    }
  }, [selectedServiceType]);

  const serviceTypes = useMemo(() => {
    if (['SATURDAY', 'SUNDAY'].includes(selectedDay)) {
      return ['Morning', 'Afternoon'];
    }
    return [getServiceTypeForDay(selectedDay)];
  }, [selectedDay, getServiceTypeForDay]);

  useEffect(() => {
    const serviceType = getServiceTypeForDay(selectedDay);
    setSelectedServiceType(serviceType);
    setShowServiceDropdown(false);
  }, [selectedDay, getServiceTypeForDay]);

  const getWeekDates = useCallback((weekOffset: number) => {
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    const dayOfWeek = now.getDay();
    const startDate = new Date(now);
    startDate.setDate(now.getDate() - dayOfWeek + (dayOfWeek === 0 ? -6 : 1) + weekOffset * 7);
    const weekDates = [];
    for (let i = 0; i < 7; i++) {
      const date = new Date(startDate);
      date.setDate(startDate.getDate() + i);
      date.setHours(0, 0, 0, 0);
      weekDates.push(date);
    }
    return weekDates;
  }, []);

  const weekDates = useMemo(() => getWeekDates(currentWeekOffset), [currentWeekOffset, getWeekDates]);

  const isDateEditable = useCallback(() => {
    const todayNormalized = new Date(today);
    todayNormalized.setHours(0, 0, 0, 0);
    const selected = new Date(selectedDate);
    selected.setHours(0, 0, 0, 0);
    return selected >= todayNormalized;
  }, [selectedDate, today]);

  const formatDateKey = useCallback((date: Date) => {
    return date.toISOString().split('T')[0];
  }, []);

  const normalizeDutyAssignments = useCallback((assignments: DutyAssignment): DutyAssignment => {
    const normalized: DutyAssignment = {};
    Object.keys(assignments).forEach(key => {
      const lowerKey = key.toLowerCase();
      if (lowerKey === 'holy messenger') {
        normalized['messenger'] = assignments[key];
      } else if (duties.includes(lowerKey as DutyKey)) {
        normalized[lowerKey as DutyKey] = assignments[key];
      }
    });
    return normalized;
  }, [duties]);

  const migrateLegacyData = useCallback((dateKey: string, branch: string) => {
    const legacyKey = branch === 'Select Branch' ? `dutyAssignments_${dateKey}` : `dutyAssignments_${dateKey}_${branch}`;
    const legacyData = localStorage.getItem(legacyKey);
    if (legacyData) {
      try {
        const parsed = JSON.parse(legacyData);
        const serviceType = getServiceTypeForDay(selectedDay);
        const newKey = branch === 'Select Branch' ? `dutyAssignments_${dateKey}_${serviceType}` : `dutyAssignments_${dateKey}_${branch}_${serviceType}`;
        localStorage.setItem(newKey, JSON.stringify(parsed));
        localStorage.removeItem(legacyKey);
        return parsed;
      } catch (e) {
        console.error(`Error migrating legacy data for ${legacyKey}:`, e);
        return null;
      }
    }
    return null;
  }, [selectedDay, getServiceTypeForDay]);

  const transformToAssignableMember = useCallback((member: Member): AssignableMember => {
    const age = member.dob
      ? Math.floor((Date.now() - new Date(member.dob).getTime()) / (1000 * 60 * 60 * 24 * 365.25))
      : undefined;
    return {
      id: member.id,
      name: member.names.join(' ') + ' ' + member.surname,
      mainBranch: member.mainBranch,
      position: member.position,
      cardNumber: member.cardNumber,
      status: member.status,
      phone: member.phone,
      email: member.email,
      virginityStatus: member.virginityStatus === 'virgin',
      age,
    };
  }, []);

  useEffect(() => {
    setIsLoading(true);
    setError(null);
    if (!mockDataTest.members || mockDataTest.members.length === 0) {
      setError('No members available in mockDataTest.');
      setMembers([]);
      setDutyAssignments({});
      setIsLoading(false);
      return;
    }

    setMembers(mockDataTest.members);
    const assignableMembers = mockDataTest.members.map(transformToAssignableMember);
    populatePastWeek(assignableMembers, duties, selectedBranch);
    const dateKey = formatDateKey(selectedDate);
    const storageKey =
      selectedBranch === 'Select Branch'
        ? `dutyAssignments_${dateKey}_${selectedServiceType}`
        : `dutyAssignments_${dateKey}_${selectedBranch}_${selectedServiceType}`;
    let parsedAssignments: DutyAssignment = {};
    const savedAssignments = localStorage.getItem(storageKey);
    try {
      if (savedAssignments) {
        parsedAssignments = JSON.parse(savedAssignments);
        parsedAssignments = normalizeDutyAssignments(parsedAssignments);
        if (Object.keys(parsedAssignments).length > 0) {
          setDutyAssignments(parsedAssignments);
          setIsLoading(false);
          return;
        }
      }
    } catch (e) {
      console.error(`Error parsing localStorage for ${storageKey}:`, e);
      localStorage.removeItem(storageKey);
    }

    const legacyAssignments = migrateLegacyData(dateKey, selectedBranch);
    if (legacyAssignments) {
      parsedAssignments = normalizeDutyAssignments(legacyAssignments);
      if (Object.keys(parsedAssignments).length > 0) {
        setDutyAssignments(parsedAssignments);
        localStorage.setItem(storageKey, JSON.stringify(parsedAssignments));
        setIsLoading(false);
        return;
      }
    }

    if (isDateEditable() && selectedBranch !== 'Select Branch') {
      const filteredMembers = mockDataTest.members
        .map(transformToAssignableMember)
        .filter(member => {
          const matchesSearch =
            searchTerm === '' ||
            member.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
            String(member.cardNumber).toLowerCase().includes(searchTerm.toLowerCase()) ||
            member.position.toLowerCase().includes(searchTerm.toLowerCase());
          const matchesBranch = selectedBranch === 'Select Branch' || member.mainBranch === selectedBranch;
          return matchesSearch && matchesBranch;
        });

      if (filteredMembers.length === 0 && selectedBranch !== 'Select Branch') {
        setError(`No members found for branch "${selectedBranch}" matching the search criteria.`);
        setDutyAssignments({});
        setIsLoading(false);
        return;
      }
      const newAssignments = autoAssignDuties(filteredMembers, duties, selectedDay, selectedBranch, selectedDate);
      const normalizedAssignments = normalizeDutyAssignments(newAssignments);

      setDutyAssignments(normalizedAssignments);
    } else {
      setDutyAssignments({});
    }
    setIsLoading(false);
  }, [selectedDay, selectedBranch, selectedServiceType, selectedDate, searchTerm, duties, normalizeDutyAssignments, formatDateKey, isDateEditable, migrateLegacyData, transformToAssignableMember]);

  useEffect(() => {
    if (Object.keys(dutyAssignments).length > 0 && isDateEditable() && selectedBranch !== 'Select Branch') {
      const dateKey = formatDateKey(selectedDate);
      const storageKey =
        selectedBranch === 'Select Branch'
          ? `dutyAssignments_${dateKey}_${selectedServiceType}`
          : `dutyAssignments_${dateKey}_${selectedBranch}_${selectedServiceType}`;
      try {
        localStorage.setItem(storageKey, JSON.stringify(dutyAssignments));
      } catch (e) {
        console.error(`Error saving to localStorage for ${storageKey}:`, e);
      }
    }
  }, [dutyAssignments, selectedDate, selectedBranch, selectedServiceType, formatDateKey, isDateEditable]);

  const filteredMembers = useMemo(() => {
    return members.filter(member => {
      const fullName = member.names.join(' ') + ' ' + member.surname;
      const matchesSearch =
        searchTerm === '' ||
        fullName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        String(member.cardNumber).toLowerCase().includes(searchTerm.toLowerCase()) ||
        member.position.toLowerCase().includes(searchTerm.toLowerCase());
      const matchesBranch = selectedBranch === 'Select Branch' || member.mainBranch === selectedBranch;
      return matchesSearch && matchesBranch;
    });
  }, [members, searchTerm, selectedBranch]);

  const getMemberName = useCallback((memberId: string) => {
    const member = members.find(m => m.id === memberId);
    return member ? `${member.names.join(' ')} ${member.surname}` : 'Unassigned';
  }, [members]);

  const getMemberPosition = useCallback((memberId: string) => {
    const member = members.find(m => m.id === memberId);
    return member ? member.position : '';
  }, [members]);

  const getMemberCardNumber = useCallback((memberId: string) => {
    const member = members.find(m => m.id === memberId);
    return member ? String(member.cardNumber) : '';
  }, [members]);

  const getMemberPhone = useCallback((memberId: string) => {
    const member = members.find(m => m.id === memberId);
    return member ? member.phone : '';
  }, [members]);

  const getMemberEmail = useCallback((memberId: string) => {
    const member = members.find(m => m.id === memberId);
    return member ? member.email : '';
  }, [members]);

  const handleAssignDuty = useCallback((duty: DutyKey, memberId: string, index: number = 0) => {
    if (!isDateEditable() || selectedBranch === 'Select Branch') {
      return;
    }
    setDutyAssignments(prev => {
      const currentAssignments = prev[duty] || [];
      const newAssignments = [...currentAssignments];
      if (index < newAssignments.length) {
        newAssignments[index] = memberId;
      } else {
        newAssignments.push(memberId);
      }
      return { ...prev, [duty]: newAssignments };
    });

    const historyKey = `memberDutyHistory_${selectedBranch}_${selectedServiceType}`;
    let memberDutyHistory: { [memberId: string]: DutyHistory } = {};
    try {
      const savedHistory = localStorage.getItem(historyKey);
      if (savedHistory) {
        memberDutyHistory = JSON.parse(savedHistory);
      }
    } catch (e) {
      console.error(`Error parsing memberDutyHistory for ${historyKey}:`, e);
    }

    if (!memberDutyHistory[memberId]) {
      memberDutyHistory[memberId] = {
        totalPoints: 0,
        chair: { count: 0, points: 0 },
        reader: { count: 0, points: 0 },
        'word reader': { count: 0, points: 0 },
        messenger: { count: 0, points: 0 },
        facilitator: { count: 0, points: 0 },
        evangelist: { count: 0, points: 0 },
        announcements: { count: 0, points: 0 },
      };
    }

    memberDutyHistory[memberId][duty] = {
      count: (memberDutyHistory[memberId][duty]?.count || 0) + 1,
      points: (memberDutyHistory[memberId][duty]?.points || 0) + dutyPoints[duty],
    };
    memberDutyHistory[memberId].totalPoints += dutyPoints[duty];

    try {
      localStorage.setItem(historyKey, JSON.stringify(memberDutyHistory));
    } catch (e) {
      console.error(`Error saving memberDutyHistory for ${historyKey}:`, e);
    }
    setEditingDuty(null);
  }, [isDateEditable, selectedBranch, selectedServiceType, duties, dutyPoints]);

  const handleClearDuty = useCallback((duty: DutyKey, index: number) => {
    if (!isDateEditable() || selectedBranch === 'Select Branch') return;
    setDutyAssignments(prev => ({
      ...prev,
      [duty]: prev[duty] ? prev[duty].filter((_, i) => i !== index) : [],
    }));
  }, [isDateEditable, selectedBranch]);

  const toggleEditMode = useCallback(() => {
    if (!isDateEditable() || selectedBranch === 'Select Branch') return;
    setIsEditing(prev => !prev);
    setEditingDuty(null);
  }, [isDateEditable, selectedBranch]);

  const toggleMemberDetails = useCallback((memberId: string) => {
    setShowMemberDetails(prev => ({ ...prev, [memberId]: !prev[memberId] }));
  }, []);

  const toggleBranchDropdown = useCallback(() => {
    setShowBranchDropdown(prev => !prev);
    setShowServiceDropdown(false);
  }, []);

  const toggleServiceDropdown = useCallback(() => {
    if (['SATURDAY', 'SUNDAY'].includes(selectedDay)) {
      setShowServiceDropdown(prev => !prev);
      setShowBranchDropdown(false);
    }
  }, [selectedDay]);

  const selectBranch = useCallback((branch: string) => {
    setSelectedBranch(branch);
    setShowBranchDropdown(false);
  }, []);

  const selectService = useCallback((service: string) => {
    setSelectedServiceType(service);
    setShowServiceDropdown(false);
  }, []);

  const selectDay = useCallback((dayIndex: number) => {
    const dayName = daysOfWeek[dayIndex];
    const newDate = new Date(weekDates[dayIndex]);
    newDate.setHours(0, 0, 0, 0);
    setSelectedDay(dayName);
    setSelectedDate(newDate);
  }, [weekDates, daysOfWeek]);

  const navigateWeek = useCallback((direction: 'prev' | 'next') => {
    const minOffset = -12;
    const maxOffset = 0;
    setCurrentWeekOffset(prev => {
      const newOffset = direction === 'prev' ? prev - 1 : prev + 1;
      return Math.max(minOffset, Math.min(maxOffset, newOffset));
    });
  }, []);

  const getCompleteDate = useCallback(() => {
    return selectedDate.toLocaleDateString('en-US', {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    });
  }, [selectedDate]);

  const requiresTwoAssignments = useCallback((duty: string) => {
    return duty === 'facilitator' || duty === 'messenger';
  }, []);

  const getEligibleMembersForDuty = useCallback(
    (duty: DutyKey) => {
      try {
        const chairAssignedIds = dutyAssignments['chair'] || [];
        return filteredMembers.map(transformToAssignableMember).filter(member => {
          if (chairAssignedIds.includes(member.id)) return false;

          const positionLower = member.position.toLowerCase();
          if (duty === 'messenger') {
            return positionLower === 'messenger' || positionLower === 'holy messenger';
          } else if (duty === 'evangelist' || duty === 'announcements') {
            return positionLower === 'evangelist';
          } else if (duty === 'chair') {
            if (selectedDay === 'WEDNESDAY') {
              return (
                member.virginityStatus &&
                member.age !== undefined &&
                member.age >= 13 &&
                member.age <= 35 &&
                positionLower !== 'evangelist' &&
                positionLower !== 'messenger' &&
                positionLower !== 'holy messenger'
              );
            }
            return positionLower !== 'messenger' && positionLower !== 'holy messenger';
          } else if (duty === 'reader' || duty === 'word reader') {
            return positionLower !== 'messenger' && positionLower !== 'holy messenger';
          } else if (duty === 'facilitator') {
            return positionLower === 'facilitator' || filteredMembers.length < 10;
          }
          return true;
        });
      } catch (e) {
        console.error('Error in getEligibleMembersForDuty:', e);
        return [];
      }
    },
    [dutyAssignments, filteredMembers, selectedDay, transformToAssignableMember]
  );

  const handleExportRoster = useCallback(() => {
    alert('Export functionality is currently disabled.');
  }, []);

  const handleViewMemberHistory = useCallback(() => {
    alert('Member history view is currently disabled.');
  }, []);

  const fadeVariants: any = {
    hidden: { opacity: 0 },
    visible: { opacity: 1, transition: { duration: 0.5, ease: 'easeInOut' } },
  };

  const buttonVariants: any = {
    hover: { scale: 1.05, transition: { duration: 0.3, ease: 'easeInOut' } },
    tap: { scale: 0.95, transition: { duration: 0.2, ease: 'easeInOut' } },
  };

  const modalItemVariants: any = {
    hover: { backgroundColor: '#f3f4f6', transition: { duration: 0.3, ease: 'easeInOut' } },
    tap: { scale: 0.98, transition: { duration: 0.2, ease: 'easeInOut' } },
  };

  return (
    <div className="roster-container bg-transparent rounded-lg flex flex-col overflow-y-auto">
      <div className="p-3 border-b border-white/30">
        <div className="flex flex-col gap-3">
          <div className="flex justify-between items-center">
            <h3 className="text-gray-500 font-semibold text-sm flex items-center gap-2">
              <svg
                className="h-4 w-4"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
                xmlns="http://www.w3.org/2000/svg"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"
                />
              </svg>
              <span>{getCompleteDate()}</span>
            </h3>
            <div className="flex gap-2">
              {selectedBranch !== 'Select Branch' && isDateEditable() && (
                <button
                  onClick={toggleEditMode}
                  className={`p-1 rounded transition-colors ${isEditing ? 'bg-white/30' : 'bg-white/20 hover:bg-white/30'}`}
                  title={isEditing ? 'Save Roster' : 'Edit Roster'}
                >
                  {isEditing ? <FiSave className="h-4 w-4 text-gray-700" /> : <FiEdit className="h-4 w-4 text-gray-700" />}
                </button>
              )}
              <button
                onClick={handleExportRoster}
                disabled={true}
                className="p-1 rounded bg-white/20 opacity-50 cursor-not-allowed"
                title="Export Roster (Disabled)"
              >
                <FiDownload className="h-4 w-4 text-gray-700" />
              </button>
            </div>
          </div>
          <div className="flex justify-between items-center text-gray-700 text-[0.6rem]">
            <div className="flex items-center relative">
              <span className="font-medium">Branch:</span>
              <div className="ml-1 relative">
                <button
                  onClick={toggleBranchDropdown}
                  className="flex items-center bg-white/10 px-2 py-1 rounded text-[0.6rem] text-gray-700"
                >
                  {selectedBranch}
                  <FiChevronDown className="ml-1 h-3 w-3" />
                </button>
                <AnimatePresence>
                  {showBranchDropdown && (
                    <motion.div
                      className="absolute top-full left-0 mt-1 bg-white rounded shadow-lg z-[1000] w-40"
                      variants={fadeVariants}
                      initial="hidden"
                      animate="visible"
                      exit="hidden"
                    >
                      {branches.map(branch => (
                        <motion.div
                          key={branch}
                          onClick={() => selectBranch(branch)}
                          className="px-3 py-2 text-xs text-gray-700 hover:bg-gray-100 cursor-pointer"
                          variants={modalItemVariants}
                          whileHover="hover"
                          whileTap="tap"
                        >
                          {branch}
                        </motion.div>
                      ))}
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>
            </div>
            <div className="flex items-center relative">
              <span className="font-medium">Service:</span>
              <div className="ml-1 relative">
                {['SATURDAY', 'SUNDAY'].includes(selectedDay) ? (
                  <button
                    onClick={toggleServiceDropdown}
                    className="flex items-center bg-white/10 px-2 py-1 rounded text-[0.6rem] text-gray-700"
                  >
                    {selectedServiceType}
                    <FiChevronDown className="ml-1 h-3 w-3" />
                  </button>
                ) : (
                  <span className="px-2 py-1 text-[0.6rem] text-gray-700">{selectedServiceType}</span>
                )}
                <AnimatePresence>
                  {showServiceDropdown && ['SATURDAY', 'SUNDAY'].includes(selectedDay) && (
                    <motion.div
                      className="absolute top-full right-0 mt-1 bg-white rounded shadow-lg z-[1000] w-40"
                      variants={fadeVariants}
                      initial="hidden"
                      animate="visible"
                      exit="hidden"
                    >
                      {serviceTypes.map(service => (
                        <motion.div
                          key={service}
                          onClick={() => selectService(service)}
                          className="px-3 py-2 text-xs text-gray-700 hover:bg-gray-100 cursor-pointer"
                          variants={modalItemVariants}
                          whileHover="hover"
                          whileTap="tap"
                        >
                          {service}
                        </motion.div>
                      ))}
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>
            </div>
          </div>
        </div>
      </div>
      <div className="h-[60px] px-3 flex items-center">
        <div className="flex justify-center items-center w-full">
          <button
            onClick={() => navigateWeek('prev')}
            disabled={currentWeekOffset <= -12}
            className={`p-1 mr-2 ${currentWeekOffset <= -12 ? 'opacity-50 cursor-not-allowed' : 'hover:bg-white/20'}`}
          >
            <FiChevronLeft className="h-4 w-4 text-gray-700" />
          </button>
          <div className="flex space-x-1 bg-white/10 rounded p-1">
            {weekDates.map((date, index) => {
              const dayName = daysOfWeek[index];
              const dayAbbr = dayName.slice(0, 3);
              const dayNumber = date.getDate();
              const isSelected = selectedDay === dayName && selectedDate.getTime() === date.getTime();
              return (
                <motion.button
                  key={index}
                  onClick={() => selectDay(index)}
                  className={`px-2 py-1 rounded text-[0.5rem] font-medium flex flex-col items-center ${
                    isSelected ? 'bg-green-500 text-white' : 'text-gray-700 hover:bg-white/20'
                  }`}
                  variants={buttonVariants}
                  whileHover="hover"
                  whileTap="tap"
                  layout
                  transition={{ duration: 0.3, ease: 'easeInOut' }}
                >
                  <span>{dayAbbr}</span>
                  <span className="text-[0.5rem] mt-1">{dayNumber}</span>
                </motion.button>
              );
            })}
          </div>
          <button
            onClick={() => navigateWeek('next')}
            disabled={currentWeekOffset >= 0}
            className={`p-1 ml-2 ${currentWeekOffset >= 0 ? 'opacity-50 cursor-not-allowed' : 'hover:bg-white/20'}`}
          >
            <FiChevronRight className="h-4 w-4 text-gray-700" />
          </button>
        </div>
      </div>
      <div className="relative z-0 p-0.5 overflow-y-auto">
        {isLoading ? (
          <div className="text-gray-700 text-center text-[0.6rem] p-4">Loading roster...</div>
        ) : error ? (
          <div className="text-red-500 text-center text-[0.6rem] p-4">{error}</div>
        ) : selectedBranch === 'Select Branch' ? (
          <div className="flex flex-col items-center justify-center text-gray-700 text-center p-8">
            <div className="bg-white/10 p-6 rounded-lg max-w-md">
              <h3 className="text-[0.8rem] font-semibold mb-3">Branch Roster Management</h3>
              <p className="text-[0.7rem] mb-4">
                To view or manage the duty roster, please select a branch from the dropdown menu above. All congregation announcements, events, programs, and other information to be shared with the congregation are created and managed here.
              </p>
            </div>
          </div>
        ) : (
          <div className="w-full overflow-y-auto">
            <table className="hidden md:table w-full text-gray-600 border-collapse">
              <thead>
                <tr className="border-b border-gray-200">
                  <th className="px-2 py-1.5 text-[0.6rem] font-medium text-gray-500 text-left uppercase tracking-wider whitespace-nowrap">Duty</th>
                  <th className="px-2 py-1.5 text-[0.6rem] font-medium text-gray-500 text-left uppercase tracking-wider whitespace-nowrap">Card No.</th>
                  <th className="px-2 py-1.5 text-[0.6rem] font-medium text-gray-500 text-left uppercase tracking-wider whitespace-nowrap">Name</th>
                  <th className="px-2 py-1.5 text-[0.6rem] font-medium text-gray-500 text-left uppercase tracking-wider whitespace-nowrap">Position</th>
                  <th className="px-2 py-1.5 text-[0.6rem] font-medium text-gray-500 text-left uppercase tracking-wider whitespace-nowrap">Phone</th>
                  <th className="px-2 py-1.5 text-[0.6rem] font-medium text-gray-500 text-left uppercase tracking-wider whitespace-nowrap">Email</th>
                  {isEditing && isDateEditable() && <th className="px-2 py-1.5 text-[0.6rem] font-medium text-gray-500 text-left uppercase tracking-wider whitespace-nowrap">Actions</th>}
                </tr>
              </thead>
              <tbody>
                {duties.map(duty => {
                  const assignments = dutyAssignments[duty] || [];
                  const needsTwoAssignments = requiresTwoAssignments(duty);
                  return (
                    <React.Fragment key={duty}>
                      <tr>
                        <td className="px-2 pt-3 pb-1.5 text-left whitespace-nowrap align-top" rowSpan={needsTwoAssignments ? 2 : 1}>
                          <div className="flex flex-col">
                            <span className="text-[0.6rem] font-medium text-gray-800">{duty.charAt(0).toUpperCase() + duty.slice(1)}</span>
                            <span className="text-[0.5rem] text-gray-600">{dutyTranslations[duty]}</span>
                          </div>
                        </td>
                        <td className="px-2 py-1.5 text-[0.6rem] text-right whitespace-nowrap align-top">
                          {assignments[0] ? (
                            <div className="cursor-pointer p-1 rounded" onClick={() => toggleMemberDetails(assignments[0])}>
                              <span className="text-[0.6rem] text-gray-600">{getMemberCardNumber(assignments[0])}</span>
                            </div>
                          ) : (
                            <span className="text-[0.6rem] text-gray-400">Unassigned</span>
                          )}
                        </td>
                        <td className="px-2 py-1.5 text-[0.6rem] text-left whitespace-nowrap align-top">
                          {assignments[0] ? (
                            <div className="cursor-pointer p-1 rounded" onClick={() => toggleMemberDetails(assignments[0])}>
                              <span className="text-[0.6rem] text-gray-700">{getMemberName(assignments[0])}</span>
                            </div>
                          ) : (
                            <span className="text-[0.6rem] text-gray-400">Unassigned</span>
                          )}
                        </td>
                        <td className="px-2 py-1.5 text-[0.6rem] text-left whitespace-nowrap align-top">
                          {assignments[0] ? (
                            <div className="cursor-pointer p-1 rounded" onClick={() => toggleMemberDetails(assignments[0])}>
                              <span className="text-[0.6rem] text-gray-600">{getMemberPosition(assignments[0]) || 'Member'}</span>
                            </div>
                          ) : (
                            <span className="text-[0.6rem] text-gray-400">Unassigned</span>
                          )}
                        </td>
                        <td className="px-2 py-1.5 text-[0.6rem] text-left whitespace-nowrap align-top">
                          {assignments[0] ? (
                            <div className="cursor-pointer p-1 rounded" onClick={() => toggleMemberDetails(assignments[0])}>
                              <span className="text-[0.6rem] text-gray-600">{getMemberPhone(assignments[0]) || 'No phone'}</span>
                            </div>
                          ) : (
                            <span className="text-[0.6rem] text-gray-400">Unassigned</span>
                          )}
                        </td>
                        <td className="px-2 py-1.5 text-[0.6rem] text-left whitespace-nowrap align-top">
                          {assignments[0] ? (
                            <div className="cursor-pointer p-1 rounded" onClick={() => toggleMemberDetails(assignments[0])}>
                              <span className="text-[0.6rem] text-gray-600 truncate">{getMemberEmail(assignments[0]) || 'No email'}</span>
                            </div>
                          ) : (
                            <span className="text-[0.6rem] text-gray-400">Unassigned</span>
                          )}
                        </td>
                        {isEditing && isDateEditable() && (
                          <td className="px-2 py-1.5 text-left whitespace-nowrap align-top">
                            <div className="flex space-x-1">
                              <button
                                onClick={() => setEditingDuty(`${duty}-0`)}
                                className="p-1.5 bg-green-500 hover:bg-green-600 text-white rounded text-[0.6rem]"
                                title="Assign First Duty"
                              >
                                Assign
                              </button>
                              {assignments[0] && (
                                <button
                                  onClick={() => handleClearDuty(duty, 0)}
                                  className="p-1.5 bg-red-200 hover:bg-red-300 text-red-700 rounded text-[0.6rem]"
                                  title="Clear First Duty"
                                >
                                  <FiX className="h-4 w-4" />
                                </button>
                              )}
                            </div>
                          </td>
                        )}
                      </tr>
                      {needsTwoAssignments && (
                        <tr>
                          <td className="px-2 py-1.5 text-[0.6rem] text-right whitespace-nowrap align-top">
                            {assignments[1] ? (
                              <div className="cursor-pointer p-1 rounded" onClick={() => toggleMemberDetails(assignments[1])}>
                                <span className="text-[0.6rem] text-gray-600">{getMemberCardNumber(assignments[1])}</span>
                              </div>
                            ) : (
                              <span className="text-[0.6rem] text-gray-400">Unassigned</span>
                            )}
                          </td>
                          <td className="px-2 py-1.5 text-[0.6rem] text-left whitespace-nowrap align-top">
                            {assignments[1] ? (
                              <div className="cursor-pointer p-1 rounded" onClick={() => toggleMemberDetails(assignments[1])}>
                                <span className="text-[0.6rem] text-gray-700">{getMemberName(assignments[1])}</span>
                              </div>
                            ) : (
                              <span className="text-[0.6rem] text-gray-400">Unassigned</span>
                            )}
                          </td>
                          <td className="px-2 py-1.5 text-[0.6rem] text-left whitespace-nowrap align-top">
                            {assignments[1] ? (
                              <div className="cursor-pointer p-1 rounded" onClick={() => toggleMemberDetails(assignments[1])}>
                                <span className="text-[0.6rem] text-gray-600">{getMemberPosition(assignments[1]) || 'Member'}</span>
                              </div>
                            ) : (
                              <span className="text-[0.6rem] text-gray-400">Unassigned</span>
                            )}
                          </td>
                          <td className="px-2 py-1.5 text-[0.6rem] text-left whitespace-nowrap align-top">
                            {assignments[1] ? (
                              <div className="cursor-pointer p-1 rounded" onClick={() => toggleMemberDetails(assignments[1])}>
                                <span className="text-[0.6rem] text-gray-600">{getMemberPhone(assignments[1]) || 'No phone'}</span>
                              </div>
                            ) : (
                              <span className="text-[0.6rem] text-gray-400">Unassigned</span>
                            )}
                          </td>
                          <td className="px-2 py-1.5 text-[0.6rem] text-left whitespace-nowrap align-top">
                            {assignments[1] ? (
                              <div className="cursor-pointer p-1 rounded" onClick={() => toggleMemberDetails(assignments[1])}>
                                <span className="text-[0.6rem] text-gray-600 truncate">{getMemberEmail(assignments[1]) || 'No email'}</span>
                              </div>
                            ) : (
                              <span className="text-[0.6rem] text-gray-400">Unassigned</span>
                            )}
                          </td>
                          {isEditing && isDateEditable() && (
                            <td className="px-2 py-1.5 text-left whitespace-nowrap align-top">
                              <div className="flex space-x-1">
                                <button
                                  onClick={() => setEditingDuty(`${duty}-1`)}
                                  className="p-1.5 bg-green-500 hover:bg-green-600 text-white rounded text-[0.6rem]"
                                  title="Assign Second Duty"
                                >
                                  Assign
                                </button>
                                {assignments[1] && (
                                  <button
                                    onClick={() => handleClearDuty(duty, 1)}
                                    className="p-1.5 bg-red-200 hover:bg-red-300 text-red-700 rounded text-[0.6rem]"
                                    title="Clear Second Duty"
                                  >
                                    <FiX className="h-4 w-4" />
                                  </button>
                                )}
                              </div>
                            </td>
                          )}
                        </tr>
                      )}
                    </React.Fragment>
                  );
                })}
              </tbody>
            </table>
            <div className="md:hidden w-full overflow-y-auto">
              <table className="w-full text-gray-600 border-collapse">
                <tbody>
                  {duties.map((duty) => {
                    const assignments = dutyAssignments[duty] || [];
                    const needsTwoAssignments = requiresTwoAssignments(duty);
                    return (
                      <React.Fragment key={duty}>
                        <tr>
                          <td className="px-2 pt-3 pb-1.5 text-left whitespace-nowrap align-top" rowSpan={needsTwoAssignments ? 2 : 1}>
                            <div className="flex flex-col">
                              <span className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider">{duty.charAt(0).toUpperCase() + duty.slice(1)}</span>
                              <span className="text-[0.5rem] text-gray-600">{dutyTranslations[duty]}</span>
                            </div>
                          </td>
                          <td className="px-2 py-1.5 text-[0.6rem] text-left whitespace-nowrap align-top">
                            <div className="flex flex-col">
                              {assignments[0] ? (
                                <div className="cursor-pointer p-1 rounded" onClick={() => toggleMemberDetails(assignments[0])}>
                                  <span className="text-[0.6rem] text-gray-700">{getMemberName(assignments[0])}</span>
                                  <div className="text-[0.5rem] text-gray-500 flex items-center gap-1">
                                    <span>Card: {getMemberCardNumber(assignments[0])}</span>
                                    <span>•</span>
                                    <span>{getMemberPosition(assignments[0]) || 'Member'}</span>
                                  </div>
                                </div>
                              ) : (
                                <span className="text-[0.6rem] text-gray-400">Unassigned</span>
                              )}
                            </div>
                          </td>
                          {isEditing && isDateEditable() && (
                            <td className="px-2 py-1.5 text-left whitespace-nowrap align-top">
                              <div className="flex space-x-1">
                                <button
                                  onClick={() => setEditingDuty(`${duty}-0`)}
                                  className="p-1 bg-green-500 hover:bg-green-600 text-white rounded text-[0.55rem]"
                                  title="Assign First Duty"
                                >
                                  Assign
                                </button>
                                {assignments[0] && (
                                  <button
                                    onClick={() => handleClearDuty(duty, 0)}
                                    className="p-1 bg-red-200 hover:bg-red-300 text-red-700 rounded"
                                    title="Clear First Duty"
                                  >
                                    <FiX className="h-3 w-3" />
                                  </button>
                                )}
                              </div>
                            </td>
                          )}
                          {!isEditing && (
                            <td className="px-2 py-1.5 text-[0.6rem] text-left whitespace-nowrap align-top">
                              {assignments[0] && (
                                <div className="cursor-pointer p-1 rounded" onClick={() => toggleMemberDetails(assignments[0])}>
                                  <div className="flex flex-col">
                                    <span className="text-[0.6rem] text-gray-600 truncate">{getMemberPhone(assignments[0]) || 'No phone'}</span>
                                    <span className="text-[0.5rem] text-gray-600 truncate">{getMemberEmail(assignments[0]) || 'No email'}</span>
                                  </div>
                                </div>
                              )}
                            </td>
                          )}
                        </tr>
                        {needsTwoAssignments && (
                          <tr>
                            <td className="px-2 py-1.5 text-[0.6rem] text-left whitespace-nowrap align-top">
                              <div className="flex flex-col">
                                {assignments[1] ? (
                                  <div className="cursor-pointer p-1 rounded" onClick={() => toggleMemberDetails(assignments[1])}>
                                    <span className="text-[0.6rem] text-gray-700">{getMemberName(assignments[1])}</span>
                                    <div className="text-[0.5rem] text-gray-500 flex items-center gap-1">
                                      <span>Card: {getMemberCardNumber(assignments[1])}</span>
                                      <span>•</span>
                                      <span>{getMemberPosition(assignments[1]) || 'Member'}</span>
                                    </div>
                                  </div>
                                ) : (
                                  <span className="text-[0.6rem] text-gray-400">Unassigned</span>
                                )}
                              </div>
                            </td>
                            {isEditing && isDateEditable() && (
                              <td className="px-2 py-1.5 text-left whitespace-nowrap align-top">
                                <div className="flex space-x-1">
                                  <button
                                    onClick={() => setEditingDuty(`${duty}-1`)}
                                    className="p-1 bg-green-500 hover:bg-green-600 text-white rounded text-[0.55rem]"
                                    title="Assign Second Duty"
                                  >
                                    Assign
                                  </button>
                                  {assignments[1] && (
                                    <button
                                      onClick={() => handleClearDuty(duty, 1)}
                                      className="p-1 bg-red-200 hover:bg-red-300 text-red-700 rounded"
                                      title="Clear Second Duty"
                                    >
                                      <FiX className="h-3 w-3" />
                                    </button>
                                  )}
                                </div>
                              </td>
                            )}
                            {!isEditing && (
                              <td className="px-2 py-1.5 text-[0.6rem] text-left whitespace-nowrap align-top">
                                {assignments[1] && (
                                  <div className="cursor-pointer p-1 rounded" onClick={() => toggleMemberDetails(assignments[1])}>
                                    <div className="flex flex-col">
                                      <span className="text-[0.6rem] text-gray-600 truncate">{getMemberPhone(assignments[1]) || 'No phone'}</span>
                                      <span className="text-[0.5rem] text-gray-600 truncate">{getMemberEmail(assignments[1]) || 'No email'}</span>
                                    </div>
                                  </div>
                                )}
                              </td>
                            )}
                          </tr>
                        )}
                      </React.Fragment>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>
      <AnimatePresence>
        {editingDuty && isDateEditable() && selectedBranch !== 'Select Branch' && (
          <motion.div
            className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-[10000]"
            variants={fadeVariants}
            initial="hidden"
            animate="visible"
            exit="hidden"
            onClick={() => setEditingDuty(null)}
          >
            <motion.div
              className="bg-white p-4 rounded-lg w-80 max-h-96 flex flex-col pointer-events-auto"
              variants={fadeVariants}
              initial="hidden"
              animate="visible"
              exit="hidden"
              onClick={e => e.stopPropagation()}
            >
              <div className="flex justify-between items-center mb-3">
                <h3 className="text-sm font-semibold text-gray-700">
                  Assign {editingDuty.split('-')[0].charAt(0).toUpperCase() + editingDuty.split('-')[0].slice(1)}{' '}
                  {editingDuty.split('-')[1] === '1' ? '(Second)' : ''}
                </h3>
                <motion.button
                  onClick={() => setEditingDuty(null)}
                  className="p-1 bg-gray-200 rounded text-gray-700"
                  variants={buttonVariants}
                  whileHover="hover"
                  whileTap="tap"
                >
                  <FiX className="h-4 w-4" />
                </motion.button>
              </div>
              <div className="flex-1 overflow-y-auto overscroll-contain">
                {getEligibleMembersForDuty(editingDuty.split('-')[0] as DutyKey).length === 0 ? (
                  <div className="text-gray-600 text-[0.6rem] p-2">No eligible members for this duty.</div>
                ) : (
                  getEligibleMembersForDuty(editingDuty.split('-')[0] as DutyKey).map(member => (
                    <motion.div
                      key={member.id}
                      className="p-2 border border-gray-200 rounded cursor-pointer hover:bg-gray-100 mb-2"
                      onClick={() => handleAssignDuty(editingDuty.split('-')[0] as DutyKey, member.id, parseInt(editingDuty.split('-')[1]))}
                      variants={modalItemVariants}
                      whileHover="hover"
                      whileTap="tap"
                    >
                      <div className="font-medium text-gray-600 text-[0.6rem]">
                        {member.name}
                      </div>
                      <div className="text-[0.55rem] text-gray-600">
                        Card: {String(member.cardNumber)} • {member.position !== 'Member' ? member.position : 'Member'} • {member.mainBranch}
                      </div>
                      <div className="text-[0.55rem] text-gray-500 mt-1 flex items-center gap-2">
                        {member.phone && <span>{member.phone}</span>}
                        {member.email && <span> • {member.email}</span>}
                        <button
                          onClick={e => {
                            e.stopPropagation();
                            handleViewMemberHistory();
                          }}
                          disabled={true}
                          className="p-1 bg-gray-200 rounded text-gray-700 opacity-50 cursor-not-allowed"
                          title="View Member History (Disabled)"
                        >
                          <FiInfo className="h-3 w-3" />
                        </button>
                      </div>
                    </motion.div>
                  ))
                )}
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
      <AnimatePresence>
        {Object.entries(showMemberDetails).map(([memberId, isOpen]) => {
          if (!isOpen) return null;
          const member = members.find(m => m.id === memberId);
          if (!member) return null;
          return (
            <motion.div
              key={memberId}
              className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-[10000]"
              variants={fadeVariants}
              initial="hidden"
              animate="visible"
              exit="hidden"
              onClick={() => toggleMemberDetails(memberId)}
            >
              <motion.div
                className="bg-white p-4 rounded-lg w-80 max-h-96 flex flex-col pointer-events-auto"
                variants={fadeVariants}
                initial="hidden"
                animate="visible"
                exit="hidden"
                onClick={e => e.stopPropagation()}
              >
                <div className="flex justify-between items-center mb-3">
                  <h3 className="text-sm font-semibold text-gray-700">Member Details</h3>
                  <motion.button
                    onClick={() => toggleMemberDetails(memberId)}
                    className="p-1 bg-gray-200 rounded text-gray-700"
                    variants={buttonVariants}
                    whileHover="hover"
                    whileTap="tap"
                  >
                    <FiX className="h-4 w-4" />
                  </motion.button>
                </div>
                <div className="flex-1 overflow-y-auto overscroll-contain text-[0.6rem] text-gray-600">
                  <p><strong>Name:</strong> {member.names.join(' ') + ' ' + member.surname}</p>
                  <p><strong>Card Number:</strong> {String(member.cardNumber)}</p>
                  <p><strong>Position:</strong> {member.position || 'Member'}</p>
                  <p><strong>Branch:</strong> {member.mainBranch}</p>
                  <p><strong>Phone:</strong> {member.phone || 'No phone'}</p>
                  <p><strong>Email:</strong> {member.email || 'No email'}</p>
                  <p><strong>Status:</strong> {member.status}</p>
                  <p><strong>Age:</strong> {member.dob ? Math.floor((Date.now() - new Date(member.dob).getTime()) / (1000 * 60 * 60 * 24 * 365.25)) : 'N/A'}</p>
                  <p><strong>Virginity Status:</strong> {member.virginityStatus}</p>
                  <button
                    onClick={() => handleViewMemberHistory()}
                    disabled={true}
                    className="mt-2 p-1 bg-gray-200 rounded text-gray-600 text-[0.6rem] opacity-50 cursor-not-allowed"
                    title="View Member History (Disabled)"
                  >
                    View History
                  </button>
                </div>
              </motion.div>
            </motion.div>
          );
        })}
      </AnimatePresence>
    </div>
  );
};

export default Roster;

