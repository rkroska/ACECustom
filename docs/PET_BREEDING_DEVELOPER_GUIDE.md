# Pet Breeding - Developer Guide

Audience: server developers. Covers breeding, mutation, the mating guardian, maturity, imprinting,
tailoring and neutering. Everything here is on branch `feature/pet-breeding-motel`.

Related: `PET_BREEDING_CONTENT_GUIDE.md` (content team), `PET_BREEDING_PLAYER_GUIDE.md` (players),
`PET_BREEDING_TEST_PLAN.md` (test steps), `PET_POTENCY_AND_STRAIN.md` (potency, a separate system).

---

## 1. Where the code lives

| File | Responsibility |
|---|---|
| `Source/ACE.Server/WorldObjects/PetDevice_Breeding.cs` | The breed itself: gates, partner search, guardian spawn, birth, ID panel block, admin diagnostics. `BreedingMath` (nested): the pure inheritance / mutation / cap maths, `Simulate`, `RollAwakenedBlessing`, `SummonedStats`. |
| `Source/ACE.Server/WorldObjects/PetDevice_BreedingReplay.cs` | `BreedingReplay`: parses, runs and reports a `[REPLAY]` blob (see section 11). |
| `Source/ACE.Server/WorldObjects/MatingGuardian.cs` | The ritual monster. `Creature` subclass with source-gated damage, pet-only targeting, no loot/XP/corpse, lost/slain callbacks. |
| `Source/ACE.Server/WorldObjects/PetDevice_Maturity.cs` | Juvenile growth (kill counter, stages, names), imprinting, kill credit from creature deaths. |
| `Source/ACE.Server/WorldObjects/PetDevice.cs` | Device properties (`VisualOverride*`, `IsMale`, `IsShiny`), `SummonCreature`, `ApplyVisualOverridesTo`, bond attunement in `ActOnUse`. |
| `Source/ACE.Server/WorldObjects/CombatPet.cs` | `Init` rating pipeline, `PrepareMaturityForSummon`, `ApplyMaturity`, `MaturityDamageMult`. |
| `Source/ACE.Server/Entity/PetTailoring.cs` | Tailoring kit extract/apply. Neutering kit is inline in `Player_Use.cs`. |
| `Source/ACE.Server/Entity/MonsterCapture.cs` | Capture (siphon). Defines the "visual model" property set tailoring mirrors. |
| `Source/ACE.Server/Services/PetMutationService.cs` | Master palette pool (DAT-derived, filtered), prewarmed at startup. |
| `Source/ACE.Server/WorldObjects/Creature_Networking.cs` | `CalculateObjDesc`: how a palette override reaches the client. `GetSetupDefaultPaletteId`. |
| `Source/ACE.Server/WorldObjects/Creature.cs` | `CanBeDamagedBy(WorldObject)` virtual, honoured by melee, missile, spell projectiles and life magic. |
| `Source/ACE.Server/WorldObjects/Creature_Death.cs` | `OnDeath` calls `PetDevice.CreditMaturityKills(this)`. |
| `Source/ACE.Server/WorldObjects/Player_Networking.cs` | Dance emote stamps `LastDanceTime` and calls `CheckMultiplayerBreeding`. |
| `Source/ACE.Server/Managers/PropertyManager.cs` | All `pet_breeding_*`, `pet_maturity_*` config. |
| `Source/ACE.Server/Command/Handlers/DeveloperCommands.cs` | `@breed`, `@breed-replay`, `@breed-debug`, `@setsex`, `@pet-reset-cooldown`, `@pet-set-maturity`, `@pet-set-mutations`, `@pet-cleanse-palette`, `@mutate_pet`, `@petdesc`. |
| `Source/ACE.Entity/Enum/Properties/*.cs` | Custom property ids (see section 9). |

Build and run notes are in `CLAUDE.md` at the repo root (Release x64 path, config-override rule, the
ASCII-only rule for anything the client renders).

---

## 2. The breed, end to end

Entry: a player performs the dance emote. `Player_Networking.BroadcastMovement` stamps
`Player.LastDanceTime` and calls `PetDevice.CheckMultiplayerBreeding(player, source)`. The chat-emote
fallback in `Player.cs` does the same for a typed emote containing "dance".

`CheckMultiplayerBreeding` is a single static method with gates in this order. Every gate returns
before anything is written:

1. `pet_breeding_enabled`; player not trading; player has a `CombatPet` summoned.
2. Location: `IsInBreedingArea` (landblock `pet_breeding_allowed_landblock`, optional variant). Admins bypass location only.
3. Partner search: iterate online players; same landblock, in the breeding area, `LastDanceTime` within `pet_breeding_dance_sync_seconds`, has a `CombatPet` out, and the two pets share a landcell. First match wins.
4. Pending guardian: refuse if either player already has a breed waiting on a guardian.
5. Both players marked `IsBusy` (cleared in `finally`).
6. Devices resolved from the pets (`TryGetSummoningDevice`, weak reference, with an inventory lookup fallback) and re-verified to be in the owner's inventory.
7. Neutered, tier (`PetDeviceWcids.GetPetLevel` must resolve for both), min level, min bond, shiny (`pet_breeding_allow_shiny`), juvenile (`IsJuvenile`), sex (exactly one male and one female).
8. Male charges (`GetAvailableMaleCharges`) and female recovery cooldown (`PetNextBreedingTime`), each with a test bypass property.

