# 🧪 ACE Server & Web Portal - Pet Breeding & Visualizer Master Test Plan

This document contains **exact step-by-step instructions** with in-game admin commands (`@create`, `@set`, `@appraise`), expected chat messages, and web portal verification steps. Check off each box (`[x]`) as you test!

---

## 📋 Section 1: In-Game Pet Breeding Engine (Seedy Motel & C# Server)

### Test 1.1: Breeding Ritual Trigger
The ritual requires **all** of the following. Use `@breed-debug` at any point to see which condition is failing.

| Condition | Rule |
|---|---|
| Location | Both players inside the approved landblock (`pet_breeding_allowed_landblock`) and variant |
| Landblock | Both players in the **same** landblock |
| Pets | Both pets summoned and sharing the **same landcell** |
| Dance | Both players danced within `pet_breeding_dance_sync_seconds` (default **5s**) of each other |
| Sex | Exactly one Male and one Female |

- [ ] **Step 1**: Log in two characters (Player A and Player B) on separate accounts.
- [ ] **Step 2**: Move both into the Seedy Motel and summon both combat pets.
- [ ] **Step 3**: Run `@appraise` on both devices and confirm one reads `Sex: Male` and the other `Sex: Female`. If not, use `@setsex male` / `@setsex female` on one.
- [ ] **Step 4**: Position both pets so they occupy the same landcell (stand the two owners together; pets follow).
- [ ] **Step 5**: Have both players type `/dance` within 5 seconds of each other.
- [ ] **Verification**: Confirm `WeddingBliss` particles play on both pets, `VisionUpWhite` on both players, and chat announces the birth.

**Negative cases** — each of these should NOT breed:
- [ ] Only one player dances (wait >5s, then have the other dance alone).
- [ ] Both dance but more than 5 seconds apart.
- [ ] Both dance in sync but the pets are in different landcells (walk one owner well away first).
- [ ] Both dance but one player is outside the approved landblock.
- [ ] Both devices are the same sex.

---

### Test 1.2: Male Breeding Charges & Regeneration
> Charge count is `pet_breeding_male_max_charges` (default **10**); the rest window is
> `pet_breeding_male_charge_reset_hours` (default **24h**, `0` disables regeneration).
>
> **Admin characters are subject to charges and cooldowns.** The admin bypass covers location
> checks only. Previously it also skipped the economy, so on admin characters charges never
> decremented and cooldowns were never written - the cap appeared not to work at all. Use
> `@pet-reset-cooldown` to reset both between test breeds.
>
> **Test-only bypass switches** (default false, isolate one rule at a time):
> - `@modifybool pet_breeding_bypass_male_charges true` - charges neither checked nor consumed
> - `@modifybool pet_breeding_bypass_female_cooldown true` - cooldown neither checked nor written
>
> To test the cooldown without waiting: `@modifydouble pet_breeding_cooldown_hours 0.02` (~72s),
> breed, retry immediately (refused: *"...is still recovering from her last litter. Ready in 0h 1m."*), wait ~72s,
> breed again (succeeds), then restore `pet_breeding_cooldown_hours 4`.

- [ ] **Step 1**: Target a Male pet device in inventory.
- [ ] **Step 2**: Run `@appraise` on the device and verify chat output shows `Sex: Male (10/10 breeding charges today)`.
- [ ] **Step 3**: Perform 10 consecutive breeds with different females.
- [ ] **Step 4**: Run `@appraise` after 10 breeds and confirm charges drop to `(0/10 Daily Charges) - refills in Xh Ym`.
- [ ] **Step 5**: Attempt an 11th breed with the same male.
- [ ] **Verification**: Confirm transient error message appears: *"[Device Name] has exhausted its 10 daily breeding charges. Rest for 24h."*

**Regeneration**
- [ ] **Step 6**: Shorten the rest window for testing: `@modifydouble pet_breeding_male_charge_reset_hours 0.02` (~72s).
- [ ] **Step 7**: Wait out the window, then `@appraise` the exhausted device again.
- [ ] **Verification**: Confirm charges have refilled to `(10/10 Daily Charges)` and that breeding works again. The refill happens lazily on the next read — no server restart or tick required.
- [ ] **Step 8**: Set `@modifydouble pet_breeding_male_charge_reset_hours 0` and confirm an exhausted device now stays at 0 indefinitely (regeneration disabled).
- [ ] **Step 9**: Restore: `@modifydouble pet_breeding_male_charge_reset_hours 24`.

