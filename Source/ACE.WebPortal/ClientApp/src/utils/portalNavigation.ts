/**
 * HashRouter navigation helper.
 *
 * We use `HashRouter` in `main.tsx`, so navigation is driven by `location.hash`.
 * This utility normalizes input paths to the `#/path` format expected by React Router.
 */
export function resetPortalHash(path: string) {
  const trimmed = (path ?? '').trim();
  const withoutHash = trimmed.startsWith('#') ? trimmed.slice(1) : trimmed;
  const normalizedPath = withoutHash.startsWith('/') ? withoutHash : `/${withoutHash}`;
  (globalThis as any).location.hash = `#${normalizedPath}`;
}
