import { useAuthStore } from '../store/useAuthStore';

/**
 * Global API Client for ILT Web Portal
 * 
 * Features:
 * - Centralized fetch wrapper
 * - Automatic Bearer token injection
 * - Global error handling (401 - Unauthorized, 503 - Portal Disabled)
 * - Standardized JSON response handling
 */

class ApiClient {
  private static instance: ApiClient;

  private constructor() {}

  public static getInstance(): ApiClient {
    if (!ApiClient.instance) {
      ApiClient.instance = new ApiClient();
    }
    return ApiClient.instance;
  }

  /**
   * Performs a fetch with automatic auth header injection.
   * If the portal is disabled (503) or the session expires (401), it triggers global state changes.
   */
  async request<T>(endpoint: string, options: RequestInit = {}): Promise<T | null> {
    const { logout, setPortalDisabled } = useAuthStore.getState();

    const headers = new Headers(options.headers || {});
    if (!headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json');
    }

    try {
      const response = await fetch(endpoint, { 
        ...options, 
        headers,
        credentials: 'include' // Required for HttpOnly cookies
      });

      // Handle Global Status Codes
      if (response.status === 401) {
        const isAuthMe = endpoint.includes('/api/auth/me');
        const { isAuthenticated } = useAuthStore.getState();
        if (!isAuthMe || isAuthenticated) {
          console.warn('Unauthorized: Session expired or invalid. Logging out.');
          logout();
        }
        throw new Error(isAuthMe ? 'Not signed in.' : 'Session expired. Please log in again.');
      }

      if (response.status === 503) {
        // Clone for reading text, check if it's the 'portal disabled' message
        const cloned = response.clone();
        try {
          const body = await cloned.json();
          if (body?.message?.toLowerCase().includes('disabled')) {
            console.error('Portal Disabled: Triggering global maintenance screen.');
            setPortalDisabled(true);
          }
        } catch (e) {
          // Fallback if not JSON or different message
        }
        throw new Error('Web Portal is currently offline.');
      }

      // Handle Empty Content (204/205) Success - Only return null for intentional no-content statuses
      if (response.ok && (response.status === 204 || response.status === 205)) {
        return null;
      }

      // If we have an empty body on an error, let the standard error path handle it
      if (!response.ok && response.headers.get('content-length') === '0') {
        const error = new Error(`Server error (${response.status})`);
        error.name = 'ApiError';
        (error as any).code = response.status.toString();
        throw error;
      }

      // Clone response to allow reading body twice (JSON then text fallback)
      const clonedResponse = response.clone();
      let data: any;

      try {
        data = await response.json();
      } catch (jsonError) {
        // JSON parsing failed (likely a plain text error from nginx or a 500 page)
        const rawText = await clonedResponse.text();
        
        if (!response.ok) {
          const error = new Error(rawText || `Server error (${response.status})`);
          error.name = 'ApiError';
          (error as any).code = response.status.toString();
          throw error;
        }
        
        // If it's OK but not JSON and not empty content, we can't reliably return it as T
        throw new Error('API returned an invalid (non-JSON) response.');
      }

      if (!response.ok) {
        const error = new Error(data.message || 'API request failed');
        error.name = 'ApiError';
        (error as any).code = data.code || response.status.toString();
        (error as any).details = data.details;
        throw error;
      }

      return data as T;
    } catch (error) {
      if (error instanceof Error) {
        throw error;
      }
      throw new Error('Network communication error.');
    }
  }

  // Helper Methods for common HTTP verbs
  async get<T>(url: string, options?: RequestInit): Promise<T | null> {
    return this.request<T>(url, { ...options, method: 'GET' });
  }

  async post<T>(url: string, body?: any, options?: RequestInit): Promise<T | null> {
    return this.request<T>(url, {
      ...options,
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  async put<T>(url: string, body?: any, options?: RequestInit): Promise<T | null> {
    return this.request<T>(url, {
      ...options,
      method: 'PUT',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  async delete<T>(url: string, options?: RequestInit): Promise<T | null> {
    return this.request<T>(url, { ...options, method: 'DELETE' });
  }

  /** POST and download binary response (e.g. zip export). */
  async postBlob(url: string, body: unknown, filename: string): Promise<void> {
    const { logout, setPortalDisabled } = useAuthStore.getState();
    const response = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify(body),
    });

    if (response.status === 401) {
      logout();
      throw new Error('Session expired.');
    }
    if (response.status === 503) {
      setPortalDisabled(true);
      throw new Error('Web Portal is offline.');
    }
    if (!response.ok) {
      let message = `Export failed (${response.status})`;
      try {
        const data = await response.json();
        if (data?.message) message = data.message;
      } catch {
        /* ignore */
      }
      throw new Error(message);
    }

    const blob = await response.blob();
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    link.click();
    URL.revokeObjectURL(link.href);
  }
}

export const api = ApiClient.getInstance();
