import { useEffect, useState } from 'react'
import { HeartHandshake, Zap, Recycle, Calculator } from 'lucide-react'
import type { PetGuideData, PotencyPlan } from './petGuideApi'
import { fetchPotencyPlan } from './petGuideApi'
import { Card, Disabled, Icon, Item, Npc, Section, Stat, Tip } from './ui'
import { num, pct, shopPrice } from './format'

export default function BondPotency({ data }: { data: PetGuideData }) {
  const items = data.world.items
  const p = data.potency
  const f = data.features
  const banderlingShop = data.world.shops.banderling
  const resonatorForSale = banderlingShop?.stock.find(s => s.wcid === items.essenceResonator?.wcid)

  return (
    <Section
      id="bond"
      title="Bond, Savage Echo and potency"
      icon={<HeartHandshake className="w-5 h-5" />}
      lead="Once your pet wears a captured look it is bonded to you. From then on, everything it fights makes it stronger."
    >
      <Card title="Bond" icon={<HeartHandshake className="w-4 h-4 text-rose-300" />} accent="rose">
        <Disabled when={!f.bond} what="Pet bond" />
        <ul className="list-disc pl-5 space-y-1">
          <li>Bond only grows on a <strong>bonded</strong> essence: one you have put a captured look on, or a bred baby after its first summon.</li>
          <li>Your pet earns bond from the kills it helps with, more for the bigger share of the damage it did. Bond levels climb like character levels.</li>
          {data.bond.levelCap > 0 && <li>Bond goes up to level {num(data.bond.levelCap)}.</li>}
          <li>Bond unlocks your pet's potency, and breeding needs at least bond {num(data.breeding.minBond)}.</li>
        </ul>
        <Tip>Check an essence's bond on its ID panel, and compare with others using <code>@top bond</code>.</Tip>
      </Card>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Card title="Savage Echo" icon={<Icon item={items.savageEcho} size={18} />} accent="amber">
          <Disabled when={!f.savageEchoDrops} what="Savage Echo drops" />
          <p>
            When your {p.echoDropRequiresBond ? 'bonded ' : ''}pet helps kill a creature, it may drop <Item item={items.savageEcho} fallback="Savage Echo" />.
            The bigger your pet's share of the damage, the better the chance.
          </p>
          <ul className="list-disc pl-5 space-y-1">
            <li>Each drop is worth about {+p.echoPerDrop.standard.toFixed(2)} echo from most creatures, {+p.echoPerDrop.tier9.toFixed(2)} from tier 9 and {+p.echoPerDrop.tier10.toFixed(2)} from tier 10 creatures.</li>
            <li>Shiny creatures drop {p.echoPerDrop.shinyMultiplier}x as much.</li>
          </ul>
        </Card>

        <Card title="Salvaging essences" icon={<Recycle className="w-4 h-4 text-emerald-300" />} accent="emerald">
          <Disabled when={!f.essenceSalvage} what="Essence salvage" />
          <p>
            Use an <Item item={items.essenceResonator} fallback="Essence Resonator" /> on a spare essence in your pack to turn it into Savage Echo.
            {banderlingShop && resonatorForSale && <> The <Npc data={data} npcKey="banderling" fallback="Bartering Banderling" /> sells one for {shopPrice(resonatorForSale.price, banderlingShop, data)}.</>}
          </p>
          <ul className="list-disc pl-5 space-y-1">
            <li>A captured look (Siphoned or Hollow) gives {num(p.salvage.captured)} echo, {p.salvage.shinyMultiplier}x for a shiny. A few creatures are worth more or less.</li>
            {f.bredEssenceSalvage && <li>A bred pet you no longer want gives {num(p.salvage.bred)} echo. Dismiss it first; the Resonator asks you to confirm if it has mutations or potency.</li>}
            <li>Other summoning essences cannot be salvaged.</li>
          </ul>
          <Tip>Register a capture with Prof. Ruggan first. The Hollow essence he gives back salvages for exactly the same amount.</Tip>
        </Card>
      </div>

      <Card title="Potency" icon={<Zap className="w-4 h-4 text-amber-300" />} accent="amber">
        <Disabled when={!f.potency} what="Pet potency" />
        <ul className="list-disc pl-5 space-y-1">
          <li>Use Savage Echo on your own bonded essence to raise its <strong>stored</strong> potency by one level. Each level costs more echo than the last.</li>
          <li>
            Only part of it is <strong>active</strong>: one active level for every {num(p.bondDivisor)} bond levels
            {p.activeCap > 0 && <>, up to {num(p.activeCap)}</>}. The rest waits, dormant, until your bond catches up.
          </li>
          <li>Each active level adds <strong>{pct(p.damagePerLevel)}</strong> to your pet's damage. Summon your pet again after training to apply it.</li>
          {p.maxStored > 0 && <li>An essence can store up to {num(p.maxStored)} potency.</li>}
          {f.bondStrain && (
            <li>
              <strong>Bond strain:</strong> above {num(p.strain.threshold)} active potency, you lose {p.strain.perLevel} damage rating per extra level while that pet is out
              {p.strain.max > 0 && <> (up to {num(p.strain.max)})</>}.
            </li>
          )}
        </ul>
        <PotencyPlanner data={data} />
      </Card>
    </Section>
  )
}

