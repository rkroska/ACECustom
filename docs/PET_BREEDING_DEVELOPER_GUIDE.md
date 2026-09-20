# Pet Breeding - Developer Guide

Audience: server developers. Covers breeding, mutation, the mating guardian, maturity, imprinting,
consumables, tailoring and neutering, pet naming and the web tooling. Everything here is on branch
`feature/pet-breeding-motel`.

Related: `PET_BREEDING_CONTENT_GUIDE.md` (content team), `PET_BREEDING_PLAYER_GUIDE.md` (players),
`PET_BREEDING_TEST_PLAN.md` (test steps), `PET_POTENCY_AND_STRAIN.md` (potency, a separate system).

---

## 1. Where the code lives

| File | Responsibility |
|---|---|
| `Source/ACE.Server/WorldObjects/PetDevice_Breeding.cs` | The breed itself: area rule (`MatchesBreedingArea`), gates, partner search, guardian spawn and callbacks, birth and delivery, ID panel block, diagnostics. `BreedingMath` (nested): the pure inheritance / mutation / cap maths as `Simulate`, `RollAwakenedBlessing` and `SummonedStats` (section 12). |
| `Source/ACE.Server/WorldObjects/PetDevice_BreedingReplay.cs` | `BreedingReplay`: parses, runs and reports a `[REPLAY]` blob from the website simulator (section 12). |
| `Source/ACE.Server/WorldObjects/PetDevice_Trace.cs`, `PetDevice_Trace_Combat.cs` | `PetTrace`: the `[PetTrace]` session trace (record format, device dump, breeding records) and its combat half (every hit, evade, DoT tick, heal and death), one switch `pet_trace` (section 15). |
| `Source/ACE.Server/WorldObjects/MatingGuardian.cs` | The ritual monster. `Creature` subclass with source-gated and scaled damage, pet-only targeting, no loot/XP/corpse, slain/lost callbacks, parent-death notification. |
| `Source/ACE.Server/Entity/DamageEvent.cs` | Guardian outgoing damage override (`attacker is MatingGuardian`). |
| `Source/ACE.Server/WorldObjects/PetDevice_Maturity.cs` | Juvenile growth (kill counter, stages, names, XP multiplier), imprinting, kill credit from creature deaths. |
| `Source/ACE.Server/WorldObjects/PetDevice.cs` | Device properties (`VisualOverride*`, `IsMale`, `IsShiny`), `SummonCreature`, `ApplyVisualOverridesTo`, bond attunement in `ActOnUse`. |
| `Source/ACE.Server/WorldObjects/CombatPet.cs` | `Init` rating pipeline, `PrepareMaturityForSummon`, `ApplyMaturity`, `MaturityDamageMult`, `IsInMotelOrEncounter`, parent-death hook in `Die`. |
| `Source/ACE.Server/WorldObjects/Player_Use.cs` | Neutering kit inline; dispatch to `PetTailoring`; the six consumables (78780250-78780255) and the Mutagenic Serum (78780257). |
| `Source/ACE.Server/Entity/PetTailoring.cs` | Tailoring kit extract/apply and the three kit WCID constants. |
| `Source/ACE.Server/WorldObjects/Healer.cs`, `Player_Magic.cs`, `WorldObject_Magic.cs` | Own-pet healing in the motel / encounter (kits, beneficial spells, heal scaling). |
| `Source/ACE.Server/Entity/MonsterCapture.cs` | Capture (siphon). Defines the "visual model" property set tailoring mirrors. |
| `Source/ACE.Server/Services/PetMutationService.cs` | Master and vibrant palette pools (DAT-derived, filtered), prewarmed at startup. |
| `Source/ACE.Server/WorldObjects/Creature_Networking.cs` | `CalculateObjDesc`: how a palette override reaches the client. `GetSetupDefaultPaletteId`. |
| `Source/ACE.Server/WorldObjects/Creature.cs` | `CanBeDamagedBy(WorldObject)` virtual, honoured by melee, missile, spell projectiles and life magic. |
| `Source/ACE.Server/WorldObjects/Creature_Death.cs` | `OnDeath` calls `PetDevice.CreditMaturityKills(this)`. |
| `Source/ACE.Server/WorldObjects/Player_Networking.cs`, `Player.cs` | Dance emote (`BroadcastMovement`) and typed-emote fallback (`HandleActionTalk`) stamp `LastDanceTime` and call `CheckMultiplayerBreeding`. |
| `Source/ACE.Server/Managers/PropertyManager.cs` | All `pet_breeding_*`, `pet_maturity_*` config. |
| `Source/ACE.Server/Command/Handlers/DeveloperCommands.cs` | `@breed`, `@breed-replay`, `@setsex`, `@pet-reset-cooldown`, `@pet-set-maturity`, `@pet-set-mutations`, `@pet-cleanse-palette`, `@pet-make-alpha`, `@mutate_pet`, `@petdesc`, `@pet-debug`, `@pet-dump`. |
| `Source/ACE.Server/Command/Handlers/PlayerCommands.cs` | `@dance`, `@breed-debug`, `@pet-name`. |
| `Source/ACE.Server/Entity/TemplateExport.cs`, `Command/Handlers/DeveloperContentCommands.cs` | `@export-template` / `@et` / `@ed template`: exports the selected live object (pet, monster, NPC, player, prop) as a weenie SQL template - rendered `ObjDesc` baked into anim part / palette / texture rows, held items as Wield rows, instance and pet bookkeeping stripped, `monster` (faithful) or `npc` (Ivo-block treatment) flavour. Ids from the temporary block `78790000`-`78799999` (`content_template_export_wcid_start` / `_end`, persisted high-water mark `_next_wcid`); the import commands refuse that block without `force`. Snapshot runs via `LandblockManager.RunOnThreadFor`. |
| `Source/ACE.Server/Controllers/PetNamingController.cs` | Portal approve/deny for name requests. |
| `Source/ACE.Server/Controllers/VisualizerController.cs`, `Services/VisualizerService.cs`, `Services/CurationService.cs` | 3D showroom data, `breeding-config`, curation and screenshots. |
| `Source/ACE.WebPortal/ClientApp/src/utils/breedingModel.ts`, `components/PetBreedingCalculator.tsx` | Website simulator: a TypeScript mirror of `BreedingMath` and the roll logic. |
| `Source/ACE.Entity/Enum/Properties/*.cs` | Custom property ids (section 9). |
| `Source/ACE.Server.Tests/PetBreedingInheritanceTests.cs`, `PetBreedingParityTests.cs`, `EnumCollisionTests.cs`, `SqlPatchSanityTests.cs` | Unit tests (section 13) and the web/server parity harness (section 12). |

Build and run notes are in `CLAUDE.md` at the repo root (Release x64 path, config-override rule, the
ASCII-only rule for anything the client renders).

---

## 2. The breed, end to end

### Entry

Two paths call `PetDevice.CheckMultiplayerBreeding(player, source)`:

- `Player_Networking.BroadcastMovement`: the `DrudgeDance` / `DrudgeDanceState` soul emote. The
  motion is the primary trigger.
- `Player.HandleActionTalk`: the typed-emote fallback. It fires only when the whole message, trimmed
  of whitespace and `*`, equals "dance" (case-insensitive), and only if the motion path did not just
  fire for the same emote (`LastSoulEmote` still within its animation length). Never loosen this to
  `Contains`; see AGENTS.md.

Both paths stamp `Player.LastDanceTime` and rate-limit the partner scan to one per 2 seconds per
player, because the scan walks every online player.

