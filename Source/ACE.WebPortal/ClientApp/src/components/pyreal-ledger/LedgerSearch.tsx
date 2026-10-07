import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Search, User, Users, X } from 'lucide-react'
import type { LedgerSearchRow } from '../../types/pyrealLedger'
import { accountPath, characterPath, useLedgerFetch } from './ledgerUtils'
import { Money } from './LedgerShared'

/** Character / account search with a results dropdown linking to the detail pages. */
export default function LedgerSearch({ days }: { days: number }) {
  const [text, setText] = useState('')
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const boxRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const t = setTimeout(() => setQuery(text.trim()), 300)
    return () => clearTimeout(t)
  }, [text])

  useEffect(() => {
    const onDown = (e: MouseEvent) => {
      if (boxRef.current && !boxRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onDown)
    return () => document.removeEventListener('mousedown', onDown)
  }, [])

  const { data, loading, error } = useLedgerFetch<LedgerSearchRow[]>(
    query.length >= 2 ? `/search?q=${encodeURIComponent(query)}` : null
  )
  const rows = data ?? []

  // Distinct accounts among the matches, so an account can be opened directly.
  const accounts = new Map<number, string>()
  for (const r of rows) if (r.accountId && !accounts.has(r.accountId)) accounts.set(r.accountId, r.accountName || `#${r.accountId}`)

  const close = () => setOpen(false)

  return (
    <div ref={boxRef} className="relative w-full sm:w-80">
      <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-neutral-500 pointer-events-none" />
      <input
        type="text"
        value={text}
        onChange={e => {
          setText(e.target.value)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        onKeyDown={e => {
          if (e.key === 'Escape') close()
        }}
        placeholder="Find a character or account..."
        className="w-full pl-9 pr-8 py-2 rounded-lg bg-neutral-950 border border-neutral-800 text-sm text-white placeholder:text-neutral-600 focus:outline-none focus:border-blue-500/50"
      />
      {text && (
        <button
          type="button"
          onClick={() => {
            setText('')
            setQuery('')
          }}
          className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-neutral-500 hover:text-white"
          aria-label="Clear search"
        >
          <X className="w-3.5 h-3.5" />
        </button>
      )}
      {open && text.trim().length > 0 && (
        <div className="absolute z-30 mt-1 w-full max-h-96 overflow-auto rounded-xl border border-neutral-800 bg-neutral-950 shadow-2xl">
          {text.trim().length < 2 ? (
            <div className="px-3 py-2 text-xs text-neutral-500">Type at least 2 characters.</div>
          ) : loading || query !== text.trim() ? (
            <div className="px-3 py-2 text-xs text-neutral-500">Searching...</div>
          ) : error ? (
            <div className="px-3 py-2 text-xs text-red-400">{error}</div>
          ) : rows.length === 0 ? (
            <div className="px-3 py-2 text-xs text-neutral-500">No character or account matches.</div>
          ) : (
            <>
              {accounts.size > 0 && (
                <div className="py-1 border-b border-neutral-800">
                  <div className="px-3 py-1 text-[10px] font-bold text-neutral-500 uppercase tracking-widest">Accounts</div>
                  {[...accounts.entries()].map(([id, name]) => (
                    <Link
                      key={id}
                      to={accountPath(id, days)}
                      onClick={close}
                      className="flex items-center gap-2 px-3 py-1.5 text-sm text-neutral-200 hover:bg-neutral-800"
                    >
                      <Users className="w-3.5 h-3.5 text-neutral-500" />
                      {name}
                    </Link>
                  ))}
                </div>
              )}
              <div className="py-1">
                <div className="px-3 py-1 text-[10px] font-bold text-neutral-500 uppercase tracking-widest">Characters</div>
                {rows.map(r => (
                  <Link
                    key={r.charId}
                    to={characterPath(r.charId, days)}
                    onClick={close}
                    className="flex items-center gap-2 px-3 py-1.5 text-sm text-neutral-200 hover:bg-neutral-800"
                  >
                    <User className="w-3.5 h-3.5 text-neutral-500 shrink-0" />
                    <span className="truncate">{r.name}</span>
                    <span className="text-[11px] text-neutral-500 truncate">{r.accountName}</span>
                    <span className="ml-auto text-[11px] text-neutral-400">
                      <Money value={r.balance} />
                    </span>
                  </Link>
                ))}
              </div>
            </>
          )}
        </div>
      )}
    </div>
  )
}
