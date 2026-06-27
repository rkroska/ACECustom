/** Common NPC emote motions (MotionCommand hex). Default greet = Ready. */
export const MOTION_PRESETS = [
  { id: '0x41000003', label: 'Ready / greet (default)' },
  { id: '0x1300007d', label: 'Bow (deep)' },
  { id: '0x1300008a', label: 'Salute' },
  { id: '0x13000087', label: 'Wave' },
  { id: '0x1300008e', label: 'Wave (high)' },
  { id: '0x1300008f', label: 'Wave (low)' },
  { id: '0x13000083', label: 'Nod' },
  { id: '0x13000086', label: 'Shrug' },
  { id: '0x13000080', label: 'Laugh' },
  { id: '0x13000089', label: 'Hearty laugh' },
  { id: '0x1300004c', label: 'Cheer' },
  { id: '0x41000012', label: 'Crouch' },
  { id: '0x41000013', label: 'Sitting' },
  { id: '0x40000039', label: 'Magic pray' },
  { id: '0x4000002f', label: 'Magic clap' },
  { id: '0x40000031', label: 'Magic heal gesture' },
] as const

export const DEFAULT_MOTION = MOTION_PRESETS[0].id

export function motionLabel(hex?: string): string {
  if (!hex) return 'Ready / greet'
  const preset = MOTION_PRESETS.find((p) => p.id.toLowerCase() === hex.toLowerCase())
  return preset?.label ?? hex
}
