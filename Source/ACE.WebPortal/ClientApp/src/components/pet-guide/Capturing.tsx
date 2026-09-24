import { Crosshair, Flame, Gem, RotateCcw, Crown } from 'lucide-react'
import type { PetGuideData } from './petGuideApi'
import { Card, Disabled, Icon, Item, Npc, Section, Steps, Tip } from './ui'
import { duration, num, oneIn, pct } from './format'

export default function Capturing({ data }: { data: PetGuideData }) {
  const c = data.capture
  const items = data.world.items
  const drops = c.lensDrops

  const lensSource: Record<string, string> = {
    flawedLens: `Any creature can drop one (${oneIn(drops.flawedChance)} kills at base).`,
    pristineLens: `Creatures level ${num(drops.pristineMinCreatureLevel)} and up (${oneIn(drops.pristineChance)} kills at full rate, reached ${num(drops.rampLevels)} levels later).`,
    perfectLens: `Creatures level ${num(drops.perfectMinCreatureLevel)} and up (${oneIn(drops.perfectChance)} kills at full rate, reached ${num(drops.rampLevels)} levels later).`,
  }

  return (
    <Section
      id="capture"
      title="Capturing creatures"
      icon={<Crosshair className="w-5 h-5" />}
      lead="A Siphon Lens pulls the essence out of a weakened creature. You keep its look, not its strength, so capture whatever you think looks best."
    >
      <Card title="How to capture" accent="violet">
        <Steps
          steps={[
            <>Weaken the creature to <strong>{pct(c.maxHealthFraction)} health</strong> or under {num(c.maxHealthPoints)} hit points.</>,
            <>Stand within <strong>{num(c.rangeMeters)} meters</strong> of it and use a lens from your pack.</>,
            <>If it works you get a <Item item={items.siphonedEssence} fallback="Siphoned Essence" /> named after the creature, and the creature dies a moment later. If your pack is full, the essence drops at your feet.</>,
          ]}
        />
        <Tip>The lens always targets the <strong>nearest</strong> creature within range, not the one you have selected. Pull your target away from the pack before you use it.</Tip>
      </Card>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {c.lenses.map(lens => (
          <Card key={lens.key} accent="blue">
            <div className="flex items-center gap-3">
              <Icon item={items[lens.key]} size={36} />
              <div>
                <div className="text-sm font-black text-white">{items[lens.key]?.name ?? lens.key}</div>
                <div className="text-xs text-neutral-400">
                  Starts at <strong className="text-neutral-100">{pct(lens.baseChance)}</strong>, up to <strong className="text-neutral-100">{pct(lens.maxChance)}</strong>
                </div>
              </div>
            </div>
            <p className="text-xs text-neutral-400">{lensSource[lens.key]}</p>
          </Card>
        ))}
      </div>

      <Card title="Where lenses come from" icon={<Gem className="w-4 h-4 text-blue-300" />}>
        <Disabled when={!data.features.siphonLensDrops} what="Siphon Lens drops from creatures" />
        <p>
          Lenses drop from creatures you kill, and higher-level creatures drop them a little more often. The lens goes to whoever
          did the most damage (a pet's kills count for its owner). The <strong>Mrs. Ruggan</strong> errand on the Start page gives
          you a starter stack, and Schneebs sells Flawed lenses.
        </p>
      </Card>

      <Card title="What changes your odds">
        <ul className="list-disc pl-5 space-y-1">
          <li><strong>Assess Creature</strong> skill adds up to +{pct(c.assessSkillBonusMax)} (the full bonus at {num(c.assessSkillForMaxBonus)} skill).</li>
          <li><strong>Specialized</strong> Assess Creature adds another +{pct(c.assessSpecializedBonus)}.</li>
          <li>The <strong>weaker</strong> the creature, the better: up to +{pct(c.lowHealthBonusMax)} as its health nears zero.</li>
          <li>Creatures <strong>above your level</strong> are harder: up to -{pct(c.levelPenaltyMax)} when one is {num(c.levelsForMaxPenalty)} or more levels above you.</li>
          <li>Some creatures are tougher or impossible to capture.</li>
        </ul>
        <p className="text-xs text-neutral-500">Every lens has its own ceiling, and no attempt is ever below {pct(c.minChance)}.</p>
      </Card>

      <Card title="When a capture fails" icon={<Flame className="w-4 h-4 text-rose-400" />} accent="rose">
        <p>
          The lens is used up either way. A failed capture <strong>enrages</strong> the creature: it heals to full, hits
          {' '}{c.enrage.damageMultiplier}x as hard and shrugs off {pct(c.enrage.damageReduction)} of the damage it takes. An enraged creature
          cannot be siphoned with an ordinary lens again, so you get <strong>one normal try per creature</strong>.
        </p>
      </Card>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Card title="Second chance: Resonance Lens" icon={<RotateCcw className="w-4 h-4 text-violet-300" />} accent="violet">
          <div className="flex items-center gap-2"><Icon item={items.resonanceLens} /><strong className="text-white">{items.resonanceLens?.name ?? 'Resonance Lens'}</strong></div>
          <p>
            Retries the <strong>last creature you failed to capture</strong>, even though it is enraged. It must still be nearby and
            weakened again. Your chance is the Pristine chance plus {pct(c.resonance.bonus)}, up to {pct(c.resonance.maxChance)}.
          </p>
          <Steps
            steps={[
              <>Climb the jumping path to the <Npc data={data} npcKey="crystallineResonator" fallback="Crystalline Resonator" /> and take a <Item item={items.shimmeringEcho} fallback="Shimmering Echo" /> (once every {duration(data.world.shimmeringEchoCooldownSeconds)}).</>,
              <>Give it to the <Npc data={data} npcKey="echoWeaver" fallback="Echo Weaver" /> for a Resonance Lens (once every {duration(data.world.resonanceLensCooldownSeconds)}).</>,
            ]}
          />
        </Card>

        <Card title="Guaranteed: Asheron's Lens" icon={<Crown className="w-4 h-4 text-amber-300" />} accent="amber">
          <div className="flex items-center gap-2"><Icon item={items.asheronsLens} /><strong className="text-white">{items.asheronsLens?.name ?? "Asheron's Lens"}</strong></div>
          <p>
            Always succeeds, on any creature, even an enraged one or one at full health. It is used up when you use it.
          </p>
          <p>
            Trade <strong>{data.world.asheronsLensFlawedCost != null ? num(data.world.asheronsLensFlawedCost) : 'a mountain of'}</strong>{' '}
            {items.flawedLens?.name ?? 'Flawed Siphon Lens'}es to the <Npc data={data} npcKey="lensCollector" fallback="Arcanum Lens Collector" />: hand him one and he takes the rest from your pack.
          </p>
        </Card>
      </div>

      <Tip>Found a <strong>shiny</strong>? Shiny creatures are rare colour variants and count separately in your Pet Log. Keep a Resonance Lens handy for them.</Tip>
    </Section>
  )
}
