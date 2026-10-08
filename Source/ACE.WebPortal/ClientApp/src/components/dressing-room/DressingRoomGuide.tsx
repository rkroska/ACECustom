import { useMemo, useState } from 'react'
import { Activity, AlertTriangle, Ban, Coins, Eye, Layers, MapPin, Shirt, Sparkles, Terminal, Trash2, Users } from 'lucide-react'
import { cn } from '../../utils/cn'
import { Card, Cmd, Disabled, Section, Stat, Steps, Tip, Where } from '../pet-guide/ui'
import { num } from '../pet-guide/format'
import { useDressingRoomGuide, type Body, type DressingRoomGuideData } from './dressingRoomApi'

const ordinal = (n: number) => {
  const words = ['first', 'second', 'third', 'fourth', 'fifth', 'sixth', 'seventh', 'eighth', 'ninth', 'tenth', 'eleventh', 'twelfth']
  return words[n] ?? `${n + 1}th`
}

/** Public player guide to the Dressing Room (#/dressing-room). All numbers come from /api/dressing-room-guide. */
export default function DressingRoomGuide() {
  const { data, error } = useDressingRoomGuide()

  return (
    <div className="absolute inset-0 bg-neutral-950 text-neutral-200 overflow-y-auto p-4 md:p-8 font-sans selection:bg-violet-500/30">
      <div className="absolute top-[-10%] left-[-10%] w-[50%] h-[50%] rounded-full bg-violet-500/10 blur-[120px] pointer-events-none" />
      <div className="absolute bottom-[-10%] right-[-10%] w-[50%] h-[50%] rounded-full bg-rose-500/10 blur-[120px] pointer-events-none" />

      <div className="max-w-6xl mx-auto space-y-8 relative z-10">
        <div className="flex items-center gap-4 border-b border-neutral-800/80 pb-5">
          <div className="w-12 h-12 rounded-xl bg-gradient-to-tr from-violet-600 to-rose-500 flex items-center justify-center shadow-lg shadow-violet-500/20">
            <Shirt className="w-6 h-6 text-white" />
          </div>
          <div>
            <h1 className="text-2xl md:text-3xl font-extrabold tracking-tight text-white">Dressing Room</h1>
            <p className="text-xs md:text-sm text-neutral-400 font-medium">Lock in how your character looks, whatever armour you wear afterwards</p>
          </div>
        </div>

        {error && (
          <div className="flex items-center gap-2 text-sm text-rose-300 bg-rose-500/10 border border-rose-500/30 rounded-xl px-4 py-3">
            <AlertTriangle className="w-4 h-4 shrink-0" />
            The guide could not load the server's Dressing Room settings ({error}). Try again in a moment.
          </div>
        )}

        {!data && !error && (
          <div className="flex flex-col items-center justify-center py-24 gap-4 text-neutral-500">
            <Activity className="w-8 h-8 animate-spin opacity-50" />
            <div className="text-[10px] font-black uppercase tracking-[0.2em]">Loading the guide</div>
          </div>
        )}

        {data && <Guide data={data} />}
      </div>
    </div>
  )
}

