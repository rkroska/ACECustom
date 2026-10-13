import { Fragment } from 'react'
import { Link } from 'react-router-dom'
import { ChevronDown, ChevronRight } from 'lucide-react'
import type { LedgerNpcHandinRow, LedgerNpcReceiverRow, LedgerNpcRow } from '../../types/pyrealLedger'
import { accountPath, characterPath, formatDateTime, useLedgerFetch } from './ledgerUtils'
import {
  EmptyState,
  ErrorState,
  LINK_CLASS,
  LoadingState,
  Money,
  Panel,
  ROW_CLASS,
  ROW_CLICK_CLASS,
  TABLE_CLASS,
  TBODY_CLASS,
  TD_CLASS,
  TD_NUM_CLASS,
  THEAD_CLASS,
  TH_CLASS,
  TH_NUM_CLASS,
  TableScroll,
} from './LedgerShared'
import { ContextLine, CutControl, WhatIfCell, useContextData, useCut } from './WhatIf'

const LIMIT = 200

function sharePct(part: number, total: number): number {
  return total > 0 ? Math.round((part / total) * 100) : 0
}

export default function NpcsTab({
  days,
  openWcid,
  onToggle,
}: {
  days: number
  openWcid: number | null
  onToggle: (wcid: number | null) => void
}) {
  const { data, loading, error } = useLedgerFetch<LedgerNpcRow[]>(`/npcs?days=${days}&limit=${LIMIT}`)
  const rows = data ?? []
  const ctx = useContextData(days).data
  const [cut, setCut] = useCut()
  const openMissing = openWcid !== null && !loading && !error && !rows.some(r => r.npcWcid === openWcid)

  return (
    <Panel
      title="Quest NPC payouts"
      help="NPCs ranked by the coins, trade notes and pyreals their quest rewards handed out. The ledger records where currency came from; it does not judge whether a reward is too generous. A reward that pays far more than the others, or one character collecting most of it, is worth a look."
    >
      <div className="px-4 pt-3 space-y-3">
        <ContextLine ctx={ctx} />
        <CutControl cut={cut} onChange={setCut} />
      </div>
      {loading ? (
        <LoadingState />
      ) : error ? (
        <ErrorState message={error} />
      ) : rows.length === 0 && !openMissing ? (
        <EmptyState message="No NPC handed out currency in this period." />
      ) : (
        <TableScroll>
          {rows.length > 0 && (
            <table className={TABLE_CLASS}>
              <thead className={THEAD_CLASS}>
                <tr className="border-b border-neutral-800">
                  <th className={TH_CLASS}>NPC</th>
                  <th className={TH_CLASS}>WCID</th>
                  <th className={TH_NUM_CLASS} title="Currency items at face value plus pyreals credited straight to the bank">Total paid</th>
                  <th className={TH_NUM_CLASS} title="Face value of coins, trade notes and peas handed out">As currency items</th>
                  <th className={TH_NUM_CLASS} title="Pyreals the NPC credited straight to the bank">Straight to bank</th>
                  <th className={TH_NUM_CLASS} title="How many times the NPC handed something out">Gives</th>
                  <th className={TH_NUM_CLASS}>Characters</th>
                  <th className={TH_NUM_CLASS}>Accounts</th>
                  <th className={TH_NUM_CLASS} title="Average paid per give">Per give</th>
                  <th className={TH_CLASS} title="The character who received the most from this NPC, and their share of the total">Top receiver</th>
                  <th className={TH_CLASS} title="The most this NPC paid out in a single hour">Busiest hour</th>
                  <th className={TH_NUM_CLASS} title="Average paid per week over the period">Per week</th>
                  <th className={TH_NUM_CLASS} title="Share of everything created on the server">Share of all creation</th>
                  <th className={TH_NUM_CLASS} title="Items handed in to this NPC (units)">Handed in</th>
                  <th className={TH_CLASS}>What if</th>
                </tr>
              </thead>
              <tbody className={TBODY_CLASS}>
                {rows.map(row => {
                  const open = openWcid === row.npcWcid
                  const lopsided = row.topReceiverSharePct >= 50
                  const hotHour = row.busiestHourValue >= 1000000000
                  return (
                    <Fragment key={row.npcWcid}>
                      <tr className={`${ROW_CLICK_CLASS} ${open ? 'bg-neutral-800/40' : ''}`} onClick={() => onToggle(open ? null : row.npcWcid)}>
                        <td className={`${TD_CLASS} whitespace-nowrap`}>
                          <span className="inline-flex items-center gap-1.5 text-neutral-100 font-medium">
                            {open ? <ChevronDown className="w-3.5 h-3.5" /> : <ChevronRight className="w-3.5 h-3.5 text-neutral-500" />}
                            {row.npcName || `WCID ${row.npcWcid}`}
                          </span>
                        </td>
                        <td className={`${TD_CLASS} font-mono text-neutral-500`}>{row.npcWcid}</td>
                        <td className={`${TD_NUM_CLASS} font-semibold`}>
                          <Money value={row.total} />
                        </td>
                        <td className={TD_NUM_CLASS}>
                          <span title={`${row.units.toLocaleString()} items`}>
                            <Money value={row.currencyValue} className="text-neutral-300" />
                          </span>
                        </td>
                        <td className={TD_NUM_CLASS}>
                          <Money value={row.bankIn} className="text-neutral-300" />
                        </td>
                        <td className={TD_NUM_CLASS}>{row.gives.toLocaleString()}</td>
                        <td className={TD_NUM_CLASS}>{row.characters.toLocaleString()}</td>
                        <td className={TD_NUM_CLASS}>{row.accounts.toLocaleString()}</td>
                        <td className={TD_NUM_CLASS}>
                          <Money value={row.perGive} />
                        </td>
                        <td className={`${TD_CLASS} whitespace-nowrap`}>
                          {row.topReceiver && row.topReceiverCharId ? (
                            <span className="inline-flex items-center gap-2">
                              <Link to={characterPath(row.topReceiverCharId, days)} className={LINK_CLASS} onClick={e => e.stopPropagation()}>
                                {row.topReceiver}
                              </Link>
                              <span
                                className={`px-1.5 py-0.5 rounded-md border text-[10px] font-bold tabular-nums ${
                                  lopsided ? 'bg-amber-500/15 text-amber-300 border-amber-500/30' : 'bg-neutral-800 text-neutral-400 border-neutral-700'
                                }`}
                                title={`${row.topReceiverValue.toLocaleString()} of ${row.total.toLocaleString()} paid`}
                              >
                                {row.topReceiverSharePct.toLocaleString(undefined, { maximumFractionDigits: 1 })}%
                              </span>
                            </span>
                          ) : (
                            <span className="text-neutral-600">-</span>
                          )}
                        </td>
                        <td className={`${TD_CLASS} whitespace-nowrap`}>
                          {row.busiestHourValue > 0 ? (
                            <span title={`${row.busiestHourValue.toLocaleString()} pyreals`}>
                              <Money value={row.busiestHourValue} className={hotHour ? 'text-amber-300 font-semibold' : ''} />
                              <div className="text-[11px] text-neutral-500">
                                {row.busiestHourGives.toLocaleString()} gives by {row.busiestHourChar ?? 'unknown'} at {formatDateTime(row.busiestHourUtc)}
                              </div>
                            </span>
                          ) : (
                            <span className="text-neutral-600">-</span>
                          )}
                        </td>
                        <td className={TD_NUM_CLASS}>
                          <Money value={row.perWeek} />
                        </td>
                        <td className={`${TD_NUM_CLASS} text-neutral-400`}>
                          {row.shareOfCreationPct.toLocaleString(undefined, { maximumFractionDigits: 1 })}%
                        </td>
                        <td className={TD_NUM_CLASS} title={`${row.handedInUnits.toLocaleString()} items`}>
                          {row.handedInUnits > 0 ? row.handedInUnits.toLocaleString() : '-'}
                        </td>
                        <td className={`${TD_CLASS} whitespace-nowrap`}>
                          <WhatIfCell perWeek={row.perWeek} cut={cut} ctx={ctx} />
                        </td>
                      </tr>
                      {open && (
                        <tr>
                          <td colSpan={15} className="bg-neutral-950/60 px-3 py-3">
                            <NpcReceivers wcid={row.npcWcid} total={row.total} days={days} />
                            <NpcHandins wcid={row.npcWcid} days={days} />
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  )
                })}
              </tbody>
            </table>
          )}
          {openMissing && openWcid !== null && (
            <div className="border-t border-neutral-800 px-3 py-3">
              <div className="text-xs text-neutral-400 mb-2">
                NPC WCID {openWcid} is not in the top {LIMIT} for this period. Who it paid:
              </div>
              <NpcReceivers wcid={openWcid} total={null} days={days} />
              <NpcHandins wcid={openWcid} days={days} />
            </div>
          )}
        </TableScroll>
      )}
    </Panel>
  )
}

function NpcReceivers({ wcid, total, days }: { wcid: number; total: number | null; days: number }) {
  const { data, loading, error } = useLedgerFetch<LedgerNpcReceiverRow[]>(`/npcs/${wcid}?days=${days}`)
  const rows = data ?? []

  if (loading) return <LoadingState />
  if (error) return <ErrorState message={error} />
  if (rows.length === 0) return <EmptyState message="Nobody received currency from this NPC in this period." />

  const sum = total ?? rows.reduce((s, r) => s + r.total, 0)

  return (
    <div className="rounded-xl border border-neutral-800 overflow-hidden">
      <div className="px-3 py-2 text-[10px] font-bold text-neutral-500 uppercase tracking-widest border-b border-neutral-800">
        Who this NPC paid
      </div>
      <TableScroll>
        <table className={TABLE_CLASS}>
          <thead className={THEAD_CLASS}>
            <tr className="border-b border-neutral-800">
              <th className={TH_CLASS}>Character</th>
              <th className={TH_CLASS}>Account</th>
              <th className={TH_NUM_CLASS}>Total</th>
              <th className={TH_NUM_CLASS}>Share</th>
              <th className={TH_NUM_CLASS}>As currency items</th>
              <th className={TH_NUM_CLASS}>Units</th>
              <th className={TH_NUM_CLASS}>Straight to bank</th>
              <th className={TH_NUM_CLASS}>Gives</th>
              <th className={TH_CLASS}>First seen</th>
              <th className={TH_CLASS}>Last seen</th>
            </tr>
          </thead>
          <tbody className={TBODY_CLASS}>
            {rows.map(r => (
              <tr key={r.charId} className={ROW_CLASS}>
                <td className={`${TD_CLASS} whitespace-nowrap`}>
                  <Link to={characterPath(r.charId, days)} className={LINK_CLASS}>
                    {r.charName || `#${r.charId}`}
                  </Link>
                </td>
                <td className={`${TD_CLASS} whitespace-nowrap`}>
                  <Link to={accountPath(r.accountId, days)} className={LINK_CLASS}>
                    {r.accountName || `#${r.accountId}`}
                  </Link>
                </td>
                <td className={`${TD_NUM_CLASS} font-semibold`}>
                  <Money value={r.total} />
                </td>
                <td className={`${TD_NUM_CLASS} text-neutral-400`}>{sharePct(r.total, sum)}%</td>
                <td className={TD_NUM_CLASS}>
                  <Money value={r.currencyValue} className="text-neutral-300" />
                </td>
                <td className={`${TD_NUM_CLASS} text-neutral-400`}>{r.units.toLocaleString()}</td>
                <td className={TD_NUM_CLASS}>
                  <Money value={r.bankIn} className="text-neutral-300" />
                </td>
                <td className={TD_NUM_CLASS}>{r.gives.toLocaleString()}</td>
                <td className={`${TD_CLASS} whitespace-nowrap text-neutral-400`}>{formatDateTime(r.firstHourUtc)}</td>
                <td className={`${TD_CLASS} whitespace-nowrap text-neutral-400`}>{formatDateTime(r.lastHourUtc)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </TableScroll>
    </div>
  )
}

function NpcHandins({ wcid, days }: { wcid: number; days: number }) {
  const { data, loading, error } = useLedgerFetch<LedgerNpcHandinRow[]>(`/npcs/${wcid}/handins?days=${days}`)
  const rows = data ?? []

  return (
    <div className="mt-3 rounded-xl border border-neutral-800 overflow-hidden">
      <div className="px-3 py-2 text-[10px] font-bold text-neutral-500 uppercase tracking-widest border-b border-neutral-800">
        Items handed in
      </div>
      {loading ? (
        <LoadingState />
      ) : error ? (
        <ErrorState message={error} />
      ) : rows.length === 0 ? (
        <EmptyState message="No hand-ins recorded yet (recording began with this build)." />
      ) : (
        <TableScroll>
          <table className={TABLE_CLASS}>
            <thead className={THEAD_CLASS}>
              <tr className="border-b border-neutral-800">
                <th className={TH_CLASS}>Item</th>
                <th className={TH_NUM_CLASS}>Units</th>
                <th className={TH_NUM_CLASS}>Gives</th>
                <th className={TH_NUM_CLASS}>Characters</th>
                <th className={TH_CLASS}>Top giver</th>
              </tr>
            </thead>
            <tbody className={TBODY_CLASS}>
              {rows.map(r => (
                <tr key={r.itemWcid} className={ROW_CLASS}>
                  <td className={`${TD_CLASS} whitespace-nowrap`}>
                    {r.itemName || 'Unknown item'} <span className="font-mono text-neutral-500">{r.itemWcid}</span>
                  </td>
                  <td className={TD_NUM_CLASS}>{r.units.toLocaleString()}</td>
                  <td className={TD_NUM_CLASS}>{r.gives.toLocaleString()}</td>
                  <td className={TD_NUM_CLASS}>{r.characters.toLocaleString()}</td>
                  <td className={`${TD_CLASS} whitespace-nowrap`}>
                    {r.topGiver ? (
                      <>
                        {r.topGiver} <span className="text-neutral-500">({r.topGiverUnits.toLocaleString()})</span>
                      </>
                    ) : (
                      <span className="text-neutral-600">-</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableScroll>
      )}
    </div>
  )
}
