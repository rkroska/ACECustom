# Pet Breeding - Content Guide

Audience: the people who build quests, NPCs, monsters, vendors and dungeons. No code required.
Everything below can be tuned live with the `@modify*` commands or placed in the world with the
normal content tools.

Related: `PET_BREEDING_PLAYER_GUIDE.md` (the rules as players see them, useful for NPC dialogue),
`PET_BREEDING_DEVELOPER_GUIDE.md` (how it works under the hood).

---

## 1. What the system is, in one paragraph

Two players bring one male and one female combat pet essence to the Seedy Motel, summon both pets in
the same cell, and both perform the dance emote within a few seconds of each other. A baby essence
is created and handed to the female's owner. The baby inherits stats from both parents and has a small
chance to mutate: a stat gains a permanent boost and the pet gets a random new colour. On a mutation,
a "spirit" of the offspring rises and the two parent pets must defeat it together. The baby is born a
newborn: half size, half strength, unable to breed, and grows into an adult by hunting. The first
character to summon a bred essence binds it forever.

---

## 2. Where it happens

- Breeding only works inside landblock `0x016C` (Seedy Motel). Change with
  `@modifylong pet_breeding_allowed_landblock <decimal>`; 0x016C is 364 in decimal.
- Both pets must be in the same landcell (roughly the same room), and both owners in the same landblock.
- This is a public place. Players can see each other's rituals, guardians and growth moments.

If you build a dedicated breeding dungeon later, the landblock property is the only thing to change.

---

## 3. NPCs: what they should teach

The item ID panels are deliberately short. Rules live with the NPCs and release notes. A breeding NPC
in the motel should cover, in this order:

1. **Bring a pair.** One male, one female. The essence ID panel shows "Sex: Male" or "Sex: Female".
   Sex is fixed per essence and is a coin flip. Two of the same sex cannot breed.
2. **Summon both, stand together, both dance.** Within five seconds of each other, in the same room.
3. **The stud tires.** A male has ten breedings a day. The count is on its ID panel and refills a day
   after it was first used.
4. **The dam rests.** A female needs four hours between litters. Her ID panel shows when she is ready.
5. **The baby goes to the dam's owner.** Always. Agree on payment before you dance.
6. **Mutations are rare.** Roughly one breed in twenty. A mutated baby gains a permanent stat and a
   new colour. Its spirit will rise; only the parents can strike it down; it cannot harm you.
7. **Young must be raised.** A newborn is small and weak and cannot breed. It grows by defeating
   creatures of its own tier or higher while it is out and doing real damage. Three hundred such kills
   makes an adult.
8. **First summon binds.** A bred essence belongs to whoever summons it first. Trade babies before
   you summon them, never after.
9. **Shiny does not breed.** Shiny is a capture-only trait.

A second NPC, or the same one on a second dialogue branch, can sell or explain the kits (section 5).

---

## 4. Tunable values

All live. Use `@showprops` to read, `@modifybool` / `@modifylong` / `@modifydouble` / `@modifystring`
to change. Stored values survive restarts and override the code defaults.

### Breeding

| Property | Default | What it does |
|---|---|---|
| pet_breeding_enabled | true | Master switch. |
| pet_breeding_allowed_landblock | 364 (0x016C) | Where breeding works. |
| pet_breeding_dance_sync_seconds | 5 | How close together the two dances must be. |
| pet_breeding_male_max_charges | 10 | Breedings a male gets per reset period. |
| pet_breeding_male_charge_reset_hours | 24 | Hours until a male's charges refill. |
| pet_breeding_cooldown_hours | 4 | Hours a female rests after a litter. |
| pet_breeding_min_parent_level | 1 | Minimum essence tier to breed. |
| pet_breeding_min_bond | 1 | Minimum bond level to breed. |
| pet_breeding_dismiss_after_breed | true | Parents are dismissed after the birth. |
| pet_breeding_allow_shiny | false | Whether shinies may breed. |

### Mutation

| Property | Default | What it does |
|---|---|---|
| pet_breeding_base_mutation_chance | 0.05 | Chance per breed of a stat mutation (and colour). |
| pet_breeding_mutation_decay_rate | 0 | Leave at 0 for a flat rate. |
| pet_breeding_potency_mutation_chance | 0.03 | Independent chance of a potency mutation. |
| pet_breeding_damage_mutation_step | 10 | Damage rating gained per mutation. |
| pet_breeding_dr_mutation_step | 10 | Damage resist gained per mutation. |
| pet_breeding_crit_mutation_step | 5 | Crit rating gained per mutation. |
| pet_breeding_vitality_mutation_step | 200 | Max health gained per mutation. |
| pet_breeding_potency_mutation_step | 25 | Potency gained per mutation. |
| pet_breeding_max_stat_mutations | 0 | Per-stat cap. 0 = none. This server does not cap. |

Changing a step rescales every existing pet the next time it is summoned. That is intended: you can
rebalance the whole population without touching items.

