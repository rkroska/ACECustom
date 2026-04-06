import React from 'react';
import ReactDOM from 'react-dom/client';
import { HashRouter } from 'react-router-dom';
import App from './App.tsx';
import { useAuthStore } from './store/useAuthStore';
import './index.css';

// Session is now bootstrapped asynchronously in App.tsx

// Global fetch interceptor to handle 'Portal Disabled' (503) errors
const originalFetch = window.fetch;
window.fetch = async (...args) => {
  try {
    const response = await originalFetch(...args);
    
    if (response.status === 503) {
      // Clone the response to read the body without consuming it for the original caller
      const clonedResponse = response.clone();
      try {
        const data = await clonedResponse.json();
        if (data.message && data.message.includes("disabled")) {
          useAuthStore.getState().setPortalDisabled(true);
        }
      } catch (e) {
        // Not a JSON response or doesn't match our criteria, ignore
      }
    }
    
    return response;
  } catch (error) {
    // Re-throw network errors
    throw error;
  }
};

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <HashRouter>
      <App />
    </HashRouter>
  </React.StrictMode>
);