`@dance` (player command) simply performs `*dance*`. `@breed` (admin) calls the same method with
`forced: true`, which skips the partner's dance-window check and tells both owners the ritual was forced.

### Gates, in order

Everything before "Every gate has passed" returns without writing anything.

1. `pet_breeding_enabled`; player not trading; player has a `CombatPet` summoned; player has a location.
2. Location: `IsInBreedingArea(player)`. Admins bypass location checks only (area, partner landblock,
   pet landcell); they do not bypass sex, charges or cooldowns.
3. Device 1 resolved from the pet (`TryGetSummoningDevice`, then an inventory lookup by GUID).
4. Partner scan over all online players: not self, not trading, same landblock, inside the area,
   `LastDanceTime` within `pet_breeding_dance_sync_seconds` (skipped when forced), has a `CombatPet`,
   both pets have locations, both pets share a landcell. Every survivor is a room candidate.
5. Partner choice: candidates whose pairing looks compatible (opposite sex, neither neutered, neither
   juvenile, shiny rule, male has a charge, female off cooldown, not busy) are preferred; among them
   the nearest wins. If none is compatible the nearest room candidate is taken anyway so the player
   gets a proper refusal message rather than silence.
6. Neither player `IsBusy`; neither has a pending guardian breed.
7. Both players marked `IsBusy` (cleared in `finally`). Device 2 resolved.
8. Neutered (either); both devices still in their owner's inventory; tier resolves for both
   (`PetDeviceWcids.GetPetLevel`); `pet_breeding_min_parent_level` (default 100; tiers are
   50/80/100/125/150/180/200/250/300); `pet_breeding_min_bond` (default 100, `PetBondLevel ?? 1`; bond
   XP is only awarded with `pet_bond_enabled` on an attuned device, and `CompleteBirth` writes babies at
   bond 1); shiny (`pet_breeding_allow_shiny`); juvenile; exactly one male and one female.
9. Male charges (`GetAvailableMaleCharges`) and female cooldown (`PetNextBreedingTime`), each with a
   test bypass property.
10. Delivery precheck: the female's owner must have a free slot in the main pack
    (`GetFreeInventorySlots(false)`) and enough burden headroom for the heavier parent device.

### Commit and decision

After the last gate: the ritual message goes to both players, steps are read, and the inheritance
and rolls below produce a `PendingBreed` record. Then the male's charge is spent (owner told the
remainder), the female's cooldown written, the species donor rolled 50/50, the palette rolled if any
mutation landed, and both parent devices saved. `PendingBreed` exists so the birth can be deferred
(guardian) without recomputing or re-rolling anything.

Finally either `TrySpawnMatingGuardian(pending)` (any mutation, guardian enabled) or
`CompleteBirth(pending)` runs.

### Inheritance model (`PetDevice.BreedingMath`)

`BreedingMath` is a nested static class with no world state so the website and the unit tests can
mirror it exactly. Every line is decided independently with one uniform roll: the parent with the
higher effective value is taken when `roll < 0.55`, otherwise the lower; ties count device 1 as the
higher parent.

| Line | Compared on | Baby receives |
|---|---|---|
| Damage, damage resist, crit | `Gear* + count * step` | that parent's `Gear*` and count (package deal) |
| Crit damage, crit resist, crit damage resist | `Gear*` only | that parent's `Gear*` |
| Vitality | count only | that parent's count |
| Potency | `PetPotencyStored` (missing = 0) | that parent's stored value and potency count |

Counts fall back to `PetMut*Rating / step` (or `Vitality / step`) on legacy devices that predate the
count properties. `CompleteBirth` writes the inherited `Gear*` values (only when > 0, like loot) and
always writes all five counts, including zeros, so the fallback can never fire on a bred device. It
never writes `DamageRating`, `CritRating` or `Vitality` on the device.

### Mutation rolls

```
statChance = clamp(max(min_floor, base / (1 + decay * babyInheritedStatCounts)) + incense, 0, 1)
```

where `babyInheritedStatCounts` is the sum of the four stat counts the baby inherited (potency
excluded) and `incense` is the two parents' `PetIncenseBonus` summed and clamped to 0.5. One roll
against that; on success one line is chosen uniformly among the eligible stat lines (damage, DR,
crit, vitality; a line is eligible unless `pet_breeding_max_stat_mutations` caps it) and its count
goes up by one. `pet_breeding_force_mutation` forces this roll only.

Potency rolls separately against `pet_breeding_potency_mutation_chance` (no incense, not forced).
`BreedingMath.PotencyMutationStep`: the configured step, quartered (min 1) at or above the soft cap,
clamped so the hard cap is never overshot; 0 means capped, no mutation. The hard cap is
`ResolvePotencyHardCap`: the smallest positive of `pet_breeding_potency_hard_cap` and
`pet_potency_max_stored`.

A palette is rolled when either roll landed (a potency-only mutation recolours too). The pool is the
master pool, or the vibrant pool if either parent carries `PetChromaticCatalystActive`; the catalyst is
removed only when a palette was actually rolled. `PetIncenseBonus` is removed from both devices on
every committed breed. `PetGuardianWeakened` is read here but consumed in `TrySpawnMatingGuardian`.
`PetLastMutatedStat` records the last line changed (1 damage, 2 DR, 3 crit, 4 vitality, 5 potency).

### Birth and delivery

`CompleteBirth` creates the baby from the donor's weenie, copies the donor's visual overrides and
captured ObjDesc strings, writes gear, counts, potency, `PetMutationCount` (sum, only when > 0),
`PetLastMutatedStat`, the palette (section 4), and `MarkBornJuvenile`.

Delivery resolves the winner through `PlayerManager.GetOnlinePlayer` because a captured `Player`
reference cannot tell you it logged out (`Session` is never nulled). Online and
`TryCreateInInventoryWithNetworking` succeeds: normal birth message and particles. Otherwise (logged
out or every pack filled during a guardian fight) the baby is written straight into the winner's
persisted inventory (`ContainerId` = winner, `Location` null) and loads on next login; both players
get the "tucked away" message. The baby is never dropped on the ground and is not attuned; it imprints
on first summon like any newborn.

Both parents are dismissed two seconds later via `WorldManager.ActionQueue` (not the pets' own queues,
which are dropped if a pet died in the fight).

### Summon-time evaluation

`CombatPet.Init`: `Gear*` from the device, then `+ count * step` for damage, DR and crit, plus derived
crit lines from the mutation bonuses only (`0.8 * mutDmg` crit damage, `0.8 * mutDr` crit resist,
`0.6 * mutDr` crit damage resist), then bond bonuses, then `pet_combat_rating_mult_*`. Max health gets
`vitCount * vitality_step` (via `ApplyMaturity`). Maturity then scales ratings, health and outgoing
damage by the stage strength. Retuning a damage/DR/crit/vitality step rescales existing pets on the
next summon; the potency step does not, because potency is stored as a finished value.
`BuildBreedingAppraisalBlock` uses the same count-times-step rule; keep them in sync.

---

## 3. Sex, charges and cooldown

Sex is derived from the device GUID (murmur3 fmix32, low bit) unless `PropertyBool.PetIsMaleOverride`
is set. It is therefore stable, needs no data migration, and is a coin flip per essence.

Male: `PetMaleBreedingCharges` (int) and `PetMaleChargesRefreshTime` (float, unix). `GetAvailableMaleCharges`
refills to `pet_breeding_male_max_charges` once `pet_breeding_male_charge_reset_hours` have passed
since the stamp, and re-stamps. A never-stamped device is stamped on first read rather than refilled.
The appraisal reads with `persist=true`, so an ID can stamp or refill. Reset hours 0 = never refill.

