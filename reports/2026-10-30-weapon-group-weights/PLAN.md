# T11+ weapon drops by group: implementation plan

Branch `feature/weapon-group-weights`, worktree `C:\Scripting\ACECustom-weapongroups`, off master `fe5868e5d`.
Status: plan only, nothing built.

## Goal

Replace the hardcoded "equal thirds" weapon lane draw (owner ruling 2026-08-31) with **five weapon groups
weighted from the Zone Control store**, so the split is tunable live with no release.

| Group | What it rolls | Default weight |
|---|---|---|
| `melee_1h` | any one-handed melee weapon; Heavy / Light / Finesse picked evenly inside it | 1 |
| `melee_2h` | a Two-handed weapon (axe, mace, spear or sword line) | 1 |
| `missile` | bow / crossbow / atlatl, 1/3 each (as today) | 1 |
| `war` | a non-nether caster (the 7 War elements) | 1 |
| `void` | a nether caster | 1/7 |

Reasoning agreed with the user (2026-10-29/30):
- Players use Heavy / Light / Finesse interchangeably, so they form ONE bucket. Today that bucket is already
  28.6% - roughly level with Missile (33.3%) and War (29.1%). The real outlier is Two-handed at 4.8%.
- Groups are equal by **number of weapons a player needs to be effective**. War needs one caster per element
  (7), Void needs one (nether only), so Void = War / 7. Today's caster table already happens to give Void
  exactly one War element's worth (3 of 24 equally weighted entries).

Simulated on the live store (regular 5-8, leader 7-10, boss 9-15; weapon weight 3 of 9), share of weapon
drops / corpses with at least one:

| Group | Today (regular) | New (regular) | New (leader) | New (boss) |
|---|---|---|---|---|
| 1H melee | 28.5% / 48% | 24.1% / 42% | 24.1% / 51% | 24.1% / 63% |
| 2H melee | 4.8% / 10% | 24.1% / 42% | 24.1% / 51% | 24.1% / 63% |
| Missile | 33.4% / 53% | 24.1% / 42% | 24.1% / 51% | 24.1% / 63% |
| War | 29.1% / 48% | 24.1% / 42% | 24.1% / 51% | 24.1% / 63% |
| Void | 4.2% / 9% | 3.4% / 7% | 3.4% / 9% | 3.4% / 13% |

Weapons per corpse are unchanged (2.17 / 2.83 / 4.00) - only the split moves. Sim: `five_sim.py` (200k corpses
per rank) in the session scratchpad; `group_sim.py` / `weapon_sim.py` there hold the earlier option comparisons.

## Current code (master fe5868e5d)

- `Creature_Death.cs:1202-1225` - budget mode reads `loot_drops_min/max` and `loot_weight_{weapon,armor,jewelry,cloak,clothing}`
  from the resolved zone profile (`zoneLoot.Get(stat, default)`, rank chain included) and calls
  `LootGenerationFactory.RollBudgetedCounts`. Line 1192 is the legacy-overflow fallback call (defaults).
- `LootGenerationFactory_ZoneSet.cs`
  - `ZoneLootSetCounts.WeaponFamilyPicks : List<int>` (line 65) - indices into the private `ZoneSetFamily` enum
    (7 melee families + Missile + Caster).
  - `RollBudgetedCounts` (812) - weapon case (892-927): lane first, equal thirds; melee families without
    replacement (`freeMelee`), missile / caster repeat.
  - `CreateZoneLootSet` (973) - budget mode creates one weapon per pick; legacy mode one per family x `Weapons`.
  - `CreateZoneSetWeapon` (1461) / `RollZoneSetMeleeWeapon` (1507) - melee skill is rolled HERE:
    `Next(1, canTwoHand ? 4 : 3)`, so 2H is the 4th option in 4 of 7 families.
  - `RollZoneSetWeaponFamily` (697) - `/zcrare` dev mint (`WeaponScalingCommands.cs:1395-1398`).
  - `ZoneSetCountsLike` (718) - Ascendant rare re-roll (`ZoneRareItems.cs:512`). Existing quirk: a 2H weapon maps
    to Axe/Mace/Sword/Spear family and then re-rolls the skill, so an Ascendant 2H can come back ONE-handed;
    a Void caster can come back War.