---

### Test 1.3: Female Recovery Cooldown (`pet_breeding_cooldown_hours`, default 4h)
> The baby is **always placed in the female owner's inventory** (no coin flip). After a successful
> breed both parents' pets are dismissed ~2s later (`pet_breeding_dismiss_after_breed`, default true),
> so re-breeding requires a re-summon and its 45s use cooldown.

- [ ] **Step 0**: Breed once. Confirm the baby lands with the **female's** owner, and that ~2s after the birth particles both summoned pets vanish.
- [ ] **Step 1**: Breed once, then `@appraise` the **female** device.
- [ ] **Verification**: Confirm chat shows `Sex: Female (recovering - ready to breed in 3h 59m)`.
- [ ] **Step 2**: Attempt to breed the same female again right away.
- [ ] **Verification**: Confirm breeding fails with *"...is still recovering from her last litter."*
- [ ] **Step 3**: `@appraise` the **male** device and confirm it took a charge, not a cooldown.
- [ ] **Step 4** *(Admin Fast-Forward)*: Run `@set Float PetNextBreedingTime 0` on the female device.
- [ ] **Verification**: `@appraise` again and confirm it reads `Sex: Female (ready to breed)`.

---

### Test 1.3b: GUID-Derived Sex
Sex is computed from the device GUID, not stored, so every essence already in the database has one.
`PropertyBool.PetIsMaleOverride` is a three-state override: unset = derive, true = male, false = female.

- [ ] **Step 1**: `@appraise` several untouched pet devices and confirm each shows `Sex: Male` or `Sex: Female`.
- [ ] **Verification**: Confirm the split is roughly even across a dozen devices, with no alternating pattern.
- [ ] **Step 2**: Log out and back in, then re-appraise the same devices.
- [ ] **Verification**: Confirm every device reports the **same** sex as before — it must be stable forever.
- [ ] **Step 3**: Run `@setsex female` on a device that derived as Male; confirm the appraisal flips.
- [ ] **Step 4**: Run `@setsex derive` on it.
- [ ] **Verification**: Confirm it reverts to its original GUID-derived sex.
- [ ] **Step 5**: Breed a baby and appraise it.
- [ ] **Verification**: Confirm the baby also has a sex (derived from its own new GUID).

---

### Test 1.4: Mendelian 55/45 Stat Inheritance & Package-Deal Coupling
- [ ] **Step 1**: Spawn Parent A with high damage rating: `@create 25749` then `@set Int DamageRating 30`.
- [ ] **Step 2**: Spawn Parent B with low damage rating: `@create 25749` then `@set Int DamageRating 10`.
- [ ] **Step 3**: Perform 10 breeds between Parent A and Parent B.
- [ ] **Step 4**: Run `@appraise` on all 10 birthed baby devices.
- [ ] **Verification**: Confirm roughly 5–6 babies inherit Parent A's high stat (30 Damage) and 4–5 inherit Parent B's low stat (10 Damage). Confirm mutation counts are coupled 1-to-1 with the inherited stat (no ghost mutation counts like 30+10=40).

---

### Test 1.5: Per-Stat Mutation Cap Enforcement (`pet_breeding_max_stat_mutations`)
> **Note**: the cap now defaults to `0` (uncapped). This test only applies once a cap is configured.

- [ ] **Step 1**: Set a cap for testing: `@modifylong pet_breeding_max_stat_mutations 20`.
- [ ] **Step 2**: Create a pet device already at the cap: `@set Int PetMutDamageCount 20`.
- [ ] **Step 3**: Breed this pet repeatedly until a stat mutation rolls.
- [ ] **Verification**: Confirm `PetMutDamageCount` never exceeds 20. Any new stat mutation rolls onto eligible uncapped stats (DR, Crit, or Vitality).
- [ ] **Step 4**: Confirm the appraisal panel shows `Damage: +200 [20/20 Muts]` (20 x the `pet_breeding_damage_mutation_step` of 10).
- [ ] **Step 5**: Restore the default: `@modifylong pet_breeding_max_stat_mutations 0`.

---

