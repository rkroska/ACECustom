import type { PetGuideData, Shop } from './petGuideApi'

// ---- formatting -------------------------------------------------------------------------------
// Only presentation lives here; every value passed in comes from the server.

/** 0.05 -> "5%", 0.025 -> "2.5%". */
export const pct = (fraction: number) => `${+(fraction * 100).toFixed(2)}%`

export const num = (n: number) => n.toLocaleString('en-US')

/** A 0..1 chance as "about 1 in N". */
export const oneIn = (chance: number) => (chance > 0 ? `about 1 in ${num(Math.round(1 / chance))}` : 'never')

/** Seconds as the largest sensible unit: "20 hours", "27 days". */
export function duration(seconds: number | null | undefined): string {
  if (seconds == null) return 'a while'
  const plural = (n: number, unit: string) => `${+n.toFixed(1)} ${unit}${n === 1 ? '' : 's'}`
  if (seconds >= 86400 * 2) return plural(seconds / 86400, 'day')
  if (seconds >= 3600) return plural(seconds / 3600, 'hour')
  if (seconds >= 60) return plural(seconds / 60, 'minute')
  return plural(seconds, 'second')
}

export const hours = (h: number) => duration(h * 3600)

/** Pyreals, with the MMD equivalent when the server's MMD note has a value. */
export function pyreals(amount: number, data: PetGuideData): string {
  const mmd = data.world.items.mmd?.value
  const base = `${num(amount)} pyreals`
  if (!mmd || amount < mmd) return base
  return `${base} (${+(amount / mmd).toFixed(2)} MMD)`
}

export const itemName = (data: PetGuideData, key: string, fallback: string) => data.world.items[key]?.name ?? fallback

export const npcName = (data: PetGuideData, key: string, fallback: string) => data.world.npcs[key]?.[0]?.name ?? fallback

/** A shop price in the currency that vendor actually charges: pyreals, MMD notes or another item. */
export function shopPrice(price: number, shop: Shop, data: PetGuideData): string {
  const currency = shop.currency
  if (!currency) return pyreals(price, data)
  if (currency.wcid === data.world.items.mmd?.wcid) return `${num(price)} MMD${price === 1 ? '' : 's'}`
  return `${num(price)} ${currency.name ?? 'tokens'}`
}
