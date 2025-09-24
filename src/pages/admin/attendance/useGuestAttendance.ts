import { useState } from 'react';

interface Member {
  id: string;
  names: string[];
  surname: string;
  mainBranch: string;
  status: string;
}

interface Notification {
  message: string;
  type: 'error' | 'info';
}

export const useGuestAttendance = (
  members: Member[],
  presentMembers: string[],
  setMembers: React.Dispatch<React.SetStateAction<Member[]>>,
  setPresentMembers: React.Dispatch<React.SetStateAction<string[]>>
) => {
  const [notification, setNotification] = useState<Notification | null>(null);

  const handleAddGuestToBranch = (memberId: string, currentBranch: string) => {
    const member = members.find(m => m.id === memberId);
    if (!member) return;

    if (member.mainBranch === currentBranch) {
      setNotification({
        message: `Cannot add ${member.names.join(' ')} ${member.surname} as guest to their own branch.`,
        type: 'error'
      });
      setTimeout(() => setNotification(null), 3000);
      return;
    }

    if (presentMembers.includes(member.id)) {
      const presentBranch = members.find(m => m.id === memberId)?.mainBranch;
      setNotification({
        message: `${member.names.join(' ')} ${member.surname} has already attended at ${presentBranch} branch.`,
        type: 'info'
      });
      setTimeout(() => setNotification(null), 3000);
      return;
    }

    // Add to present members first
    setPresentMembers(prev => [...prev, memberId]);
    
    // Then update member status if needed
    if (member.status.toLowerCase() === 'pre-ra') {
      setMembers(prevMembers =>
        prevMembers.map(m =>
          m.id === memberId
            ? {
                ...m,
                status: 'Active',
                raCount: 0,
                deceased: false,
                dateOfDeath: null
              }
            : m
        )
      );
    }

    setNotification({
      message: `${member.names.join(' ')} ${member.surname} added as guest from ${member.mainBranch}`,
      type: 'info'
    });
    setTimeout(() => setNotification(null), 3000);
  };

  return { notification, setNotification, handleAddGuestToBranch };
};