After the last gate the breed is committed: the male's charge is spent, the female's cooldown is
written, both devices are saved, and the stud's owner is told the remaining charge count.

Then the decision phase computes everything about the offspring into a `PendingBreed` record:
species donor (random parent), winner (always the female's owner), inherited stats (55/45 per stat
between parents, mutation counts carried as counts), the mutation roll, and the rolled palette.
`PendingBreed` exists so the birth can be deferred without recomputing anything.

Finally either `TrySpawnMatingGuardian(pending)` (mutation breeds with the guardian enabled) or
`CompleteBirth(pending)` runs. `CompleteBirth` creates the baby device from the donor's weenie, writes
inheritance, mutation counts, the palette (see section 4), marks it juvenile, delivers it, announces,
and dismisses both parents two seconds later via the world action queue.

### Stat model

Every mutable stat is stored as a **count** (`PetMutDamageCount`, `PetMutDamageResistCount`,
`PetMutCritCount`, `PetMutVitalityCount`, `PetMutPotencyCount`). The applied bonus is always
`count x step` where the step is a live property. This means retuning a step rescales every existing
pet on its next summon. Legacy devices without counts fall back to the stored rating divided by the
step. `CombatPet.Init` and `BuildBreedingAppraisalBlock` both use this rule; keep them in sync.

Mutation: one global roll at `pet_breeding_base_mutation_chance` (flat when decay is 0); on success
one eligible stat gains a step. Potency mutates on its own independent roll
(`pet_breeding_potency_mutation_chance`), with a soft cap that quarters the step above
`pet_breeding_potency_soft_cap`. Caps default to 0 = unlimited; this server does not cap progression.

---

## 3. Sex, charges and cooldown

Sex is derived from the device GUID (murmur3 fmix32, low bit) unless `PropertyBool.PetIsMaleOverride`
is set. It is therefore stable, needs no data migration, and is a coin flip per essence.

Male: `PetMaleBreedingCharges` (int) and `PetMaleChargesRefreshTime` (float, unix). `GetAvailableMaleCharges`
refills to `pet_breeding_male_max_charges` once `pet_breeding_male_charge_reset_hours` have passed
since the stamp. A never-stamped device is stamped on first read rather than refilled. The appraisal
reads this with `persist=true`, so an ID can stamp or refill.

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

The palette pool is `PetMutationService.GetMasterPalettePool()`: every 0x04 palette in the DAT that
passes `IsUsableCreaturePalette` (2048 entries, under 40% black, mean luminance above 0.15). It is
deliberately not species-filtered; the roll is a lottery. The web showroom draws from the same pool.

`PetDevice.ApplyVisualOverridesTo(Creature)` dresses any creature in a device's look. It is used for
the summoned pet and for the guardian.

---

## 5. The mating guardian

Spawned by `TrySpawnMatingGuardian` on a mutation breed when `pet_breeding_guardian_enabled`.

- Built from the template weenie `pet_breeding_guardian_template_wcid` (default 7, drudgeskulker),
  dressed with `donor.ApplyVisualOverridesTo`, then given the rolled palette under the base/template rule.
  `Biota.PropertiesPalette` is cleared so the colour renders. Named "Spirit of <creature>", translucency
  from `pet_breeding_guardian_translucency`.
- Stats: level = max parent level; ratings = the offspring's; `DamageRating` offset by
  `(damage_mult - 1) x 100`; health = (pet1 max + pet2 max) x `health_mult`.
- `NeutraliseTemplate` strips loot, XP, luminance, corpse, faction, generator ties, kill quests, and
  destroys any items the template instantiated; then `SetMonsterState()` recomputes the cached monster flags.
- Placement: `FindGuardianSpawnPosition` tries in front of / behind each pet at radius + radius + 1.5 m.
  Creatures do not push each other apart at placement, so this must be computed. `Home` is set after
  `EnterWorld` because placement can nudge the position.
- Damage: `CanBeDamagedBy` returns true only for the two parent pets (or any pet of the two owners, so a
  resummon mid-fight still counts). Projectile sources are resolved through `ProjectileSource`.
  `TakeDamage`, `SpellProjectile.DamageTarget` and life-magic harm all consult it.
- Targeting: `FindNextTarget` picks the nearest parent pet; `HandleFindTarget` nulls any non-pet target
  every tick (players set as target by proximity wake-ups); `Sleep` is a no-op so it never idles.
