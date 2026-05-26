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
| `78780013`–`78780019` | **Spare** | |
| `78780020`–`78780029` | World / NPC / generators | Echo Weaver, Lens Collector, Crystal Gen, etc. |
| **`78780030`–`78780089`** | **Ability charms (ILT)** | **Allocate new toggle charms here only** |
| `78780090`–`78780098` | **Spare buffer** | Emergency items; stay below debug |
| `78780099` | Debug siphon lens | Keep fixed |
| `787801001`–`787801072` | Pet device essences (250/300) | Do not use for charms |
| `787802001`–`787802072` | Combat pet summon weenies | Do not use for charms |
| `787802073`+ | Future combat pets / extensions | Append only |

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
