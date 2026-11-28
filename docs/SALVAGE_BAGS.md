# Salvage Bags

Consumables a player uses on a tier 11+ Zone Control item to change its properties. Created 2026-10-02 as "Gear Essences";
renamed to Salvage Bags 2026-10-04 (owner). The code keeps the original names (`GearEssences`, `EssenceKind`, the
`GearEssence*` properties); everything a player or admin sees says "bag".
Code: `Source/ACE.Server/Entity/GearEssences.cs`. Weenies: `Database/Updates/World/2026-10-02-00-Salvage-Bags.sql`.
The 2026-11-26 follow-up (Second Thoughts, Tempering, Madness) and the four new bags need database updates that are not in
this repository. As with other ILT content, the ILT admins keep the two finished SQL scripts with their own world content
and run them for the release; nobody has to write them, and a server that isn't ILT's needs neither:
- **World script** (`ace_world`, run any time, safe to re-run): adds the four new bag weenies (78780320, 78780321,
  78780322, 78780325) and sets the new Use text (PropertyString 14) on Second Thoughts (78780312), Tempering (78780313) and
  Madness (78780318). Only bags that drop after it read the new text.
- **Shard script** (`ace_shard`, run with the server stopped): sets the same Use text on those three kinds of bags that
  players already hold, since each bag keeps its own copy of the text from when it dropped. It shows a count of affected
  bags first, updates them in one transaction, and checks afterwards that no old text is left. An online player's bags
  are saved from memory, so with the server running they would write the old text back.

Skipping either script changes only the wording: the behaviour and the confirm prompts are code, so they are right either way.

