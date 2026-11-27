/** Shapes returned by the kill rates endpoint (/api/audit/kill-rates). JSON is camelCase; times are unix seconds (UTC). */

/** How a kill was made (the server's KillStyle): the killer's stance at the kill, and for magic their last cast's school. */
export type KillStyle = 'Melee' | 'Missile' | 'War' | 'Void' | 'Pet' | 'Other'

export interface KillRateHour {
  /** Unix seconds the clock hour starts at. */
  hourStart: number
  /** The character's own tier that hour; 0 = unknown. */
  ownTier: number
  /** Own-tier kills that hour. */
  own: number
  /** All T11+ kills that hour. */
  total: number
  /** Kills per tier ("15": 730), only tiers with kills. */
  byTier: Record<string, number>
  /** Kills per style ("Missile": 700), all tiers, only styles with kills. */
  byStyle: Partial<Record<KillStyle, number>>
  first: number
  last: number
  /** The hour still running. */
  current: boolean
  /** Not a full hour of play (running, or first kill after :05 / last before :55). */
  part: boolean
  /** Counts toward the average: full hour, own-tier kills at least 95 pct. */
  clean: boolean
  /** Off-tier kills worth showing next to the own-tier count ("188 (389)"). */
  showsTotal: boolean
  /** Day rows only (7d / 30d): clean hours that UTC day; null on hour rows. */
  cleanHours: number | null
  /** Day rows only: average own-tier kills over that day's clean hours; null on hour rows or with none. */
  avgOwnPerHour: number | null
}

export interface KillRateCharacter {
  guid: number
  name: string
  /** Own tier now (newest known); 0 = unknown. */
  ownTier: number
  /** Average own-tier kills over the clean hours; null when there are none. */
  avgOwnPerHour: number | null
  cleanHours: number
  /** The average rests on at least minSteadyHours clean hours; shorter ones rank after the steady ones. */
  steady: boolean
  /** Logged in right now. */
  online: boolean
  ownKills: number
  totalKills: number
  ownShare: number
  /** The style with the most kills over the window; null with no kills. */
  mainStyle: KillStyle | null
  /** Kills per style over the window, all tiers, only styles with kills. */
  byStyle: Partial<Record<KillStyle, number>>
  /** Oldest first; only hours with kills. */
  hours: KillRateHour[]
}

export interface KillRateReport {
  generatedAt: number
  hours: number
  /** The daily window asked for (7, 30); 0 = hourly. */
  days: number
  /** The width of one column: 3600 (clock hours, from memory) or 86400 (UTC days, from the saved history). */
  bucketSeconds: number
  /** Days of saved history kept (the server setting kill_rate_history_days); 0 = everything. */
  historyDays: number
  hoursKept: number
  partHourSlackMinutes: number
  cleanOwnShare: number
  /** Clean hours an average needs to count as steady. */
  minSteadyHours: number
  /** Every clock hour of the window, oldest first. */
  hourStarts: number[]
  characters: KillRateCharacter[]
}
