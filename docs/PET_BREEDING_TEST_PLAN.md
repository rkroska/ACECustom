# ACE Server & Web Portal - Pet Breeding & Visualizer Test Plan

> **SUPERSEDED (2026-09-21).** Kept for history. Use `PET_BREEDING_REFERENCE.md (section 11)`, `PET_BREEDING_TECHNICAL_DESIGN.md` and
> `PET_BREEDING_PLAYER_GUIDE.md`. This file predates the annex move to landblock 0x0106 variation 2
> and contains details that are now wrong.


Step-by-step manual tests with the real admin commands, expected chat text and portal checks. Tick
each box (`[x]`) as you go.

Conventions used throughout:

- "Appraise" means the client's ID/appraise action on an item (there is no `@appraise` command).
  The breeding lines are at the bottom of the ID panel.
- `@setproperty PropertyInt.<Name> <value>` (Developer) writes a property on the **last appraised**
  object. Types are `PropertyInt`, `PropertyInt64`, `PropertyFloat`, `PropertyBool`, `PropertyString`,
  `PropertyDataId`, `PropertyInstanceId`. `null` removes the property. Bond properties are accepted on
  combat pet devices only.
- Dancing means the soul emote: type `*dance*`, or use `@dance`.
- Pet devices for tests: `@ci 787801001` (Fire Skeleton Samurai Essence, tier 250), `@ci 49215`
  (Acid Skeleton Minion Essence, tier 100), `@ci 49213` (tier 50). `@ci 25749` spawns an Olthoi
  Harvester creature and is not a pet device.
- Config is read with `@showprops` and changed with `@modifybool` / `@modifylong` / `@modifydouble`.
  Stored values override code defaults, so check before assuming a default.
- Admin characters bypass **location** checks only. Sex, tier, bond, charges, cooldowns and the pack
  precheck all apply to admins. `@pet-reset-cooldown` resets charges and cooldown on the appraised
  device between breeds.

Test switches (default false; use one at a time and restore afterwards):
`pet_breeding_force_mutation`, `pet_breeding_bypass_male_charges`, `pet_breeding_bypass_female_cooldown`,
`pet_breeding_verbose_logging`, `pet_visual_packet_debug`, `pet_trace`.

`pet_trace` writes the copy-paste session trace (`[PetTrace]` records: every gate, roll, guardian event,
birth, consumable, AND every combat exchange server-wide) to the server log; see the developer guide,
section 15, for the record catalogue and how to extract a session (`findstr "[PetTrace]" ACE_Log.txt`).
While it is on, the plain `[PetBreeding]` / `[PetMaturity]` / `[PetTailoring]` INFO lines quoted in the
tests below are replaced by the equivalent trace records (`guardian.spawn`, `guardian.slain`, `birth`,
`maturity.kill`, ...). `@pet-dump [note]` writes a `device.dump` record for the appraised device at any
time, switch or not; use it before and after a breed to capture both parents and the baby.

---

## Section 1: In-Game Pet Breeding Engine

### Test 1.1: Breeding Ritual Trigger

| Condition | Rule |
|---|---|
| Location | Both players inside `pet_breeding_allowed_landblock` (default 0x013A, Seedy Motel) and `pet_breeding_allowed_variant` (default 3) |
| Landblock | Both players in the **same** landblock |
| Pets | Both pets summoned and sharing the **same landcell** |
| Dance | Both players danced within `pet_breeding_dance_sync_seconds` (default **5s**) |
| Sex | Exactly one Male and one Female |
| Tier / bond | Both essences tier >= `pet_breeding_min_parent_level` (100) and bond >= `pet_breeding_min_bond` (100) |
| Pack | The female's owner has a free main-pack slot and burden headroom |

- [ ] **Step 1**: Log in two characters (Player A and Player B) on separate accounts.
- [ ] **Step 2**: Take the motel portal (98760388) and confirm `@breed-debug` reports
  `Allowed Target: whole landblock 0x13A, Variant=3 => Location Match: VALID (Room OK)`. Summon both pets.
- [ ] **Step 3**: Appraise both devices and confirm one reads `Sex: Male` and the other `Sex: Female`.
  If not, appraise one and run `@setsex male` / `@setsex female`.
- [ ] **Step 4**: Stand the two owners together so the pets share a landcell.
- [ ] **Step 5**: Both players `*dance*` within 5 seconds of each other.
- [ ] **Verification**: Both see "The mating ritual has begun between <pet> and <pet>...", then
  "Congratulations! A baby pet has been born: <name>! Placed in <female owner>'s inventory.",
  `WeddingBliss` particles on both pets, `VisionUpWhite` on both players, and the stud's owner sees
  "[Breeding] <stud> spent a breeding charge: 9/10 left today."

**Negative cases** - each of these should NOT breed:
- [ ] Only one player dances. **Verify** they see "[Breeding] Your pet performs the courtship dance, waiting for a partner...".
- [ ] Both dance but more than 5 seconds apart.
- [ ] Both dance in sync but the pets are in different landcells (walk one owner well away first).
- [ ] Both dance but one player is outside the approved landblock (a non-admin character).
- [ ] Both devices are the same sex. **Verify** "Breeding cancelled: two males cannot breed. You need one male and one female."
- [ ] Type "attendance" or "dance party" in chat. **Verify** nothing triggers: only a message that is exactly `dance` (with or without `*`) counts.
- [ ] Dance twice within 2 seconds. **Verify** only one partner scan happens (with `pet_breeding_verbose_logging` true, one "Breeding trigger received" line).

**Partner selection in a crowd**
- [ ] Three players in the same cell, all dancing: A (male), B (female, on cooldown), C (female, ready). Dance A last.
  **Verify** A breeds with C, not the nearer-but-incompatible B.

---

### Test 1.2: Tier and Bond Gates
> `pet_breeding_min_parent_level` (default **100**) is the essence tier (50, 80, 100, 125, 150, 180,
> 200, 250, 300). `pet_breeding_min_bond` (default **100**) is `PetBondLevel`; a device without one
> counts as 1. Bond XP is only awarded with `pet_bond_enabled` true on an attuned device.

