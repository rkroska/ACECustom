import { Info } from 'lucide-react'
import { ServerEventMetadata } from '../../types'
import { GetButton, SetButton, CopyIcon } from '../common/ActionButtons'

interface EventListItemProps {
  e: ServerEventMetadata
  copiedText: string | null
  copyToClipboard: (text: string, id: string) => void
}

export default function EventListItem({ e, copiedText, copyToClipboard }: EventListItemProps) {
  const nameBtnId = `name-${e.name}`
  const statusBtnId = `status-${e.name}`
  const startBtnId = `start-${e.name}`
  const stopBtnId = `stop-${e.name}`

  const stateBadge = (() => {
    if (e.isDisabled) return { label: 'Disabled', className: 'bg-neutral-500/10 text-neutral-400 border-neutral-500/20' }
    if (e.isActive) return { label: 'Active', className: 'bg-green-500/10 text-green-400 border-green-500/20' }
    if (e.state === 'On') return { label: 'On', className: 'bg-green-500/10 text-green-400 border-green-500/20' }
    if (e.state === 'Off') return { label: 'Inactive', className: 'bg-amber-500/10 text-amber-400 border-amber-500/20' }
    if (e.state === 'Enabled') return { label: 'Enabled', className: 'bg-blue-500/10 text-blue-400 border-blue-500/20' }
    return { label: e.state, className: 'bg-neutral-500/10 text-neutral-400 border-neutral-500/20' }
  })()

  return (
    <div className="grid grid-cols-[1fr_140px_120px_1fr_220px] gap-4 px-6 py-5 items-center group hover:bg-white/[0.02] transition-colors border-l-2 border-transparent hover:border-blue-600/30">
      <div className="flex items-center gap-2 min-w-0 pr-4 group/name">
        <span
          className="text-sm font-bold text-white tracking-tight truncate group-hover:text-blue-400 transition-colors font-mono"
          title={e.name}
        >
          {e.name}
        </span>
        <CopyIcon
          onClick={() => copyToClipboard(e.name, nameBtnId)}
          isCopied={copiedText === nameBtnId}
          className="opacity-0 group-hover/name:opacity-100"
        />
      </div>

      <div className="flex flex-col gap-1.5 items-start">
        <div className={`px-2 py-0.5 rounded-md border text-[9px] font-bold uppercase tracking-widest ${stateBadge.className}`}>
          {stateBadge.label}
        </div>
        <span className="text-[10px] text-neutral-600 font-bold px-0.5 whitespace-nowrap">
          State: {e.state}
        </span>
      </div>

      <div className="flex flex-col gap-1">
        {e.isActive ? (
          <div className="text-[8px] font-bold text-green-500/80 uppercase tracking-widest">Running</div>
        ) : (
          <div className="text-[8px] font-bold text-neutral-600 uppercase tracking-widest">Not running</div>
        )}
        {e.isScheduled && (
          <div className="text-[8px] font-bold text-blue-500/60 uppercase tracking-widest flex items-center gap-1">
            <Info className="w-2.5 h-2.5" />
            Scheduled
          </div>
        )}
      </div>

      <div className="text-[11px] leading-relaxed text-neutral-400 font-medium">
        {e.isVirtual ? (
          <span>Virtual event — driven by <span className="text-neutral-300 font-mono">pk_server</span>, not <span className="font-mono">/event</span>.</span>
        ) : e.isDisabled ? (
          <span>Disabled in world DB — cannot start until enabled.</span>
        ) : e.isActive ? (
          <span>Generators and emotes treating this event as started.</span>
        ) : (
          <span>Registered but not currently active. Copy START to run in-game.</span>
        )}
      </div>

      <div className="flex items-center justify-end gap-2">
        <GetButton
          onClick={() => copyToClipboard(e.statusCommand, statusBtnId)}
          isCopied={copiedText === statusBtnId}
          title={`Copy status command:\n${e.statusCommand}`}
        />
        {e.canStart && (
          <SetButton
            onClick={() => copyToClipboard(e.startCommand, startBtnId)}
            isCopied={copiedText === startBtnId}
            title={`Copy start command:\n${e.startCommand}`}
            label="START"
          />
        )}
        {e.canStop && !e.isVirtual && (
          <SetButton
            onClick={() => copyToClipboard(e.stopCommand, stopBtnId)}
            isCopied={copiedText === stopBtnId}
            title={`Copy stop command:\n${e.stopCommand}`}
            label="STOP"
          />
        )}
        {e.isVirtual && (
          <SetButton
            onClick={() => copyToClipboard(e.isActive ? e.stopCommand : e.startCommand, startBtnId)}
            isCopied={copiedText === startBtnId}
            title={e.isActive ? `Copy PK off:\n${e.stopCommand}` : `Copy PK on:\n${e.startCommand}`}
            label={e.isActive ? 'PK OFF' : 'PK ON'}
          />
        )}
      </div>
    </div>
  )
}