### Test 1.6: Fixed Step-Size Mutation Boosts (config-driven)
- [ ] **Step 1**: Force mutations on every breed: `@modifybool pet_breeding_force_mutation true`.
- [ ] **Step 2**: Perform 5 breeds and appraise the babies.
- [ ] **Verification**: Confirm each stat boost is exactly its configured step (current defaults shown):
  - Damage Rating boost = `pet_breeding_damage_mutation_step` (**+10**)
  - Damage Resist Rating boost = `pet_breeding_dr_mutation_step` (**+10**)
  - Crit Rating boost = `pet_breeding_crit_mutation_step` (**+5**)
  - Vitality / Health boost = `pet_breeding_vitality_mutation_step` (**+200 HP**)
- [ ] **Step 3**: Change a step at runtime: `@modifylong pet_breeding_damage_mutation_step 25`.
- [ ] **Verification**: Re-appraise an **existing** bred pet. Because ratings are now stored as *counts* and evaluated as `count x step`, its Damage bonus should retroactively rescale (e.g. 3 muts: +30 -> +75). Confirm the same value applies to the summoned pet.
- [ ] **Step 4**: Restore: `@modifylong pet_breeding_damage_mutation_step 10` and `@modifybool pet_breeding_force_mutation false`.

---

### Test 1.7: Independent Potency Mutation Track (soft cap 1000, hard cap disabled)
> **Note**: `pet_breeding_potency_mutation_chance` now defaults to **3.0%**, the step to **+25**, and
> `pet_breeding_potency_hard_cap` to **0 (uncapped)**.

- [ ] **Step 1**: Spawn a pet device with 900 Potency: `@set Int PetPotencyStored 900`.
- [ ] **Step 2**: Raise the roll rate for testing: `@modifydouble pet_breeding_potency_mutation_chance 1.0`.
- [ ] **Verification**: Confirm Potency increases by **+25** per breed while below the soft cap of 1,000.
- [ ] **Step 3**: Set pet Potency above the soft cap: `@set Int PetPotencyStored 1500`.
- [ ] **Verification**: Confirm the diminishing step applies (**+6**, i.e. `step / 4` rounded down, min 1) and that Potency is *not* blocked, since the hard cap is disabled.
- [ ] **Step 4**: Enable a hard cap: `@modifylong pet_breeding_potency_hard_cap 2000` and set Potency to 1,995.
- [ ] **Verification**: Confirm the next Potency mutation clamps to exactly 2,000 rather than overshooting, and that no further Potency mutations are announced.
- [ ] **Step 5**: Restore: `@modifydouble pet_breeding_potency_mutation_chance 0.03` and `@modifylong pet_breeding_potency_hard_cap 0`.

---

### Test 1.8: Double Mutation Jackpot (stat + Potency on the same breed)
- [ ] **Step 1**: Force both rolls: `@modifybool pet_breeding_force_mutation true` and `@modifydouble pet_breeding_potency_mutation_chance 1.0`.
- [ ] **Step 2**: Perform a breed.
- [ ] **Verification**: Confirm the birth announcement lists **both** gains, e.g.
  *"[GENETIC MUTATION] Gained +25 Potency and +10 Damage Rating & Rare DAT Palette unlocked!"*
  (Previously the stat mutation silently overwrote the Potency line and only one was reported.)
- [ ] **Step 3**: Appraise the baby and confirm both `PetMutPotencyCount` and the rolled stat count incremented.
- [ ] **Step 4**: Restore: `@modifybool pet_breeding_force_mutation false` and `@modifydouble pet_breeding_potency_mutation_chance 0.03`.

---

### Test 1.9: Mutation Palette Visibility & `@mutate_pet` Re-roll
> Mutation palettes are drawn from a pool filtered on the FULL 2048-colour range (black slots < 40%,
> mean luminance >= 0.15). Palettes that are colourful in their first 256 entries but mostly black
> beyond that paint a creature near-black and read as "no change" - that was the original
> invisible-mutation bug.
>
> **Base vs template rule**: the mutation goes in `PaletteTemplate` (the overlay). `PaletteBase` must
> stay a palette the model already renders with - its native one, or the one it was captured with.
> Every in-game case that wrote the mutation into the base rendered nothing. `@mutate_pet` and
> breeding both write base=native / template=mutation, which is exactly what `@create` does.

