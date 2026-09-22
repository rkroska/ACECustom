# Pet Breeding - Content Guide

> **SUPERSEDED (2026-09-21).** Kept for history. Use `PET_BREEDING_REFERENCE.md`, `PET_BREEDING_TECHNICAL_DESIGN.md` and
> `PET_BREEDING_PLAYER_GUIDE.md`. This file predates the annex move to landblock 0x0106 variation 2
> and contains details that are now wrong.


Audience: the people who build quests, NPCs, monsters, vendors and dungeons. No code required.
Everything below can be tuned live with the `@modify*` commands or placed in the world with the
normal content tools.

Related: `PET_BREEDING_PLAYER_GUIDE.md` (the rules as players see them, useful for NPC dialogue),
`PET_BREEDING_DEVELOPER_GUIDE.md` (how it works under the hood), `PET_BREEDING_ANNEX_DESIGN.md`
(the motel NPCs and vendor).

---

## 1. What the system is, in one paragraph

Two players bring one male and one female combat pet essence to the Seedy Motel, summon both pets in
the same cell, and both perform the dance emote within a few seconds of each other. A baby essence
is created and handed to the female's owner. The baby inherits stats from both parents and has a small
chance to mutate: a stat or potency gains a permanent boost and the pet gets a random new colour.
With the guardian switched on, a mutation makes a "spirit" of the offspring rise; the two parent pets
must defeat it together for a bonus mutation. The baby is born a newborn: half size, half strength,
unable to breed, and grows into an adult by hunting. The first character to summon a bred essence
binds it forever.

---

## 2. Where it happens

- The code default is landblock `0x013A` (314 decimal), variation `3`: the instanced Seedy Motel the
  portal (98760388) delivers players to. `0x016C` is the Marketplace, not the motel; older notes that
  said otherwise were wrong.
- `pet_breeding_allowed_landblock` accepts three shapes: `0` = anywhere; a 16-bit landblock
  (`0x013A`) = the whole landblock; a full 32-bit cell (`0x013A02AE`, 20578990 decimal) = that exact
  cell only. `pet_breeding_allowed_variant` must match the instance (`-1` ignores it).
- Stored server config overrides the code default. Run `@showprops` before assuming; a shard that
  was set to 364 (`0x016C`) earlier still says 364 until `@modifylong` fixes it.
- Both pets must be in the same landcell (roughly the same room), and both owners in the same
  landblock. `@breed-debug` prints the cell you are standing in and whether it matches.
