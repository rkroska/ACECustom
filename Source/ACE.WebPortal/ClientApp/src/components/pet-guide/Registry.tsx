import { BookOpen } from 'lucide-react'
import type { PetGuideData } from './petGuideApi'
import { Card, Cmd, Item, Npc, Section, Steps, Tip } from './ui'
import { num } from './format'

export default function Registry({ data }: { data: PetGuideData }) {
  const items = data.world.items
  const milestones = data.registry.milestones.map(num).join(', ')

  return (
    <Section
      id="registry"
      title="The Pet Log and Prof. Ruggan"
      icon={<BookOpen className="w-5 h-5" />}
      lead="Prof. Ruggan catalogues the creatures of Dereth. Every new species you bring him goes into your Pet Log, which is shared by every character on your account."
    >
      <Card title="Registering a capture" accent="violet">
        <Steps
          steps={[
            <>Hand your <Item item={items.siphonedEssence} fallback="Siphoned Essence" /> to <Npc data={data} npcKey="profRuggan" fallback="Prof. Ruggan" place="in Lin" />.</>,
            <>If the species is new to your account, it is added to your Pet Log. A shiny counts as its own entry.</>,
            <>He always hands the look back as a <Item item={items.hollowEssence} fallback="Hollowed Essence" />, even for a species you already had.</>,
          ]}
        />
        <p>
          A Hollow essence keeps the creature's look, so you can still put it on a summon. It can be traded or stored, but it
          cannot be registered again.
        </p>
        <Tip>Always register a capture <strong>before</strong> you use it. Registering costs you nothing, and the Hollow essence you get back does everything the Siphoned one did.</Tip>
      </Card>

      <Card title="Quest bonus rewards">
        <ul className="list-disc pl-5 space-y-1">
          <li>Your first capture of each <strong>kind</strong> of creature (your first drudge, your first banderling and so on).</li>
          <li>Your first <strong>shiny</strong> of each kind.</li>
          <li>Pet Log milestones at {milestones}, then every {num(data.registry.milestoneInterval)} species.</li>
        </ul>
      </Card>

      <Card title="Checking your collection">
        <p>
          <Cmd>@pets</Cmd> lists every species in your Pet Log and <Cmd>@shinies</Cmd> lists your shinies. The{' '}
          <Item item={items.monsterDex} fallback="Monster-Dex" /> from Mrs. Ruggan shows the same collection as a book, and{' '}
          <Cmd>@top pets</Cmd> shows how you rank.
        </p>
      </Card>
    </Section>
  )
}
