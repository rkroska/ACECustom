# 7878 WCID allocation (ACECustom / ILT)

**Admin / deploy / mastery behavior:** [ADMIN_PET_SUMMON_CHARMS.md](ADMIN_PET_SUMMON_CHARMS.md)

Prefix: `CAST(class_Id AS CHAR) LIKE '7878%'` (8- and 9-digit IDs).

Do **not** use bare `7878` (legacy stub).

## Reserved blocks

| Range | Purpose | Notes |
|-------|---------|--------|
| `78780001`–`78780006` | Siphon / capture core | Lenses tier 1–3, siphoned essence, monster dex, hollow essence |
| `78780007`–`78780009` | **Spare (utility)** | Small gap; OK for one-off gems, avoid for charm series |
| `78780010`–`78780012` | Siphon lenses (extended) | Resonance, Shimmering Echo, Asheron's lens |
| `78780013` | **Savage Echo** | Pet potency currency; internal doc alias: Essence Residue |
| `78780014` | **Essence Resonator** | Salvage tool: converts spare captured essences → Savage Echo |
| `78780015`–`78780019` | **Spare** | Reserved |
| `78780020`–`78780029` | World / NPC / generators | Echo Weaver, Lens Collector, Crystal Gen, etc. |
| **`78780030`–`78780089`** | **Ability charms (ILT)** | **Allocate new toggle charms here only** |
| `78780090`–`78780098` | **Spare buffer** | Emergency items; stay below debug. **Overlaps QuestBuilder** (see below) |
| `78780099` | Debug siphon lens | Keep fixed. **Inside the QuestBuilder auto-allocation range** (see below) |
| `78780090`–`78780199` | **QuestBuilder auto-allocation** | `QuestBuilderCompiler.FindNextWcid` (`Source/ACE.Server/Managers/QuestBuilder/QuestBuilderCompiler.cs` ~739) and `QuestBuilderController.GetNextWcid` (`Source/ACE.Server/Controllers/QuestBuilderController.cs` ~35) hand out the first WCID in this range with no `weenie` row. It skips ids that already exist, so it will not clobber the spare buffer / debug lens / loot items once they are in the DB, but it **overlaps** all three. Documented here so the overlap is visible; nothing is renumbered. `78780092` is also the QuestBuilder template WCID (`QuestBuilderTemplates.cs`) |
| `78780101`–`78780103` | Loot: Flawed / Pristine / Perfect essence drops | Created in code by `LootGenerationFactory` (~261-289). Inside the QuestBuilder range above |
| `78780200`–`78780249` | **Ruggan's Annex** (pet breeding area) | NPCs and props. See `PET_BREEDING_ANNEX_DESIGN.md`; SQL in `Database/Updates/World/2026-09-09-00-Ruggans-Annex-NPCs.sql` (NPCs 200-204, 210-214, 220-224) |
| `78780250`–`78780259` | **Pet Breeding Consumables & Sinks** | Courtship Incense (250-252), Nurturing Draught (253), Chromatic Catalyst (254), Offering of Subjugation (255). `78780256` Ancestral Gene Re-roller is **reserved / unbuilt**: no weenie, no handler, and `Player_Use.cs` only accepts 250-255. SQL in `Database/Updates/World/2026-09-12-00-Pet-Breeding-Sinks.sql` |

## Legacy block (pre-7878 ids still in use)

These predate the 7878 scheme and are **not** being renumbered; the code references them by
number, so leave them where they are.

| WCID | Purpose | Defined in |
|------|---------|------------|
| `98760388` | Portal to Seedy Motel (destination cell `0x013A02AE`, variation 3) | `Database/Updates/World/2026-07-26-00-Seedy-Motel-Portal.sql` |
| `98760399` | Pet Neutering Kit | `Database/Updates/World/2026-09-09-01-Pet-Tailoring-and-Neutering-Kits.sql` (mirrored in `Content/sql/weenies/`); constants in `Source/ACE.Server/Entity/PetTailoring.cs` |
| `98760400` | Pet Tailoring Kit | same |
| `98760401` | Pet Tailoring Kit (Filled) | same |
| `787801001`-`787801072` | Pet device essences (250/300) | Do not use for charms |
| `787802001`-`787802072` | Combat pet summon weenies | Do not use for charms |
| `787802073`+ | Future combat pets / extensions | Append only |

The breeding sinks briefly lived at `98760410`-`98760412`, `98760415` and `98760418` before
moving to `78780250`-`78780254`; the sinks patch deletes those old rows on every run.

## Ability charms (`78780030`–`78780089`)

- **60 WCIDs**, sequential assignment recommended.
- Register each WCID in `CharmAbilityRegistry.WCIDToAbilityId` (C#) with a unique `CharmGrantsAbility` id (int).
- Weenie: `type` 38 (Gem), `IsAbilityCharm`, `CharmGrantsAbility`, `IsCharm` (9040).

| WCID | Assigned |
|------|----------|
| `78780030` | Summon Essence Refill Charm (`CharmGrantsAbility` 24) |
| `78780031` | Universal Summoning Mastery Charm (`CharmGrantsAbility` 25) |
| `78780032`–`78780089` | *Available* |

Tier variants of the same ability (if needed later) can use adjacent IDs (e.g. 31–33) without leaving the block.

## Next free charm WCID

```sql
SELECT MIN(seq.id) AS next_charm_wcid
FROM (
    SELECT 78780030 + n AS id
    FROM (
        SELECT a.N + b.N * 10 + c.N * 100 AS n
        FROM (SELECT 0 N UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4
              UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9) a
        CROSS JOIN (SELECT 0 N UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4
              UNION SELECT 5 UNION SELECT 6) b
        CROSS JOIN (SELECT 0 N UNION SELECT 1) c
    ) nums
    WHERE 78780030 + n <= 78780089
) seq
LEFT JOIN weenie w ON w.class_Id = seq.id
WHERE w.class_Id IS NULL;
```

## Related server config

| Charm / feature | ServerConfig | Default |
|-----------------|--------------|---------|
| Summon Essence Refill (ability 24) | `pet_device_pyreal_auto_refill_enabled`, `pet_device_pyreal_auto_refill_cost_per_charge` | off / 1 pyreal |
| Universal Summoning Mastery (ability 25) | `pet_charm_universal_summoning_mastery_enabled` | on |

Enable at runtime: `/modifybool pet_charm_universal_summoning_mastery_enabled true`

**Note:** Universal mastery charm does not change player `PropertyInt` 362; it sets bool `50038` while active. See admin doc for VTank / client implications.