- The same area rule decides where players may heal their own summoned pet with kits and beneficial
  spells (heals scale up to the pet's health). Harmful spells on your own pet are never allowed.
- This is a public place. Players can see each other's rituals, guardians and growth moments.

If you build a dedicated breeding room later, set the landblock property to that room's exact cell.

---

## 3. NPCs: what they should teach

The item ID panels are deliberately short. Rules live with the NPCs and release notes. A breeding NPC
in the motel should cover, in this order:

1. **Bring a pair.** One male, one female. The essence ID panel shows "Sex: Male" or "Sex: Female".
   Sex is fixed per essence and is a coin flip. Two of the same sex cannot breed. Both must be tier
   100 or higher and bonded to level 100 or higher.
2. **Summon both, stand together, both dance.** Within five seconds of each other, in the same room.
3. **The stud tires.** A male holds ten breeding charges. The count is on its ID panel and refills
   24 hours after its last refill.
4. **The dam rests.** A female needs four hours between litters. Her ID panel shows when she is ready.
5. **The baby goes to the dam's owner.** Always. Agree on payment before you dance. Her main pack
   needs a free slot or the breed is refused.
6. **Mutations are rare.** Roughly one breed in twenty for a stat, one in thirty-three for potency.
   A mutated baby gains a permanent boost and a new colour. If the spirit is enabled it will rise;
   only the parents can strike it down; it cannot harm you; beating it adds a bonus mutation.
7. **Young must be raised.** A newborn is small and weak and cannot breed. It grows by defeating
   creatures of its own tier or higher while it is out and doing real damage. Three hundred such kills
   makes an adult, and it still needs bond 100 before it can breed.
8. **First summon binds.** A bred essence belongs to whoever summons it first. Trade babies before
   you summon them, never after.
9. **Shiny does not breed.** Shiny is a capture-only trait.

A second NPC, or the same one on a second dialogue branch, can sell or explain the kits and
consumables (section 5).

---

## 4. Tunable values

All live. Use `@showprops` to read, `@modifybool` / `@modifylong` / `@modifydouble` / `@modifystring`
to change. Stored values survive restarts and override the code defaults. The web portal's Pet
Breeding Calculator reads these live from the server and simulates breeds with them, so it is the
place to try a change before applying it.

### Breeding

| Property | Default | What it does |
|---|---|---|
| pet_breeding_enabled | true | Master switch. |
| pet_breeding_allowed_landblock | 314 (0x013A) | Where breeding works. 0 = anywhere; 16-bit = landblock; 32-bit = exact cell. |
| pet_breeding_allowed_variant | 3 | Landblock instance that must match. -1 = ignore. |
| pet_breeding_dance_sync_seconds | 5 | How close together the two dances must be. |
| pet_breeding_male_max_charges | 10 | Breedings a male gets per reset period. |
| pet_breeding_male_charge_reset_hours | 24 | Hours after the last refill until a male's charges refill. 0 = never. |
| pet_breeding_cooldown_hours | 4 | Hours a female rests after a litter. |
| pet_breeding_min_parent_level | 100 | Minimum essence tier to breed. Tiers are 50, 80, 100, 125, 150, 180, 200, 250, 300. |
| pet_breeding_min_bond | 100 | Minimum bond level on both parents. An essence with no bond counts as 1. |
| pet_breeding_dismiss_after_breed | true | Parents are dismissed after the birth. |
| pet_breeding_allow_shiny | false | Whether shinies may breed. |

What the 100/100 gates mean in practice:

- Tier 50 and 80 essences never breed. The refusal reads "Parent pets must be at least tier 100 to
  breed."
- Bond XP is only awarded while `pet_bond_enabled` is true and the essence is bonded (attuned) to
  its owner, so with bonding off nobody can breed at all: every essence sits at bond 1 and is refused
  with "Parent pets must have a bond level of at least 100 to breed." Turn bonding on before the
  motel goes live.
- Bred babies are written at bond 1 and imprint on first summon, so a baby must be raised to
  adulthood (300 qualifying kills) and bonded to 100 before it can breed. Both are kill-driven; the
  NPC text should say so.
- While a pet is still growing, bond comes only from the same kills that grow it: at or above its
  tier, with at least `pet_maturity_min_damage_share` of the damage. Farming trivial creatures moves
  neither counter. Adults and captured essences are unaffected. This also gates potency, since a
  pet's usable potency is half its bond level rounded up, so an inherited pile of stored potency
  unlocks at the pace the pet grows.

### Mutation

| Property | Default | What it does |
|---|---|---|
| pet_breeding_base_mutation_chance | 0.05 | Chance per breed of a stat mutation (and colour). |
| pet_breeding_mutation_decay_rate | 0 | Leave at 0 for a flat rate. Above 0, the chance shrinks with the baby's inherited stat mutations. |
| pet_breeding_mutation_min_floor | 0.02 | The stat chance never decays below this. |
| pet_breeding_potency_mutation_chance | 0.03 | Independent chance of a potency mutation. Incense does not raise it. |
| pet_breeding_damage_mutation_step | 10 | Damage rating gained per mutation. |
| pet_breeding_dr_mutation_step | 10 | Damage resist gained per mutation. |
| pet_breeding_crit_mutation_step | 5 | Crit rating gained per mutation. |
| pet_breeding_vitality_mutation_step | 50 | Max health gained per mutation. |
| pet_breeding_potency_mutation_step | 25 | Potency gained per mutation. |
| pet_breeding_potency_soft_cap | 1000 | At or above this stored potency the step is quartered (min 1). |
| pet_breeding_potency_hard_cap | 0 | Potency never exceeds this. 0 = none. The smaller positive of this and `pet_potency_max_stored` wins. |
| pet_breeding_max_stat_mutations | 0 | Per-stat cap. 0 = none. This server does not cap. |

Changing a damage, DR, crit or vitality step rescales every existing pet the next time it is summoned,
because those bonuses are stored as counts. The potency step is different: potency is stored as a
finished value on the essence, so a step change only affects future mutations.

### Mating guardian

| Property | Default | What it does |
|---|---|---|
| pet_breeding_guardian_enabled | false | Turn the ritual fight on. Also makes the Offering of Subjugation usable. |
| pet_breeding_guardian_timeout_seconds | 90 | Fight window (minimum 5). The birth completes either way, without the bonus. |
| pet_breeding_guardian_template_wcid | 7 | Creature weenie used as the combat template (see below). |
| pet_breeding_guardian_health_mult | 1.0 | Guardian health = both parents' max health x this. |
| pet_breeding_guardian_damage_mult | 0.5 | True multiplier on the guardian's hits. |
| pet_breeding_guardian_translucency | 0.6 | 0 = solid, higher = more ghostly. |

How the fight actually scales, so the numbers make sense:

- **Its hits** ignore the template. Each hit is 8% of the defending pet's max health, clamped to
  20-500, times `damage_mult`, halved again if an Offering of Subjugation was used.
- **Damage it takes** is scaled down whenever a pet would kill it in fewer than about thirty hits
  (a very hard hitter still needs roughly fifteen), and no single hit takes more than 10% of its
  health. Pets that would need more than thirty hits get no help. When weakened it takes 2.5x damage
  and the per-hit cap is 25%. `health_mult` therefore stretches the fight less than it looks; it is
  not a linear "ten times tougher". Watch the server log line `slain after Ns` when tuning.
- Killing it grants the **Awakened Blessing**: one extra mutation on a random eligible line,
  including potency. Timeout, a parent pet dying, or the guardian being removed all complete the birth
  without it.

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

Stages step evenly from the juvenile value to 1.0: with defaults a Newborn fights at 50%, a Young
Adult at 90%, an Adult at 100%. Only creatures at or above the essence's tier count toward growth.
Most pets are tier 200-300, so players will hunt endgame content to raise them.

### Test switches (keep false in production)

`pet_breeding_force_mutation`, `pet_breeding_bypass_male_charges`, `pet_breeding_bypass_female_cooldown`,
`pet_breeding_verbose_logging`, `pet_visual_packet_debug`.

---

## 5. Items you can place, sell or reward

### Kits

| WCID | Item | Effect |
|---|---|---|
| 98760399 | Pet Neutering Kit | Use on a pet essence: permanently unable to breed. Cannot be undone. |
| 98760400 | Pet Tailoring Kit | Use on a combat essence: extracts its entire look into a filled kit. The source essence is destroyed. |
| 98760401 | Pet Tailoring Kit (Filled) | Use on another combat essence: it takes on the stored look. Stats, potency, bond, sex, lineage and growth are unchanged. |

SQL: `Database/Updates/World/2026-09-09-01-Pet-Tailoring-and-Neutering-Kits.sql` (mirrored in
`Content/sql/weenies/`). Ivo (78780201) sells the two empty kits; the filled kit is only ever made
by extraction. The filled kit carries any mutation colour and the shiny trait of its source, so a shiny
look tailored onto an ordinary essence makes that essence shiny and therefore unable to breed. Price
them with that in mind.

What each refuses, with a message: the tailoring kits refuse a passive pet crate, an essence whose pet
is currently summoned, and an item in the trade window. The neutering kit only refuses an essence that
is already neutered; it works on any pet device in the pack, summoned or not.

### Consumables

| WCID | Item | Value | Effect |
|---|---|---|---|
| 78780250 | Lesser Courtship Incense | 100,000 | +2.5% stat mutation chance on the next breed. |
| 78780251 | Refined Courtship Incense | 500,000 | +5% stat mutation chance. |
| 78780252 | Exquisite Courtship Incense | 2,500,000 | +10% stat mutation chance. |
| 78780253 | Nurturing Draught | 500,000 | Juvenile earns 2 maturity kills per qualifying kill until adult. |
| 78780254 | Chromatic Catalyst | 1,000,000 | Next palette mutation rolls from the vibrant pool. |
| 78780255 | Offering of Subjugation | 500,000 | Next mating guardian is weakened. |
| 78780257 | Mutagenic Serum | 250,000 | Re-rolls the essence's colour from the master palette pool, right now. Appearance only. |

SQL: `Database/Updates/World/2026-09-12-00-Pet-Breeding-Sinks.sql`, which also stocks the first six
on Ivo if he exists, and `Database/Updates/World/2026-09-19-00-Pet-Mutagenic-Serum.sql` for the
serum, stocked on Ivo the same way. Rules that matter for pricing:

- Incense is applied to one parent's essence; both parents' bonuses add, capped at +50%. A stronger
  tier replaces a weaker one; equal or weaker is refused. Neutered essences cannot be anointed. It
  is spent by any breed that passes the gates, whether or not a mutation lands. It does nothing to the
  potency roll.
- The Catalyst stays on the essence until a palette actually rolls (stat or potency mutation), then
  is spent. One per essence.
- The Draught is refused on adults and on an essence already dosed; the effect ends at adulthood.
- The Offering is refused, and not consumed, while `pet_breeding_guardian_enabled` is false. It is
  spent only when a guardian actually spawns.
- The Serum works on any combat pet essence (captured, looted or bred, juvenile or adult) and is
  spent immediately. It draws from the same master pool a bred mutation uses, not the Catalyst's
  vibrant pool, and touches nothing but the colour: no stats, mutation counts or potency. The new
  colour shows on the next summon; if the pet is already out it is repainted in place. It is refused,
  and not consumed, on anything that is not a combat pet essence. Priced below the Catalyst because
  it is a plain lottery from the whole pool and is meant to be bought repeatedly while hunting a
  colour.

`78780256` (Ancestral Gene Re-roller) is reserved and unbuilt.

---

## 6. Things players will see that you did not script

- Chat lines to both owners at each step: ritual begins, guardian rises, guardian yields or fades,
  Awakened Blessing, birth, stud charge spent, and "tucked away" delivery if the female's owner is
  offline or full at birth.
- Local emotes from a growing pet ("shudders and swells as it grows into a Whelp!") and a roar at
  adulthood, visible to anyone nearby, with particle effects.
- The pet's name changes with its stage: "Schneebly's Whelp Browerk".
- ID panel lines on every combat essence: sex and status, growth, bond, total mutations, per-stat mutation
  list, combat ratings (base plus mutation bonus).
- A coloured square behind every combat essence icon: male and female at a glance, no appraisal needed.
  Sex comes from the essence's GUID, so essences that already existed pick their colour up the next
  time they load. The art is set by `pet_sex_icon_underlay_male` (default 100670255 / 0x06001B2F) and
  `pet_sex_icon_underlay_female` (default 100670253 / 0x06001B2D); both icons must exist in the client
  patch or the square simply will not draw. `pet_sex_icon_underlay_enabled false` turns it off.
- `@pet-name <name>` requests land on the web portal's **Pet Name Approvals** page (Monitoring
  section, portal admins or users granted that page) and, if configured, in the admin Discord
  channel. Approving renames the essence in place; a request whose essence has moved, been renamed or
  destroyed is denied automatically.