- `CasterWcids.cs` - T10 row (203-229) is what every T11+ tier rolls (tier clamped to 10). Nether entries:
  `ace43381_nethersceptre`, `ace43382_netherbaton`, `ace43383_netherstaff`. T1-T2 rows have NO nether entry.
- `ZoneScalingModels.cs` - `ZoneStat` constants (565-574) and `ZoneStat.All` (616+). `All` is the registry:
  `/zonecontrol set|clearstat` reject names not in it (`ZoneControlCommands.cs:828, 2991, 5221`), the plugin sync
  emits every entry by NAME (`ZoneControlCommands.cs:3667`), rifts validate against it (`RiftManager.cs:720`).
  Append-only (comments at 660-672).

## Changes

### 1. Stats (`ZoneScalingModels.cs`)
Add five constants next to `LootWeightClothing`, and APPEND them at the end of `ZoneStat.All`:

```
loot_weight_melee_1h, loot_weight_melee_2h, loot_weight_missile, loot_weight_war, loot_weight_void
```

Comment: weights WITHIN the weapon category, normalized at roll time, 0 = never, unset = code default.
No `_t25` anchors (not needed; can be added later the same way as other knobs).

### 2. Weight carrier (`LootGenerationFactory_ZoneSet.cs`)
```csharp
public readonly record struct WeaponGroupWeights(double Melee1H, double Melee2H, double Missile, double War, double Void)
{
    public static readonly WeaponGroupWeights Default = new(1.0, 1.0, 1.0, 1.0, 1.0 / 7.0);
}
```
Constants `ZoneLootWeaponWeightDefault = 1.0` and `ZoneLootVoidWeightDefault = 1.0 / 7.0` with the reasoning in
the doc comment (Void = one War element; War needs 7 casters, Void needs 1).
Sanitize INSIDE the record (constructor / a `Sanitized()` used by `RollBudgetedCounts`), not at the read site, so the
legacy-overflow call and the tests get it too: NaN, negative or infinite -> 0. (Review: `+Infinity` passes the
existing `> 0` checks in `Sum` / `PickSlot`, makes the total infinite and every pick fall through to the last entry.)

### 3. Pick carrier
Change `WeaponFamilyPicks : List<int>` to `WeaponPicks : List<ZoneSetWeaponPick>`:
```csharp
internal readonly record struct ZoneSetWeaponPick(int Family, ZoneSetWeaponVariant Variant);
internal enum ZoneSetWeaponVariant { Any, OneHanded, TwoHanded, War, Void }
```
`Any` keeps today's behaviour (legacy mode, `/zcrare weapon`). Keep the field name change contained:
`Any`, `CreateZoneLootSet`, `ZoneSetCountsLike`, `RollZoneSetWeaponFamily`, `WeaponScalingCommands.cs:1395-1398`.
Other `ZoneLootSetCounts` constructors: `Creature_Death.cs:1270` (slot-special spawn) and
`TestCharacterCommands.Preset.cs:225` (shield mint). Both leave the picks null and `Weapons = 0`, so they take the
legacy path and make no weapons - correct as is, leave them alone. (`ZoneStatResolver.cs` and `ZoneCraftGateStore.cs`
only mention `CreateZoneLootSet` in comments.)
Do NOT add entries to `ZoneSetFamily`: legacy mode iterates every family (line 995), so the variant record is the
safe carrier.

### 4. Draw (`RollBudgetedCounts`)
- New parameter `WeaponGroupWeights groups` (last, so the call sites are explicit). Legacy-overflow call at
  `Creature_Death.cs:1192` passes `WeaponGroupWeights.Default`.
- Weapon category is offered only when `wWeapon > 0 && w.Weapons > 0 && groups total > 0` (all five at 0 =
  weapons off, budget goes to the other categories, never a lost item).
