import React, { useState, useEffect } from 'react';
import { FiArrowLeft, FiEdit2, FiSave, FiLock, FiUnlock, FiUser, FiCalendar, FiPhone, FiMail, FiMapPin, FiShield, FiMessageSquare, FiSend, FiInfo, FiCheck, FiTrash2, FiX } from 'react-icons/fi';
import { motion, AnimatePresence } from 'framer-motion';

export interface Member {
  id: string;
  names: string[];
  surname: string;
  cardNumber: number | string;
  dob: Date | null;
  virginityStatus: "virgin" | "non-virgin" | "inapplicable" | "unset";
  dateOfEntry: Date | null;
  reasonOfEntry: string;
  gender: "male" | "female" | "other" | "unset";
  position: string;
  mainBranch: "Johannesburg" | "Bulawayo" | "Gaborone" | "Harare" | "Ireland" | "unset";
  phone: string;
  email: string;
  otpPreference: "phone" | "email" | "unset";
  password: string;
  role: "member" | "admin" | "super_admin";
  lastSeenOffsetDays: number;
  raCount: number;
  status: "Active" | "Pre-RA" | "RA" | "Inactive";
  reAdmissionOffsetDays: number | null;
  raHistory: {
    dateOffsetDays: number;
    removedDate: string | null;
    removedReason: string | null;
  }[];
  trackingActive: boolean;
  preRaWarningSentOffsetDays: number | null;
  globalLock: boolean;
  lockStatus: "Unlocked" | "RA Lock" | "Warning" | "Deceased" | "Untracked";
  attendance: {
    dateOffsetDays: number;
    branch: "unset" | "Johannesburg" | "Bulawayo" | "Gaborone" | "Harare" | "Ireland";
    type: "Regular" | "Guest";
    timestampOffsetHours: number;
  }[];
  selfManagementData: {
    messages: string[];
    attendanceSummary: {
      totalAttendances: number;
      last90Days: number;
      raCount: number;
    };
  };
  deceasedInfo?: {
    dateOfDeath: Date;
    reason: string;
    memorialServiceDate: Date;
    deathCertificateNumber?: string;
  };
  lastLogin?: Date;
  lastPasswordChange?: Date;
  failedLoginAttempts?: number;
  accountActivated?: boolean;
  profilePicture?: string;
  emergencyContact?: {
    name: string;
    relationship: string;
    phone: string;
  };
  notes?: string[];
  permissions?: {
    canManageAttendance?: boolean;
    canManageMembers?: boolean;
    canManageFinance?: boolean;
  };
  conversations?: {
    date: Date;
    from: "member" | "admin";
    message: string;
    adminName?: string;
  }[];
}

interface MemberProfileDialogProps {
  member: Member;
  onClose: () => void;
  onSave: (updatedMember: Member) => void;
  isNewMember?: boolean;
}

const tabVariants = {
  hidden: { opacity: 0, x: 50 },
  visible: { opacity: 1, x: 0, transition: { duration: 0.3 } },
  exit: { opacity: 0, x: -50, transition: { duration: 0.2 } }
};

const availablePositions = [
  "Member",
  "Evangelist",
  "Holy Messenger",
  "Facilitator",
  "Clerk",
  "Conciliator",
  "Steward",
  "Songster"
];

const getDefaultMember = (): Member => ({
  id: '',
  names: [],
  surname: '',
  cardNumber: '',
  dob: null,
  virginityStatus: "unset",
  dateOfEntry: null,
  reasonOfEntry: '',
  gender: "unset",
  position: 'Member',
  mainBranch: "unset",
  phone: '',
  email: '',
  otpPreference: "unset",
  password: '',
  role: "member",
  lastSeenOffsetDays: 0,
  raCount: 0,
  status: 'Active',
  reAdmissionOffsetDays: null,
  raHistory: [],
  trackingActive: true,
  preRaWarningSentOffsetDays: null,
  globalLock: false,
  lockStatus: 'Unlocked',
  attendance: [],
  selfManagementData: {
    messages: [],
    attendanceSummary: {
      totalAttendances: 0,
      last90Days: 0,
      raCount: 0
    }
  }
});

