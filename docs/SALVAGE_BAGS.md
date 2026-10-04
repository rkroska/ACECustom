# Salvage Bags

Consumables a player uses on a tier 11+ Zone Control item to change its properties. Created 2026-10-02 as "Gear Essences";
renamed to Salvage Bags 2026-10-04 (owner). The code keeps the original names (`GearEssences`, `EssenceKind`, the
`GearEssence*` properties); everything a player or admin sees says "bag".
Code: `Source/ACE.Server/Entity/GearEssences.cs`. Weenies: `Database/Updates/World/2026-10-02-00-Salvage-Bags.sql`.

| WCID | Name | What it does |
|------|------|--------------|
| 78780310 | Bag of Forgetfulness | Removes one random property (never the last one) |
| 78780311 | Bag of Memory | Adds one random property |
| 78780312 | Bag of Second Thoughts | Rerolls one random property within its range (can go up or down) |
| 78780313 | Bag of Tempering | Rerolls a weapon's Weapon Grade at normal drop odds (can go up or down) |
| 78780314 | Bag of Fortune | Rerolls one random property; if the new roll is no better the property stays as it was (the bag is still used up), and the value never goes down |
| 78780315 | Bag of Rebirth | Rerolls every property (a locked one stays) |
| 78780316 | Bag of Locking | Locks one random property; other bags skip it. One lock per item; another Bag of Locking moves it |
| 78780317 | Bag of Vengeance | Sets a weapon's Slayer to the monster type that dropped the bag (named for it, e.g. "Bag of Vengeance (Olthoi)"). An existing Slayer keeps its strength; a weapon without one gains one if under its cap |
| 78780318 | Bag of Madness | **Redesign pending.** Today: 50% every property rerolled into the top third plus one more added (if the tier has room); 50% every property removed (a locked one, and Biting Strike / Crushing Blow under a Bandit Hilt, stay). Either way the item becomes Tainted: no bag works on it again |
| 78780319 | Bag of Transmutation | Changes a weapon's damage type to a random NEW one of the eight (Nether, Fire, Frost, Lightning, Acid, Slashing, Piercing, Bludgeoning); its rending, Cast on Strike spells, tint and a leading type word in its name follow so they keep matching |
| 78780323 | Bag of Emptiness | Removes every property a bag can change (a locked one stays) |
| 78780324 | Bag of Exchange | Removes one random property and adds a different one |

78780320-78780322 and 78780325-78780329 are free (the retired single-type bags were never live).

**Look:** the retail Salvage Bag icon (0x0600102C) and ground model (0x02000181), a retail material picture as the overlay
(each bag its own: Smoky Quartz, Moonstone, Opal, Steel, Gold, Fire Opal, Iron, Bloodstone, Black Opal, Pyreal, White Quartz,
Silver), and a green S badge as the underlay (0x06001DAE), which shows in the bag's see-through top-left corner, so a bag
never reads as real salvage. The client receives exactly three icon layers (icon, overlay, underlay); a second overlay
(PropertyDataId 51) is never sent, so it cannot be used. No client DAT changes.

## What counts as a property

- **Armor, shields, jewelry, clothing, cloaks:** the Zone Control modifier lines (the "Modifiers:" block).
- **Weapons:** the graded cards: Biting Strike, Crushing Blow, Rending (with its Rend Power), Armor Rending,
  Shield Cleaving and Slayer. Cast on Strike, Cleave and Split Arrow are not touched (Transmutation
  does re-pick the Cast on Strike spells to the new type).

## Rules

- **Protected, never touched:** Damage Resist, Crit Damage Resist, Crit Resist, Nether Resist (the Always Rolled
  lines 50-53 and the legacy core four), Reinforced (49), the slot specials, and the line locked by a Bag of Locking.
- **Eligible items:** tier 11+ Zone drops, with or without rolled lines or cards (owner 2026-10-04: a T11 weapon that
  dropped with no cards takes bags, so a Bag of Memory can give it its first). Bag of Tempering works on any weapon with
  a Weapon Grade. Vengeance and Transmutation work on weapons only.
- **The item must be in the pack,** not worn or wielded, and no bag works while the player has a trade open.
- **Tainted items** (after Madness) refuse every bag. The appraisal shows "Tainted" (also on a piece Madness emptied). A
  locked armour line shows "(Locked)"; a locked weapon card shows "- Locked: <card>" in Property Details.
- **Forgetfulness** never takes an item's last property (a locked line counts as one); Emptiness and a Madness wipe do
  clear everything they can, on purpose. On a weapon with a Bandit Hilt, nothing removes Biting Strike or Crushing Blow
  (the hilt adds onto them), and Madness refuses a piece where nothing could be removed.
