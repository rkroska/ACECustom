# Admin guide: Pet summon charms (7878 block)

Quick reference for **Summon Essence Refill** and **Universal Summoning Mastery** ability charms, server toggles, and deployment.

For WCID numbering rules see [WCID_ALLOCATION_7878.md](WCID_ALLOCATION_7878.md).

---

## What each charm does (player-facing)

### Summon Essence Refill Charm — WCID `78780030`

- **Ability ID:** `24` (`CharmGrantsAbility` on weenie)
- **Player flag:** `PetDevicePyrealAutoRefillEnrolled` (PropertyBool `9049`)
- **Behavior:** While the charm is **activated** (in inventory, toggled on), using a summoning essence with **0 charges** can spend **pyreals** to restore **1 charge** immediately before the summon proceeds.
- **Does not replace** Encapsulated Spirits (full refill still works as before).

### Universal Summoning Mastery Charm — WCID `78780031`

- **Ability ID:** `25`
- **Player flag:** `HasUniversalSummoningMastery` (PropertyBool `50038`)
- **Behavior:** While activated, the player may use **Primalist**, **Necromancer**, or **Naturalist** pet devices even if their character’s chosen `SummoningMastery` does not match.
- **Does not:** Grant Summoning skill, change the mastery shown on the character panel, or bypass luminance-aug / skill requirements on essences.
- **Activate guard:** If `pet_charm_universal_summoning_mastery_enabled` is false, the charm cannot be turned on (“not enabled on this server”).

---

## Property IDs (quick reference)

| What | Kind | ID | Values / notes |
|------|------|-----|----------------|
| Character **chosen** summoning mastery | `PropertyInt` | **362** | `0` Undef, `1` Primalist, `2` Necromancer, `3` Naturalist |
| Essence required mastery | `PropertyInt` | **362** | Same enum on the pet device weenie |
| Universal charm (active) | `PropertyBool` | **50038** | Set while charm 25 is activated; **not** a replacement for 362 |
| Pyreal refill enrollment | `PropertyBool` | **9049** | Set while charm 24 is activated |

Persisted on the player in `ace_shard.biota_properties_int` / `biota_properties_bool` like any other weenie property.

---

## How summoning mastery is stored (characters)

Summoning mastery is **not** a skill and **not** set at character creation in code (unlike melee `354` / ranged `355` heritage masteries).

- **Storage:** `PropertyInt.SummoningMastery` (**362**) on the player biota.
- **Set in play:** World NPC emotes (`SetIntStat` with stat `362`, amount `1`/`2`/`3`) when the player picks Primalist / Necromancer / Naturalist.
- **Client / plugins:** Decal and VTank read the same int as `getcharintprop[362]`.
- **Free reset:** Quest `UsedFreeSummoningMasteryReset`; renewal clears it via `FreeMasteryResetRenewed` (bool `9010`).

The universal charm adds bool **50038** only. It does **not** write to int **362**, so the character panel and client-side mastery checks stay on the real chosen mastery.

### Server check (`PetDevice.CheckUseRequirements`)

When an essence has mastery **362** set and not `Undef`, the server compares **essence** mastery to **player** mastery. Bypass if **all** of:

1. `pet_charm_universal_summoning_mastery_enabled` is true, and  
2. Player has `HasUniversalSummoningMastery` (charm 25 active).

Implementation: `Source/ACE.Server/WorldObjects/PetDevice.cs` (use `global::ACE.Entity.Enum.SummoningMastery.Undef` in code — the `SummoningMastery` property name shadows the enum type).

### VTank / meta bots

**Server-only bypass.** VTank met files that gate `SummonPet` on `getcharintprop[362]==1|2|3` still follow the character’s real mastery; the charm does not change **362**. Manual use in-game works with the charm; bot met files are unchanged unless edited separately.

---

## ServerConfig (runtime `/modifybool` / `/modifylong`)

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `pet_device_pyreal_auto_refill_enabled` | bool | **false** | Master switch for pyreal-per-charge refill |
| `pet_device_pyreal_auto_refill_cost_per_charge` | long | **1** | Pyreals spent per restored charge (`0` = free when enrolled + enabled) |
| `pet_charm_universal_summoning_mastery_enabled` | bool | **true** | Master switch for universal mastery bypass |

### Example commands (in-game, dev/admin)

```
/modifybool pet_device_pyreal_auto_refill_enabled true
/modifylong pet_device_pyreal_auto_refill_cost_per_charge 5000

/modifybool pet_charm_universal_summoning_mastery_enabled true
/modifybool pet_charm_universal_summoning_mastery_enabled false
```

Changes apply immediately; no restart required.

---

## Deploy checklist