function Guide({ data }: { data: DressingRoomGuideData }) {
  const attendantName = data.attendants.find(a => a.name)?.name ?? 'a dressing room attendant'
  const firstFee = data.fee.schedule[0]

  return (
    <>
      <Disabled when={!data.enabled} what="The Dressing Room" />

      <Section
        id="how"
        title="How it works"
        icon={<Sparkles className="w-5 h-5" />}
        lead="A look is a copy of how the armour and clothing you are wearing appears, colours and dyes included. Once it is locked in, that is how your character is drawn for you and for everyone else, no matter what you really have equipped. Your real gear still gives all of its armour, spells and bonuses."
      >
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <Card title="Locking in a look" icon={<Shirt className="w-4 h-4 text-violet-300" />} accent="violet">
            <Steps steps={[
              <>Put on the armour and clothing you want to look like.</>,
              <>Use {attendantName}. He lists each piece and its fee in chat.</>,
              <>Answer <strong className="text-neutral-100">Yes</strong> to the confirmation box. If you say No, or wait too long, nothing happens.</>,
              <>The fee is paid and the look is saved. You keep everything you were wearing.</>,
            ]} />
          </Card>

          <Card title="What it costs you" icon={<Coins className="w-4 h-4 text-amber-300" />} accent="amber">
            <p><strong className="text-neutral-100">Only pyreals.</strong> The attendant copies how each piece looks. Your gear is not taken, changed or used up.</p>
            <p>You pay a fee for every piece{firstFee > 0 ? <>, starting at <strong className="text-neutral-100">{num(firstFee)} pyreals</strong></> : null}. It is taken from your banked pyreals first, then from pyreal coins in your pack.</p>
            <p>To change your look, wear something different and lock that in.</p>
          </Card>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <Card title="Part of a look" icon={<Shirt className="w-4 h-4 text-violet-300" />}>
            <p>Every piece of armour and clothing you are wearing when you say Yes, including your cloak and anything under your armour.</p>
            <p className="text-neutral-400">Slots a look can hold: {data.slots.join(', ')}.</p>
          </Card>
          <Card title="Not part of a look" icon={<Ban className="w-4 h-4 text-emerald-300" />}>
            <p>Weapons, shields, wands and bows, jewellery and trinkets. They always show as what you really hold and wear.</p>
          </Card>
        </div>

        <Tip>You are charged for every piece that goes into the look, including ones you cannot see, such as a shirt under a breastplate. Take off anything you do not want to pay for before you ask.</Tip>
      </Section>

      <Section
        id="cost"
        title="The fee"
        icon={<Coins className="w-5 h-5" />}
        lead="Each piece is priced by the part of the body it covers. The first time a body area is locked it costs the base fee; every later lock of that same area on that character costs more. A piece covering several areas is priced by whichever of them you have locked the most."
      >
        <FeeTable data={data} />
        <Tip>Removing your look does not reset the price. The count of how many times each body area has been locked stays with the character for good.</Tip>
      </Section>

      <Section
        id="rules"
        title="How a look is drawn"
        icon={<Layers className="w-5 h-5" />}
      >
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <Card title="Saved pieces always show" icon={<Eye className="w-4 h-4 text-violet-300" />}>
            <p>A saved piece is drawn on the part of the body it covers whether you have something equipped there or nothing at all.</p>
            <p>The Show Helm and Show Cloak character options still work on a saved helm or cloak.</p>
          </Card>
          <Card title="Real gear under a saved piece is hidden" icon={<Layers className="w-4 h-4 text-violet-300" />}>
            <p>A real piece that covers any of the same body area as a saved piece is hidden <strong className="text-neutral-100">whole</strong>. Pieces you could normally wear together, such as a shirt with trousers or underclothes with armour, do not hide each other.</p>
            <p className="text-neutral-400">Example: with only a breastplate saved, a real hauberk is hidden completely, so your arms show whatever is under it.</p>
          </Card>
          <Card title="Everywhere else shows your real gear" icon={<Shirt className="w-4 h-4 text-violet-300" />}>
            <p>Parts of the body your look does not cover are drawn from what you really wear. A look of just a helm leaves the rest of you as normal.</p>
          </Card>
          <Card title="A new piece replaces what it overlaps" icon={<Trash2 className="w-4 h-4 text-violet-300" />}>
            <p>Locking in a new piece removes every saved piece that covers any of the same body area, completely. The attendant tells you which saved pieces will be replaced before you confirm.</p>
            <p className="text-neutral-400">Example: locking a breastplate over a saved hauberk removes the whole hauberk, sleeves included.</p>
          </Card>
        </div>
      </Section>

      <Section id="refused" title="When the attendant says no" icon={<Ban className="w-5 h-5" />}
        lead="Nothing is ever taken from you except the fee, and the fee is only taken when you say Yes.">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          <Card title="Pieces your body cannot show">Left out of the look and not charged. The attendant names them. See the table below.</Card>
          <Card title="Not enough pyreals">The attendant tells you the total and what you have.</Card>
          <Card title="Busy or trading">Finish what you are doing and ask again.</Card>
        </div>
      </Section>

      <Section id="commands" title="Commands" icon={<Terminal className="w-5 h-5" />}>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          <Card><Cmd>/look</Cmd> lists your saved pieces and whether the look is showing.</Card>
          <Card><Cmd>/look hide</Cmd> shows your real gear. Free, and the look stays saved.</Card>
          <Card><Cmd>/look show</Cmd> brings the saved look back. Free.</Card>
          <Card accent="rose"><Cmd>/look clear</Cmd> removes the saved look for good. The fee is not refunded, and it does not lower your next fee.</Card>
        </div>
      </Section>

      <Section id="where" title="Where to go" icon={<MapPin className="w-5 h-5" />}>
        <Card>
          {data.attendants.length === 0 && <p>No dressing room attendant has been placed in the world yet.</p>}
          {data.attendants.map(a => (
            <p key={a.wcid} className="flex items-center gap-2 flex-wrap">
              <strong className="text-neutral-100">{a.name ?? 'Dressing room attendant'}</strong>
              {a.spots.length > 0 ? <Where npcs={[a]} place="inside a dungeon" /> : <span className="text-neutral-400">not placed in the world yet</span>}
            </p>
          ))}
        </Card>
      </Section>

      <Section
        id="bodies"
        title="What each body can show"
        icon={<Users className="w-5 h-5" />}
        lead="Not every body has a model for every kind of gear. The table counts, for each part of the body, how many of the pieces a human can wear there also show on that heritage. The attendant leaves out a piece that would not show and does not charge for it."
      >
        <BodyTable bodies={data.bodies} />
      </Section>
    </>
  )
}

