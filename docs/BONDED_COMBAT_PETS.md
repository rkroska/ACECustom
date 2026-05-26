## Bonded Combat Pets (Pokémon-style partner progression)

**STATUS: Design document for future implementation.** Describes planned player-facing features (bond XP, attunement, upkeep); not a guarantee that every item below is live on production. Bond-related code may exist behind server config (e.g. `pet_bond_enabled`, default off).

This document describes the **player-facing logic, story hooks, and balance intent** for a combat pet progression + upkeep system. It is written for content/design review (not code review).

---

## Summary (what players will experience)

- When you **apply a captured essence** to **re-skin** a combat **pet device**, that device becomes **attuned / bonded** to your character (same “make it yours” beat as **monster capture**). Every **combat pet** you summon from that device from then on is **that bonded partner**—progress lives on the gem, not on each temporary spawn.
- Bond **levels up automatically** from combat, based on **how much damage the pet dealt**; cap **1000**, not tied to player level.
- Bond makes pets **more stable** (survive longer, feel reliable) with **small helpful damage**—not a second full carry.
- Pets are **not meant to main-tank** endless hits; mitigation is **capped**, with optional mechanics so bond doesn’t become infinite damage reduction.
- Combat pets should **prefer attacking the owner’s current combat target** (assist behavior)—big fun / QoL win.
- Pet devices need upkeep: **full refill** of uses costs **25,000,000 banked pyreals** (anywhere).

---

## Core concepts

### 1) The bonded partner is the **Pet Device**
Bond progression is stored on the **summoning device item** (the pet gem), not on the temporary spawned pet creature.

Each Pet Device has:
- **Bond Level**: ranges from **0** up to **1000**
- **Bond XP**: progress toward the next level

---

### 2) Attunement / bonding trigger — **essence applied to re-skin** (aligned with monster capture)

Bonding is intentionally tied to **monster capture**, not to “first summon”:

- When a **captured essence** is successfully applied to re-skin **that** combat pet device — i.e. `ApplyAppearanceToCrate` (use essence on device) completes successfully — the device is immediately **attuned** and **bonded** to the applying character.
- **Terminology:** **Attuned** and **bonded** mean the same thing here: the gem is **locked to that character** and is now a **bonded partner device** (eligible for Bond XP and bond bonuses on summon).
- **Bond scope:** **character-bound**: only **that character** may use or progress that device’s bond.
- **Bond XP** accrues only after attunement (starts at Bond Level 0 / 1 per tuning—implementation detail). Player-facing story: **“Once the essence takes hold, the partnership is real.”**
- **Changing the skin later** (another essence) does **not** reset bond or remove attunement—you’ve already committed.

**Relationship to the spawned creature**

- The **CombatPet** in the world is ephemeral; it does **not** carry a separate bond flag. It is **always** the manifestation of **whatever pet device** summoned it—so if the device is **bonded**, the pet you see is **your bonded combat pet** for that session.

**Edge cases (design defaults)**

- **Generic / unskinned** combat pet devices remain **tradeable** until **attuned** (essence re-skin applied).
- **Attuned / bonded devices cannot be traded** (or placed in trade / vendors—same pattern as other bound gear).
- **Hollow Essence:** if it can apply appearance like the normal essence, decide whether it triggers bonding the same way (recommend: **yes**, if the goal is “any permanent visual commitment”; **no**, if hollow is treated as “storage copy only” and shouldn’t lock trade—pick one for consistency).

---

### 3) Trade economy (why character-bound fits)

- Players can still buy/sell **blank** summoning gems.
- **Leveled / bonded** partners stay **yours**—no market for “pre-grinded bond 900 devices.”
- Bonding at **re-skin** matches the fantasy: you didn’t just equip a gem—you **named the partnership** by applying a capture skin.

---

## How leveling works (automatic)

### 4) Bond XP comes from combat, based on pet damage
Bond XP is awarded when a monster dies. It is **automatic** and based on how much damage the pet actually contributed.

**XP is awarded only if the pet contributed meaningfully** to prevent “tag once and farm XP.”
- There will be a tunable **minimum contribution threshold**.

Conceptually:

Bond XP on a kill =
- **Base XP** (from monster difficulty)
× **Pet Damage Share** (pet damage / total relevant damage or a max-HP-based proxy)
× **Diminishing returns** (higher bond levels level slower)

Design intent:
- Early levels feel frequent and exciting.
- High levels become a long-term pursuit (a “long tail,” not linear power).

---

## What leveling gives (stability-first, not overpowered)

### 5) Primary rewards: stability and reliability
Bond Level increases should mainly improve:
- **Durability**
  - higher max health (percent-based)
  - small mitigation / damage reduction (**percent-based and hard-capped** so bond cannot become infinite tanking)
- **Consistency**
  - slight improvements that help the pet “stay relevant” without turning it into a second player

