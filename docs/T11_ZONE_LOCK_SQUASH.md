# T11 Zone Lock Squash - Current State

Status as of 2026-10-10, branch `feature/t11-squash-max-t10` (cut from master at `482d44d26`).
Sections "How the squash works today" through "Cause" describe master before this branch.
"Changes on this branch" lists what has been changed since (uncommitted, compiled, not tested in game).

## Background

When T11 launched, players earned T11 gear inside the T11 zones, then wore it in T10 content and
overpowered it. PR #544 (`911a3597b`, merged 2026-10-07) added the "zone lock" fallback: T11 gear
worn outside an enabled T11 zone is squashed toward T10 strength. Before #544 the lock simply
removed the T11 pieces' ratings (zero).

Player feedback since: a locked T11 suit is worse in T10 content than the player's own T10 suit.
The intent is that locked T11 gear should behave like **max T10 gear**.

## How the squash works today

"Locked" means `ZoneControlManager.WornPowerSuppressed(player)` is true: the player is outside
every enabled zone with `zc_armor_zone_lock` on, or is standing on a gear-lock spot. The T10
fallback additionally needs `zonecontrol_enabled` on (`ZoneLockFallback.Active`); with it off the
old subtract-only behaviour applies (zero).

### Ratings (the nine Gear* ratings)

- `Creature_Equipment.GetEquippedItemsRatingSum` (line ~575) subtracts everything the worn T11
  pieces contributed and adds `ZoneLockFallback.WornRating(rating, pieces)`.
- `WornRating` = table value x min(1, worn T11 pieces / 18), rounded.
- Every T11 piece is worth the same 1/18 share, whatever it rolled. A full kit is 9 armor, shirt,
  pants, cloak, necklace, 2 rings, 2 bracelets, trinket. Weapons, casters, shields and ammo do not
  count as pieces. A multi-slot piece counts once.
- Non-T11 gear (T10 pieces, retail cloaks, aetheria) keeps counting on its own.
- The table (`ZoneLockFallback.WornTotals`, line 45) is the average of two characters chosen as
  "normal" T10 players (Grumpy Old Man, Good Grief). Drexel and Nerd Parade were deliberately
  excluded as "not normal" per the code comment.

| Rating | Table today | Per piece (1/18) |
|---|---|---|
| Damage | 116 | 6.4 |
| Damage Resist | 36 | 2.0 |
| Crit | 10 | 0.6 |
| Crit Resist | 2 | 0.1 |
| Crit Damage | 116 | 6.4 |
| Crit Damage Resist | 21 | 1.2 |
| Healing Boost | 123 | 6.8 |
| Nether Resist | 96 | 5.3 |
| Max Health | 715 | 39.7 |

### What goes to zero while locked

- The whole 50200 property block (`GetZoneModifierBonus` / `GetZoneModifierMax`): All Attributes,
  skill bonuses, Spell Duration, Max Health Pct, Life on Hit, and the slot specials (Fortify
  Vitals, Battle Mending, Pct HP Damage, Cheat Death, Regeneration).
- Jewelry Cast on Strike.
- PK damage / PK damage resist gear ratings (not in the table).
- A T11 shield's own ratings (it is T11 gear, but not one of the 18 pieces, so it earns no share).

T10 gear has no equivalent of the 50200 lines, so zero is consistent with "acts like T10".

### What is NOT squashed

- **Armor Level.** `Creature_BodyPart.GetArmorMod` (line 101) reads the stamped value directly.
  Locked T11 armor keeps 1100-1350 AL per piece. Worn T10 armor: median 600, 99th percentile 742,
  max 788. `ZoneFallback.ArmorLevel` already holds a max-T10 figure of 732 but the lock does not
  use it.
- Reinforced protection ranks (frozen into the piece at drop).

### Item panel

`AppraiseInfo` (line ~1064) prints every locked line on a T11 piece at 0 (`line.TextAt(0)`), with
a "Zone Locked (Power Reduced)" header. Armor Level keeps its number. So a player inspecting
locked gear sees +0 on every rating even though combat uses the table above.

### Weapons (separate system, not part of this effort)

Weapons use `WeaponPowerSuppressed` and the weapon half of `ZoneLockFallback`: best T10 damage of
the weapon type +10 pct, Legendary Blood / Spirit Thirst, T10 best attack / defense percents, and
T10 caps on Biting Strike, Crushing Blow, slayer, cleave, proc rate and Split Arrows. These values
were read from code only; they were not checked against the database.

## Measurements

Source: local shard DB, a copy of prod from 2026-10-07. Gear ratings only (no augs, enchantments
or enlightenment); aetheria excluded.

### T10 worn totals, 77 all-T10 characters (0 T11 pieces, 14+ rated pieces)

| Rating | Squash table | Median | Top quarter | Best single (Drexel) | Highest seen on anyone |
|---|---|---|---|---|---|
| Damage | 116 | 96 | 129 | 211 | 211 |
| Damage Resist | 36 | 48 | 72 | 92 | 118 |
| Crit | 10 | 5 | 11 | 28 | 28 |
| Crit Resist | 2 | 3 | 7 | 19 | 19 |
| Crit Damage | 116 | 110 | 126 | 194 | 196 |
| Crit Damage Resist | 21 | 29 | 45 | 73 | 77 |
| Healing Boost | 123 | 70 | 180 | 355 | 865 (outlier; 95th pct 380) |
| Nether Resist | 96 | 88 | 102 | 142 | 149 |
| Max Health | 715 | 805 | 1045 | 1810 | 1810 |

Drexel's Damage 211, DR 92 and CDR 73 match the max-T10 constants already in `ZoneFallback.cs`.