- [ ] **Step 1**: `@ci 49213` (tier 50). Appraise it, `@setproperty PropertyInt.PetBondLevel 100`, summon it and dance with a valid partner.
- [ ] **Verification**: Both players see "Parent pets must be at least tier 100 to breed." No charge is spent.
- [ ] **Step 2**: `@ci 49215` (tier 100) with bond untouched. Dance with a valid partner.
- [ ] **Verification**: "Parent pets must have a bond level of at least 100 to breed." No charge is spent.
- [ ] **Step 3**: Appraise it and `@setproperty PropertyInt.PetBondLevel 100`; re-summon and dance.
- [ ] **Verification**: The breed goes through (tier 100 is the lowest breedable tier).
- [ ] **Step 4**: With `pet_bond_enabled` false, kill creatures with a bonded pet. **Verify** the ID panel bond level does not move, so no fresh essence can reach 100.
- [ ] **Step 5**: Appraise a bred baby. **Verify** it was written at bond 1 (ID panel), so it needs both adulthood and bond 100 before it can breed.

---

### Test 1.3: Male Breeding Charges & Regeneration
> Charge count is `pet_breeding_male_max_charges` (default **10**); the rest window is
> `pet_breeding_male_charge_reset_hours` (default **24h**, `0` disables regeneration). The window runs
> from the **last refill stamp** (`PetMaleChargesRefreshTime`), which is written the first time an
> unstamped device is read, not from the first breed.
>
> To test the female cooldown without waiting: `@modifydouble pet_breeding_cooldown_hours 0.02` (~72s),
> breed, retry immediately (refused: "<dam> is still recovering from her last litter. Ready in 0h 1m."),
> wait ~72s, breed again (succeeds), then restore `pet_breeding_cooldown_hours 4`.

- [ ] **Step 1**: Appraise a fresh male device. **Verify** `Sex: Male (10/10 breeding charges today)` with no refill suffix.
- [ ] **Step 2**: Breed once. Appraise. **Verify** `Sex: Male (9/10 breeding charges today) - refills in 23h 59m`.
- [ ] **Step 3**: Perform 10 breeds with the same male (use `pet_breeding_bypass_female_cooldown` or several females).
- [ ] **Step 4**: Appraise. **Verify** `Sex: Male (0/10 breeding charges today) - refills in Xh Ym`.
- [ ] **Step 5**: Attempt an 11th breed.
- [ ] **Verification**: The stud's owner sees "<stud> has exhausted its 10 daily breeding charges. Rest for 24h."

**Regeneration**
- [ ] **Step 6**: `@modifydouble pet_breeding_male_charge_reset_hours 0.02` (~72s).
- [ ] **Step 7**: Wait out the window, then appraise the exhausted device again.
- [ ] **Verification**: `Sex: Male (10/10 breeding charges today)` and breeding works again. The refill happens lazily on the read; no restart or tick required.
- [ ] **Step 8**: `@modifydouble pet_breeding_male_charge_reset_hours 0`. **Verify** an exhausted device stays at 0 and shows no refill suffix.
- [ ] **Step 9**: Restore: `@modifydouble pet_breeding_male_charge_reset_hours 24`.

---

### Test 1.4: Female Recovery Cooldown (`pet_breeding_cooldown_hours`, default 4h)
> The baby is **always placed with the female's owner**. After a successful breed both parents' pets
> are dismissed ~2s later (`pet_breeding_dismiss_after_breed`, default true), so re-breeding requires
> a re-summon and its use cooldown.

- [ ] **Step 0**: Breed once. Confirm the baby lands with the **female's** owner, and that ~2s after the birth particles both summoned pets vanish.
- [ ] **Step 1**: Appraise the **female** device. **Verify** `Sex: Female (recovering - ready to breed in 3h 59m)`.
- [ ] **Step 2**: Try to breed the same female again. **Verify** "<dam> is still recovering from her last litter. Ready in 3h 59m." and the partner sees "Breeding cancelled: the female is still recovering from her last litter."
- [ ] **Step 3**: Appraise the **male** device and confirm it took a charge, not a cooldown.
- [ ] **Step 4** *(fast-forward)*: Appraise the female and run `@setproperty PropertyFloat.PetNextBreedingTime 0` (or `@pet-reset-cooldown`).
- [ ] **Verification**: Appraise again: `Sex: Female (ready to breed)`.

---

### Test 1.5: GUID-Derived Sex
Sex is computed from the device GUID, not stored, so every essence already in the database has one.
`PropertyBool.PetIsMaleOverride` is a three-state override: unset = derive, true = male, false = female.

- [ ] **Step 1**: Appraise several untouched pet devices and confirm each shows `Sex: Male` or `Sex: Female`.
- [ ] **Verification**: The split is roughly even across a dozen devices, with no alternating pattern.
- [ ] **Step 2**: Log out and back in, then re-appraise the same devices. **Verify** every device reports the same sex.
- [ ] **Step 3**: Appraise a device that derived as Male and run `@setsex female`; confirm the appraisal flips.
- [ ] **Step 4**: `@setsex derive`. **Verify** it reverts to its original GUID-derived sex.
- [ ] **Step 5**: Breed a baby and appraise it. **Verify** the baby also has a sex (derived from its own new GUID).

---

### Test 1.6: 55/45 Inheritance of Gear Ratings and Mutation Counts
> Each line is decided independently: the higher-effective parent is taken 55% of the time, ties go
> to device 1. Damage, DR and crit compare `Gear* + count * step` and the winner's gear **and** count
> travel together. Crit damage / crit resist / crit damage resist are gear-only. Vitality is
> count-only. Potency compares `PetPotencyStored` (missing = 0).

