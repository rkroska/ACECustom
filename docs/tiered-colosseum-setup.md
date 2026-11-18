# Tiered Colosseum - Zone Control setup (tiers 11-15)

This is the paste-in command list that gives each tier of the Tiered Colosseum its own Zone Control
zone, so the Colosseum monsters get tier stats instead of retail stats. Every command below was checked
against the C# handler on branch `feature/tiered-colosseum` (file and line numbers are in the Evidence
appendix at the bottom). Anything that could not be done by a command is said so plainly.

Who can run what:

- `/zonecontrol ...` needs Developer access or higher. The server settings in Step 10 need Admin.
- None of the commands in Steps 1-10c need you to stand anywhere or select anything. Step 10d (placing
  the entry portals) uses where you stand, and so do the optional checks in Step 11 (`mobcheck` with no zone name, or with no wcid, and `here`) use where you stand or
  what you have selected.
- Zone names that contain a number are put in double quotes everywhere ("Colosseum 11"), because
  `create` would otherwise read the number as the variation.
- Paste one line at a time. The game chat sends each pasted line as one command.

How the tiers map: tier N is variation N. The tier copy of the Colosseum runs at variation 11 for
tier 11, variation 12 for tier 12, and so on. The zone must be at that same variation.

## Step 1 - Create the zone, add the landblocks, leave it NOT bounded

```
/zonecontrol create "Colosseum 11" 11 00AF
/zonecontrol addlb "Colosseum 11" 00B0,00B1,00B2,00B3,00B4
/zonecontrol boundary "Colosseum 11" show
```

- `create` makes the zone DISABLED with one landblock (00AF). `addlb` adds the five arenas.
- Do NOT run `boundary ... on`. A new zone starts un-bounded, and that is what you want.
  Why this is safe: variation 11 already has bounded zones (Tou Tou and Neftlet are bounded at 11 on the
  local copy of the live data), so the boundary system is active at variation 11. The boundary check
  skips any player standing in an indoor cell, and every placed cell in landblocks 00AF-00B4 is
  indoor (cell number 0x0100 or higher: 40 rows in 00AF and 331 in each of 00B0-00B4, none outdoor).
  So a player inside the Colosseum is never hurt by another zone's boundary, even though the Colosseum
  zone is not in the bounded list. Making the Colosseum zone bounded would add nothing.
- `boundary show` should print `free roam` for this zone.

## Step 2 - Stats: monster health, and a loot budget of zero

```
/zonecontrol set "Colosseum 11" max_health 1000000
/zonecontrol set "Colosseum 11" loot_drops_min 0
/zonecontrol set "Colosseum 11" loot_drops_max 0
```

- `max_health` is the ROOM 6 health. Later rooms get more: the server multiplies it by the room ramp
  (`tiered_dungeon_room_health_ramp`, default 1.8 = x1.8 in room 18, a straight line from x1 in room 6).
  Per-tier values are in the table under "Tiers 12-15".
- `max_health` applies to monsters when they spawn. Monsters already standing in an arena keep their
  old health until they respawn.
- The 4,000,000 in earlier versions of this doc came from an October sim run BEFORE the 2026-10-13
  tier ladder re-apply; at 4M a median 4-player T13 group needs about 88 minutes even without the room ramp (0 pct finish). Do not use it.
- Defining `loot_drops_min` switches the zone to "budget mode". With a budget of 0 the generator
  returns an empty set, so no gear, weapons, jewelry, cloaks or clothing drop from the zone's loot set.
  The tier 11 default (7 to 12 items per kill on the local data) is overridden, because a zone's own
  values beat the tier's.
- On the local data every Colosseum monster that has a death-treasure profile is tier 2-7 (94 of the
  103; the other 9 have none). All of them fall under the "zone loot floor" (they drop the zone's tier
  set), which is why the budget of 0 is what stops drops. Wielded weapons do not drop
  (`creatures_drop_createlist_wield` is false by default) and carried inventory is suppressed in zones
  (`drop_carried_inventory` is unset).