- Weapon pick: weighted group draw, then
  - `melee_1h`: family from a no-repeat list of all 7 melee families (refill when empty, so a 15-item boss
    never loses the group), variant `OneHanded`.
  - `melee_2h`: family from a no-repeat list of Axe / Mace / Spear / Sword (refill when empty), variant `TwoHanded`.
  - `missile`: Missile family, `Any`.
  - `war` / `void`: Caster family, `War` / `Void`.
- Rewrite the two big comments at the weapon case AND the method summary (798-804, including its "three melee
  picks yield three different families, never three swords" line - with refill, a 15-item boss can carry two of one
  1H family): lane-first thirds are replaced by group weights (owner ruling of 2026-08-31 superseded - quote the new
  ruling and date once the owner signs off). Keep the reason weights are applied per PICK (structural, not a fitted
  number) - it still holds.

### 5. Creation (`CreateZoneSetWeapon`, `RollZoneSetMeleeWeapon`, `CasterWcids`)
- `RollZoneSetMeleeWeapon(family, variant, roll)`:
  - `OneHanded`: skill = `Next(1, 3)` (Heavy/Light/Finesse) for every family.
  - `TwoHanded`: go straight to the 2H branch (family must be Axe/Mace/Spear/Sword; anything else falls back to
    `OneHanded` with a warn).
  - `Any`: today's roll exactly (legacy mode, `/zcrare weapon|melee`).
- `CasterWcids`: add `RollFiltered(int tier, bool nether)` that rolls the tier row restricted to nether / non-nether
  entries. Do NOT build a filtered `ChanceTable` and call `Roll()`: `ChanceTable.Roll` runs `VerifyTable`
  (`ChanceTable.cs:17-33`), which logs an error unless the table sums to 1.0, and a short table falls through to its
  last entry. Instead walk the tier row's `(result, chance)` tuples, sum the matching chances, draw
  `ThreadSafeRandom.Next(0, total)` and walk manually (same shape as `PickSlot`, `LootGenerationFactory_ZoneSet.cs:845`).
  Nether set = the three wcids above, held in one static `HashSet` next to the tables (verified: no other caster
  wcid in any row is nether). If the filtered row is empty (T1-T2 have no nether), return `undef`.
- `CreateZoneSetWeapon`: caster `War` / `Void` call `RollFiltered`; `Any` calls `Roll` as today.
  On `undef` for `Void`, fall back to `War` (log once per tier, not per kill) so a below-T3 zone that turns the
  set on never silently drops nothing.

### 6. Read the stats (`Creature_Death.cs:1216`)
Build `WeaponGroupWeights` from `zoneLoot.Get(ZoneStat.LootWeightMelee1H, default)` etc. and pass it in.
The rank chain (leader / boss rows) and per-zone overrides come for free through `zoneLoot`.

### 7. Ascendant and /zcrare
- `ZoneSetCountsLike`: emit the matching variant - `TwoHanded` for `WeaponType.TwoHanded`, `OneHanded` for other
  melee, `War` / `Void` by `W_DamageType.HasFlag(DamageType.Nether)` for casters (matches `ZoneLootMutator.cs:333`;
  `ZoneSlashRendDamageType` rewrites `W_DamageType` on melee only, so casters keep theirs). Put the mapping in a pure
  helper taking `WeaponType` / `DamageType` so it can be unit-tested. Fixes the existing quirk (an Ascendant 2H or Void
  piece keeps its kind). Mention it in the PR as a behaviour change.
- `/zcrare`: keep `weapon|melee|missile|caster` working (`Any`), optionally add `1h|2h|war|void` keywords. Any new
  `Msg(...)` / chat output stays 7-bit ASCII (CLAUDE.md hard rule).

### 7b. Disabling a group: 0, not the toggle
`/zonecontrol ... togglestat` (`ZoneControlCommands.cs:3434`) turns a stat OFF, and an OFF stat resolves to the code
default - so toggling `loot_weight_void` off gives 1/7, not 0. A group is disabled with `set loot_weight_<group> 0`.
Say so in the stat comments and the `/zonecontrol` help line.

