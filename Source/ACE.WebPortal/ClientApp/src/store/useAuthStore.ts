import { create } from 'zustand';

interface AuthState {
  isAuthenticated: boolean;
  user: string | null;
  accessLevel: number | null; // Storing the numeric access level for permission checks
  error: string | null;
  isLoading: boolean;
  isBootstrapping: boolean;
  isPortalDisabled: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  clearError: () => void;
  bootstrap: () => Promise<void>;
  setPortalDisabled: (disabled: boolean) => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  isAuthenticated: false,
  user: null,
  accessLevel: null,
  error: null,
  isLoading: false,
  isBootstrapping: true,
  isPortalDisabled: false,
  setPortalDisabled: (disabled) => set({ isPortalDisabled: disabled }),
  login: async (username, password) => {
    set({ isLoading: true, error: null, isPortalDisabled: false });
    try {
      const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password }),
      });

      if (!response.ok) {
        const data = await response.json();
        set({ error: data.message || 'Login failed.', isLoading: false });
        return;
      }

      const data = await response.json();
      
      set({ 
        isAuthenticated: true, 
        user: data.username, 
        accessLevel: data.accessLevel,
        isLoading: false 
      });
    } catch (err) {
      set({ error: 'Network error. Please try again.', isLoading: false });
    }
  },
  logout: async () => {
    try {
      // Notify backend to clear the HttpOnly cookie
      await fetch('/api/auth/logout', { method: 'POST' });
    } catch (e) {
      // Silent fail for network errors on logout
    }
    set({ isAuthenticated: false, user: null, accessLevel: null, error: null });
  },
  clearError: () => set({ error: null }),
  bootstrap: async () => {
    try {
      const response = await fetch('/api/auth/me');
      if (response.ok) {
        const data = await response.json();
        set({ 
          isAuthenticated: true, 
          user: data.username, 
          accessLevel: data.accessLevel 
        });
      }
    } catch (err) {
      // No active session
      set({ isAuthenticated: false, user: null, accessLevel: null });
    } finally {
      set({ isBootstrapping: false });
    }
  },
}));