---

## 7. Admin commands for events and support

| Command | Level | Use |
|---|---|---|
| @breed | Admin | Force a breed with the nearest eligible partner, skipping the dance. Both players are told it was forced. Location checks are skipped for admins; charges and cooldowns are not. |
| @setsex male/female/derive | Admin | Override the last appraised essence's sex, or return it to its natural coin flip. Aliases @setalpha, @makealpha. |
| @pet-reset-cooldown | Admin | Clears the female cooldown and refills male charges on the targeted essence. |
| @pet-set-maturity juvenile/adult/N | Admin | Set growth directly (N = kill count). Applies in place if the pet is out. |
| @pet-set-mutations dmg dr crit vit [pot] | Admin | Sets the per-stat mutation counts; omitted values are 0. |
| @pet-cleanse-palette | Admin | Removes every palette override (template, base, shade, captured palettes) so the next summon is the natural look. |
| @dance | Player | Performs the dance emote without typing it. |
| @breed-debug | Player | Prints breeding switch, location match, the player's own pet status and a count of nearby players with pets. Admins also see each online player's name, distance, landblock and pet. |
| @pet-debug | Developer | Same output as @breed-debug. |
| @export-template, @et, @ed template | Developer | Turns the creature or object you are looking at into a new weenie SQL file that looks exactly like it. See below. |

