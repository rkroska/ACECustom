# T11+ rare elements and the zone Slayer pool

Worktree `C:\Scripting\ACECustom-slayerpool`, branch `feature/zone-slayer-pool`, off upstream master `699f6101a`
(includes the weapon group weights, #586). Built 2026-10-30, uncommitted and untested in game. Numbers come from the local
databases, not prod.

## Goal

1. A Pristine or Ascendant weapon always deals the element the monster that dropped it is weakest to, so its
   guaranteed Slayer is never cancelled by a 4x resist.
2. Normal Slayer drops can also name other species in the zone, without making Slayers worse to hunt for.
3. Bags of Vengeance do not change.

## Rules

| Drop | Slayer species | Element |
|---|---|---|
| Normal weapon | `slayer_pool_share` chance: any species in the zone, weighted by spawns. Otherwise the killed species. | Random, as today |
| Pristine, Ascendant | Killed species, as today | Killed monster's weakest (new) |
| Bag of Vengeance | Killed species, as today | Not applicable |
| `/zcrare` dev mint | Named species, as today | As rolled (no kill) |

Rare element edge cases:

- **Nether weapons:** every Nether weapon stays Nether, casters and also the Nether melee (1.4-1.7%) and missile (5%)
  weapons in the T11+ tables (owner 2026-10-30). Every custom-resist monster takes the same 25% from nether, so there is
  no better target for one. The rule only chooses among the other seven elements, and it never makes a weapon Nether.
- **No single weakest element** (Neftlet, where all are 1.0): the weapon keeps its rolled element.
- **Weapon already deals only an element in the weakest set:** no change.
- **Slash/Pierce weapon:** it becomes one single element from the weakest set, preferring one of its own two. Otherwise a
  Pierce-weak monster could produce a Slash Rending weapon, which `ZoneSlashRendDamageType.Normalize` then makes
  Slashing only.
- **A tie for weakest (2-6 elements):** keep the weapon's element if it is in the set. Otherwise take the lowest bit of
  the mask, so the result is deterministic and testable. No Tou Tou monster has a tie today, but `resist_*` can create one.
- **Plain bows (`W_DamageType` Undef):** skipped, though the T11+ tables hold none today. Their element comes from the ammo, and they have no elemental damage
  bonus, so a stamped element would add a rend and procs that do not match what the bow deals.

Normal Slayer rules:

- **Not matched to the weapon (owner ruling 2026-10-30).** The pool ignores the weapon's element and rend. Matching a Slayer
  to its element is for Pristines and Ascendants only. An element-matched pool was built first by mistake and removed: it
  raised usable Slayers about 4x, which the owner did not ask for.
- **The pool is empty:** use the killed species.

## The evidence

From `slayer_math.py` and `tier_check.py` in this folder.

- All 36 Tou Tou monsters with custom resists, on all 15 tiers: one element at 1.00, six at 0.25, nether 0.25.
- Random rare element: 36% of best damage on the species it slays. "3 weakest": 50%. Weakest: 100%.
- Resist x rend x Slayer on its Slayer target (T11 / T18 / T25):
  - rare, right element: 5.57 / 7.71 / 10.18
  - normal drop, right element, no Slayer: 2.64 / 3.17 / 3.69
  - rare, wrong element: 1.39 / 1.93 / 2.54
- Pool, with Slayer chance unchanged. Time to a Pierce Ursuin Slayer, 1H melee, 300 kills an hour, share 0.5:

| How you play | Today | Plain pool |
|---|---|---|
| Only killing Ursuin | 14 min | About 25 min |
| Killing the zone's natural mix | About 2 hours | About 2 hours |
| Killing anything but Ursuin | Never | About 4 hours |

The overall rate stays flat. The pool only adds variety, at a cost to anyone hunting one species. A lower share costs them less.

## Build

### 1. A monster's weakest elements

`Creature_Properties.cs`, new public method `GetWeakestElements()`, returning a `DamageType` mask of the top elements
among slash, pierce, bludgeon, fire, cold, acid and electric. It returns `Undef` when all seven are equal.

Score per element: `ZoneStatOr(resist_x, ResistX ?? 1.0)`, times `ElementalWeaknessFactor` when the element is in
`ElementalWeaknessMask`, then the armor curve on `armor_vs_x`.

Do not reuse `DamageEvent.GetWeakestElement`. It reads `GetResistanceMod`, so a vuln on the dying monster and the
attacker's rend would change the answer.

Put the scoring in a pure static (`WeakestElements(resist7, weakMask, weakFactor, armorVs7, avgBaseArmor)`), so tests can
drive it without a `Creature`. `GetWeakestElements()` only gathers the inputs, resolving the zone profile once instead
of 14 times per spawn.

### 2. The pool

New `Managers/ZoneControl/ZoneSlayerPool.cs`:

- Key: zone name + variation. Add `ZoneControlManager.ZoneKeyFor(Creature)`, which returns null when
  `IsZoneScalingExempt(c)` is true and otherwise uses `FindZoneRef`. `FindZoneRef` alone misses the creature's own exempt
  bool and non-combat pets, which loot and combat treat as ungoverned.
- Value: spawn counts per `CreatureType`.
- `Record(Creature)` runs on landblock threads, so use `ConcurrentDictionary` and `Interlocked`.
- `Pick(zoneKey, double roll)`: weighted by count. Pure, so tests can drive it.

Recording filter: not `IsNPC`, `Attackable`, `CreatureType` not null or `Invalid`, not `OnlyCombatPetsCanDamage`, and
`ZoneKeyFor` not null. On Tou Tou v11 `Attackable` splits them exactly: all 36 monsters are true and the other 24 creatures
store false. A loot-table check was tried and removed, because a governed monster with no table still drops zone loot
through the fallback profile, and the review's premise that NPCs store nothing came from a bug in my data script.

Weights: each spawn source counts up to its capacity, so the mix settles instead of drifting with uptime. A placed monster
counts once. A generator's source is the top of its chain, and its cap is the profile's `MaxCreate` (or the generator's
limit when that is -1) times each parent generator's limit.

Call `Record` from the two world spawn paths:

- `Entity/GeneratorProfile.cs`, after the `Spawn_*` call succeeds. `ApplyToSpawn` (line 323) is too early here: the
  generator link that exempt generators rely on, and the final location, are only set by then.
- `Factories/WorldObjectFactory.cs`, right after `ApplyToSpawn` (placed monsters).

**Not inside `ApplyToSpawn`.** The admin spawn commands and the Zone Control preview also call it, and those must not
add species.

The pool lives in memory and starts empty on restart. Zone Control reloads do not clear it, because the plugin syncs
about every 2 seconds.

### 3. The setting

`ZoneScalingModels.cs`: `SlayerPoolShare = "slayer_pool_share"`, appended at the end of `ZoneStat.All`, after
`LootWeightVoid`. Default 0, which is today's behaviour. I would set 0.5 on v11 when turning it on. Read it with
`p.GetT(stat, 0, lootTier)`, as the other chance stats do, so `_t25` anchors work.

**Name trap:** do not use the `weapon_slayer_pool_chance` name proposed earlier. `BuildWeaponCardChances` treats every
`weapon_*_chance` stat as a weapon card and it would become a `/zonecontrol weaponcard` toggle.

Update the master's stat-order tests: `Stats_AreAppendedInOrder` now checks a run, so it should keep passing.

### 4. Normal Slayer pick

`ZoneLootMutator.cs`, inside `if (gotSlayer)`:

```csharp
wo.SlayerCreatureType = isRare ? slayerType : ZoneSlayerPool.PickFor(killed, p, lootTier) ?? slayerType;
```

Eligibility at line 514 stays as it is, so the Slayer chance does not change. Only the species does. No random number
is drawn when the share is 0.

### 5. Rare element

`ZoneRareItems.cs`, new `ZoneRare.ApplyWeakestElement(WorldObject wo, Creature killed)`. Call it from
`Creature.ApplyZoneDropSweep` immediately after `StampGateTier` (`Creature_Death.cs:1764`), before `ApplyZoneGearStats`,
only when `rare != None` and `killed != null`.

It must run before `ApplyZoneSpells` (which reads `W_DamageType`) and `MutateLootItem` (rend and proc eligibility, lines
506-511). A source-order test pins this.

The existing `GearEssences.ApplyElement` (the Bag of Transmutation code) is the right model, but do not call it as is:
it refreshes the wield gate from a tier that is not stamped yet. Extract the rename and rend-swap part into an
`internal` method and call that. The later steps of the sweep already do the tint, the wield gates and `Normalize`.

Skip non-weapons, nether casters (`W_DamageType == Nether`) and the edge cases above. Applies to melee, elemental bows
and casters.

Names: `ElementRename` only swaps a word that is already there, so a physical melee rare ("Ono") made Fire would keep its
plain name while tinted Fire. When going physical to elemental, add the type word if the name has none. The icon stays
the old element's, the same gap the Bag of Transmutation has today.

### 6. Dev command

`/zcslayerpool [part of a zone name]`: prints each species and its count. Add `clear`. Developer only, plain
ASCII, built with `"\n"`.

## Tests

New `Source/ACE.Server.Tests/ZoneSlayerPoolTests.cs`:

1. `Pick` honours weights and stays in its own zone.
2. An empty pool returns null, so the killed species is used.
3. `Record` ignores NPCs, pets and invalid types.
4. Share 0 never picks from the pool and draws no random number.
5. The pure `WeakestElements` with custom resists, a weakness mask, ties and all-equal resists. A `Creature` can't be
   built in the test project (its constructor needs DATs).
6. Rare element choice as a pure function: single type, Slash/Pierce to Pierce, a tie, leaves nether, Undef and all-equal
   alone.
7. Build a melee weapon, launcher and caster from a hand-made Weenie (as `ZoneSlashRendDamageTypeTests` does), call the
   extracted rename and rend-swap method, and check `GetMatchingRends` and `TryGetProcSpells` agree with what it carries.
   Physical to elemental adds the name word. The full sweep can't run in tests, so in-game step 5 covers it.
8. Source guard: the Slayer stamp passes nothing about the weapon to the pool, and rares keep the kill.
9. Source guards: `Record` is not inside `ApplyToSpawn`, `ApplyWeakestElement` comes before `ApplyZoneGearStats` and
   `MutateLootItem`, and `slayer_pool_share` is not in `WeaponCardChances`.

Release x64 build, then the full suite. Compare failures with master's known list.

## In game

1. `/zcslayerpool` in Tou Tou lists monsters only, with no Watch NPCs and no summoned pets.
2. Summon a pet, spawn a monster with an admin command: counts do not move.
3. Set `slayer_pool_share` to 1 on v11, kill Sleech: Slayers name other zone species, regardless of the weapon's element.
4. Set it to 0: back to the killed species.
5. Rares: set `@modifylong zc_rare_pristine_odds 1` on the test server (the `/zcrare` mint has no kill, so it can't test
   this), then kill a Gloom Shadow. Confirm Slashing with Slash Rending, a Shadow Slayer, and correct name, tint and wield
   gates. Repeat for a bow and a War caster, and for Ascendant. Restore the odds afterwards.
6. A Void rare stays Nether.
7. Restart: pool empty, drops fall back to the killed species, nothing errors.

## Check on prod before building

I have no access to prod, so the owner runs this and sends the file:

```sql
SELECT value FROM ace_shard.config_properties_string WHERE `key` = 'zonecontrol_data';
```

Confirm from it:

- The one-weak, six-at-0.25 pattern holds on prod.
- `bag_odds` and `bag_weight_vengeance`. The local store has neither, so Bags of Vengeance may not drop at all, and
  they are part of the argument that a wrong Slayer can be fixed.
- Card chances above T11. The tier defaults I read had none for T12+, so the 21% Slayer chance is proven for T11 only.

## Risks and open questions

1. **Hunting one species gets slower.** At 0.5, a Pierce Ursuin Slayer goes from 14 to about 25 minutes while hunting Ursuin, and the overall rate is unchanged. Start with a lower share if that is too much.
2. **Rare element is now predictable by species.** At 1 in 864,000 this is not farmable in practice, but every rare from
   a species will now share an element, so a rare has less surprise. Owner sign-off needed.
3. **Missile weapons.** `DamageEvent` uses the ammo's type unless it is prismatic, so a bow's rare element only matches
   with matching or prismatic ammo. Needs checking
   in game.
4. **Casters.** The element sets the rend and the elemental damage bonus, not which spell is cast. The player must cast
   the new element to get both. A mismatched caster keeps 25-36% instead of 25%, before that bonus, so it loses more than
   the math shows.
5. **Omni Strike.** Melee with this charm already hits the best element, so the rule gives them nothing extra.
6. **Capacity weights** ignore respawn speed, so a slow-respawning camp counts the same as a fast one of equal size.
   Pool contents depend on which areas have loaded since restart. Check real shares with the dev command.
7. **Pool never forgets within an uptime.** A monster later exempted or removed stays until restart or `clear`.
8. **Existing rares are not changed.** Only new drops. Existing wrong-element rares stay as they are, because rares
   take no bags. Owner decision whether to offer anything for those.
9. **Plugin GUI.** The Zone Control plugin source is not on this machine, so the new setting is command-line only until
   its stat list is updated.
11. **Nether-weak monsters.** The rule only scores the seven non-nether elements, so a monster weak only to nether reads
    as having no weakness. None exist in Tou Tou. If one is authored, decide then whether its rares should become Nether.
10. **Ascendant base damage** was left out of the math, so a wrong-element Ascendant is somewhat better than shown.

## Review

Fable reviewed this plan on 2026-10-30 and all of its findings are folded in above: no blockers, seven should-fix, two
nits. The main ones were pool picks for Slash/Pierce weapons, NPCs getting past the recording filter, `FindZoneRef`
missing exempt creatures, ties, plain bows, the sweep position, and tests that can't run without DATs. I checked the
lines behind the top findings, and the NPC filter against Tou Tou v11 data.

Not verified: whether `ApplyZoneDropSweep` can run in the test project (assumed not), and how the elements are really
distributed across the weapon tables (the math assumes all seven are equal).

## Not in this change

- Changing rare Slayers or Bags of Vengeance.
- Changing normal drops' elements.
- Any bag use on rares.
- Persisting the pool across restarts.