Female: `PetNextBreedingTime` (float, unix). Written as now + `pet_breeding_cooldown_hours`.

Both bypass properties (`pet_breeding_bypass_male_charges`, `pet_breeding_bypass_female_cooldown`)
skip both the check and the write. They are test switches.

---

## 4. Appearance: how a mutation colour actually renders

This cost a long investigation; the rules are:

- The baby's `VisualOverridePaletteBase` must be a palette the model natively renders with. Use the
  setup's highres `DefaultPaletteId` via `Creature.GetSetupDefaultPaletteId` (reads `client_highres.dat`,
  cached; the portal DAT copies are stubs). Falling back to the inherited captured base is acceptable.
- The mutation palette goes in `VisualOverridePaletteTemplate`. `Creature.CalculateObjDesc` expands a
  full 0x04 palette in the template into two subpalette ranges covering all 2048 slots.
- That branch is unreachable if the creature's biota carries any `PropertiesPalette`, `PropertiesAnimPart`
  or `PropertiesTextureMap` rows. `ApplyCapturedObjDesc` populates those from the device's
  `CapturedObjDesc*` strings. `CompleteBirth` therefore removes `CapturedObjDescPalettes` from a
  mutated baby. Donors that also carry captured anim parts or textures (humanoids) will still early-return;
  that is a known limitation.
- `@create <wcid> 1 <palette> <shade>` works the same way and is the reference behaviour.
- `@pet-cleanse-palette` removes template, base, shade and captured palettes together; clearing only
  the template leaves the rewritten base with no subpalettes, which is not the natural look.

The palette pool is `PetMutationService.GetMasterPalettePool()`: every 0x04 palette in the DAT that
passes `IsUsableCreaturePalette` (2048 entries, under 40% black, mean luminance above 0.15).
`GetVibrantPalettePool()` is the catalyst's subset. Neither is species-filtered; the roll is a lottery.
The web showroom draws from the same pools.

`PetDevice.ApplyVisualOverridesTo(Creature)` dresses any creature in a device's look. It is used for
the summoned pet and for the guardian.

---

## 5. The mating guardian

Spawned by `TrySpawnMatingGuardian` when the breed rolled any mutation and `pet_breeding_guardian_enabled`.

- Built from the template weenie `pet_breeding_guardian_template_wcid` (default 7, drudgeskulker),
  dressed with `donor.ApplyVisualOverridesTo`, then given the rolled palette under the base/template rule.
  `Biota.PropertiesPalette` is cleared so the colour renders. Named "Spirit of <creature>", translucency
  from `pet_breeding_guardian_translucency`.
- Stats: level = max parent level; the six ratings are the offspring's effective values
  (`PendingBreed.Effective*`, gear + mutations as a summon would evaluate them); health =
  (pet1 max + pet2 max) x `health_mult`.
- Outgoing damage is overridden in `DamageEvent`: base hit = 8% of the defending pet's max health,
  clamped 20-500, x `pet_breeding_guardian_damage_mult`, x 0.5 if `IsWeakened`. `BaseDamageMod` is
  rewritten to match so crits scale with it (AGENTS.md rule 3).
- Incoming damage (`MatingGuardian.TakeDamage`): 0 unless `CanBeDamagedBy`; otherwise scaled by
  `clamp((maxHp / rawHit / 30) ^ 0.81, 0.01, 1)` so hard hitters do not one-shot it, x 2.5 when
  weakened, then capped per hit at 10% of max health (25% weakened), minimum 1.
- `NeutraliseTemplate` strips loot, XP, luminance, corpse, faction, generator ties, kill quests, and
  destroys any items the template instantiated; then `SetMonsterState()` recomputes the cached monster flags.
- Placement: `FindGuardianSpawnPosition` tries in front of / behind each pet at radius + radius + 1.5 m.
  Creatures do not push each other apart at placement, so this must be computed. `Home` is set after
  `EnterWorld` because placement can nudge the position.
- Registration order: `Bind`, `EnterWorld`, timeout chain on `WorldManager.ActionQueue` (minimum 5 s),
  and only then `pendingGuardianBreeds[guid] = pending`. Anything that throws before registration
  falls back to an immediate birth with no second baby possible. The Offering is consumed from both
  devices after registration.
- Damage gate: `CanBeDamagedBy` returns true only for the two parent pets, or any `CombatPet` owned by
  one of the two owners (a resummon mid-fight is adopted into the allowed set). Projectile sources are
  resolved through `ProjectileSource`. `TakeDamage`, `SpellProjectile.DamageTarget` and life-magic harm
  all consult it.
- Targeting: `FindNextTarget` picks the nearest parent pet; `HandleFindTarget` nulls any non-pet target
  every tick (players set as target by proximity wake-ups); `Sleep` is a no-op so it never idles.
- Resolution, exactly one of:
  - `OnGuardianSlain` (from `OnDeath`): "yields to its parents" message, then the **Awakened Blessing**:
    one extra mutation on a random eligible line among damage, DR, crit, vitality and potency (potency
    only if `PotencyMutationStep` > 0 under the same caps as the breed roll). If nothing is eligible a
    capped message is sent instead. Then `CompleteBirth`.
  - `OnGuardianTimeout` (world queue): guardian fades, "dissolves back into the ether", `CompleteBirth`
    without the blessing.
  - `OnGuardianLost` (from `Destroy` without a death, or `OnParentDied` when `CombatPet.Die` notifies
    the guardian that a parent pet died): same message, `CompleteBirth` without the blessing.
  All three remove the entry from `pendingGuardianBreeds` (a `ConcurrentDictionary` keyed by guardian
  GUID). The ritual can never lose a paid breed.
- `Die` is overridden to a minimal animation + destroy; no Siphon Lens, no emotes, no treasure.
- `CombatPet.IsInMotelOrEncounter()` is true inside the breeding area or while registered as a parent
  pet of a live guardian. `Healer`, `Player_Magic` (beneficial spells only; harmful spells on your own
  pet stay blocked by `CanDamage`) and `WorldObject_Magic` (health boosts scaled up to 8x by pet/caster
  max health) use it.
- A server restart drops in-memory pending breeds. The parents have paid at that point. Documented,
  not handled.

---

## 6. Maturity and imprinting

Properties on the device: `PetMaturityKills` (int, presence = "born from a breed"),
`PetIsJuvenile` (bool), `PetMaturityXpMultiplier` (float, Nurturing Draught). Config: `pet_maturity_*`.

- `MarkBornJuvenile` runs in `CompleteBirth`. The counter is always written; the juvenile flag only
  when `pet_maturity_enabled`. Captured essences never get either.
- Stage = `kills / ceil(required / stages) + 1`, clamped. `MaturityFraction` = `(stage - 1) / stages`.
  Scale and strength multipliers lerp from the juvenile values to 1.0 by that fraction: with defaults
  0.5, 0.6, 0.7, 0.8, 0.9 by stage and 1.0 for an adult.
- Summon: `PetDevice.SummonCreature` calls `CombatPet.PrepareMaturityForSummon` BEFORE `Init`, because
  `Pet.Init` enters the world (create packet, physics scale). It records the adult scale, shrinks
  `ObjScale`, and prefixes the stage name. `CombatPet.Init` then captures the adult ratings and health
  after its normal pipeline and calls `ApplyMaturity(grew:false)`.
- `ApplyMaturity` derives everything from the captured adult values, so it is idempotent and can run
  again on growth. With `grew:true` it also heals to full, updates `PhysicsObj` scale via
  `SetScaleStatic`, and broadcasts `GameMessageUpdateObject` so clients re-render.