### 6) Secondary rewards: small helpful damage (percent-based)
Optional, but likely important for “feel”:
- a **small percent-based damage scalar** for the pet
- strongly capped and diminishing with level to avoid runaway power

Why percent-based:
- Your server scales broadly and long-term; flat values tend to break.

---

## Pets should not main-tank (unless we add a future “charm” mode)

### 7) Anti-infinite mitigation
Bond mitigation must never read as “my pet solos the boss.” Recommended guardrails:

- **Hard cap** on damage reduction coming from bond (and overall pet mitigation from this system).
- **Threat:** pet should not reliably hold boss-level aggro over a real tank unless you later add an explicit tank/charm stance.
- Optional later: **damage link** — the pet absorbs **a capped percentage** of the **owner’s** incoming damage so survivability has **cost** and pet death stays meaningful without wiping bond progress.

---

## Pet AI: assist the owner’s target

### 8) Prefer attacking what you’re attacking
Default combat pet behavior should **prioritize the owner’s current combat target** when valid (in range, attackable), falling back to existing targeting if not. This is a high **fun-per-complexity** improvement and reads as “trained partner.”

Future-friendly names if you add modes later: **Assist** (default), **Defend** (retaliate), etc.

---

## Preventing “double DPS” (balance safety valve)

### 9) Optional: Bond Strain (player tradeoff while pet is active)
To prevent pets from becoming mandatory meta, the system can apply a simple tradeoff:

- While a **combat pet is out**, the player receives **−X% outgoing damage** (tunable).

Design intent:
- Player + pet feels like a **different playstyle**, not strict power creep.
- Players who don’t want the system can simply not run a combat pet.

Note:
- This can ship in v1 or be added after observing live balance.

---

## Death, downtime, and “maintenance”

### 10) What happens if the pet dies?
- Pet death does **not** wipe Bond XP or Bond Level.
- The pet is gone until resummoned.

Design intent:
- Death is a normal gameplay setback (tempo loss), not a punitive progression loss.

---

## Upkeep: refilling uses with banked pyreals

**Implemented charm (per-charge):** [ADMIN_PET_SUMMON_CHARMS.md](ADMIN_PET_SUMMON_CHARMS.md) — WCID `78780030` enrolls the player to pay configured pyreals for **one** charge when summoning on empty structure (separate from the 25M full refill below).

### 11) Pet Device refills are a pyreal sink
Pet devices have uses/charges (Structure). When depleted, they must be refilled.

**Refill should be available anywhere** (no bank NPC requirement).

### 12) Cost
- **Full refill cost:** **25,000,000 banked pyreals** (≈ 10 MMD)

Design intent:
- A consistent and meaningful sink for common currency.
- Easy to understand (“pay once, full refill”) and easy to tune.

UX options (choose one, or support multiple):
- command-driven refill (simple)
- NPC service (story-friendly)
- use-action on the device (convenient)

---

## Monster capture integration (visual-only + bonding gate)

### 13) Capture remains appearance-only for combat
Monster Capture provides **cosmetics** for pet devices (appearance skins), not combat cloning.

### 14) Attunement is tied to the same moment as capture application
Successful **apply essence to pet device** (re-skin) = **attune / bond** + **character bind** (see §2).

### 15) Visual compatibility policy for combat pets (recommended)
To reduce “animation vs damage timing” complaints when applying wildly different skins:
- For **combat pets**, prefer a **safe mode** that avoids overriding motion/attack-critical animation timing.
- Allow cosmetic-only changes freely (name/icon/palette/texture-type elements).
- Optionally allow full overrides only for whitelisted “compatible body families.”

---

## Tuning and success criteria (what we will measure)

### What to observe
- Pet survival rate at various bond levels (is it meaningfully more stable?)
- Player downtime and refill frequency (is the sink functioning?)
- Clear speed comparisons:
  - player only
  - player + pet (low bond)
  - player + pet (mid bond)
  - player + pet (high bond)
- Pet **not** replacing tanks in hard content without intentional future systems.

### Success looks like
- Pets become noticeably more reliable by mid-bond levels.
- Pet damage feels helpful but not dominant.
- Players don’t feel forced to run a pet to be competitive.
- Refill cost feels like a real sink but not an oppressive chore.
- **Assist owner target** feels responsive and “smart.”

---

## Optional content hooks (story / UX)
- Name/theme: Bond, Attunement, Companion Training, Familiar Mastery, etc.
- NPC framing: stablemaster, professor, arcane binder, etc.
- Tutorial moment: **re-skin with essence** explains **attunement / bond**; later tips cover bond XP from pet damage, pyreal refill, and assist targeting.

---

## Player-facing patch note (draft)
“When you **apply a captured essence** to re-skin a combat pet summoning device, it becomes **attuned (bonded) to your character** and can earn **Bond XP** from combat based on **your pet’s damage**. Bond level (up to 1000) improves **stability** first, with capped mitigation so pets aren’t infinite tanks; pets **assist your target** by default. Full device refills cost **25,000,000 banked pyreals** anywhere.”
