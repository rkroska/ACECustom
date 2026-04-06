import React from 'react';
import { AlertCircle } from 'lucide-react';
import logo from '../../assets/logo.svg';

const DisabledScreen: React.FC = () => (
  <div className="fixed inset-0 bg-[#0a0a0a] flex items-center justify-center z-[100] font-sans antialiased overflow-hidden">
    {/* Dynamic Background Elements */}
    <div className="absolute top-[-10%] left-[-10%] w-[40%] h-[40%] bg-blue-600/5 rounded-full blur-[120px] animate-pulse"></div>
    <div className="absolute bottom-[-10%] right-[-10%] w-[40%] h-[40%] bg-red-600/5 rounded-full blur-[120px] animate-pulse" style={{ animationDelay: '2s' }}></div>

    <div className="relative max-w-md w-full mx-4 flex flex-col items-center">
      {/* Central Graphic - ILT Icon substituted for DB/Server icon */}
      <div className="relative mb-10 group">
        <div className="absolute inset-0 bg-red-500/20 rounded-full blur-2xl animate-pulse scale-150 opacity-50"></div>
        <div className="relative w-28 h-28 rounded-[2.5rem] bg-neutral-900 border border-neutral-800 flex items-center justify-center shadow-inner overflow-hidden">
            <div className="absolute inset-x-0 bottom-0 h-1/2 bg-gradient-to-t from-red-500/10 to-transparent"></div>
            <div className="w-16 h-16 p-2 opacity-80 filter grayscale brightness-125 transition-all duration-700 group-hover:grayscale-0">
              <img src={logo} alt="ILT Icon" className="w-full h-full object-contain" />
            </div>
        </div>
        
        <div className="absolute -top-1 -right-1 w-8 h-8 rounded-full bg-red-500 border-4 border-[#0a0a0a] flex items-center justify-center animate-bounce shadow-lg">
          <AlertCircle className="w-4 h-4 text-white" />
        </div>
      </div>

      {/* Messaging */}
      <h1 className="text-4xl font-black text-white text-center mb-4 tracking-tight leading-tight uppercase">
        Portal Offline
      </h1>
      
      <p className="text-neutral-500 text-center mb-12 px-6 leading-relaxed font-medium text-sm">
        The Web Portal is currently <span className="text-red-400">disabled</span> by the server administrator. Please try again later.
      </p>
    </div>
  </div>
);

export default DisabledScreen;
