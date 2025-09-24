import { useState, useEffect } from 'react';
import { FiX } from 'react-icons/fi';
import { motion, AnimatePresence } from 'framer-motion';

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

interface RAStatusCardProps {
  members: Member[];
  setMembers: React.Dispatch<React.SetStateAction<Member[]>>;
  selectedBranch: string;
  presentMembers?: string[];
  setPresentMembers?: React.Dispatch<React.SetStateAction<string[]>>;
}

export const RAStatusCard = ({
  members,
  setMembers,
  selectedBranch,
  presentMembers = [],
  setPresentMembers = () => {}
}: RAStatusCardProps) => {
  const [showClearModal, setShowClearModal] = useState<string | null>(null);
  const [clearReason, setClearReason] = useState('');
  const [customReason, setCustomReason] = useState('');
  const [deathDetails, setDeathDetails] = useState({
    causeOfDeath: '',
    dateOfDeath: '',
    burialDate: '',
    burialLocation: '',
    burialTime: ''
  });
  const [filteredRaMembers, setFilteredRaMembers] = useState<Member[]>([]);
  const [filteredPreRaMembers, setFilteredPreRaMembers] = useState<Member[]>([]);

  // Update filtered members whenever the members list or presentMembers changes
  useEffect(() => {
    // Filter RA members - only show those with RA status who are NOT currently present
    const raMembers = members.filter(
      member =>
        (selectedBranch === 'All' || member.mainBranch === selectedBranch) &&
        member.status.toLowerCase() === 'ra' &&
        !presentMembers.includes(member.id)
    );

    // Filter Pre-RA members - only show those with Pre-RA status who are NOT currently present
    const preRaMembers = members.filter(
      member =>
        (selectedBranch === 'All' || member.mainBranch === selectedBranch) &&
        member.status.toLowerCase() === 'pre-ra' &&
        !presentMembers.includes(member.id)
    );

    setFilteredRaMembers(raMembers);
    setFilteredPreRaMembers(preRaMembers);
  }, [members, selectedBranch, presentMembers]);

  const handleClearRAStatus = (memberId: string) => {
    const member = members.find(m => m.id === memberId);
    if (!member) return;

    if (!clearReason) {
      alert('Please select a reason for clearing RA status.');
      return;
    }
    if (clearReason === 'Other' && !customReason.trim()) {
      alert('Please provide a custom reason for clearing RA status.');
      return;
    }

    // Handle different clearance reasons
    if (clearReason === 'Deceased') {
      // Mark as deceased with death details
      setMembers(prevMembers =>
        prevMembers.map(m =>
          m.id === memberId
            ? {
                ...m,
                status: 'Inactive',
                raCount: 0,
                deceased: true,
                dateOfDeath: deathDetails.dateOfDeath
              }
            : m
        )
      );
      // TODO: Send death notification to congregation view
      console.log('Death notification:', {
        member: `${member.names.join(' ')} ${member.surname}`,
        ...deathDetails
      });
    } else if (clearReason === 'Permanently Left Religion' || clearReason === 'Lost Faith') {
      // Deactivate member
      setMembers(prevMembers =>
        prevMembers.map(m =>
          m.id === memberId
            ? { ...m, status: 'Inactive', raCount: 0, deceased: false, dateOfDeath: null }
            : m
        )
      );
    } else {
      // Relocation or other reasons - just clear RA status
      setMembers(prevMembers =>
        prevMembers.map(m =>
          m.id === memberId
            ? { ...m, status: 'Active', raCount: 0, deceased: false, dateOfDeath: null }
            : m
        )
      );
    }

    // Remove from present members if they were present
    setPresentMembers(prev => prev.filter(id => id !== memberId));

    setShowClearModal(null);
    setClearReason('');
    setCustomReason('');
    setDeathDetails({
      causeOfDeath: '',
      dateOfDeath: '',
      burialDate: '',
      burialLocation: '',
      burialTime: ''
    });
  };

  const handleClearPreRAStatus = (memberId: string) => {
    // Simply clear Pre-RA status without requiring reason
    setMembers(prevMembers =>
      prevMembers.map(member =>
        member.id === memberId
          ? { ...member, status: 'Active', raCount: 0 }
          : member
      )
    );

    // Remove from present members if they were present
    setPresentMembers(prev => prev.filter(id => id !== memberId));
  };

  const getStatusStyles = (isPresent: boolean) => {
    return isPresent ? 'bg-green-100' : '';
  };

  const getLastAttendanceInfo = (member: Member) => {
    if (!member.attendance || member.attendance.length === 0) {
      return { branch: 'Never', date: null };
    }

    const sortedAttendance = [...member.attendance].sort((a, b) => a.dateOffsetDays - b.dateOffsetDays);
    const lastAttendance = sortedAttendance[0];

    return {
      branch: lastAttendance.branch,
      date: calculateActualDate(lastAttendance.dateOffsetDays),
    };
  };

  const formatDate = (date: Date | string | undefined | null) => {
    if (!date) return 'N/A';
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    return isNaN(dateObj.getTime()) ? 'N/A' : dateObj.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  const calculateActualDate = (offsetDays: number): Date => {
    const date = new Date();
    date.setDate(date.getDate() - offsetDays);
    return date;
  };

  const desktopColumns = [
    { width: '60px', name: 'Card' },
    { width: '100px', name: 'Name' },
    { width: '100px', name: 'Surname' },
    { width: '100px', name: 'Branch' },
    { width: '100px', name: 'Last Attended' },
    { width: '60px', name: 'Status' },
    { width: '80px', name: 'Action' },
  ];

  const renderTable = (filteredMembers: Member[], title: string, description: string, isRATable: boolean) => (
    <div className="mb-6">
      <h2 className="text-[0.8rem] font-bold text-gray-800 mb-2 md:max-w-3xl md:mx-auto">{title}</h2>
      <p className="text-xs text-gray-700 mb-3 md:max-w-3xl md:mx-auto">{description}</p>
      <div className="overflow-auto">
        {filteredMembers.length === 0 && (
          <div className="text-center text-gray-500 text-[0.6rem] py-4">
            No {title.toLowerCase()} found
          </div>
        )}
        <table className="min-w-full table-fixed bg-transparent">
          <thead className="bg-transparent sticky top-0 z-10">
            <tr className="md:hidden">
              <th
                style={{ width: '30%' }}
                className="px-2 py-1.5 text-left text-[0.55rem] font-semibold text-gray-500 uppercase tracking-wider"
              >
                Name
              </th>
              <th
                style={{ width: '30%' }}
                className="px-2 py-1.5 text-left text-[0.55rem] font-semibold text-gray-500 uppercase tracking-wider"
              >
                Last Attended
              </th>
              <th
                style={{ width: '20%' }}
                className="px-2 py-1.5 text-left text-[0.55rem] font-semibold text-gray-500 uppercase tracking-wider"
              >
                Status
              </th>
              <th
                style={{ width: '20%' }}
                className="px-2 py-1.5 text-left text-[0.55rem] font-semibold text-gray-500 uppercase tracking-wider"
              >
                Action
              </th>
            </tr>
            <tr className="hidden md:table-row">
              {desktopColumns.map(col => (
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
            {filteredMembers.map(member => {
              const lastAttendance = getLastAttendanceInfo(member);
              const isPresent = presentMembers.includes(member.id);

              return (
                <tr key={member.id} className={getStatusStyles(isPresent)}>
                  <td style={{ width: '30%' }} className="md:hidden px-2 py-1.5 whitespace-nowrap">
                    <div className="text-[0.6rem] font-semibold text-gray-700">
                      {member.names.join(' ')} {member.surname}
                    </div>
                    <div className="text-[0.5rem] text-gray-500">Card: {member.cardNumber}</div>
                  </td>
                  <td style={{ width: '30%' }} className="md:hidden px-2 py-1.5 whitespace-nowrap">
                    <div className="text-[0.6rem] text-gray-700">{lastAttendance.branch}</div>
                    <div className="text-[0.5rem] text-gray-500">
                      {lastAttendance.date ? formatDate(lastAttendance.date) : 'Never'}
                    </div>
                  </td>
                  <td style={{ width: '20%' }} className="md:hidden px-2 py-1.5 whitespace-nowrap">
                    <div
                      className={`text-[0.5rem] font-medium ${
                        member.status.toLowerCase() === 'ra'
                          ? 'text-red-600'
                          : member.status.toLowerCase() === 'pre-ra'
                          ? 'text-orange-500'
                          : 'text-green-600'
                      }`}
                    >
                      {member.status}
                    </div>
                  </td>
                  <td style={{ width: '20%' }} className="md:hidden px-2 py-1.5 whitespace-nowrap">
                    <button
                      onClick={() =>
                        isRATable
                          ? setShowClearModal(member.id)
                          : handleClearPreRAStatus(member.id)
                      }
                      className="px-2 py-1 border border-gray-300 rounded text-[0.5rem] font-medium text-gray-700 bg-white hover:bg-gray-50"
                    >
                      Clear Status
                    </button>
                  </td>
                  {desktopColumns.map(col => {
                    let content;
                    switch (col.name) {
                      case 'Card':
                        content = (
                          <div className="text-[0.6rem] text-right text-gray-600">
                            {member.cardNumber}
                          </div>
                        );
                        break;
                      case 'Name':
                        content = (
                          <div className="text-[0.6rem] text-gray-700">{member.names.join(' ')}</div>
                        );
                        break;
                      case 'Surname':
                        content = (
                          <div className="text-[0.6rem] text-gray-700">{member.surname}</div>
                        );
                        break;
                      case 'Branch':
                        content = (
                          <div className="text-[0.6rem] text-gray-700">{member.mainBranch}</div>
                        );
                        break;
                      case 'Last Attended':
                        content = (
                          <div className="text-[0.6rem] text-gray-500">
                            {lastAttendance.date ? formatDate(lastAttendance.date) : 'Never'}
                          </div>
                        );
                        break;
                      case 'Status':
                        content = (
                          <span
                            className={`text-[0.45rem] font-medium ${
                              member.status.toLowerCase() === 'ra'
                                ? 'text-red-600'
                                : member.status.toLowerCase() === 'pre-ra'
                                ? 'text-orange-500'
                                : 'text-green-600'
                            }`}
                          >
                            {member.status}
                          </span>
                        );
                        break;
                      case 'Action':
                        content = (
                          <button
                            onClick={() =>
                              isRATable
                                ? setShowClearModal(member.id)
                                : handleClearPreRAStatus(member.id)
                            }
                            className="px-2 py-1 border border-gray-300 rounded text-[0.5rem] font-medium text-gray-700 bg-white hover:bg-gray-50"
                          >
                            Clear Status
                          </button>
                        );
                        break;
                      default:
                        content = null;
                    }
                    return (
                      <td
                        key={`${member.id}-${col.name}`}
                        style={{ width: col.width }}
                        className="hidden md:table-cell px-2 py-1.5 whitespace-nowrap"
                      >
                        {content}
                      </td>
                    );
                  })}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );

  return (
    <div className="bg-transparent rounded-lg shadow-sm">
      <h1 className="text-[0.8rem] font-bold text-gray-800 mb-2 md:max-w-3xl md:mx-auto">
        RA Status Overview
      </h1>
      <p className="text-xs text-gray-700 mb-4 md:max-w-3xl md:mx-auto">
        Manage member attendance statuses. RA (Re-admission) status is assigned to members absent for 3
        months, requiring a reason for removal. Pre-RA status indicates members within 7 days of RA
        assignment, prompting outreach to prevent escalation. Former RA members are those previously on RA
        status, with their removal reasons and count of RA occurrences recorded.
      </p>
      {renderTable(
        filteredRaMembers,
        'Current RA Members',
        'Members absent for 3 months and assigned RA (Re-admission) status. Clear RA status by providing a reason.',
        true
      )}
      {renderTable(
        filteredPreRaMembers,
        'Current Pre-RA Members',
        'Members within 7 days of being assigned RA status. Clear Pre-RA status to prevent escalation or reach out to these members.',
        false
      )}
      <AnimatePresence>
        {showClearModal && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-black bg-opacity-10 flex items-center justify-center p-4 z-50 backdrop-blur-sm"
          >
            <div className="bg-white rounded-lg shadow-lg border border-gray-200 w-full max-w-md">
              <div className="px-4 py-3 border-b border-gray-200 flex justify-between items-center">
                <h3 className="text-sm font-bold text-gray-800">Clear RA Status</h3>
                <button
                  onClick={() => {
                    setShowClearModal(null);
                    setClearReason('');
                    setCustomReason('');
                    setDeathDetails({
                      causeOfDeath: '',
                      dateOfDeath: '',
                      burialDate: '',
                      burialLocation: '',
                      burialTime: ''
                    });
                  }}
                  className="text-gray-400 hover:text-gray-500"
                >
                  <FiX size={18} />
                </button>
              </div>
              <div className="p-4 max-h-96 overflow-y-auto">
                <div className="mb-4">
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    Reason for Clearing RA Status *
                  </label>
                  <select
                    value={clearReason}
                    onChange={e => setClearReason(e.target.value)}
                    className="block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
                    required
                  >
                    <option value="">Select a reason</option>
                    <option value="Permanently Left Religion">Permanently Left Religion</option>
                    <option value="Relocation">Relocation</option>
                    <option value="Lost Faith">Lost Faith</option>
                    <option value="Deceased">Deceased</option>
                    <option value="Other">Other</option>
                  </select>
                </div>

                {clearReason === 'Deceased' && (
                  <div className="space-y-3 mb-4 p-3 bg-gray-50 rounded-md">
                    <h4 className="text-xs font-medium text-gray-700">Death Details</h4>
                    <div>
                      <label className="block text-xs font-medium text-gray-700 mb-1">
                        Cause of Death *
                      </label>
                      <input
                        type="text"
                        value={deathDetails.causeOfDeath}
                        onChange={e => setDeathDetails(prev => ({ ...prev, causeOfDeath: e.target.value }))}
                        className="block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
                        required
                        placeholder="Enter cause of death"
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-medium text-gray-700 mb-1">
                        Date of Death *
                      </label>
                      <input
                        type="date"
                        value={deathDetails.dateOfDeath}
                        onChange={e => setDeathDetails(prev => ({ ...prev, dateOfDeath: e.target.value }))}
                        className="block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
                        required
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-medium text-gray-700 mb-1">
                        Burial Date
                      </label>
                      <input
                        type="date"
                        value={deathDetails.burialDate}
                        onChange={e => setDeathDetails(prev => ({ ...prev, burialDate: e.target.value }))}
                        className="block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
                        placeholder="Optional"
                      />
                    </div>
                    {deathDetails.burialDate && (
                      <>
                        <div>
                          <label className="block text-xs font-medium text-gray-700 mb-1">
                            Burial Location
                          </label>
                          <input
                            type="text"
                            value={deathDetails.burialLocation}
                            onChange={e => setDeathDetails(prev => ({ ...prev, burialLocation: e.target.value }))}
                            className="block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
                            placeholder="Burial service location"
                          />
                        </div>
                        <div>
                          <label className="block text-xs font-medium text-gray-700 mb-1">
                            Burial Time
                          </label>
                          <input
                            type="time"
                            value={deathDetails.burialTime}
                            onChange={e => setDeathDetails(prev => ({ ...prev, burialTime: e.target.value }))}
                            className="block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
                          />
                        </div>
                      </>
                    )}
                  </div>
                )}

                {clearReason === 'Other' && (
                  <div className="mb-4">
                    <label className="block text-xs font-medium text-gray-700 mb-1">
                      Custom Reason *
                    </label>
                    <input
                      type="text"
                      value={customReason}
                      onChange={e => setCustomReason(e.target.value)}
                      className="block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
                      required
                      placeholder="Enter custom reason"
                    />
                  </div>
                )}
              </div>
              <div className="px-4 py-3 border-t border-gray-200 flex justify-end space-x-2">
                <button
                  onClick={() => {
                    setShowClearModal(null);
                    setClearReason('');
                    setCustomReason('');
                    setDeathDetails({
                      causeOfDeath: '',
                      dateOfDeath: '',
                      burialDate: '',
                      burialLocation: '',
                      burialTime: ''
                    });
                  }}
                  className="px-3 py-1 border border-gray-300 rounded text-xs font-medium text-gray-700 bg-white hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  onClick={() => handleClearRAStatus(showClearModal)}
                  className="px-3 py-1 border border-transparent rounded text-xs font-medium text-white bg-red-600 hover:bg-red-700"
                  disabled={!clearReason || (clearReason === 'Other' && !customReason.trim()) || (clearReason === 'Deceased' && (!deathDetails.causeOfDeath || !deathDetails.dateOfDeath))}
                >
                  Confirm
                </button>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
};

export default RAStatusCard;
