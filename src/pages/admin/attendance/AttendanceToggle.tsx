import { FiCheck, FiX } from 'react-icons/fi';

interface AttendanceToggleProps {
  isPresent: boolean;
  onToggle: (e: React.MouseEvent) => void;
  disabled?: boolean;
}

export const AttendanceToggle = ({ isPresent, onToggle, disabled }: AttendanceToggleProps) => {
  return (
    <button
      onClick={onToggle}
      disabled={disabled}
      className={`flex items-center justify-center w-8 h-5 rounded-full p-0.5 transition-colors duration-200 ${
        isPresent ? 'bg-green-500' : 'bg-gray-300'
      } ${disabled ? 'opacity-50 cursor-not-allowed' : 'hover:opacity-90'}`}
    >
      <div
        className={`w-4 h-4 bg-white rounded-full shadow-sm transform transition-transform duration-200 ${
          isPresent ? 'translate-x-3' : 'translate-x-0'
        } flex items-center justify-center`}
      >
        {isPresent ? (
          <FiCheck className="text-green-500 text-[0.6rem]" />
        ) : (
          <FiX className="text-gray-500 text-[0.6rem]" />
        )}
      </div>
    </button>
  );
};
