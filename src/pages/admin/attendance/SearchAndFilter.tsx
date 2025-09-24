import { useEffect, useRef, useState } from 'react';
import { FiSave, FiSearch, FiEdit, FiUserPlus, FiX, FiUsers, FiAlertCircle } from 'react-icons/fi';
import { motion, AnimatePresence } from 'framer-motion';
import { BRANCHES } from './constants';
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

interface SearchAndFilterProps {
  searchTerm: string;
  setSearchTerm: (term: string) => void;
  selectedBranches: string[];
  toggleBranchFilter: (branch: string) => void; // Not used - can be removed from props if not needed
  setSelectedBranches: (branches: string[]) => void;
  selectedStatus: string[]; // Not used - can be removed from props if not needed
  toggleStatusFilter: (status: string) => void; // Not used - can be removed from props if not needed
  setSelectedStatus: (status: string[]) => void;
  showBranchFilter: boolean;
  setShowBranchFilter: (show: boolean) => void;
  totalMembers: number;
  isBranchFilterMode: boolean;
  branchStats: string;
  presentMembers: string[];
  setPresentMembers: React.Dispatch<React.SetStateAction<string[]>>;
  currentBranch: string;
  onAddGuestToBranch: (memberId: string, currentBranch: string) => void;
  onRemoveGuestFromBranch: (memberId: string) => void;
  guestMembers: GuestMember[];
  allMembers: Member[];
}