| WCID | Name | What it does |
|------|------|--------------|
| 78780310 | Bag of Forgetfulness | Removes one random property (never the last one) |
| 78780311 | Bag of Memory | Adds one random property |
| 78780312 | Bag of Second Thoughts | Rerolls the item's **lowest** property within its range at the tier's normal odds (can go up or down; 2026-11-26) |
| 78780313 | Bag of Tempering | Moves a weapon's Weapon Grade **one sub-grade**: 60% up, 30% no change, 10% down (2026-11-26). Never above the configured top (`bag_tempering_max_subgrade`, A+ by default), never reaches S; an S weapon or one at the top is refused |
| 78780314 | Bag of Fortune | Rerolls one random property; if the new roll is no better the property stays as it was (the bag is still used up), and the value never goes down |
| 78780315 | Bag of Rebirth | Rerolls every property (a locked one stays) |
| 78780316 | Bag of Locking | Locks one random property; other bags skip it. One lock per item; another Bag of Locking moves it |
| 78780317 | Bag of Vengeance | Sets a weapon's Slayer to the monster type that dropped the bag (named for it, e.g. "Bag of Vengeance (Olthoi)"). An existing Slayer keeps its strength; a weapon without one gains one if under its cap |
| 78780318 | Bag of Madness | 30% nothing; 30% one random property SET past its max (+10-25% of its range); 30% every property SHIFTED -15..+15% of its range from where it is (may pass max); 10% one random property removed. Only the Madness list, never a locked property. The item becomes Tainted every time: no bag works on it again (2026-11-26) |
| 78780319 | Bag of Transmutation | Changes a weapon's damage type to a random NEW one of the eight (Nether, Fire, Frost, Lightning, Acid, Slashing, Piercing, Bludgeoning); its rending, Cast on Strike spells, tint and a leading type word in its name follow so they keep matching |
| 78780323 | Bag of Emptiness | Removes every property a bag can change (a locked one stays) |
| 78780324 | Bag of Exchange | Removes one random property and adds a different one |
| 78780320 | Bag of Genesis | Adds one random property to an item with **no** properties (Path of Exile's transmute) |
| 78780321 | Bag of Augmentation | Adds one random property to an item with **exactly one** property |
| 78780322 | Bag of Alteration | On an item with **one or two** properties: removes the ones a bag can change and adds **one** new random property (a locked property, a Slayer and built-ins such as Reinforced or Split Arrows stay) |
| 78780325 | Bag of Reforging | Replaces **every** property with a new random one, keeping the count (a locked property and a Slayer stay; Path of Exile's chaos) |

78780326-78780329 are free. 320-322 and 325 held retired single-type bags that were never live; the crafting-step bags took
them on 2026-11-26 (their weenies are added by the admins in the world database, cloned from the Bag of Memory, like
other ILT content; not kept in the repo).

**The crafting steps (owner 2026-11-26).** Building an item from nothing used to take a Bag of Emptiness and then a stack of
Memory. The four steps give the cheap early moves: Emptiness, then Genesis (0 -> 1), Alteration until the line you want,
Augmentation (1 -> 2), then Memory for the third and beyond; Reforging reworks a full item in one go.
- The counts are the appraisal's "Properties: N of cap" (`PropertiesCount`): Reinforced, a locked line, and a weapon's
  Split Arrows / Cleave / Cast on Strike count. An emptied piece that keeps Reinforced shows 1, so it takes Augmentation,
  not Genesis.
- Every add goes through Memory's planner: never a property the item has, no second rend and none next to the player's own
  imbue, never a Slayer, the slot rules and the tier cap, each line's own loot chance.
- Alteration and Reforging remove what a bag may remove (`RemovableUnits`) except the Slayer. Reforging then plans the same
  number of adds; Alteration plans exactly one (`ReplacementCount`; an item with two loses the second, owner 2026-11-26).
  The adds are planned one at a time, each seeing the removals and the adds before it (`PlanReplacements`): a removed property may come
  back, and a removed Rending frees the rend slot. On a weapon its Rending is replaced too (the confirm text says so).
  When the item can't take every add the use needs (one for Alteration; for Reforging, as many as it removes, held to
  the tier cap), the bag refuses and nothing is used.
- Review fixes (2026-11-26, after a lab test on DESKTOP2):
  - **No forced Chase line.** When every line a bag could add is a Chase line (Crit Chance, Max Health Pct, Life on
    Hit, Cast on Strike), the bag refuses (`OnlyChaseLeft`). Before, the weighted pick handed one out every time, so
    Reforging a capped piece fished Chase lines. This also fixes Memory and Exchange on the last free slot.
    Alteration and Reforging check BEFORE any random pick that the ordinary lines alone can fill every new slot
    (`CanFillWithoutChase`): a refusal that depended on the picks would be free to retry until a Chase line slipped in.
  - **Never past the cap.** Reforging on an item above its tier's cap brings it down to the cap (`ReplacementCount`).
  - **The confirm text states the result** for this item (how many go, how many it ends with), and warns when a Rending
    or tinkered lines are replaced.
  - **The item must be unchanged since the prompt opened** (`PromptState`, every bag), or the bag refuses. For Alteration
    and Reforging that includes how many properties go and come, so a tier-cap change under an open prompt is caught.
  - **No replace with a rend bit outside the first imbue slot,** as Transmutation already checks.
  - **A retail or crafted proc keeps the proc slot taken** when a Cast on Strike line is replaced.

**Look:** the retail Salvage Bag icon (0x0600102C) and ground model (0x02000181), a retail material picture as the overlay
(each bag its own: Smoky Quartz, Moonstone, Opal, Steel, Gold, Fire Opal, Iron, Bloodstone, Black Opal, Pyreal, White Quartz,
Silver), and a green S badge as the underlay (0x06001DAE), which shows in the bag's see-through top-left corner, so a bag
never reads as real salvage. The client receives exactly three icon layers (icon, overlay, underlay); a second overlay
(PropertyDataId 51) is never sent, so it cannot be used. No client DAT changes.

## What counts as a property

- **Armor, shields, jewelry, clothing, cloaks:** the Zone Control modifier lines (the "Modifiers:" block).
- **Weapons:** the graded cards: Biting Strike, Crushing Blow, Rending (with its Rend Power), Armor Rending,
  Shield Cleaving, Slayer and Spell Armor Rend (casters only: a bag never adds it to a melee or missile weapon).
  Cast on Strike, Cleave and Split Arrow are not touched (Transmutation
  does re-pick the Cast on Strike spells to the new type).

## Rules

- **Protected, never touched:** Damage Resist, Crit Damage Resist, Crit Resist, Nether Resist (the Always Rolled
  lines 50-53 and the legacy core four), Reinforced (49), the slot specials, and the line locked by a Bag of Locking.
- **Eligible items:** tier 11+ Zone drops, with or without rolled lines or cards (owner 2026-10-04: a T11 weapon that
  dropped with no cards takes bags, so a Bag of Memory can give it its first). Bag of Tempering works on any weapon with
  a Weapon Grade. Vengeance and Transmutation work on weapons only.
- **The item must be in the pack,** not worn or wielded, and no bag works while the player has a trade open.
- **Tainted items** (after Madness) refuse every bag, and the blacksmith refuses to forge (as the main or the feeder) or
  hone them (`ForgeWeaponReader.RefusalReason`; dyes still work). The appraisal shows "Tainted". A
  locked armour line shows "(Locked)"; a locked weapon card shows "- Locked: <card>" in Property Details.
- **Forgetfulness** never takes an item's last property (a locked line counts as one); Emptiness does clear everything
  it can, on purpose. On a weapon with a Bandit Hilt, nothing removes Biting Strike or Crushing Blow (the hilt adds onto
  them).
- **Memory** (and Exchange's add) picks with each line's own loot chance (`modifier_chance_<key>`, `weapon_<card>_chance`)
  for the item's tier Default, so rare lines stay rare (not exactly drop-rare: a pick is weighted among the lines still free, so a
  Chase line's share grows as the ordinary lines run out, roughly 2-6x on a nearly full piece; it is never forced). It respects `armor_modifier_cap` / `weapon_modifier_cap`, the
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
- **Second Thoughts** (owner 2026-11-26) rerolls the lowest-graded property a bag may change (`FreeUnits`: a locked one is
  never picked). Grades are compared, so on a weapon the cards compete the same way. Tie: the first of the equally low
  ones in the item's record order, which is the order the armour appraisal lists them (`LowestIndex`). The confirm prompt
  names the property and its value. A T15 simulation: every property of a 4-property piece at 800 or more takes 11 bags
  (median), down from 247 with a random pick.
- **Tempering** (owner 2026-11-26) steps the Weapon Grade (`PropertyInt.WeaponAugScaleQuality`) along
  `WeaponScalingManager.SubGradeBands` (F- -> F -> F+ -> D- ... -> A+). Up and down land on the new sub-grade's midpoint
  (`QMid`), the point the weapon ladder prices each sub-grade at; "no change" leaves the quality exactly as it was. Never
  below F- (a down roll at F- changes nothing). Never above the top (`bag_tempering_max_subgrade`, default A+); S is never
  reached, so it stays a drop-only chase. An S weapon, and a weapon already at or above the top (it could only stay or go
  down), are refused before anything is used. With the defaults F to A takes 21 bags (median), 30 at the 90th percentile;
  the old full reroll took 62 / 199. The confirm prompt shows the live chances and the grade each step leads to.

  | Stat (tier Default, read on use) | Meaning | Unset |
  |---|---|---|
  | `bag_tempering_up_chance` | chance to go up one sub-grade | 0.60 |
  | `bag_tempering_down_chance` | chance to go down one (held to 1 - up; no change is the rest) | 0.10 |
  | `bag_tempering_max_subgrade` | top sub-grade: 0 = F- ... 12 = A-, 13 = A, 14 = A+ | 14 (A+) |
- **Madness** (owner 2026-11-26, a Vaal orb: the only way past a perfect drop, and a one-use last step).

  | Default | Outcome |
  |---|---|
  | 30% | nothing changes |
  | 30% | one random eligible property is SET to max + 10..25% of its range, whatever it was |
  | 30% | every eligible property SHIFTS from its current value by -15..+15% of its range (each its own roll), and may pass the max |
  | 10% | one random eligible property is removed |

  - **Eligible (the Madness list):** Max Health (19), Damage Rating (28), Crit Damage Rating (29), Healing Boost (31), All
    Attributes (43), Armor Level (25); on weapons Biting Strike, Crushing Blow, Rending (Rend Power) and Slayer. Everything
    else is left alone by every outcome: the range-2 lines (32, 33, 47, 48), Cast on Strike (54), Shield Cleaving, Armor
    Rending, Spell Armor Rend, the protected lines, slot specials and built-ins. An item with no eligible property is
    refused before anything is used.
  - **A locked property is skipped by every outcome** (lock your best property first and outcome 4 can't take it).
  - **Removal** follows `RemovableUnits`: a Bandit Hilt keeps Biting Strike and Crushing Blow, and a Rending whose rend
    bits sit outside the first imbue slot stays. If nothing eligible may be removed, outcome 4 changes nothing.
  - **The item is Tainted every time,** "nothing" included. There is no full wipe any more.
  - **"% of range" is in grade points:** 10% of the range = 100 grade points. An over-max result is stored as a grade
    above 1000 (1100 = max + 10%), so a later rebalance of the band moves the item with it. The ceiling is grade 1250
    (max + 25%). A shift never goes below the band's minimum.
  - **Weapon cards** keep their hard clamp (`WeaponBand.Lo/Hi`; Rend Power at T25 is already at its 5.0 clamp, so it
    cannot go further), and Biting Strike has its own over-max ceiling of 0.625 crit chance (the T25 default band's value
    at grade 1250, so it only bites under a wide authored pin; with a Bandit Hilt's +0.25 it stays under the imbue-era 0.88).
    A band whose max is already above 0.625 keeps its max: the ceiling only limits the part past max.
  - **Appraisal:** an over-max armour line reads `Damage Rating +80 (over max) [14-69]`; an over-max weapon card ends
    with ` (over max)` in Property Details. The loot properties send the real grade (up to 1250) and value; Gear Grade
    counts an over-max line as 1000.
  - **What honours an over-max grade** (`ZoneStatResolver` OVER-MAX notes): the record (`Read` / `Format`, Madness keys
    only), `ValueFor` / `ValueForD` / `CardValue`, and so `Compute`: equip re-resolve (`ApplyIfStale`), ladder re-stamp,
    appraisal, loot properties and the bag chat lines. **Nothing else produces one:** drops (`RollGrade`), `AddLine`,
    `StampGraded`, the other bags and the forge's writes all clamp at 1000. A Madness item never reaches the forge anyway:
    it is Tainted, and the forge and honing refuse Tainted items. `GradeFor` (pre-grade migration) clamps at 1000 too.

  | Stat (tier Default, read on use) | Meaning | Unset |
  |---|---|---|
  | `bag_madness_nothing_chance` / `_overmax_chance` / `_shift_chance` / `_remove_chance` | outcome weights (shares of their total) | 0.30 / 0.30 / 0.30 / 0.10 |
  | `bag_madness_overmax_min` / `_max` | over-max range, share of the property's range (held to 0..0.25) | 0.10 / 0.25 |
  | `bag_madness_shift` | shift range, either way | 0.15 |

  If testing shows players still Madness junk items, the spec's fallback is a gate: every eligible property at 80% or
  more of its range.
- **Prices come from the tier Default.** A changed property is re-priced on its tier Default's band, as every bag
  always has: a piece stamped from a zone's own band override keeps that value only until a bag changes the property
  (the prompts show tier-Default values too). Live has no zone-level band overrides (checked 2026-11-26), so this only
  matters if one is authored later.
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
| `bag_weight_genesis` / `_augmentation` / `_alteration` / `_reforging` | weight of each crafting-step bag | 0 (never drops) |

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

`/create 78780310` through `78780325` give the bags (320-322 and 325 are the crafting steps). A created Bag of Vengeance has no monster
type; give it one with `/setproperty PropertyInt.GearEssenceHuntCreatureType <CreatureType number>` on the appraised bag,
or test it from a real drop. Use a bag on a T11 piece in your pack and check the appraisal before and after.

New properties: `PropertyInt.GearEssenceLockedKey` (51000), `PropertyInt.GearEssenceHuntCreatureType` (51001),
`PropertyBool.GearEssenceTainted` (51000), `PropertyBool.GearEssenceWorked` (51001).