### 8. Not changed
- Legacy per-slot mode (unused by every live zone) keeps one weapon per family with `Any`.
- `GetZoneLootDisplayOrder` (owner decision 2026-07-20).
- Missile sub-split, the melee wcid tables, the caster table weights, the T10 clamp.
- No DB / store migration: unset stats use the code defaults, which ARE the new target.

## Tests (`Source/ACE.Server.Tests/ZoneWeaponGroupTests.cs`, MSTest, `InternalsVisibleTo` exists)
1. Defaults, 200k picks via `RollBudgetedCounts` with only weapons weighted: each of 1H/2H/Missile/War within
   +-0.5 pt of 24.1% (7/29), Void within +-0.3 pt of 3.4% (1/29). Repeat at a non-default set (e.g. 2H = 5) so the
   normalization path is covered, not just the defaults.
2. A group at 0 never appears; all five at 0 -> zero weapon picks and the budget still fills other categories.
3. NaN / negative weights are treated as 0.
4. 1H no-repeat: within one corpse no family repeats until all 7 are used; a 15-weapon roll still yields 1H picks.
5. Variant routing is pure (no DB): unit-test the variant -> skill mapping in `RollZoneSetMeleeWeapon`'s skill
   choice by extracting it into a small static helper.
6. `CasterWcids.RollFiltered(10, true)` only returns the three nether wcids; `(10, false)` never does;
   `(1, true)` returns `undef`.
7. Registry: the five names are in `ZoneStat.All` (pattern from `CharmTierCapTests`) and are its LAST five entries,
   after `CharmLevelCap` (`ZoneScalingModels.cs:766`) - the plugin mirrors the tail by name.
8. Source guard like `ZoneKillExtrasTests`: `Creature_Death.cs` passes the weights into `RollBudgetedCounts`.
9. `ZoneSetCountsLike` mapping helper: 2H -> `TwoHanded`, other melee -> `OneHanded`, nether caster -> `Void`,
   other caster -> `War`.
10. Legacy guard: `zoneSetFamilies` still has exactly 9 entries, so legacy mode with `Weapons = 1` still makes one
    weapon per family.

## Build and verify
- Stop the local server first; build `Release|x64` (`bin/x64/Release/net10.0`). Never verify with `-t:Compile`
  (it strips embedded resources).
- `dotnet test Source/ACE.Server.Tests`.
- In game (local, T11 zone): `/zonecontrol` show the new stats in the default T11 row; kill ~50 mobs and count;
  then `set loot_weight_void 0` on the T11 default and confirm no nether casters; `set loot_weight_melee_2h 5`
  and confirm 2H dominates; clear both.
- Ascendant: `/zcrare` mint a 2H and a Void piece, force an Ascendant re-roll, confirm the kind is kept.

## Rollout
- Owner sign-off needed: this reverses the 2026-08-31 "equal thirds" ruling.
- Release notes for players: Two-handed drops ~5x more; Missile about 28% less, War about 17% less, one-handed
  melee about 15% less; Void stays at one War element's worth.

## Review
Fable reviewed this plan against the code on 2026-10-30. All line refs and the nether wcid list were verified;
its findings (filtered caster roll, HasFlag, real call sites, sanitize in the sampler, toggle vs 0, comment at
798-804, ASCII, test gaps, release-note numbers) are folded in above.
- Live tuning afterwards is pure Zone Control (`/zonecontrol ... set loot_weight_<group> <w>`), per tier default,
  per zone or per rank.

## Open questions
1. Owner sign-off on the five defaults (1 / 1 / 1 / 1 / 1/7).
2. The Zone Control plugin (Decal GUI) source was not found on this machine. Stats sync by name, so the new
   keys are sent but the GUI will not show them until the plugin adds fields; chat commands work regardless.
3. Should the 1H bucket be split evenly by SKILL (as planned) or by FAMILY? Same result today, since every
   1H family exists in all three skill tables.
