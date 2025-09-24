import { useState } from 'react';
import MKNLogo from '../../../assets/MKN.png';
import Roster from './Roster';

const CongregationManagement = () => {
  const [searchTerm] = useState('');

  return (
    <div className="flex flex-col min-h-screen pb-[80px] bg-white">
      <header className="sticky top-0 z-50 w-full bg-white shadow-sm">
        <div className="flex items-center px-4 py-3">
          <img src={MKNLogo} alt="MKN Logo" className="h-8 w-auto" />
          <div className="ml-2 flex flex-col">
            <span className="text-lg font-semibold text-gray-900 tracking-wider">Muzi Ka Nkulunkulu</span>
            <p className="text-[0.65rem] text-gray-500 -mt-0.5 tracking-tight">where illnesses and troubles are cured</p>
          </div>
        </div>
      </header>
      <div className="p-4">
        <div className="grid grid-cols-1 gap-6 mb-6">
          <Roster searchTerm={searchTerm} />
        </div>
      </div>
    </div>
  );
};

export default CongregationManagement;

