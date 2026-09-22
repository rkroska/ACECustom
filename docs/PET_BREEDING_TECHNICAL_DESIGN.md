# Pet Breeding and Ruggan's Annex - Technical Design

| | |
|---|---|
| Status | Ready for production pending the open questions in section 16 |
| Branch | `feature/pet-breeding-motel` |
| Verified | 2026-09-21, against code and the test databases |
| Companions | `PET_BREEDING_REFERENCE.md` (developer reference), `PET_BREEDING_PLAYER_GUIDE.md` (players), `deploy/PROD_DEPLOY_PLAN.md` (rollout) |

This document explains what the system is, how its parts fit together, and why each decision was
made. The developer reference holds the exhaustive line-level detail. This document holds the model
behind it, and still defines every setting and property (appendices A and B) and how each part is
tested (section 14).

---

## Contents

1. [Summary](#1-summary)
2. [Goals and non-goals](#2-goals-and-non-goals)
3. [Architecture](#3-architecture)
4. [Data model](#4-data-model)
5. [The breeding pipeline](#5-the-breeding-pipeline)
6. [The genetics model](#6-the-genetics-model)
7. [Sex, charges and rest](#7-sex-charges-and-rest)
8. [The mating guardian](#8-the-mating-guardian)
9. [Maturity and imprinting](#9-maturity-and-imprinting)
10. [Appearance and colour](#10-appearance-and-colour)
11. [Economy](#11-economy)
12. [Location and instancing](#12-location-and-instancing)
13. [The annex as content](#13-the-annex-as-content)
14. [Test strategy](#14-test-strategy)
15. [Deployment architecture](#15-deployment-architecture)
16. [Risks, known issues and open questions](#16-risks-known-issues-and-open-questions)
17. [Future work](#17-future-work)
- [Appendix A: server settings](#appendix-a-server-settings)
- [Appendix B: object properties](#appendix-b-object-properties)
- [Appendix C: WCIDs](#appendix-c-wcids)

---

## 1. Summary

Players pair two combat pet essences, one male and one female, in a dedicated place and perform a
social ritual: both owners dance within five seconds. The server then produces a new essence:

- **Inherited:** each stat line comes from one parent or the other.
- **Occasionally mutated:** a permanent stat gain and a new colour drawn from the game's own palettes.
- **Born juvenile:** it must be raised through kills before it reaches full strength or can breed.
- **Spirit fight:** when a litter mutates, an optional guardian fight can add one extra mutation.

The place is **Ruggan's Annex**, a private instance (landblock 0x0106, variation 2) reached from Lin.
It is dressed as a comic set piece: two feuding factions of pets, an intern who explains the rules,
a shopkeeper who sells the gold sinks, and an ambient storyline that plays out on its own.

---

## 2. Goals and non-goals

### Goals

1. **A long-term pet progression loop:** capture, then bond, then breed, then raise, then breed
   better. Each step is gated so it can't be skipped.
2. **Social by construction.** Every breed needs two players present at once, and the baby always
   goes to the dam's owner. That creates a market for studs.
3. **Rare, visible achievement.** Mutations are permanent, stack across generations, and come with a
   colour nobody else has.
4. **Real gold sinks** that shift odds or convenience without buying outcomes outright.
5. **Nothing lost to the system.** A refused breed spends nothing, and a birth is never dropped on the
   ground or lost to a full pack. (The one exception is H2, section 16.)
6. **Deterministic and auditable.** Any breed can be replayed exactly from a log line, and the website
   simulator uses the same maths.
7. **Tunable without code.** Every rate is a server setting, and content is data.

### Non-goals

- Breeding passive (non-combat) pets.
- Choosing the species, sex or colour of a baby.
- Cross-server or offline breeding. Both owners must be online, dancing.
- Persisting in-flight spirit fights across a restart (section 16, H2).

---

## 3. Architecture

```
   Player dance (motion or *dance*)                     Web portal
           |                                         (simulator, visualizer,
           v                                          pet-name approvals)
  +------------------------+   reads   +---------------------+
  | PetDevice_Breeding     |---------->| ServerConfig         |
  | (world side:           |           | (51 settings)        |
  |  trigger, partner      |           +---------------------+
  |  match, gates, commit, |
  |  delivery, appraisal)  |   pure    +---------------------+
  |                        |---------->| BreedingMath         |  <- unit tests, REPLAY harness,
  +-----------+------------+           | (inherit, roll, caps,|     website breedingModel.ts
              |                        |  summon arithmetic)  |
      mutated | and guardian on        +---------------------+
              v
  +------------------------+           +---------------------+
  | MatingGuardian         |           | PetMutationService   |  <- palette pools from the
  | (spirit fight,         |           | (master and vibrant  |     portal DAT
  |  blessing)             |           |  pools, apply rule)  |
  +-----------+------------+           +----------+----------+
              v                                   |
  +------------------------+                      v
  | CompleteBirth          |            Creature.CalculateObjDesc
  | (baby essence,         |            (renders the 0x04 template on
  |  juvenile, deliver)    |             every creature path)
  +-----------+------------+
              v
  +------------------------+   +--------------------+   +---------------------+
  | PetDevice (essence)    |   | Maturity/imprint   |   | Consumables and kits|
  | counts + visual set    |<--| (kills, stages,    |   | (Player_Use,        |
  | persisted on biota     |   |  first-summon bind)|   |  PetTailoring)      |
  +-----------+------------+   +--------------------+   +---------------------+
              | summon
              v
  CombatPet: ratings = gear + count x step (+ bond), x juvenile strength
```

**Where state lives.** Only the essence (`PetDevice`) persists. The summoned `CombatPet` is derived
from it at every summon. Guardians, pending breeds, NPC dialogue state and showcase colours live in
memory only.

**Threading.** Breeds run on the dancing player's landblock thread. Delayed work (parent dismissal,
guardian timeout) goes on `WorldManager.ActionQueue`. Live recolours and redraws must run on the
thread that owns the creature's landblock group.

---

## 4. Data model

The central decision: **store the recipe, compute the result.**

| Stored on the essence | Computed at summon |
|---|---|
| Base ("gear") ratings, six lines | Effective ratings = gear + mutation count x **current** step + bond bonuses |
| Mutation **counts** per line | Crit damage, crit resist and crit damage resist derived from the damage and DR bonuses (0.8, 0.8, 0.6) |
| Stored potency (**baked**) | Active potency (bond-limited, only while the potency system is on) |
| Visual override set and capture strings | The pet's model, palettes and textures |
| Sex override, charges, rest time | Juvenile scale and strength from the growth stage |

Consequences:

- **Retuning a step rescales every existing pet at its next summon**, and nothing needs migrating.
  Designers can adjust balance after launch.
- **Potency is the exception.** It is baked into `PetPotencyStored` at birth, because potency has its
  own soft and hard caps that must be applied at the moment of gain.
- **Legacy essences** that stored ratings instead of counts are read through a fallback
  (`rating / step`).

The visual identity of a pet is a **set** of `VisualOverride*` properties plus three
`CapturedObjDesc*` strings. It moves as a unit: donor to baby at birth, source to kit to target in
tailoring. That is why appearance is never partly inherited: the species donor is a single 50/50 pick.

---

## 5. The breeding pipeline

```
 dance --> trigger (2 s rate limit) --> partner scan --> gates --> COMMIT
                                                                     |
     charges spent, rest written, incense removed, devices saved <---+
                                                                     |
                  +------------------- mutated? --------------------+
                  | no, or guardian off                             | yes and guardian on
                  v                                                 v
            CompleteBirth                                   spawn Spirit of X
                  |                                        slain / timeout / lost
                  |                                                 |
                  |                                     (slain: Awakened Blessing)
                  |                                                 v
                  +-------------------> CompleteBirth <-------------+
                                              |
                     baby created, juvenile, delivered (or deferred), parents dismissed
```

### Design points

- **The trigger is social, not mechanical.** The dance comes either from the DrudgeDance motion or
  from the typed `*dance*` soul emote (and `@dance`). A per-player rate limit of 2 s stops a macro
  from flooding scans. The partner must have danced within `pet_breeding_dance_sync_seconds`.
- **Partner selection is forgiving.** Among candidates in range, **compatible** partners are
  preferred, nearest first. If none is compatible, the nearest incompatible one is chosen anyway, so
  the player gets a real refusal reason instead of silence.
- **Every refusal happens before the commit.** Gates run in a fixed order (reference, section 6), and
  none of them change state. Nothing is spent until every gate has passed.
- **Commit order is deliberate.** Charges, rest, incense and devices are written and saved **before**
  the guardian spawns, so a crash cannot duplicate a breed. The trade-off is H2 (section 16): a
  restart mid-fight loses the baby while the costs stay spent.
- **Delivery never loses the baby.** If the dam's owner is online with a free main-pack slot, the baby
  goes straight in. Otherwise it is written to their persisted inventory for the next login. The gates
  check pack space and burden before the commit, so the deferred path only covers the spirit fight
  window.

---

## 6. The genetics model

### Inheritance (per line, independent)

For each of the eight lines: with probability 0.55 the baby takes the parent with the **higher
effective value**, otherwise the lower one. A tie counts the dancer's device as higher. This gives a
slight upward drift without ever creating a value that neither parent had.

| Line | Compared on | Travels with it |
|---|---|---|
| Damage, Damage Resist, Crit | gear + count x step | the gear rating **and** that line's mutation count |
| Crit Damage, Crit Resist, Crit Damage Resist | gear | gear only |
| Vitality | mutation count | the count |
| Potency | stored potency | stored potency **and** the potency count |

These **package deals** are the key design choice: a parent's mutations can only be inherited
together with that parent's line. Players can't cherry-pick a mutation off a weak line.

### Mutation

```
stat chance    = clamp( max(floor, base / (1 + decay x inheritedStatMutations)) + incense, 0, 1 )
potency chance = pet_breeding_potency_mutation_chance        (independent; incense does not apply)
```

| | Default |
|---|---|
| `base` | 0.05 |
| `floor` | 0.02 |
| `decay` | 0 (flat) in code; **0.1 on prod** (shard script) |
| `incense` | sum of both parents' bonuses, clamped 0..0.50 (practical maximum +0.20) |

- **A stat mutation** picks one line uniformly from damage, DR, crit and vitality, skipping any line
  already at the per-line cap (default: no cap).
- **A potency mutation** adds the potency step: quartered at or above the soft cap, clamped to the
  hard cap.
- **The colour** is not a separate roll. **Any** mutation also rolls a new palette. This keeps colour
  meaningful: a unique colour marks a mutated line.

### Determinism: the draw-order contract

Every breed consumes random draws in a fixed order:

1. Draws 1-8: the eight inheritance picks.
2. Draw 9: the stat roll, always drawn, even when forced.
3. Draw 10: the line pick, only when it mutated.
4. Draw 11: the potency roll.
5. Draw 12: the blessing, only after a slain spirit.

With `pet_trace` on, every breed writes a REPLAY blob of its inputs and draws, and `@breed-replay`
re-runs it through the same pure function. The website's `breedingModel.ts` implements the same
contract, and `PetBreedingParityTests` pins it. **Any change to the maths must keep this order, or
bump the model and update both sides.**

---

## 7. Sex, charges and rest

- **Sex** is derived from the essence's GUID (murmur3 fmix32, low bit), so every essence has one
  without a migration. An admin override (`PetIsMaleOverride`) exists. A baby gets a new GUID, so its
  sex is an independent coin flip.
- **Studs** hold **10 charges** that refill **24 h after the last refill** (lazily, on first read).
  This caps any one stud's output per day and supports a stud-rental market.
- **Dams** rest **4 h** after a litter. The baby arrives at the breed, so this is recovery, not
  gestation. The rest limits how often any one dam can produce.
- The **sex square** behind the essence icon is derived on read, so it never needs a data fix. Two
  DIDs are configurable, and the client patch must contain both icons.

---

## 8. The mating guardian

**Purpose:** turn the rarest moment, a mutation, into a shared spectacle and a small skill check,
without ever putting the baby at risk.

- **Spawn:**
  - Only on a breed that actually mutated, and only with `pet_breeding_guardian_enabled`.
  - A template creature (default wcid 7) is neutralised: no loot, XP, corpse or faction.
  - It is dressed in the baby's exact look and colour, made translucent, and named "Spirit of X".
  - It spawns clear of both parent pets.
- **Who can fight:** only the two parents, or a re-summoned pet of either owner, can damage it. It only
  targets the parent pets, and any other target is dropped every tick.
- **Stats:**
  - Health: both parents' max health combined (x `health_mult`).
  - Level: the higher parent's.
  - Ratings: the baby's adult ratings.
  - Attack skill: the better parent's melee defence.
- **Damage normalisation.** Incoming damage is rescaled so the fight takes roughly 15-30 parent hits,
  whatever the parents' power, with a cap of 10% of its health per hit. Outgoing damage is 8% of the
  defending pet's max health, clamped 20-500, x `damage_mult` (0.5). The fight is tense but not lethal
  for healthy pets.
- **Resolution** (exactly one):
  - **Slain:** the Awakened Blessing, one extra mutation drawn over the lines not yet capped, plus
    potency. Then the birth.
  - **Timeout** (90 s): the birth, without the blessing.
  - **Lost** (despawned, or a parent died): the birth, without the blessing.

  **The baby is always born.**
- **Offering of Subjugation:** the spirit hits for half, takes x2.5 damage, and its per-hit cap rises
  to 25%. Consumed only when a spirit spawns.
- **State:** pending breeds are held in an in-memory dictionary. This is known issue H2 (section 16).

---

## 9. Maturity and imprinting

**Purpose:** make a bred pet an investment of play, not just gold, and stop newborns being farmed and
flipped at full power.

- **Born juvenile:** 5 stages (Newborn, Whelp, Juvenile, Adolescent, Young Adult) over **300 kills**,
  60 per stage. Size and strength step from 0.5 to 1.0 in even increments. Strength scales health,
  damage and all ratings.
- **What counts as a kill:** the pet dealt at least 10% of the victim's health, and the victim's level
  is at or above the essence tier. This stops a strong owner carrying the pet, and stops trivial
  farming. The same rule gates bond XP for juveniles.
- **Growth is live.** The pet resizes, renames, heals to full and shows an effect in front of others.
- **Imprinting.** The first character to summon a bred essence owns it for good (attuned and bonded,
  no trade or drop). Until then it is freely tradeable. This preserves a **newborn market** and stops
  **raised-pet resale**.
- **Breeding a bred pet** needs both adulthood and bond 100. Two gates, both earned through play.
- Turning `pet_maturity_enabled` off instantly treats every juvenile as an adult, and turning it on
  restores them. The stored flag is never destroyed.

---

## 10. Appearance and colour

### Palette pools

`PetMutationService` scans the portal DAT palettes (0x04000001-0x04002500) once at startup:

- **Master pool:** at least 2048 colours, less than 40% pure black, mean luminance at least 0.15.
  Mutations, the Mutagenic Serum and `@mutate_pet` draw from it.
- **Vibrant pool:** the master pool filtered for saturation. The Chromatic Catalyst and the annex
  showcase NPCs draw from it. It falls back to the master pool if empty.

The pools are deliberately **not species-filtered**: any creature can come out in any valid palette.
That is what makes a mutation feel rare.

### The write rule

A mutation colour is written as:

- base = the setup's native default palette, when it has one
- `PaletteTemplate` = the 0x04 palette
- `CapturedObjDescPalettes` removed

Captured palettes would otherwise keep overriding the new template.

### Rendering

`Creature.CalculateObjDesc` has three paths:

1. **Biota rows:** captured and bred pets, baked NPCs. It returns early.
2. **Clothing base.**
3. **Equipped items.**

The 0x04 template is applied on paths 1 and 2 by `ApplyPaletteTemplateOverride`, which appends the
full 2048-colour palette as subpalettes. **Equipped items keep their own palettes**, which is why an
armoured NPC can't be whole-body mutated while wearing armour. The annex's mutated Sawato bandit
therefore has its armour **baked** into model rows instead (section 13).

### Visibility

Some captured essences retexture most body parts, and a palette can't show through a texture.
`GetColourChangeVisibility` counts retextured parts:

- At 90% or more, the colour is hidden: the serum refuses and is not consumed, and appraisal says so.

### Showcase colour cycle

The annex's Ward pets change colour live:

- A per-creature setting (`ShowcaseColourCycleSeconds`) rolls a vibrant palette every N seconds, from
  a random starting phase.
- It only runs while a player is within 96 m.
- It sends an ObjDesc update and a sparkle.
- **Nothing is persisted.** Placed-NPC biota is never saved, so the colour resets on respawn.

This is general-purpose: any creature weenie can opt in.

---

## 11. Economy

All prices are in pyreals, from Ivo, and set in whole MMDs (1 MMD = 250,000 pyreals). The ladder runs
from 5 MMD (Lesser incense) to 500 MMD (the tailoring kit).

| Item | Price | Lever | Why it is a sink, not a purchase |
|---|---|---|---|
| Pet Neutering Kit | 2.5M (10 MMD) | Permanently removes an essence from breeding | Supply control |
| Pet Tailoring Kit | 125M (500 MMD) | Moves a look between essences; the source is destroyed | The top sink: a prized look costs a destroyed essence plus 500 MMD |
| Lesser / Refined / Exquisite Courtship Incense | 1.25M / 2.5M / 5M (5 / 10 / 20 MMD) | +2.5 / 5 / 10% stat mutation chance on the next litter | Spent on every committed breed, mutated or not |
| Nurturing Draught | 2.5M (10 MMD) | Double growth credit until adulthood | Time, not power |
| Chromatic Catalyst | 2.5M (10 MMD) | Vivid palette **if** a mutation happens | Only spent when a colour rolls, so no wasted gold |
| Offering of Subjugation | 2.5M (10 MMD) | Much easier spirit fight | Only spent when a spirit spawns |
| Mutagenic Serum | 25M (100 MMD) | Re-roll colour, no stats | Pure cosmetics |

The mutation odds at defaults and the effect of incense:

- **Base:** a 5% stat roll per litter, plus 3% potency.
- **Best case:** Exquisite incense on both parents gives 25% + 3%.
- **Mutations stack across generations**, so the long-term sink is breeding many litters to build a
  line.

---

## 12. Location and instancing

- **The area** is a setting, not code. A 16-bit value means a whole landblock, a 32-bit value one
  exact cell, and 0 anywhere, plus a variation filter.
- **The annex** is landblock **0x0106, variation 2**:
  - The base variation is a retail drudge dungeon that stays untouched.
  - Variations load only their own placements, so the annex is a private, fully furnished copy.
  - Breeding is scoped to it by `pet_breeding_allowed_landblock` 262 and `_variant` 2, which are
    stored in the shard. **The code default still points at the old motel (0x013A / 3).**
- **The same-landcell rule.** Both **pets** must stand in the same landcell. The area can be large,
  but a pairing is always intimate, and two strangers down a corridor can't be paired by accident.
- **Entry and exit:**
  - **Entry:** portal 98760388 beside Prof. Ruggan in Lin lands at the Drop (cell 0x01060186).
  - **Exit:** none. Players recall (open question Q4).
- **Healing:** "in the motel" also enables own-pet healing (`CombatPet.IsInMotelOrEncounter`), so
  players can support their pets during a spirit fight.

---

## 13. The annex as content

### Cast and factions

| Group | Who |
|---|---|
| **Registry** (plain, pedigreed) | Bexley and four pets, including an armoured Sawato bandit |
| **Ward** (mutated, proud) | Splotch and five pets whose colours cycle |
| **Staff** | Fenwick (the tutorial on click) and Ivo (the vendor) |
| **Flavour** | DJ Skulk, Gary, Mrs. Ruggan, Denton, and the drudge family Mubb, Gorta and Mubb Junior |

All NPCs are clones of retail weenies that already render, made harmless: not attackable, no
targeting tactic, stuck and invincible.

### Dialogue engine constraints

The engine is ACE's emote system, and the design works within three constraints found in the code:

1. **Stacked probabilities.** Emote probabilities are thresholds, and the lowest one above the roll
   wins, so an NPC's idle lines must use distinct, ascending values.
2. **Local signals.** One NPC can cue another in the same landblock instance within the listener's
   radius (60 m, straight line). An NPC never hears its own cue.
3. **The busy rule.** An NPC running an emote set, including its pre-delays, ignores **everything**
   else: cues, clicks, chat and its own heartbeat.

### The director pattern

Constraint 3 makes independent openers unreliable: two scenes collide, or a line lands on a busy
actor. The solution is a **single hidden director**:

- **It starts every scene.** A heartbeat roll (5% in total) picks a scene and sends its first cue.
- **Then it stays deliberately busy for 120 s.** A no-op action with a long pre-delay does this, and
  it guarantees **one scene at a time**.
- **Each line is a cue response on its speaker.** It says the line and cues the next speaker, and the
  last line cues no one, so every chain terminates.
- **Actors have no long idle sets**, so they are never busy when a cue arrives.
- **Motion loops keep an NPC busy.** DJ Skulk's dance does, so his cue is sent twice, with a reply
  pre-delay longer than the gap, which de-duplicates it.

The result: a scene about every 3-4 minutes, each 7-20 s long. The room is quiet about 90% of the
time, and scenes never overlap.

### The paternity storyline

Seven short scenes, each self-contained, since players arrive mid-scene. A drudge couple argue over
whose their neon baby is. The dad accuses the neighbours (Splotch, the DJ, Gary), Denton keeps his
distance, and a rare scene (6% of starts) has Bexley read the results: it's a mutation. That restarts
the argument. The storyline teaches the mutation mechanic without a lecture.

### Baked looks

For whole-body mutation on an armoured NPC:

1. The normal NPC's rendered look is captured with `@et`.
2. It is stored as literal model and texture rows.
3. The palette rows are dropped and a 0x04 template is added.

The mutated Sawato Situation is built exactly like a bred armoured pet.

---

## 14. Test strategy

| Layer | Method | What it proves |
|---|---|---|
| **Maths** | Unit tests (`PetBreedingInheritanceTests`, `PetBreedingParityTests`) | Inheritance, rolls, caps, summon arithmetic, the area matcher, the draw-order contract |
| **Server/website parity** | REPLAY blobs through `@breed-replay` | The server and website models agree on real breeds |
| **Appearance** | `PetMutationServiceTests`; `@petdesc` in game | The palette write rule and the visibility maths; the render path actually taken |
| **Content data** | Generator + checker (`gen_paternity.py`, `check_paternity.py`); `SqlPatchSanityTests` | Every cue has one listener, no loops, no self-cues, distinct thresholds, no NULL-quest HearChat, ASCII, re-runnable |
| **Deployment fidelity** | The bundle run on test in a rolled-back transaction | All 2,388 weenie and placement rows reproduce exactly; test data unchanged |
| **Placement geometry** | Distance check over live placements | Every scene link within 60 m (the longest is 37 m) |
| **Gameplay** | In-game recipes with fast test settings (reference, sections 11.2 and 11.3) | Every gate, the spirit paths, maturity, imprint, consumables, naming |
| **Scenes** | The Scene Tester (click or exact phrase), then the real director | Every line fires in order; one scene at a time; the cadence |
| **Settings** | `@petserverconfig`, `@fetchlong`, shard script checks V1-V4 | Effective values, including stored overrides |

**Fast test configuration:** bond on, minimum bond 1, force mutation, guardian on, bypass charges and
cooldown, 5 kills to adulthood, trace on. Revert all of them after the session.

---

## 15. Deployment architecture

**Test is the source of truth for content.** Designers build on test, in game and with SQL, and prod
receives an exact copy:

1. **`deploy/export_annex_bundle.py` reads test**, then writes one world script
   (`Annex-Prod-Bundle.sql`):
   - **Weenies:** every weenie in the feature's ranges, copied as literal rows. Nothing is cloned from
     templates at import time, so prod can't drift.
   - **Placements:** every placement, with **fresh guids** computed on the target, so they can't
     collide with prod's own content.
   - **Excluded:** test-only NPCs.
2. **`deploy/Annex-Prod-Shard.sql`** creates the name-request table, writes the location, turns on the
   guardian and pins the test switches off. It has a wrong-database guard.
3. **Both scripts** run in a transaction, can be re-run, and end with verification queries.
4. **The individual `Database/Updates` files** remain the reviewable history, but **are not run on
   prod**. Older files re-run after newer ones would undo fixes.

The full procedure, the settings table and rollback are in `deploy/PROD_DEPLOY_PLAN.md`. A full test
backup is kept at `C:\Scripting\db-backups\2026-09-21-test-before-prod-restore\` for a rehearsal on a
prod copy.

---

## 16. Risks, known issues and open questions

### Known issues

| Id | Issue | Impact | Mitigation |
|---|---|---|---|
| H2 | A restart during a spirit fight loses the baby; the charge, rest time and consumables are already spent | Rare; tied to the restart schedule | Documented for players; possible fix: persist pending breeds |
| - | Refusals are transient errors that never reach chat | Players miss them | Documented; possible fix: echo to chat |
| - | Denied renames send no in-game message | Confusion | Documented; possible fix: notify on deny |
| - | Bexley's and Splotch's click pools (about 10 s) can drop a scene cue | A rare cut-off scene | Accepted |
| - | Actors' idle lines can land mid-scene | Cosmetic | Accepted; the fix is to move idle lines onto the director |
| - | Stale text: Ivo's "three things", two setting descriptions, the `PetIsMaleOverride` comment | Cosmetic and misleading | Fix in a content pass |

### Open questions (from the deploy plan)

| Q | Question |
|---|---|
| Q1 | Is `pet_bond_enabled` on in prod? **Yes** (from the prod dump). |
| Q2 | Should the guardian be on? **Yes.** |
| Q3 | Mutation decay? **0.1**, written by the shard script. |
| Q4 | Add an exit portal to the annex? **Yes**, back to Prof. Ruggan (in progress). |
| Q5 | Confirm the portal arrival point in game. **Confirmed**; facing turned toward Fenwick. |
| Q6 | Does prod have Prof. Ruggan at 0xDB3B? **Yes.** |
| Q7 | The placement leftovers: a duplicate Shreth, and two unplaced pets. **Intentional.** |
| Q8 | Change the code default for the location to 0x0106 / 2? **Done.** |
| Q9 | Update or delete the superseded docs and deploy files? **Deploy files deleted; old docs left.** |
| Q10 | Which branch does the prod build come from? **`feature/pet-breeding-motel`.** |

---

## 17. Future work

- **Persist pending breeds (fixes H2):** save the pending breed on the dam's device and resolve it at
  the next login or server start.
- **Echo breeding refusals to chat.**
- **Move actor idle lines onto the director**, so nothing ever talks over a scene.
- **Put the feud** (Bexley versus Splotch) **on the director** as scenes, instead of the removed
  openers.
- **78780256 Ancestral Gene Re-roller** (reserved).
- **78780205 Ruggan's Notes and 78780206 the knocked-over sign** (readable items).
- **An exit portal** back to Prof. Ruggan.
- **Restrict breeding to one room** by setting the area to a single verified cell.

---

## Appendix A: server settings

Default = code default. Prod = the value `deploy/Annex-Prod-Shard.sql` stores; a dash means it is
left at the default. Full behaviour, edge cases and code locations are in the reference, section 2.

| Setting | Default | Prod | Purpose |
|---|---|---|---|
| `pet_breeding_enabled` | true | true | Master switch |
| `pet_breeding_allowed_landblock` | 0x013A | **262** | Breeding area: 0 anywhere, a 16-bit landblock, or a 32-bit exact cell |
| `pet_breeding_allowed_variant` | 3 | **2** | Required variation (-1 any) |
| `pet_breeding_dance_sync_seconds` | 5.0 | - | Partner dance window |
| `pet_breeding_min_parent_level` | 100 | - | Minimum essence tier |
| `pet_breeding_min_bond` | 100 | - | Minimum bond on both parents (needs `pet_bond_enabled`) |
| `pet_breeding_allow_shiny` | false | - | Whether shiny essences may breed, and whether shiny is inherited |
| `pet_breeding_male_max_charges` | 10 | - | Stud charges |
| `pet_breeding_male_charge_reset_hours` | 24.0 | - | Stud refill interval (0 = never) |
| `pet_breeding_bypass_male_charges` | false | **false** | Test only |
| `pet_breeding_cooldown_hours` | 4.0 | - | Dam rest |
| `pet_breeding_bypass_female_cooldown` | false | **false** | Test only |
| `pet_breeding_dismiss_after_breed` | true | - | Dismiss the parents 2 s after the birth |
| `pet_breeding_base_mutation_chance` | 0.05 | - | Base stat mutation chance |
| `pet_breeding_mutation_decay_rate` | 0.0 | - | Diminishing returns per inherited stat mutation |
| `pet_breeding_mutation_min_floor` | 0.02 | - | Floor after decay |
| `pet_breeding_force_mutation` | false | **false** | Test only: force the stat roll |
| `pet_breeding_max_stat_mutations` | 0 | - | Per-line count cap (0 = none) |
| `pet_breeding_damage_mutation_step` | 10 | - | Damage rating per mutation (live at summon) |
| `pet_breeding_dr_mutation_step` | 10 | - | Damage resist rating per mutation (live) |
| `pet_breeding_crit_mutation_step` | 5 | - | Crit rating per mutation (live) |
| `pet_breeding_vitality_mutation_step` | 50 | - | Max health per mutation (live) |
| `pet_breeding_potency_mutation_chance` | 0.03 | - | Independent potency chance |
| `pet_breeding_potency_mutation_step` | 25 | - | Potency per mutation (baked at birth) |
| `pet_breeding_potency_soft_cap` | 1000 | - | Quarter gains at or above this |
| `pet_breeding_potency_hard_cap` | 0 | - | Cap, combined with `pet_potency_max_stored` (smallest positive) |
| `pet_breeding_guardian_enabled` | false | **true** | Spirit fight on mutated litters; also gates the Offering |
| `pet_breeding_guardian_template_wcid` | 7 | - | Spirit combat template |
| `pet_breeding_guardian_timeout_seconds` | 90 | - | Spirit lifetime (minimum 5) |
| `pet_breeding_guardian_health_mult` | 1.0 | - | x (both parents' max health) |
| `pet_breeding_guardian_damage_mult` | 0.5 | - | x (8% of defender max health, clamped 20-500) |
| `pet_breeding_guardian_translucency` | 0.6 | - | Spirit see-through amount |
| `pet_breeding_verbose_logging` | false | **false** | Logs every dance (spammable) |
| `pet_maturity_enabled` | true | - | Juvenile system |
| `pet_maturity_kills_required` | 300 | - | Kills to adulthood |
| `pet_maturity_stages` | 5 | - | Growth stages |
| `pet_maturity_stage_names` | Newborn,...,Young Adult | - | Stage names |
| `pet_maturity_min_damage_share` | 0.10 | - | Kill credit threshold (also gates juvenile bond XP) |
| `pet_maturity_juvenile_scale` | 0.5 | - | Stage-1 size |
| `pet_maturity_juvenile_strength` | 0.5 | - | Stage-1 strength |
| `pet_maturity_imprint_on_summon` | true | - | First summoner owns it |
| `pet_sex_icon_underlay_enabled` | true | - | Sex square on combat essences |
| `pet_sex_icon_underlay_male` | 0x06001B2F | - | Male square DID |
| `pet_sex_icon_underlay_female` | 0x06001B2D | - | Female square DID |
| `pet_trace` | false | **false** | Full session trace (debug) |
| `pet_visual_packet_debug` | false | **false** | ObjDesc logging (debug) |
| `content_template_export_wcid_start` | 78790000 | - | `@et` staging block start |
| `content_template_export_wcid_end` | 78799999 | - | `@et` staging block end |
| `content_template_export_next_wcid` | 0 | - | `@et` high-water mark |
| `content_template_export_auto_import` | true | - | `@et` loads into `ace_world` |
| `content_template_export_auto_discord` | true | - | `@et` posts to Discord |
| `pet_bond_enabled` (pre-existing) | **false** | **unknown (Q1)** | **Required on for breeding** |
| `pet_potency_enabled` (pre-existing) | false | unknown | Potency's combat effect |

**How to test each one:** change it live with `@modifybool`, `@modifylong`, `@modifydouble` or
`@modifystring`, then follow the matching recipe in the reference, section 2 (the "How to test" column)
and section 11.3. Read effective values with `@petserverconfig`.

---

## Appendix B: object properties

On the essence (DEV), unless marked. Full writers and readers are in the reference, section 3.

| Property | Id | Purpose |
|---|---|---|
| `PetMaleBreedingCharges` | Int 9057 | Stud charges left |
| `PetMaleChargesRefreshTime` | Float 9057 | Last refill time |
| `PetNextBreedingTime` | Float 9056 | Dam ready time |
| `PetNeutered` | Bool 9051 | Permanently cannot breed |
| `PetIsMaleOverride` | Bool 50053 | Sex override (unset = derived from the GUID) |
| `PetIsJuvenile` | Bool 50054 | Juvenile flag |
| `PetMaturityKills` | Int 9077 | Growth counter; its presence marks the essence bred |
| `PetMaturityXpMultiplier` | Float 9059 | Nurturing Draught |
| `PetIncenseBonus` | Float 9058 | Courtship Incense |
| `PetChromaticCatalystActive` | Bool 50055 | Chromatic Catalyst |
| `PetGuardianWeakened` | Bool 50056 | Offering of Subjugation |
| `PetMutDamageCount` ... `PetMutPotencyCount` | Int 9070-9074 | Mutation counts |
| `PetMutationCount`, `PetLastMutatedStat` | Int 9075, 9078 | Trace bookkeeping |
| Legacy `PetMut*Rating`, `PetMutVitality`, `PetMutPotency` | Int 9062, 9066-9069 | Read-only fallbacks for old essences |
| `PetBondAttuned`, `PetBondAttunedCharacterId`, `PetBondLevel`, `PetBondXp`, `PetBondXpTotal` | Bool 9047, Int64 9052, Int 9053, Int64 9050/9051 | Bond and ownership |
| `PetPotencyStored` | Int 9056 | Stored potency (baked) |
| `VisualOverride*` (setup, tables, palette base and template, clothing, icon, shade, scale) | DID 9033-9040, Int 9035, Float 9042/9043 | The pet's look at summon; the mutation colour is `VisualOverridePaletteTemplate` |
| `CapturedObjDescAnimParts` / `Palettes` / `Textures` | String 9011-9013 | Captured look recipe; Palettes is cleared by a mutation |
| `CapturedCreatureName`, `CapturedItems`, `CapturedCreatureVariant`, `CapturedCreatureType`, `CapturedCreatureWCID`, `CapturedSourceDamageType` | String 9009/9010, Int 9039/9037/9033/9054 | Species identity; travels with a tailored look |
| `PetCustomName` | String 9018 | Approved custom name |
| `ShowcaseColourCycleSeconds` (CRE) | Float 9060 | NPC colour cycle interval; never saved |
| Retail, reused: `PaletteTemplate`, `PaletteBase`, `DefaultScale`, `IconUnderlay`, `Translucency`, `Attuned`, `Bonded`, `Gear*`, `*Rating`, `HearLocalSignals`, `HearLocalSignalsRadius` | Int 3, DID 6, Float 39, DID 52, Float 76, Int 114/33, Int 370-375, Int 307-316, Int 290/291 | See the reference, 3.5 |

**How to test:** appraise an essence (the breeding, growth and mutation lines are built from these);
use `@pet-dump` to write the full device state to the log; use `@petdesc` for the visual set on a
summoned pet. Admin commands write most of them directly (reference, section 5.3).

---

## Appendix C: WCIDs

| Range | Contents |
|---|---|
| 78780200-78780204 | Fenwick, Ivo (vendor), DJ Skulk, Gary, Mrs. Ruggan |
| 78780210-78780215 | Bexley and the Registry pets, including the Certified Sawato Bandit |
| 78780220-78780225 | Splotch and the Ward pets, including The Sawato Situation (colour cycle on 221-225) |
| 78780230-78780233 | Mubb, Gorta, Mubb Junior, Denton |
| 78780240, 78780241 | Scene Director (hidden), Scene Tester (test only) |
| 78780250-78780255, 78780257 | Incense x3, Nurturing Draught, Chromatic Catalyst, Offering of Subjugation, Mutagenic Serum |
| 98760388 | Portal to Seedy Motel |
| 78780258-78780260 | Neutering Kit, Tailoring Kit, Tailoring Kit (Filled). Moved from 98760399-98760401 on 2026-09-21: prod uses those ids for other content. |
| 78790000-78799999 | `@et` staging block (temporary; never shipped) |
| Reserved | 78780205, 78780206, 78780256 |