**World weenie SQL** lives only under `Database/Updates/World/` (not `Content/`). With `AutoApplyDatabaseUpdates` and `AutoApplyWorldCustomizations` off, run these **manually** against the world DB, in order:

1. `Database/Updates/World/2026-05-24-00-Summon-Essence-Refill-Charm.sql`
2. `Database/Updates/World/2026-05-24-01-Universal-Summoning-Mastery-Charm.sql`

Example:

```bash
mysql -h HOST -P PORT -u USER -p ace_world < Database/Updates/World/2026-05-24-00-Summon-Essence-Refill-Charm.sql
mysql -h HOST -P PORT -u USER -p ace_world < Database/Updates/World/2026-05-24-01-Universal-Summoning-Mastery-Charm.sql
```

Then:

3. **Rebuild & restart** ACE server (C# changes in `CharmAbilityRegistry`, `PetDevice`, `Gem`, `PropertyManager`).
4. Enable features as desired via ServerConfig (see above).
5. Distribute charms: `@add 78780030` / `@add 78780031` (or vendor/loot).

---

## Giving items & testing

| Item | Command |
|------|---------|
| Summon Essence Refill Charm | `@add 78780030` |
| Universal Summoning Mastery Charm | `@add 78780031` |

**Player use:** Double-click charm → “activated” (stays in pack; not consumed). Only **one** charm per ability ID can be active at a time (same rule as Mana Barrier, Penta Cast, etc.).

**Pyreal refill test:**

1. `/modifybool pet_device_pyreal_auto_refill_enabled true`
2. Set cost if needed: `/modifylong pet_device_pyreal_auto_refill_cost_per_charge 100`
3. Player activates `78780030`, drains an essence to 0 charges, uses essence → should spend pyreals and summon.

**Universal mastery test:**

1. `/modifybool pet_charm_universal_summoning_mastery_enabled true` (default on new builds)
2. Player is e.g. **Naturalist** only; activate `78780031`
3. Use a **Necromancer** or **Primalist** essence → should work instead of “You must be a …”

---

## Charm block IDs (for content authors)

| WCID | Name | `CharmGrantsAbility` |
|------|------|----------------------|
| `78780030` | Summon Essence Refill Charm | `24` |
| `78780031` | Universal Summoning Mastery Charm | `25` |
| `78780032`–`78780089` | *Next charms* | assign `26+` in code + registry |

Find next free WCID in charm block:

```bash
# Run on world DB
Database/tools/find_next_wcid_7878.sql
```

New charms always need:

1. World weenie SQL (`78780032+`, type 38, `IsAbilityCharm`, `CharmGrantsAbility`, `IsCharm`)
2. C# row in `CharmAbilityRegistry.cs` (ability id + player bool getter/setter)
3. Optional `PropertyBool` on player if not reusing an existing flag
4. WCID map entry in `CharmAbilityRegistry.WCIDToAbilityId`

---

## Troubleshooting

| Symptom | Likely cause |
|---------|----------------|
| Charm toggles but nothing happens | ServerConfig off for that feature |
| “…not enabled on this server” on activate | `pet_charm_universal_summoning_mastery_enabled` is false |
| Pyreal refill never triggers | `pet_device_pyreal_auto_refill_enabled` false, charm not activated, or no pyreals |
| Still “You must be a Necromancer” | Universal mastery off, charm not active, or server not rebuilt |
| VTank won’t summon wrong-mastery essence | Expected: charm is server-side; met still uses `getcharintprop[362]` |
| “Not enough charges” with refill charm on | Refill master switch off or insufficient pyreals |
| Two of same charm type | Only one activated charm per ability ID allowed |

---

## Code touchpoints (for devs)

| Area | File |
|------|------|
| Charm registry & WCIDs | `Source/ACE.Server/WorldObjects/CharmAbilityRegistry.cs` |
| Toggle on use | `Source/ACE.Server/WorldObjects/Gem.cs` |
| Pyreal refill on summon | `Source/ACE.Server/WorldObjects/PetDevice.cs` → `TryApplyPyrealAutoRefillBeforeSummon` |
| Mastery check | `Source/ACE.Server/WorldObjects/PetDevice.cs` → `CheckUseRequirements` |
| Server properties | `Source/ACE.Server/Managers/PropertyManager.cs` |
| Player enrollment flag (refill) | `PropertyBool.PetDevicePyrealAutoRefillEnrolled` = `9049` |
| Player universal mastery | `PropertyBool.HasUniversalSummoningMastery` = `50038` |

---

## Related docs

- [WCID_ALLOCATION_7878.md](WCID_ALLOCATION_7878.md) — full 7878 range map
- [BONDED_COMBAT_PETS.md](BONDED_COMBAT_PETS.md) — bond, attunement, banked pyreal refill design notes (separate from per-charge charm refill)
