import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Heart, CheckCircle2, Circle, Baby, Ghost, ShoppingBag, FlaskConical } from 'lucide-react'
import type { PetGuideData } from './petGuideApi'
import { Card, Cmd, Disabled, Icon, Npc, Section, Steps, Tip } from './ui'
import { hours, num, pct, shopPrice } from './format'

export default function Breeding({ data }: { data: PetGuideData }) {
  const b = data.breeding
  const f = data.features
  const m = b.maturity
  const s = b.mutationSteps
  const ivo = data.world.shops.ivo

  const checklist: string[] = [
    'One male and one female combat essence. The ID panel shows which is which.',
    `Both essences are tier ${num(b.minTier)} or higher.`,
    `Both have bond ${num(b.minBond)} or higher.`,
    ...(f.maturity ? ['Both are grown adults, not babies still growing up.'] : []),
    ...(b.shinyCanBreed ? [] : ['Neither is shiny. Shinies cannot breed.']),
    'Neither has been neutered.',
    `The male has a breeding left: he gets ${num(b.maleCharges)} every ${hours(b.maleChargeResetHours)}.`,
    `The female has rested: she needs ${hours(b.femaleRestHours)} after each litter.`,
    'Both owners are in the Seedy Motel with the essences in their packs, and both pets are summoned in the same room.',
    "The female's owner has a free slot in their main pack and can carry a little more.",
    `Both owners dance within ${num(b.danceWindowSeconds)} seconds of each other.`,
  ]

  return (
    <Section
      id="breeding"
      title="Breeding at the Seedy Motel"
      icon={<Heart className="w-5 h-5" />}
      lead="Prof. Ruggan's 'filing annex' is really a motel for pets. Bring a well-bonded pair and they can have a baby that inherits the best of both, with a small chance of something new."
    >
      <Disabled when={!f.breeding} what="Pet breeding" />

      <Card title="Getting there" accent="rose">
        <p>
          Take the <Npc data={data} npcKey="motelPortal" fallback="Portal to Seedy Motel" /> next to Prof. Ruggan.{' '}
          <Npc data={data} npcKey="fenwick" fallback="Fenwick" place="inside the Seedy Motel" /> runs the front desk and explains the house rules.
        </p>
      </Card>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <Card title="Can we breed? Checklist" accent="violet">
          <Checklist entries={checklist} />
          <p className="text-xs text-neutral-500">
            If anything is missing, nothing is used up: fix it and dance again. <Cmd>@breed-debug</Cmd> tells you what is wrong.
          </p>
        </Card>

        <Card title="How it works" accent="violet">
          <Steps
            steps={[
              <>Both owners summon their pets in the same room.</>,
              <>Both owners type <Cmd>*dance*</Cmd> or <Cmd>@dance</Cmd> within {num(b.danceWindowSeconds)} seconds of each other.</>,
              <>The baby essence goes straight into the <strong>female's owner's</strong> pack, every time. Agree on who keeps it before you dance.</>,
            ]}
          />
          {m.imprintOnSummon && <Tip>A baby can be traded until someone summons it. The first character to summon it owns it for good.</Tip>}
        </Card>
      </div>

      <Card title="What the baby inherits" icon={<Baby className="w-4 h-4 text-rose-300" />} accent="rose">
        <ul className="list-disc pl-5 space-y-1">
          <li>It is the same kind of essence, with the same look, as one of its parents.</li>
          <li>Each of its combat ratings, its health bonus and its potency comes from the <strong>stronger</strong> parent {pct(b.betterParentChance)} of the time, and from the other parent otherwise.</li>
          <li>Shiny colouring is never passed on.</li>
        </ul>
      </Card>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <Card title="Mutations" icon={<FlaskConical className="w-4 h-4 text-emerald-300" />} accent="emerald">
          <p>
            Each litter has about a <strong>{pct(b.mutationChance)}</strong> chance of a mutation: a permanent gain of one of these, which
            the baby's own babies can inherit too:
          </p>
          <ul className="list-disc pl-5 space-y-0.5">
            <li>+{num(s.damageRating)} damage rating</li>
            <li>+{num(s.damageResistRating)} damage resistance rating</li>
            <li>+{num(s.critRating)} critical rating</li>
            <li>+{num(s.vitality)} health</li>
          </ul>
          <p>Separately, there is a {pct(b.potencyMutationChance)} chance of a potency mutation worth {num(s.potency)} potency. A mutated baby also gets a new colour.</p>
          <p className="text-xs text-neutral-500">Courtship Incense from Ivo raises the mutation chance for one breeding.</p>
        </Card>

        <Card title="The spirit" icon={<Ghost className="w-4 h-4 text-violet-300" />} accent="violet">
          {f.breedingSpirit ? (
            <>
              <p>
                When a litter mutates, the baby's spirit appears. Only the two <strong>parent pets</strong> can hurt it, and it only
                fights them, never the players. Defeat it within{' '}
                {num(b.spiritSeconds)} seconds and the baby gains a <strong>bonus mutation</strong>.
              </p>
              <p>If time runs out, nothing is lost: the baby is born as normal.</p>
            </>
          ) : (
            <p>The mutation spirit fight is currently turned off, so mutated babies are born straight away.</p>
          )}
        </Card>
      </div>

      {f.maturity && (
        <Card title="Growing up" icon={<Baby className="w-4 h-4 text-amber-300" />} accent="amber">
          <p>
            Babies are born small and weak and cannot breed. They grow by fighting: a kill counts when the creature is at least the
            essence's tier in level and your baby did at least {pct(m.minDamageShare)} of its health. After {num(m.killsRequired)} kills it is an adult.
          </p>
          <div className="overflow-x-auto">
            <table className="w-full text-xs">
              <thead>
                <tr className="text-left text-[10px] uppercase tracking-widest text-neutral-500">
                  <th className="py-1.5 pr-4">Stage</th>
                  <th className="py-1.5 pr-4">Strength</th>
                  <th className="py-1.5">Size</th>
                </tr>
              </thead>
              <tbody>
                {m.stages.map(stage => (
                  <tr key={stage.name} className="border-t border-neutral-800/70">
                    <td className="py-1.5 pr-4 font-black text-white">{stage.name}</td>
                    <td className="py-1.5 pr-4">{pct(stage.strength)}</td>
                    <td className="py-1.5">{pct(stage.size)}</td>
                  </tr>
                ))}
                <tr className="border-t border-neutral-800/70">
                  <td className="py-1.5 pr-4 font-black text-white">Adult</td>
                  <td className="py-1.5 pr-4">{pct(1)}</td>
                  <td className="py-1.5">{pct(1)}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </Card>
      )}

      {ivo && ivo.stock.length > 0 && (
        <Card title="Ivo's shop" icon={<ShoppingBag className="w-4 h-4 text-blue-300" />} accent="blue">
          <p><Npc data={data} npcKey="ivo" fallback="Ivo" place="inside the Seedy Motel" /> sells everything a breeder needs.</p>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
            {ivo.stock.map(entry => (
              <div key={entry.wcid} className="flex gap-3 bg-neutral-950/60 border border-neutral-800 rounded-xl p-3">
                <Icon item={entry} size={32} />
                <div className="min-w-0">
                  <div className="text-sm font-black text-white">{entry.name}</div>
                  <div className="text-xs font-bold text-amber-300">{shopPrice(entry.price, ivo, data)}</div>
                  {entry.description && <div className="text-xs text-neutral-400 mt-1 leading-relaxed">{entry.description}</div>}
                </div>
              </div>
            ))}
          </div>
        </Card>
      )}

      <Card title="Plan your pairings">
        <p>
          The <Link to="/pet-breeding" className="text-rose-300 font-bold hover:underline">Breeding Simulator</Link> lets you try pairings,
          see possible babies and estimate how long it takes to reach a goal.
        </p>
      </Card>
    </Section>
  )
}

function Checklist({ entries }: { entries: string[] }) {
  const [done, setDone] = useState<boolean[]>(() => entries.map(() => false))
  const all = done.every(Boolean)
  return (
    <div className="space-y-1.5">
      {entries.map((entry, i) => (
        <button
          key={i}
          onClick={() => setDone(d => d.map((v, j) => (j === i ? !v : v)))}
          className="w-full flex items-start gap-2 text-left text-[13px] text-neutral-300 hover:text-white cursor-pointer"
        >
          {done[i] ? <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0 mt-0.5" /> : <Circle className="w-4 h-4 text-neutral-600 shrink-0 mt-0.5" />}
          <span className={done[i] ? 'line-through decoration-neutral-600' : ''}>{entry}</span>
        </button>
      ))}
      {all && <div className="text-xs font-black text-emerald-300">All set. Time to dance.</div>}
    </div>
  )
}