- Resolution: exactly one of `OnGuardianSlain` (from `OnDeath`), `OnGuardianTimeout` (world queue,
  `pet_breeding_guardian_timeout_seconds`) or `OnGuardianLost` (from `Destroy` without a death) runs.
  All three remove the entry from `pendingGuardianBreeds` (a `ConcurrentDictionary` keyed by guardian
  GUID) and call `CompleteBirth`. The ritual can never lose a paid breed.
- `Die` is overridden to a minimal animation + destroy; no Siphon Lens, no emotes, no treasure.
- A server restart drops in-memory pending breeds. The parents have paid at that point. Documented,
  not handled.

---

## 6. Maturity and imprinting

Properties on the device: `PetMaturityKills` (int, presence = "born from a breed"),
`PetIsJuvenile` (bool). Config: `pet_maturity_*`.

- `MarkBornJuvenile` runs in `CompleteBirth`. The counter is always written; the juvenile flag only
  when `pet_maturity_enabled`. Captured essences never get either.
- Stage = `kills / (required / stages) + 1`, clamped. `MaturityFraction` = `(stage - 1) / stages`.
  Scale and strength multipliers lerp from the juvenile values to 1.0 by that fraction. Adult = 1.0.
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
- Imprint: `TryImprintOnSummon` runs after a successful summon. First summoner of a bred essence gets
  `PetBondAttuned`, `PetBondAttunedCharacterId`, `Attuned`, `Bonded`. `ActOnUse` refuses other characters
  for bred essences regardless of `pet_bond_enabled`.
- Stage names come from `pet_maturity_stage_names` (comma list). `ApplyMaturity` strips any known stage
  name before inserting the current one, so names never stack.

---

## 7. Tailoring and neutering

`PetTailoring.HandleExtract` / `HandleApply`, dispatched from `Player_Use.cs` by WCID.

- Extract creates the filled kit, copies the visual set, places it in the pack, and only then consumes
  the source essence and tool. A failed pack placement leaves everything untouched.
- Apply writes only the visual set, rebuilds the name with `BuildDisplayNameAfterCaptureApply`, syncs the
  Use string, pushes name/icon/use to the client, then sends a full `GameMessageUpdateObject` and mirrors
  the slot move (the client caches inventory labels; per-property updates alone are not enough).
- `CopyVisuals` is one list used in both directions. Extend it there if capture gains a property.
- Both ends must be combat essences (passive crates normalise scale differently). Both refuse while the
  device's pet is summoned or the item is in the trade window.
- Neutering sets `PropertyBool.PetNeutered`; breeding gate 7 refuses it; the ID panel shows it.

---

## 8. Client-facing text

Everything that reaches the AC client must be 7-bit ASCII with `\n` line endings. `AppendLine` emits
`\r\n` and the client draws the CR as a music note; `BuildBreedingAppraisalBlock` and
`RunBreedingDiagnostics` strip it. Emoji render as boxes. See `CLAUDE.md`.

---

## 9. Custom property ids

| Property | Id | Notes |
|---|---|---|
| PropertyInt.PetMaleBreedingCharges | 9057 | |
| PropertyInt.PetMutDamageCount .. PetMutPotencyCount | 9070-9074 | counts |
| PropertyInt.PetMutDamageRating .. | 9060-9067 | legacy stored bonuses, fallback only |
| PropertyInt.PetMutationCount | 9075 | written, never read (was 9058, collided with Vitality) |
| PropertyInt.EssenceSalvageYield | 9076 | (was 9057, collided with charges) |
| PropertyInt.PetMaturityKills | 9077 | presence = bred |
| PropertyFloat.PetNextBreedingTime | 9056 | |
| PropertyFloat.PetMaleChargesRefreshTime | 9057 | |
| PropertyBool.PetIsMaleOverride | 50053 | |
| PropertyBool.PetIsJuvenile | 50054 | |

Before adding an id, grep the enum for the number. Two collisions shipped on this branch before they
were caught.

---

## 10. Extension points and gotchas

- Add a breeding gate: insert it in `CheckMultiplayerBreeding` before the charge/cooldown writes.
  Nothing after "Every gate has passed" may return without completing the breed.
- Add a visual property: add it to capture, to `ApplyVisualOverridesTo`, and to `PetTailoring.CopyVisuals`.
- Add a stat: add a count property, a step property, inheritance in the decision phase, application in
  `CombatPet.Init`, capture in `matureXxx`, scaling in `ApplyMaturity`, display in the appraisal block.
- Never enqueue a must-run action on a creature that may be gone; use `WorldManager.ActionQueue`.
- `IsMonster` / `IsFactionMob` are cached at construction. Call `SetMonsterState()` after changing
  `Attackable` or faction on a live creature.
- Logging: `pet_breeding_verbose_logging` and `pet_visual_packet_debug` gate the noisy lines. Keep new
  per-packet or per-death logs behind a switch.

---

## 11. Simulator parity harness

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

The server writes the same blob when `pet_breeding_verbose_logging` is on:
`[PetBreeding] [REPLAY] {...}` after the decision (`guardianKilled:false`, draws 1-11) and again
after a slain guardian (`guardianKilled:true`, draw 12 appended). Those lines replay too.

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
