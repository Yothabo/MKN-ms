import { useState, useEffect, useMemo } from "react";
import { mockDataTest } from "../../../types/mockDataTest";

export const useAttendance = () => {
  const [searchTerm, setSearchTerm] = useState("");
  const [showBranchFilter, setShowBranchFilter] = useState(false);
  const [selectedBranches, setSelectedBranchesState] = useState<string[]>([]);
  const [membersState, setMembersState] = useState(mockDataTest.members);

  // Get all members (unfiltered)
  const allMembers = useMemo(() => membersState, [membersState]);

  // Wrapper to debug setSelectedBranches
  const setSelectedBranches = (branches: string[]) => {
    console.log('useAttendance: Setting selectedBranches to:', branches);
    setSelectedBranchesState(branches);
  };

  const toggleBranchFilter = (branch: string) => {
    console.log('Toggling branch:', branch);
    console.log('Current selectedBranches:', selectedBranches);
    
    setSelectedBranches(
      selectedBranches.includes(branch)
        ? selectedBranches.filter(b => b !== branch)
        : [...selectedBranches, branch]
    );
  };

  const currentMembers = useMemo(() => {
    return allMembers.filter(member => {
      const matchesSearch =
        member.cardNumber.toString().toLowerCase().includes(searchTerm.toLowerCase()) ||
        member.names.join(" ").toLowerCase().includes(searchTerm.toLowerCase()) ||
        member.surname.toLowerCase().includes(searchTerm.toLowerCase()) ||
        member.position.toLowerCase().includes(searchTerm.toLowerCase()) ||
        member.phone.toLowerCase().includes(searchTerm.toLowerCase()) ||
        member.email.toLowerCase().includes(searchTerm.toLowerCase());

      const matchesBranch = selectedBranches.length === 0 ||
        selectedBranches.includes(member.mainBranch);

      return matchesSearch && matchesBranch;
    });
  }, [searchTerm, selectedBranches, allMembers]);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      const filterElement = document.getElementById('branch-filter');
      if (filterElement && !filterElement.contains(event.target as Node)) {
        setShowBranchFilter(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  return {
    searchTerm,
    setSearchTerm,
    showBranchFilter,
    setShowBranchFilter,
    selectedBranches,
    setSelectedBranches,
    toggleBranchFilter,
    currentMembers,
    allMembers,
    setMembers: setMembersState
  };
};