- [ ] **Step 1**: Parent A: `@ci 787801001`, appraise, `@setproperty PropertyInt.GearDamage 30`, `@setproperty PropertyInt.PetMutDamageCount 2`, `@setproperty PropertyInt.GearCritDamage 12`, `@setproperty PropertyInt.PetBondLevel 100`.
- [ ] **Step 2**: Parent B: `@ci 787801001`, appraise, `@setproperty PropertyInt.GearDamage 10`, `@setproperty PropertyInt.PetMutDamageCount 0`, `@setproperty PropertyInt.GearCritDamage 4`, `@setproperty PropertyInt.PetBondLevel 100`. Make them opposite sexes with `@setsex`.
- [ ] **Step 3**: Perform 10 breeds (use the bypass switches).
- [ ] **Step 4**: Appraise all 10 babies. The Combat Ratings block prints `* Damage: <total> (Base: <gear>, Mut: +<count x step>)`.
- [ ] **Verification**: Every baby shows either `Base: 30, Mut: +20` (A's package) or `Base: 10, Mut: +0` (B's package); never `Base: 30, Mut: +0` or `Base: 10, Mut: +20`. Roughly 5-6 of 10 take A.
- [ ] **Step 5**: Summon a baby that took A's damage line. **Verify** its Damage Rating is 50 (30 + 2 x 10) and its Crit Damage Rating is its inherited `GearCritDamage` plus 16 (0.8 x 20), with no crit resist bonus.
- [ ] **Step 6**: In `ace_shard.biota_properties_int` for a baby's GUID. **Verify** all five count rows (types 9070-9074) exist, zeros included, and there is no `DamageRating` (307), `CritRating` (313) or `Vitality` (9058) row.

**Potency: missing counts as 0**
- [ ] Parent A with `PetPotencyStored` 500, Parent B with the property removed (`@setproperty PropertyInt.PetPotencyStored null`). Breed 10 times.
- [ ] **Verification**: Babies have either 500 or 0 stored potency (about 55/45), never 150 or another default.

---

### Test 1.7: Per-Stat Mutation Cap Enforcement (`pet_breeding_max_stat_mutations`)
> The cap defaults to `0` (uncapped). This test only applies once a cap is configured.

- [ ] **Step 1**: `@modifylong pet_breeding_max_stat_mutations 20`.
- [ ] **Step 2**: Appraise a device and `@setproperty PropertyInt.PetMutDamageCount 20`.
- [ ] **Step 3**: Breed this pet with `pet_breeding_force_mutation` true.
- [ ] **Verification**: `PetMutDamageCount` never exceeds 20. New stat mutations land on DR, crit or vitality only.
- [ ] **Step 4**: The ID panel shows `* Damage:        +200 [20/20 Muts]`.
- [ ] **Step 5**: Restore: `@modifylong pet_breeding_max_stat_mutations 0`.

---

### Test 1.8: Fixed Step-Size Mutation Boosts (config-driven)
- [ ] **Step 1**: `@modifybool pet_breeding_force_mutation true`.
- [ ] **Step 2**: Perform 5 breeds and appraise the babies.
- [ ] **Verification**: Each stat boost is exactly its configured step (defaults): Damage **+10**, Damage Resist **+10**, Crit **+5**, Vitality **+50 HP**. The birth line reads e.g. "[GENETIC MUTATION] Gained +10 Damage Rating & Rare DAT Palette unlocked!"
- [ ] **Step 3**: `@modifylong pet_breeding_damage_mutation_step 25`.
- [ ] **Verification**: Re-appraise an **existing** bred pet with damage mutations. Because ratings are stored as counts, its Damage bonus rescales (3 muts: +30 -> +75). Re-summon and confirm the summoned pet shows the same.
- [ ] **Step 4**: Restore: `@modifylong pet_breeding_damage_mutation_step 10`, `@modifybool pet_breeding_force_mutation false`.

---

### Test 1.9: Independent Potency Mutation Track (soft cap 1000, hard cap disabled)
> `pet_breeding_potency_mutation_chance` defaults to **0.03**, the step to **+25**, the soft cap to
> **1000**, `pet_breeding_potency_hard_cap` to **0**. The effective hard cap is the smallest positive
> of that and `pet_potency_max_stored`. Incense and `pet_breeding_force_mutation` do not affect this roll.

- [ ] **Step 1**: Appraise a device and `@setproperty PropertyInt.PetPotencyStored 900`.
- [ ] **Step 2**: `@modifydouble pet_breeding_potency_mutation_chance 1.0`.
- [ ] **Verification**: Each breed announces "+25 Potency" while the baby's inherited potency is below 1,000, and the baby also gets a new palette even when no stat mutation rolled.
- [ ] **Step 3**: `@setproperty PropertyInt.PetPotencyStored 1500`.
- [ ] **Verification**: "+6 Potency" (25 / 4, min 1) and potency is not blocked, since the hard cap is disabled.
- [ ] **Step 4**: `@modifylong pet_breeding_potency_hard_cap 2000`, set stored potency to 1,995.
- [ ] **Verification**: The next potency mutation announces "+5 Potency" and lands on exactly 2,000; at 2,000 no potency mutation is announced at all.
- [ ] **Step 5**: `@modifylong pet_breeding_potency_hard_cap 0`, `@modifylong pet_potency_max_stored 1200`, stored potency 1,197.
- [ ] **Verification**: The global stored cap acts as the hard cap: "+3 Potency" (soft-capped step 6, clamped to the 3 remaining) to exactly 1,200.
- [ ] **Step 6**: `@modifylong pet_breeding_potency_mutation_step 100`, re-appraise an existing pet with potency mutations.
- [ ] **Verification**: Its stored potency does **not** change (potency is stored as a finished value); only the ID-panel "Mut: +" estimate changes.
- [ ] **Step 7**: Restore: chance 0.03, step 25, hard cap 0, `pet_potency_max_stored 0`.

---

### Test 1.10: Double Mutation (stat + Potency on the same breed)
- [ ] **Step 1**: `@modifybool pet_breeding_force_mutation true` and `@modifydouble pet_breeding_potency_mutation_chance 1.0`.
- [ ] **Step 2**: Perform a breed.
- [ ] **Verification**: The birth announcement lists **both** gains, e.g. "[GENETIC MUTATION] Gained +25 Potency and +10 Damage Rating & Rare DAT Palette unlocked!"
- [ ] **Step 3**: Appraise the baby and confirm both `PetMutPotencyCount` and the rolled stat count incremented, and `PetLastMutatedStat` is the stat's id (1 dmg, 2 DR, 3 crit, 4 vit; 5 only if potency was the last line).
- [ ] **Step 4**: Restore both switches.