## Step 3 - Special pieces OFF

DO NOT set `special_odds 0`: the code turns 0 into "1 in 1", so EVERY kill would drop a special.

Turn off all five slot specials instead (keys 41 Fortify Vitals, 42 Battle Mending, 44 Pct HP Damage,
45 Cheat Death, 46 Regeneration - the complete list of slot specials in the game):

```
/zonecontrol modifier "Colosseum 11" special 41 off
/zonecontrol modifier "Colosseum 11" special 42 off
/zonecontrol modifier "Colosseum 11" special 44 off
/zonecontrol modifier "Colosseum 11" special 45 off
/zonecontrol modifier "Colosseum 11" special 46 off
```

The tier 11 default turns specials 41 and 45 explicitly ON; the zone's off beats it (per key, zone over tier).
Optional extra safety (not required once all five are off): make the roll astronomically rare.

```
/zonecontrol set "Colosseum 11" special_odds 2000000000
```

What was verified in the code: after the odds roll hits, the game builds the list of slot specials,
removes every one whose toggle is off, and only continues `if (specials.Count > 0)`. With all five off
the list is empty, so no special is chosen, no special piece is found or spawned, and nothing is
stamped. Nothing else in the kill path creates a special piece.

## Step 4 - Rank every Colosseum monster "Regular" (103 commands)

There is no bulk form. A rank is a marker on the monster type: the bool property 50050 (the "Regular"
rank, called minion in the code) set on that monster's bucket in the zone. This is the same command the
plugin's Rank button sends. Setting it on the bucket (not zone-wide) means the very first spawn already
resolves as Regular. Without a rank a monster reads only the tier's Default row (at tier 11: damage
resist 820 instead of 0).

```
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 194
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 195
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 227
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 231
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 1630
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 1762
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 4102
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 5711
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 6645
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 7085
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 7089
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 7090
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 7105
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 7330
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 7335
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 7336
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 7345
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 7822
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 8562
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 8673
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 9242
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 9244
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 14517
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 21164
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 21168
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 22208
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 22810
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 25596
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 25597
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 25598
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 25599
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 25961
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 25962
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 25963
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 25964
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 25965
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 25966
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 27710
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 28248
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 28249
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 34612
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 34613
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 34615
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 34620
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35115
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35116
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35117
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35118
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35119
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35121
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35122
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35123
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35124
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35125
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35126
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35127
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35128
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35129
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35130
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35131
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35132
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35133
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35134
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35135
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35136
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35137
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35138
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35139
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35140
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35141
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35142
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35143
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35144
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35145
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35146
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35147
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35148
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35149
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35150
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35151
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35152
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35153
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35154
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35155
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35156
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35157
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35158
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35159
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35160
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35161
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35162
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35163
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35164
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35165
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35166
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35167
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35252
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35253
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35254
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35255
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35256
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35305
/zonecontrol prop "Colosseum 11" bool 50050 true --wcid 35306
```

## Step 5 - Monsters that ignore the player's magic resistance and shield (creature-side flags)

Verified against the local world database: wcids 35160 Annihilator and 35161 Stomper carry
PropertyBool 65 (IgnoreMagicResist) = true and PropertyFloat 151 (IgnoreShield) = 1.0 on the creature itself.
The damage code reads both from the attacking creature at hit time, so stamping them off works.
`ZonePropGuard` only blocks int 1, 9007, 9043 and bool 50047, so these are allowed.

```
/zonecontrol prop "Colosseum 11" bool 65 false --wcid 35160
/zonecontrol prop "Colosseum 11" float 151 0 --wcid 35160
/zonecontrol prop "Colosseum 11" bool 65 false --wcid 35161
/zonecontrol prop "Colosseum 11" float 151 0 --wcid 35161
```

The stamp is applied when a monster spawns (it does not change monsters already alive).