### Mating guardian

| Property | Default | What it does |
|---|---|---|
| pet_breeding_guardian_enabled | false | Turn the ritual fight on. |
| pet_breeding_guardian_timeout_seconds | 90 | Fight window. The birth completes either way. |
| pet_breeding_guardian_template_wcid | 7 | Creature weenie used as the combat template (see below). |
| pet_breeding_guardian_health_mult | 1.0 | Guardian health = both parents' max health x this. |
| pet_breeding_guardian_damage_mult | 0.5 | Scales the guardian's damage against the pets. |
| pet_breeding_guardian_translucency | 0.6 | 0 = solid, higher = more ghostly. |

Health mult 1.0 dies in a few seconds against strong pets. Ten is a real fight. Watch the server log line
`slain after Ns` to tune.

**Choosing a template.** The template only supplies combat behaviour (attack animations, body-part
damage, AI). Its look is replaced by the offspring's. Use a plain, attackable, non-faction retail monster
with no special abilities. The default drudge skulker is safe. Avoid anything with spells, summons,
quest hooks, emotes on death, or wielded weapons.

### Maturity and binding

| Property | Default | What it does |
|---|---|---|
| pet_maturity_enabled | true | Babies are born young. Off = born adult. |
| pet_maturity_kills_required | 300 | Kills to reach adulthood. |
| pet_maturity_stages | 5 | Growth steps between birth and adult. |
| pet_maturity_stage_names | Newborn,Whelp,Juvenile,Adolescent,Young Adult | Names, first = birth. |
| pet_maturity_min_damage_share | 0.10 | The young pet must deal this share of a kill's damage. |
| pet_maturity_juvenile_scale | 0.5 | Newborn size relative to adult. |
| pet_maturity_juvenile_strength | 0.5 | Newborn health, damage and ratings relative to adult. |
| pet_maturity_imprint_on_summon | true | First summon binds the essence to that character. |

Only creatures at or above the essence's tier count toward growth. Most pets are tier 200-300, so
players will hunt endgame content to raise them.

### Test switches (keep false in production)

`pet_breeding_force_mutation`, `pet_breeding_bypass_male_charges`, `pet_breeding_bypass_female_cooldown`,
`pet_breeding_verbose_logging`, `pet_visual_packet_debug`.

---

## 5. Items you can place, sell or reward

| WCID | Item | Effect |
|---|---|---|
| 98760399 | Pet Neutering Kit | Use on a combat essence: permanently unable to breed. Cannot be undone. |
| 98760400 | Pet Tailoring Kit | Use on a combat essence: extracts its entire look into a filled kit. The source essence is destroyed. |
| 98760401 | Pet Tailoring Kit (Filled) | Use on another combat essence: it takes on the stored look. Stats, potency, bond, sex, lineage and growth are unchanged. |

The SQL for these lives in `Content/sql/weenies/98760399-98760401 Pet Tailoring and Neutering Kits.sql`.
Load it into `ace_world` before placing them. Good homes: a vendor in the motel, a quest reward, or a
rare drop. The filled kit carries any mutation colour and the shiny trait of its source, so a shiny look
tailored onto an ordinary essence makes that essence shiny and therefore unable to breed. Price them with
that in mind.

Things a kit will refuse, with a message: a passive pet crate, an essence whose pet is currently
summoned, or an item in the trade window.

---

## 6. Things players will see that you did not script

- Chat lines to both owners at each step: ritual begins, guardian rises, guardian yields or fades, birth,
  stud charge spent.
- Local emotes from a growing pet ("shudders and swells as it grows into a Whelp!") and a roar at
  adulthood, visible to anyone nearby, with particle effects.
- The pet's name changes with its stage: "Schneebly's Whelp Browerk".
- ID panel lines on every combat essence: sex and status, growth, bond, total mutations, per-stat mutation
  list, combat ratings.

---

## 7. Admin commands for events and support

| Command | Level | Use |
|---|---|---|
| @breed | Admin | Force a breed with the nearest eligible partner, skipping the dance. Both players are told it was forced. |
| @setsex male/female/derive | Admin | Override an essence's sex, or return it to its natural coin flip. |
| @pet-reset-cooldown | Admin | Clears the female cooldown and refills male charges on the targeted essence. |
| @pet-set-maturity juvenile/adult/N | Admin | Set growth directly. Applies in place if the pet is out. |
| @pet-set-mutations N | Admin | Sets the displayed total. |
| @pet-cleanse-palette | Admin | Removes a mutation colour, back to natural. |
| @breed-debug | Player | Prints why a breed is or is not possible right now. |

---

## 8. Known limits

- A server restart during a guardian fight completes nothing; the parents have already paid.
- Humanoid captures with stored anim parts or textures may not show a mutation colour.
- Live growth relies on the client re-rendering size on an object refresh. If a stage-up changes the
  name but not the size, report it; the size is correct on the next summon regardless.