---

### Test 1.11: Mutation Chance Formula, Floor and Decay
> `chance = clamp(max(min_floor, base / (1 + decay x inherited stat counts)) + incense, 0, 1)`.

- [ ] **Step 1**: `@modifydouble pet_breeding_mutation_decay_rate 0.5`. Breed two parents whose baby inherits 6 stat mutations, 200 times (a macro and the bypass switches).
- [ ] **Verification**: About 2.0-2.5% mutate (0.05 / 4 = 1.25% is below the 2% floor, so the floor applies).
- [ ] **Step 2**: `@modifydouble pet_breeding_mutation_min_floor 0`. Repeat. **Verify** ~1.25%.
- [ ] **Step 3**: Restore decay 0 and floor 0.02.

---

### Test 1.12: Mutation Palette Visibility & `@mutate_pet` Re-roll
> Mutation palettes are drawn from a pool filtered on the FULL 2048-colour range (black slots < 40%,
> mean luminance >= 0.15). The mutation goes in `PaletteTemplate` (the overlay); `PaletteBase` must
> stay a palette the model already renders with. `@mutate_pet` and breeding both write base=native /
> template=mutation, which is exactly what `@create` does.

- [ ] **Step 1**: Breed until a mutation lands, then summon the baby.
- [ ] **Verification**: The baby is visibly a different colour from its parent. With `pet_visual_packet_debug` true, the packet line shows `PaletteID` = the creature's native base and `PaletteTemplate` = the mutation, with two subpalettes.
- [ ] **Step 2**: Appraise a mutated device and run `@mutate_pet` with no arguments.
- [ ] **Verification**: Chat prints a before/after diff. `VisualOverridePaletteTemplate` changes to the rolled palette `(rolled from filtered pool)`, `VisualOverridePaletteBase` moves to the model's native palette (or is unchanged for a model with none), and `CapturedPalettes` becomes `(none)`. Re-summon and confirm the new colour.
- [ ] **Step 3**: `@mutate_pet 33556773 67111092` (the Drudge palette, proven on an Ursuin). **Verify** the diff marks the palette `(explicit)` and the summoned Ursuin turns Drudge-coloured.
- [ ] **Step 4**: Target a **summoned** pet and run `@mutate_pet` with no arguments. **Verify** it recolours live (`Applied to LIVE pet; client redraw forced.`).
- [ ] **Step 5**: Appraise a mutated device and run `@pet-cleanse-palette`. **Verify** "[Admin] Palette overrides (template, base, shade, captured palettes) cleansed on <name>..." and the next summon is the natural look, not a black or flat-coloured model.

**Chromatic Catalyst**
- [ ] Use a Chromatic Catalyst (78780254) on a parent device. **Verify** "You infuse <name> with the Chromatic Catalyst!..." and a second use is refused ("already infused").
- [ ] Breed with `pet_breeding_force_mutation` false until a non-mutation breed happens. **Verify** the catalyst is still on the device (appraise or a second use still says "already infused").
- [ ] Breed with `pet_breeding_force_mutation` true. **Verify** the baby's palette is a saturated one from the vibrant pool and the catalyst is now gone from the device.

**Mutagenic Serum (78780257, colour-only re-roll)**
Load `Database/Updates/World/2026-09-19-00-Pet-Mutagenic-Serum.sql` first; `@ci 78780257 5`. Appraise the target before and after each use and compare `VisualOverridePaletteTemplate` / `CapturedPalettes` with `@petdesc` or the ID panel.
- [ ] **Captured essence**: use a serum on a captured combat essence (pet NOT summoned). **Verify** "You inject <name> with the Mutagenic Serum. Its colour has changed; summon it to see the new look.", one serum is consumed, `VisualOverridePaletteTemplate` is a new value and `CapturedPalettes` is `(none)`. Summon it: it is a different colour from before.
- [ ] **Bred essence**: repeat on a baby that already carries a mutation colour. **Verify** the template changes again and the pet is a new colour on summon.
- [ ] **Juvenile**: repeat on a juvenile essence. **Verify** it is accepted, the colour changes, and the growth stage, scale and mutation counts on the ID panel are unchanged.
- [ ] **Colour only**: on each of the above, **verify** damage / DR / crit / vitality ratings, per-stat mutation counts, potency, bond, sex, name and lineage are identical before and after.
- [ ] **Summoned pet**: use a serum on an essence whose pet is out beside you. **Verify** "...Its colour has changed and your summoned pet has been recoloured." and the pet repaints in place without a re-summon.
- [ ] **Refusal, non-essence**: use a serum on a passive pet crate and on an ordinary item. **Verify** "The Mutagenic Serum can only be used on combat pet essences." and the stack count is unchanged.
- [ ] **Refusal does not consume**: after every refusal above, **verify** the serum stack is the same size as before.
- [ ] With `pet_trace` true, **verify** a `consumable.use` line with `property=VisualOverridePaletteTemplate`, `before=` the old palette (or `none`), `after=` the new palette and `consumed=true`; the refusal writes `consumed=false reason=target is not a combat pet essence`.

---

### Test 1.13: Delivery, Full Pack and Offline Owner
- [ ] **Step 1**: With a free main-pack slot, complete a breed. **Verify** the baby is in the female owner's pack and is **not** attuned (it can be dropped or traded; the ID panel reads `Bond: Unbound (imprints on first summon)`).
- [ ] **Step 2**: Fill the female owner's **main pack** (side packs may have room). Dance.
- [ ] **Verification**: Refused up front: she sees "Breeding cancelled: your main pack has no free slot for the baby. Free a slot and dance again.", her partner sees "Breeding cancelled: <name>'s main pack has no free slot for the baby.", and **no** charge, cooldown or incense is spent.
- [ ] **Step 3**: Load her to just under the burden limit. Dance. **Verify** "Breeding cancelled: you are too encumbered to carry the baby..." and nothing spent.
- [ ] **Step 4** *(guardian on, 50x health)*: Start a guardian fight, then fill every pack before it resolves.
- [ ] **Verification**: On resolution both players see "A baby pet has been born: <name>! <owner>'s packs are full, so the baby has been tucked away and will be in <owner>'s pack at their next login." Nothing is dropped on the ground. Relog: the baby is in the pack, unattuned.
- [ ] **Step 5** *(guardian on, 50x health)*: Start a guardian fight, then log the **female owner** out before it resolves.
- [ ] **Verification**: The online partner sees "...<owner> is not online, so the baby has been tucked away and will be in their pack at their next login." Log the owner back in: the baby is in the pack and summonable, and imprints on that summon.

