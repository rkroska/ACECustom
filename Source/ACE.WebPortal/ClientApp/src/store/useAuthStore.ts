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
        isAuthenticated: true, 
        user: data.username, 
        accessLevel: data.accessLevel,
        isLoading: false 
      });
    } catch (err) {
      set({ error: 'Connection error. Please check your internet and try again.', isLoading: false });
    }
  },
  logout: async () => {
    // Immediately clear local state for responsiveness
    set({ isAuthenticated: false, user: null, accessLevel: null, error: null });

    try {
      // Notify backend to clear the HttpOnly cookie in the background
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
          set({ 
            isAuthenticated: true, 
            user: data.username, 
            accessLevel: data.accessLevel 
          });
          return;
        }
      }
      // If not OK or malformed, ensure we are logged out
      set({ isAuthenticated: false, user: null, accessLevel: null });
    } catch (err) {
      set({ isAuthenticated: false, user: null, accessLevel: null });
    } finally {
      set({ isBootstrapping: false });
    }
  },
}));