- Outgoing damage: `CombatPet.MaturityDamageMult` is applied to `BaseDamage` in `DamageEvent`, before
  enrage and ratings. Pets do not cast, so there is no spell path.
- Credit: `Creature_Death.OnDeath` calls `PetDevice.CreditMaturityKills(victim)` for every creature
  death. It walks `DamageHistory`, credits each juvenile combat pet whose share is at least
  `pet_maturity_min_damage_share` and whose essence tier is at or below the victim's level. One credit
  per device per death. Players, combat pets and mating guardians never count as victims.
  `AddMaturityKill` adds `round(PetMaturityXpMultiplier)` kills (default 1, Draught 2) and removes the
  multiplier at adulthood.
- Bond follows growth for a juvenile: the bond XP loop in `Creature_Death.OnDeath` applies the same
  two tests (share at or above `pet_maturity_min_damage_share`, victim level at or above the essence
  tier) before awarding, so a newborn cannot farm trivial mobs for bond. That matters because bond is
  what unlocks inherited potency - active potency is `ceil(bond / pet_potency_bond_divisor)` - so
  without the gate a baby born with high stored potency reached full strength on kills that were never
  good enough to grow it. Adults and captured essences keep the ordinary bond rules; when a kill is
  refused, the `maturity.kill` trace record's `reason` is the reason bond was skipped too.
- Imprint: `TryImprintOnSummon` runs after a successful summon. First summoner of a bred essence gets
  `PetBondAttuned`, `PetBondAttunedCharacterId`, `Attuned`, `Bonded`. `ActOnUse` refuses other characters
  for bred essences regardless of `pet_bond_enabled`.
- Stage names come from `pet_maturity_stage_names` (comma list). `ApplyMaturity` strips any known stage
  name before inserting the current one, so names never stack.

---

## 7. Consumables, tailoring and neutering

All are `Player_Use` source-on-target uses with the generic target-type check skipped for these
WCIDs; the target must be a `PetDevice` in the pack (not in the world). Each consumes the item with
`TryConsumeFromInventoryWithNetworking` before writing the property (AGENTS.md rule 1).

| WCID | Property written | Refusals | Consumed by |
|---|---|---|---|
| 78780250-52 Courtship Incense | `PetIncenseBonus` 0.025 / 0.05 / 0.10 | not a combat essence; neutered; existing bonus >= new | every committed breed |
| 78780253 Nurturing Draught | `PetMaturityXpMultiplier` 2.0 | not a combat essence; not juvenile; already >= 2.0 | adulthood |
| 78780254 Chromatic Catalyst | `PetChromaticCatalystActive` | not a combat essence; already active | a palette roll |
| 78780255 Offering of Subjugation | `PetGuardianWeakened` | not a combat essence; guardian disabled; already active | a guardian spawn |
| 78780257 Mutagenic Serum | `VisualOverridePaletteTemplate` (+ native `VisualOverridePaletteBase`, `CapturedObjDescPalettes` removed) via `PetMutationService.ApplyMutationPalette` | not a combat essence; master palette pool empty | immediately |

`78780256` is reserved and has no handler; `Player_Use` accepts 78780250-78780255 and
`PetMutationService.MutagenicSerumWcid` (78780257) only. The serum rolls with
`PetMutationService.TryRollMasterPalette` before consuming, writes the device with the same
`ApplyMutationPalette` that `@mutate_pet` uses (so the two cannot drift from the `CompleteBirth`
rule), and repaints the live pet with the `Pet` overload plus `ForceClientRedraw` only when the pet's
landblock group is the player's own.

Tailoring (`PetTailoring.HandleExtract` / `HandleApply`):

- Extract creates the filled kit, copies the visual set, places it in the pack, and only then consumes
  the source essence and tool. A failed pack placement leaves everything untouched.
- Apply writes only the visual set, rebuilds the name with `BuildDisplayNameAfterCaptureApply`, syncs the
  Use string, pushes name/icon/use to the client, then sends a full `GameMessageUpdateObject` and mirrors
  the slot move (the client caches inventory labels; per-property updates alone are not enough).
- `CopyVisuals` is one list used in both directions. Extend it there if capture gains a property.
- Both ends must be combat essences (passive crates normalise scale differently). Both refuse while the
  device's pet is summoned or the item is in the trade window.

Neutering (inline in `Player_Use`) sets `PropertyBool.PetNeutered` on any `PetDevice`; it only refuses
one that is already neutered. Breeding gate 8 and the incense refuse a neutered device; the ID panel
shows it.

---

## 8. Client-facing text

Everything that reaches the AC client must be 7-bit ASCII with `\n` line endings. `AppendLine` emits
`\r\n` and the client draws the CR as a music note; `BuildBreedingAppraisalBlock` and
`RunBreedingDiagnostics` strip it. Emoji render as boxes. See `CLAUDE.md`.

`RunBreedingDiagnostics` (`@breed-debug`, player; `@pet-debug`, developer) prints the enable switch,
the player's cell/variant against the configured area, the player's own pet (sex, charges or
recovery) and a count of other players with pets within 30 m or in the landblock. Per-player names,
distances and landblocks are printed only for admins.

---

## 9. Custom property ids

| Property | Id | Notes |
|---|---|---|
| PropertyInt.PetPotencyStored | 9056 | potency, a finished value |
| PropertyInt.PetMaleBreedingCharges | 9057 | |
| PropertyInt.PetMutCritRating .. PetMutPotency | 9062-9067 | legacy stored bonuses, fallback only |
| PropertyInt.PetMutDamageRating / PetMutDamageResistRating | 9068 / 9069 | legacy (were 9060/9061, collided with WeaponAug*) |
| PropertyInt.PetMutDamageCount .. PetMutPotencyCount | 9070-9074 | counts, the live model |
| PropertyInt.PetMutationCount | 9075 | sum, written for display (was 9058, collided with Vitality) |
| PropertyInt.EssenceSalvageYield | 9076 | (was 9057, collided with charges) |
| PropertyInt.PetMaturityKills | 9077 | presence = bred |
| PropertyInt.PetLastMutatedStat | 9078 | 1 dmg, 2 DR, 3 crit, 4 vit, 5 potency |
| PropertyInt.VisualOverridePaletteTemplate | 9035 | mutation colour |
| PropertyDataId.VisualOverridePaletteBase | 9036 | |
| PropertyFloat.PetNextBreedingTime | 9056 | |
| PropertyFloat.PetMaleChargesRefreshTime | 9057 | |
| PropertyFloat.PetIncenseBonus | 9058 | |
| PropertyFloat.PetMaturityXpMultiplier | 9059 | |
| PropertyBool.PetBondAttuned | 9047 | |
| PropertyBool.PetNeutered | 9051 | |
| PropertyBool.PetIsMaleOverride | 50053 | |
| PropertyBool.PetIsJuvenile | 50054 | |
| PropertyBool.PetChromaticCatalystActive | 50055 | |
| PropertyBool.PetGuardianWeakened | 50056 | |
| PropertyString.CapturedObjDescAnimParts / Palettes / Textures | 9011 / 9012 / 9013 | |

Before adding an id, grep the enum for the number; `EnumCollisionTests` fails the build on a
duplicate in the custom range. Three collisions shipped on this branch before that test existed.

---

## 10. Pet naming

`@pet-name <name>` (`PlayerCommands.HandlePetName`): 3-32 characters matching `^[a-zA-Z0-9' -]+$`,
targets the summoned pet's device or a selected combat essence the player possesses, refuses the
current name, and enforces a 60 s per-character cooldown taken before the background work starts.
The DB write (`pet_name_requests`, one pending row per character, rewritten in place) and the optional
Discord embed run on a `Task`; the reply is delivered through `WorldManager.EnqueueAction` with the
player re-resolved, because the world objects belong to the landblock thread.