---

### Test 1.14: Mating Guardian - two admin characters
Config (all live): `pet_breeding_guardian_enabled` (default **false**), `pet_breeding_guardian_timeout_seconds` (90, minimum 5), `pet_breeding_guardian_template_wcid` (7 = drudgeskulker), `pet_breeding_guardian_health_mult` (1.0), `pet_breeding_guardian_damage_mult` (0.5), `pet_breeding_guardian_translucency` (0.6).

Setup (either admin):
- [ ] `@modifybool pet_breeding_guardian_enabled true`
- [ ] `@modifybool pet_breeding_force_mutation true` (a guardian only spawns when a mutation rolled)
- [ ] `@modifybool pet_breeding_bypass_male_charges true` and `@modifybool pet_breeding_bypass_female_cooldown true`
- [ ] `@modifybool pet_breeding_dismiss_after_breed false` for the first pass so the pets stay out after the fight

**A. Spawn and appearance**
- [ ] Both admins in the motel with one male + one female pet summoned in the same landcell.
- [ ] Either admin: `@breed`. **Verify** both see "[Breeding] Breed was forced by an admin - dance ritual not active." then the ritual line.
- [ ] **Verify** both get "The union stirs something... Spirit of <species> rises before <pet1> and <pet2>! Only the two parents can harm it. They have 90s to bring it down together."
- [ ] **Verify** a translucent creature named `Spirit of <donor species>` appears in front of pet 1, clear of both pets (their radii plus a 1.5 m gap; behind it or near pet 2 if that spot is blocked), wearing the **donor's model** with the **rolled mutation colour**. If it renders in the donor's normal colour, the palette rows were not cleared - report it.
- [ ] **Verify** no baby essence has been placed yet.
- [ ] Server log: `[PetBreeding] Mating guardian ... spawned ... hp=<pet1 max + pet2 max>, dmgRating=<baby effective damage>, drRating=..., weakened=False, ..., timeout=90s`.

**B. Only the parent pets can hurt it**
- [ ] Admin 1 melees / casts war and life-magic harm at the guardian. **Verify** 0 damage every hit and the health bar does not move.
- [ ] Summon a third, unrelated pet (a third character) and let it attack. **Verify** 0 damage.
- [ ] **Verify** both parent pets engage it on their own and its health drops.
- [ ] **Verify** the guardian only ever swings at the pets, never at a player standing next to it, even after 20 s with no pet in sight.
- [ ] Dismiss and re-summon one parent pet mid-fight. **Verify** the new pet can still damage it.

**C. Damage scaling**
- [ ] Watch the pets' hits. **Verify** no single hit removes more than 10% of its max health, and a pet that would one-shot it needs roughly 15 hits.
- [ ] Watch the guardian's hits on a pet with 5,000 max health. **Verify** each base hit is about 200 (8% = 400, x 0.5). `@modifydouble pet_breeding_guardian_damage_mult 1.0` -> about 400; `2.0` -> about 800. Restore 0.5.

**D. Kill path and Awakened Blessing**
- [ ] Let the pets kill it. Do not `@smite` it: smite bypasses `TakeDamage`, so it proves nothing about the damage gate.
- [ ] **Verify** "Spirit of <species> yields to its parents and dissolves into light...", then "[Breeding] The Awakened Blessing stirs within the newborn: bonus <stat> mutation!", a `LevelUp` particle on both players, then the birth message whose mutation list ends with "Awakened Blessing: +<step> <stat>".
- [ ] Appraise the baby. **Verify** it carries the forced mutation **and** the blessing (two counts, or one count plus potency), with the **same colour the guardian wore**.
- [ ] Repeat with `pet_breeding_max_stat_mutations 1` and a baby already at 1 on every stat line and potency at the hard cap. **Verify** "[Breeding] The Awakened Blessing flares, but every mutation line has reached its limit." Restore the cap to 0.
- [ ] **Verify** no corpse, no loot on the ground, no XP or luminance message for either player.
- [ ] Server log: `slain after <n>s`.

**E. Timeout path (no-fail)**
- [ ] `@modifydouble pet_breeding_guardian_timeout_seconds 15`, `@modifydouble pet_breeding_guardian_health_mult 50`.
- [ ] `@breed` again. Wait 15 s.
- [ ] **Verify** "[Breeding] The spectral guardian dissolves back into the ether..." and the baby arrives **without** a blessing line.
- [ ] `@modifydouble pet_breeding_guardian_timeout_seconds 1`. **Verify** the spawn message still says 5s (the minimum). Restore timeout 90, health_mult 1.

**F. Pending-breed refusal**
- [ ] With a guardian standing (50x health), `@breed` again from either admin.
- [ ] **Verify** "[Breeding] You already have a mating guardian to defeat. Finish that ritual first." (the partner sees "A mating guardian is still standing from a previous ritual...") and no second guardian.

**G. Lost paths**
- [ ] Spawn a guardian (50x health), then `@delete` it. **Verify** "[Breeding] The spectral guardian dissolves back into the ether..." and the baby arrives; a fresh `@breed` afterwards is NOT refused with the pending-guardian message.
- [ ] Spawn a guardian, then `@smite` one **parent pet**. **Verify** the guardian dissolves immediately, the same message, and the baby arrives without a blessing.
- [ ] Both owners recall out until the landblock unloads. **Verify** the same resolution.

