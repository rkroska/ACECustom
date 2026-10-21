import { useRef, useState } from 'react'
import { Copy, Download, X } from 'lucide-react'
import type { EconomyPrompt } from '../../types/economy'
import { ErrorState, LoadingState, Segmented } from '../pyreal-ledger/LedgerShared'
import { useEconomyFetch } from './economyUtils'

/** Modal that fetches a ready-to-paste economy analysis prompt for Claude. */
export default function PromptModal({ currencyKey, days, onClose }: { currencyKey: string | null; days: number; onClose: () => void }) {
  const [scope, setScope] = useState<'one' | 'all'>(currencyKey ? 'one' : 'all')
  const key = scope === 'one' && currencyKey ? currencyKey : 'all'
  const { data, loading, error } = useEconomyFetch<EconomyPrompt>(`/prompt?currency=${encodeURIComponent(key)}&days=${days}`)
  const [notice, setNotice] = useState('')
  const areaRef = useRef<HTMLTextAreaElement>(null)

  const copy = async () => {
    const text = data?.text ?? ''
    try {
      await navigator.clipboard.writeText(text)
      setNotice('Copied to clipboard.')
    } catch {
      areaRef.current?.focus()
      areaRef.current?.select()
      try {
        setNotice(document.execCommand('copy') ? 'Copied to clipboard.' : 'Text selected - press Ctrl+C to copy.')
      } catch {
        setNotice('Text selected - press Ctrl+C to copy.')
      }
    }
  }

  const download = () => {
    const blob = new Blob([data?.text ?? ''], { type: 'text/markdown;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `economy-prompt-${key}-${new Date().toISOString().slice(0, 10)}.md`
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4" onClick={onClose}>
      <div
        className="w-full max-w-4xl max-h-[90vh] flex flex-col rounded-2xl border border-neutral-800 bg-neutral-900 shadow-2xl"
        onClick={e => e.stopPropagation()}
        role="dialog"
        aria-label="Claude prompt"
      >
        <div className="flex flex-wrap items-center gap-3 px-4 py-3 border-b border-neutral-800">
          <h2 className="text-sm font-bold text-white">Claude prompt</h2>
          {currencyKey && (
            <Segmented<'one' | 'all'>
              value={scope}
              onChange={setScope}
              options={[
                { value: 'one', label: 'This currency' },
                { value: 'all', label: 'Whole economy' },
              ]}
            />
          )}
          <button type="button" onClick={onClose} className="ml-auto text-neutral-400 hover:text-white" aria-label="Close">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="p-4 flex-1 min-h-0 flex flex-col gap-3">
          {loading ? (
            <LoadingState />
          ) : error ? (
            <ErrorState message={error} />
          ) : (
            <textarea
              ref={areaRef}
              readOnly
              value={data?.text ?? ''}
              className="w-full flex-1 min-h-[50vh] rounded-lg border border-neutral-800 bg-neutral-950 p-3 font-mono text-xs text-neutral-300 resize-none"
            />
          )}
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              disabled={!data}
              onClick={copy}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-500 disabled:opacity-40 text-white text-xs font-semibold"
            >
              <Copy className="w-3.5 h-3.5" /> Copy to clipboard
            </button>
            <button
              type="button"
              disabled={!data}
              onClick={download}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-neutral-700 hover:bg-neutral-800 disabled:opacity-40 text-neutral-200 text-xs font-semibold"
            >
              <Download className="w-3.5 h-3.5" /> Download .md
            </button>
            {notice && <span className="text-xs text-emerald-400">{notice}</span>}
          </div>
        </div>
      </div>
    </div>
  )
}
