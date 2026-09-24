import { Wand2, Shirt, Coins, Infinity as InfinityIcon, Landmark } from 'lucide-react'
import type { PetGuideData } from './petGuideApi'
import { Card, Disabled, Icon, Item, Npc, Section, Steps, Tip } from './ui'
import { duration, num, pct, pyreals } from './format'

const MASTERY_BLURB: Record<string, string> = {
  Primalist: 'Elementals, K\'nath and wisps.',
  Necromancer: 'Skeletons, zombies and spectres.',
  Naturalist: 'Moars, grievvers and Phyntos swarms.',
}

export default function Summoning({ data }: { data: PetGuideData }) {
  const items = data.world.items
  const mastery = data.world.mastery
  const essences = data.world.essences
  const refillDiscounts = data.charms.refillDiscountByTier.filter(d => d > 0)

  return (
    <Section
      id="summoning"
      title="Summoning, masteries and looks"
      icon={<Wand2 className="w-5 h-5" />}
      lead="Your pet is a summoning essence: the Summoning skill items that drop as loot. Your mastery decides which ones you can use, and a captured look decides what your pet looks like."
    >
      <Card title="Choose a mastery" icon={<Landmark className="w-4 h-4 text-violet-300" />} accent="violet">
        <p>
          Each summoning essence belongs to one of three masteries, and you can only summon essences of your own mastery.
          Pick one at the three mastery statues: <Npc data={data} npcKey="masteryStatues" fallback="the mastery statues" />.
        </p>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          {essences.masteries.map(m => (
            <div key={m.mastery} className="bg-neutral-950/60 border border-neutral-800 rounded-xl p-3 space-y-2">
              <div className="text-sm font-black text-white">{m.mastery}</div>
              <div className="text-xs text-neutral-400">{MASTERY_BLURB[m.mastery]}</div>
              <div className="flex flex-wrap gap-1">
                {m.families.map(f => (
                  <span key={f.wcid} title={f.name ?? ''}><Icon item={f} size={28} /></span>
                ))}
              </div>
            </div>
          ))}
        </div>
        <ul className="list-disc pl-5 space-y-1">
          {mastery?.firstChangeMinLevel != null && (
            <li>Your <strong>first choice is free</strong> from level {num(mastery.firstChangeMinLevel)}: use the statue of the mastery you want.</li>
          )}
          {mastery?.paidChangeMinLevel != null && (
            <li>
              Changing again later needs level {num(mastery.paidChangeMinLevel)} and the Path of Light
              {mastery.paidChangeCooldownSeconds != null && <>, and you can only change once every {duration(mastery.paidChangeCooldownSeconds)}</>}.
              Each change costs more than the last:
            </li>
          )}
        </ul>
        {mastery && mastery.paidChanges.length > 0 && (
          <div className="flex flex-wrap gap-2">
            {mastery.paidChanges.map((c, i) => (
              <div key={i} className="bg-neutral-950/60 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs">
                <div className="text-[10px] font-bold uppercase tracking-widest text-neutral-500">Change {i + 1}</div>
                <div className="font-black text-neutral-100">{num(c.mmd)} MMDs</div>
                <div className="text-neutral-400">+ {num(c.luminance)} luminance</div>
              </div>
            ))}
          </div>
        )}
        <p>
          A <Item item={items.masteryCertificate} fallback="Mastery Reset Certificate" /> handed to a statue switches you to that mastery with no other cost.
        </p>
      </Card>

      <Card title="Summoning essence tiers">
        <p>
          Essences come in tiers. Higher tiers are stronger and ask more of you: the highest ones need your Summoning skill specialized. They drop as loot, and a bred baby is the same essence, tier included, as the parent it takes after.
        </p>
        <div className="overflow-x-auto">
          <table className="w-full text-xs">
            <thead>
              <tr className="text-left text-[10px] uppercase tracking-widest text-neutral-500">
                <th className="py-1.5 pr-4">Tier</th>
                <th className="py-1.5 pr-4">Character level</th>
                <th className="py-1.5 pr-4">Summoning skill</th>
                <th className="py-1.5 pr-4">Specialized</th>
                <th className="py-1.5">Summoning augmentations</th>
              </tr>
            </thead>
            <tbody>
              {essences.tierRequirements.map(t => (
                <tr key={t.tier} className="border-t border-neutral-800/70">
                  <td className="py-1.5 pr-4 font-black text-white">{t.tier}</td>
                  <td className="py-1.5 pr-4">{t.level != null ? num(t.level) : '-'}</td>
                  <td className="py-1.5 pr-4">{t.summoningSkill != null ? num(t.summoningSkill) : '-'}</td>
                  <td className="py-1.5 pr-4">{t.summoningSpecialized ? <strong className="text-amber-300">Required</strong> : '-'}</td>
                  <td className="py-1.5">{t.summonAugs ? num(t.summonAugs) : '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <Card title="Put a captured look on your pet" icon={<Shirt className="w-4 h-4 text-rose-300" />} accent="rose">
        <Steps
          steps={[
            <>Leave combat mode, then use your <Item item={items.siphonedEssence} fallback="Siphoned Essence" /> or <Item item={items.hollowEssence} fallback="Hollow Essence" /> on a summoning essence.</>,
            <>The captured essence is used up. The summoning essence takes the creature's look and name{data.features.captureDamageType ? ', and the creature\'s damage type if it was carrying a weapon' : ''}.</>,
            <>Summon your pet again to see its new look.</>,
          ]}
        />
        <p><strong>Your pet's stats do not change.</strong> A look is cosmetic: however fearsome the creature, your pet keeps exactly the same stats.</p>
        {data.features.bond && (
          <p>
            A combat essence with a captured look becomes <strong>bonded to you</strong>: it can no longer be traded, and it starts building
            bond. This is how every pet starts its journey, so capture a look you like.
          </p>
        )}
        <Tip>You can change the look later by applying another captured essence. Your bond, potency and stats stay with the essence.</Tip>
      </Card>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Card title="Summon Essence Refill Charm" icon={<Coins className="w-4 h-4 text-amber-300" />} accent="amber">
          <Disabled when={!data.features.refillCharm} what="The Refill Charm" />
          <div className="flex items-center gap-2"><Icon item={items.refillCharm} /><strong className="text-white">{items.refillCharm?.name ?? 'Summon Essence Refill Charm'}</strong></div>
          <p>
            While the charm is active, summoning an essence that is out of charges restores one charge for{' '}
            <strong>{pyreals(data.charms.refillCostPerCharge, data)}</strong> instead of needing an Encapsulated Spirit.
            {refillDiscounts.length > 0 && <> Higher-tier charms take up to {pct(Math.max(...refillDiscounts))} off.</>}
          </p>
          <p>
            <strong>How to get it:</strong> <Npc data={data} npcKey="elmer" fallback="Elmer" /> wants the Guardian of Spirit's soul fragment.
            Ask <Npc data={data} npcKey="gunther" fallback="Gunther" place="near Tou-Tou" /> where it is, and see{' '}
            <Npc data={data} npcKey="tamantha" fallback="Tamantha the Tamer" /> first: its guards can only be hurt by bonking.
          </p>
        </Card>

        <Card title="Universal Summoning Mastery Charm" icon={<InfinityIcon className="w-4 h-4 text-emerald-300" />} accent="emerald">
          <Disabled when={!data.features.universalCharm} what="The Universal Summoning Mastery Charm" />
          <div className="flex items-center gap-2"><Icon item={items.universalCharm} /><strong className="text-white">{items.universalCharm?.name ?? 'Universal Summoning Mastery Charm'}</strong></div>
          <p>
            While the charm is active you can summon essences of <strong>any</strong> mastery. Your own mastery does not change, and
            each essence's level, skill and augmentation requirements still apply.
          </p>
          <p>
            <strong>How to get it:</strong> <Npc data={data} npcKey="bao" fallback="Bao" /> mastered all three. Collect a Charred Bone
            from the Graveyard, a Glimmering Essence from the Crystalline Crag and a Frozen Herb from the Frozen North, combine them,
            and bring him the result.
          </p>
        </Card>
      </div>
      <p className="text-xs text-neutral-500">Charms are switched on and off by using them, and are not used up.</p>
    </Section>
  )
}
