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
- **Males** are studs. Each has ten breedings a day. The count refills a day after it was first used.
- **Females** are dams. After a litter she rests for four hours before she can breed again.
- **The baby always goes to the female's owner.** If you are lending a stud, agree on your cut first.

Some essences cannot breed, and their ID panel says so: shiny essences, neutered essences, and young
that have not grown up yet.

---

## The ritual

1. Both players go to the **Seedy Motel**.
2. Each summons their pet. The two pets must stand in the **same room**.
3. Both players perform the **dance emote** within five seconds of each other.

You will see "The mating ritual has begun between <pet> and <pet>..." and moments later the birth
message. The baby essence appears in the female owner's pack. Both parents are dismissed afterwards;
resummon them to breed again.

If nothing happens, `@breed-debug` tells you exactly which condition is not met.

---

## What the baby inherits

- **Species and look** come from one parent, chosen at random.
- **Stats** come from both. For each stat the baby leans toward one parent or the other, roughly
  55/45, so two strong parents make a strong baby but never a weaker one than the weaker parent.
- **Mutations** are inherited as counts and stack across generations. A lineage that has been bred
  carefully for many generations is visibly better than a fresh capture.

---

## Mutations

About one breed in twenty mutates. When it happens:

- One stat gains a permanent boost: damage, damage resist, crit, vitality, or potency.
- The baby gets a **new colour**, rolled from every creature palette in the game. It can be
  anything. Some are beautiful, some are odd, all are rare.
- The ID panel lists every mutation the essence carries under "Genetic Mutations".

There is no limit to how many mutations a lineage can accumulate.

### The spirit

On a mutation, before the baby is born, something stirs. A **spirit of the offspring** rises in front
of the two parents, wearing the exact look and colour the baby will have. Only the two parent pets can
harm it, and it will only fight them. It cannot hurt you. Bring it down together and the birth
completes. If the fight runs long, the spirit fades and the birth completes anyway; you never lose
a litter to the spirit.

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

Every sixty kills it grows a stage: Newborn, Whelp, Juvenile, Adolescent, Young Adult. Your pet grows
before your eyes, its name changes, and it is healed to full. At three hundred kills it becomes an
**Adult**: full size, full strength, and able to breed. Anyone nearby sees it happen.

You do not need to resummon to grow. If your pet is out when it crosses a stage, it changes on the spot.

---

## Binding

A bred essence belongs to the **first character who summons it**. From that moment it is attuned:
it cannot be traded or dropped, and no other character can use it. Its ID panel shows who it is bound to.

This means:

- If you want to sell or gift a baby, do it **before anyone summons it**. A newborn straight from the
  litter is tradeable.
- You cannot buy a finished pet. Whoever raises it, keeps it.
- Captured essences already work this way. Breeding does not change that.

---

## Kits

Two tools exist for essences. Ask around the motel.

- **Pet Neutering Kit.** Use on a combat essence to make it permanently unable to breed. Cannot be
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
and one female, in the motel. `@breed-debug` will tell you which one failed.

**The colour did not change.** Only mutations change colour, and mutations are rare. A normal litter
looks like its donor parent.

**Can I breed a shiny?** No. Shiny is a capture-only trait.

**I gave someone a baby and now they cannot use it.** It was summoned before the trade and is bound to
you. Trade newborns unsummoned.

**Does growth count when my pet is not out?** No. It must be summoned and fighting.
