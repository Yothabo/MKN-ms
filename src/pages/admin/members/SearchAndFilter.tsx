import { FiSearch, FiFilter, FiPlus } from 'react-icons/fi';
import { motion, AnimatePresence } from 'framer-motion';
import { useRef } from 'react';

interface SearchAndFilterProps {
  searchTerm: string;
  setSearchTerm: (term: string) => void;
  selectedBranches: string[];
  toggleBranchFilter: (branch: string) => void;
  selectedAgeGroups: string[];
  toggleAgeGroupFilter: (ageGroup: string) => void;
  setSelectedBranches: (branches: string[]) => void;
  setSelectedAgeGroups: (ageGroups: string[]) => void;
  showBranchFilter: boolean;
  setShowBranchFilter: (show: boolean) => void;
  totalMembers: number;
  onAddMember: () => void;
  isNewMemberFilterActive: boolean; // New prop for New Members filter
  setIsNewMemberFilterActive: (isActive: boolean) => void; // New prop for setter
}

export const SearchAndFilter = ({
  searchTerm,
  setSearchTerm,
  selectedBranches,
  toggleBranchFilter,
  selectedAgeGroups,
  toggleAgeGroupFilter,
  setSelectedBranches,
  setSelectedAgeGroups,
  showBranchFilter,
  setShowBranchFilter,
  totalMembers,
  onAddMember,
  isNewMemberFilterActive,
  setIsNewMemberFilterActive,
}: SearchAndFilterProps) => {
  const filterRef = useRef<HTMLDivElement>(null);
  const branches = ["Johannesburg", "Bulawayo", "Gaborone", "Harare", "Ireland"];
  const ageGroups = ["Kids (0-12)", "Youth (13-35)", "Adults (36-49)", "50+"];

  const handleClearAllFilters = () => {
    setSelectedBranches([]);
    setSelectedAgeGroups([]);
    setIsNewMemberFilterActive(false); // Clear New Members filter
  };

  const hasFilters = selectedBranches.length > 0 || selectedAgeGroups.length > 0 || isNewMemberFilterActive;

  return (
    <div className="w-full">
      <div className="flex items-center gap-2 w-full">
        <div className="relative flex-1 min-w-[150px]">
          <div className="absolute inset-y-0 left-0 pl-2 flex items-center pointer-events-none">
            <FiSearch className="h-3 w-3 text-gray-400" />
          </div>
          <input
            type="text"
            placeholder="Search..."
            className="block w-full pl-7 pr-2 py-1 text-[0.7rem] border border-gray-300 rounded bg-white placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-green-500 focus:border-green-500"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
        </div>

        <div className="relative" ref={filterRef}>
          <button
            onClick={() => setShowBranchFilter(!showBranchFilter)}
            className={`flex items-center px-2 py-1 border border-gray-300 rounded text-[0.7rem] focus:outline-none ${
              hasFilters
                ? "bg-green-50 text-green-700 border-green-300"
                : "bg-white text-gray-700 hover:bg-gray-50"
            }`}
          >
            <FiFilter className="mr-1 h-2.5 w-2.5" />
            Filter
          </button>

          <AnimatePresence>
            {showBranchFilter && (
              <motion.div
                className="absolute z-50 mt-1 right-0 w-56 bg-white rounded-md shadow-lg border border-gray-200"
                initial={{ opacity: 0, y: -10 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -10 }}
                transition={{ duration: 0.15 }}
              >
                <div className="p-1">
                  <div className="flex justify-between items-center px-1.5 py-1 border-b">
                    <span className="text-[0.6rem] font-medium uppercase tracking-wider text-gray-500">Branches</span>
                    {hasFilters && (
                      <button
                        onClick={handleClearAllFilters}
                        className="text-[0.6rem] text-green-600 hover:text-green-800 focus:outline-none"
                      >
                        Clear All
                      </button>
                    )}
                  </div>
                  <div className="max-h-48 overflow-y-auto">
                    {branches.map(branch => (
                      <label key={branch} className="flex items-center px-1.5 py-1 hover:bg-gray-50">
                        <input
                          type="checkbox"
                          className="h-3 w-3 text-green-600 rounded border-gray-300 focus:ring-green-500"
                          checked={selectedBranches.includes(branch)}
                          onChange={() => toggleBranchFilter(branch)}
                        />
                        <span className="ml-1.5 text-[0.7rem] text-gray-700">{branch}</span>
                      </label>
                    ))}
                  </div>

                  <div className="flex justify-between items-center px-1.5 py-1 border-b border-t mt-1">
                    <span className="text-[0.6rem] font-medium uppercase tracking-wider text-gray-500">Age Groups</span>
                  </div>
                  <div className="max-h-48 overflow-y-auto">
                    {ageGroups.map(ageGroup => (
                      <label key={ageGroup} className="flex items-center px-1.5 py-1 hover:bg-gray-50">
                        <input
                          type="checkbox"
                          className="h-3 w-3 text-green-600 rounded border-gray-300 focus:ring-green-500"
                          checked={selectedAgeGroups.includes(ageGroup)}
                          onChange={() => toggleAgeGroupFilter(ageGroup)}
                        />
                        <span className="ml-1.5 text-[0.7rem] text-gray-700">{ageGroup}</span>
                      </label>
                    ))}
                  </div>

                  <div className="flex justify-between items-center px-1.5 py-1 border-b border-t mt-1">
                    <span className="text-[0.6rem] font-medium uppercase tracking-wider text-gray-500">Membership</span>
                  </div>
                  <div className="max-h-48 overflow-y-auto">
                    <label className="flex items-center px-1.5 py-1 hover:bg-gray-50">
                      <input
                        type="checkbox"
                        className="h-3 w-3 text-green-600 rounded border-gray-300 focus:ring-green-500"
                        checked={isNewMemberFilterActive}
                        onChange={() => setIsNewMemberFilterActive(!isNewMemberFilterActive)}
                      />
                      <span className="ml-1.5 text-[0.7rem] text-gray-700">New Members (≤ 3 months)</span>
                    </label>
                  </div>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>

        <button
          onClick={onAddMember}
          className="flex items-center px-3 py-1 bg-green-600 text-white rounded text-[0.7rem] hover:bg-green-700 whitespace-nowrap"
        >
          <FiPlus className="mr-1 h-2.5 w-2.5" />
          Add Member
        </button>
      </div>

      <div className="mt-1 text-[0.7rem] text-gray-600">
        Total: {totalMembers} member{totalMembers !== 1 ? 's' : ''}
        {hasFilters && (
          <span>
            {selectedBranches.length > 0 && (
              <span> in {selectedBranches.length === 1 ? selectedBranches[0] : `${selectedBranches.length} branches`}</span>
            )}
            {(selectedBranches.length > 0 && (selectedAgeGroups.length > 0 || isNewMemberFilterActive)) && ' and '}
            {selectedAgeGroups.length > 0 && (
              <span> in {selectedAgeGroups.length === 1 ? selectedAgeGroups[0] : `${selectedAgeGroups.length} age groups`}</span>
            )}
            {(selectedAgeGroups.length > 0 && isNewMemberFilterActive) && ' and '}
            {isNewMemberFilterActive && (
              <span> new members (≤ 3 months)</span>
            )}
          </span>
        )}
      </div>
    </div>
  );
};
