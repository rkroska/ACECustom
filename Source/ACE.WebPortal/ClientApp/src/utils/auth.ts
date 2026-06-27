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

/** Matches PortalAccessManager.DefaultRestrictedPageMinLevel on the server. */
export const DEFAULT_RESTRICTED_PAGE_MIN_LEVEL = 4;

export const canAccessPage = (userLevel: number | null, pageAccess: Record<string, boolean> | null, pageKey: string): boolean => {
  if (userLevel === null) return false;
  if (pageAccess && pageKey in pageAccess) return pageAccess[pageKey];
  // Fallback when server has not sent pageAccess (older build)
  if (pageKey === 'characters' || pageKey === 'leaderboards' || pageKey === 'patch-notes') return true;
  if (pageKey === 'patch-notes-admin') return userLevel >= DEFAULT_RESTRICTED_PAGE_MIN_LEVEL;
  return userLevel >= DEFAULT_RESTRICTED_PAGE_MIN_LEVEL;
};
