import React, { ReactNode } from 'react';
import Sidebar from '../components/common/Sidebar';

interface MainLayoutProps {
  children: ReactNode;
}

const MainLayout: React.FC<MainLayoutProps> = ({ children }) => {
  return (
    <div className="flex h-screen bg-neutral-900 text-neutral-100 font-sans">
      <Sidebar />
      <main className="flex-1 flex flex-col relative overflow-hidden h-full">
        {children}
      </main>
    </div>
  );
};

export default MainLayout;
