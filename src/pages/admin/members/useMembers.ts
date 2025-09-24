import { useState, useEffect, useMemo } from "react";
import { mockDataTest } from "../../../types/mockDataTest";

export const useMembers = () => {
  const [searchTerm, setSearchTerm] = useState("");
  const [showBranchFilter, setShowBranchFilter] = useState(false);
  const [selectedBranches, setSelectedBranchesState] = useState<string[]>([]);

  // Wrapper to debug setSelectedBranches
  const setSelectedBranches = (branches: string[]) => {
    console.log('useMembers: Setting selectedBranches to:', branches);
    setSelectedBranchesState(branches);
  };

  const currentMembers = useMemo(() => {
    return mockDataTest.members.filter(member => {
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
  }, [searchTerm, selectedBranches]);

  const toggleBranchFilter = (branch: string) => {
    setSelectedBranches(
      selectedBranches.includes(branch)
        ? selectedBranches.filter(b => b !== branch)
        : [...selectedBranches, branch]
    );
  };

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
    currentMembers
  };
};
