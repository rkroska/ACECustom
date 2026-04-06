/**
 * Shared authentication utilities for role mapping and access levels.
 */
export const getRoleName = (level: number): string => {
  switch (level) {
    case 1: return 'Advocate';
    case 2: return 'Sentinel';
    case 3: return 'Envoy';
    case 4: return 'Developer';
    case 5: return 'Admin';
    default: return 'Player';
  }
};