- **Memory** (and Exchange's add) picks with each line's own loot chance (`modifier_chance_<key>`, `weapon_<card>_chance`)
  for the item's tier Default, so rare lines stay rare. It respects `armor_modifier_cap` / `weapon_modifier_cap`, the
  slot rules and the weapon-card on/off switches. It never adds Slayer (that is Vengeance's job), never adds a
  Rending or Armor Rending next to one the weapon already has, and never adds either to a weapon that carries a player's
  own imbue - Critical Strike, Crippling Blow, or an Armor Rending / rend imbue with no card behind it - the drop path
  never makes that combination.
- **The limit:** the appraisal shows "Properties: X of Y" (armour: top of Modifiers; weapons: under Weapon Grade). Y is the
  tier's `armor_modifier_cap` / `weapon_modifier_cap`; X counts what a drop's limit counts - every Modifiers line that is
  not marked "(Built-in)", Reinforced included, and on weapons every card: the graded ones plus Cast on Strike, Cleave and
  Split Arrow, which bags count but never change (a dropped Cleave card always lands above the weapon's own Cleave, so it
  is always visible to the count). Memory and Vengeance refuse at the limit; Exchange never adds to the
  count, so it is never refused for it.
- **Rerolls** use the tier's normal grade odds (the tier Default's - a rank's `loot_grade_floor` does not apply, so a boss
  piece rerolls like any other; owner: "crafting is gambling"); the value lands inside the live band and keeps its place
  in the list.
- **Ladder no-nerf policy is respected:** after a bag the item's OTHER lines re-resolve exactly as an equip would;
  only the lines the bag changed are set to their new values.
- **Transmutation** (owner 2026-10-04) works on every weapon - melee, thrown, bows and casters - and picks at random from
  the eight types the weapon does NOT already deal, so every use is a real change (a Slash/Pierce sword picks from the other
  six; a weapon with no type of its own, like a plain bow, picks from all eight). It refuses only a weapon whose Rending is
  locked and a weapon carrying a rend outside its first imbue slot (only that slot is rewritten). It changes the damage
  type, rending (and its icon underlay), Cast on Strike spells (on cards stamped since 2026-08-27), tint, and a type word
  at the start of the name, in the weapon kind's own words (retail names):

  | Kind | Fire | Cold | Acid | Lightning | Nether | Slash / Pierce / Bludgeon |
  |---|---|---|---|---|---|---|
  | Melee, thrown | Flaming | Frost | Acid | Lightning | Corrupted | (no word) |
  | Bows | Fire | Frost | Acid | Electric | Corrupted | Slashing / Piercing / Blunt |
  | Casters | Fire | Frost | Acid | Electric | Nether | Slashing / Piercing / Blunt |

  "Frost Ono" -> "Flaming Ono" or "Ono"; "Piercing Bow" -> "Blunt Bow". A name with no type word, the model and the
  colours stay as they were. A caster's T16+ charm gate follows the new element (Nether Veil for Nether, Battlemage's
  Wrath otherwise).
- **Vengeance** on a weapon that is already a Slayer (card or base weapon) changes only its type and keeps its strength.
  On a weapon with no Slayer it adds a Slayer card - only while the tier's Slayer card is switched on with a chance above
  0, and only under the limit.
- A **confirmation popup** comes first. Any refusal leaves the bag in the pack. After use the player gets one chat
  line saying exactly what changed, and the server logs `[SALVAGE BAG] ...`. If applying ever fails after the bag was used
  up, the player gets a replacement bag if the item was left unchanged, and the server logs the item's record for review.

## Drops (zone kills only)

One roll per kill, the same gate as the slot special (Zone Control on, a zone covers the monster, tier 11+).

| Stat | Meaning | Unset |
|------|---------|-------|
| `bag_odds` | 1 in N per kill (per rank, like `special_odds`) | bags never drop |
| `bag_weight_forgetfulness` / `_memory` / `_second_thoughts` / `_tempering` | weight of each core bag on a hit | 1 each |
| `bag_weight_fortune` / `_rebirth` / `_locking` / `_vengeance` / `_madness` / `_transmutation` / `_emptiness` / `_exchange` | weight of each extra | 0 (never drops) |

Vengeance only drops from monsters with a creature type, and remembers it. To stop a core bag dropping, set its
weight to 0; clearing the stat brings back the default of 1. Examples:

```
/zonecontrol default 11 set bag_odds 500
/zonecontrol default 11 set bag_weight_fortune 0.25
/zonecontrol default 11 set bag_weight_madness 0.05
/zonecontrol set <zone> bag_odds 100 --rank boss
```

Each drop logs `[ZONELOOT] SALVAGE BAG: <killer> killed <mob> (<wcid>) -> <bag>, odds 1 in N`.

## Testing

`/create 78780310` through `78780319`, `78780323` and `78780324` give the bags. A created Bag of Vengeance has no monster
type; give it one with `/setproperty PropertyInt.GearEssenceHuntCreatureType <CreatureType number>` on the appraised bag,
or test it from a real drop. Use a bag on a T11 piece in your pack and check the appraisal before and after.

New properties: `PropertyInt.GearEssenceLockedKey` (51000), `PropertyInt.GearEssenceHuntCreatureType` (51001),
`PropertyBool.GearEssenceTainted` (51000), `PropertyBool.GearEssenceWorked` (51001).