export const SearchAndFilter = ({
  searchTerm,
  setSearchTerm,
  selectedBranches,
  setSelectedBranches,
  setSelectedStatus,
  showBranchFilter,
  setShowBranchFilter,
  totalMembers,
  isBranchFilterMode,
  branchStats,
  presentMembers,
  setPresentMembers,
  currentBranch,
  onAddGuestToBranch,
  onRemoveGuestFromBranch,
  guestMembers,
  allMembers,
}: SearchAndFilterProps) => {
  const filterRef = useRef<HTMLDivElement>(null);
  const guestSearchRef = useRef<HTMLDivElement>(null);
  const [showGuestSearch, setShowGuestSearch] = useState(false);
  const [guestSearchTerm, setGuestSearchTerm] = useState('');
  const [addError, setAddError] = useState<string | null>(null);
  const [showGuestTable, setShowGuestTable] = useState(guestMembers.length > 0);
  const [_selectedBranch, _setSelectedBranch] = useState<string>('');

  const normalizeSearchTerm = (term: string): string => {
    return term.toLowerCase().replace(/[\s-]/g, '');
  };

  const guestEligibleMembers = allMembers.filter(member =>
    member.mainBranch !== currentBranch
  );

  const filteredGuestMembers = guestSearchTerm
    ? guestEligibleMembers.filter(member => {
        const searchNormalized = normalizeSearchTerm(guestSearchTerm);
        const fullName = normalizeSearchTerm(`${member.names.join(' ')} ${member.surname}`);
        const cardNumber = normalizeSearchTerm(member.cardNumber);
        const mainBranch = normalizeSearchTerm(member.mainBranch);
        return (
          fullName.includes(searchNormalized) ||
          cardNumber.includes(searchNormalized) ||
          mainBranch.includes(searchNormalized)
        );
      })
    : guestEligibleMembers.slice(0, 20);

  const handleClearAllFilters = () => {
    setSelectedBranches([]);
    setSelectedStatus([]);
    setPresentMembers([]);
  };

  const handleSaveRegister = () => {
    setSelectedBranches([]);
    setSelectedStatus([]);
    setShowBranchFilter(false);
  };

  const handleBranchSelection = (branch: string) => {
    if (selectedBranches.includes(branch)) {
      setSelectedBranches([]);
    } else {
      setSelectedBranches([branch]);
    }
    setShowBranchFilter(false);
  };

  const toggleFilterDropdown = (e: React.MouseEvent) => {
    e.stopPropagation();
    setShowBranchFilter(!showBranchFilter);
  };

  const handleAddGuestClick = () => {
    setShowGuestSearch(true);
    setGuestSearchTerm('');
    setAddError(null);
  };

  const handleToggleGuest = (memberId: string, event?: React.MouseEvent) => {
    if (event) {
      event.stopPropagation();
    }
    const member = allMembers.find(m => m.id === memberId);
    if (!member) return;

    if (member.mainBranch === currentBranch) {
      setAddError(`Cannot add ${member.names.join(' ')} ${member.surname} as guest to their own branch.`);
      setTimeout(() => setAddError(null), 5000);
      return;
    }

    const isCurrentlyPresent = presentMembers.includes(memberId);

    if (isCurrentlyPresent) {
      onRemoveGuestFromBranch(memberId);
    } else {
      onAddGuestToBranch(memberId, currentBranch);
      if (!showGuestTable) {
        setShowGuestTable(true);
      }
    }
  };

  const handleRemoveGuest = (memberId: string) => {
    onRemoveGuestFromBranch(memberId);
    if (guestMembers.length === 1) {
      setShowGuestTable(false);
    }
  };

  const getStatusStyles = (isPresent: boolean) => {
    return isPresent ? 'bg-green-100' : '';
  };

  const hasFilters = selectedBranches.length > 0;

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (filterRef.current && !filterRef.current.contains(event.target as Node)) {
        setShowBranchFilter(false);
      }
      if (guestSearchRef.current && !guestSearchRef.current.contains(event.target as Node)) {
        setShowGuestSearch(false);
        setAddError(null);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const shouldShowGuestTable = isBranchFilterMode && showGuestTable && guestMembers.length > 0;

  return (
    <div className="w-full max-w-3xl mx-auto">
      {shouldShowGuestTable && (
        <div className="mb-4 p-3 bg-yellow-50 border border-yellow-200 rounded-md">
          <div className="flex items-center justify-between mb-2">
            <h3 className="text-xs font-semibold text-yellow-800 flex items-center">
              <FiUsers className="mr-1" /> Guest Members at {currentBranch} ({guestMembers.length})
            </h3>
            <button
              onClick={() => setShowGuestTable(false)}
              className="text-yellow-600 hover:text-yellow-800 text-xs"
            >
              <FiX size={14} />
            </button>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-xs">
              <thead>
                <tr className="bg-yellow-100">
                  <th className="px-2 py-1 text-left">Name</th>
                  <th className="px-2 py-1 text-left">Home Branch</th>
                  <th className="px-2 py-1 text-left">Action</th>
                </tr>
              </thead>
              <tbody>
                {guestMembers.filter(guest => guest.visitedBranch === currentBranch).map(guest => (
                  <tr key={guest.id} className="border-b border-yellow-200">
                    <td className="px-2 py-1">{guest.names.join(' ')} {guest.surname}</td>
                    <td className="px-2 py-1">{guest.mainBranch}</td>
                    <td className="px-2 py-1">
                      <button
                        onClick={() => handleRemoveGuest(guest.id)}
                        className="text-red-600 hover:text-red-800 text-xs"
                      >
                        Remove
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <div className="flex items-center gap-2">
        <div className="relative flex-1">
          <FiSearch className="absolute left-2 top-1/2 transform -translate-y-1/2 h-3 w-3 text-gray-400" />
          <input
            type="text"
            placeholder="Search members..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full pl-8 pr-2 py-1.5 text-[0.7rem] border border-gray-300 rounded-md bg-white placeholder-gray-400 focus:ring-1 focus:ring-green-500 focus:border-green-500"
          />
        </div>

        <div className="relative" ref={filterRef}>
          <button
            onClick={toggleFilterDropdown}
            className={`flex items-center px-3 py-1.5 border rounded-md text-[0.7rem] transition-colors ${
              hasFilters ? 'bg-green-50 border-green-300 text-green-700' : 'bg-white border-gray-300 text-gray-700 hover:bg-gray-50'
            }`}
          >
            <FiEdit className="mr-1 h-3 w-3" />
            {hasFilters ? 'Change Register' : 'Mark Register'}
          </button>
          <AnimatePresence>
            {showBranchFilter && (
              <motion.div
                initial={{ opacity: 0, y: -10 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -10 }}
                className="absolute z-50 mt-2 right-0 w-48 bg-white rounded-md shadow-lg border border-gray-200"
              >
                <div className="p-2">
                  <div className="px-2 py-1 border-b border-gray-200 flex justify-between items-center">
                    <span className="text-[0.6rem] font-medium uppercase text-gray-500">Branches</span>
                    {hasFilters && (
                      <button
                        onClick={handleClearAllFilters}
                        className="text-[0.6rem] text-green-600 hover:text-green-800"
                      >
                        Clear All
                      </button>
                    )}
                  </div>
                  {BRANCHES.map((branch) => (
                    <label key={branch} className="flex items-center px-2 py-1.5 hover:bg-gray-50">
                      <input
                        type="radio"
                        name="branch-selection"
                        checked={selectedBranches.includes(branch)}
                        onChange={() => handleBranchSelection(branch)}
                        className="h-3 w-3 text-green-600 rounded-full border-gray-300 focus:ring-green-500"
                      />
                      <span className="ml-2 text-[0.7rem] text-gray-700">{branch}</span>
                    </label>
                  ))}
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>

        {isBranchFilterMode && (
          <div className="flex items-center gap-2">
            <button
              onClick={handleSaveRegister}
              className="flex items-center px-3 py-1.5 bg-green-600 text-white rounded-md text-[0.7rem] hover:bg-green-700"
            >
              <FiSave className="mr-1 h-3 w-3" />
              Save & Exit
            </button>

            <div className="relative" ref={guestSearchRef}>
              <button
                onClick={handleAddGuestClick}
                className="flex items-center px-3 py-1.5 bg-orange-500 text-white rounded-md text-[0.7rem] hover:bg-orange-600 transition-colors"
                disabled={!currentBranch}
              >
                <FiUserPlus className="mr-1 h-3 w-3" />
                Add Guest
              </button>

              <AnimatePresence>
                {showGuestSearch && (
                  <motion.div
                    initial={{ opacity: 0, y: -10 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: -10 }}
                    className="absolute z-50 mt-2 right-0 w-80 bg-white rounded-md shadow-lg border border-gray-200 max-h-96 overflow-hidden"
                  >
                    <div className="p-3">
                      <div className="flex items-center justify-between mb-3">
                        <h3 className="text-[0.7rem] font-medium text-gray-700">Add Guests from Other Branches</h3>
                        <button
                          onClick={() => {
                            setShowGuestSearch(false);
                            setAddError(null);
                          }}
                          className="text-gray-400 hover:text-gray-500"
                        >
                          <FiX className="h-3 w-3" />
                        </button>
                      </div>

                      {addError && (
                        <div className="mb-3 p-2 bg-red-50 border border-red-200 rounded-md flex items-start">
                          <FiAlertCircle className="text-red-500 mr-2 mt-0.5 flex-shrink-0" />
                          <span className="text-[0.6rem] text-red-700">{addError}</span>
                        </div>
                      )}
                      <div className="relative mb-3">
                        <FiSearch className="absolute left-2 top-1/2 transform -translate-y-1/2 h-3 w-3 text-gray-400" />
                        <input
                          type="text"
                          placeholder="Search guests..."
                          value={guestSearchTerm}
                          onChange={(e) => setGuestSearchTerm(e.target.value)}
                          className="w-full pl-8 pr-2 py-1.5 text-[0.7rem] border border-gray-300 rounded-md bg-white placeholder-gray-400 focus:ring-1 focus:ring-orange-500 focus:border-orange-500"
                          autoFocus
                        />
                      </div>
                      <div className="overflow-y-auto max-h-60">
                        {filteredGuestMembers.length === 0 ? (
                          <p className="text-[0.6rem] text-gray-500 text-center py-2">
                            {guestSearchTerm
                              ? `No guests found matching "${guestSearchTerm}"`
                              : 'No members from other branches available'
                            }
                          </p>
                        ) : (
                          <table className="min-w-full table-fixed bg-transparent">
                            <thead className="bg-white sticky top-0 z-10">
                              <tr>
                                <th className="px-2 py-1.5 text-left text-[0.55rem] font-semibold text-gray-500 uppercase tracking-wider w-[30%]">
                                  Name
                                </th>
                                <th className="px-2 py-1.5 text-left text-[0.55rem] font-semibold text-gray-500 uppercase tracking-wider w-[30%]">
                                  Branch
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
                              {filteredGuestMembers.map(member => {
                                const isPresent = presentMembers.includes(member.id);
                                const isRAMember = member.status === 'RA';

                                return (
                                  <tr
                                    key={member.id}
                                    className={getStatusStyles(isPresent)}
                                  >
                                    <td className="px-2 py-1.5 whitespace-nowrap">
                                      <div className="text-[0.6rem] font-semibold text-gray-700 text-left">
                                        {member.names.join(' ')} {member.surname}
                                      </div>
                                      <div className="text-[0.5rem] text-gray-500 text-left">Card: {member.cardNumber}</div>
                                    </td>
                                    <td className="px-2 py-1.5 whitespace-nowrap">
                                      <div className="text-[0.6rem] text-gray-700 text-left">{member.mainBranch}</div>
                                    </td>
                                    <td className="px-2 py-1.5 whitespace-nowrap">
                                      <div
                                        className={`text-[0.5rem] font-medium text-left ${
                                          member.status.toLowerCase() === 'ra'
                                            ? 'text-red-600'
                                          : member.status.toLowerCase() === 'pre-ra'
                                            ? 'text-orange-500'
                                          : member.status.toLowerCase() === 'inactive'
                                            ? 'text-gray-500'
                                            : 'text-green-600'
                                        }`}
                                      >
                                        {member.status}
                                      </div>
                                    </td>
                                    <td className="px-2 py-1.5 whitespace-nowrap text-left">
                                      <AttendanceToggle
                                        isPresent={isPresent}
                                        onToggle={e => handleToggleGuest(member.id, e)}
                                        disabled={isRAMember}
                                      />
                                    </td>
                                  </tr>
                                );
                              })}
                            </tbody>
                          </table>
                        )}
                      </div>
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>
            </div>
          </div>
        )}
      </div>

      <div className="mt-2 text-[0.7rem] text-gray-600">
        <p>Total: {totalMembers} member{totalMembers !== 1 ? 's' : ''}</p>
        {branchStats && <p className="mt-1 font-medium">{branchStats}</p>}
        {hasFilters && (
          <p className="mt-1">
            {selectedBranches.length > 0 && (
              <span>
                in {selectedBranches.length === 1 ? selectedBranches[0] : `${selectedBranches.length} branches`}
              </span>
            )}
          </p>
        )}
      </div>
    </div>
  );
};

export default SearchAndFilter;
