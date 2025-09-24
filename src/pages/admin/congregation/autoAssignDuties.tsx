// Constants
const fairnessWindow = 90; // Extended to 90 days for better historical tracking
const dutyPoints: { [key: string]: number } = {
  chair: 3,
  reader: 2,
  facilitator: 2,
  messenger: 1,
  evangelist: 1,
  announcements: 1,
  'word reader': 2,
};

// Interfaces
interface Member {
  id: string;
  name: string;
  mainBranch: string;
  position: string;
  gender?: string;
  age?: number;
  virginityStatus?: boolean;
}

// Define DutyKey to match Roster.tsx
type DutyKey = 'chair' | 'reader' | 'word reader' | 'messenger' | 'facilitator' | 'evangelist' | 'announcements';

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

interface DutyAssignment {
  [duty: string]: string[];
}

export const autoAssignDuties = (
  members: Member[],
  duties: DutyKey[],
  selectedDay: string,
  selectedBranch: string,
  selectedDate: Date
): DutyAssignment => {
  console.log('autoAssignDuties called:', {
    selectedDay,
    selectedBranch,
    selectedDate: selectedDate.toISOString(),
    memberCount: members.length,
  });

  // Load and initialize duty history
  const historyKey = `memberDutyHistory_${selectedBranch}`;
  let memberDutyHistory: { [memberId: string]: DutyHistory } = {};

  try {
    const savedHistory = localStorage.getItem(historyKey);
    if (savedHistory) {
      memberDutyHistory = JSON.parse(savedHistory);
    }
  } catch (e) {
    console.error(`Error parsing memberDutyHistory for ${selectedBranch}:`, e);
    localStorage.removeItem(historyKey);
  }

  // Initialize history for new members
  members.forEach(member => {
    if (!memberDutyHistory[member.id]) {
      memberDutyHistory[member.id] = {
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
  });

  // Filter members by branch
  const filteredMembers = members.filter(member => {
    const matchesBranch = selectedBranch === 'All Branches' || member.mainBranch === selectedBranch;
    return matchesBranch;
  });

  if (filteredMembers.length === 0) {
    console.warn(`No members available for branch: ${selectedBranch}`);
    const emptyAssignments: DutyAssignment = {};
    duties.forEach(duty => {
      emptyAssignments[duty] = [];
    });
    return emptyAssignments;
  }

  // Check for special duty members (Messengers and Evangelists)
  const messengers = filteredMembers.filter(m =>
    m.position.toLowerCase() === 'messenger' || m.position.toLowerCase() === 'holy messenger'
  );

  const evangelists = filteredMembers.filter(m =>
    m.position.toLowerCase() === 'evangelist'
  );

  console.log('Special duty members:', { messengers: messengers.length, evangelists: evangelists.length });

  // Get members who recently performed each duty (extended to 90 days)
  const recentDutyMembers: { [duty: string]: string[] } = {};
  if (filteredMembers.length >= 10) { // Skip for small branches
    duties.forEach(duty => {
      recentDutyMembers[duty] = [];
      for (let i = 1; i <= fairnessWindow; i++) {
        const checkDate = new Date(selectedDate);
        checkDate.setDate(selectedDate.getDate() - i);
        const dateKey = checkDate.toISOString().split('T')[0];
        const storageKey = selectedBranch === 'All Branches'
          ? `dutyAssignments_${dateKey}`
          : `dutyAssignments_${dateKey}_${selectedBranch}`;

        try {
          const savedAssignments = localStorage.getItem(storageKey);
          if (savedAssignments) {
            const assignments: DutyAssignment = JSON.parse(savedAssignments);
            if (assignments[duty]) {
              recentDutyMembers[duty].push(...assignments[duty]);
            }
          }
        } catch (e) {
          console.error(`Error parsing assignments for ${storageKey}:`, e);
        }
      }
    });
  }

  const newAssignments: DutyAssignment = {};
  const availableMembers = [...filteredMembers];
  const assignedMemberIds: { [memberId: string]: number } = {};
  const chairAssignedIds: string[] = [];

  const getWeightedMember = (eligibleMembers: Member[]): Member | null => {
    if (eligibleMembers.length === 0) return null;

    const maxPoints = Math.max(...eligibleMembers.map(m => memberDutyHistory[m.id]?.totalPoints || 0), 1);
    const weights = eligibleMembers.map(member => {
      const points = memberDutyHistory[member.id]?.totalPoints || 0;
      return maxPoints - points + 1;
    });
    const totalWeight = weights.reduce((sum, w) => sum + w, 0);
    const random = Math.random() * totalWeight;
    let sum = 0;

    for (let i = 0; i < eligibleMembers.length; i++) {
      sum += weights[i];
      if (random <= sum) {
        return eligibleMembers[i];
      }
    }
    return eligibleMembers[eligibleMembers.length - 1];
  };

  const getEligibleMembers = (duty: DutyKey, level: number): Member[] => {
    return availableMembers.filter(member => {
      if (chairAssignedIds.includes(member.id)) return false;

      const positionLower = member.position.toLowerCase();

      // Special handling for messengers and evangelists
      if (duty === 'messenger' || duty === 'evangelist' || duty === 'announcements') {
        return positionLower === duty; // Strict: only exact position
      }

      // Skip recent duty check for small branches
      if (filteredMembers.length >= 10 && level === 1 && recentDutyMembers[duty].includes(member.id)) {
        return false;
      }

      if (level === 1) {
        if (duty === 'chair') {
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
          return positionLower === 'facilitator' && member.gender === 'female';
        }
      }

      if (level === 2) {
        if (duty === 'chair') {
          return positionLower !== 'messenger' && positionLower !== 'holy messenger';
        } else if (duty === 'reader' || duty === 'word reader') {
          return positionLower !== 'messenger' && positionLower !== 'holy messenger';
        } else if (duty === 'facilitator') {
          return positionLower === 'facilitator'; // Male facilitators
        }
      }

      if (level === 3) {
        if (duty === 'chair') {
          return positionLower !== 'messenger' && positionLower !== 'holy messenger';
        } else if (duty === 'facilitator') {
          return true; // Any member
        }
        return true;
      }

      return false;
    });
  };

  const assignDuty = (duty: DutyKey, level: number): boolean => {
    const eligibleMembers = getEligibleMembers(duty, level);

    if (duty === 'facilitator' || duty === 'messenger' || duty === 'evangelist' || duty === 'announcements') {
      const assigned: string[] = [];
      if (eligibleMembers.length > 0) {
        const member = getWeightedMember(eligibleMembers);
        if (member) {
          assigned.push(member.id);
          assignedMemberIds[member.id] = (assignedMemberIds[member.id] || 0) + 1;
          memberDutyHistory[member.id][duty]!.count += 1;
          memberDutyHistory[member.id][duty]!.points += dutyPoints[duty];
          memberDutyHistory[member.id].totalPoints += dutyPoints[duty];

          const memberIndex = availableMembers.findIndex(m => m.id === member.id);
          if (memberIndex !== -1) {
            availableMembers.splice(memberIndex, 1);
          }
        }
      }
      newAssignments[duty] = assigned;
      return assigned.length > 0;
    } else {
      if (eligibleMembers.length > 0) {
        const member = getWeightedMember(eligibleMembers);
        if (member) {
          newAssignments[duty] = [member.id];
          assignedMemberIds[member.id] = (assignedMemberIds[member.id] || 0) + 1;
          memberDutyHistory[member.id][duty]!.count += 1;
          memberDutyHistory[member.id][duty]!.points += dutyPoints[duty];
          memberDutyHistory[member.id].totalPoints += dutyPoints[duty];

          if (duty === 'chair') {
            chairAssignedIds.push(member.id);
          }

          const memberIndex = availableMembers.findIndex(m => m.id === member.id);
          if (memberIndex !== -1) {
            availableMembers.splice(memberIndex, 1);
          }
          return true;
        }
      }
      newAssignments[duty] = [];
      return false;
    }
  };

  // Ensure chair is always assigned first with highest priority
  if (duties.includes('chair')) {
    let chairAssigned = false;
    for (let level = 1; level <= 3 && !chairAssigned; level++) {
      chairAssigned = assignDuty('chair', level);
    }

    // Ultimate fallback for chair
    if (!chairAssigned) {
      const fallbackMember = availableMembers.find(() => true); // Any available member
      if (fallbackMember) {
        newAssignments['chair'] = [fallbackMember.id];
        assignedMemberIds[fallbackMember.id] = (assignedMemberIds[fallbackMember.id] || 0) + 1;
        memberDutyHistory[fallbackMember.id]['chair']!.count += 1;
        memberDutyHistory[fallbackMember.id]['chair']!.points += dutyPoints['chair'];
        memberDutyHistory[fallbackMember.id].totalPoints += dutyPoints['chair'];
        chairAssignedIds.push(fallbackMember.id);
        const memberIndex = availableMembers.findIndex(m => m.id === fallbackMember.id);
        if (memberIndex !== -1) {
          availableMembers.splice(memberIndex, 1);
        }
        console.log(`Used ultimate fallback for chair: ${fallbackMember.id}`);
      } else {
        console.warn('No members available for chair even after all fallbacks');
        newAssignments['chair'] = [];
      }
    }
  }

  // Handle special duties (messenger and evangelist)
  if (duties.includes('messenger')) {
    let messengerAssigned = false;
    for (let level = 1; level <= 1 && !messengerAssigned; level++) { // Only level 1 for strict position
      messengerAssigned = assignDuty('messenger', level);
    }
    if (!messengerAssigned) {
      newAssignments['messenger'] = [];
      console.log('No messengers available, leaving messenger duty empty');
    }
  }

  if (duties.includes('evangelist')) {
    let evangelistAssigned = false;
    for (let level = 1; level <= 1 && !evangelistAssigned; level++) { // Only level 1 for strict position
      evangelistAssigned = assignDuty('evangelist', level);
    }
    if (!evangelistAssigned) {
      newAssignments['evangelist'] = [];
      console.log('No evangelists available, leaving evangelist duty empty');
    }
  }

  // Assign other duties
  duties.forEach(duty => {
    if (duty === 'chair' || duty === 'messenger' || duty === 'evangelist') return; // Already assigned

    let assigned = false;
    for (let level = 1; level <= 3 && !assigned; level++) {
      assigned = assignDuty(duty, level);
      if (!assigned && level < 3) {
        console.log(`Level ${level} failed for ${duty}, trying level ${level + 1}`);
      }
    }

    // Final fallback for facilitator and other duties
    if (!assigned && newAssignments[duty]?.length === 0) {
      const fallbackMember = availableMembers.find(() => true); // Any available member
      if (fallbackMember) {
        newAssignments[duty] = [fallbackMember.id];
        assignedMemberIds[fallbackMember.id] = (assignedMemberIds[fallbackMember.id] || 0) + 1;
        memberDutyHistory[fallbackMember.id][duty]!.count += 1;
        memberDutyHistory[fallbackMember.id][duty]!.points += dutyPoints[duty];
        memberDutyHistory[fallbackMember.id].totalPoints += dutyPoints[duty];

        const memberIndex = availableMembers.findIndex(m => m.id === fallbackMember.id);
        if (memberIndex !== -1) {
          availableMembers.splice(memberIndex, 1);
        }
        console.log(`Used ultimate fallback for ${duty}: ${fallbackMember.id}`);
      } else {
        console.warn(`No members available for ${duty} even after all fallbacks`);
      }
    }
  });

  try {
    localStorage.setItem(historyKey, JSON.stringify(memberDutyHistory));
  } catch (e) {
    console.error(`Error saving memberDutyHistory for ${selectedBranch}:`, e);
  }
  console.log('Generated assignments:', newAssignments);
  return newAssignments;
};

export const populatePastWeek = (members: Member[], duties: DutyKey[], selectedBranch: string) => {
  const today = new Date();
  for (let i = 1; i <= 90; i++) {
    const pastDate = new Date(today);
    pastDate.setDate(today.getDate() - i);
    const dateKey = pastDate.toISOString().split('T')[0];
    const storageKey = selectedBranch === 'All Branches'
      ? `dutyAssignments_${dateKey}`
      : `dutyAssignments_${dateKey}_${selectedBranch}`;
    if (!localStorage.getItem(storageKey)) {
      const dayName = ['SUNDAY', 'MONDAY', 'TUESDAY', 'WEDNESDAY', 'THURSDAY', 'FRIDAY', 'SATURDAY'][pastDate.getDay()];
      const assignments = autoAssignDuties(members, duties, dayName, selectedBranch, pastDate);
      try {
        localStorage.setItem(storageKey, JSON.stringify(assignments));
      } catch (e) {
        console.error(`Error saving assignments for ${storageKey}:`, e);
      }
    }
  }
};
