import { useAuthStore } from '../store/useAuthStore';
import { ApiError } from '../types';

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
  async request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const { logout, setPortalDisabled } = useAuthStore.getState();

    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...(options.headers as Record<string, string>),
    };

    try {
      const response = await fetch(endpoint, { 
        ...options, 
        headers,
        credentials: 'include' // Required for HttpOnly cookies
      });

      // Handle Global Status Codes
      if (response.status === 401) {
        console.warn('Unauthorized: Session expired or invalid. Logging out.');
        logout();
        throw new Error('Session expired. Please log in again.');
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

      // Clone response to allow reading body twice (JSON then text fallback)
      const clonedResponse = response.clone();
      let data: any;

      try {
        data = await response.json();
      } catch (jsonError) {
        // JSON parsing failed (likely a plain text error from nginx or a 500 page)
        const rawText = await clonedResponse.text();
        
        if (!response.ok) {
          const error: ApiError = {
            message: rawText || `Server error (${response.status})`,
            code: response.status.toString(),
          };
          throw error;
        }
        
        // If it's OK but not JSON, we can't reliably return it as T
        throw new Error('API returned an invalid (non-JSON) response.');
      }

      if (!response.ok) {
        const error: ApiError = {
          message: data.message || 'API request failed',
          code: data.code,
          details: data.details,
        };
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
  async get<T>(url: string, options?: RequestInit): Promise<T> {
    return this.request<T>(url, { ...options, method: 'GET' });
  }

  async post<T>(url: string, body?: any, options?: RequestInit): Promise<T> {
    return this.request<T>(url, {
      ...options,
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  async delete<T>(url: string, options?: RequestInit): Promise<T> {
    return this.request<T>(url, { ...options, method: 'DELETE' });
  }
}

export const api = ApiClient.getInstance();
