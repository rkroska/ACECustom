/** Match rank for search — mirrors server WeenieSearchOrdering. */

function indexOfIgnoreCase(text: string, q: string): number {
  if (!text || !q) return -1
  return text.toLowerCase().indexOf(q.toLowerCase())
}

function isWordChar(c: string): boolean {
  return /[a-zA-Z0-9_'-]/.test(c)
}

function earliestWordStartingWith(text: string, q: string): number {
  if (!text || !q) return -1
  const lower = text.toLowerCase()
  const qLower = q.toLowerCase()
  let best = -1
  for (let i = 0; i < text.length; i++) {
    if (i > 0 && isWordChar(text[i - 1]!)) continue
    let end = i
    while (end < text.length && isWordChar(text[end]!)) end++
    if (end > i && end - i >= q.length && lower.slice(i, i + q.length) === qLower) {
      if (best < 0 || i < best) best = i
    }
    i = end
  }
  return best
}

export function getMatchScore(
  query: string,
  name: string,
  className: string
): { rank: number; position: number } {
  const q = query.trim()
  if (!q) return { rank: 6, position: Number.MAX_SAFE_INTEGER }

  const n = name ?? ''
  const c = className ?? ''
  const lowerN = n.toLowerCase()
  const lowerC = c.toLowerCase()
  const qLower = q.toLowerCase()

  if (lowerN.startsWith(qLower)) return { rank: 0, position: 0 }
  if (lowerC.startsWith(qLower)) return { rank: 1, position: 0 }

  const wordName = earliestWordStartingWith(n, q)
  if (wordName >= 0) return { rank: 2, position: wordName }

  const wordClass = earliestWordStartingWith(c, q)
  if (wordClass >= 0) return { rank: 3, position: wordClass }

  const idxN = indexOfIgnoreCase(n, q)
  if (idxN >= 0) return { rank: 4, position: idxN }

  const idxC = indexOfIgnoreCase(c, q)
  if (idxC >= 0) return { rank: 5, position: idxC }

  return { rank: 6, position: Number.MAX_SAFE_INTEGER }
}

export function sortBySearchRelevance<T>(
  items: T[],
  query: string,
  getName: (item: T) => string,
  getClassName: (item: T) => string,
  getWcid: (item: T) => number
): T[] {
  const q = query.trim()
  if (!q) return items

  const exactWcid = /^\d+$/.test(q) ? Number(q) : null

  return [...items].sort((a, b) => {
    if (exactWcid != null) {
      const aExact = getWcid(a) === exactWcid ? -1 : 0
      const bExact = getWcid(b) === exactWcid ? -1 : 0
      if (aExact !== bExact) return aExact - bExact
    }
    const sa = getMatchScore(q, getName(a), getClassName(a))
    const sb = getMatchScore(q, getName(b), getClassName(b))
    if (sa.rank !== sb.rank) return sa.rank - sb.rank
    if (sa.position !== sb.position) return sa.position - sb.position
    const nameCmp = getName(a).localeCompare(getName(b), undefined, { sensitivity: 'base' })
    if (nameCmp !== 0) return nameCmp
    return getWcid(a) - getWcid(b)
  })
}