**H. Offering of Subjugation**
- [ ] With `pet_breeding_guardian_enabled` **false**, use an Offering (78780255) on a device. **Verify** "Mating guardians are not enabled on this server, so the Offering of Subjugation would have no effect. It was not consumed." and the item is still in the pack.
- [ ] Enable guardians, use it. **Verify** "You consecrate <name> with the Offering of Subjugation..." and a second use is refused.
- [ ] Breed without a mutation (`force_mutation` false, several tries). **Verify** the Offering stays on the device.
- [ ] Breed with a mutation. **Verify** the log line shows `weakened=True`, the guardian's hits are half the normal value, its per-hit cap is 25% and it falls in roughly 10-15 s; the property is gone from both devices afterwards.

**I. Disabled = immediate birth**
- [ ] `@modifybool pet_breeding_guardian_enabled false`, `@breed`. **Verify** immediate birth with no guardian.
- [ ] Non-mutation breeds never spawn a guardian even when enabled.
- [ ] Log noise: with `pet_breeding_verbose_logging` and `pet_visual_packet_debug` both false, dance repeatedly and summon pets. **Verify** no "[PetBreeding] Breeding trigger received" or "[CREATURE PACKET DEBUG]" lines.
- [ ] Trace off: with `pet_trace` false, dance, breed and fight. **Verify** no "[PetTrace]" line at all. Trace on: one `breed.trigger` per dance, a `breed.attempt` + `breed.config` + eight `breed.inherit` + two `breed.roll` + `breed.replay` + `breed.commit` per committed breed, `guardian.spawn`, one `combat.damage` per landed hit in the fight (with `gIn.*` on hits the guardian takes and `gOut.*` on hits it deals), `guardian.slain` with `fight.*` and the blessing pick, a second `breed.replay` (`phase=blessing`) and a `birth`. **Verify** `@breed-replay` passes on both `json=` values.

Cleanup: restore `pet_breeding_force_mutation`, both bypass switches and `pet_breeding_dismiss_after_breed`. Leave `pet_breeding_guardian_enabled` at whatever you want live.

Known limits: a server restart drops any in-flight guardian breed (the parents already paid); `@smite` on the guardian bypasses the damage gate; the spawn falls back to an immediate birth if the template weenie is missing or the pet has no location (logged as a warning).

---

### Test 1.15: Motel Healing Zone
> `CombatPet.IsInMotelOrEncounter()` is true inside the breeding area (same rule as breeding) or
> while the pet is a registered parent in a guardian fight.

- [ ] Outside the motel, use a healing kit on your own pet. **Verify** refused (`YouCantHealThat`).
- [ ] Inside the motel, use a healing kit on your own pet. **Verify** it heals, scaled up by pet/healer max health (up to 8x).
- [ ] Inside the motel, cast a beneficial life spell on your own pet. **Verify** it lands and heals.
- [ ] Inside the motel, cast a harmful spell on your own pet. **Verify** it is still refused.
- [ ] Inside the motel, heal **another** player's pet. **Verify** refused.
- [ ] Outside the motel with `pet_breeding_allowed_landblock 0`. **Verify** heals now work anywhere (restore afterwards).

---

### Test 1.16: Exact-Cell Location Gating
- [ ] `@breed-debug` in the ritual room and note `Cell=0x013A02AE` (or whichever cell). `@modifylong pet_breeding_allowed_landblock 20578990` (0x013A02AE).
- [ ] **Verify** `@breed-debug` now prints `Allowed Target: exact cell 0x13A02AE` and `VALID` in that cell, `INVALID` one cell over, and that a breed is refused one cell over for non-admins.
- [ ] `@modifylong pet_breeding_allowed_variant -1`. **Verify** breeding works in the non-instanced copy of the block too. Restore 3.
- [ ] `@modifylong pet_breeding_allowed_landblock 0`. **Verify** breeding works in town. Restore 314.

---

### Test 1.17: Pet Maturity (juvenile growth)
Config: `pet_maturity_enabled` (default **true**), `pet_maturity_kills_required` (300), `pet_maturity_stages` (5), `pet_maturity_stage_names` ("Newborn,Whelp,Juvenile,Adolescent,Young Adult"), `pet_maturity_min_damage_share` (0.10), `pet_maturity_juvenile_scale` (0.5), `pet_maturity_juvenile_strength` (0.5). Only essences born from a breed are ever juvenile.

- [ ] Breed a baby. **Verify** its ID panel reads `Sex: <sex> (newborn - cannot breed yet)`, `Growth: Newborn 1/5 - 60 kills to Whelp, 300 to Adult` and `Bond: Unbound (imprints on first summon)`.
- [ ] Summon it. **Verify** it is named `<You>'s Newborn <Creature>`, is half the adult size, has half the adult max health, and hits for half.
- [ ] Try to breed with it. **Verify**: "<name> is still a newborn and cannot breed until it is an adult (0/300 kills)."
- [ ] With the juvenile out, kill a creature **below** its tier while it deals damage. **Verify** the kill count on ID does not move.
- [ ] Kill a creature **at or above** its tier where the pet deals at least 10% of the damage. **Verify** the count increments by 1.
- [ ] `@pet-set-maturity 59` then one qualifying kill. **Verify** "<name> has grown into a Whelp! Its body swells with new strength. (2/5)", nearby players see "<pet> shudders and swells as it grows into a Whelp!", the pet is renamed `<You>'s Whelp <Creature>`, and it grows, heals to full and now fights at 60% without a resummon.
- [ ] `@pet-set-maturity 299` then one qualifying kill. **Verify** "<name> has reached adulthood! It stands at its full size, fights at full strength, and can now breed.", the stage tag drops from the name, ID reads `Growth: Adult`, nearby players see "<pet> lets out a roar and rises to its full size!".
- [ ] `@pet-set-maturity juvenile` on a summoned adult. **Verify** it shrinks and renames in place. `@pet-set-maturity adult` restores it.
- [ ] `@modifybool pet_maturity_enabled false`. **Verify** existing juveniles summon at full size and strength, and new babies are not born juvenile.
- [ ] Breed from a donor whose essence shows no explicit scale. **Verify** the stage-1 juvenile is exactly half size, not a quarter, and the adult is full size.