- [ ] **Step 1**: Breed until a mutation lands, then summon the baby.
- [ ] **Verification**: The baby is visibly a different colour from its parent. The server log packet line shows `PaletteID` = the creature's native base (e.g. `0x04000FF0` for an Ursuin) and `PaletteTemplate` = the mutation, with two subpalettes `0/255` and `255/1`.
- [ ] **Step 2** *(re-roll an existing mutant)*: Appraise a mutated device and run `@mutate_pet` with **no arguments**.
- [ ] **Verification**: Chat prints a before/after diff. `VisualOverridePaletteTemplate` changes to the rolled palette, marked `(rolled from filtered pool)`. `VisualOverridePaletteBase` moves to the model's **native** palette (or is left unchanged for a model with no native one, e.g. Browerk), and `CapturedPalettes` becomes `(none)`. Re-summon and confirm the new colour.
- [ ] **Step 3**: Run `@mutate_pet 33556773 67111092` (the Drudge palette, proven on an Ursuin).
- [ ] **Verification**: The diff marks the palette `(explicit)` and the summoned Ursuin turns Drudge-coloured.
- [ ] **Step 4**: Target a **summoned** pet and run `@mutate_pet` with no arguments.
- [ ] **Verification**: It recolours live without re-summoning (`Applied to LIVE pet; client redraw forced.`).

---

### Test 1.9: Master 0x04 DAT Palette Assignment on Mutation
- [ ] **Step 1**: Perform a breed that triggers a color mutation.
- [ ] **Step 2**: Run `@appraise` on the baby pet device.
- [ ] **Verification**: Confirm `PaletteBase` is assigned a master 2,048-color DAT Palette ID starting with `0x04......` (e.g. `0x040001BE`), and the pet's 3D mesh renders with the rare mutated color palette!

---

### Test 1.10: Inventory Placement & Full Inventory Drop (Attunement)
- [ ] **Step 1**: Ensure player has empty inventory space and complete a breed.
- [ ] **Verification**: Confirm baby pet device appears in inventory and is marked attuned.
- [ ] **Step 2**: Fill inventory completely with pyreals/items so 0 slots remain.
- [ ] **Step 3**: Complete another breed.
- [ ] **Verification**: Confirm chat message states inventory was full, baby pet drops to the ground at player's feet, and the ground item is correctly attuned to the player.

---

### Test 1.11: Mating Guardian (Phase 2) - two admin characters
Config (all live via `@modify*`): `pet_breeding_guardian_enabled` (default **false**), `pet_breeding_guardian_timeout_seconds` (90), `pet_breeding_guardian_template_wcid` (7 = drudgeskulker), `pet_breeding_guardian_health_mult` (1.0), `pet_breeding_guardian_damage_mult` (0.5), `pet_breeding_guardian_translucency` (0.6; retail ghosts are 0.5-0.65, raise it if the spirit looks solid).

Setup (either admin):
- [ ] `@modifybool pet_breeding_guardian_enabled true`
- [ ] `@modifybool pet_breeding_force_mutation true` (guardian only spawns on mutation breeds)
- [ ] `@modifybool pet_breeding_bypass_male_charges true` and `@modifybool pet_breeding_bypass_female_cooldown true` (repeat breeds without waiting)
- [ ] `@modifybool pet_breeding_dismiss_after_breed false` for the first pass so the pets stay out after the fight (turn back on later)

**A. Spawn and appearance**
- [ ] Both admins in the Seedy Motel (LB 0x016C) with one male + one female pet summoned in the same landcell (`@setsex` on the devices if needed).
- [ ] Either admin: `@breed`.
- [ ] **Verify** both players get "The union stirs something... Spirit of <species> rises before <pet1> and <pet2>!".
- [ ] **Verify** a translucent creature named `Spirit of <donor species>` appears at pet 1's position wearing the **donor's model** with the **rolled mutation colour** (this is the exact look the baby will have). If it renders in the donor's normal colour, the palette rows were not cleared - report it.
- [ ] **Verify** no baby essence has been placed yet (inventory unchanged).
- [ ] Server log: `[PetBreeding] Mating guardian ... spawned ... hp=<pet1 max hp + pet2 max hp>, dmgRating=<baby dmg - 50>`.

**B. Only the parent pets can hurt it**
- [ ] Admin 1 melees / casts at the guardian. **Verify** 0 damage every hit and the health bar does not move.
- [ ] Summon a third, unrelated pet (a third character, or an admin with a second device after dismissing) and let it attack. **Verify** 0 damage.
- [ ] **Verify** both parent pets engage it on their own (`@breed-debug` / pet AI console if needed) and its health drops.
- [ ] **Verify** the guardian only ever swings at the pets, never at a player standing next to it.

