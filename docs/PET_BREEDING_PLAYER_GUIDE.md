# Pet Breeding - Player Guide

You can breed your combat pets. A litter needs two players, one male and one female pet, and a dance.
The baby inherits from both parents, can mutate into something rarer than either, and has to be
raised before it comes into its own.

---

## Before you start

Look at the ID panel of any combat pet essence. Near the bottom you will see lines like:

```
Sex: Male (9/10 breeding charges today) - refills in 16h 30m
Sex: Female (ready to breed)
Sex: Female (recovering - ready to breed in 3h 12m)
```

- **Sex** is fixed for each essence. It is a coin flip when the essence is created. Two males or two
  females cannot breed.
- **Males** are studs. Each holds ten breeding charges. The whole count refills 24 hours after its
  last refill, not 24 hours after each breed; the ID panel shows when. An essence that has never been
  checked starts its clock the first time you look at it.
- **Females** are dams. After a litter she rests for four hours before she can breed again.
- **The baby always goes to the female's owner.** If you are lending a stud, agree on your cut first.
  Her owner needs a free slot in the main pack and room under the burden limit, or the breed is
  refused before anything is spent.

Some essences cannot breed, and their ID panel says so: shiny essences, neutered essences, and young
that have not grown up yet.

Two more gates are checked at the dance and refused in red text if they fail:

- **Tier 100 or higher.** Pet essences come in tiers (50, 80, 100, 125, 150, 180, 200, 250, 300).
  Tier 50 and 80 essences cannot breed.
- **Bond level 100 or higher on both parents.** Bond grows from kills while the essence is bonded
  to you, and only while the server has pet bonding switched on. An essence that has never bonded
  counts as level 1. A bred baby starts at bond 1, so raising it to breed means growing it to an
  adult *and* bonding it to 100.

---

## The ritual

1. Both players go to the **Seedy Motel**.
2. Each summons their pet. The two pets must stand in the **same room**.
3. Both players perform the **dance emote** (`*dance*`, or `@dance`) within five seconds of each
   other.

You will see "The mating ritual has begun between <pet> and <pet>..." and moments later the birth
message. The baby essence appears in the female owner's pack. Both parents are dismissed afterwards;
resummon them to breed again.

If you dance and see "Your pet performs the courtship dance, waiting for a partner...", the server
could not find a partner: nobody in the room danced within five seconds with a pet standing in your
pet's cell. If the pair was found but the breed was refused, the reason is in the red text.

`@breed-debug` shows whether breeding is switched on, whether you are standing in the right place,
your own pet's sex, charges or recovery time, and how many players with pets are near you. It does
not check every rule, so read the refusal message first.

If the birth happens while the female's owner is logged out or has filled every pack (only possible
during a spirit fight), the baby is not dropped on the ground. It waits in her pack and is there at
her next login.

---

## What the baby inherits

- **Species and look** come from one parent, chosen at random.
- **Stats** come from both, line by line. For each line the baby takes the stronger parent's value
  about 55% of the time and the weaker parent's the rest, so two strong parents make a strong baby
  and the baby is never weaker than the weaker parent on any line.
- **Mutations** travel with the line they belong to. A lineage that has been bred carefully for many
  generations is visibly better than a fresh capture.
- **Potency** is inherited the same way as a stat line. An essence with no potency counts as zero.

---

## Mutations

Two independent rolls happen on every breed:

- About one breed in twenty, one stat gains a permanent boost: damage, damage resist, crit rating,
  or vitality (max health).
- About one breed in thirty-three, potency gains a permanent boost. Above 1,000 potency the gain is
  a quarter of the usual amount.

Both can land on the same breed. Whenever either lands, the baby also gets a **new colour**, rolled
from every creature palette in the game. It can be anything. Some are beautiful, some are odd, all
are rare. The ID panel lists every mutation the essence carries under "Genetic Mutations".

There is no limit to how many mutations a lineage can accumulate.

### The spirit

This part of the ritual is switched off unless the server enables it. When it is on and a mutation
lands, something stirs before the baby is born. A **spirit of the offspring** rises in front of the
two parents, wearing the exact look and colour the baby will have. Only the two parent pets can harm
it, and it will only fight them. It cannot hurt you. You can heal your own pet with kits or spells
while it is in the motel or fighting the spirit.

Bring it down together and the baby is born with an extra **Awakened Blessing**: one more mutation
on top of whatever the breed rolled. If the fight runs long, the spirit fades and the birth completes
without the blessing; you never lose a litter to the spirit. If one of the parents dies, the same
thing happens: the spirit dissolves and the baby is born without the blessing.

---

## Consumables

