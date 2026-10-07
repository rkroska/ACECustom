import { useEffect, useState } from 'react'
import type { NpcSpot } from '../pet-guide/petGuideApi'

/**
 * Shape of /api/dressing-room-guide (DressingRoomGuideController). Every number the guide shows comes from
 * here: the page itself carries no fees or counts, so it always matches the live server.
 */

export interface Attendant {
  wcid: number
  name: string | null
  spots: NpcSpot[]
}

export interface BodyArea {
  key: string
  label: string
  /** Pieces covering this area that draw on this body. */
  shown: number
  /** Pieces covering this area that draw on a human. */
  total: number
}

export interface Body {
  heritage: string
  gender: string
  areas: BodyArea[]
}

export interface DressingRoomGuideData {
  /** The server's master switch. When false nobody's look is drawn and attendants refuse. */
  enabled: boolean
  fee: {
    baseFee: number
    /** The most one piece can cost; 0 means no cap. */
    cap: number
    /** Fee for a piece whose most-locked slot has been locked 0, 1, 2... times before. */
    schedule: number[]
    /** True when the last entry is what every later lock costs. False when the list just stops and later locks cost more. */
    scheduleComplete: boolean
  }
  /** The wear slots a look can hold, named as the attendant names them. */
  slots: string[]
  attendants: Attendant[]
  bodies: Body[]
}

function isGuideData(json: unknown): json is DressingRoomGuideData {
  const d = json as DressingRoomGuideData
  return !!d && typeof d.enabled === 'boolean' && !!d.fee && Array.isArray(d.fee.schedule) && d.fee.schedule.length > 0
    && typeof d.fee.scheduleComplete === 'boolean'
    && Array.isArray(d.slots) && Array.isArray(d.attendants) && Array.isArray(d.bodies)
}

export function useDressingRoomGuide() {
  const [data, setData] = useState<DressingRoomGuideData | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    fetch('/api/dressing-room-guide')
      .then(res => {
        if (!res.ok) throw new Error(`The server answered ${res.status}.`)
        return res.json()
      })
      .then(json => {
        if (!isGuideData(json)) throw new Error('The server sent incomplete guide data. It may need a restart after an update.')
        if (!cancelled) setData(json)
      })
      .catch(e => { if (!cancelled) setError(e instanceof Error ? e.message : String(e)) })
    return () => { cancelled = true }
  }, [])

  return { data, error }
}
