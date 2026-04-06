import { Book } from 'lucide-react'
import { PropertyMetadata } from '../../types'
import { GetButton, SetButton, CopyIcon } from '../common/ActionButtons'
import TypePill from '../common/TypePill'

interface PropertyListItemProps {
  prop: PropertyMetadata
  copiedText: string | null
  copyToClipboard: (text: string, id: string) => void
  navigateToEnum: (name: string) => void
}

export default function PropertyListItem({ prop, copiedText, copyToClipboard, navigateToEnum }: PropertyListItemProps) {
  const fullType = `Property${prop.type}`
  const propKey = `${prop.type}-${prop.id}`
  const getCmd = `/getproperty ${fullType}.${prop.name}`
  const setCmd = `/setproperty ${fullType}.${prop.name} {value}`
  const getBtnId = `get-${propKey}`
  const setBtnId = `set-${propKey}`
  const idBtnId = `id-${propKey}`
  const nameBtnId = `name-${propKey}`

  return (
    <div className="grid grid-cols-[160px_80px_1fr_200px] gap-4 px-6 py-4 items-center group hover:bg-white/[0.02] transition-colors border-l-2 border-transparent hover:border-blue-600/30">
      <div className="flex items-center">
        <TypePill type={prop.type} />
      </div>

      <div className="flex items-center gap-2 group/id">
        <span className="text-xs font-mono font-bold text-neutral-500 group-hover/id:text-blue-400 transition-colors">
          {prop.id}
        </span>
        <CopyIcon 
          onClick={() => copyToClipboard(prop.id.toString(), idBtnId)}
          isCopied={copiedText === idBtnId}
        />
      </div>

      <div className="flex items-center gap-2 group/name min-w-0">
        <span className={`text-sm truncate transition-all duration-200 ${copiedText === nameBtnId ? 'text-green-500 font-bold transition-none scale-[1.01]' : 'text-neutral-200 group-hover:text-white'}`}>
          {prop.name}
        </span>
        <CopyIcon 
          onClick={() => copyToClipboard(`${fullType}.${prop.name}`, nameBtnId)}
          isCopied={copiedText === nameBtnId}
        />
      </div>

      <div className="flex items-center justify-end gap-2 transition-opacity">
        <GetButton 
          onClick={() => copyToClipboard(getCmd, getBtnId)}
          isCopied={copiedText === getBtnId}
          title={`Copy GET Command:\n${getCmd}`}
        />
        {prop.linkedEnum ? (
          <button 
            onClick={() => navigateToEnum(prop.linkedEnum!)}
            title={`Open ${prop.linkedEnum} table`}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[10px] font-bold uppercase transition-all bg-purple-600/20 border border-purple-500/30 text-purple-400 hover:bg-purple-600 hover:text-white hover:border-purple-500 shadow-lg shadow-purple-600/10"
          >
            <Book className="w-3.5 h-3.5" />
            OPEN TABLE
          </button>
        ) : (
          <SetButton 
            onClick={() => copyToClipboard(setCmd, setBtnId)}
            isCopied={copiedText === setBtnId}
            title={`Copy command template:\n${setCmd}`}
          />
        )}
      </div>
    </div>
  )
}