`PetNamingController` (`/api/PetNaming`, `[Authorize]`, portal admin or `pet-naming` page access):
`GET requests`, `POST approve/{id}`, `POST deny/{id}`. Approve claims the row atomically
(`UPDATE ... WHERE status = pending`). If the owner is online the rename is enqueued on the world action
queue and awaited with a 10 s timeout; offline, the shard biota is edited directly. Both paths verify
the GUID is still a `PetDevice`, still possessed by the requesting character, and still carries the old
name; a mismatch auto-denies with a reason (HTTP 409), a transient failure returns the row to pending.
The portal page is **Pet Name Approvals** (`/pet-names`, `PetNameApprovals.tsx`).

---

## 11. Visualizer, curation and the simulator

`VisualizerController` (`/api/visualizer`): every `GET` (mesh, palettes, pools, `breeding-config`,
textures, curation reads, species presets, search) is `[AllowAnonymous]`. The two `POST`s, `curation`
and `save-screenshot`, are `[Authorize]` and additionally require `IsPortalAdmin` or the `world-viewer`
page access (`CanWrite()`), returning 403 otherwise. `GET breeding-config` returns the live chance,
floor, decay, steps, caps, `pet_potency_max_stored`, `force_mutation` and `guardian_enabled` values.

The **Pet Breeding Calculator** (`PetBreedingCalculator.tsx`) fetches `breeding-config` on load and
shows `LIVE SERVER` or `FALLBACK DEFAULTS` next to its config panel. `breedingModel.ts` is a line-for-line
mirror of `BreedingMath` and the roll logic (55/45 with ties to A, package-deal lines, gear-only crit
lines, potency missing = 0, floor/decay/incense formula, potency step and cap resolution, palette on any
mutation, `0.8/0.8/0.6` derived crit lines, maturity multipliers `0.5..0.9`) plus a campaign
simulator. **Any change to `BreedingMath`, the roll formula or `CombatPet.Init` must be mirrored
there.** `runSelfCheck()` (the "Run Model Self-Check" button) breeds a scripted pair with a fixed RNG
and prints PASS/FAIL lines against known outcomes; extend its expectations when the model changes.

---

## 12. Simulator parity harness

The website's breeding simulator (`Source/ACE.WebPortal/ClientApp/src/utils/breedingModel.ts`) and
the server's `PetDevice.BreedingMath` (`PetDevice_Breeding.cs`) implement the same maths. The harness
proves it and keeps it that way.

### How it works

- `BreedingMath.Simulate(in BreedingInputs, Func<double> nextDouble)` is the whole breed as one pure,
  deterministic function: parent genetics + config + options + a draw source in, `BreedingOutcome`
  (the baby and every intermediate decision) out. It never reads a device, a player, `ServerConfig`
  or the global RNG.
- The live breed (`CheckMultiplayerBreeding`) builds the inputs (`BreedingConfig.FromServerConfig`,
  `device.ReadBreedingGenetics`, incense/catalyst flags), calls `Simulate` with
  `ThreadSafeRandom.Next(0.0f, 1.0f)` as the draw source, and only then applies side effects
  (charges, cooldown, consumables, palette, guardian, `CompleteBirth`). It passes
  `GuardianKilled = false` because the guardian's fate is unknown; `OnGuardianSlain` later calls
  `BreedingMath.RollAwakenedBlessing`, the same helper `Simulate` runs for draw 12. The species donor
  coin flip and the palette pick stay in the live path and are not part of the model.
- `PetDevice.BreedingReplay` parses a `[REPLAY]` blob, feeds its inputs and draws through `Simulate`
  with a scripted draw source, and compares the result with the blob's `baby`. Every draw must be
  consumed and every gear value, count and stored potency must match.
- `Source/ACE.Server.Tests/PetBreedingParityTests.cs` runs three blobs captured from the live site and
  eight generated from `breedingModel.ts` with scripted draws (no mutation, soft cap, max-stored cap,
  per-line cap, incense, guardian disabled, guardian not killed, everything capped), plus the summon
  maths (`BreedingMath.SummonedStats`) including the juvenile multipliers at exact `.5` values.

### Draw order (the contract)

All draws are uniform doubles in `[0, 1)`. The `rngDraws` array in a blob is exactly this list.

1-8. Inheritance, one draw per line: damage, damageResist, crit, critDamage, critResist,
     critDamageResist, vitality, potency. `roll < 0.55` takes the parent with the higher effective
     value (ties favour parent A), else the lower.
9.   Stat mutation roll (always drawn; ignored when `pet_breeding_force_mutation` is on).
10.  Stat line pick, ONLY when a stat mutation happened and a line is under the per-line cap:
     `index = min(n - 1, floor(roll * n))` over `[dmg, dr, crit, vit]` minus capped lines.
11.  Potency mutation roll.
12.  Awakened Blessing pick, ONLY when the guardian spawned (enabled and the breed mutated) and was
     killed: eligible stat lines, then potency when its capped step is above 0.

Where one side skips a draw the other must skip it too; a leftover or missing draw is a FAIL.

### Rounding

JavaScript `Math.round` rounds `.5` up; C# `Math.Round` defaults to banker's rounding (`4.5 -> 4`).
Everything in `BreedingMath` that can land on `.5` (the juvenile multipliers `0.5 .. 0.9`, the derived
crit lines) goes through `BreedingMath.RoundHalfUp` (`MidpointRounding.AwayFromZero`).
`CombatPet.ApplyMaturity` must use the same mode. Never add a bare `Math.Round` on a rating.

### Capturing a REPLAY line from the site

1. Open the breeding simulator, tick the verbose log option, and run one breed.
2. The log ends with a line starting `[REPLAY] {"model":...}`. Copy everything from the `{` to the
   final `}` (one line, no spaces).

The server writes the same blob when `pet_trace` is on (section 15), as the `json=` value of a
`breed.replay` record: `phase=decision` after the decision (`guardianKilled:false`, draws 1-11) and
`phase=blessing` after a slain guardian (`guardianKilled:true`, draw 12 appended). Those lines replay
too: `@breed-replay` finds the `{...}` inside the record. (They used to sit under
`pet_breeding_verbose_logging`; that switch now covers only the dance-trigger and location lines.)

### Running @breed-replay

`@breed-replay <blob>` (developer access, works from the game client or the server console) runs
the blob through `BreedingMath.Simulate` and prints the baby, its adult and stage-1 summon ratings,
and `RESULT: PASS` or `RESULT: FAIL` with one line per differing field. The chat parser strips double
quotes from a command line; the handler re-quotes the blob, so pasting it as-is works. If the client
truncates a long paste, save the line(s) to a text file on the server and run
`@breed-replay file <path>`: every `[REPLAY]` line in the file is checked and a pass count printed.

### The rule

`breedingModel.ts` and `BreedingMath` change together, in the same PR, with `BREEDING_MODEL_VERSION`
bumped and the blobs in `PetBreedingParityTests.cs` regenerated. A parity test failure after a change
to one side means the other side was not updated; do not "fix" the test.

---

## 13. Tests

- `PetBreedingInheritanceTests`: `BreedingMath` (pick rule and 0.55 boundary, tie to device 1,
  effective value, package deal, gear-only lines, potency missing = 0 and count travels with stored,
  hard cap resolution, potency step below/at soft cap and clamped to the hard cap, derived crit lines)
  and `MatchesBreedingArea` (0 = anywhere, 16-bit = landblock, 32-bit = exact cell, variant filter).
