/** Portal uses HashRouter — public links must include #/… */
export function patchNotesPublicUrl(baseUrl: string, slug?: string): string {
  const base = baseUrl.replace(/#.*$/, '').replace(/\/$/, '')
  const hashPath = slug ? `/patch-notes/${slug}` : '/patch-notes'
  return `${base}#${hashPath}`
}

/** Fix bookmarks/MOTD links that used path-only URLs before the hash fix. */
export function normalizePatchNotesBrowserUrl(): void {
  const { pathname, hash, origin, search } = window.location
  if (!pathname.startsWith('/patch-notes'))
    return

  const hashPath = hash.startsWith('#/patch-notes')
    ? hash
    : `#${pathname}`

  const target = `${origin}${search}${hashPath}`
  if (window.location.href !== target)
    window.location.replace(target)
}
