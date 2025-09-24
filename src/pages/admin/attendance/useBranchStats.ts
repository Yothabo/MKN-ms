import { BRANCHES } from './constants';

interface Member {
  id: string;
  mainBranch: string;
}

interface BranchStats {
  total: number;
  present: number;
}

export const useBranchStats = (members: Member[], presentMembers: string[]) => {
  const calculateBranchStats = (): Record<string, BranchStats> => {
    const branchStats: Record<string, BranchStats> = {};

    BRANCHES.forEach(branch => {
      branchStats[branch] = { total: 0, present: 0 };
    });

    members.forEach(member => {
      if (branchStats[member.mainBranch]) {
        branchStats[member.mainBranch].total++;
        if (presentMembers.includes(member.id)) {
          branchStats[member.mainBranch].present++;
        }
      }
    });

    return branchStats;
  };

  const formatBranchStats = (branchStats: Record<string, BranchStats>): string => {
    return Object.entries(branchStats)
      .filter(([_, stats]) => stats.total > 0)
      .map(([branch, stats]) => {
        const percentage = stats.total > 0 ? Math.round((stats.present / stats.total) * 100) : 0;
        return `${branch}: ${percentage}%`;
      })
      .join(' | ');
  };

  return { calculateBranchStats, formatBranchStats };
};