**Nurturing Draught**
- [ ] Use a Nurturing Draught (78780253) on an adult essence. **Verify** refused ("only be given to juvenile combat pets...") and not consumed.
- [ ] Use it on a juvenile. **Verify** "You administer the Nurturing Draught to <name>. It now earns 2x maturity kill credit until adulthood!" and a second use is refused.
- [ ] One qualifying kill. **Verify** the ID kill count rises by 2.
- [ ] `@pet-set-maturity 299`, one kill. **Verify** adulthood, and `PetMaturityXpMultiplier` is gone from the device.

**Imprinting** (`pet_maturity_imprint_on_summon`, default **true**)
- [ ] Trade a newborn to the other admin. They summon it. **Verify** "<name> has imprinted on you. It is now bound to this character and cannot be traded or dropped.", the ID now reads `Bond: <name>`, and the client shows it attuned.
- [ ] Appraise the imprinted device and clear only the trade flags (`@setproperty PropertyInt.Attuned null`, `@setproperty PropertyInt.Bonded null`) so it can be handed back; the original owner tries to summon it. **Verify** "This pet device is bonded to another character." (the imprint lives in `PetBondAttunedCharacterId`, which was not touched).
- [ ] Captured (non-bred) essences show no Bond line and behave exactly as before.

---

### Test 1.18: Pet Tailoring, Neutering and Courtship Incense
Items: 98760399 Pet Neutering Kit, 98760400 Pet Tailoring Kit, 98760401 Pet Tailoring Kit (Filled). Load `Database/Updates/World/2026-09-09-01-Pet-Tailoring-and-Neutering-Kits.sql` into ace_world first. `@ci 98760400` etc. Ivo (78780201) sells the two empty kits, all six breeding consumables and the Mutagenic Serum.

- [ ] **Extract**: use a Tailoring Kit on a captured combat essence (pet NOT summoned). **Verify** "You extract the appearance of <Creature> into the kit. The source essence is consumed." and a `Pet Tailoring Kit (<Creature>)` appears; the source essence and the tool are gone.
- [ ] **Extract, pack full**: **Verify** "Your pack is full. Make room for the filled kit before extracting." and nothing consumed.
- [ ] **Extract, pet summoned**: **Verify** "Dismiss <name>'s pet before extracting its appearance." and nothing consumed.
- [ ] **Apply**: use the filled kit on a different combat essence. **Verify** "You tailor the appearance onto <name>. Summon it to see the new look.", name, icon and Use text update without relog; stats, potency, bond, sex, mutation counts, growth and imprint are unchanged; the kit is consumed once. Summon it: it looks like the extracted creature.
- [ ] **Apply to a passive pet crate**: **Verify** refused ("...can only be applied to a combat pet essence.").
- [ ] **Shiny / mutation colour**: extracting a shiny or a mutated essence carries the variant and palette. **Verify** the tailored target shows `(shiny - cannot breed)` when a shiny look was applied.
- [ ] **Neuter**: use a Neutering Kit on a combat essence. **Verify** "You have permanently spayed/neutered <name>. It can no longer be used for breeding!", ID shows `Sex: <sex> (neutered - cannot breed)`, breeding refuses it ("Your pet is spayed/neutered and cannot breed.") and a second kit is refused ("already spayed/neutered").
- [ ] **Neuter while summoned**: **Verify** it is accepted (the neutering kit has no summoned check).

**Courtship Incense**
- [ ] Use Lesser Incense (78780250) on a device. **Verify** the +2.5% message; then Refined (78780251) replaces it; then Lesser again is refused ("already primed with equal or stronger Courtship Incense (+5%)").
- [ ] Use incense on a neutered device. **Verify** refused and not consumed.
- [ ] Prime both parents with Exquisite (+10% each), `@modifydouble pet_breeding_base_mutation_chance 0.40`, breed. **Verify** in the simulator or log that the stat chance is 0.60 (the incense sum is capped at +0.50, and here 0.40 + 0.20 = 0.60 is under the clamp of 1.0), and the incense is gone from both devices after the breed, mutation or not.
- [ ] Prime a parent, then trigger a **refused** breed (same sex). **Verify** the incense is still on the device.

---

### Test 1.19: Pet Naming
- [ ] Summon a pet and run `@pet-name Fluffy`. **Verify** "Submitting your request to rename <old> to "Fluffy"..." then "Your request to rename <old> to "Fluffy" has been submitted for staff review."
- [ ] Run it again with another name within 60 s. **Verify** "You may submit another pet name request in N seconds."
- [ ] After 60 s, `@pet-name Fluffier`. **Verify** "Your pending pet name request has been replaced: ..." (one pending request per character).
- [ ] `@pet-name ab`, `@pet-name <33 chars>`, `@pet-name Bad!Name`. **Verify** the length and character refusals.
- [ ] With no pet summoned and nothing selected. **Verify** "Summon the combat pet you want to rename, or select its device in your inventory."
- [ ] Portal, **Pet Name Approvals** page (`/pet-names`): as a portal admin, approve the request while the owner is online. **Verify** the device is renamed in the pack without relog and the page reports "Pet renamed to "Fluffier"."
- [ ] Approve while the owner is offline. **Verify** the rename is in the pack at next login.
- [ ] Deny a request. **Verify** the row shows denied with the note.
- [ ] Submit a request, then trade the essence away (unsummoned baby) or rename it via another approval, then approve. **Verify** the page shows "Request denied automatically: ..." (HTTP 409) and the essence is untouched.
- [ ] Sign in to the portal as a user without the `pet-naming` page. **Verify** the page's API returns 403.

---

## Section 2: Summoned Combat Pet Stat Scaling (CombatPet.cs)

### Test 2.1: Mutated Rating Application at Summon Time
> All mutated lines are evaluated as `count x step` at summon time. Crit damage, crit resist and
> crit damage resist get derived bonuses (0.8 x damage muts, 0.8 x DR muts, 0.6 x DR muts).

- [ ] **Step 1**: Appraise a device and run `@pet-set-mutations 3 2 2 0 0` (3 damage, 2 DR, 2 crit).
- [ ] **Step 2**: Summon it and appraise the summoned pet.
- [ ] **Verification**: Damage Rating = gear + **30**, Damage Resist = gear + **20**, Crit Rating = gear + **10**, Crit Damage = gear + **24**, Crit Resist = gear + **16**, Crit Damage Resist = gear + **12**.
- [ ] **Step 3**: The device ID panel shows `Total Mutations: 7` and the three lines under `--- Genetic Mutations ---`.