**C. Kill path**
- [ ] Let the pets kill it. Do not `@smite` it: smite bypasses `TakeDamage`, so it proves nothing about the damage gate.
- [ ] **Verify** "Spirit of <species> yields to its parents and dissolves into light..." then the normal birth message to both players, baby in the **female owner's** inventory, with the mutation and the **same colour the guardian wore**.
- [ ] **Verify** no corpse, no loot on the ground, no XP or luminance message for either player.
- [ ] Server log: `slain after <n>s` - note the number; that is the tuning input for `pet_breeding_guardian_health_mult`.

**D. Timeout path (no-fail)**
- [ ] `@modifydouble pet_breeding_guardian_timeout_seconds 15`, `@modifydouble pet_breeding_guardian_health_mult 50` (unkillable in 15s).
- [ ] `@breed` again. Wait 15s.
- [ ] **Verify** "Spirit of <species> fades before it can be bested. The birth proceeds regardless." and the baby still arrives.
- [ ] Restore: timeout 90, health_mult 1.

**E. Pending-breed refusal**
- [ ] With a guardian standing (use the 50x health setting), `@breed` again from either admin.
- [ ] **Verify** "[Breeding] You already have a mating guardian to defeat. Finish that ritual first." and no second guardian / no charges consumed.

**F. Offline winner fallback**
- [ ] Spawn a guardian (50x health), then log the **female owner** out before it resolves (timeout or kill).
- [ ] **Verify** on resolution the baby drops **on the ground at the ritual spot**, attuned to the female owner, and the online partner sees "<name> is not online, so the baby was left where the ritual took place".

**F2. Hardening (added after review)**
- [ ] Spawn a guardian (50x health). An admin casts war magic and a life-magic harm at it. **Verify** zero damage and no health movement (only melee/missile was gated before).
- [ ] Stand next to the guardian doing nothing for 20s, then walk around it. **Verify** it never swings at a player, even when it has no pet in sight.
- [ ] Spawn a guardian, then `@delete` it (or both owners recall out until the landblock unloads). **Verify** both players get "...has vanished. The birth proceeds regardless." and the baby arrives; a fresh `@breed` afterwards is NOT refused with the pending-guardian message.
- [ ] Kill a guardian with the pets. **Verify** no Siphon Lens, no dropped items, and the parents ARE dismissed 2s after the birth (previously the post-combat recall block vetoed the dismiss).
- [ ] Log noise: with `pet_breeding_verbose_logging` and `pet_visual_packet_debug` both false (defaults), dance repeatedly and summon pets. **Verify** no "[PetBreeding] Breeding trigger received" or "[CREATURE PACKET DEBUG]" lines in the log. Set either true to get them back.

**G. Disabled = Phase 1 behaviour**
- [ ] `@modifybool pet_breeding_guardian_enabled false`, `@breed`. **Verify** immediate birth with no guardian.
- [ ] Non-mutation breeds (`pet_breeding_force_mutation false`) never spawn a guardian even when enabled.

Cleanup: `@modifybool pet_breeding_force_mutation false`, `@modifybool pet_breeding_bypass_male_charges false`, `@modifybool pet_breeding_bypass_female_cooldown false`, `@modifybool pet_breeding_dismiss_after_breed true`. Leave `pet_breeding_guardian_enabled` at whatever you want live.

Known limits: a server restart drops any in-flight guardian breed (the parents already paid); `@smite` bypasses the damage gate; the spawn falls back to an immediate birth if the template weenie is missing or the pet has no location (logged as a warning).

---

### Test 1.12: Pet Maturity (juvenile growth)
Config: `pet_maturity_enabled` (default **true**), `pet_maturity_kills_required` (300), `pet_maturity_stages` (5), `pet_maturity_stage_names` ("Newborn,Whelp,Juvenile,Adolescent,Young Adult"), `pet_maturity_min_damage_share` (0.10), `pet_maturity_juvenile_scale` (0.5), `pet_maturity_juvenile_strength` (0.5). Only essences born from a breed are ever juvenile; existing essences are untouched.