### Turning a pet (or anything) into a new NPC or monster: `@et`

A summoned pet's look exists only at runtime - the setup, clothing base, palette, shade, scale and the
per-part anim / palette / texture rows the capture and mutation systems stamped on it. `@ed <wcid>` and
`@export-sql` only export weenies that are already in the world database, so that look could not be
captured. `@export-template` (short `@et`, or `@ed template ...` to also drop the file in Discord) exports
the **currently selected** object (or the last one you appraised) as a ready-to-load weenie.

The visuals are the point of the tool. The appearance is taken from what is actually **rendered**
(`CalculateObjDesc`): for a pet that is its captured / mutated rows, for a monster its overrides, and for a
player character the composed armour and clothing they are wearing at that moment. Held items (weapon,
shield, caster) are separate objects, so they are written as `create_list` Wield rows instead. The flavour
only decides whether the result fights back:

- `monster` (the default): a faithful copy - attackable, keeps its attributes, vitals, skills, body parts
  and spell book. For a **pet** the held weapons are capture skins, so they are left out here (the copy
  fights with its body parts like the pet did); use `npc` if you want them shown.
- `npc`: same look, plus the NPC treatment used for Ruggan's Annex (not attackable, invincible, stuck,
  yellow radar blip, click-to-talk, no loot / XP / corpse) and every held item as a Wield row.