function FeeTable({ data }: { data: DressingRoomGuideData }) {
  const { schedule, scheduleComplete, cap } = data.fee
  const [pieces, setPieces] = useState(9)
  const [prior, setPrior] = useState(0)
  const last = schedule.length - 1
  // the dropdown only offers listed rows, so this never prices a lock the server did not list
  const perPiece = schedule[Math.min(prior, last)]
  // only claim the last row covers later locks when the server says the fee can no longer rise
  const lastCoversLater = scheduleComplete && schedule.length > 1

  if (schedule[0] <= 0)
    return <Card accent="emerald">Locking in a look is currently free. You keep the pieces.</Card>

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
      <Card title="Fee per piece" icon={<Coins className="w-4 h-4 text-amber-300" />} accent="amber">
        <table className="w-full text-[13px] tabular-nums">
          <thead>
            <tr className="text-[10px] uppercase tracking-widest text-neutral-500 text-left">
              <th className="py-1 font-bold">Lock of that body area</th>
              <th className="py-1 font-bold text-right">Pyreals</th>
            </tr>
          </thead>
          <tbody>
            {schedule.map((fee, i) => (
              <tr key={i} className="border-t border-neutral-800/70">
                <td className="py-1.5 capitalize">{ordinal(i)}{i === last && lastCoversLater ? ' and every one after' : ''}</td>
                <td className="py-1.5 text-right font-bold text-neutral-100">{num(fee)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {cap > 0 && schedule[last] >= cap && <p className="text-neutral-400">One piece never costs more than {num(cap)} pyreals.</p>}
        {!scheduleComplete && <p className="text-neutral-400">The fee keeps rising after the {ordinal(last)} lock. The attendant tells you the exact fee before you confirm.</p>}
      </Card>

      <Card title="Work out an outfit" icon={<Shirt className="w-4 h-4 text-violet-300" />} accent="violet">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <label className="space-y-1 block">
            <span className="text-[10px] font-bold uppercase tracking-widest text-neutral-500">Pieces in the outfit</span>
            <input
              id="dr-pieces"
              type="number"
              min={1}
              max={data.slots.length}
              value={pieces}
              onChange={e => setPieces(Math.max(1, Math.min(data.slots.length, Math.round(Number(e.target.value) || 1))))}
              className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-3 py-2 text-sm font-bold text-neutral-100"
            />
          </label>
          <label className="space-y-1 block">
            <span className="text-[10px] font-bold uppercase tracking-widest text-neutral-500">For these pieces this is your</span>
            <select
              id="dr-prior"
              value={prior}
              onChange={e => setPrior(Number(e.target.value))}
              className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-3 py-2 text-sm font-bold text-neutral-100 capitalize"
            >
              {schedule.map((_, i) => <option key={i} value={i}>{ordinal(i)} look{i === last && lastCoversLater ? ' or later' : ''}</option>)}
            </select>
          </label>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Stat label="Per piece" value={`${num(perPiece)}`} />
          <Stat label="Whole outfit" value={`${num(perPiece * pieces)} pyreals`} />
        </div>
        <p className="text-neutral-400">This assumes every piece in the outfit covers areas locked the same number of times. A mixed outfit is the sum of each piece's own fee, which the attendant lists before you confirm.</p>
      </Card>
    </div>
  )
}

function BodyTable({ bodies }: { bodies: Body[] }) {
  // Bodies with the same numbers are one row: most heritages share a model.
  const rows = useMemo(() => {
    const grouped = new Map<string, { names: string[]; body: Body }>()
    for (const body of bodies) {
      const key = body.areas.map(a => `${a.shown}/${a.total}`).join('|')
      const name = `${body.heritage} ${body.gender.toLowerCase()}`
      const row = grouped.get(key)
      if (row) row.names.push(name)
      else grouped.set(key, { names: [name], body })
    }
    return Array.from(grouped.values())
  }, [bodies])

  if (rows.length === 0)
    return <Card>The server did not send any body data.</Card>

  const areas = rows[0].body.areas

  return (
    <div className="bg-neutral-900/70 border border-neutral-800 rounded-2xl p-4 overflow-x-auto">
      <table className="w-full text-[12px] tabular-nums min-w-[720px]">
        <thead>
          <tr className="text-[10px] uppercase tracking-widest text-neutral-500 text-left">
            <th className="py-1 pr-3 font-bold">Body</th>
            {areas.map(a => <th key={a.key} className="py-1 px-1 font-bold text-center">{a.label}</th>)}
          </tr>
        </thead>
        <tbody>
          {rows.map(({ names, body }) => (
            <tr key={names.join()} className="border-t border-neutral-800/70 align-top">
              <td className="py-2 pr-3 text-neutral-200 max-w-[260px]">{names.join(', ')}</td>
              {body.areas.map(a => {
                const share = a.total > 0 ? a.shown / a.total : 0
                return (
                  <td key={a.key} className="py-2 px-1 text-center">
                    <span
                      title={`${a.shown} of ${a.total} pieces`}
                      className={cn('inline-block rounded px-1.5 py-0.5 font-bold',
                        a.shown === 0 ? 'bg-rose-500/15 text-rose-300' : a.shown === a.total ? 'bg-emerald-500/10 text-emerald-300' : 'bg-amber-500/10 text-amber-200')}
                    >
                      {a.shown === 0 ? 'None' : a.shown === a.total ? 'All' : `${Math.round(share * 100)}%`}
                    </span>
                  </td>
                )
              })}
            </tr>
          ))}
        </tbody>
      </table>
      <p className="text-[11px] text-neutral-500 pt-3">All = every counted piece changes that part of this body. None = no counted piece changes that part; a piece that also covers another part can still show there and be accepted. A percentage means some pieces change that part and some do not.</p>
    </div>
  )
}