### T11 pieces (ZcTier 11, about 1,650 worn-type pieces)

Per piece: Damage Resist 35-69 on every piece; Crit Resist, CDR and Nether Resist 21-42 on every
piece; Damage, Crit Damage, Healing Boost and Max Health 14-69 on roughly 25-60 pct of pieces.
Armor AL 1100-1350. A near-full kit in zone (SuperTummy, 17 pieces): Damage 1139, DR 1172,
Crit Resist 713, Crit Damage 1104, CDR 714, Nether 714, armor AL 9900.

### Characters checked

"Locked today" and "proposed" are the whole worn kit (T11 share + their non-T11 pieces).
"T10 in packs" is the best non-T11 piece per slot in the character's own packs - a rough upper
bound that ignores bank and mules.

| Character | T11 pieces | Locked today (Dmg / CritDmg / DR / MaxHP) | T10 in packs | Proposed |
|---|---|---|---|---|
| Double Dreggon | 10 | 132 / 133 / 40 / 947 | 183 / 189 / 22 / 350 | 185 / 177 / 71 / 1556 |
| Reawakener | 13 | 117 / 121 / 46 / 1116 | 138 / 139 / 15 / 375 | 185 / 177 / 86 / 1907 |
| Schneebly | 7 | 134 / 131 / 22 / 628 | 141 / 139 / 12 / 450 | 171 / 161 / 44 / 1054 |
| Moreck | 12 | 126 / 131 / 62 / 1277 | 71 / 92 / 38 / 200 | 190 / 183 / 99 / 2007 |
| Ix | 9 | 78 / 79 / 18 / 938 | 57 / 73 / 24 / 100 | 126 / 118 / 46 / 1485 |
| Moreck II | 11 | 111 / 111 / 22 / 437 | 35 / 31 / 10 / 10 | 169 / 159 / 56 / 1106 |

## Cause

1. The ratings table is the average of two mid-geared characters. Its defensive lines sit below
   the median T10 suit, and every line sits below a top-quarter suit - the group that actually
   wears T11.
2. The share is per T11 piece, and most players wear mixed kits (7-13 pieces), so they get
   39-72 pct of an already low table.
3. A good T10 armor piece carries 9-10 Damage (best 15-16); a locked T11 piece is worth 6.4.
   Damage-stacked players lose rating on every slot they swap.
4. The item panel shows +0, which reads as "ratings removed".

## Changes on this branch (2026-10-10)

1. **The ratings table is now nine live properties** (`PropertyManager.cs`, read by
   `ZoneLockFallback.WornTotal`). Change with `@modifylong`, read with `@showprops`. Defaults are
   max T10 (Drexel's worn totals), so a server with no stored values picks them up on deploy.

   | Property | Default | Was (fixed) |
   |---|---|---|
   | `zc_lock_t10_damage` | 211 | 116 |
   | `zc_lock_t10_damage_resist` | 92 | 36 |
   | `zc_lock_t10_crit` | 28 | 10 |
   | `zc_lock_t10_crit_resist` | 19 | 2 |
   | `zc_lock_t10_crit_damage` | 194 | 116 |
   | `zc_lock_t10_crit_damage_resist` | 73 | 21 |
   | `zc_lock_t10_healing_boost` | 355 | 123 |
   | `zc_lock_t10_nether_resist` | 142 | 96 |
   | `zc_lock_t10_max_health` | 1810 | 715 |

   Each value is what a full 18-piece T11 kit counts as; one worn T11 piece gives 1/18. A value of
   0 or less turns that rating's share off. A live change to Max Health reaches a player at their
   next zone border, re-equip or login (no forced refresh was added).

2. **Item panels are not squashed in the mule spots.** In the Marketplace (0x016C) and the
   apartment landblocks (`LandblockCollections.ConnectionExemptLandblocks`) an appraisal shows a
   T11 item's own values and no "Zone Locked" header - worn gear and weapons. Display only: combat
   and the wearer's real ratings stay locked there. New helpers in `ZoneControlManager`
   (`InLockDisplayExemptSpot`, `WeaponPowerSuppressedForDisplay`, `WornPowerSuppressedForDisplay`),
   used by `AppraiseInfo.cs` and `WeaponProfile.cs` only.

## Still proposed (not applied)

- **Item panel outside the mule spots:** locked rating lines still print +0. Showing the real
  per-piece share is not done.
- **Armor Level clamp:** hold a locked T11 piece to 732 (`ZoneFallback.ArmorLevel`). Not done.
  This would be the only nerf.

## Open questions

- **Zero-rating reports are not reconciled.** Nothing here was tested in game. If players saw zero
  ratings in combat after #544 was live, the table is not applying on prod for a reason not yet
  found, and raising the table would fix nothing until that is. Check: developer character in T11
  gear, outside any T11 zone, armor lock on, run `@zcvitals`. Gear Max Health should read about
  715 x T11 pieces / 18 plus non-T11 pieces.
- **Prod state unknown:** which build is running, and whether `zc_armor_zone_lock` is on (it is
  not stored in the local DB copy).
- **Stackers:** a suit stacked on Crit Damage / Nether Resist (Double Dreggon) can still edge out
  the proposed table on those lines. Raising them to the highest seen on any all-T10 character
  (196 and 149) would close that.
- **Healing Boost 355 and Max Health 1810** are well above the median T10 suit (70 and 805).
  Confirm these are wanted at Drexel's level.
- **Armor Level clamp:** include or not.
- **Not checked:** whether T10 suits carry set bonuses or item spells that T11 pieces lack, and
  whether the weapon fallback numbers match real best-in-slot T10 weapons.
- The existing table reflects a recorded decision to use "normal" players, not the best-geared.
  This proposal reverses that.