---

### Test 2.2: Mutated Vitality HP Boost Application
- [ ] **Step 1**: `@pet-set-mutations 0 0 0 5` (5 vitality mutations).
- [ ] **Step 2**: Summon the pet.
- [ ] **Verification**: Max HP is **250** higher (5 x step 50) than an unmutated adult of the same level.
- [ ] **Step 3**: `@modifylong pet_breeding_vitality_mutation_step 300`, re-summon.
- [ ] **Verification**: Max HP is now **1,500** higher.
- [ ] **Step 4**: Restore: `@modifylong pet_breeding_vitality_mutation_step 50`.

---

### Test 2.3: Maturity Strength Scaling
- [ ] Take a bred device with known mutations, `@pet-set-maturity 0`, summon. **Verify** ratings, vitality bonus, max health and hits are 50% of the adult values.
- [ ] `@pet-set-maturity 240` (stage 5). **Verify** 90%.
- [ ] `@pet-set-maturity adult`. **Verify** 100%.

---

### Test 2.4: Potency Scaling in Combat
- [ ] **Step 1**: Summon a pet with 1,000 Potency.
- [ ] **Step 2**: Have the pet attack an enemy creature.
- [ ] **Verification**: Pet damage scales according to the `PetPotency.cs` formulas (see `PET_POTENCY_AND_STRAIN.md`).

---

## Section 3: Web Portal SPA & 3D Visualizer

### Test 3.1: Pet Breeding Simulator Parity
- [ ] **Step 1**: Open the web portal and navigate to the **Pet Breeding Calculator**.
- [ ] **Step 2**: **Verify** the Breeding Config panel badge reads `LIVE SERVER` (it fetched `GET /api/visualizer/breeding-config`) and the values match `@showprops`. Stop the server API and reload: the badge reads `FALLBACK DEFAULTS`.
- [ ] **Step 3**: Set both parents' **Gear Base Ratings** and **Mutation Counts**, pick Courtship Incense tiers, and click **Perform *dance* Breeding Ritual**.
- [ ] **Verification**: The debug log shows one `[INHERIT ...]` line per line (A/B effective values, roll, `< 0.55` or `>= 0.55`, which parent), the stat chance line including incense, the `[POTENCY ROLL]` line with "incense ignored", the palette resolution, and a `Summoned (adult)` block whose numbers match Test 2.1 for the same counts.
- [ ] **Step 4**: Click **Run Model Self-Check**. **Verify** the log begins with `Breeding model self-check: ALL PASS` and every line is `PASS`.
- [ ] **Step 5**: Change a step with `@modifylong`, reload the page. **Verify** the simulator picks up the new value.

---

### Test 3.2: 3D WebGL Model Preview & Palette Swatches
- [ ] **Step 1**: Click any birthed baby in the results list.
- [ ] **Verification**: The 3D viewport renders the creature model with rotation controls and shows colour swatch HEX codes.

---

### Test 3.3: HTTP Safe Clipboard Copy Fallback
- [ ] **Step 1**: Open the web portal over plain HTTP by IP address.
- [ ] **Step 2**: Click **Copy @create Command** or **Copy Full Debug Log**.
- [ ] **Verification**: The button flips to its "Copied" state, the text is on the clipboard, and no `navigator.clipboard is undefined` console error.

---

### Test 3.4: Visualizer Endpoint Authorization
- [ ] Signed out: `GET /api/visualizer/breeding-config`, `/master-mutation-pool`, `/species-presets`. **Verify** 200.
- [ ] Signed out: `POST /api/visualizer/curation` and `POST /api/visualizer/save-screenshot`. **Verify** 401.
- [ ] Signed in as a portal user **without** the `world-viewer` page and not an admin. **Verify** both POSTs return 403.
- [ ] Signed in as a portal admin (or with `world-viewer`). **Verify** both POSTs succeed with valid bodies.

---

## Section 4: Automated Unit Tests & Admin Commands

### Test 4.1: Unit Test Suite Execution
- [ ] **Step 1**: Open a terminal in `Source`.
- [ ] **Step 2**: `dotnet test ACE.Server.Tests\ACE.Server.Tests.csproj --filter "FullyQualifiedName~PetBreeding"`.
- [ ] **Verification**: `PetBreedingInheritanceTests` (BreedingMath and MatchesBreedingArea) and `PetBreedingTests` all pass, 0 failed.
- [ ] **Step 3**: `dotnet test ACE.Server.Tests\ACE.Server.Tests.csproj --filter "FullyQualifiedName~EnumCollisionTests|FullyQualifiedName~SqlPatchSanityTests"`.
- [ ] **Verification**: All pass (no duplicate custom property ids; SQL patches are ASCII/LF and no `landblock_instance` INSERT names the generated column).

---

### Test 4.2: Admin Commands Verification
- [ ] `@create 7 1 0x040001BE 0.5`. **Verify** a Drudge Skulker spawns wearing the given 0x04 palette; this is the reference rendering path breeding and `@mutate_pet` copy.
- [ ] `@pet-set-mutations 2 0 1 0 1` on an appraised device. **Verify** "[Admin] Mutation counts on <name> set: dmg=2 dr=0 crit=1 vit=0 pot=1 (total 4)..." and the ID panel agrees.
- [ ] `@pet-set-mutations` with a negative or non-numeric value. **Verify** the usage line.
- [ ] `@breed-debug` as a **player**. **Verify** it prints the enable switch, your location and match, your pet's sex and charges/cooldown, and "Eligible breeding partners nearby: N" with **no** other players' names or positions.
- [ ] `@breed-debug` as an admin. **Verify** the extra "--- Other Online Players ---" block with names, distance, landblock and pet.
- [ ] `@pet-debug` (Developer). **Verify** the same output.
- [ ] `@dance`. **Verify** your character performs the dance emote and it counts for the ritual.
