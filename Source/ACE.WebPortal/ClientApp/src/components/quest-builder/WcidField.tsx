type Props = {
  label: string
  wcid: number
  displayName?: string | null
  onWcidChange: (wcid: number) => void
  onSearch?: () => void
  searchLabel?: string
  hint?: string
  /** When true, WCID is read-only (allocated ID). */
  idOnly?: boolean
}

export default function WcidField({
  label,
  wcid,
  displayName,
  onWcidChange,
  onSearch,
  searchLabel = 'Search…',
  hint,
  idOnly = false,
}: Props) {
  const friendly = displayName?.trim() || null

  return (
    <label className="text-[10px] text-neutral-500 block">
      {label}
      {friendly && (
        <p className="text-sm text-white font-medium mt-0.5 truncate" title={friendly}>
          {friendly}
        </p>
      )}
      <div className="flex gap-1 mt-0.5">
        <input
          type="number"
          value={wcid || ''}
          readOnly={idOnly}
          onChange={(e) => onWcidChange(Number(e.target.value))}
          className={`flex-1 bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-xs font-mono ${
            friendly ? 'text-neutral-500' : 'text-white'
          }`}
          placeholder="WCID"
          title="Weenie class ID (new quest content uses the next free ID in the toolbar)"
        />
        {onSearch && (
          <button
            type="button"
            onClick={onSearch}
            className="shrink-0 px-3 py-1 rounded bg-blue-600/80 hover:bg-blue-500 text-[10px] text-white font-medium"
          >
            {searchLabel}
          </button>
        )}
      </div>
      {hint && <p className="text-[10px] text-neutral-600 mt-1 leading-snug">{hint}</p>}
    </label>
  )
}