- [ ] Breed a baby. **Verify** its ID panel reads `Sex: <sex> (newborn - cannot breed yet)`, `Growth: Newborn 1/5 - 60 kills to Whelp, 300 to Adult` and `Bond: Unbound (imprints on first summon)`.
- [ ] Summon it. **Verify** it is named `<You>'s Newborn <Creature>`, is half the adult size, has half the max health of the adult, and hits for half (ratings, base damage, and health are all scaled by the same percentage) (`@breed-debug` or the pet's ID).
- [ ] Try to breed with it. **Verify** the refusal: "...is still a newborn and cannot breed until it is an adult (0/300 kills)."
- [ ] With the juvenile out, kill a creature **below** its tier while it deals damage. **Verify** the kill count on ID does not move.
- [ ] Kill a creature **at or above** its tier where the pet deals at least 10% of the damage. **Verify** the count increments by 1 (log: `[PetMaturity] ...` only on stage changes; check ID).
- [ ] `@pet-set-maturity 59` then one qualifying kill. **Verify** "has grown into a Whelp! Its body swells with new strength. (2/5)", nearby players see "<pet> shudders and swells as it grows into a Whelp!" in local chat, and the pet is renamed `<You>'s Whelp <Creature>`, a level-up particle, and the pet visibly grows and heals to full without a resummon.
- [ ] `@pet-set-maturity 299` then one qualifying kill. **Verify** "has reached adulthood!", the stage tag drops from the name, full size and ratings, ID reads `Growth: Adult`, nearby players see "<pet> lets out a roar and rises to its full size!", and it can now breed.
- [ ] `@pet-set-maturity juvenile` on a summoned adult. **Verify** it shrinks and renames in place. `@pet-set-maturity adult` restores it.
- [ ] `@modifybool pet_maturity_enabled false`. **Verify** existing juveniles summon at full size and strength and can breed, and new babies are not born juvenile.
- [ ] Breed from a donor whose essence shows no explicit scale (most captured creatures). **Verify** the stage-1 juvenile is exactly half size, not a quarter, and the adult is full size. After a stage-up while summoned, **verify** its melee reach grew with it (it should not have to stand inside the target).

**Imprinting** (`pet_maturity_imprint_on_summon`, default **true**)
- [ ] Breed a baby. **Verify** its ID reads `Bond: Unbound - imprints on the first character to summon it` and it can be traded or dropped.
- [ ] Trade it to the other admin. They summon it. **Verify** "...has imprinted on you. It is now bound to this character...", the ID now reads `Bond: <name>`, and the client shows it attuned (cannot trade or drop).
- [ ] Have the original owner get it back via `@give` or similar and try to summon. **Verify** "This pet device is bonded to another character."
- [ ] Captured (non-bred) essences show no Bond line and behave exactly as before.

---

### Test 1.13: Pet Tailoring and Neutering Kits
Items: 98760399 Pet Neutering Kit, 98760400 Pet Tailoring Kit, 98760401 Pet Tailoring Kit (Filled). Load `Content/sql/weenies/98760399-98760401 Pet Tailoring and Neutering Kits.sql` into ace_world first (they did not exist before). `@ci 98760400` etc.

- [ ] **Extract**: use a Tailoring Kit on a captured combat essence (pet NOT summoned). **Verify** a `Pet Tailoring Kit (<Creature>)` appears in your pack showing the creature's portrait, the source essence and the tool are gone.
- [ ] **Extract, pack full**: fill your pack, repeat. **Verify** "Your pack is full..." and the source essence and tool are both still there (nothing consumed).
- [ ] **Extract, pet summoned**: summon the source, use the kit. **Verify** the refusal and nothing consumed.
- [ ] **Apply**: use the filled kit on a different combat essence with different stats/potency/mutations. **Verify** name, portrait icon and Use text update immediately without relog; the essence keeps its own stats, potency, bond, sex, mutation counts, growth and imprint; the kit is consumed once. Summon it: it looks exactly like the extracted creature (model, colour, size, equipment).
- [ ] **Apply to a passive pet crate**: **verify** refused (combat essences only).
- [ ] **Shiny / mutation colour**: extracting a shiny or a mutated essence carries the variant and palette. **Verify** the tailored target shows `(shiny - cannot breed)` when a shiny look was applied.
- [ ] **Neuter**: use a Neutering Kit on a combat essence. **Verify** ID shows `Sex: <sex> (neutered - cannot breed)` and breeding refuses it.

---

## 📋 Section 2: Summoned Combat Pet Stat Scaling (CombatPet.cs)

### Test 2.1: Mutated Rating Application at Summon Time
> All four mutated lines are now evaluated as `count x step` at summon time. The stored
> `DamageRating` / `DamageResistRating` / `CritRating` values on the device are **not** read by
> `CombatPet` — only the `PetMut*Count` properties (with a legacy `PetMut*Rating / step` fallback).

- [ ] **Step 1**: Take a pet device with 3 Damage mutations and 2 Crit mutations: `@set Int PetMutDamageCount 3` and `@set Int PetMutCritCount 2`.
- [ ] **Step 2**: Use the pet device to summon the active combat pet into the world.
- [ ] **Step 3**: Run `@appraise` on the active summoned combat pet entity.
- [ ] **Verification**: Confirm the pet's stats include **+30** Damage Rating (3 x step 10) and **+10** Crit Rating (2 x step 5) on top of its `Gear*` base.

---

### Test 2.2: Mutated Vitality HP Boost Application
- [ ] **Step 1**: Take a pet device with 5 Vitality mutations: `@set Int PetMutVitalityCount 5`.
- [ ] **Step 2**: Use the device to summon the pet.
- [ ] **Verification**: Confirm summoned pet's Max HP (`Health.MaxValue`) is **1,000** points higher (5 x step 200) than an unmutated pet of the same level.
- [ ] **Step 3**: Retune the step at runtime: `@modifylong pet_breeding_vitality_mutation_step 300`, then re-summon the same pet.
- [ ] **Verification**: Confirm Max HP is now **1,500** higher. Vitality rescales retroactively exactly like the combat ratings — before this change it was read from the raw `Vitality` value and did **not** respond to step retunes.
- [ ] **Step 4**: Restore: `@modifylong pet_breeding_vitality_mutation_step 200`.

---

### Test 2.3: Potency Scaling in Combat
- [ ] **Step 1**: Summon a pet with 1,000 Potency.
- [ ] **Step 2**: Have the pet attack an enemy creature.
- [ ] **Verification**: Confirm pet spell tier / body part damage scales up according to `PetPotency.cs` formulas.

---

## 📋 Section 3: Web Portal SPA & 3D Visualizer (React & WebGL)

### Test 3.1: Pet Breeding Simulator Parity
- [ ] **Step 1**: Open Web Portal at `http://localhost:5001` or `http://76.237.151.184:5001`.
- [ ] **Step 2**: Navigate to **Pet Breeding Calculator** tab.
- [ ] **Step 3**: Click **Simulate Breeding Ritual**.
- [ ] **Verification**: Confirm terminal debug log displays step-by-step stat inheritance, 55/45 rolls, mutation checks, and DAT palette resolution matching C# server output.

---

### Test 3.2: 3D WebGL Model Preview & Palette Swatches
- [ ] **Step 1**: Select any birthed baby in the Breeding Simulator results list.
- [ ] **Verification**: Confirm 3D WebGL viewport renders the creature model with active rotation controls and displays color swatch HEX codes.

---

### Test 3.3: HTTP Safe Clipboard Copy Fallback
- [ ] **Step 1**: Open web portal over plain HTTP IP address: `http://76.237.151.184:5001`.
- [ ] **Step 2**: Click **Copy @create In-Game Command** or **Copy Full Debug Log**.
- [ ] **Verification**: Confirm green checkmark appears, text copies to system clipboard, and **NO browser console error** (`navigator.clipboard is undefined`) is thrown!

---

## 📋 Section 4: Automated Unit Tests & Admin Commands

### Test 4.1: Unit Test Suite Execution
- [ ] **Step 1**: Open PowerShell terminal in `Source` directory.
- [ ] **Step 2**: Run `dotnet test ACE.Server.Tests\ACE.Server.Tests.csproj --filter "FullyQualifiedName~PetBreedingTests"`.
- [ ] **Verification**: Confirm all 21 unit tests pass (`Passed: 21, Failed: 0`).

---

### Test 4.2: Admin Commands Verification
- [ ] **Step 1**: Execute `@create 25749 1 0x040001BE 0.5` in-game.
- [ ] **Verification**: Confirm pet device spawns with specified WCID and 0x04 DAT Palette ID!