Ivo, Ruggan's Quartermaster, in the motel sells four things that change a breed, and one that
changes a pet's colour. Each is used on a combat pet essence in your pack; the first four go on
before the dance and are spent by the breed they affect.

- **Courtship Incense** (Lesser +2.5%, Refined +5%, Exquisite +10%). Adds to the chance of a stat
  mutation on the next breed. Both parents can be primed and the bonuses add, up to +50%. A weaker
  incense will not replace a stronger one already on the essence. It does not change the potency
  roll, and it is spent by any breed that goes through, mutation or not.
- **Chromatic Catalyst.** If the next breed rolls a new colour, it is drawn from a pool of vivid,
  saturated palettes instead of the whole range. It is only used up when a colour actually rolls, so
  it stays on the essence until then.
- **Nurturing Draught.** Give to a young essence and every qualifying kill counts double until it
  reaches adulthood. Adults cannot take it.
- **Offering of Subjugation.** Weakens the next spirit: it hits half as hard and falls much faster.
  It is only used up when a spirit actually rises, and the vendor item is refused outright while
  spirits are switched off on the server.
- **Mutagenic Serum.** Use it on any combat pet essence and its colour is re-rolled on the spot from
  the same pool a mutation draws on. Colour only: nothing else about the pet changes. Summon it (or
  re-summon it) to see the new look.

---

## Raising young

A bred essence is born a **Newborn**: half the size, half the strength, and it cannot breed. Its ID
panel shows its growth:

```
Growth: Newborn 1/5 - 60 kills to Whelp, 300 to Adult
```

It grows by hunting. A kill counts when:

- the creature is **at or above your pet's tier**, and
- your pet did a real share of the damage, at least a tenth.

Every sixty kills it grows a stage: Newborn, Whelp, Juvenile, Adolescent, Young Adult. Each stage adds
a tenth of adult size and strength (health, damage and ratings), so a Young Adult fights at 90%. Your
pet grows before your eyes, its name changes, and it is healed to full. At three hundred kills it
becomes an **Adult**: full size, full strength, and able to breed once its bond reaches 100. Anyone
nearby sees it happen.

You do not need to resummon to grow. If your pet is out when it crosses a stage, it changes on the spot.

---

## Binding

A bred essence belongs to the **first character who summons it**. From that moment it is attuned:
it cannot be traded or dropped, and no other character can use it. Its ID panel shows who it is bound to.

This means:

- If you want to sell or gift a baby, do it **before anyone summons it**. A newborn straight from the
  litter is tradeable, including one that was tucked into your pack while you were offline.
- You cannot buy a finished pet. Whoever raises it, keeps it.
- Only bred essences imprint this way. A captured essence keeps whatever trade rules it already had.

---

## Naming

`@pet-name <name>` asks for a new name for your summoned pet (or the essence selected in your pack).
Names are 3 to 32 characters of letters, numbers, spaces, apostrophes and hyphens. Staff review every
request; you get a chat message when it is submitted, and the pet is renamed when it is approved. One
request per minute, and a new request replaces any you still have waiting. If the essence changes
hands, is renamed or is destroyed before review, the request is denied automatically.

---

## Kits

Ivo sells two tools for essences.

- **Pet Neutering Kit.** Use on a pet essence to make it permanently unable to breed. Cannot be
  undone.
- **Pet Tailoring Kit.** Use on a combat essence to extract its entire look, model, colour, size and
  all, into a filled kit. The source essence is destroyed. Use the filled kit on another combat essence
  and it takes on that look while keeping its own stats, potency, bond and lineage. Dismiss the pet
  first. A shiny look makes the new essence shiny, which means it can no longer breed.

---

## Quick answers

**My stud shows 10/10 after breeding.** Check you are looking at the essence that was actually
summoned. Identical essences are easy to mix up.

**I danced and nothing happened.** Same room, both pets out, both dances within five seconds, one male
and one female, in the motel, both tier 100+ and bond 100+. `@breed-debug` confirms the place and
your own pet; the refusal text in red names anything else.

**Breeding cancelled: your main pack has no free slot.** The baby needs a slot in the female owner's
main pack, not a side pack. Free one and dance again.

**The colour did not change.** Only mutations change colour, and mutations are rare. A normal litter
looks like its donor parent. A potency mutation changes colour too.

**Can I breed a shiny?** No. Shiny is a capture-only trait.

**I gave someone a baby and now they cannot use it.** It was summoned before the trade and is bound to
you. Trade newborns unsummoned.

**Does growth count when my pet is not out?** No. It must be summoned and fighting.

**The Offering was not consumed.** Spirits are off on this server, or no mutation rolled, so no spirit
rose. It stays on the essence for next time.
