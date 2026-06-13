export function isPublicPatchNotesPath(pathname: string): boolean {
  if (pathname === '/patch-notes') return true
  if (!pathname.startsWith('/patch-notes/')) return false
  return !pathname.startsWith('/patch-notes/manage')
}
