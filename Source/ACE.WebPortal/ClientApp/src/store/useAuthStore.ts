import { create } from 'zustand';
import { canAccessPage as checkPageAccess } from '../utils/auth';
import { resetPortalHash } from '../utils/portalNavigation';

interface AuthState {
  isAuthenticated: boolean;
  user: string | null;
  accessLevel: number | null;
  pageAccess: Record<string, boolean> | null;
  error: string | null;
  isLoading: boolean;
  isBootstrapping: boolean;
  isPortalDisabled: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  clearError: () => void;
  bootstrap: () => Promise<void>;
  setPortalDisabled: (disabled: boolean) => void;
  setPageAccess: (pageAccess: Record<string, boolean>) => void;
  canAccessPage: (pageKey: string) => boolean;
}

const applyAuthPayload = (data: { username: string; accessLevel: number; pageAccess?: Record<string, boolean> }) => ({
  isAuthenticated: true as const,
  user: data.username,
  accessLevel: data.accessLevel,
  pageAccess: data.pageAccess ?? null,
});

export const useAuthStore = create<AuthState>((set, get) => ({
  isAuthenticated: false,
  user: null,
  accessLevel: null,
  pageAccess: null,
  error: null,
  isLoading: false,
  isBootstrapping: true,
  isPortalDisabled: false,
  setPortalDisabled: (disabled) => set({ isPortalDisabled: disabled }),
  setPageAccess: (pageAccess) => set({ pageAccess }),
  canAccessPage: (pageKey) => checkPageAccess(get().accessLevel, get().pageAccess, pageKey),
  login: async (username, password) => {
    set({ isLoading: true, error: null, isPortalDisabled: false });
    try {
      const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password }),
      });

      const data = await response.json().catch(() => ({ message: 'Invalid server response structure.' }));

      if (!response.ok) {
        set({ error: data.message || 'Login failed.', isLoading: false });
        return;
      }

      if (!data.username || data.accessLevel === undefined) {
        set({ error: 'Incomplete user data received from server.', isLoading: false });
        return;
      }
      
      set({ 
        ...applyAuthPayload(data),
        isLoading: false 
      });
      resetPortalHash('/characters');
    } catch (err) {
      set({ error: 'Connection error. Please check your internet and try again.', isLoading: false });
    }
  },
  logout: async () => {
    set({ isAuthenticated: false, user: null, accessLevel: null, pageAccess: null, error: null });
    resetPortalHash('/');

    try {
      await fetch('/api/auth/logout', { method: 'POST' });
    } catch (e) {
      // Silent fail for network errors on logout
    }
  },
  clearError: () => set({ error: null }),
  bootstrap: async () => {
    try {
      const response = await fetch('/api/auth/me');
      if (response.ok) {
        const data = await response.json().catch(() => null);
        if (data && data.username && data.accessLevel !== undefined) {
          set(applyAuthPayload(data));
          return;
        }
      }
      set({ isAuthenticated: false, user: null, accessLevel: null, pageAccess: null });
    } catch (err) {
      set({ isAuthenticated: false, user: null, accessLevel: null, pageAccess: null });
    } finally {
      set({ isBootstrapping: false });
    }
  },
}));