/** Asks the server what a bond / potency combination gives, so the page never carries its own copy of the formulas. */
function PotencyPlanner({ data }: { data: PetGuideData }) {
  const [bond, setBond] = useState(data.breeding.minBond)
  const [stored, setStored] = useState(0)
  const [target, setTarget] = useState(10)
  const [plan, setPlan] = useState<PotencyPlan | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    const timer = setTimeout(() => {
      fetchPotencyPlan(bond, stored, Math.max(target, stored))
        .then(result => { if (!cancelled) { setPlan(result); setError(null) } })
        .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : String(err)) })
    }, 250)
    return () => { cancelled = true; clearTimeout(timer) }
  }, [bond, stored, target])

  const field = (label: string, value: number, set: (n: number) => void) => (
    <label className="block">
      <span className="text-[10px] font-bold uppercase tracking-widest text-neutral-500">{label}</span>
      <input
        type="number"
        min={0}
        value={value}
        onChange={e => set(Math.max(0, Math.floor(Number(e.target.value) || 0)))}
        className="mt-0.5 w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-sm font-bold text-neutral-100 focus:outline-none focus:border-amber-500"
      />
    </label>
  )

  return (
    <div className="bg-neutral-950/50 border border-neutral-800 rounded-xl p-4 space-y-3 mt-2">
      <div className="text-xs font-black uppercase tracking-wider text-amber-300 flex items-center gap-2">
        <Calculator className="w-4 h-4" /> Potency planner
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        {field('Bond level', bond, setBond)}
        {field('Stored potency now', stored, setStored)}
        {field('Stored potency goal', target, setTarget)}
      </div>
      {error && <div className="text-xs text-rose-300">Could not reach the server: {error}</div>}
      {plan && (
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
          <Stat label="Active now" value={`${num(plan.active)} of ${num(plan.stored)}`} />
          <Stat label="Damage bonus" value={`+${pct(plan.damageBonus)}`} />
          <Stat label="Next level costs" value={`${num(plan.nextLevelCost)} echo`} />
          <Stat label={`Echo to reach ${num(plan.target)}`} value={num(plan.costToTarget)} />
          <Stat label="Most active at this bond" value={num(plan.activeLimitFromBond)} />
          <Stat label="Dormant" value={num(plan.dormant)} />
          {data.features.bondStrain && <Stat label="Bond strain" value={plan.strain > 0 ? `-${num(plan.strain)} damage rating` : 'none'} />}
        </div>
      )}
    </div>
  )
}
