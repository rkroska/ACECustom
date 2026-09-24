import type { ReactNode } from 'react'
import { ArrowRight, Compass, Sparkles } from 'lucide-react'
import type { PetGuideData } from './petGuideApi'
import { Card, Cmd, Icon, Item, Npc, Section, Steps, Tip } from './ui'
import { itemName, npcName, num, shopPrice } from './format'

export type GuideSectionId = 'start' | 'capture' | 'registry' | 'summoning' | 'bond' | 'breeding'

export default function StartHere({ data, go }: { data: PetGuideData; go: (id: GuideSectionId) => void }) {
  const items = data.world.items
  const schneebsShop = data.world.shops.schneebs
  const flawedFromSchneebs = schneebsShop?.stock.find(s => s.wcid === items.flawedLens?.wcid)

  const journey: { id: GuideSectionId; title: string; text: string }[] = [
    { id: 'capture', title: 'Capture a creature', text: `Weaken a monster and use a Siphon Lens on it to pull out a ${itemName(data, 'siphonedEssence', 'Siphoned Essence')}.` },
    { id: 'registry', title: 'Register it', text: `Hand it to ${npcName(data, 'profRuggan', 'Prof. Ruggan')} to add the species to your Pet Log. He gives the look back as a Hollow Essence.` },
    { id: 'summoning', title: 'Give your summon that look', text: 'Use the captured essence on a summoning essence. Your pet now looks like the creature, and the essence becomes bonded to you.' },
    { id: 'bond', title: 'Fight together', text: 'Your bonded pet earns bond as it fights and drops Savage Echo, which trains its potency (damage).' },
    { id: 'breeding', title: 'Breed', text: 'Take a strong, well-bonded pair to the Seedy Motel to breed babies that can inherit the best of both parents.' },
  ]

  return (
    <div className="space-y-8">
      <Section
        id="start"
        title="Start here"
        icon={<Compass className="w-5 h-5" />}
        lead="The pet system lets you capture the look of almost any creature, put it on your combat summons, bond with them, make them stronger, and breed them. It has several steps, and each one unlocks the next. This page walks you through them in order."
      >
        <Card title="Three different things are called 'essence'" icon={<Sparkles className="w-4 h-4 text-violet-300" />} accent="violet">
          <p>Most confusion comes from this. Keep these three apart and the rest falls into place:</p>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3 pt-1">
            <EssenceKind
              item={items.siphonedEssence}
              fallback="Siphoned Essence"
              title="A captured look"
              text={<>What a Siphon Lens gives you. It holds only the creature's <strong>appearance</strong>, not its strength. Register it with Prof. Ruggan, then put the look on a summon. After registering it comes back as a <Item item={items.hollowEssence} fallback="Hollow Essence" />, which works the same way.</>}
            />
            <EssenceKind
              item={data.world.essences.masteries[0]?.families[0] ?? null}
              fallback="Summoning Essence"
              title="Your pet"
              text={<>A summoning essence (the retail kind: skeletons, wisps, Phyntos wasps and so on). It holds the pet's <strong>stats</strong>, charges, bond, potency and breeding traits. This is the thing you summon.</>}
            />
            <EssenceKind
              item={items.savageEcho}
              fallback="Savage Echo"
              title="Pet currency"
              text={<>Dropped when your bonded pet helps with kills, or salvaged from spare captured essences. You spend it to raise your pet's <strong>potency</strong>.</>}
            />
          </div>
        </Card>

        <Card title="The journey">
          <div className="grid grid-cols-1 md:grid-cols-5 gap-3">
            {journey.map((step, i) => (
              <button
                key={step.id}
                onClick={() => go(step.id)}
                className="text-left bg-neutral-950/60 hover:bg-neutral-800/60 border border-neutral-800 hover:border-violet-500/40 rounded-xl p-3 transition-all group cursor-pointer"
              >
                <div className="text-[10px] font-black uppercase tracking-widest text-violet-300">Step {i + 1}</div>
                <div className="text-sm font-black text-white flex items-center gap-1">
                  {step.title}
                  <ArrowRight className="w-3.5 h-3.5 opacity-0 group-hover:opacity-100 transition-opacity" />
                </div>
                <div className="text-xs text-neutral-400 mt-1 leading-relaxed">{step.text}</div>
              </button>
            ))}
          </div>
        </Card>

        <Card title="Your first lenses: help Mrs. Ruggan" accent="emerald">
          <p>New to pets? This short errand gets you everything you need to start capturing.</p>
          <Steps
            steps={[
              <>Talk to <Npc data={data} npcKey="mrsRuggan" fallback="Mrs. Ruggan" place="in Lin" />. Her husband forgot to pick up a lens order, and she gives you a <Item item={items.sealedOrder} fallback="Sealed Order" />.</>,
              <>Take it up the hill to <Npc data={data} npcKey="schneebs" fallback="Schneaky Schneebs" place="near Lin" />. You get a <Item item={items.sealedLensOrder} fallback="Sealed Lense Order" /> in return.</>,
              <>Bring it back to Mrs. Ruggan. She pays you with {data.world.starterLensCount != null ? <strong>{num(data.world.starterLensCount)}</strong> : 'a stack of'} <Item item={items.flawedLens} fallback="Flawed Siphon Lens" /> and a <Item item={items.monsterDex} fallback="Monster-Dex" />, a book that lists every creature you have registered.</>,
              <>Find <Npc data={data} npcKey="profRuggan" fallback="Prof. Ruggan" place="in Lin" /> at his camp. He registers your captures, and his camp is home to most of the people in this guide.</>,
            ]}
          />
          {schneebsShop && flawedFromSchneebs && (
            <Tip>Out of lenses? Schneebs also sells {flawedFromSchneebs.name ?? 'Flawed Siphon Lenses'} for {shopPrice(flawedFromSchneebs.price, schneebsShop, data)} each.</Tip>
          )}
        </Card>

        <Card title="Handy commands">
          <ul className="space-y-1.5">
            <li><Cmd>@pets</Cmd> shows your Pet Log (every species you have registered). <Cmd>@shinies</Cmd> shows your shiny ones.</li>
            <li><Cmd>@top pets</Cmd>, <Cmd>@top shinies</Cmd>, <Cmd>@top bond</Cmd>, <Cmd>@top potency</Cmd>, <Cmd>@top mutations</Cmd> and <Cmd>@top litters</Cmd> show the pet leaderboards.</li>
            <li><Cmd>@pet-name &lt;name&gt;</Cmd> asks staff to approve a custom name for your summoned combat pet.</li>
            <li><Cmd>@dance</Cmd> performs the courtship dance, and <Cmd>@breed-debug</Cmd> tells you why a breeding attempt is not working.</li>
          </ul>
        </Card>
      </Section>
    </div>
  )
}

function EssenceKind({ item, fallback, title, text }: { item: { icon: string | null; name: string | null } | null; fallback: string; title: string; text: ReactNode }) {
  return (
    <div className="bg-neutral-950/60 border border-neutral-800 rounded-xl p-3 space-y-2">
      <div className="flex items-center gap-2">
        <Icon item={item} />
        <div>
          <div className="text-[10px] font-black uppercase tracking-widest text-violet-300">{title}</div>
          <div className="text-sm font-black text-white">{item?.name ?? fallback}</div>
        </div>
      </div>
      <p className="text-xs text-neutral-400 leading-relaxed">{text}</p>
    </div>
  )
}
