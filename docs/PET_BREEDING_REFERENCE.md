# Pet Breeding and Ruggan's Annex - Developer Reference

Audience: developers and content builders working on the pet breeding system or the annex.
Companion documents:

- `PET_BREEDING_TECHNICAL_DESIGN.md` - why the system is built the way it is.
- `PET_BREEDING_PLAYER_GUIDE.md` - the rules as players see them.
- `deploy/PROD_DEPLOY_PLAN.md` - how it reaches production.

Verified against branch `feature/pet-breeding-motel` on 2026-09-21, including the uncommitted work:
the colour cycle, the paternity scenes, the Sawato bandits and the annex move. Line numbers are as of
that date. Paths are relative to `Source/ACE.Server/` unless they start with `Database/`, `deploy/`,
`docs/` or `Source/`.

This document supersedes `PET_BREEDING_DEVELOPER_GUIDE.md`, `PET_BREEDING_CONTENT_GUIDE.md`,
`PET_BREEDING_ANNEX_DESIGN.md` and `PET_BREEDING_TEST_PLAN.md`. Where they disagree with this one,
this one was checked against the code.

---

## Contents

1. [Code map](#1-code-map)
2. [Server settings](#2-server-settings)
3. [Object properties](#3-object-properties)
4. [WCIDs](#4-wcids)
5. [Commands](#5-commands)
6. [Breeding gates and messages](#6-breeding-gates-and-messages)
7. [Consumables and kits](#7-consumables-and-kits)
8. [Content: the annex](#8-content-the-annex)
9. [Content: authoring NPC dialogue and scenes](#9-content-authoring-npc-dialogue-and-scenes)
10. [Content: building and placing NPCs](#10-content-building-and-placing-npcs)
11. [Testing](#11-testing)
12. [Observability: trace, replay, diagnostics](#12-observability-trace-replay-diagnostics)
13. [Deployment tooling](#13-deployment-tooling)
14. [Gotchas](#14-gotchas)

---

## 1. Code map

| Area | Files |
|---|---|
| Breeding, world side (trigger, gates, commit, guardian spawn, birth, appraisal text, diagnostics) | `WorldObjects/PetDevice_Breeding.cs` |
| Breeding maths (pure, unit-tested: inheritance, rolls, caps, summon arithmetic) | `PetDevice.BreedingMath`, inside `PetDevice_Breeding.cs` (about lines 84-573) |
| Replay / parity harness | `WorldObjects/PetDevice_BreedingReplay.cs` |
| Session trace | `WorldObjects/PetDevice_Trace.cs`, `PetDevice_Trace_Combat.cs` |
| Maturity and imprint | `WorldObjects/PetDevice_Maturity.cs`, `WorldObjects/CombatPet.cs` (`ApplyMaturity`) |
| Device core (sex, icon underlay, summon, visual overrides, use gate) | `WorldObjects/PetDevice.cs` |
| Mating guardian | `WorldObjects/MatingGuardian.cs`, guardian damage in `Entity/DamageEvent.cs:344-365` |
| Dance trigger | `WorldObjects/Player_Networking.cs:400-451` (motion), `WorldObjects/Player.cs:1013-1041` (`HandleActionSoulEmote`) |
| Consumables and kits | `WorldObjects/Player_Use.cs:144-515`, `Entity/PetTailoring.cs` |
| Mutation palettes and rendering | `Services/PetMutationService.cs`, `WorldObjects/Creature_Networking.cs` (`CalculateObjDesc`, `ApplyPaletteTemplateOverride`) |
| Showcase colour cycle | `WorldObjects/Creature_ShowcaseColour.cs`, called from `WorldObjects/Creature_Tick.cs:39` |
| Pet naming | `Command/Handlers/PlayerCommands.cs:1755-1948`, `Controllers/PetNamingController.cs`, portal page `PetNameApprovals.tsx` |
| Visualizer / simulator | `Controllers/VisualizerController.cs`, `Services/VisualizerService.cs`, `ClientApp/src/components/PetBreedingCalculator.tsx`, `utils/breedingModel.ts` |
| Template export (`@et`) | `Entity/TemplateExport.cs`, `Command/Handlers/DeveloperContentCommands.cs:3268+` |
| Settings | `Managers/PropertyManager.cs:561-608` (breeding, maturity, icons, trace), `:865-869` (export) |
| Property ids | `Source/ACE.Entity/Enum/Properties/*.cs` |
| NPC emote engine (dialogue) | `WorldObjects/Managers/EmoteManager.cs`, `Entity/Landblock.cs:1981` (`EmitSignal`) |
| World content | `Database/Updates/World/2026-07-26-00` through `2026-09-22-04` (see section 13) |
| Shard schema | `Database/Updates/Shard/2026-09-15-00-Pet-Name-Requests.sql` |
| Deployment | `deploy/Annex-Prod-Bundle.sql`, `deploy/Annex-Prod-Shard.sql`, `deploy/export_annex_bundle.py`, `deploy/PROD_DEPLOY_PLAN.md` |

---

## 2. Server settings

### 2.1 How settings work

- **Declaration:** each setting is declared in `Managers/PropertyManager.cs` as `ServerConfig.<name>`.
- **Stored values win.** A value stored in the shard database (`config_properties_boolean`, `_long`,
  `_double`, `_string`) **overrides the code default**. Changing a default in code does nothing on a
  server that already has a stored value.
- **Changing a value live:**
  - `@modifybool <name> true|false`
  - `@modifylong <name> <n>` (decimal or `0x` hex)
  - `@modifydouble <name> <x>`
  - `@modifystring <name> "<value>"` (quote any value that contains spaces)
- **Reading a value:** `@fetchbool`, `@fetchlong`, `@fetchdouble` and `@fetchstring` (`@showprops` currently crashes: a pre-existing bug on master);
  and `@petserverconfig`, which lists every effective `pet_*` value with ready-to-paste `/modify` lines.
- **Sync:** the server writes pending changes and re-reads the database every 5 minutes
  (`PropertyManager.cs:963`).

The **Prod** column below is what `deploy/Annex-Prod-Shard.sql` sets. A dash means it is left at the
code default. The branch adds 51 settings, and every one is read by the code; none are dead.

### 2.2 Breeding

| Setting | Type | Default | Prod | What it does | Read at | How to test |
|---|---|---|---|---|---|---|
| `pet_breeding_enabled` | bool | true | true | Master switch. False: breeding returns immediately, the appraisal breeding block is not built, and `PetNextBreedingTime` is not synced. **Does not** stop consumables being used. The description in code ("using one device on another") is stale: breeding is dance-driven. | `PetDevice_Breeding.cs:688, 1908, 2083`; `PetDevice.cs:224` | Set false, dance with a partner: nothing happens (admins see a debug line). Appraise: no "Sex:" block. |
| `pet_breeding_allowed_landblock` | long | 0x0106 | **262** | Where breeding is allowed. 0 = anywhere. At most 0xFFFF = the whole landblock. Above 0xFFFF = that exact 32-bit cell only. Checked for the dancer and each candidate partner, and used by `CombatPet.IsInMotelOrEncounter` (own-pet healing). Negative matches nothing. | `PetDevice_Breeding.cs:48, 729, 2085`; `CombatPet.cs:1859` | `@breed-debug` in the annex: "Location Match: VALID". |
| `pet_breeding_allowed_variant` | long | 2 | **2** | Landblock variation that must match. -1 = any. | `PetDevice_Breeding.cs:49, 2086` | `@breed-debug` shows Variant and the match. |
| `pet_breeding_dance_sync_seconds` | double | 5.0 | - | Window the partner's last dance must fall in, measured when the second player dances. Ignored by `@breed`. There is also a fixed 2 s per-player dance rate limit. | `PetDevice_Breeding.cs:744` (used 787-823) | Set 30; two players dance 20 s apart and the breed fires. |
| `pet_breeding_min_parent_level` | long | 100 | - | Minimum **tier** of both essences (`PetDeviceWcids.GetPetLevel`; tiers 50, 80, 100, 125, 150, 180, 200, 250, 300). A wcid missing from the table refuses with "Failed to determine parent pet tiers". | `PetDevice_Breeding.cs:961` | Set 300, breed tier 200s: "Parent pets must be at least tier 300 to breed." |
| `pet_breeding_min_bond` | long | 100 | - | Both devices' `PetBondLevel` (unset = 1) must be at least this. **Bond only grows while `pet_bond_enabled` is true, and that defaults to false, so on pure defaults nobody can breed.** Babies start at bond 1. | `PetDevice_Breeding.cs:971` | Set 1, breed two fresh essences. |
| `pet_breeding_allow_shiny` | bool | false | - | False: a shiny essence cannot breed (matchmaking and gate), appraisal says "(shiny - cannot breed)", and the baby's variant is cleared. True: shiny may breed, and the donor's variant is inherited. | `PetDevice_Breeding.cs:837, 985, 1712, 1920` | Appraise a shiny essence. |
| `pet_breeding_male_max_charges` | long | 10 | - | Breeds a stud can sire before refill. Unset reads as full. 0 = no male can breed. Lowering it clamps existing devices down; raising it only takes effect at the next refill. | `PetDevice_Breeding.cs:842, 1033, 1930, 2041, 2101` | Appraise a male: "(10/10 breeding charges today)"; breed: "spent a breeding charge: 9/10 left today". |
| `pet_breeding_male_charge_reset_hours` | double | 24.0 | - | Lazy refill: on the first read after `PetMaleChargesRefreshTime + hours`, charges reset to max and the stamp moves to now. A never-stamped device is stamped, not refilled. At or below 0 = never refills. | `PetDevice_Breeding.cs:1037, 1934-1943, 2044-2074` | Set 0.01 (36 s), spend a charge, wait, re-appraise. |
| `pet_breeding_bypass_male_charges` | bool | false | **false** | TEST ONLY. Charges are neither checked nor spent. | `PetDevice_Breeding.cs:839, 1035, 1164` | Breed repeatedly with a 0-charge male. |
| `pet_breeding_cooldown_hours` | double | 4.0 | - | Female rest after a litter: `PetNextBreedingTime = now + hours x 3600`. This is rest, not gestation. 0 or negative = ready immediately. | `PetDevice_Breeding.cs:1180` | Appraise the female: "(recovering - ready to breed in 3h 59m)". |
| `pet_breeding_bypass_female_cooldown` | bool | false | **false** | TEST ONLY. The cooldown is neither checked nor written. | `PetDevice_Breeding.cs:846, 1048, 1178` | Breed the same female twice. |
| `pet_breeding_dismiss_after_breed` | bool | true | - | Both parent pets are destroyed 2 s after the birth. | `PetDevice_Breeding.cs:1885-1900` | Pets vanish about 2 s after the birth message. |
| `pet_breeding_base_mutation_chance` | double | 0.05 | - | Stat-mutation chance = `clamp(max(floor, (base + incense) / (1 + decay x babyInheritedStatMutations)), 0, 1)`. Incense decays with the line like the base does (changed 2026-09-22; it used to be added after the decay). The floor applies after decay, so base 0 with floor 0.02 still gives 2%. Also served anonymously at `GET /api/Visualizer/breeding-config`. | `PetDevice_Breeding.cs:178` (formula 432-436) | Set 1.0: every birth is "[GENETIC MUTATION]". |
| `pet_breeding_mutation_decay_rate` | double | 0.0 (prod: 0.1 via the shard script) | - | Divides the base chance by `1 + decay x (stat mutations the baby inherited)`. Potency is not counted. 0 = flat. There is no guard against negative values. | `PetDevice_Breeding.cs:180, 434` | Set 1.0 with `pet_trace` on: `breed.roll` chance falls. |
| `pet_breeding_mutation_min_floor` | double | 0.02 | - | Lower bound on the decayed chance (base and incense together). | `PetDevice_Breeding.cs:181, 435` | As above. |
| `pet_breeding_force_mutation` | bool | false | **false** | TEST ONLY. The stat roll always succeeds (the draw is still consumed, so replays stay aligned). Does **not** force potency. The code description ("and color mutation") is loose: colour follows from any mutation. | `PetDevice_Breeding.cs:191, 329` | Every birth mutates. |
| `pet_breeding_max_stat_mutations` | long | 0 | - | Per-line cap on the mutation **count** for damage, damage resist, crit and vitality. At or below 0 = uncapped. A capped line is removed from the pick list and from the blessing. Appraisal shows `[n/cap Muts]`. | `PetDevice_Breeding.cs:190, 413-421, 1993, 2005` | Set 1, force mutations on already-mutated parents. |
| `pet_breeding_damage_mutation_step` | long | 10 | - | Damage Rating per damage mutation. **Counts are stored, ratings are computed at summon**, so a change re-scales every bred pet at its next summon. Also derives crit damage `round(0.8 x bonus)`. | `CombatPet.cs:449`; `PetDevice_Breeding.cs:182, 1978` | Change it, re-summon a mutated pet. |
| `pet_breeding_dr_mutation_step` | long | 10 | - | Damage Resist Rating per DR mutation. Derives crit resist `round(0.8 x bonus)` and crit damage resist `round(0.6 x bonus)`. Applied live at summon. | `CombatPet.cs:450` | As above. |
| `pet_breeding_crit_mutation_step` | long | 5 | - | Crit Rating per crit mutation. Applied live at summon. | `CombatPet.cs:451` | As above. |
| `pet_breeding_vitality_mutation_step` | long | 50 | - | Max health per vitality mutation, scaled by juvenile strength. Applied live at summon. | `CombatPet.cs:669` | Compare max health after a change. |
| `pet_breeding_potency_mutation_chance` | double | 0.03 | - | Independent potency roll, clamped 0..1. Incense does not apply. | `PetDevice_Breeding.cs:179, 347` | Set 1.0: "+25 Potency" in the birth line. |
| `pet_breeding_potency_mutation_step` | long | 25 | - | Potency per potency mutation: quartered (minimum 1) at or above the soft cap, then clamped to the hard cap. **Baked into `PetPotencyStored` at birth**, so a later change does not re-scale existing pets. The appraisal "[k Muts]" line uses the current step and can disagree with the stored value. | `PetDevice_Breeding.cs:186, 493-503` | Force a potency mutation and appraise. |
| `pet_breeding_potency_soft_cap` | long | 1000 | - | At or above this stored potency, each potency mutation gives `max(1, step/4)`. At or below 0 = no soft cap. | `PetDevice_Breeding.cs:187, 350, 496` | Trace shows `potencySoftCapped`. |
| `pet_breeding_potency_hard_cap` | long | 0 | - | Effective cap = the smallest **positive** of this and `pet_potency_max_stored`. Both 0 = uncapped. A capped line gives step 0 and is excluded from the blessing. | `PetDevice_Breeding.cs:188, 479-486` | Set to stored + 1: the next mutation gains 1. |
| `pet_breeding_guardian_enabled` | bool | false | **true** | A breed that actually mutated spawns "Spirit of X". The birth waits for the spirit to be slain (blessing), time out, or be lost. Any spawn failure means an immediate birth. Also gates the Offering of Subjugation, which is refused and not consumed while this is off. | `PetDevice_Breeding.cs:192, 362, 1263`; `Player_Use.cs:383` | Enable plus `force_mutation`, breed: "The union stirs something...". |
| `pet_breeding_guardian_template_wcid` | long | 7 | - | Creature weenie cloned for the spirit's combat template. Its look is replaced by the baby's. A missing weenie means the spirit is skipped and the birth is immediate (warning logged). | `PetDevice_Breeding.cs:1296` | Set a bad wcid: the birth is immediate. |
| `pet_breeding_guardian_timeout_seconds` | double | 90.0 | - | Seconds until the spirit fades (minimum 5). The birth completes without a blessing. | `PetDevice_Breeding.cs:1378, 1658` | Set 10, idle: "dissolves back into the ether". |
| `pet_breeding_guardian_health_mult` | double | 1.0 | - | Spirit health = `max(1, round((pet1 max HP + pet2 max HP) x mult))`, with mult floored at 0.01. | `PetDevice_Breeding.cs:1328, 1340` | Set 0.1: it dies in a few hits. |
| `pet_breeding_guardian_damage_mult` | double | 0.5 | - | Spirit hit on a parent pet = `clamp(defender max HP x 0.08, 20, 500) x mult`, x0.5 if weakened. NaN, infinite or negative becomes 1.0. | `Entity/DamageEvent.cs:349` | Set 0: it does no damage. |
| `pet_breeding_guardian_translucency` | double | 0.6 | - | Spirit translucency, clamped 0..0.95. | `PetDevice_Breeding.cs:1321` | Set 0: solid. |
| `pet_breeding_verbose_logging` | bool | false | **false** | Logs every dance trigger and location failure. Players can spam it with a dance macro. | `PetDevice_Breeding.cs:681, 690, 725` | Server log `[PetBreeding] Breeding trigger received`. |

### 2.3 Maturity

| Setting | Type | Default | Prod | What it does | Read at | How to test |
|---|---|---|---|---|---|---|
| `pet_maturity_enabled` | bool | true | - | `IsJuvenile = enabled && PetIsJuvenile`. False: every juvenile acts as an adult at once (full size and strength, can breed, no growth credit). The flag is kept, so re-enabling restores them. A birth always writes `PetMaturityKills = 0` (which marks the essence "bred" for imprinting) but only sets `PetIsJuvenile` while enabled. | `PetDevice_Maturity.cs:20, 116, 166, 213`; `CombatPet.cs:61, 84` | Summon a newborn: "(Owner)'s Newborn X" at half size. |
| `pet_maturity_kills_required` | long | 300 | - | Kills to adulthood (minimum 1). Kills per stage = `ceil(required / stages)`, which is 60 at defaults. | `PetDevice_Maturity.cs:27` | Set 5, kill 5 qualifying creatures. |
| `pet_maturity_stages` | long | 5 | - | Number of juvenile stages (minimum 1). Stage = `min(stages, kills / perStage + 1)`. | `PetDevice_Maturity.cs:29` | Appraise: "Growth: Newborn 1/5 ...". |
| `pet_maturity_stage_names` | string | Newborn,Whelp,Juvenile,Adolescent,Young Adult | - | Comma list, first entry = stage 1. Entries are trimmed and stripped to printable ASCII; a missing entry becomes "Stage N". Used in pet names, messages and appraisal, and to strip old tags from names. | `PetDevice_Maturity.cs:70, 87` | `@modifystring pet_maturity_stage_names "Pup,Cub"`. |
| `pet_maturity_juvenile_scale` | double | 0.5 | - | Stage-1 size relative to adult, lerping to 1.0: 0.5, 0.6, 0.7, 0.8, 0.9 at defaults. Clamped 0.05..1. | `PetDevice_Maturity.cs:98`; `CombatPet.cs:70, 110` | Set 0.2: a tiny newborn. |
| `pet_maturity_juvenile_strength` | double | 0.5 | - | Same curve, applied to all six combat ratings, max health (base plus vitality) and outgoing damage (`MaturityDamageMult`). Rounded half up. | `PetDevice_Maturity.cs:101`; `CombatPet.cs:85-108`; `Entity/DamageEvent.cs:335` | Compare newborn and adult max health. |
| `pet_maturity_min_damage_share` | double | 0.10 | - | Share of the victim's total health the juvenile must have dealt for the kill to count. The victim's level must also be at least the essence tier. Players, combat pets and spirits never count. **The same rule gates bond XP for juveniles.** | `PetDevice_Maturity.cs:194-205, 222`; `Creature_Death.cs:378` | Set 0.9 and let the owner do most of the damage: no credit. |
| `pet_maturity_imprint_on_summon` | bool | true | - | The first summon of a bred essence that is not attuned makes it Attuned and Bonded to that character. Other characters are refused use even with bond disabled (`PetDevice.cs:765` checks `pet_bond_enabled \|\| WasBredJuvenile`). | `PetDevice_Maturity.cs:127, 151`; `PetDevice.cs:1238` | Summon a newborn on a second character: imprint message; then try to trade it. |

### 2.4 Icons, trace and debug

| Setting | Type | Default | Prod | What it does | Read at |
|---|---|---|---|---|---|
| `pet_sex_icon_underlay_enabled` | bool | true | - | A combat essence's `IconUnderlayId` is derived on read: the male or female square. | `PetDevice.cs:456, 488-507` |
| `pet_sex_icon_underlay_male` | long | 100670255 (0x06001B2F) | - | Male square DID. At or below 0, or out of range, means no square. | `PetDevice.cs:473` |
| `pet_sex_icon_underlay_female` | long | 100670253 (0x06001B2D) | - | Female square DID. | `PetDevice.cs:474` |
| `pet_trace` | bool | false | **false** | Full `[PetTrace]` session trace at INFO: every gate, config value, inheritance line, roll, REPLAY blob, guardian event, birth, maturity credit, consumable, tailoring, neuter **and every combat exchange server-wide**. Test sessions only. | `PetDevice_Trace.cs:39` |
| `pet_visual_packet_debug` | bool | false | **false** | Logs `[CREATURE PACKET DEBUG]` for every creature ObjDesc built on the non-early-return path. Bred and captured pets use the early return, so they don't produce it. | `Creature_Networking.cs:386` |

### 2.5 Template export (content tool)

| Setting | Type | Default | What it does |
|---|---|---|---|
| `content_template_export_wcid_start` | long | 78790000 | First wcid of the **temporary staging block** for `@et`. The import commands refuse ids in the block without `force`. |
| `content_template_export_wcid_end` | long | 78799999 | Last wcid (inclusive). When the block is used up, exports are refused; there is no wrap-around. |
| `content_template_export_next_wcid` | long | 0 | Persisted high-water mark, saved **before** the file is written. A failed export still uses up an id. |
| `content_template_export_auto_import` | bool | true | Loads the export into `ace_world` (block ids only) so it can be spawned at once. |
| `content_template_export_auto_discord` | bool | true | Also posts the file to the Discord exports channel, when one is configured. **Prod too.** |

### 2.6 Pre-existing settings breeding depends on

| Setting | Type | Default | Why it matters |
|---|---|---|---|
| `pet_bond_enabled` | bool | **false** | Bond XP is only awarded while this is true (`Creature_Death.cs:331`). With it off, bond never reaches 100 and **nobody can breed**. It also switches bond combat bonuses, bond mitigation and attune-on-capture. |
| `pet_bond_xp_multiplier` | double | 1.0 | Bond XP per kill = `round(baseXp x petShare x mult x owner kill-XP mods)`, floored at `pet_bond_xp_min_award` (1). Set it high for breeding tests. |
| `pet_potency_enabled` | bool | **false** | With it off, active potency is 0 (no combat effect), residue and salvage refuse, and the appraisal potency block is hidden. Breeding still inherits and mutates `PetPotencyStored`. |
| `pet_potency_max_stored` | long | 0 | Caps stored potency, and folds into breeding's effective hard cap. |

---

## 3. Object properties

"Shown" means the property carries `[AssessmentProperty]`: it is sent in the appraisal packet, which
plugins can read. The stock client does not label custom ids. Players see the human-readable lines that
`PetDevice_Breeding.cs` builds into the long description. All of these properties are persisted on the
biota except the showcase colour.

Where it lives: **DEV** = pet device (essence), **PET** = summoned combat pet, **CRE** = any creature,
**KIT** = tailoring kit.

### 3.1 Breeding, maturity and consumable state (DEV)

| Property | Id | Type | Meaning | Written by | Read by |
|---|---|---|---|---|---|
| `PetMaleBreedingCharges` | Int 9057 | int | Stud charges left. Unset = full. | breed `PetDevice_Breeding.cs:1167`, refill `:2068`, admin commands | `:842, 2042` |
| `PetMaleChargesRefreshTime` | Float 9057 | double | Unix time of the last refill. | `:2057, 2069` | `:1937, 2049` |
| `PetNextBreedingTime` | Float 9056 | double | Female ready time (Unix seconds). | `:1183`; removed by `@pet-reset-cooldown` | `:849, 1047, 1951, 2103` |
| `PetNeutered` | Bool 9051 | bool | Permanently cannot breed. Nothing clears it. | Neutering Kit, `Player_Use.cs:206` | gates, appraisal |
| `PetIsMaleOverride` | Bool 50053 | bool | Tri-state sex override. Unset = derived from the GUID (murmur3 fmix32 of `Guid.Full`, even = male). The code comment in `PropertyBool.cs:377` is stale. | `@setsex`, `@pet-make-alpha` | `PetDevice.cs:403` |
| `PetIsJuvenile` | Bool 50054 | bool | Juvenile flag, effective only while maturity is enabled. | birth, adulthood, `@pet-set-maturity` | `PetDevice_Maturity.cs:20`; the Draught checks the raw flag |
| `PetMaturityKills` | Int 9077 | int | Growth counter. **Its presence marks the essence as bred** (`WasBredJuvenile`), which drives imprinting and the Growth line. | birth (always 0), kills | `PetDevice_Maturity.cs:23, 25` |
| `PetMaturityXpMultiplier` | Float 9059 | double | Nurturing Draught: credit per qualifying kill = `max(1, round(mult))`. Removed at adulthood. | `Player_Use.cs:361` | `PetDevice_Maturity.cs:294` |
| `PetIncenseBonus` | Float 9058 | double | Courtship Incense bonus (0.025, 0.05 or 0.10). Removed from **both** devices on every committed breed. | `Player_Use.cs:275` | `PetDevice_Breeding.cs:1106` |
| `PetChromaticCatalystActive` | Bool 50055 | bool | The next rolled palette comes from the vibrant pool. Consumed from both parents only when a palette is rolled. | `Player_Use.cs:313` | `PetDevice_Breeding.cs:1112` |
| `PetGuardianWeakened` | Bool 50056 | bool | Offering of Subjugation. Consumed from both parents only when a spirit spawns. | `Player_Use.cs:407` | `PetDevice_Breeding.cs:1118, 1428` |
| `PetMutDamageCount` / `DamageResistCount` / `CritCount` / `VitalityCount` / `PotencyCount` | Int 9070-9074 | int | Mutation **counts**. Stat counts are multiplied by the live step at summon. The potency value itself is in `PetPotencyStored`. | birth `:1755-1759`, `@pet-set-mutations` | `CombatPet.cs:453-455, 670`, appraisal |
| `PetMutationCount` | Int 9075 | int | Total, written only when above 0. Trace only. | `:1763` | trace |
| `PetLastMutatedStat` | Int 9078 | int | Line mutated on the latest breed: 1 dmg, 2 DR, 3 crit, 4 vit, 5 potency. | `:1766` | trace |
| `PetMutDamageRating` / `DamageResistRating` / `CritRating` | Int 9068/9069/9062 | int | Legacy. Read only as a fallback (`rating / step`) when there is no count. | none | `CombatPet.cs:453` |
| `PetMutVitality` / `PetMutPotency` | Int 9066/9067 | int | Legacy fallbacks. | none | `CombatPet.cs:671` |
| `PetMutCritDamageRating` / `CritResistRating` / `CritDamageResistRating` | Int 9063/9064/9065 | int | Declared but never used. | none | none |

### 3.2 Bond and potency (DEV)

| Property | Id | Type | Meaning |
|---|---|---|---|
| `PetBondAttuned` | Bool 9047 | bool | Bond-attuned. Set on capture (when bond is enabled) and on imprint; false on a newborn. |
| `PetBondAttunedCharacterId` | Int64 9052 | long | Owning character. 0 on a newborn. |
| `PetBondLevel` | Int 9053 | int | Bond level. Unset counts as 1 for breeding. Babies start at 1. |
| `PetBondXp` / `PetBondXpTotal` | Int64 9050/9051 | long | Bond XP toward the next level / lifetime total. |
| `PetPotencyStored` | Int 9056 | int | Stored potency: inherited 55/45, plus mutation steps baked in at birth. |

### 3.3 Visual override and capture set (DEV)

The species donor passes this whole set to the baby at birth (`PetDevice_Breeding.cs:1702-1732`), and
the tailoring kit copies it (`Entity/PetTailoring.cs:245-274`).

| Property | Id | Meaning |
|---|---|---|
| `VisualOverrideSetup` | DID 9033 | Model at summon. Its presence triggers `ApplyVisualOverridesTo`, and tailoring extract requires it. |
| `VisualOverrideMotionTable` / `CombatTable` / `SoundTable` | DID 9034 / 9040 / 9035 | Tables applied at summon. |
| `VisualOverridePaletteBase` | DID 9036 | Palette base. On a mutated birth it is set to the setup's native default palette. |
| `VisualOverrideClothingBase` | DID 9037 | Clothing base. |
| `VisualOverrideIcon` | DID 9038 | Portrait icon (kit and device). |
| `VisualOverridePaletteTemplate` | Int 9035 | **The mutation colour**: a full 0x04 palette id. Written by births, the Mutagenic Serum and `@mutate_pet`; removed by `@pet-cleanse-palette`. |
| `VisualOverrideShade` / `VisualOverrideScale` | Float 9042 / 9043 | Shade and adult scale (juvenile scale multiplies this). |
| `CapturedCreatureName` | String 9009 | Species name (wrapped as `VisualOverrideName`). The baby takes the donor's. |
| `CapturedItems` | String 9010 | Equipment the pet wears. |
| `CapturedObjDescAnimParts` / `Palettes` / `Textures` | String 9011 / 9012 / 9013 | Captured look recipe. **Palettes is cleared** on a mutated birth, serum or mutate, or the new template never reaches the client. |
| `CapturedCreatureVariant` | Int 9039 | Shiny flag (`IsShiny`). Tailoring copies it. |
| `CapturedCreatureType` / `CapturedCreatureWCID` / `CapturedSourceDamageType` | Int 9037 / 9033 / 9054 | Species, source wcid and damage type. All travel with a tailored look. |
| `PetCustomName` | String 9018 | Staff-approved name, used verbatim at summon, kept through tailoring. |

### 3.4 Showcase colour (CRE, new)

| Property | Id | Meaning |
|---|---|---|
| `ShowcaseColourCycleSeconds` | Float 9060 | Seconds between colour changes on an NPC. Checked on each creature heartbeat (about 5 s). The first tick picks a random phase. When due and a player is within 96 m, the NPC rolls a **vibrant** palette into `PaletteTemplate`, broadcasts an ObjDesc update and plays `EnchantUpPurple`. Unset or at or below 0 = off. **Never saved**, so the colour resets on respawn. Set to 60 on 78780221-78780225. (PropertyInt 9060 is an unrelated property on a different enum.) |

### 3.4a Spawn and pet properties (new)

| Property | Id | Meaning |
|---|---|---|
| `OnlyCombatPetsCanDamage` | Bool 50057 | On a creature: only combat pets can damage it. Players, other monsters and damage over time from anyone else do nothing. On a generator: copied to everything it spawns, so nested generators pass it down. Enforced in `Creature.CanBeDamagedBy`, `Creature.TakeDamage` and `EnchantmentManager.ApplyDamageTick`. |
| `SpawnColourMutationChance` | Float 9061 | On a generator: each creature it spawns has this chance (0.01 = 1%) of a random vivid mutation colour. Nested generators inherit it. Models drawn mostly with full-colour textures are skipped. A captured coloured spawn keeps its colour. `GeneratorProfile.ApplySpawnColour`. |
| `PetTranslucency` | Float 9062 | On a combat pet essence: the translucency its pet summons with, 0 (solid) to 0.5, stepped a tenth at a time by the Solidifying (78780262) and Fading (78780263) Tinctures. Replaces the summon template's value (Maiden and K'nath templates carry 0.5). Unset = the template's value. Shown on appraisal. `PetDevice` summon, `Player_Use` handler. |

**How to test:** set one on a generator (`INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (<wcid>, 50057, 1);` or float 9061 at 1.0 for a sure hit), `@clearcache`, and let it respawn. Live, on one object: appraise it, then `@setproperty PropertyBool.OnlyCombatPetsCanDamage true`.

### 3.4b Colour visibility and rendering rules

- **Colour visibility** (`PetMutationService.GetColourChangeVisibility`): `Hidden` when captured textures cover 90%+ of
  the parts; `FixedColour` when 60%+ of the model's drawn polygons use full-colour (non-palette) textures, read from the
  Portal DAT with the capture's part and texture swaps applied and cached per model (a palette can only recolour
  PFID_INDEX16 / PFID_P8 textures); `Unknown` when textures exist but no part list; else `Visible`. `Hidden` and
  `FixedColour` refuse the serum without consuming it, show a `Colour:` line on the ID panel, and warn in `@mutate_pet`.
  Breeding still rolls a colour for them.
- **Ordinary creatures render exactly as master.** Only objects whose `PaletteTemplate` is a full 0x04 palette take the
  mutation-colour path in `Creature.CalculateObjDesc`; everything else falls through to master's base clothing path, and
  the single-entry ClothingBase fallback in `WorldObject.CalculateObjDesc` is limited to 0x04 objects too. Reason: master
  writes a 2048-colour clothing sub-palette as Length 256, which goes out as byte 0 and is ignored by the client, and
  clothing mods were tuned against that (Vile Remoran 71600059, ClothingBase 0x10000636 template 85, turned grey when
  the branch split the range into 255 + 1).

### 3.5 Retail properties the features reuse

| Property | Id | Use |
|---|---|---|
| `PaletteTemplate` | Int 3 | On a pet, spirit or showcase NPC: a full 0x04 mutation palette. |
| `PaletteBase` | DID 6 | Set to the native base when a mutation colour is applied. |
| `DefaultScale` | Float 39 | Juvenile size on a pet. NPC sizes in the annex (for example Gary 0.275, the director 0.05). |
| `IconUnderlay` | DID 52 | Sex square, derived on read. |
| `Icon` / `IconOverlay` | DID 8 / 50 | Copied from the donor to the baby. |
| `Translucency` | Float 76 | The spirit. |
| `Attuned` / `Bonded` | Int 114 / 33 | Set on imprint. |
| `GearDamage` ... `GearCritDamageResist` | Int 370-375 | Base ratings on the device, inherited 55/45 and written only when above 0. |
| `DamageRating` ... `CritDamageResistRating` | Int 307, 308, 313-316 | Effective ratings on the summoned pet, computed at summon and never written to the device. |
| `HearLocalSignals` / `HearLocalSignalsRadius` | Int 290 / 291 | Annex dialogue cues. See [section 9](#9-content-authoring-npc-dialogue-and-scenes). |

---

## 4. WCIDs

### 4.1 The annex (landblock 0x0106 variation 2)

| WCID | Name | Built from | Role | Placed |
|---|---|---|---|---|
| 78780200 | Fenwick, Kennel Intern | 42720 Ealdred | Greeter at the Drop; 14-line tutorial on click | yes |
| 78780201 | Ivo, Ruggan's Quartermaster | 46425 Marid | **Vendor** (type 12), pyreals, 11 items | yes |
| 78780202 | DJ Skulk | 5595 dancing drudge | Dance floor; suspect two | yes |
| 78780203 | Gary | 29008 Browerk | "Just here for the music"; suspect three; scale 0.275 | yes |
| 78780204 | Mrs. Ruggan | 3920 | Walk-on | yes |
| 78780206 | Fallen Sign | 8564 Old Rotted Sign, ethereal | House rules on appraisal (LongDesc). Placed at (39.0, -23.0) tipped 90 degrees about X so it lies on its back; `@create` always spawns it upright | yes |
| 78780210 | Bexley, Keeper of the Registry | 42720 | Registry lead; reads the paternity results | yes |
| 78780211 | Registered Browerk, Champion Line | 29008 | Registry pet | **not placed** |
| 78780212 | Certified Shreth, Third Generation | 4108 | Registry pet | yes |
| 78780213 | Pedigreed Ursuin (Papers Pending) | 7990 | Registry pet, scale 0.5 | yes |
| 78780214 | Drudge Skulker of Record | 7 | Registry pet | yes |
| 78780215 | Certified Sawato Bandit, Reformed | 33831 Sawato Bandit | Registry armoured pet: 4 armour pieces and a Tachi | yes |
| 78780220 | Splotch | 29008 + mutation palette | Ward lead; suspect one; scale 0.275 | yes |
| 78780221 | The Teal Incident | 4108 + palette | Ward pet, colour cycle | **not placed** |
| 78780222 | Ursuin, Unregistered | 7990 + palette | Ward pet, colour cycle, scale 0.5 | yes |
| 78780223 | Nine-Colour Shreth | 4110 + palette | Ward pet, colour cycle, scale 0.75 | **twice** |
| 78780224 | Subject Twelve | 7 + palette | Ward pet, colour cycle | yes |
| 78780225 | The Sawato Situation | baked from @et export of 78780215 | Ward armoured mutant, colour cycle | yes |
| 78780230 | Mubb | 1608 Drudge Lurker | Paternity storyline: the father | yes |
| 78780231 | Gorta | 193 Drudge Slinker | The mother | yes |
| 78780232 | Mubb Junior | 7 Drudge Skulker, scale 0.55, palette 0x04001081 | The neon baby (no colour cycle) | yes |
| 78780233 | Denton | 35462 Jarvis Hammerstone | Thinks mutations are contagious | yes |
| 78780240 | Annex Scene Director | 7, scale 0.05 | Hidden; starts every scene | yes |
| 78780264 | Annex Prismatic Generator | 31015111 TethBSDGeneratorNew | 10 Nasty Brass Monkeys (260031), 5 m scatter, 20 s regen. `SpawnColourMutationChance` 0.5 and `OnlyCombatPetsCanDamage`, both passed to the monkeys | yes, cell 0x01060163 |
| 78780241 | Scene Tester | 7 | **Test only**: click or say a phrase to start a scene | test only |
| 78780234 / 78780235 | Baby Candidate B / C | - | Retired; deleted by the paternity patch | no |

Reserved and unbuilt: 78780205 (Ruggan's Notes), 78780256 (Ancestral Gene Re-roller). The remaining ids in
78780200-78780249 are free; the next free id in the block is 78780265.

### 4.2 Items and portal

| WCID | Name | Type | Price (Ivo) |
|---|---|---|---|
| 98760388 | Portal to Seedy Motel | Portal | - |
| 78780261 | Portal to Prof. Ruggan (annex exit) | Portal | - |
| 78780258 | Pet Neutering Kit | Tool | 2,500,000 (10 MMD) |
| 78780259 | Pet Tailoring Kit | Tool | 125,000,000 (500 MMD) |
| 78780260 | Pet Tailoring Kit (Filled) | Tool | not sold; only produced by extract (Value 125,000,000, kept equal to the empty kit) |
| 78780250 | Lesser Courtship Incense | Tool | 1,250,000 (5 MMD) |
| 78780251 | Refined Courtship Incense | Tool | 2,500,000 (10 MMD) |
| 78780252 | Exquisite Courtship Incense | Tool | 5,000,000 (20 MMD) |
| 78780253 | Nurturing Draught | Tool | 2,500,000 (10 MMD) |
| 78780254 | Chromatic Catalyst | Tool | 2,500,000 (10 MMD) |
| 78780255 | Offering of Subjugation | Tool | 2,500,000 (10 MMD) |
| 78780257 | Mutagenic Serum | Tool | 25,000,000 (100 MMD) |
| 78780262 | Solidifying Tincture | Tool | 12,500,000 (50 MMD) |
| 78780263 | Fading Tincture | Tool | 12,500,000 (50 MMD) |

Ivo's sell rate is 1.0, so the price equals the item's `Value` (int 19). A stack's Value is unit price x
count in a 32-bit int, so stack sizes keep it under 2.1B: the Mutagenic Serum stacks to 50 and the Pet
Tailoring Kit to 10 (1.25B each). The filled kit never stacks, because each one holds a different look.

**The kits were renumbered on 2026-09-21.** They used to be 98760399-98760401, but on production those three
ids are other content: Tyrannical Drudge Gen (a generator placed 65 times), the Realm of Woe portal and
Doriathazaar. The constants live in `Entity/PetTailoring.cs`. **Never write to 98760399-98760401 from any
script that can reach prod.** Test servers carrying the old kits are converted by
`deploy/test-only/Move-Kits-To-7878-TEST-SERVER-ONLY.sql`, which is guarded by the kit names.

**Both portals use the standard portal model, setup 0x020001B3** (2,083 retail portals use it). They were
built with 0x02000004, which is the Armoredillo model and has no portal swirl.

**Portal 98760388:**
- Placed beside Prof. Ruggan (694201298) at 0xDB3B0019; on prod at (83.99, 1.70, 30.94). The bundle leaves that
  placement alone.
- Destination: cell 0x01060186 (35.98, -20.04, 0.005), **variation 2**, facing Fenwick. That is the Drop.

**Exit portal 78780261 (Portal to Prof. Ruggan):**
- Placed in the annex at cell 0x01060179 (29.79, -30.03, 0.005), variation 2.
- Destination: 0xDB3B0019 (80.28, 18.18, 30.70), beside Prof. Ruggan and a few steps from the motel portal.
- SQL: `Database/Updates/World/2026-09-22-05-Annex-Exit-Portal.sql`; it is in the prod bundle.

Prof. Ruggan is older custom content that is not in this repo.

---

## 5. Commands

Access levels, lowest to highest: Player, Advocate, Sentinel, Envoy, Developer, Admin. Most pet admin
commands act on the last appraised essence, or on the summoned pet's device if nothing is appraised.

### 5.1 Player

| Command | What it does |
|---|---|
| `@dance` | The breeding trigger: the same as typing `*dance*` (`PlayerCommands.cs:4311`). |
| `@breed-debug` | Breeding diagnostics: switch, your landblock, cell and variant, whether the area matches, your pet, its sex and charges, and a nearby partner count. The count is loose (anyone with a combat pet within 30 m or in the landblock). Admins also get every candidate's details. |
| `@pet-name <name>` | Rename request for staff approval. See section 7.1. |
| `@pets` | The account's Pet Registry. |
| `@petdesc` / `@pet-desc` | Dumps the look actually sent for **your own** summoned pet: model, palette base and template, every sub-palette, and which render path ran. Player-level since 2026-09-21 for troubleshooting with staff. |

### 5.2 Developer

| Command | What it does | Gotchas |
|---|---|---|
| `@mutate_pet [setupId] [paletteId\|random] [scale] [shade]` | Recolours the selected summoned pet (live redraw) or the appraised essence (saved). With no palette given, it rolls from the master pool. | The help text says "filtered pool"; it is the master pool. Scale and shade on a live pet are not saved. |
| `@pet-debug` | The same as `@breed-debug`. | |
| `@pet-dump [note]` | Writes a `[PetTrace] device.dump` record to the server log, whatever `pet_trace` is set to. | Output goes to the log, not chat. |
| `@breed-replay <json>` / `@breed-replay file <path>` | Runs a REPLAY blob through `BreedingMath.Simulate` and prints PASS or FAIL. | `file` mode skips `[PetTrace] breed.replay` lines; paste single lines instead. |
| `@testpal_*` | Legacy palette experiments. | Not used by breeding. Don't use. |
| `@et` / `@export-template [wcid] [npc\|monster] [overwrite] [name...]` | Exports the selected object's rendered look and properties as a new weenie in the staging block, loads it into `ace_world` and posts it to Discord. | Refuses a pet device ("summon the pet and select it"). The default flavour is `monster`. |
| `@createinst <wcid>`, `@removeinst [wcid]`, `@nudge`, `@rotate` | Place, remove and adjust placements at **your** variation. | `@removeinst <wcid>` removes the **nearest** copy in your landblock and variation. |
| `@export-sql <landblock> landblock` | Exports the placements in the variation you stand in, as a hex landblock id (for example `0106`). | It copies test's guids; use the bundle exporter for prod. |
| `@reload-landblock` | Destroys and reloads every non-player object in the current landblock. | Summoned pets are destroyed. A standing spirit counts as lost, and its birth completes. |
| `@clearcache [type]` | Clears the world database caches. | Doesn't reload live objects; follow it with `@reload-landblock`. |
| `@spawnshiny <wcid> [n]` | Spawns shiny test creatures. | |

### 5.3 Admin

| Command | What it does | Gotchas |
|---|---|---|
| `@breed` / `@pet-breed-test` | Forces a breed with a partner. It skips only the partner's dance window; every other gate applies. Both owners are told it was forced. | |
| `@setsex [male\|female\|derive]` (also `@setalpha`, `@makealpha`) | Sets the sex override. Male refills charges. | **Does not repaint the sex square** until the item is re-sent. |
| `@pet-make-alpha` | **Toggles** the sex, refills charges and repaints the square. | A toggle, not a set. |
| `@pet-reset-cooldown` | Clears the female cooldown and refills male charges. | |
| `@pet-cleanse-palette` | Removes all palette overrides **and** the captured palettes. | The captured palettes can't be restored. |
| `@pet-set-mutations <dmg> <dr> <crit> <vit> [pot]` | Writes the mutation counts. | Doesn't change `PetPotencyStored`. |
| `@pet-set-maturity <juvenile\|adult\|kills>` | Sets growth, and resizes and heals a summoned pet. | **Writes `PetMaturityKills` on a captured essence, which marks it bred: it will imprint on its next summon.** |
| `@petserverconfig` | Lists every effective `pet_*` setting with `/modify` lines. | |

---

## 6. Breeding gates and messages

Gates run in `CheckMultiplayerBreeding` (`PetDevice_Breeding.cs:671-1276`) in the order below.
"TE" means a transient error: it flashes centre-screen and is **not written to chat**. Admins also get
`[Breeding Debug]` lines, and with `pet_trace` on each refusal is recorded as a `breed.gate` entry.

**The admin bypass covers location only:** the area check, the partner's landblock and area, and the
same-landcell check. It is keyed on the dancer. Sex, tier, bond, charges, cooldown and pack space
still apply to admins.

| # | Gate | Refused when | Message |
|---|---|---|---|
| 1 | enabled | Breeding is disabled | admins only |
| 2 | trading | The dancer is trading | admins only |
| 3 | noPet | No summoned combat pet | admins only |
| 4 | location | Not in the breeding area | TE "You must be in the designated breeding area to perform the breeding ritual." |
| 5 | device1 | The dancer's device is missing | TE "Failed to locate parent summoning device." |
| 6 | partnerScan | No candidate: another online player, not trading, same landblock, in the area, danced within the window, with a summoned combat pet, **and both pets in the same landcell** | chat "[Breeding] Your pet performs the courtship dance, waiting for a partner... (Your partner must have their pet summoned in the same room and \*dance\* within 5 seconds)." |
| - | partner choice | Compatible candidates are preferred, nearest first. If none is compatible, the nearest is used so the player gets a real refusal. | |
| 7 | busy | Either player is busy | admins only |
| 8 | pendingGuardian | Either player has a spirit pending | chat "[Breeding] You already have a mating guardian to defeat. Finish that ritual first." (and variants) |
| 9 | device2 | The partner's device is missing | TE "Failed to locate parent summoning devices." |
| 10 | neutered | Either is neutered | TE "Your pet is spayed/neutered and cannot breed." / "(partner)'s pet is ..." |
| 11 | inventory | A device is not in its owner's inventory | TE "Summoning devices must remain in inventory to breed." |
| 12 | tierLookup | A wcid is not in `PetDeviceWcids` | TE "Failed to determine parent pet tiers." |
| 13 | minParentLevel | Tier below `min_parent_level` | TE "Parent pets must be at least tier 100 to breed." |
| 14 | minBond | Bond below `min_bond` | TE "Parent pets must have a bond level of at least 100 to breed." |
| 15 | shiny | Either is shiny and shiny is disallowed | TE "(device) is shiny and cannot breed. Shiny is a capture-only trait." |
| 16 | juvenile | Either is a juvenile | TE "(device) is still a (stage) and cannot breed until it is an adult (k/300 kills)." |
| 17 | sex | Not exactly one male | TE "Breeding cancelled: two males cannot breed. You need one male and one female." |
| 18 | maleCharges | The stud has no charges | TE, **to the stud's owner only**: "(device) has exhausted its 10 daily breeding charges. Rest for 24h." |
| 19 | femaleCooldown | The dam is still resting | TE, **to the dancer** (not necessarily the dam's owner): "(device) is still recovering from her last litter. Ready in Xh Ym." |
| 20 | packSlot | The dam's owner has no main-pack slot | TE "Breeding cancelled: your main pack has no free slot for the baby. Free a slot and dance again." |
| 21 | burden | The dam's owner can't carry the baby | TE "Breeding cancelled: you are too encumbered to carry the baby. Lighten your load and dance again." |

**On success** (`PetDevice_Breeding.cs:1084-1269`), in order:

1. Both players get "The mating ritual has begun between (pet1) and (pet2)...".
2. Incense is read and removed from both devices.
3. `BreedingMath.Simulate` runs.
4. The stud spends a charge and its owner sees "[Breeding] (stud) spent a breeding charge: 9/10 left
   today.".
5. The dam's cooldown is written.
6. A 50/50 roll picks the species donor.
7. A palette is rolled if the breed mutated, from the vibrant pool if either parent has a catalyst,
   otherwise from the master pool. The catalyst is consumed.
8. Both parent devices are saved.
9. The spirit spawns, or `CompleteBirth` runs.

**Birth** (`CompleteBirth`, `:1679-1901`):

- **The baby:** created from the donor's wcid and name. It gets the donor's visual set and capture
  strings, bond 1, not attuned, the inherited potency, and gear ratings (written only when above 0).
  All five mutation counts are written.
- **A mutation colour** sets the native base and the template, and clears the captured palettes.
- **It is marked juvenile, then delivered:**
  - **Delivered:** "Congratulations! A baby pet has been born: (name)! Placed in (winner)'s inventory."
    plus " [GENETIC MUTATION] Gained (list) & Rare DAT Palette unlocked!". The players get
    VisionUpWhite and the pets WeddingBliss.
  - **Owner offline or packs full:** written to the owner's persisted inventory, with a "tucked away"
    message. **Never dropped.**
- **Parents:** dismissed 2 s later.

---

## 7. Consumables and kits

All of these are used on a pet device **in the player's inventory** (`Player_Use.cs:173`). Only the
Mutagenic Serum checks that the target is a **combat** device. The others accept any `PetDevice`,
passive ones included.

| Item | Effect | Refused (not consumed) when | Consumed |
|---|---|---|---|
| Courtship Incense (78780250/51/52) | Sets `PetIncenseBonus` to 0.025, 0.05 or 0.10. Both parents' bonuses add to the base before the decay (`(base + incense) / (1 + decay x mutations)`), capped at +0.50 combined, so the practical maximum is +0.20 on a fresh line. Doesn't affect potency. | The essence is neutered, or already has an equal or stronger bonus. A stronger bonus replaces a weaker one, and the weaker one is lost. | By the next committed breed, mutated or not |
| Nurturing Draught (78780253) | `PetMaturityXpMultiplier` = 2.0: each qualifying kill credits 2. | The raw `PetIsJuvenile` flag is not set, or the draught is already active. | Cleared at adulthood |
| Chromatic Catalyst (78780254) | Sets `PetChromaticCatalystActive`: a rolled palette comes from the vibrant pool. | Already active. | Only when a palette is rolled, from both parents |
| Offering of Subjugation (78780255) | Sets `PetGuardianWeakened`: the spirit hits for x0.5, takes x2.5 damage, and its per-hit cap rises from 10% to 25% of its health. | Spirits are disabled, or the offering is already active. | When a spirit spawns, from both parents. |
| Solidifying / Fading Tincture (78780262 / 78780263) | Steps `PetTranslucency` (float 9062) down / up by 0.1, between 0 and 0.5. The starting level is the essence's own value, else its summon template's `Translucency` (0.5 for Maiden and K'nath, else 0). `PetDevice` applies it at summon; 0 removes the property. | Not a combat essence, or the step would pass 0 or 0.5. | On success |
| Mutagenic Serum (78780257) | Rolls a master-pool palette onto the essence the way a bred mutation is written. Recolours a live pet on the same landblock group. | Not a combat essence; captured textures cover at least 90% of the body parts; **the model mostly uses full-colour textures** (60%+ of its drawn polygons have non-palette textures such as R8G8B8, e.g. the Spectral Nanjou Shou-jen); or the pool is empty. | On success |
| Pet Neutering Kit (78780258) | Sets `PetNeutered` permanently. | Already neutered. | On success |
| Pet Tailoring Kit (78780259) | Extract: creates "Pet Tailoring Kit (creature)" holding the source's full visual set; **the source essence is destroyed**, and only after the kit is safely in the pack. | Not a combat essence, no `VisualOverrideSetup`, pet summoned, in a trade, or pack full. | Tool and source essence |
| Filled kit (78780260) | Apply: copies the visuals only, including the shiny variant, creature type, wcid and **damage type**. Keeps stats, potency, bond, sex, mutations, maturity and imprint. Renames the essence unless it has a `PetCustomName`. Repaints the sex square. | Not a combat essence, the kit is empty, pet summoned, or in a trade. | On success |

### 7.1 Pet naming

**`@pet-name <name>`** (`PlayerCommands.cs:1755-1948`):

- **Target:** the summoned combat pet's device, otherwise the **selected** combat device the player
  owns.
- **Rules:**
  - 3-32 characters, matching `^[a-zA-Z0-9' -]+$`.
  - Not the current name (the essence name or the pet's own name).
  - 60 s per-character in-memory cooldown.
  - There is no profanity or uniqueness check.
- **Storage:** one pending row per character in `ace_shard.pet_name_requests`. A new request closes the
  pending one as denied ("Replaced by a newer request", reviewed by `system`) and inserts a new row, in
  one transaction, so a reviewer who loaded the old name cannot approve the new one unseen: their
  Approve on the old id gets "already been reviewed". Optionally posts to Discord, and confirms in chat.

**Approval** (`Controllers/PetNamingController.cs`, `/api/PetNaming`, portal page `/pet-names`):

- **Access:** every action requires portal admin or the `pet-naming` page permission.
- **Approve:** an atomic claim (`status 0 -> 1`).
- **If the owner is online:** the rename runs on the world queue, waiting up to 10 s. The device must
  be in the owner's possession and still carry `old_name`. It sets `VisualOverrideName` and
  `PetCustomName` to the new name, and swaps only the creature part of `Name`, so
  "Slash Spectral Nanjou Shou-jen Essence" becomes "Slash Sir Fluffington Essence" (damage word and tier
  kept). It renames a summoned pet live and tells the owner.
- **If the owner is offline:** it edits the shard biota directly.
- **Name mismatch:** auto-deny and HTTP 409. A transient failure returns the request to pending
  (HTTP 500).
- **Deny** takes a reason of up to 255 characters. **The player gets no in-game message on a deny.**

Status values: 0 pending, 1 approved, 2 denied.

---

## 8. Content: the annex

### 8.1 Location

| | Value |
|---|---|
| Landblock | **0x0106** (262). The base variation is a retail drudge dungeon. |
| Variation | **2**. A private copy holding only annex content, because variations do not inherit base placements. |
| Breeding setting | `pet_breeding_allowed_landblock` 262, `pet_breeding_allowed_variant` 2 (stored in the shard) |
| Entry | Portal 98760388 in Lin, arriving at the Drop (cell 0x01060186) |
| Exit | Portal 78780261 at cell 0x01060179, back to Prof. Ruggan |
| Floor | One floor, z roughly 0. Placements span x 34..74, y -33..-6. |

**Same landcell:** breeding needs both **pets** in the same landcell. The whole landblock is the area,
but players must stand their pets together. To restrict breeding to one room, set
`pet_breeding_allowed_landblock` to that room's full 32-bit cell id. Walk the whole room with
`@breed-debug` first: if the cell id changes anywhere inside it, the room is more than one cell.

### 8.2 Cast and layout

- **Registry wing:** Bexley, 78780212-78780215.
- **Ward wing:** Splotch, 78780222-78780225.
- **The Drop:** Fenwick, Mrs. Ruggan, the fallen sign at Fenwick's feet, and the exit portal behind.
- **Ivo's nook.**
- **The dance floor:** DJ Skulk, Gary.
- **Room 4:** Mubb, Gorta, Mubb Junior.
- **Hallway:** Denton.
- **Hidden:** the director, in a corner, within 60 m of Mubb, Denton and Bexley.
- **Lower level:** the prismatic monkey generator (cell 0x01060163).

The longest scene link in the current layout is Mubb to DJ Skulk, at 37 m.

### 8.3 The paternity storyline (`2026-09-22-00-Annex-Paternity-Scenes.sql`)

A generated file: `gen_paternity.py` built it from a scene table, and its checker runs on every
change.

| Scene | Cue prefix | Share of starts | Lines |
|---|---|---|---|
| He's NEON, Gorta | `pat_neon` | 16% | Mubb, Gorta, Junior |
| He's getting BRIGHTER | `pat_brighter` | 16% | Mubb, Gorta, Mubb |
| Suspect one: Splotch | `pat_splotch` | 16% | Mubb, Splotch, Gorta, Splotch |
| Suspect two: the DJ | `pat_dj` | 16% | Mubb, Gorta, Mubb, DJ Skulk (cue sent twice, see 9.4) |
| Suspect three: Gary | `pat_gary` | 16% | Mubb, Gorta, Gary, Mubb |
| Denton, from a safe distance | `pat_denton` | 14% | Denton, Mubb, Gorta, Denton |
| The Registry results (rare) | `pat_results` | 6% | Bexley, Gorta, Bexley, Mubb, Gorta, Mubb, Junior |

Click pools (private Tells): Mubb 5, Gorta 5, Junior 4, Denton 4.

The patch also:
- gives DJ Skulk `HearLocalSignals` (the original annex file strips it)
- deletes Bexley's and Splotch's old feud **openers**; their replies stay in place, unused

### 8.4 Idle lines

- **Fenwick:** 3 lines at 0.0067, 0.0133 and 0.02.
- **DJ Skulk:** 3 lines at 0.0133, 0.0267 and 0.04, plus his stance-gated dance motions.
- **Gary:** 1 line at 0.02.
- **Mrs. Ruggan:** 1 line at 0.03.

See section 9.2 for why the values are stacked. Idle lines are not scheduled by the director, so they
can occasionally land mid-scene.

### 8.5 Colour cycle

Ward pets 78780221-78780225 have `ShowcaseColourCycleSeconds` = 60 (`2026-09-22-02` and `-03`). The
Registry pets deliberately don't: they stay brown. Mubb Junior deliberately doesn't either: he stays
neon for the jokes.

### 8.6 The Sawato bandits (`2026-09-22-03`)

- **78780215:** an NPC clone of 33831 wearing Amuli coat and leggings, Nariyid boots, Helm of the
  Crag and a Tachi.
- **78780225:** the same look **baked** into anim-part and texture rows from `@et` export 78790009,
  with **all palette rows removed** and a 0x04 mutation template added. Equipped armour keeps its own
  palettes, so a mutation colour on an NPC wearing armour only reaches the skin. Baking the armour into
  the model lets the colour cover everything, exactly as on a bred armoured pet.

---

## 9. Content: authoring NPC dialogue and scenes

All of this is data in `weenie_properties_emote` (one row per set) and
`weenie_properties_emote_action` (one row per action). Engine: `WorldObjects/Managers/EmoteManager.cs`.

### 9.1 Categories and actions you will use

| Category | Id | Fires when |
|---|---|---|
| HeartBeat | 5 | Every heartbeat (about 5 s), unless the creature is awake. Sets with a `style`/`substyle` only fire in that stance; leave both NULL. |
| Use | 7 | A player clicks the NPC (needs `ItemUseable` int 16 = 32). |
| HearChat | 24 | A player says something in local chat within 96 m, and the NPC is known to them (visible). `quest` = the exact phrase, compared case-insensitively. **A NULL `quest` matches every line said nearby.** |
| ReceiveLocalSignal | 37 | Another object in the same landblock sends a LocalSignal whose name equals `quest`. |

| Action | Id | Notes |
|---|---|---|
| Say | 8 | Everyone within 96 m sees "(name) says, ...". |
| Tell | 10 | Only the target player sees it. No-op without a player target. |
| LocalSignal | 88 | `message` = the cue name, broadcast to the landblock. |
| Motion | 5 | Returns the animation length as a delay: **it keeps the NPC busy**. |

- **`delay` is a pre-delay**: the number of seconds to wait before that action runs.
- **Text must be 7-bit ASCII** (CLAUDE.md): no curly quotes, em dashes or emoji. Apostrophes are
  fine; double them in SQL (`''`).

### 9.2 Probability: stacked thresholds

`GetEmoteSet` (`EmoteManager.cs:3660`) rolls r in [0,1), keeps the sets whose probability is **greater
than r**, and takes the **lowest**. So the values are thresholds, not independent chances:

- Three lines at 0.01, 0.02 and 0.03 each fire 1% of the time, 3% in total.
- **Equal values mean only the first line ever fires.** That was a real bug, fixed by
  `2026-09-21-00-Annex-Idle-Line-Weights.sql`.
- A click pool of n lines at 1/n, 2/n ... 1.0 is an even pick.
- A cue response at 1.0 always fires.

### 9.3 Local signals

`Landblock.EmitSignal` (`Entity/Landblock.cs:1981`) delivers a cue to every object in **the same
landblock instance** (so the same variation) that has:

- `HearLocalSignals` (int 290) not 0
- a **straight-line** distance to the emitter within **its own** `HearLocalSignalsRadius` (int 291).
  Walls and floors don't matter; height does.

**The emitter never hears its own cue.** The annex uses a radius of 60 m.

### 9.4 The busy rule (the most important one)

`ExecuteEmoteSet` returns early while `IsBusy` (`EmoteManager.cs:3712`). An NPC is busy from the moment
a set starts until its last action finishes, **including every pre-delay**. A busy NPC silently drops:

- incoming cues, and the scene stops there
- clicks
- chat phrases
- its own heartbeat rolls

Design consequences:

- **One director starts every scene.** It sends the first cue, then a final no-op action (a
  LocalSignal nobody hears, `pat_rest`) with delay 120. That keeps it busy for 120 s, so **only one
  scene can run at a time**.
- **Actors have no long-running idle sets.** Short idle Says with no delay barely register as busy.
- **Motion loops make an NPC busy.** DJ Skulk's dance can swallow a cue, so his cue is sent twice, 2.5 s
  apart. His reply waits 4.5 s, so if he caught the first copy he is busy for the second and ignores
  it.
- **A click pool with long pre-delays** (Bexley's and Splotch's run about 10 s) can drop a scene cue
  that arrives during a click. This is rare, and accepted.

### 9.5 Adding a scene

1. Write it as a table: `speaker | delay | line`. Keep it to 2-5 lines, and make the first line make
   sense to a player who walks in cold.
2. Choose a cue prefix, for example `pat_newscene`. Line n is a category-37 set on its speaker with
   `quest = prefix_n`. It says the line, then sends `prefix_(n+1)`. The last line sends nothing.
3. Add a director HeartBeat set that sends `prefix_1` and then `pat_rest` with delay 120. **Re-stack
   every director threshold** so the new scene takes a share and the total stays about 0.05.
4. Give every speaker `HearLocalSignals` 1 and radius 60, and keep speakers within 60 m of whoever
   cues them.
5. Add a Scene Tester HearChat set with an **exact phrase** and a click threshold.
6. Check it:
   - every cue has exactly one listener
   - no loops
   - no NPC cues itself
   - thresholds are distinct
   - no NULL-quest HearChat
   - ASCII only
   - dry-run in a rolled-back transaction

   The generator and checker (`gen_paternity.py` and `check_paternity.py`) do all of this. Extend them
   rather than hand-writing SQL.

### 9.6 Director tuning

| Knob | Current | Effect |
|---|---|---|
| Rest | 120 s | The minimum gap between scene starts. |
| Total chance per heartbeat | 0.05 | About 100 s of rolling on average after the rest. |
| Result | | A scene starts about every 3-4 minutes (roughly 16 an hour). The room is quiet about 90% of the time. |
| Per-scene share | 0.003-0.008 | The Results scene gets 0.003 (about once an hour). |

The director only runs while the landblock is loaded. The Scene Tester rests 35 s, the length of the
longest scene.

---

## 10. Content: building and placing NPCs

### 10.1 Cloning pattern

Every annex NPC is a clone of a retail weenie that already renders correctly: `INSERT ... SELECT` from
every property table, then overrides. Monster templates are cloned **without** their create lists, so
they carry no loot or weapons. To make one a harmless NPC:

- Delete ints 67 (`Tolerance`) and 68 (`TargetingTactic`). `Creature.IsNPC` needs `!Attackable &&
  TargetingTactic == None`.
- Set bool 19 `Attackable` = False, bool 1 `Stuck` = True and bool 98 `Invincible` = True.
- Set int 16 `ItemUseable` = 32 (clickable), int 95 = 8 and int 133 = 4 (radar), and int 134 = 16.
- For dialogue, set int 290 = 1 and int 291 = 60.

### 10.2 Colours and sizes

- **Mutation colour on an NPC:** PaletteTemplate (int 3) = a full 0x04 palette id. Use ids from the
  breeding pools or from real bred pets (`ace_shard.biota_properties_int` type 9035), so they are known
  to render.
- **Armoured NPCs:** equipped items keep their own palettes. For a whole-body mutation colour, bake the
  look (next section) and drop the palette rows.
- **Size:** DefaultScale (float 39). Scale doesn't affect cue hearing.

### 10.3 Baking a look with `@et`

1. Place the normal NPC and select it.
2. Run `@et npc <name>`. It writes a staging weenie (7879xxxx) with the rendered anim parts, textures
   and flattened subpalettes, and loads it.
3. Copy it into the 7878 block **as literal rows**, so the file doesn't depend on the staging weenie.
   `gen_bandit_mutant.py` is the worked example.
4. Delete the staging weenie when you no longer need it. `@et` also writes a file to the content folder.
   That folder is only re-applied at startup when **both** `Offline.AutoUpdateWorldDatabase` and
   `Offline.AutoApplyWorldCustomizations` are true (`Program.cs:226-231`); with `AutoUpdateWorldDatabase`
   false, as configured here, it never is.

### 10.4 Placing

- `@createinst <wcid>` places at your position **in your variation**; stand in the annex.
- `@nudge` and `@rotate` write straight back to the placement.
- `@removeinst <wcid>` removes the nearest copy in your landblock and variation.
- Placements are **not** in the world SQL patches. They reach prod through the bundle (section 13).

---

## 11. Testing

### 11.1 Unit tests

```
dotnet test Source/ACE.Server.Tests/ACE.Server.Tests.csproj --filter "FullyQualifiedName~Pet|FullyQualifiedName~TemplateExport"
```

MSTest, net10.0, x64.

| Class | Covers |
|---|---|
| `PetBreedingInheritanceTests` | 55/45 pick and the tie rule, package deals, gear-only lines, potency inheritance, hard cap resolution, potency step, derived crits, `MatchesBreedingArea` (anywhere, landblock, exact cell, variant) |
| `PetBreedingTests` | Tier flooring |
| `PetBreedingParityTests` | 11 REPLAY blobs, the two-phase guardian path equal to one-shot, determinism, failure detection, parsing, summon maths |
| `PetTraceTests` | Trace record format |
| `PetNamingTests` | Summon name resolution only |
| `PetMutationServiceTests` | `ApplyMutationPalette`, the serum wcid, colour visibility |
| `PetPotencyFormulaTests` | Potency formulas (a separate system) |
| `TemplateExportTests` | `@et` argument parsing, wcid allocation, filters, builders |
| `EnumCollisionTests`, `SqlPatchSanityTests` | Property id collisions; SQL patch ASCII and LF |

**Building:** never check compilation with `-t:Compile`. It writes a DLL with no embedded resources
to `obj/`, and later builds ship it. Build x64 Release with the server stopped, or build to a scratch
folder with `-o`.

### 11.2 Fast in-game test settings

For a test session only; revert them after.

```
@modifybool pet_bond_enabled true
@modifylong pet_breeding_min_bond 1
@modifybool pet_breeding_force_mutation true
@modifybool pet_breeding_guardian_enabled true
@modifybool pet_breeding_bypass_male_charges true
@modifybool pet_breeding_bypass_female_cooldown true
@modifylong pet_maturity_kills_required 5
@modifybool pet_trace true
```

### 11.3 In-game recipes

| Feature | Steps | Expect |
|---|---|---|
| **Location** | `@breed-debug` in the annex, and again outside it | "VALID" inside, "INVALID" outside |
| **Basic breed** | Two characters, tier 100+ male and female, pets side by side, both `*dance*` within 5 s | Ritual message, charge message, birth to the dam's owner, parents dismissed |
| **Same-cell rule** | Pets in different rooms, both dance | "waiting for a partner" |
| **Gates** | Same sex, tier 80, shiny, neutered, juvenile, stud at 0 charges, resting dam, dam owner with a full pack | Each TE in section 6, with nothing spent |
| **Mutation** | `force_mutation` on | "[GENETIC MUTATION]", a new colour on the baby |
| **Spirit** | Guardian and force on | Spirit spawns; parents kill it; blessing message |
| **Spirit timeout** | Guardian on, don't fight | Fades after 90 s; birth without blessing |
| **Offering** | Offering on either parent | Spirit hits softer, falls faster; offering consumed from both |
| **Catalyst** | Catalyst plus force | Vivid colour; catalyst consumed |
| **Incense** | Incense, `pet_trace` on | `breed.roll` chance raised; incense removed after the breed |
| **Maturity** | Summon a newborn, kill qualifying creatures | Stage messages; adulthood at 300 (or 5 with the test setting) |
| **Imprint** | Hand a newborn to a second character, summon it there | Imprint message; trade then blocked |
| **Serum** | Use on a summoned essence | Live recolour |
| **Tailoring** | Extract from a spare essence, apply to another | Source destroyed; target changes look but keeps its stats |
| **Naming** | `@pet-name Test Name`, approve on `/pet-names` | Online rename message |
| **Scenes** | Scene Tester: click, or say `scene neon`, `scene brighter`, `scene splotch`, `scene dj`, `scene gary`, `scene denton`, `scene results` | Every line in order; the Tester ignores input for 35 s |
| **Director** | Remove the Tester, place the director, wait | A scene about every 3-4 minutes, never two at once |
| **Colour cycle** | Stand in the Ward for a minute (needs the new build) | Each Ward pet sparkles and changes colour at a different moment |
| **Portal** | Use the portal beside Prof. Ruggan | Arrive at the Drop, facing Fenwick; both portals show the swirl model |
| **Exit portal** | Use the Portal to Prof. Ruggan in the annex | Arrive beside Ruggan, clear of the motel portal |
| **Fallen sign** | Appraise it at Fenwick's feet | House rules; it lies on its back (reload the landblock, `@create` shows it upright) |
| **Tinctures** | Solidifying five times on a Maiden or K'nath essence, then once more; Fading on a normal one | 40, 30, 20, 10, solid, then refused; appraisal shows the level; resummon to see it |
| **Pets-only** | Hit a monkey yourself (melee, a war spell, a ring spell such as Rocky Shrapnel), then send your pet | You do nothing and get "can only be harmed by combat pets" once per 10 s; debuffs still land; the pet kills it |
| **Spawn colour** | Watch the monkey generator respawn; capture a coloured monkey | About half are coloured; the captured pet keeps its colour |
| **Rename** | `@pet-name A` then, after 60 s, `@pet-name B`; approve on `/pet-names` | Two rows: A denied "Replaced by a newer request", B pending; approving B keeps the damage word and tier in the essence name |

### 11.4 Database verification

Useful read-only checks:

```sql
-- every annex placement, by variation
SELECT weenie_Class_Id, HEX(landblock), variation_Id, COUNT(*) FROM landblock_instance
WHERE weenie_Class_Id BETWEEN 78780200 AND 78780249 GROUP BY 1,2,3;

-- idle and click thresholds must be distinct per NPC and category
SELECT object_Id, category, probability, COUNT(*) FROM weenie_properties_emote
WHERE object_Id BETWEEN 78780200 AND 78780249 AND category IN (5,7)
GROUP BY 1,2,3 HAVING COUNT(*) > 1;

-- portal destination
SELECT HEX(obj_Cell_Id), origin_X, origin_Y, origin_Z, variation_Id
FROM weenie_properties_position WHERE object_Id = 98760388;
```

---

## 12. Observability: trace, replay, diagnostics

- **`@breed-debug`:** location match, pet, sex and charges. The fastest first check.
- **`pet_trace`:**
  - **Record format:** one line per event, `[PetTrace] <event> | session=<8 hex> | t=<ISO> | k=v |
    ...`. Each breed attempt gets one session id, shared with its spirit and that spirit's fight.
  - **Events:** `breed.trigger/gate/attempt/config/inherit/roll/replay/commit`,
    `guardian.spawn/skipped/slain/timeout/lost`, `consumable.*`, `birth`, `device.dump`,
    `neuter.apply`, `maturity.kill/imprint` and `tailoring.*`, plus server-wide `combat.*`.
  - **To find a session:** `findstr "session=abcd1234" <log>`.
- **REPLAY blobs:** a one-line JSON (`{model,parentA,parentB,config,options,rngDraws,baby}`) that
  reproduces a breed exactly. Paste one line into `@breed-replay` to check that server and website
  maths agree. `file` mode skips `[PetTrace]` lines.
- **The draw order is a contract.** RNG draws are consumed in a fixed order:
  1. 1-8: the eight inheritance lines
  2. 9: the stat roll, always drawn
  3. 10: the line pick
  4. 11: the potency roll
  5. 12: the blessing

  Changing that order breaks every stored REPLAY and the website simulator. Add new draws at the end,
  and update `breedingModel.ts` together with the server.
- **`@petdesc`:** which render path a pet's colour took, and every subpalette sent.
- **Visualizer** (`/api/Visualizer`): previews meshes and palettes, and serves the live breeding config
  anonymously.

---

## 13. Deployment tooling

**Source files** (`Database/Updates/World`), in date order:

| File | Contents |
|---|---|
| `2026-07-26-00` | Portal (destination now the annex) |
| `2026-09-09-00` | 15 annex NPCs |
| `2026-09-09-01` | Kits |
| `2026-09-12-00` | Consumables |
| `2026-09-19-00` | Serum |
| `2026-09-21-00` | Idle weights |
| `2026-09-22-00` | Paternity |
| `2026-09-22-01` | Sizes |
| `2026-09-22-02` | Ward colour cycle |
| `2026-09-22-03` | Bandits |
| `2026-09-22-04` | Portal destination patch |

**Re-running an older file after a newer one undoes the newer fix.** For example, `2026-09-09-00`
re-adds the feud openers and deafens DJ Skulk. Run them in order, or not at all.

**Prod bundle:**
- `deploy/export_annex_bundle.py` reads the **test** database and writes `deploy/Annex-Prod-Bundle.sql`:
  every weenie copied as literal rows, plus all placements with **fresh guids** computed on the target
  (`MAX(guid)` in the landblock's static range, plus n).
- Test-only NPCs are left out.
- It is proven by running the bundle on test inside a rolled-back transaction: all 2,388 rows
  identical.
- Regenerate it after **any** change on test.

**Prod shard script:** `deploy/Annex-Prod-Shard.sql` creates `pet_name_requests` and writes the
settings. It has a wrong-database guard.

**The plan:** `deploy/PROD_DEPLOY_PLAN.md`: steps, the settings table, open questions and rollback.

**Test backup:** `C:\Scripting\db-backups\2026-09-21-test-before-prod-restore\` holds full test dumps
plus every SQL file. Restore it before regenerating the bundle if test has been overwritten.

---

## 14. Gotchas

- **Stored config overrides code.** A stored value always wins over the code default (0x0106 / 2). Always check
  `@fetchlong` / `@petserverconfig` (not `@showprops`, which crashes).
- **`pet_bond_enabled` false means no breeding, ever.**
- **Transient errors never reach chat.** Players miss them, so tell them to watch the screen.
- **`@setsex` doesn't repaint the sex square.** Use `@pet-make-alpha`, or relog.
- **`@pet-set-maturity` on a captured essence marks it as bred**, so it imprints on its next summon.
- **`@breed-replay file` skips trace lines.** Paste them one at a time instead.
- **A restart during a spirit fight loses the baby** (known issue H2). The charges, cooldown and
  consumables are already saved.
- **Busy NPCs drop cues.** See 9.4. Motion loops and long click pools are the usual culprits.
- **Equal emote probabilities:** only the first set ever fires.
- **A NULL-quest HearChat set** answers every line spoken nearby.
- **Variations don't inherit** base placements, and `@createinst` places in *your* variation.
- **Armour recolours only where its textures take a palette.** A full mutation palette is added last and
  overrides paletted armour (loot-gen gear, the Sawato Situation). Custom armour with full-colour (R8G8B8)
  textures keeps its look; measure it with the per-polygon rule in `PetMutationService.MeasureFixedColourPolygons`.
- **Anything that calls `TakeDamage` directly and prints its own hit line must check `CanBeDamagedBy`
  first.** The ring AoE in `Player_Magic.cs` did not, so a pets-only monster showed 11,000,000-point hits
  that never landed. `TakeDamage` refuses the damage, but it cannot take back a message already built.
- **`landblock_instance.guid` is AUTO_INCREMENT.** Setting it to NULL invents a guid instead of failing; the
  bundle's range guard raises error 1242 with a two-row subquery instead.
- **`mysql --abort-source-on-error` does not exist in MySQL 8.0.** Batch mode (`mysql db < file`) already stops
  at the first error; never add `--force`.
- **Content-folder SQL is not applied at startup here.** `AutoApplyWorldCustomizations` only runs inside the
  `AutoUpdateWorldDatabase` block (`Program.cs:226-231`), which is off. The setting reading `true` in
  `Config.js` is misleading on its own.
- **Never verify compilation with `-t:Compile`**, and stop the server before an x64 Release build.
- **Workbench:**
  - It runs against the **last selected schema**.
  - It keeps executing after an error.
  - It leaves a failed transaction holding locks until you run `ROLLBACK` in the same tab.
- **Stale code text:**
  - The `pet_breeding_enabled` and `pet_breeding_force_mutation` descriptions.
  - The `PetIsMaleOverride` comment.
  - A comment in `PetDevice_BreedingReplay.cs:14`.
