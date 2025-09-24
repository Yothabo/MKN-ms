import { useEffect, useState } from 'react';

interface NotificationBannerProps {
  message: string;
  type: 'error' | 'info';
  duration?: number;
  onClose?: () => void;
}

export const NotificationBanner = ({
  message,
  type,
  duration = 3000,
  onClose
}: NotificationBannerProps) => {
  const [isVisible, setIsVisible] = useState(true);

  useEffect(() => {
    const timer = setTimeout(() => {
      setIsVisible(false);
      onClose?.();
    }, duration);

    return () => clearTimeout(timer);
  }, [duration, onClose]);

  if (!isVisible) return null;

  return (
    <div className={`fixed top-20 left-1/2 transform -translate-x-1/2 z-50 px-4 py-2 rounded-md text-sm ${
      type === 'error' ? 'bg-red-100 text-red-800 border border-red-200' : 'bg-blue-100 text-blue-800 border border-blue-200'
    } shadow-lg transition-opacity duration-300`}>
      {message}
    </div>
  );
};
