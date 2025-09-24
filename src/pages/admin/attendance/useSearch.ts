import { useState } from 'react';

interface Member {
  id: string;
  names: string[];
  surname: string;
  cardNumber: string;
}

export const useSearch = (members: Member[]) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [guestSearchTerm, setGuestSearchTerm] = useState('');
  const [guestSearchResults, setGuestSearchResults] = useState<Member[]>([]);

  const normalizeSearchTerm = (term: string): string => {
    return term.toLowerCase().replace(/[\s-]/g, '');
  };

  const handleSearchGuest = (searchTerm: string) => {
    if (!searchTerm.trim()) {
      setGuestSearchResults([]);
      return;
    }

    const searchNormalized = normalizeSearchTerm(searchTerm);

    const filtered = members.filter(member => {
      const fullName = `${member.names.join(' ')} ${member.surname}`.toLowerCase();
      const cardNumber = normalizeSearchTerm(member.cardNumber);

      return (
        fullName.includes(searchNormalized) ||
        cardNumber.includes(searchNormalized)
      );
    });

    setGuestSearchResults(filtered);
  };

  return {
    searchTerm,
    setSearchTerm,
    guestSearchTerm,
    setGuestSearchTerm,
    guestSearchResults,
    handleSearchGuest
  };
};
