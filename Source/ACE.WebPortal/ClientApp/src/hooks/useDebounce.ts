import { useState, useEffect } from 'react';

/**
 * Standard debounce hook to prevent excessive re-renders/API calls on rapid input changes.
 * 
 * @param value The value to debounce.
 * @param delay The delay in milliseconds (default: 500ms).
 * @returns The debounced value.
 */
export function useDebounce<T>(value: T, delay: number = 500): T {
  const [debouncedValue, setDebouncedValue] = useState<T>(value);

  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedValue(value);
    }, delay);

    return () => {
      clearTimeout(handler);
    };
  }, [value, delay]);

  return debouncedValue;
}
