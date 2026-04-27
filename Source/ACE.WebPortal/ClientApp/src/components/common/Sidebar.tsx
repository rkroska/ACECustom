import React from 'react';
import { NavLink } from 'react-router-dom';
import { User, Users, Globe, Search, Book, FileCode, Terminal, Settings, LogOut } from 'lucide-react';
import { useAuthStore } from '../../store/useAuthStore';
import { getRoleName } from '../../utils/auth';
import { cn } from '../../utils/cn';
import logo from '../../assets/logo.svg';

const Sidebar: React.FC = () => {
  const { user, logout, accessLevel } = useAuthStore();
  const isAdmin = accessLevel !== null && accessLevel > 0;

  return (
    <div className="w-64 bg-neutral-950 border-r border-neutral-800 flex flex-col z-10 shadow-2xl">
      <div className="p-6 border-b border-neutral-800 flex items-center gap-3">
        <div className="w-8 h-8 rounded-lg flex items-center justify-center overflow-hidden">
          <img src={logo} alt="ACE Logo" className="w-full h-full object-contain" />
        </div>
        <h1 className="font-bold text-lg tracking-tight text-white">ILT Web Portal</h1>
      </div>
      
      <nav className="flex-1 p-4 space-y-2 overflow-y-auto custom-scrollbar">
        <SidebarItem to="/characters" icon={<User className="w-4 h-4" />} label="Characters" />

        <div className="h-px bg-neutral-800/50 my-6 mx-2" />

        {isAdmin && (
          <>
            <div className="mt-10 mb-4 px-4">
              <h2 className="text-[14px] font-black text-blue-400 uppercase tracking-[0.1em]">
                Admin Tools
              </h2>
            </div>
            
            <NavSection label="Monitoring" />
            <div className="space-y-1">
              <SidebarItem to="/players" icon={<Users className="w-4 h-4" />} label="Player List" />
              <SidebarItem to="/map" icon={<Globe className="w-4 h-4" />} label="World Map" />
            </div>

            <NavSection label="Content Tools" />
            <div className="space-y-1">
              <SidebarItem to="/properties" icon={<Search className="w-4 h-4" />} label="Property Explorer" />
              <SidebarItem to="/lookup" icon={<Book className="w-4 h-4" />} label="Lookup Tables" />
              <SidebarItem to="/weenie" icon={<FileCode className="w-4 h-4" />} label="Weenie Editor" />
            </div>

            <NavSection label="Server Management" />
            <div className="space-y-1">
              <SidebarItem to="/console" icon={<Terminal className="w-4 h-4" />} label="Console" />
              <SidebarItem to="/params" icon={<Settings className="w-4 h-4" />} label="Server Params" />
            </div>
          </>
        )}
      </nav>
      
      <div className="p-4 border-t border-neutral-800 space-y-5">
        {/* User Profile Card */}
        <div className="flex items-center gap-3 px-2 py-1">
          <div className="w-10 h-10 rounded-full bg-blue-600/20 border border-blue-500/30 flex items-center justify-center text-blue-400 font-bold text-sm tracking-tighter">
            {user?.substring(0, 2).toUpperCase() || 'U'}
          </div>
          <div className="flex flex-col min-w-0">
            <span className="text-sm font-semibold text-white truncate leading-tight">{user}</span>
            {isAdmin && (
              <span className="text-[10px] text-blue-400/80 font-bold uppercase tracking-wider">
                {getRoleName(accessLevel!)}
              </span>
            )}
          </div>
        </div>

        <div className="space-y-3">
          <button 
            onClick={() => logout()}
            className="w-full flex items-center gap-3 px-4 py-2.5 rounded-xl text-neutral-500 hover:bg-red-500/10 hover:text-red-500 transition-all duration-200 group"
          >
            <LogOut className="w-4 h-4 group-hover:scale-110 transition-transform" />
            <span className="text-sm font-medium">Log out</span>
          </button>
          
          <div className="flex items-center gap-2 text-[10px] text-neutral-600 px-4 uppercase tracking-[0.2em] font-bold">
            <div className="w-1.5 h-1.5 rounded-full bg-green-500 shadow-[0_0_8px_rgba(34,197,94,0.5)]"></div>
            Server Status: <span className="text-green-500/80">Online</span>
          </div>
        </div>
      </div>
    </div>
  );
};

const SidebarItem: React.FC<{ to: string; icon: React.ReactNode; label: string }> = ({ to, icon, label }) => (
  <NavLink
    to={to}
    className={({ isActive }) => cn(
      "w-full flex items-center gap-3 px-4 py-2.5 rounded-xl transition-all duration-200",
      isActive 
        ? "bg-blue-600 text-white shadow-lg shadow-blue-600/20" 
        : "text-neutral-400 hover:bg-neutral-800 hover:text-neutral-200"
    )}
  >
    {({ isActive }) => (
      <>
        <div className={cn(isActive ? 'text-white' : 'text-neutral-500')}>
          {icon}
        </div>
        <span className="text-sm font-medium">{label}</span>
      </>
    )}
  </NavLink>
);

const NavSection: React.FC<{ label: string; className?: string }> = ({ label, className }) => (
  <div className={cn("mt-8 mb-3 px-4 flex items-center gap-2", className)}>
    <h3 className="text-[10px] font-bold text-neutral-500 uppercase tracking-[0.2em] whitespace-nowrap">
      {label}
    </h3>
    <div className="h-px bg-neutral-800 flex-1 opacity-30" />
  </div>
);

export default Sidebar;