export const MemberProfileDialog = ({ member, onClose, onSave, isNewMember = false }: MemberProfileDialogProps) => {
  const [isEditing, setIsEditing] = useState(isNewMember);
  const [formData, setFormData] = useState<Member>(isNewMember ? getDefaultMember() : { ...member });
  const [activeTab, setActiveTab] = useState<'profile' | 'attendance' | 'history' | 'conversations' | 'hints'>('profile');
  const [newMessage, setNewMessage] = useState('');
  const [showSaveSuccess, setShowSaveSuccess] = useState(false);
  const [showDeactivateModal, setShowDeactivateModal] = useState(false);
  const [deactivationReason, setDeactivationReason] = useState('');
  const [dateOfDeath, setDateOfDeath] = useState('');
  const [causeOfDeath, setCauseOfDeath] = useState('');
  const [memorialDate, setMemorialDate] = useState('');

  useEffect(() => {
    if (!isNewMember) {
      setFormData({ ...member });
    }
  }, [member, isNewMember]);

  const calculateActualDate = (offsetDays: number): Date => {
    const date = new Date();
    date.setDate(date.getDate() - offsetDays);
    return date;
  };

  const formatDateTime = (date: Date | string | undefined | null) => {
    if (!date) return 'N/A';
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    return dateObj.toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  const formatDate = (date: Date | string | undefined | null) => {
    if (!date) return 'N/A';
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    return dateObj.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  const handleEditToggle = () => {
    if (isEditing) {
      onSave(formData);
      setShowSaveSuccess(true);
      setTimeout(() => setShowSaveSuccess(false), 2000);
    }
    setIsEditing(!isEditing);
  };

  const handleInputChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>,
    field: keyof Member
  ) => {
    const value = e.target.value;
    let updatedValue: any = value;

    if (field === 'dob' || field === 'dateOfEntry') {
      updatedValue = value ? new Date(value) : null;
    }

    setFormData(prev => ({
      ...prev,
      [field]: updatedValue
    }));
  };

  const handleNamesChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFormData({ ...formData, names: e.target.value.split(' ').filter(Boolean) });
  };

  const handleDeactivate = () => {
    if (deactivationReason === 'Deceased') {
      if (!dateOfDeath || !causeOfDeath || !memorialDate) {
        alert('Please fill all required fields for deceased members');
        return;
      }

      const updatedMember = {
        ...formData,
        status: 'Inactive' as 'Inactive',
        lockStatus: 'Deceased' as 'Deceased',
        deceasedInfo: {
          dateOfDeath: new Date(dateOfDeath),
          reason: causeOfDeath,
          memorialServiceDate: new Date(memorialDate),
          deathCertificateNumber: ''
        }
      };

      onSave(updatedMember);
    } else {
      const updatedMember = {
        ...formData,
        status: 'Inactive' as 'Inactive',
        lockStatus: 'Untracked' as 'Untracked'
      };
      onSave(updatedMember);
    }

    setShowDeactivateModal(false);
    setDeactivationReason('');
    setDateOfDeath('');
    setCauseOfDeath('');
    setMemorialDate('');
  };

  const handleSendMessage = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newMessage.trim()) return;

    const newConversation = {
      date: new Date(),
      from: 'admin' as const,
      message: newMessage,
      adminName: 'Admin'
    };

    const updatedMember = {
      ...formData,
      conversations: [...(formData.conversations || []), newConversation]
    };

    setFormData(updatedMember);
    onSave(updatedMember);
    setNewMessage('');
  };

  const getStatusBadge = (status: string) => {
    switch (status.toLowerCase().trim()) {
      case 'ra': return { backgroundColor: 'rgba(220, 38, 38, 0.1)', color: '#dc2626' };
      case 'pre-ra': return { backgroundColor: 'rgba(249, 115, 22, 0.1)', color: '#f97316' };
      case 'inactive': return { backgroundColor: 'rgba(107, 114, 128, 0.1)', color: '#6b7280' };
      default: return { backgroundColor: 'rgba(22, 163, 74, 0.1)', color: '#16a34a' };
    }
  };

  const getLockStatusIcon = () => {
    switch (formData.lockStatus) {
      case 'RA Lock': return <FiLock className="text-red-500 text-sm" />;
      case 'Warning': return <FiLock className="text-yellow-500 text-sm" />;
      case 'Deceased': return <FiLock className="text-gray-500 text-sm" />;
      case 'Untracked': return <FiLock className="text-blue-500 text-sm" />;
      default: return <FiUnlock className="text-green-500 text-sm" />;
    }
  };

  const renderField = (label: string, value: React.ReactNode, icon?: React.ReactNode) => (
    <div className="flex items-start py-2 border-b border-gray-100">
      {icon && <div className="mr-2 text-gray-400 text-xs">{icon}</div>}
      <div className="flex-1">
        <div className="text-[0.65rem] font-medium text-gray-500 uppercase tracking-wider">{label}</div>
        <div className="mt-0.5 text-xs font-medium text-gray-800">{value || 'N/A'}</div>
      </div>
    </div>
  );

  const renderEditableField = (
    label: string,
    field: keyof Member,
    type: 'text' | 'select' | 'date' | 'textarea' = 'text',
    options?: string[],
    icon?: React.ReactNode,
    customHandler?: (e: React.ChangeEvent<HTMLInputElement>) => void
  ) => (
    <div className="flex items-start py-2 border-b border-gray-100">
      {icon && <div className="mr-2 text-gray-400 text-xs">{icon}</div>}
      <div className="flex-1">
        <label className="text-[0.65rem] font-medium text-gray-500 uppercase tracking-wider">{label}</label>
        {isEditing ? (
          type === 'select' ? (
            <select
              value={(formData[field] as any)?.toString() || ''}
              onChange={(e) => handleInputChange(e, field)}
              className="mt-0.5 block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
            >
              {options?.map((option) => (
                <option key={option} value={option}>{option}</option>
              ))}
            </select>
          ) : type === 'date' ? (
            <input
              type="date"
              value={formData[field] ? (formData[field] as Date).toISOString().split('T')[0] : ''}
              onChange={(e) => handleInputChange(e, field)}
              className="mt-0.5 block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
            />
          ) : type === 'textarea' ? (
            <textarea
              value={(formData[field] as any)?.toString() || ''}
              onChange={(e) => handleInputChange(e, field)}
              rows={2}
              className="mt-0.5 block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
            />
          ) : (
            <input
              type={type}
              value={field === 'names' ? formData.names.join(' ') : (formData[field] as any)?.toString() || ''}
              onChange={customHandler || ((e) => handleInputChange(e, field))}
              className="mt-0.5 block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
            />
          )
        ) : (
          <div className="mt-0.5 text-xs font-medium text-gray-800">
            {type === 'date' ? formatDate(formData[field] as Date) : field === 'names' ? formData.names.join(' ') : (formData[field] as any)?.toString() || 'N/A'}
          </div>
        )}
      </div>
    </div>
  );

  return (
    <div className="fixed inset-0 bg-black bg-opacity-10 flex items-center justify-center p-4 z-50 backdrop-blur-sm">
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        exit={{ opacity: 0, y: 20 }}
        className="bg-white rounded-xl shadow-xl w-full max-w-md flex flex-col border border-gray-100"
        style={{ height: '70vh' }}
      >
        {/* Header */}
        <div className="bg-gray-50 px-4 py-3 rounded-t-xl flex items-center justify-between border-b border-gray-200">
          <motion.button
            onClick={onClose}
            className="text-gray-600 hover:text-gray-900 rounded-full p-1 transition-colors"
            whileHover={{ scale: 1.1 }}
            whileTap={{ scale: 0.95 }}
          >
            <FiArrowLeft size={16} />
          </motion.button>
          <div className="flex items-center">
            <h2 className="text-sm font-bold text-gray-800">
              {isNewMember ? 'New Member' : 'Member Profile'}
            </h2>
            {!isNewMember && (
              <div style={getStatusBadge(formData.status)} className="ml-2 inline-flex items-center rounded-full px-2 py-1 text-[0.65rem] font-semibold uppercase">
                {formData.status}
              </div>
            )}
          </div>
          {activeTab === 'profile' && (
            <motion.button
              onClick={handleEditToggle}
              className="text-gray-600 hover:text-gray-900 rounded-full p-1 transition-colors"
              whileHover={{ scale: 1.1 }}
              whileTap={{ scale: 0.95 }}
            >
              {isEditing ? <FiSave size={16} /> : <FiEdit2 size={16} />}
            </motion.button>
          )}
        </div>

        {/* Save Success Notification */}
        <AnimatePresence>
          {showSaveSuccess && (
            <motion.div
              initial={{ opacity: 0, y: -20 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -20 }}
              className="absolute top-16 left-0 right-0 flex justify-center"
            >
              <div className="bg-green-100 text-green-800 px-4 py-2 rounded-md shadow-md flex items-center">
                <FiCheck className="mr-2" />
                <span className="text-sm font-medium">Changes saved successfully!</span>
              </div>
            </motion.div>
          )}
        </AnimatePresence>

        {/* Tabs */}
        <div className="border-b border-gray-200 px-4 bg-gray-50">
          <nav className="-mb-px flex space-x-4">
            <motion.button
              onClick={() => setActiveTab('profile')}
              className={`py-2 px-1 border-b-2 font-medium text-xs ${activeTab === 'profile' ? 'border-gray-800 text-gray-800' : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'}`}
              whileHover={{ scale: 1.05 }}
              whileTap={{ scale: 0.95 }}
            >
              Profile
            </motion.button>
            {!isNewMember && (
              <>
                <motion.button
                  onClick={() => setActiveTab('attendance')}
                  className={`py-2 px-1 border-b-2 font-medium text-xs ${activeTab === 'attendance' ? 'border-gray-800 text-gray-800' : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'}`}
                  whileHover={{ scale: 1.05 }}
                  whileTap={{ scale: 0.95 }}
                >
                  Attendance
                </motion.button>
                <motion.button
                  onClick={() => setActiveTab('history')}
                  className={`py-2 px-1 border-b-2 font-medium text-xs ${activeTab === 'history' ? 'border-gray-800 text-gray-800' : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'}`}
                  whileHover={{ scale: 1.05 }}
                  whileTap={{ scale: 0.95 }}
                >
                  History
                </motion.button>
                <motion.button
                  onClick={() => setActiveTab('conversations')}
                  className={`py-2 px-1 border-b-2 font-medium text-xs ${activeTab === 'conversations' ? 'border-gray-800 text-gray-800' : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'}`}
                  whileHover={{ scale: 1.05 }}
                  whileTap={{ scale: 0.95 }}
                >
                  Conversations
                </motion.button>
              </>
            )}
            <motion.button
              onClick={() => setActiveTab('hints')}
              className={`py-2 px-1 border-b-2 font-medium text-xs ${activeTab === 'hints' ? 'border-gray-800 text-gray-800' : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'}`}
              whileHover={{ scale: 1.05 }}
              whileTap={{ scale: 0.95 }}
            >
              <FiInfo size={14} />
            </motion.button>
          </nav>
        </div>

        {/* Content Area */}
        <div className="overflow-y-auto flex-1 p-4 bg-white relative">
          <AnimatePresence mode="wait">
            <motion.div
              key={activeTab}
              variants={tabVariants}
              initial="hidden"
              animate="visible"
              exit="exit"
              className="h-full"
            >
              {/* Profile Tab */}
              {activeTab === 'profile' && (
                <div className="space-y-2">
                  {renderEditableField('Full Name', 'names', 'text', undefined, <FiUser className="text-xs" />, handleNamesChange)}
                  {renderEditableField('Surname', 'surname', 'text', undefined, <FiUser className="text-xs" />)}
                  {renderEditableField('Card Number', 'cardNumber', 'text', undefined, <FiUser className="text-xs" />)}
                  {renderEditableField('Date of Birth', 'dob', 'date', undefined, <FiCalendar className="text-xs" />)}
                  {renderEditableField('Date of Entry', 'dateOfEntry', 'date', undefined, <FiCalendar className="text-xs" />)}
                  {renderEditableField('Reason of Entry', 'reasonOfEntry', 'textarea')}
                  {renderEditableField('Gender', 'gender', 'select', ['male', 'female', 'other', 'unset'])}
                  {renderEditableField('Position', 'position', 'select', availablePositions)}
                  {renderEditableField('Main Branch', 'mainBranch', 'select', ['Johannesburg', 'Bulawayo', 'Gaborone', 'Harare', 'Ireland', 'unset'], <FiMapPin className="text-xs" />)}
                  {renderEditableField('Phone', 'phone', 'text', undefined, <FiPhone className="text-xs" />)}
                  {renderEditableField('Email', 'email', 'text', undefined, <FiMail className="text-xs" />)}
                  {renderEditableField('OTP Preference', 'otpPreference', 'select', ['phone', 'email', 'unset'])}
                  {renderEditableField('Role', 'role', 'select', ['member', 'admin', 'super_admin'])}
                  {renderEditableField('Virginity Status', 'virginityStatus', 'select', ['virgin', 'non-virgin', 'inapplicable', 'unset'])}
                  {!isNewMember && renderField('Lock Status', formData.lockStatus, getLockStatusIcon())}
                  {!isNewMember && renderField('Account Activated', formData.accountActivated ? 'Yes' : 'No', <FiShield className="text-xs" />)}
                  {renderField('Profile Picture', formData.profilePicture || 'Not available', <FiUser className="text-xs" />)}
                  {formData.emergencyContact && (
                    <>
                      {renderField('Emergency Contact Name', formData.emergencyContact.name, <FiUser className="text-xs" />)}
                      {renderField('Emergency Contact Relationship', formData.emergencyContact.relationship)}
                      {renderField('Emergency Contact Phone', formData.emergencyContact.phone, <FiPhone className="text-xs" />)}
                    </>
                  )}
                  {formData.permissions && (
                    <>
                      {renderField('Can Manage Attendance', formData.permissions.canManageAttendance ? 'Yes' : 'No', <FiShield className="text-xs" />)}
                      {renderField('Can Manage Members', formData.permissions.canManageMembers ? 'Yes' : 'No', <FiShield className="text-xs" />)}
                      {renderField('Can Manage Finance', formData.permissions.canManageFinance ? 'Yes' : 'No', <FiShield className="text-xs" />)}
                    </>
                  )}
                  {formData.notes && formData.notes.length > 0 && (
                    <>
                      <div className="text-xs font-medium text-gray-700 mt-3">Notes</div>
                      <div className="space-y-1">
                        {formData.notes.map((note, index) => (
                          <div key={index} className="bg-yellow-50 p-2 rounded text-xs border border-yellow-100">
                            {note}
                          </div>
                        ))}
                      </div>
                    </>
                  )}
                </div>
              )}

              {/* Attendance Tab */}
              {!isNewMember && activeTab === 'attendance' && (
                <div className="space-y-4">
                  <div>
                    <div className="text-xs font-medium text-gray-700 mb-2">Recent Attendance</div>
                    {formData.attendance.length > 0 ? (
                      <div className="space-y-2">
                        {formData.attendance.slice(0, 5).map((record, index) => (
                          <div key={index} className="border-l-2 border-gray-800 pl-2 py-1">
                            <div className="flex justify-between text-xs">
                              <span className="font-medium">{formatDate(calculateActualDate(record.dateOffsetDays))}</span>
                              <span className={`px-1 rounded ${
                                record.type === 'Regular'
                                  ? 'bg-gray-100 text-gray-800'
                                  : 'bg-blue-100 text-blue-800'
                              }`}>
                                {record.type}
                              </span>
                            </div>
                            <div className="text-[0.65rem] text-gray-600">
                              {record.branch}
                            </div>
                          </div>
                        ))}
                      </div>
                    ) : (
                      <div className="text-xs text-gray-500 py-2">No attendance records</div>
                    )}
                  </div>

                  <div className="border-t border-gray-100 pt-3">
                    <div className="text-xs font-medium text-gray-700 mb-2">Summary</div>
                    <div className="grid grid-cols-2 gap-2">
                      <div className="bg-gray-50 p-2 rounded border border-gray-100">
                        <div className="text-[0.65rem] font-medium text-gray-500">RA Count</div>
                        <div className="text-sm font-bold text-gray-800">{formData.raCount}</div>
                      </div>
                      <div className="bg-gray-50 p-2 rounded border border-gray-100">
                        <div className="text-[0.65rem] font-medium text-gray-500">Last Seen</div>
                        <div className="text-sm font-bold text-gray-800">
                          {formatDate(calculateActualDate(formData.lastSeenOffsetDays))}
                        </div>
                      </div>
                      <div className="bg-gray-50 p-2 rounded border border-gray-100">
                        <div className="text-[0.65rem] font-medium text-gray-500">Re-Admission</div>
                        <div className="text-sm font-bold text-gray-800">
                          {formData.reAdmissionOffsetDays ? formatDate(calculateActualDate(formData.reAdmissionOffsetDays)) : 'N/A'}
                        </div>
                      </div>
                      <div className="bg-gray-50 p-2 rounded border border-gray-100">
                        <div className="text-[0.65rem] font-medium text-gray-500">Pre-RA Warning Sent</div>
                        <div className="text-sm font-bold text-gray-800">
                          {formData.preRaWarningSentOffsetDays ? formatDate(calculateActualDate(formData.preRaWarningSentOffsetDays)) : 'N/A'}
                        </div>
                      </div>
                      <div className="bg-gray-50 p-2 rounded border border-gray-100">
                        <div className="text-[0.65rem] font-medium text-gray-500">Tracking Active</div>
                        <div className="text-sm font-bold text-gray-800">
                          {formData.trackingActive ? 'Yes' : 'No'}
                        </div>
                      </div>
                      <div className="bg-gray-50 p-2 rounded border border-gray-100">
                        <div className="text-[0.65rem] font-medium text-gray-500">Total Attendances</div>
                        <div className="text-sm font-bold text-gray-800">
                          {formData.selfManagementData.attendanceSummary.totalAttendances}
                        </div>
                      </div>
                    </div>
                  </div>

                  {formData.deceasedInfo && (
                    <div className="border-t border-gray-100 pt-3">
                      <div className="text-xs font-medium text-gray-700 mb-2">Deceased Information</div>
                      <div className="bg-red-50 p-2 rounded text-xs border border-red-100">
                        <div className="font-medium">Date: {formatDate(formData.deceasedInfo.dateOfDeath)}</div>
                        <div>Reason: {formData.deceasedInfo.reason}</div>
                        <div>Memorial Service: {formatDate(formData.deceasedInfo.memorialServiceDate)}</div>
                        {formData.deceasedInfo.deathCertificateNumber && (
                          <div>Certificate: {formData.deceasedInfo.deathCertificateNumber}</div>
                        )}
                      </div>
                    </div>
                  )}
                </div>
              )}

              {/* History Tab */}
              {!isNewMember && activeTab === 'history' && (
                <div className="space-y-4">
                  <div>
                    <div className="text-xs font-medium text-gray-700 mb-2">RA History</div>
                    {formData.raHistory.length > 0 ? (
                      <div className="space-y-2">
                        {formData.raHistory.map((record, index) => (
                          <div key={index} className="border-l-2 border-red-500 pl-2 py-1 bg-red-50 rounded-r">
                            <div className="flex justify-between text-xs">
                              <span className="font-medium">
                                {record.removedDate ? 'Removed' : 'Added'} on {formatDate(calculateActualDate(record.dateOffsetDays))}
                              </span>
                              {record.removedDate && (
                                <span className="text-gray-500">
                                  {formatDate(new Date(record.removedDate))}
                                </span>
                              )}
                            </div>
                            {record.removedReason && (
                              <div className="text-[0.65rem] text-gray-600 mt-1">
                                <span className="font-medium">Removal Reason:</span> {record.removedReason}
                              </div>
                            )}
                          </div>
                        ))}
                      </div>
                    ) : (
                      <div className="text-xs text-gray-500 py-2">No RA history</div>
                    )}
                  </div>
                </div>
              )}

              {/* Conversations Tab */}
              {!isNewMember && activeTab === 'conversations' && (
                <div className="space-y-3 h-full flex flex-col">
                  <div className="flex-1 overflow-y-auto space-y-3">
                    {formData.conversations && formData.conversations.length > 0 ? (
                      formData.conversations.map((convo, index) => (
                        <div key={index} className={`flex ${convo.from === 'admin' ? 'justify-end' : 'justify-start'}`}>
                          <div className={`max-w-xs p-3 rounded-lg ${convo.from === 'admin' ? 'bg-gray-200' : 'bg-green-100'}`}>
                            <div className="text-[0.65rem] text-gray-500 mb-1">
                              {convo.from === 'admin' ? (convo.adminName || 'Admin') : 'Member'} • {formatDateTime(convo.date)}
                            </div>
                            <div className="text-xs text-gray-800">
                              {convo.message}
                            </div>
                          </div>
                        </div>
                      ))
                    ) : (
                      <div className="text-center py-8">
                        <FiMessageSquare className="mx-auto text-gray-300 text-2xl" />
                        <div className="text-xs text-gray-500 mt-2">No conversations yet</div>
                      </div>
                    )}
                  </div>

                  <form onSubmit={handleSendMessage} className="border-t border-gray-200 pt-3">
                    <div className="flex items-center space-x-2">
                      <input
                        type="text"
                        value={newMessage}
                        onChange={(e) => setNewMessage(e.target.value)}
                        placeholder="Type your message..."
                        className="flex-1 text-xs p-2 border border-gray-300 rounded focus:outline-none focus:ring-1 focus:ring-gray-400"
                      />
                      <motion.button
                        type="submit"
                        className="p-2 bg-gray-800 text-white rounded hover:bg-gray-700"
                        whileHover={{ scale: 1.05 }}
                        whileTap={{ scale: 0.95 }}
                      >
                        <FiSend size={16} />
                      </motion.button>
                    </div>
                  </form>
                </div>
              )}

              {/* Hints Tab */}
              {activeTab === 'hints' && (
                <div className="space-y-4 text-xs">
                  <div className="bg-blue-50 p-3 rounded border border-blue-100">
                    <h3 className="font-bold text-blue-800 mb-1">RA Status</h3>
                    <p className="text-gray-700">
                      <strong>RA (Re-Admission)</strong>: A status assigned to a member who has not attended for 3 months. This indicates they are eligible for re-admission into active participation, subject to congregational processes, and serves as a marker for tracking their return.
                    </p>
                    <p className="text-gray-700 mt-2">
                      <strong>Pre-RA</strong>: A warning status applied to a member who is 7 days away from attaining RA status due to inactivity. This acts as a final notice to encourage attendance before re-admission becomes necessary.
                    </p>
                    <p className="text-gray-700 mt-2">
                      <strong>Inactive</strong>: Denotes a member who is no longer actively participating, either because they are deceased or have formally left the religion. This status is permanent and excludes them from active attendance or duty rosters.
                    </p>
                  </div>

                  <div className="bg-yellow-50 p-3 rounded border border-yellow-100">
                    <h3 className="font-bold text-yellow-800 mb-1">Lock Status</h3>
                    <p className="text-gray-700">
                      <strong>RA Lock</strong>: Restricts a member's account due to prolonged inactivity leading to RA status (3 months without attendance), preventing further activity until re-admission is processed.
                    </p>
                    <p className="text-gray-700 mt-2">
                      <strong>Warning</strong>: A temporary restriction applied to a member's account when they are approaching the inactivity threshold (e.g., 7 days from RA), serving as a cautionary measure to prompt engagement.
                    </p>
                    <p className="text-gray-700 mt-2">
                      <strong>Deceased</strong>: Permanently locks a member's account upon confirmation of their passing, halting all activity and tracking.
                    </p>
                    <p className="text-gray-700 mt-2">
                      <strong>Untracked</strong>: Indicates a member's account is fully restricted and no longer monitored for attendance or other metrics. This applies after a member has accumulated 4 RA statuses, signifying a long-term disengagement from the congregation.
                    </p>
                  </div>
                  <div className="bg-green-50 p-3 rounded border border-green-100">
                    <h3 className="font-bold text-green-800 mb-1">Attendance Types</h3>
                    <p className="text-gray-700">
                      <strong>Regular</strong>: Records a member's attendance at their designated home branch, fully contributing to their activity requirements and maintaining their active status.
                    </p>
                    <p className="text-gray-700 mt-2">
                      <strong>Guest</strong>: Logs attendance at a branch other than the member's home branch, still counting toward activity requirements as valid participation and reflecting flexibility in attendance options.
                    </p>
                  </div>

                  <div className="bg-purple-50 p-3 rounded border border-purple-100">
                    <h3 className="font-bold text-purple-800 mb-1">Virginity Status</h3>
                    <p className="text-gray-700">
                      <strong>Tracked</strong>: Actively monitored for youths aged 13 to 35, where spiritual purity (virginity) is a requirement for specific duties or rites within the congregation. This status is critical for eligibility in roles demanding this criterion.
                    </p>
                    <p className="text-gray-700 mt-2">
                      <strong>Inapplicable</strong>: Indicates exemption from the virginity status check, applying to members who are Evangelists, Holy Messengers, married, have children, are above 35 years old, or are below 13 years old. This status reflects roles, life stages, or ages where the requirement does not apply.
                    </p>
                  </div>
                </div>
              )}
            </motion.div>
          </AnimatePresence>

          {/* Deactivate Member Modal - Now inside the dialog */}
          <AnimatePresence>
            {showDeactivateModal && (
              <motion.div
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                className="absolute inset-0 bg-white bg-opacity-95 p-4 flex items-center justify-center"
              >
                <div className="w-full max-w-sm bg-white rounded-lg shadow-lg border border-gray-200">
                  <div className="px-4 py-3 border-b border-gray-200 flex justify-between items-center">
                    <h3 className="text-sm font-bold text-gray-800">
                      Deactivate {formData.names.join(' ')} {formData.surname}
                    </h3>
                    <button
                      onClick={() => setShowDeactivateModal(false)}
                      className="text-gray-400 hover:text-gray-500"
                    >
                      <FiX size={18} />
                    </button>
                  </div>

                  <div className="p-4">
                    <div className="mb-4">
                      <label className="block text-xs font-medium text-gray-700 mb-1">
                        Reason for Deactivation
                      </label>
                      <select
                        value={deactivationReason}
                        onChange={(e) => setDeactivationReason(e.target.value)}
                        className="block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
                      >
                        <option value="">Select a reason</option>
                        <option value="Left Congregation">Left Congregation</option>
                        <option value="Deceased">Deceased</option>
                        <option value="Other">Other</option>
                      </select>
                    </div>

                    {deactivationReason === 'Deceased' && (
                      <>
                        <div className="mb-3">
                          <label className="block text-xs font-medium text-gray-700 mb-1">
                            Date of Death
                          </label>
                          <input
                            type="date"
                            value={dateOfDeath}
                            onChange={(e) => setDateOfDeath(e.target.value)}
                            className="block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
                          />
                        </div>

                        <div className="mb-3">
                          <label className="block text-xs font-medium text-gray-700 mb-1">
                            Cause of Death
                          </label>
                          <input
                            type="text"
                            value={causeOfDeath}
                            onChange={(e) => setCauseOfDeath(e.target.value)}
                            className="block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
                          />
                        </div>

                        <div className="mb-3">
                          <label className="block text-xs font-medium text-gray-700 mb-1">
                            Memorial Service Date
                          </label>
                          <input
                            type="date"
                            value={memorialDate}
                            onChange={(e) => setMemorialDate(e.target.value)}
                            className="block w-full rounded border-gray-300 shadow-sm focus:border-gray-500 focus:ring focus:ring-gray-200 focus:ring-opacity-50 py-1 px-2 border text-xs"
                          />
                        </div>
                      </>
                    )}
                  </div>

                  <div className="px-4 py-3 border-t border-gray-200 flex justify-end space-x-2">
                    <button
                      onClick={() => setShowDeactivateModal(false)}
                      className="px-3 py-1 border border-gray-300 rounded text-xs font-medium text-gray-700 bg-white hover:bg-gray-50"
                    >
                      Cancel
                    </button>
                    <button
                      onClick={handleDeactivate}
                      className="px-3 py-1 border border-transparent rounded text-xs font-medium text-white bg-red-600 hover:bg-red-700"
                    >
                      Confirm Deactivation
                    </button>
                  </div>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>

        {/* Footer */}
        <div className="border-t border-gray-200 px-4 py-2 bg-gray-50 rounded-b-xl flex justify-between">
          {!isNewMember && formData.status !== 'Inactive' && (
            <motion.button
              onClick={() => setShowDeactivateModal(true)}
              className="px-3 py-1 border border-red-300 rounded text-xs font-medium text-red-700 bg-red-50 hover:bg-red-100"
              whileHover={{ scale: 1.03 }}
              whileTap={{ scale: 0.98 }}
            >
              <FiTrash2 className="inline mr-1" />
              Deactivate Member
            </motion.button>
          )}

          <div className="flex">
            <motion.button
              onClick={onClose}
              className="px-3 py-1 border border-gray-300 rounded text-xs font-medium text-gray-700 bg-white hover:bg-gray-50"
              whileHover={{ scale: 1.03 }}
              whileTap={{ scale: 0.98 }}
            >
              Close
            </motion.button>

            {isNewMember ? (
              <motion.button
                onClick={() => onSave(formData)}
                className="ml-2 px-3 py-1 border border-transparent rounded text-xs font-medium text-white bg-green-600 hover:bg-green-700"
                whileHover={{ scale: 1.03 }}
                whileTap={{ scale: 0.98 }}
              >
                Save
              </motion.button>
            ) : (
              <motion.button
                onClick={handleEditToggle}
                className="ml-2 px-3 py-1 border border-transparent rounded text-xs font-medium text-white bg-gray-700 hover:bg-gray-600"
                whileHover={{ scale: 1.03 }}
                whileTap={{ scale: 0.98 }}
              >
                {isEditing ? 'Save Changes' : 'Edit Profile'}
              </motion.button>
            )}
          </div>
        </div>
      </motion.div>
    </div>
  );
};

