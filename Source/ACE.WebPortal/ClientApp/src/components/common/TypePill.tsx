
interface TypePillProps {
  type: string
  className?: string
}

export default function TypePill({ type, className = '' }: TypePillProps) {
  const getColors = (t: string) => {
    const lowerType = t.toLowerCase();
    
    // Integer types
    if (['int', 'int64', 'uint', 'uint32', 'uint64', 'int32', 'int16', 'uint16', 'byte', 'sbyte', 'short', 'ushort', 'long', 'ulong'].includes(lowerType)) {
      return 'bg-orange-500/10 text-orange-400 border-orange-500/20';
    }
    
    // Boolean types
    if (lowerType === 'bool' || lowerType === 'boolean') {
      return 'bg-green-500/10 text-green-400 border-green-500/20';
    }
    
    // Floating point types
    if (lowerType === 'float' || lowerType === 'double') {
      return 'bg-blue-500/10 text-blue-400 border-blue-500/20';
    }
    
    // String types
    if (lowerType === 'string') {
      return 'bg-purple-500/10 text-purple-400 border-purple-500/20';
    }
    
    // ID/Instance types
    if (['dataid', 'instanceid', 'did', 'guid', 'iid'].includes(lowerType)) {
      return 'bg-cyan-500/20 text-cyan-400 border-cyan-500/40';
    }
    
    return 'bg-neutral-800/50 text-neutral-500 border-neutral-800';
  }

  const formatType = (t: string) => {
    const mapping: Record<string, string> = {
      'sbyte': 'SByte',
      'byte': 'Byte',
      'short': 'Int16',
      'ushort': 'UInt16',
      'int': 'Int32',
      'uint': 'UInt32',
      'long': 'Int64',
      'ulong': 'UInt64'
    };
    return mapping[t.toLowerCase()] || t;
  }

  return (
    <div className={`flex items-center gap-2 px-3 py-1.5 rounded-lg border font-bold text-[10px] tracking-wider transition-all ${getColors(type)} ${className}`}>
      {formatType(type)}
    </div>
  );
}
