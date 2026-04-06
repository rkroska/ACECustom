import { InventoryItem } from '../types'

export function getIconUrl(item: Partial<InventoryItem>): string {
  if (!item.iconId) return '';
  const params = new URLSearchParams();
  if (item.iconUnderlayId) params.append('underlay', item.iconUnderlayId.toString());
  if (item.iconOverlayId) params.append('overlay', item.iconOverlayId.toString());
  if (item.iconOverlaySecondaryId) params.append('overlaySecondary', item.iconOverlaySecondaryId.toString());
  if (item.uiEffects) params.append('uiEffects', item.uiEffects.toString());
  const qs = params.toString();
  return `/api/icon/${item.iconId}${qs ? '?' + qs : ''}`;
}

export function getIconBgClass(item: Partial<InventoryItem>): string {
  // If the item has a physical image underlay, the backend already rendered it. No UI tint needed.
  if (item.iconUnderlayId && item.iconUnderlayId !== 0) return 'bg-transparent';

  const type = item.itemType || 0;
  const WEAPON = 0x00000001 | 0x00000100 | 0x00008000; // MeleeWeapon | MissileWeapon | Caster
  const ARMOR = 0x00000002 | 0x00000004; // Armor | Clothing

  if (type & WEAPON) return 'bg-red-900/30 border border-red-800/40 shadow-inner rounded-sm';
  if (type & ARMOR) return 'bg-blue-900/30 border border-blue-800/40 shadow-inner rounded-sm';
  
  return 'bg-neutral-900/40 border border-neutral-800/40 shadow-inner rounded-sm';
}
