import React from 'react';
import { Link } from 'react-router-dom';
import { ShieldAlert } from 'lucide-react';
import { useAuthStore } from '../../store/useAuthStore';

type ProtectedRouteProps = Readonly<{
  pageKey: string;
  children: React.ReactNode;
}>;

const AccessDenied: React.FC = () => (
  <div className="flex-1 flex items-center justify-center p-8 text-center bg-neutral-900 text-neutral-100">
    <div className="max-w-md w-full">
      <ShieldAlert className="w-12 h-12 text-red-500 mb-4 opacity-50 mx-auto" />
      <h2 className="font-bold text-white mb-2 uppercase tracking-widest text-[10px]">Access Restricted</h2>
      <p className="text-neutral-500 text-sm font-medium">You don&apos;t have permission to view this page.</p>
      <div className="mt-6">
        <Link
          to="/characters"
          className="inline-flex items-center justify-center px-4 py-2 bg-neutral-800 hover:bg-neutral-700 rounded-xl text-xs font-bold transition-all uppercase tracking-widest border border-neutral-700"
        >
          Back to Characters
        </Link>
      </div>
    </div>
  </div>
);

export default function ProtectedRoute({ pageKey, children }: ProtectedRouteProps) {
  const canAccess = useAuthStore((s) => s.canAccessPage(pageKey));
  if (!canAccess) return <AccessDenied />;
  return <>{children}</>;
}
