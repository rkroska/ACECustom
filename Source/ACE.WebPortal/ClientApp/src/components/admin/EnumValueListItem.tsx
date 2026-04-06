import { SetButton, CopyIcon } from '../common/ActionButtons'
import { EnumValueMetadata } from '../../types'

interface EnumValueListItemProps {
  v: EnumValueMetadata
  setCmd: string
  enumValueCmd: string
  copiedText: string | null
  copyToClipboard: (text: string, id: string) => void
}

export default function EnumValueListItem({ v, setCmd, enumValueCmd, copiedText, copyToClipboard }: EnumValueListItemProps) {
  const setBtnId = `set-${v.id}`
  const idBtnId = `id-${v.id}`
  const nameBtnId = `name-${v.id}`

  return (
    <div className="group flex items-center justify-between p-3.5 hover:bg-white/[0.02] transition-colors rounded-2xl mx-1 border-l-2 border-transparent hover:border-blue-600/30">
      <div className="flex items-center gap-5 min-w-0">
        <div className="flex items-center gap-2 group/id shrink-0">
          <span className="text-xs font-mono font-bold text-neutral-500 group-hover/id:text-blue-400 transition-colors w-16 text-right">
            {v.hexValue || v.id}
          </span>
          <CopyIcon 
            onClick={() => copyToClipboard(v.hexValue || v.id.toString(), idBtnId)}
            isCopied={copiedText === idBtnId}
            className="opacity-0 group-hover/id:opacity-100"
          />
        </div>
        
        <div className="flex items-center gap-2 group/name min-w-0">
          <span className={`text-sm transition-colors leading-none ${
              copiedText === nameBtnId ? 'text-green-500 font-bold' : 'text-neutral-200 group-hover:text-white'
          }`}>
            {v.name}
          </span>
          <CopyIcon 
            onClick={() => copyToClipboard(enumValueCmd, nameBtnId)}
            isCopied={copiedText === nameBtnId}
            className="opacity-0 group-hover/name:opacity-100"
          />
        </div>
      </div>

      <div className="flex items-center gap-2 shrink-0">
        <SetButton 
          onClick={() => copyToClipboard(setCmd, setBtnId)}
          isCopied={copiedText === setBtnId}
          title={`Copy command template:\n${setCmd}`}
        />
      </div>
    </div>
  )
}
