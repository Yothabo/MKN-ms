import React from 'react';
import { usePwaInstall } from '../hooks/usePwaInstall';
import { FiDownload } from 'react-icons/fi';

export default function InstallBanner() {
  const { promptEvent, triggerInstall } = usePwaInstall();

  if (!promptEvent) return null;

  return (
    <button
      onClick={triggerInstall}
      className="fixed top-4 right-4 z-50 w-10 h-10 bg-green-600 text-white rounded-full flex items-center justify-center shadow-lg transform transition-all duration-300 hover:scale-110"
      title="Install App"
    >
      <FiDownload size={20} />
    </button>
  );
}
