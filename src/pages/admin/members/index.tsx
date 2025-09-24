// src/pages/admin/members/index.tsx
import { useState, useEffect } from 'react';
import { useMembers } from './useMembers';
import { SearchAndFilter } from './SearchAndFilter';
import { MembersTable } from './MembersTable';
import { MemberProfileDialog, type Member } from './MemberProfileDialog';
import MKNLogo from '../../../assets/MKN.png';

const MembersPage = () => {
  const membersData = useMembers();
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [currentMember, setCurrentMember] = useState<Member | null>(null);
  const [showBranchFilter, setShowBranchFilter] = useState(false);
  const [selectedAgeGroups, setSelectedAgeGroups] = useState<string[]>([]);
  const [isNewMemberFilterActive, setIsNewMemberFilterActive] = useState(false);

  useEffect(() => {
    console.log('selectedBranches:', membersData.selectedBranches);
    console.log('selectedAgeGroups:', selectedAgeGroups);
    console.log('isNewMemberFilterActive:', isNewMemberFilterActive);
    console.log('filteredMembers length:', filteredMembers.length);
  }, [membersData.selectedBranches, selectedAgeGroups, isNewMemberFilterActive]);

  const handleAddMember = () => {
    const newMember: Member = {
      id: 'new-member-' + Date.now(),
      names: [''],
      surname: '',
      cardNumber: '',
      dob: new Date(),
      virginityStatus: "unset",
      dateOfEntry: new Date(),
      reasonOfEntry: '',
      gender: "unset",
      position: "Member",
      mainBranch: "unset",
      phone: '',
      email: '',
      otpPreference: "unset",
      password: '',
      role: "member",
      lastSeenOffsetDays: 0,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [],
      selfManagementData: {
        messages: [],
        attendanceSummary: {
          totalAttendances: 0,
          last90Days: 0,
          raCount: 0
        }
      },
      lastLogin: undefined,
      lastPasswordChange: undefined,
      failedLoginAttempts: 0,
      accountActivated: false,
      profilePicture: '',
      emergencyContact: undefined,
      notes: [],
      permissions: {
        canManageAttendance: false,
        canManageMembers: false,
        canManageFinance: false
      },
      conversations: []
    };
    setCurrentMember(newMember);
    setIsDialogOpen(true);
  };

  const handleSaveMember = (updatedMember: Member) => {
    console.log('Saving new member:', updatedMember);
    setIsDialogOpen(false);
  };

  const toggleAgeGroupFilter = (ageGroup: string) => {
    setSelectedAgeGroups(prev =>
      prev.includes(ageGroup)
        ? prev.filter(g => g !== ageGroup)
        : [...prev, ageGroup]
    );
  };

  const filteredMembers = membersData.currentMembers.filter(member => {
    const ageGroupMatch = selectedAgeGroups.length === 0 ||
      selectedAgeGroups.includes(getAgeGroup(member.dob));
    const newMemberMatch = !isNewMemberFilterActive ||
      (member.dateOfEntry &&
        (new Date().getTime() - new Date(member.dateOfEntry).getTime()) <= 90 * 24 * 60 * 60 * 1000);
    return ageGroupMatch && newMemberMatch;
  });

  return (
    <div className="flex flex-col min-h-screen pb-[80px]">
      <header className="sticky top-0 z-50 w-full bg-white shadow-sm">
        <div className="flex items-center px-4 py-3">
          <img
            src={MKNLogo}
            alt="MKN Logo"
            className="h-8 w-auto"
          />
          <div className="ml-2 flex flex-col">
            <span className="text-lg font-semibold text-gray-800 tracking-wider">Muzi Ka Nkulunkulu</span>
            <p className="text-[0.65rem] text-gray-400 -mt-0.5 tracking-tight">where illnesses and troubles are cured</p>
          </div>
        </div>
      </header>
      <div className="p-4">
        <div className="mb-3 w-full">
          <SearchAndFilter
            {...membersData}
            selectedAgeGroups={selectedAgeGroups}
            toggleAgeGroupFilter={toggleAgeGroupFilter}
            setSelectedAgeGroups={setSelectedAgeGroups}
            showBranchFilter={showBranchFilter}
            setShowBranchFilter={setShowBranchFilter}
            totalMembers={filteredMembers.length}
            onAddMember={handleAddMember}
            isNewMemberFilterActive={isNewMemberFilterActive}
            setIsNewMemberFilterActive={setIsNewMemberFilterActive}
          />
        </div>

        <div className="flex-1 overflow-hidden">
          <MembersTable
            {...membersData}
            currentMembers={filteredMembers}
          />
        </div>
      </div>

      {isDialogOpen && currentMember && (
        <MemberProfileDialog
          member={currentMember}
          onClose={() => setIsDialogOpen(false)}
          onSave={handleSaveMember}
          isNewMember={true}
        />
      )}
    </div>
  );
};

function getAgeGroup(dob: Date): string {
  const today = new Date();
  const birthDate = new Date(dob);
  let age = today.getFullYear() - birthDate.getFullYear();
  const monthDiff = today.getMonth() - birthDate.getMonth();
  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birthDate.getDate())) {
    age--;
  }
  if (age < 13) return "Kids (0-12)";
  if (age < 36) return "Youth (13-35)";
  if (age < 50) return "Adults (36-49)";
  return "50+";
}

export default MembersPage;