- `PetTraceTests`: a `[PetTrace]` record round-trips its `event | session | t | k=v` shape, stays 7-bit
  ASCII on one line with no pipe inside a value, and parses from behind a log prefix (section 15).
- `EnumCollisionTests`: no duplicate ids in the custom property ranges.
- `SqlPatchSanityTests`: `Database/Updates/World` and `Shard` are 7-bit ASCII with LF line endings,
  and no `landblock_instance` INSERT names the generated `landblock` column.
- `TemplateExportTests`: the pure half of `@export-template` (order-insensitive argument parsing,
  lowest-free / high-water-mark wcid allocation and its refusals, `tmpl<wcid>_<slug>` class names,
  property filtering for creatures and the player whitelist, the npc overlay, held-item Wield rows,
  the narrow DELETE, and the ASCII / LF shape of the header and chat reply).

Run from `Source`: `dotnet test ACE.Server.Tests\ACE.Server.Tests.csproj --filter "FullyQualifiedName~PetBreeding"`.

---

## 14. Extension points and gotchas

- Add a breeding gate: insert it in `CheckMultiplayerBreeding` before the ritual message. Nothing after
  "Every gate has passed" may return without completing the breed.
- Add a visual property: add it to capture, to `ApplyVisualOverridesTo`, and to `PetTailoring.CopyVisuals`.
- Add a stat: add a count property, a step property, an `InheritLine` call in the decision phase,
  application in `CombatPet.Init`, capture in `matureXxx`, scaling in `ApplyMaturity`, display in the
  appraisal block, the `@pet-set-mutations` table, and the mirror in `breedingModel.ts`.
- Add a consumable: a WCID in the sinks patch, a branch in `Player_Use` (extend the range check), a
  property id, and a consumption point that runs only when the effect actually applied.
- Never enqueue a must-run action on a creature that may be gone; use `WorldManager.ActionQueue`.
- `IsMonster` / `IsFactionMob` are cached at construction. Call `SetMonsterState()` after changing
  `Attackable` or faction on a live creature.
- `ThreadSafeRandom.Next(min, max)` is inclusive of `max`; index pools with `Count - 1`.
- Logging: `pet_breeding_verbose_logging` and `pet_visual_packet_debug` gate the noisy lines. Keep new
  per-packet or per-death logs behind a switch. Anything a reviewer might need to recompute belongs in
  the `[PetTrace]` session trace (section 15) as a `k=v` record, guarded by `PetTrace.Enabled` before
  any string is built; do not add a second ad-hoc log line for a fact the trace already carries.

---

## 15. Session trace (`pet_trace`)

One switch, `pet_trace` (`@modifybool pet_trace true`, default false), turns on a server-side trace
that a shard owner can copy and paste to a third party so every value and every piece of arithmetic
in a breeding session can be checked without database access. It covers the whole breeding session
(consumables, every gate, the effective config, inheritance per line, the rolls, the REPLAY blob, the
guardian, the birth, maturity credit, tailoring, neutering) **and every combat exchange server-wide**
(every melee, missile and spell hit or miss, every DoT tick, every heal and every death, for players,
monsters, combat pets and the mating guardian).

**Volume warning.** With the switch on the server writes one line per swing, cast, tick, heal and
death for everyone online. It is for a controlled test session on a test shard, never for a live
shard. There is no second knob: turn it off when the session is done. With it off the cost is one
bool read per call site (every record is guarded before any string is built).

### Format

```
[PetTrace] <event> | session=<id> | t=<utc iso8601> | key=value | key=value | ...
```

- Logger `PetTrace` (log4net), level INFO, so the lines land in `ACE_Log.txt` next to everything
  else; `log4net.config.example` has a commented appender/logger pair that routes them to their own
  file (`PetTrace.txt`) instead.
- One line per record, 7-bit ASCII, no tabs. Values never contain `|` (replaced by `/`), line breaks
  or non-ASCII (replaced by `?`), so a line splits on ` | ` and each pair on the first `=`.
- Numbers are invariant-culture and round-trip (`R`), so `0.30000001` is the exact float the code
  compared. Guids are `0x` + 8 hex digits. Booleans are `true` / `false`, missing values `null`.
- `session=` is minted once per breed attempt (at the dance) and carried by every record of that
  attempt, including the guardian's records seconds later and every combat record the guardian is
  part of. Standalone events (consumables, kills, tailoring, deaths, ordinary combat) mint a fresh id
  per record or per exchange; correlate those on the device / creature guid keys.
- Many records carry a `...Rule=` key: a one-line statement of the formula the numbers beside it
  satisfy. It is documentation for the reader, not something the code evaluates.
- `PetTrace.TryParse` (and `PetTraceTests`) is the reference parser.

### Capturing and sharing a session

1. `@modifybool pet_trace true` on the test shard.
2. Optional: `@pet-dump before` with the parent device appraised (both parents), which writes a
   `device.dump` record whatever the switch says.
3. Run the breed (dance, or `@breed`), fight the guardian, let the birth land. Appraise the baby and
   `@pet-dump after`.
4. `@modifybool pet_trace false`.
5. Extract the lines. Windows: `findstr "[PetTrace]" ACE_Log.txt > trace.txt`, or for one attempt
   `findstr "session=3f9a1c2e" ACE_Log.txt`. Linux / Docker: `grep -F "[PetTrace]" ACE_Log.txt`.
   The `breed.replay` line's `json=` value runs unchanged through `@breed-replay` or the website
   simulator.
6. Paste `trace.txt`. Nothing in it needs the database: every number's inputs are on the same line
   or on an earlier line of the same session.

With the trace on, the plain `[PetBreeding]`, `[PetMaturity]` and `[PetTailoring]` INFO lines for
the same events (stud charges, guardian spawned / slain / lost / timed out, palette applied, deferred
delivery, growth, imprint, extract / apply) are replaced by the records below, so a fact is never
logged twice. `pet_breeding_verbose_logging` still gates the dance-trigger and location lines only.

### Breeding records

