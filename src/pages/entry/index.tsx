import React, { useState } from 'react';
import TopImage from '../../assets/Bg2.svg';
import LoginSignupModal from '../../components/LoginSignupModal';

const Entry: React.FC = () => {
  const [isModalOpen, setIsModalOpen] = useState(false);

  const handleOpenModal = () => setIsModalOpen(true);
  const handleCloseModal = () => setIsModalOpen(false);

  return (
    <div className="min-h-screen flex items-center justify-center p-4 sm:p-6 md:p-8 text-center relative">
      {/* Modal */}
      <LoginSignupModal isOpen={isModalOpen} onClose={handleCloseModal} />

      <div
        className={`flex flex-col items-center space-y-6 transition-all duration-300 ${
          isModalOpen ? 'blur-sm pointer-events-none' : ''
        }`}
      >
        {/* Top Image */}
        <div className="mb-4">
          <img
            src={TopImage}
            alt="Welcome Header"
            className="w-full max-w-sm sm:max-w-md md:max-w-lg object-contain"
            loading="lazy"
          />
        </div>

        {/* Title and Description */}
        <div className="flex flex-col items-center space-y-2 mb-4 animate-fadeIn">
          <h1 className="text-2xl md:text-3xl font-bold text-text-dark mb-2">
            Muzi ka Nkulunkulu
          </h1>
          <p className="text-sm md:text-base text-gray-700 max-w-xl font-semibold leading-relaxed">
            A centralized management platform for streamlined oversight and real-time coordination. Control over duty assignments, attendance tracking, and service operations.
          </p>
        </div>

        {/* Get Started Button */}
        <button
          onClick={handleOpenModal}
          className="bg-gradient-to-r from-green-500 to-teal-600 hover:from-green-600 hover:to-teal-700 text-white font-semibold py-4 px-16 md:px-20 rounded-full shadow-xl transition-transform duration-300 hover:scale-105 animate-slideUp delay-100 w-full max-w-[420px]"
          aria-label="Open login/signup modal"
        >
          Get Started
        </button>

        {/* Disclaimer */}
        <p className="text-[0.375rem] sm:text-[0.5rem] text-gray-600 max-w-md leading-tight animate-fadeIn delay-200">
          By continuing, you affirm your membership in the religion and agree to its{' '}
          <span className="text-brand-orange">values</span>,{' '}
          <span className="text-brand-orange">duties</span>, and{' '}
          <span className="text-brand-orange">structure</span>.
        </p>
      </div>
    </div>
  );
};

export default Entry;