- A **player** source defaults to `npc`, is always exported as a plain creature, and has all account and
  character state stripped (only appearance, level, heritage, gender, attributes, trained skills and held
  items survive). Re-export after a gear change and you get a different look.
- A pet **essence / device** is refused: summon the pet and select the pet.
- A non-creature (chest, portal, prop) exports its type, look and name; no combat properties are invented.

Every argument is optional and the order does not matter:

```
@et                              selected object, its own name, next free id
@et npc                          same, as an NPC
@et Gene's Handler               name override (apostrophes are fine)
@et 78790300 npc Gene's Handler  explicit id (decimal or 0x hex), any order
@et 78790300 overwrite           replace a weenie that already exists at that id
@ed template npc                 same export, always sent to Discord (even with auto-Discord off)
```

By default every export is also **loaded into ace_world** and **sent to the Discord exports channel**, so
it can be spawned straight away and anyone can download the file. The chat reply says it is loading; a
second line confirms `Loaded <wcid> into ace_world` (or prints the error and the manual command). Two
server switches turn this off: `content_template_export_auto_import` and
`content_template_export_auto_discord` (`@modifybool`). Only ids **inside** the staging block are ever
auto-loaded: an explicit id outside it, and especially an `overwrite` there, is written to the file only,
so real content is never replaced without you loading it yourself.

Ids come from the **temporary export block `78790000`-`78799999`** - the whole 7879 prefix is automatic
export space and nothing you hand-author lives there, so a file from this tool can never collide with
the 7878 content. The tool picks the lowest free id (checked against the world database at export time),
keeps a persisted high-water mark so two exports made before either file is loaded never share an id,
refuses an explicit id that already exists unless you add `overwrite`, warns when an explicit id is
outside the block, and refuses rather than wraps when the block is used up
(`content_template_export_wcid_start` / `_end` / `_next_wcid` in the server config).

The block is **staging, not a home**: export (it loads itself), `@ci <wcid>` or `@create <wcid>`,
iterate, and when the creature is final renumber it into your own 7878 range. `@id`,
`@import-sql`, `@import-sql-folders` and `@import-json` refuse a wcid inside the block ("temporary export
staging block ... add the word force") so nothing accumulates there by accident.

The file lands in the content folder like every other export (`<content_folder>/sql/weenies/<wcid>
<name>.sql`); the chat reply prints the full path, what was captured in one line, and the next steps.
The SQL header records the source name and wcid, who exported it, the UTC time, the flavour, the held-item
dependencies and a warning that the look depends on the client having the same DAT art. Its DELETE is
deliberately narrow (id **and** the generated `tmpl<wcid>_<slug>` class name) so re-running the file only
ever replaces its own export.

---

## 8. Known limits

- A server restart during a guardian fight completes nothing; the parents have already paid.
- Humanoid captures with stored anim parts or textures may not show a mutation colour.
- Live growth relies on the client re-rendering size on an object refresh. If a stage-up changes the
  name but not the size, report it; the size is correct on the next summon regardless.