| Event | When | Keys (beyond `session`, `t`) |
|---|---|---|
| `breed.trigger` | every dance / `@breed` that reaches `CheckMultiplayerBreeding` | `p1.*`, `source`, `forced`, `cell`, `variant` |
| `breed.gate` | one per refusal, before or after the devices are known | `gate` (stable name: `enabled`, `trading`, `noPet`, `location`, `device1`, `partnerScan`, `busy`, `pendingGuardian`, `device2`, `neutered`, `inventory`, `tierLookup`, `minParentLevel`, `minBond`, `shiny`, `juvenile`, `sex`, `maleCharges`, `femaleCooldown`, `packSlot`, `burden`), `reason` (the same text the admin chat line shows, with the compared values), `p1.*`, `p2.*` |
| `breed.attempt` | once both devices are resolved, before the gates | both players (`p1.*`, `p2.*`), both pets (`pet1*`, `pet2*`, cells), and the full device block for each parent under `a.` / `b.`: guid, wcid, name, owner, tier, sex and override, neutered, shiny, bred, juvenile, stage, kills, bond, `chargesStored` / `chargesAvail` / `chargesRefresh`, `nextBreed`, `now`, every `gear*`, every `mut*` (as `ReadBreedingGenetics` resolves them), `potencyStored` / `potencyActive`, `incense`, `catalyst`, `weakened`, `xpMult`, setup / palette / variant |
| `breed.config` | right after `breed.attempt` | every `BreedingConfig` field (the same set `/api/visualizer/breeding-config` exposes) plus `resolvedHardCap`, `higherParentChance`, `pet_bond_enabled`, the area (`allowedLandblock`, `allowedVariant`), dance window, bond / tier minimums, charges, cooldown, both bypass switches, dismiss, shiny, maturity and every guardian setting |
| `breed.inherit` | eight records, one per line, draws 1-8 | `line`, `mode` (`gear+count`, `gearOnly`, `countOnly`, `stored+count`), `draw`, `a.gear` / `a.count` / `a.eff`, `b.*`, `step`, `higher`, `higherChance`, `roll`, `picked`, `baby.gear`, `baby.count`. For potency `gear` is the stored value. |
| `breed.roll` `kind=stat` | draw 9 (and 10) | `base`, `decay`, `inherited`, `decayed`, `floor`, `incenseA` / `incenseB` / `incense`, `chance`, `roll`, `forced`, `mutated`, `maxStatMutations`, `eligible`, `eligibleCount`, `pickDraw`, `pickRoll`, `pickIndex`, `line`, `lineName`, `step`, `allLinesCapped` |
| `breed.roll` `kind=potency` | draw 11 | `chance`, `draw`, `roll`, `mutated`, `storedBefore`, `stepConfig`, `softCap`, `softCapped`, `hardCapBreeding`, `maxStored`, `hardCap`, `step`, `applied`, `storedAfter`, `guardianSpawns` |
| `breed.replay` | after the decision and after a slain guardian | `phase` (`decision` / `blessing`), `json` = the `BreedingReplay.ToJson` blob (section 12) |
| `breed.commit` | after the side effects, before the guardian / birth | male and female device, winner, `maleChargesBefore` / `maleChargesAfter`, `now` / `femaleNextBreed`, both bypass flags, `incenseRemovedA/B`, `donorRoll`, `donor`, `babyWcid`, `mutated`, `mutations`, `catalystA/B`, `palettePool`, `paletteCount`, `paletteIndex`, `palette`, `catalystConsumed`, `guardianWeakened`, `guardianSpawns`, `lastMutatedStat`, `baby.*` genetics |
| `guardian.spawn` | guardian entered the world | `g.*`, `template`, both pets' level and max hp, `level`, `healthMult`, `maxHp`, the six ratings it was given, `damageMult`, `weakened`, the outgoing and incoming damage rules, `palette`, `translucency`, `cell`, `timeout` |
| `guardian.skipped` | spawn fell back to an immediate birth | `reason` |
| `consumable.consumed` | the Offering removed from a device at spawn | `item`, `property`, `device`, `consumedBy` |
| `guardian.slain` | parents killed it | `g.*`, `fight.*` (seconds, hits taken, raw vs applied totals, biggest hit, capped hits, hits dealt, damage dealt, health), the Awakened Blessing pick: `eligible`, `eligibleCount`, `potencyStep`, `drew`, `roll`, `line`, `step`, `allLinesCapped`, `baby.*` after it |
| `guardian.timeout` / `guardian.lost` | the other two resolutions | `g.*`, `fight.*`, `reason` (`parentDied:<pet>`, `destroyed`, `landblockUnload`), `blessing=false` |
| `birth` | the baby exists and delivery was attempted | `baby`, `babyName`, `babyWcid`, `donor`, `winner`, `mutations`, `lastMutatedStat`, bond, juvenile / kills / stage, `decided.*` (what Simulate produced) vs `stored.*` (what the device now carries) and `storedMatchesDecided`, `summon.*` (the summon maths line by line: each `gear + count * step`, the derived crit lines, `bonusHp`, the live stage-1 multiplier and every stage-1 value), palette keys, `winnerOnline`, `delivery` (`inventory` / `deferred`) and `deliveryReason`, `dismissParents` |
| `device.dump` | `@pet-dump [note]` (always written) | `reason`, the full `d.*` device block, `summon.*`, `id.breeding` and `id.potency` (the ID panel text, `/`-joined) |
| `maturity.kill` | a juvenile credited a kill, or refused one | `victim.*`, `victimLevel`, device / owner / pet, `petDamage`, `historyTotal`, `share`, `minShare`, `tier`, `credited`, `reason`; when credited `xpMult`, `killsToAdd`, `killsBefore` / `killsAfter`, `killsRequired`, `stages`, `killsPerStage`, `stageBefore` / `stageAfter`, `adult` |
| `maturity.imprint` | first summon of a bred essence | `p.*`, `device`, `bondChar` |
| `consumable.use` | any of the six consumables used on a device (success or refusal) | `p.*`, `item`, `itemWcid`, `target`, `property`, `before`, `after`, `consumed`, `reason`, `targetJuvenile`, `targetNeutered` |
| `neuter.apply` | neutering kit (success or refusal) | `kit`, `target`, `before`, `after`, `applied`, `consumed`, `reason` |
| `tailoring.extract` / `tailoring.apply` | kit used (success or refusal) | tool / kit / source / target guids, `read.*` (the visual set read from the kit), `before.*` / `after.*` (the device's visual set around an apply), `nameBefore` / `nameAfter`, `applied`, `consumed`, `reason` |

### Combat records

A landed hit is **one** record, `combat.damage`, which carries the attack section; `combat.attack` is
written only for a swing or cast that dealt nothing (`reason=` `evaded`, `resisted`, `lifestone`,
`invincible`, `hitGateOrBodyPart`, `targetDead`, `cannotBeDamagedBy`, ...). A DoT tick is one record
per creature per tick however many DoTs stack. Rolls are captured where they are drawn
(`DamageEvent.EvadeRoll` / `CritRoll` / `CritDefenseRoll` / `BaseDamageRoll` / `SchemeCRoll`, the
spell projectile's crit roll); nothing about the order or count of draws changed.

| Event | Keys |
|---|---|
| `combat.attack` | `kind` (`melee` / `missile` / `magic`), `atk.*` and `def.*` (guid, name, kind = `Player` / `Monster` / `CombatPet` / `Pet` / `MatingGuardian`, wcid, level, pet owner), weapon or `spell` / `spellId` / `school`, `dmgType`, `attackType`, `height`, `atkMotion`, `atkPart` (the body part a creature attacks with), `defPart`, `quadrant`, `atkSkill`, `defSkill`, `accuracyMod`, `evadeChance`, `evadeRoll`, `overpower`, `evaded`, `lifestone`; magic adds `magicSkill`, `magicDefense`, `resisted` (the resist roll is internal to `MagicDefenseCheck`); `hit=false`, `reason` |
| `combat.damage` (melee / missile) | the attack section, `hit=true`, then in the order `DamageEvent` applies them: `partDVal` / `partDVar` (already potency-scaled for a pet), `baseMaxRaw`, `baseVariance`, `baseDamageBonus`, `baseElemental`, `baseDamageMod`, `baseMin` / `baseMax`, `baseRoll`, `maturityMult`, `enrageMult`, `lumFlat`, `wsFlat`, `schemeCRoll`, `base`; `attrMod`, `powerMod`, `slayerMod`, `dmgRating` and `dmgRatingBaseMod` (`(100+r)/100`), `recklessMod`, `sneakMod`, `heritageMod`, `pkDmgMod`, `dmgRatingMod`; `critRating`, `critResistRating`, `critChance`, `critRoll`, `critDefenseRoll`, `critDefended`, `crit`, `critDmgMod`, `critDmgRating`, `critDmgRatingMod`, `preMit`; `defBaseArmor`, `armorLayers`, `armorMod`, `shieldMod`, `weaponResistMod`, `resistMod`, `ownerResistMod`, `drr` and `drrBaseMod` (`100/(100+r)`), `critDrr` / `critDrrMod`, `pkDrrMod`, `drrMod`; `splitArrow`, `enrageReduction`, `pctHpFloor` / `preFloor` / `floorWon` (player defender), `prePetMit` / `petCritMult` / `petPhysMult` (pet defender), `mitigated`, `damage`, `absorbed`; `pet.*` when a combat pet attacks (below); `petDef.*` when one defends; `gOut.*` / `gIn.*` for the mating guardian's outgoing base (`clamp(petMaxHp*0.08,20,500)*damageMult*weakened`) and incoming scaling (`nRaw`, `mod`, `weakMult`, `cap`, `capped`, `final`); `vital`, `dealt`, `before`, `after`, `died` |
| `combat.damage` (`kind=magic`) | the attack section, `critRating`, `critResistRating`, `critChance`, `critRoll`, `critDefenseRoll`, `crit`, `endgameCrit`, `pvp`; war/void: `spellMin`, `spellMax`, `zcProc`, `base` (after augs / replacement / variance), `augs`, `skillBonus`; life: `lifeBase`, `base`; `critDmgMod`, `critBonus`, `elementalMod`, `slayerMod`, `weaponResistMod`, `resistMod`, `absorbMod`, `attribMod`, `forkMult`, `zoneMult`, `petSpellMult`, `preRating`; then the `DamageTarget` chain: `dmgRating`, `heritageMod`, `sneakMod`, `critDmgRating` / `critDmgRatingMod`, `pkDmgRatingMod`, `dmgRatingMod`, `drr`, `critDrr` / `critDrrMod`, `pkDrrMod`, `drrMod`, `enrageReduction`; `pet.*` / `petDef.*`; `damage`, `absorbed`, `vital`, `dealt`, `before`, `after`, `died` |
| `combat.damage` (`kind=boost`) | a harmful Boost (Harm etc.): `spell`, `minBoost` / `maxBoost` / `roll`, `resistType` / `resistMod`, `harmCap`, `afterResist`, `lifeAugs`, `afterAugs`, `mbAbsorbed`, `requested`, `applied`, `before` / `after`, `died` |
| `combat.damage` (`kind=dot`) | one per creature per tick: `def.*`, `dmgType`, `aetheria`, `dots`, per DoT (`d1.` .. `d4.`) `from`, `spellId`, `base`, `resistMod`, `drrMod`, `dotResistMod`, `netherMod`, `amount`; `tick`, `before` / `after`, `died` |
| `combat.damage` (`kind=lifeCost`) | the caster's own vital paid for a life projectile (Blight / Tenacity / Martyr's): `drainPercentage`, `requested`, `applied`, `before` / `after` |
| `combat.heal` (`kind=kit`) | `kit`, `vital`, the skill check (`healingSkill`, `kitBoost`, `trainedMod`, `combatMod`, `effectiveSkill`, `difficulty`, `success`), `healkitMod`, `healBase`, `healMin` / `healMax`, `healingRatingMod`, `motelScale` (the own-pet scaling in the motel / encounter), `crit`, `staminaCost`, `amount`, `before` / `after`, `usesLeft` |
| `combat.heal` (`kind=boost`) | a beneficial Boost: the same keys as the harmful one; `motelScale` is the motel own-pet heal scaling |
| `combat.heal` (`kind=overflow`) | self-boost overflow handed to the caster's pet: `overflow`, `applied`, `after` |
| `combat.heal` (`kind=transfer`) | drain / infuse: `src.*`, `dst.*`, `srcVital` / `dstVital`, `drain`, `proportion`, `drainMod`, `transferCap`, `lossPercent`, `boostMod`, `lifeAugs`, `mbAbsorbed`, `srcRequested` / `srcApplied` / `srcBefore` / `srcAfter`, `dstRequested` / `dstApplied` / `dstBefore` / `dstAfter`, `srcDied` |
| `combat.death` | `def.*` (the victim), `dmgType`, `crit`, `maxHp`, `killer`, `killerGuid`, `killerKind`, `killerIsPlayer`, `killerOwner`, `historyTotal`, top five contributors `c1.` .. `c5.` (`name`, `guid`, `dmg`, `share`), `more` |

The `pet.*` block on every hit a combat pet lands is what proves a bred pet's ratings and potency
reached the damage it dealt: `dmgRatingProp` (the `DamageRating` property on the summoned creature),
`dmgRatingEff` (`GetDamageRating()`, enchantments included, the value the swing used), the same pair
for crit and crit damage, `maturityMult`, `dpsFactor`, `potencyApplied`, and from the device
`gearDmg`, `mutDmg`, `dmgStep`, `expectedDmgRating` (`gear + count * step`, the adult value before the
maturity multiplier), `mutCrit` / `critStep`, `mutVit`, `bond`, `juvenile` / `stage` / `strengthMult`,
`potencyStored`, `potencyActive`, `potencyPerLevel`, `potencyMult` (the body-part multiplier that was
baked into `partDVal` at summon).

Example (abridged) of a stage-2 juvenile bred pet swinging at a monster:

```
[PetTrace] combat.damage | session=3f9a1c2e | t=2026-09-19T14:02:11.482Z | kind=melee | atk.name=Whelp Fire Skeleton Samurai | atk.kind=CombatPet | atk.owner=Schneebly | def.name=Olthoi Warrior | def.kind=Monster | def.level=180 | dmgType=Slash | atkPart=Hand | defPart=Head | atkSkill=612 | defSkill=430 | evadeChance=0.1234 | evadeRoll=0.5512 | evaded=false | hit=true | partDVal=63 | partDVar=0.5 | baseMax=63 | baseMin=31.5 | baseRoll=48.2 | maturityMult=0.6 | base=28.92 | attrMod=1.31 | powerMod=1 | slayerMod=1 | dmgRating=17 | dmgRatingBaseMod=1.17 | dmgRatingMod=1.17 | critRating=9 | critResistRating=0 | critChance=0.19 | critRoll=0.7134 | crit=false | preMit=44.3257 | defBaseArmor=180 | armorMod=0.3571 | shieldMod=1 | resistMod=1 | drr=20 | drrMod=0.8333 | damage=13.19 | pet.dmgRatingProp=17 | pet.dmgRatingEff=17 | pet.gearDmg=9 | pet.mutDmg=2 | pet.dmgStep=10 | pet.expectedDmgRating=29 | pet.stage=2 | pet.strengthMult=0.6 | pet.potencyStored=158 | pet.potencyActive=50 | pet.potencyPerLevel=0.01 | pet.potencyMult=1.5 | pet.potencyApplied=true | vital=health | dealt=13 | before=4200 | after=4187 | died=false
```

Reading it: the device says `gear 9 + 2 x 10 = 29` damage rating; at stage 2 the summoned pet carries
`roundHalfUp(29 x 0.6) = 17`, which is the `dmgRating` the swing used (`(100 + 17) / 100 = 1.17`).
The weenie hand part was `DVal 42`; potency 50 active at 0.01 per level is `x 1.5`, so `partDVal=63`
is what the pet rolled from: `48.2 x 0.6 (maturity) = 28.92`, `x 1.31 x 1 x 1 x 1.17 = 44.33`
before mitigation, `x 0.3571 (armor) x 1 x 1 x 0.8333 (DRR 20) = 13.19`, rounded to `13` dealt,
`4200 -> 4187`. Change the mutation count, the potency or the stage and the same keys move.

Not traced (not a creature-vs-creature exchange): falling damage, zone effect ticks, hotspots,
`@smite`. The spell resist roll and the healing kit's amount roll are internal to their helpers; the
records give the chance inputs (`magicSkill` / `magicDefense`, `healMin` / `healMax`) and the outcome.
