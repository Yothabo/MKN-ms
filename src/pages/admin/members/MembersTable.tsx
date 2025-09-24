// src/pages/admin/members/MembersTable.tsx
import { useState } from 'react';
import { type Member, MemberProfileDialog } from './MemberProfileDialog';

export const MembersTable = ({ currentMembers }: { currentMembers: Member[] }) => {
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [selectedMember, setSelectedMember] = useState<Member | null>(null);

  const getStatusStyles = (status: string) => {
    switch (status.toLowerCase().trim()) {
      case 'ra':
        return '!bg-red-50 !text-red-600 hover:!bg-red-100';
      case 'pre-ra':
        return '!bg-orange-50 !text-orange-500 hover:!bg-orange-100';
      case 'inactive':
        return '!bg-gray-100 !text-gray-500 hover:!bg-gray-200';
      default:
        return 'hover:bg-gray-50';
    }
  };

  const sortedMembers = [...currentMembers].sort((a, b) => {
    const firstNameA = a.names[0]?.toLowerCase() || '';
    const firstNameB = b.names[0]?.toLowerCase() || '';
    return firstNameA.localeCompare(firstNameB);
  });

  const handleRowClick = (member: Member) => {
    setSelectedMember(member);
    setIsDialogOpen(true);
  };

  const handleCloseDialog = () => {
    setIsDialogOpen(false);
    setSelectedMember(null);
  };
  
  const handleSaveMember = (updatedMember: Member) => {
    console.log('Member saved:', updatedMember);
  };

  const desktopColumns = [
    { width: '60px', name: 'Card' },
    { width: '100px', name: 'Name' },
    { width: '100px', name: 'Surname' },
    { width: '100px', name: 'Phone' },
    { width: '150px', name: 'Email' },
    { width: '80px', name: 'Branch' },
    { width: '80px', name: 'Position' },
    { width: '60px', name: 'Status' }
  ];

  return (
    <>
      <div className="overflow-hidden" style={{ height: 'calc(100vh - 160px)' }}>
        <div className="overflow-y-auto h-full">
          <table className="min-w-full table-fixed bg-transparent">
            <thead className="bg-gray-50 sticky top-0 z-10">
              <tr className="md:hidden">
                <th style={{ width: '33%' }} className="px-2 py-1.5 text-left text-[0.55rem] font-semibold text-gray-500 uppercase tracking-wider">
                  Name
                </th>
                <th style={{ width: '33%' }} className="px-2 py-1.5 text-left text-[0.55rem] font-semibold text-gray-500 uppercase tracking-wider">
                  Contact
                </th>
                <th style={{ width: '34%' }} className="px-2 py-1.5 text-left text-[0.55rem] font-semibold text-gray-500 uppercase tracking-wider">
                  Branch
                </th>
              </tr>
              <tr className="hidden md:table-row">
                {desktopColumns.map((col) => (
                  <th
                    key={col.name}
                    style={{ width: col.width }}
                    className="px-2 py-1.5 text-left text-[0.6rem] font-semibold text-gray-500 uppercase tracking-wider"
                  >
                    {col.name}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="bg-transparent">
              {sortedMembers.length > 0 ? (
                sortedMembers.map((member) => (
                  <tr
                    key={member.id}
                    className={`${getStatusStyles(member.status)} transition-colors duration-150 cursor-pointer`}
                    onClick={() => handleRowClick(member)}
                  >
                    <td style={{ width: '33%' }} className="md:hidden px-2 py-1.5 whitespace-nowrap">
                      <div className="text-[0.6rem] font-semibold text-gray-700">
                        {member.names.join(' ')} {member.surname}
                      </div>
                      <div className="text-[0.5rem] flex items-center gap-1">
                        <span className="text-gray-500">Card: {member.cardNumber}</span>
                        {member.status.toLowerCase() !== 'active' && (
                          <span className={`font-medium ${
                            member.status.toLowerCase() === 'ra' ? '!text-red-600' :
                            member.status.toLowerCase() === 'pre-ra' ? '!text-orange-500' :
                            '!text-gray-500'
                          }`}>
                            {member.status}
                          </span>
                        )}
                      </div>
                    </td>
                    <td style={{ width: '33%' }} className="md:hidden px-2 py-1.5 whitespace-nowrap">
                      <div className="text-[0.6rem] text-gray-700">{member.phone}</div>
                      <div className="text-[0.5rem] text-gray-500">{member.email}</div>
                    </td>
                    <td style={{ width: '34%' }} className="md:hidden px-2 py-1.5 whitespace-nowrap">
                      <div className="text-[0.6rem] text-gray-700 font-semibold">{member.mainBranch}</div>
                      <div className="text-[0.5rem] text-gray-500">{member.position}</div>
                    </td>

                    <td style={{ width: '60px' }} className="hidden md:table-cell px-2 py-1.5 whitespace-nowrap">
                      <div className="text-[0.6rem] text-right text-gray-600">
                        {member.cardNumber}
                      </div>
                    </td>
                    <td style={{ width: '100px' }} className="hidden md:table-cell px-2 py-1.5 whitespace-nowrap">
                      <div className="text-[0.6rem] text-gray-700">
                        {member.names.join(' ')}
                      </div>
                    </td>
                    <td style={{ width: '100px' }} className="hidden md:table-cell px-2 py-1.5 whitespace-nowrap">
                      <div className="text-[0.6rem] text-gray-700">
                        {member.surname}
                      </div>
                    </td>
                    <td style={{ width: '100px' }} className="hidden md:table-cell px-2 py-1.5 whitespace-nowrap">
                      <div className="text-[0.6rem] text-gray-700">
                        {member.phone}
                      </div>
                    </td>
                    <td style={{ width: '150px' }} className="hidden md:table-cell px-2 py-1.5 whitespace-nowrap">
                      <div className="text-[0.6rem] text-gray-500 truncate">
                        {member.email}
                      </div>
                    </td>
                    <td style={{ width: '80px' }} className="hidden md:table-cell px-2 py-1.5 whitespace-nowrap">
                      <div className="text-[0.6rem] text-gray-700">
                        {member.mainBranch}
                      </div>
                    </td>
                    <td style={{ width: '80px' }} className="hidden md:table-cell px-2 py-1.5 whitespace-nowrap">
                      <div className="text-[0.6rem] text-gray-500">
                        {member.position}
                      </div>
                    </td>
                    <td style={{ width: '60px' }} className="hidden md:table-cell px-2 py-1.5 whitespace-nowrap">
                      {member.status.toLowerCase() !== 'active' ? (
                        <span className={`text-[0.45rem] font-medium ${
                          member.status.toLowerCase() === 'ra' ? '!text-red-600' :
                          member.status.toLowerCase() === 'pre-ra' ? '!text-orange-500' :
                          '!text-gray-500'
                        }`}>
                          {member.status}
                        </span>
                      ) : null}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={desktopColumns.length} className="px-3 py-3 text-center text-[0.6rem] text-gray-500">
                    No members found
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
      {isDialogOpen && selectedMember && (
        <MemberProfileDialog
          member={selectedMember}
          onClose={handleCloseDialog}
          onSave={handleSaveMember}
          isNewMember={false}
        />
      )}
    </>
  );
};

