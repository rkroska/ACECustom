import { Info, RefreshCcw } from 'lucide-react'
import { ServerParamMetadata } from '../../types'
import { GetButton, SetButton, CopyIcon } from '../common/ActionButtons'

interface ServerParamListItemProps {
  p: ServerParamMetadata
  copiedText: string | null
  copyToClipboard: (text: string, id: string) => void
}

export default function ServerParamListItem({ p, copiedText, copyToClipboard }: ServerParamListItemProps) {
  const getModifyCommand = (param: ServerParamMetadata) => {
    const cmdType = param.type === 'bool' ? 'bool' : 
                    param.type === 'long' ? 'long' : 
                    param.type === 'double' ? 'double' : 'string';
    
    let valueStr = "{value}";
    if (param.type === 'bool') {
        const currentVal = param.isSet ? param.currentValue.toLowerCase() : param.defaultValue.toLowerCase();
        valueStr = currentVal === 'true' ? 'false' : 'true';
    }

    return `/modify${cmdType} ${param.name} ${valueStr}`;
  }

  const getGetCommand = (param: ServerParamMetadata) => {
    const cmdType = param.type === 'bool' ? 'bool' : 
                    param.type === 'long' ? 'long' : 
                    param.type === 'double' ? 'double' : 'string';
    return `/show${cmdType} ${param.name}`;
  }

  const modifyCmd = getModifyCommand(p)
  const modifyBtnId = `modify-${p.name}`
  const getBtnId = `get-${p.name}`
  const nameBtnId = `name-${p.name}`

  return (
    <div className="grid grid-cols-[1fr_120px_2fr_140px_220px] gap-4 px-6 py-5 items-center group hover:bg-white/[0.02] transition-colors border-l-2 border-transparent hover:border-blue-600/30">
      <div className="flex items-center gap-2 min-w-0 pr-4 group/name">
        <span className="text-sm font-bold text-white tracking-tight truncate group-hover:text-blue-400 transition-colors lowercase font-mono" title={p.name}>
          {p.name}
        </span>
        <CopyIcon 
          onClick={() => copyToClipboard(p.name, nameBtnId)}
          isCopied={copiedText === nameBtnId}
          className="opacity-0 group-hover/name:opacity-100"
        />
      </div>

      <div className="flex flex-col gap-1.5 items-start">
        <div className={`px-2 py-0.5 rounded-md border text-[9px] font-bold uppercase tracking-widest ${
          p.type === 'bool' ? 'bg-green-500/10 text-green-400 border-green-500/20' :
          p.type === 'long' ? 'bg-orange-500/10 text-orange-400 border-orange-500/20' :
          p.type === 'double' ? 'bg-blue-500/10 text-blue-400 border-blue-500/20' :
          'bg-purple-500/10 text-purple-400 border-purple-500/20'
        }`}>
          {p.type}
        </div>
        <span className="text-[10px] text-neutral-600 font-bold px-0.5 whitespace-nowrap">Default: {p.defaultValue}</span>
      </div>

      <div className="group/desc">
        <p className="text-[11px] leading-relaxed text-neutral-400 font-medium group-hover/desc:text-neutral-300 transition-colors">
          {p.description}
        </p>
      </div>

      <div className="flex flex-col">
        {p.isSet ? (
          <div className="flex flex-col items-start gap-1">
            <span className="text-xs font-mono font-bold text-blue-400">{p.currentValue}</span>
            <div className="text-[8px] font-bold text-blue-500/60 uppercase tracking-widest flex items-center gap-1">
              <RefreshCcw className="w-2.5 h-2.5" />
              Modified
            </div>
          </div>
        ) : (
          <div className="flex flex-col items-start gap-1">
            <span className="text-xs font-mono font-bold text-neutral-600">{p.defaultValue}</span>
            <div className="text-[8px] font-bold text-neutral-700 uppercase tracking-widest flex items-center gap-1">
              <Info className="w-2.5 h-2.5" />
              Not Set
            </div>
          </div>
        )}
      </div>

      <div className="flex items-center justify-end gap-2">
        <GetButton 
          onClick={() => copyToClipboard(getGetCommand(p), getBtnId)}
          isCopied={copiedText === getBtnId}
          title={`Copy get command:\n${getGetCommand(p)}`}
        />
        <SetButton 
          onClick={() => copyToClipboard(modifyCmd, modifyBtnId)}
          isCopied={copiedText === modifyBtnId}
          title={`Copy update command:\n${modifyCmd}`}
          label="SET"
        />
      </div>
    </div>
  )
}