Also found on the local database (not in your brief; the same command fixes them, run these if you want
those monsters to obey the player's protections too):

- 35118 Sentient Crystal Shard carries bool 65 (IgnoreMagicResist) AND bool 66 (IgnoreMagicArmor) on the creature.
- 35146 Olthoi Slayer, 35149 Olthoi Ripper, 35150 Olthoi Slasher and 35151 Swarm Demon carry float 151 (IgnoreShield) = 1.0 on the creature.

```
/zonecontrol prop "Colosseum 11" bool 65 false --wcid 35118
/zonecontrol prop "Colosseum 11" bool 66 false --wcid 35118
/zonecontrol prop "Colosseum 11" float 151 0 --wcid 35146
/zonecontrol prop "Colosseum 11" float 151 0 --wcid 35149
/zonecontrol prop "Colosseum 11" float 151 0 --wcid 35150
/zonecontrol prop "Colosseum 11" float 151 0 --wcid 35151
```

## Step 6 - The two damage spikes: Tukora (room 13) and Mukkir (room 12)

Verified: wcid 35305 Tukora Commander and 35306 Tukora Lieutenant each wield weapons 31033, 31250 and
31251, and all three weapons carry bool 65 (IgnoreMagicResist) and bool 66 (IgnoreMagicArmor) on the
WEAPON (31033 also carries IgnoreShield 0.6). A zone prop stamp goes on the creature only, so it cannot
reach the weapon. No command found that changes a weapon's flags for a zone.

The four Mukkir types of room 12 (35142 Mukkir Draktehn, 35143 Mukkir Kartak, 35144 Mukkir Laktar, 35145
Umbral Mukkir) have no ignore flags but hit about 3x harder than the Colosseum median in the 2026-11-11 sim.

Fix: a per-monster damage multiplier. It is RELATIVE, so it stays right when the tier ladder is re-tuned
(the older fixed `attack_damage` per tier went stale at every re-apply), and one command covers every tier.
It applies only where a Zone Control zone governs the monster (never retail), and these six types spawn
nowhere but the Colosseum rooms (checked: no placed instance, no other generator).

```
/zonecontrol damagemult 35305 0.25
/zonecontrol damagemult 35306 0.25
/zonecontrol damagemult 35142 0.5
/zonecontrol damagemult 35143 0.5
/zonecontrol damagemult 35144 0.5
/zonecontrol damagemult 35145 0.5
```

These are run ONCE, not per tier. Starting values from the sim (see "Measured 2026-11-11" at the bottom);
re-tune after a logged test run. If a zone from an older version of this doc still has an `attack_damage`
override on 35305 / 35306, clear it (`/zonecontrol clearstat "Colosseum N" attack_damage --wcid 35305`, and the
same for 35306) so the two fixes do not stack.

NOT NEEDED for 34615 Elite Guardian: on the local database it wields weapon 32665 (Guardian's
Smoldering Atlan Sword), which has no IgnoreMagicResist, IgnoreMagicArmor or IgnoreShield, and the
creature has none either. No command is given for it. Among the 103 Colosseum wcids only 35305 and 35306
wield 31033 / 31250 / 31251.

## Step 7 - Gear essences (salvage bags) OFF

The kill code rolls a gear essence only when `bag_odds` is set; unset or below 1 means never. There is
no bag_odds in any tier default on the local data, so nothing should drop. To make it explicit, and to
beat any tier setting added later:

```
/zonecontrol set "Colosseum 11" bag_odds 0
```

`bag_odds 0` really is off (unlike special_odds): the roll returns "no drop" when the odds are below 1.

## Step 8 - Rares

Nothing to do. The server skips the retail rare roll for every kill inside a tier copy of the Colosseum
(`Creature_Death.cs`, the `TierAt(Location) == 0` check before `corpse.TryGenerateRare`). The tier copy pays
Prestige Coins only. Retail kills are unchanged.

## Step 9 - Enable the zone

```
/zonecontrol enable "Colosseum 11"
```

Stats read live apply at once; health, attributes and prop stamps apply as monsters spawn. A reload is
not needed (each command saves immediately). `/zonecontrol reload` only re-reads the stored data and is
for hand edits to the database.

## Step 10 - Content, no-log, switch on, entry portals (in this order)

### 10a - Apply the content SQL first

Run `Database/Updates/World/2026-10-09-00-Tiered-Colosseum.sql` against `ace_world` (it clones the retail
Arbitrator 34441, ticket 34449, ticket seller 34442 and portal 777705100, so the world DB must still hold
those). Then check it landed - both counts must match before anything is switched on:

```sql
SELECT COUNT(*) FROM ace_world.quest
 WHERE name IN ('TieredColoRooms', 'TieredColoTopTier', 'TieredColoRun', 'TieredColoPayTimer');   -- 4
SELECT COUNT(*) FROM ace_world.weenie
 WHERE class_Id IN (78780800, 78780801, 78780802, 78780803,
                    78780811, 78780812, 78780813, 78780814, 78780815);                          -- 9
```

Without the quest rows the server credits no room and pays nothing (it logs
`[TIERED] tiered dungeon content is missing`). Restart, or reload the world cache, after applying.

### 10b - No-log for the arenas

```
/nolog add 00B0,00B1,00B2,00B3,00B4 all
```

- This ADDS to whatever is stored and saves at once, so existing overrides on the shard are kept.
- It covers the five arenas (00B0-00B4) for the base world AND every variation. The retail seed list
  already has them for the base copy, so retail behaviour does not change; the only new effect is for
  variations 11 and up. The lobby 00AF is not added (it is not in the seed either).
- Do NOT use `/modifystring nolog_landblocks ...` for this: it REPLACES the whole stored value. If you must,
  first read the current value (`/showprops nolog_landblocks` or `/nolog list`) and include every token of
  it, with no spaces (`modifystring` only takes the first word).

### 10c - Switch on

```
/modifybool tiered_dungeons_enabled true
/modifystring tiered_dungeon_tiers 11
```

- `tiered_dungeon_tiers` accepts `11`, `11-15`, or `11,13`. List a tier only once its zone (Steps 1-9) exists.
- `modifybool` and `modifystring` change the value in memory and queue a database save that runs about
  every 5 minutes. Wait 5 minutes before restarting the server.

### 10d - Place an entry portal for every enabled tier

The SQL creates the entry portals but does NOT place them (no `landblock_instance` rows): where they stand
is your choice. Each one sends players to its tier's copy and checks that tier's aug gate:

| Tier | Entry portal wcid | Gate (creature augs / item augs) |
|---|---|---|
| 11 | 78780811 | 4000 / 2000 |
| 12 | 78780812 | 4500 / 2500 |
| 13 | 78780813 | 5000 / 3000 |
| 14 | 78780814 | 5500 / 3500 |
| 15 | 78780815 | 6000 / 4000 |

For each enabled tier: go to the spot you chose, on the layer players of that tier use (the plan puts it in
that tier's Tou Tou, the same place the lobby exit portal returns to: cell 0xF559003D at the tier's
variation), and run

```
/createinst 78780813
```

(with that tier's wcid). `/createinst` saves one permanent `landblock_instance` row at the layer you stand on;
at variations 11-25 it never mirrors to other layers. An entry portal whose tier is not listed in
`tiered_dungeon_tiers` refuses players ("This arena is closed at the moment"), so placing all five early is
safe.

Run tuning (live settings, defaults shown - nothing to type unless you change them):

| Setting | Default | What it does |
|---|---|---|
| `tiered_dungeon_room_health_ramp` | 1.8 | Monster health in room 18 vs room 6 (straight line between). Stamped at spawn. |
| `tiered_dungeon_room_damage_ramp` | 1.0 | Everything monsters deal, room 18 vs room 6. Off at launch; raise (e.g. 1.4) after deaths per room are logged. Read live. |
| `tiered_dungeon_min_room_tag_share` | 0.25 | A player needs this many tags, as a share of the room's kills, to be credited with the room. A tag = a monster they or their pet damaged, or a heal that restored health to another player in the room (so pure healers qualify). A room with no kills recorded credits nobody (logged as an error). 0 = standing there is enough. |
| `tiered_dungeon_pay_table` | `13:6,14:9,15:13,16:17,17:23,18:30` | Coins in T11 terms by furthest room. |
| `zc_pcoin_kill_tier_step` | 1.2 | Shared with per-kill coins: each tier pays this much more (full clear 30 / 36 / 43 / 52 / 62 at T11-T15). |
| `tiered_dungeon_pay_percents` | `100,50,0` | Pay at your tier / one tier above the copy / two or more. |

The ramps multiply the copy's OWN tier stats. They never borrow the next tier's stats: those are tuned for
the next tier's gear and do not step evenly (stored T16 monsters hit about 17 pct softer than T15).

## Step 11 - Verify in game

```
/zonecontrol show "Colosseum 11" --wcid 35160
/zonecontrol mobcheck "Colosseum 11" 35160
/zonecontrol here
```

- `show ... --wcid 35160` prints the monster's final values and which layer each came from. For a ranked
  Regular monster at tier 11 you should see: the header `rank Regular (from zone)`, `max_health = 1000000`
  (from the zone; the room ramp is added on top at spawn, so an appraised monster in room 12 shows about
  1.4M), `damage_resist_rating = 0` (from the tier) and `attack_damage = 1898`.
- `mobcheck "Colosseum 11" 35160` should print `ok    rank: Regular (from zone) ...` and `ok    zone enabled at
  variation 11`. With no zone name it uses the zone you stand in; with no wcid it uses the creature you
  have selected.
- `here` (stand in the arena) lists the zones covering your landblock.
- A live monster: start a fresh arena run (monsters that were already spawned keep old values), select a
  monster, and appraise it to read its health; `/zonecontrol mobcheck "Colosseum 11"` with it selected
  checks that monster's type.

## Tiers 12-15 - repeat Steps 1-7 and 9 with these values

In a text editor, take the Step 1-5, 7 and Step 9 lines, replace `Colosseum 11` with the new zone name,
the variation `11` in the `create` line with the new variation, and change `max_health` to the tier's value
below. Step 3, Step 4 (the 103 lines), Step 5 and Step 7 are identical apart from the zone name. Step 6
(damagemult) is NOT repeated: it already covers every tier.

| Tier | Zone name | Variation | max_health (room 6) | Room 18 at ramp 1.8 |
|---|---|---|---|---|
| 11 | Colosseum 11 | 11 | 1000000 | 1.80M |
| 12 | Colosseum 12 | 12 | 900000 | 1.62M |
| 13 | Colosseum 13 | 13 | 1250000 | 2.25M |
| 14 | Colosseum 14 | 14 | 1300000 | 2.34M |
| 15 | Colosseum 15 | 15 | 1500000 | 2.70M |

For example, tier 14 begins:

```
/zonecontrol create "Colosseum 14" 14 00AF
/zonecontrol addlb "Colosseum 14" 00B0,00B1,00B2,00B3,00B4
/zonecontrol set "Colosseum 14" max_health 1300000
```

### Measured 2026-11-11

Each max_health above was solved so the MEDIAN random 4-player fellowship of that tier's active characters
clears rooms 6-18 in about 50 minutes, with the room ramp on and the damage ramp off. Player damage is the
server's own damage code on each character as saved (prod copy 2026-11-09, admin characters left out);
wave timing from the arena generators (53 waves, 544 monsters, ~90 s a room floor), fights 70 pct efficient.

| Tier | Characters | 3 players | 4 players | 5 players |
|---|---|---|---|---|
| 11 | 16 of 80 | 57 min, 58% finish | 49 min, 77% | 44 min, 95% |
| 12 | 10 | 59 min, 55% | 50 min, 89% | 46 min, 100% |
| 13 | 10 | 57 min, 57% | 48 min, 88% | 44 min, 97% |
| 14 | 6 | 60 min, 54% | 50 min, 100% | 44 min, 100% |
| 15 | 5 | 59 min, 60% | 50 min, 100% | 45 min, 100% |

- T12 needs less health than T11: its active characters do less damage against their own tier (median
  62k single-target vs 82k at T11). That is the data, not a typo.
- T14 and T15 rest on 6 and 5 characters; treat them as a starting point.
- Within one tier, single-target damage spreads 30:1 or more (two void casters carry T13), so WHO is in the
  group matters more than how many. The model has no deaths, regroups, monster spells or healing: real runs
  will be slower. Re-tune from logged room times after the first test (estimate, not simulated: each 10 pct of health moves a
  4-player clear by about 3 minutes).

- The Regular attack_damage values match the stored tier defaults on the local data (1898, 2523, 3102,
  3731, 4723). The tier Regular damage_resist_rating on the local data is 0, 104, 230, 410, 791 for
  tiers 11-15, so only tier 11 is 0.
- `/zonecontrol clonezone` is not a shortcut: it copies only the landblocks and flags, not stats or
  ranks, and it names the copy "<source name> <variation>" (a zone called "Colosseum 11" would give
  "Colosseum 11 12").
- Enable tiers 12-15 only after their tier defaults (`default <variation>`) are authored; they are on the
  local data.

## Summary: what has a command and what does not

| Item | Result |
|---|---|
| Zone, landblocks, variation, enabled | command found |
| Not bounded; indoor safety | no command needed; indoor cells confirmed exempt |
| max_health | command found |
| Regular rank for 103 wcids | command found (one per wcid, no bulk form) |
| Loot off | command found (loot_drops_min and loot_drops_max 0) |
| Specials off | command found (5 toggles; never special_odds 0) |
| Gear essences off | command found (bag_odds 0) |
| Rares off for the zone | Nothing to do; the server skips the rare roll inside a tier copy |
| Annihilator / Stomper flags | command found (prop bool / float) |
| Flags on the Tukora weapons | NO command for the weapon; per-monster damagemult 0.25 (Mukkir 0.5), once for all tiers |
| Elite Guardian | not needed (its weapon has no such flags) |
| Server settings | command found (modifybool, modifystring, or /nolog add) |
| Verification | command found (show, mobcheck, here) |

## Evidence appendix

All paths are under `C:\Scripting\ACECustom-colosseum\Source\ACE.Server\` (branch feature/tiered-colosseum).
`ZCC` = `Command/Handlers/ZoneControlCommands.cs`. `ZSM` = `Managers/ZoneScaling/ZoneScalingModels.cs`.
`ZCM` = `Managers/ZoneControl/ZoneControlManager.cs`. `CD` = `WorldObjects/Creature_Death.cs`.

Commands:

- `/zonecontrol` handler, Developer access: ZCC:132. Quote-aware tokenizer: ZCC:4105-4123. Zone-name collapse: ZCC:4128-4143. `--wcid` flag: ZCC:4181-4188.
- `create`: ZCC:412-464 (variation must be 11 or higher: ZCC:443; created DISABLED: ZCC:457-462; landblock token: ZCC:4153-4166).
- `addlb` (comma lists): ZCC:647-667.
- `boundary show|on|off`: ZCC:2402-2440. A new zone is not bounded: `Bounded` defaults to false, `Managers/ZoneControl/ZoneControlModels.cs:33`.
- `set <zone> <stat> <value> [--wcid]`: ZCC:821-840 (number parse ZCC:4234; stat names must be in `ZoneStat.All`: ZCC:5176-5180).
- Stat names: max_health ZSM:40; attack_damage ZSM:169; special_odds ZSM:354; bag_odds ZSM:358; loot_drops_min and loot_drops_max ZSM:504-505.
- `modifier <zone> special <key> <on|off|clear>`: ZCC:944-962 (rejects keys that are not slot specials).
- `prop <zone> <int|int64|float|bool> <id> <value> [--wcid]`: ZCC:1462-1499 (id parse ZCC:5216-5257; guard ZCC:5259-5266; a `bool` value is true for true, 1 or on, anything else false: ZCC:1487).
- `ZonePropGuard` (blocks only int 1, 9007, 9043 and bool 50047): ZSM:841-861. Re-checked at stamp time: `Managers/ZoneControl/ZoneSpawnScaler.cs:179-201`.
- `enable`: ZCC:486-494. `reload`: ZCC:272-275. `here`: ZCC:258-270. `show`: ZCC:2833-2852. `mobcheck`: ZCC:2760-2829, report text ZCC:5610-5655.
- Every verb saves at once: ZCM:1999-2013 (MutateArea calls Save).
- `clonezone` copies landblocks and flags only and derives the name: ZCC:2679-2750 (clone object ZCC:2722-2735).
- `/modifybool`: `Command/Handlers/AdminCommands.cs:7388-7411`. `/modifystring`: AdminCommands.cs:7797-7807 (uses the first two words only). `/nolog add`: AdminCommands.cs:7583-7740, writes and flushes at 7693-7714 (the 5-minute save delay is explained in the comment at 7707-7713).
- Server settings: `Managers/PropertyManager.cs:985` (tiered_dungeons_enabled), `:986` (tiered_dungeon_tiers); the tier parser is `Managers/TieredDungeons/TieredDungeonRegistry.cs:159-196` (accepts `11`, `11-15`, `11,13`).

Rank:

- Rank bool ids (Boss 50048, Leader 50049, Regular 50050): ZSM:404-406. Rank from a bucket's bools: ZSM:915-922.
- A bucket's rank bool decides the rank before the monster's own bools: ZCM:1043-1048 (RankOf), ZCM:1114-1130 (ResolveRankForWcid), used at spawn resolve ZCM:988-989. Why the bucket: the comment at ZCM:1036-1042.
- The plugin's Rank button is `prop bool <id>`: comment at ZCC:5235-5237.
- Layer order (tier default, then zone Default row, then per-WCID bucket; a zone's Default row beats a tier's rank row): ZSM:1042-1050 and ZCM:779-789.

Boundary:

- `WorldObjects/Player_ZoneBoundary.cs:42-52` (indoor players are exempt and any wisp is cleared), `:58` (no bounded zone at the variation means free roam), `:65` (outside the allowed list is punished), `:167-172` (indoor danger check).
- Indoor test: `Source/ACE.Entity/LandblockId.cs:85` (cell low word >= 0x100). Allowed-list logic: ZCM:1568-1583. Minimum bounded variation 11: ZCM:110.
- Local data (read-only SELECT): `landblock_instance` rows in landblocks 0x00AF-0x00B4 are all indoor (40 in 00AF, 331 in each of 00B0-00B4, 0 outdoor). Bounded zones at variation 11: Tou Tou and Neftlet (shard table `config_properties_string`, key `zonecontrol_data`).

Loot, specials, essences, rares:

- Zone loot floor and tier: CD:1005-1031. Budget mode: CD:1110-1135. A budget of 0 returns an empty set: `Factories/LootGenerationFactory_ZoneSet.cs:701-707`. The set is only generated `if (slotCounts.Any)`: CD:1142-1144.
- Specials: odds CD:1163-1168 (`Math.Max(1, OddsToInt(odds))`; OddsToInt returns 0 for 0: `Managers/ZoneControl/ZoneStatResolver.cs:299-300`); disabled specials removed CD:1173; an empty list skips everything CD:1174. Slot specials in the catalog: `Managers/ZoneControl/ZoneModifiers.cs:273-282` (keys 41, 42, 44, 45, 46) and `:612-619`. Toggle read: ZSM:1363-1365; merged per key: ZSM:1154-1156.
- Gear essences: roll CD:1310-1325; `Entity/GearEssences.cs:353-357` (`bag_odds` unset or below 1 returns null).
- Carried inventory suppressed: CD:1387-1388. Wielded drops: `Managers/PropertyManager.cs:444`.
- Rares: CD:904-926 (CanGenerateRare, `corpse.TryGenerateRare`), `WorldObjects/Corpse.cs:259-345`, `Factories/LootGenerationFactory_Rare.cs:44-54` (global rate only; default `Managers/PropertyManager.cs:855`). No zone or rare stat: no match for "rare" in the stat definitions under `Managers/ZoneScaling` or `Managers/ZoneControl`.

Damage path (the flags are read live from the attacker):

- `Entity/DamageEvent.cs:126-128` (weapon OR attacker). `WorldObjects/Creature_BodyPart.cs:48-49`. `WorldObjects/Creature_Combat.cs:727` and `:768` (shield). `WorldObjects/WorldObject_Weapon.cs:935-945` (bool getters read the property), `:1209-1227` (IgnoreShield: the larger of creature and weapon).
- Spawn stamping: `Managers/ZoneControl/ZoneSpawnScaler.cs:129` calls ApplyProps; generator spawns reach it at `Entity/GeneratorProfile.cs:323`.

Database facts (read-only SELECT on the local world database, a copy of the live data from 2026-10-07):

- weenie_properties_bool 65 = 1 on 35160, 35161 and 35118; 66 = 1 on 35118. weenie_properties_float 151 = 1.0 on 35160, 35161, 35146, 35149, 35150 and 35151.
- weenie_properties_create_list: 35305 and 35306 wield 31033, 31250, 31251 (destination 2 = wield). 34615 wields 32665 only. Weapons 31033, 31250, 31251 have bool 65 and 66 = 1; 31033 also has float 151 = 0.6. Weapon 32665 has none of them.
- All 103 wcids in `colo_wcids.txt` are creatures (weenie type 10). Death treasure tiers: 2-7 for 94, none for 9.
- Tier defaults in `zonecontrol_data`: v11 Default row max_health 1000000, loot_drops 7-12, special_odds 151200, customSpecials 41 and 45 on; Regular row damage_resist_rating 0 and attack_damage 1898; no bag_odds anywhere; v12-15 Regular attack_damage 2523 / 3102 / 3731 / 4723.

Server setting facts:

- Retail seed contains 00B0-00B4 for base: `WorldObjects/Player_Location.cs:813-817`. Token format `[+|-]HEX[:base|all|variation]`: `:903-939`. "all" means base plus every variation, and the seed only ever covers base: `:964-990`, `:849-850`. Stored value on the local copy: `654C:all` (shard `config_properties_string`, key `nolog_landblocks`).

## Open items / could not verify

1. Nothing here has been run on a live server. It is from reading the code and the local database copy. Check Step 11 on a real arena run, at tier 11 first.
2. Rares: handled in code (no rare roll inside a tier copy). Confirm on the test run that no rare drops.
3. Tukora weapons: the real fix is to remove bool 65 and 66 from weapons 31033, 31250 and 31251 in the world database (those weapons are shared with other monsters outside the Colosseum list). Not done here. The damagemult values in Step 6 (0.25 Tukora, 0.5 Mukkir) are starting values to be re-measured.
4. Zone-wide bounty (`/zonecontrol zonebounty`): if it is on, kills in any enabled T11+ zone, these included, count toward it. Not checked on the local data. No per-zone exclusion was found.
5. Bonus currency and Zone Share: the v11 tier default has no bonus-currency drops and Zone Share is off by default on the local data. If either changes, check the Colosseum zones.
6. Everything in `zonecontrol_data` could also be edited at the store level (the shard `config_properties_string` row), but no procedure is given.
7. Elite Guardian 34615: the brief said it wields 31033 / 31250 / 31251; the local database says otherwise. If the live database differs, repeat the check.
8. The `show` provenance text (the part in brackets after each value) was not run, so the exact wording on screen may differ from the description in Step 11.
9. `/zonecontrol set ... special_odds 2000000000` (the optional line in Step 3) is allowed by the code (large values are clamped, ZoneStatResolver.cs:299-300) but is not needed once all five specials are off.
