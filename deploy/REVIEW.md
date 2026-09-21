# Pre-production review: `feature/pet-breeding-motel`

Reviewed at `4312ce4b8`, 119 commits ahead of `master`, 99 files, ~30k insertions.
Scope: will this corrupt data, break existing content, or hurt performance on a live shard.

**Verdict: one blocker, and it is configuration rather than code.** The breeding system is
carefully built - the maths is pure and unit-tested, the guardian cannot produce a double
birth, the trace is properly guarded, and the SQL does not touch a single row it did not
create.

The creature appearance rewrite (B1) was the thing worth worrying about, and it has now been
measured end to end against the live world database and the real DAT files: it changes the
look of **298 weenies / 996 placed instances, about 1.7% of the candidate set**, all of them
named below, none of them common trash and none of them players. Cosmetic only. That is a
look-and-confirm item, not a blocker.

What is left is B2, and it is configuration: the breeding area is stored as landblock **364**
(the Marketplace) while the portal delivers players to **314** (the motel), and `pet_trace` is
live on the shard right now. Both are one command each. (An earlier revision of this review
called the bond gate a blocker - that was wrong, see B2a.)

---

## What I verified myself

Stated up front so you know which claims are checked and which are read.

| Claim | How |
|---|---|
| `dotnet build ... -c Release -p:Platform=x64` compiles clean | Ran it. 0 `error CS`. 159 warnings, all pre-existing categories. |
| Tests: 284 pass / 8 skip / 9 fail | Ran it. The 9 failures are **exactly** your baseline list. No regressions. |
| Committed web bundle matches `ClientApp` source | Rebuilt it and compared. Byte-identical after stripping CR. |
| All SQL is 7-bit ASCII, LF, no BOM | Byte-scanned all 7 files. Clean. |
| No new non-ASCII reaches the AC client | Scanned every string literal **added** by the branch. All 163 non-ASCII hits are in the web portal JS bundle, which renders in a browser. Zero in C# client-facing strings. |
| No broad or unscoped SQL DELETEs | Audited all 103 DELETEs and 15 UPDATEs in the consolidated file. Every one is scoped to a WCID this branch owns. |
| WCIDs are collision-free | Cross-checked every created WCID against all other SQL in the repo. Only hit is the known duplicate file (F17). |
| The `78790000`-`78799999` block is untouched | Grepped. Nothing on this branch writes into it. Your template-export tool's reservation is safe. |

I could not run the server or a database - see **What is still on you** at the end.

---

## BLOCKERS

> **B1 was downgraded to MEDIUM after measuring it against the world DB and the DAT files.**
> It is kept here, in full, because the investigation is the evidence. **B2 is the only
> remaining blocker, and it is configuration, not code.**

### B1 (now MEDIUM). The creature appearance rewrite runs for every unequipped creature with a ClothingBase

`Source/ACE.Server/WorldObjects/Creature_Networking.cs:253`

This is the change you flagged, and you were right to. It is the highest-risk thing on the
branch by a wide margin.

**What changed.** Previously, a creature with nothing equipped, no biota appearance rows and
a `ClothingBase` returned `base.CalculateObjDesc()` and stopped. Now it falls into a new
inline block that rebuilds the clothing effects and then **falls through** to the rest of the
method. Four behaviour changes come out of that, and all four apply to retail content:

1. **Naked body parts are now added** (`Creature_Networking.cs:362`). `base.CalculateObjDesc()`
   never did this. Any creature whose ClothingBase covers only some parts now gets the
   remaining parts appended from `baseSetup.Parts`.
2. **`PaletteID` is now forced to the setup's `DefaultPaletteId`** when it would otherwise be
   0 (`Creature_Networking.cs:268`). Creatures without a `PaletteBaseDID` previously rendered
   with `PaletteID = 0`, which the client discards - it drew the model default. They now get
   a real base palette, so the subpalettes below actually take effect. The code comment says
   this is deliberate; the consequence is that **creatures that used to render in model
   default colours now render recoloured**.
3. **Subpalettes are applied even with no `Shade` and no `PaletteTemplate`.** `base.CalculateObjDesc()`
   gated this on `item.ClothingSubPalEffects.Count > 0 && (Shade.HasValue || PaletteTemplate.HasValue)`.
   The new block has no such gate.
4. **The shade default changed from `0` to `0.5`** (`Creature_Networking.cs:303`). For a
   creature with a `PaletteTemplate` but no `Shade`, `GetPaletteID(0)` and `GetPaletteID(0.5)`
   can return different palettes, so the creature changes colour.

There is also a hard-coded special case at `Creature_Networking.cs:328` - when the first
subpalette range starts at offset 320, an extra `(offset 40, length 40)` range is injected,
commented as an Olthoi-legs fix. It fires for *any* creature whose clothing table happens to
start there, not only olthoi.

**Measured against `ace_world` (read-only queries, 2026-09-19).** The blast radius is much
narrower than "the whole bestiary", but it is not nothing.

| Population | Count |
|---|---|
| Creature-type weenies (`Creature`, `Cow`, `Vendor`, `Pet`, `CombatPet`) | 11,467 |
| ... with no `ClothingBase` - path unchanged | 5,987 |
| ... taking the biota early return - path unchanged | 21 |
| **... reaching the rewritten block** | **5,480** |

Of those 5,480, most of the four deltas turn out to be inert on real data:

| Delta | Weenies actually affected |
|---|---|
| `PaletteID` forced to setup default (fires when `PaletteBase` is absent) | **313** - see the correction below |
| The two magic `PaletteID` overrides (`0x040002AB`, `0x0400007E`) | **0** |
| The `0x04xxxxxx` full-palette branch | **0** (no retail creature uses one) |
| Has `PaletteTemplate` **and** `Shade` - same palette picked as before | 4,056 |
| Has `Shade`, no `PaletteTemplate` - same as before | 81 |
| **Bucket B: has `PaletteTemplate`, no `Shade`** - shade default moved `0` -> `0.5` | **1,076** |
| **Bucket A: neither** - old applied *no* subpalettes, new applies them at shade 0.5 | **267** |

> **Correction to the table above.** The `PaletteID` row first read **6**. That query tested
> `weenie_properties_d_i_d type = 2`, which is MotionTable - `PropertyDataId.PaletteBase` is
> **6**. Re-run correctly, **313 creatures / 1,000 placed instances** have no PaletteBase and so
> had their base palette forced. This was confirmed in-game: the Undead Custodian's packet showed
> `PaletteID=0x04000742` where master sends `0`, and its robe lost the brown trim. Fixed by
> scoping the override to the `0x04` pet-palette case; the fix also cut the "gains subpalettes"
> group from 267 to 106, because a creature with no PaletteBase now sends an unresolvable base
> and the client falls back to the model's own colours, exactly as on master.

### Resolved against the real DAT files

Those 1,343 were then run through `client_portal.dat` (79,694 files, `c:\ACE\Dats`) with
ACE.DatLoader, reproducing both the old and the new selection logic per weenie.

`PaletteSet.GetPaletteID(hue)` is `palIndex = (int)((Count - 0.000001) * hue)`. At hue `0` that
is always index 0; at hue `0.5` it is still index 0 for a PaletteSet of **1 or 2** entries, and
only reaches index 1+ at **3 or more**. So the shade change can only matter for multi-entry
PaletteSets - and most creature PaletteSets are not.

| Verdict | Weenies | Placed instances |
|---|---|---|
| Bucket B, **same** palette picked - unchanged | 968 | 5,970 |
| No SubPalEffects in the table - unchanged | 64 | 9 |
| No readable ClothingTable - old path, unchanged | 13 | 3 |
| **Bucket A: gains subpalettes it never had** | 199 | 666 |
| **`PaletteTemplate & 0xFFFF` used as a literal subpalette id** | 71 | 230 |
| **Bucket B: genuinely different palette picked** | 28 | 100 |
| **TOTAL ACTUALLY CHANGED** | **298** | **996** |

**996 placed instances out of the 59,196 in the candidate set - about 1.7%.** And the Tusker
family, Assailer, Devastator, Rampager and Olthoi Grub - the common trash I was most worried
about - all land in the 968 "unchanged" row. Their PaletteSets have one or two entries, so
shade 0 and shade 0.5 pick the same palette.

**Everything that actually changes, by name:**

- **Gains subpalettes it never had** (had neither Shade nor PaletteTemplate; the old guard
  applied *no* subpalettes at all): Master Soldier (38845/38846/38847, 56+56+55 placements),
  Spectral Archer (72593, 27), Master Mage (38842-38844, 22 each), Spectral Minion
  (72589/72590, 21+20), Master Archer (38839-38841, 20 each), Guardian Statue (72270, 20),
  Corpse of Pon Mi (80094, 18), Soldier (72870, 15), Spectral Blade Master (46570).
- **`PaletteTemplate & 0xFFFF` as a literal id**: Undead Librarian (2001006, **105**),
  Mitey Knight (2001011, 55), Noodle (694200049, 31), Obliterator (22903, 7), Huge Sand Worm,
  Winternadir, Hea Trelye, a few Operations Specialists/Aids.
- **Different palette picked**: Undead Custodian (2001007, 46), Ensnared Soul (47049, 33),
  Spectral Blade Adept (46569), Y'z, Kaltus, Exarch Nanjou Shou-jen, Janus Roma the Shopkeep,
  Fayza, and a cluster of one-off `64454xxx` NPCs.

**The one that looks like a real bug, not a nuance.** The 71 weenies in the
`PaletteTemplate & 0xFFFF` group take this branch (`Creature_Networking.cs:308-311`):

```csharp
if (palOption > 0 && !creatureCloTable.ClothingSubPalEffects.ContainsKey((uint)palOption))
    itemPal = (ushort)(palOption & 0xFFFF);
```

When a creature's `PaletteTemplate` is a small `PaletteTemplate` enum value (2, 8, 22 ...) that
is not a key in its clothing table, the new code uses that number **as a subpalette id**. A
`PaletteTemplate` of 22 becomes subpalette 22, i.e. palette `0x04000016` - an unrelated palette
picked by numeric coincidence. The old code fell back to the table's first SubPalEffect and
read a real PaletteSet. This is the group most likely to look visibly wrong, and Undead
Librarian at 105 placements is the most-placed changed weenie on the branch.

### CONFIRMED IN-GAME, and it also hits player equipment

Shard owner checked `@create 2001006` (Undead Librarian): **its robe lost its colour palette.**
`@create 38845` (Master Soldier) looked correct. That confirms the `PaletteTemplate & 0xFFFF`
branch is a real defect and that the "gains subpalettes" group renders acceptably.

**Mechanism.** `WorldObject_Networking.cs:244` writes each subpalette with
`WritePackedDwordOfKnownType(palette.SubPaletteId, 0x4000000)`, so a raw ordinal `N` reaches
the client as palette `0x040000NN`. The Undead Librarian's `PaletteTemplate` is an ordinal, not
a palette id, so the client is handed an unrelated palette and the robe's colour block is lost.

**The same branch exists on the EQUIPPED-ITEM path** (`Creature_Networking.cs:222-236`), which
was not part of the original creature analysis. Measured the same way:

| Armor/Clothing weenies with ClothingBase + PaletteTemplate | 2,269 |
|---|---|
| `PaletteTemplate` IS a key in the table - unchanged | 2,165 |
| No readable ClothingTable - unchanged | 62 |
| `PaletteTemplate` is `0x04xxxxxx` | 0 |
| `PaletteTemplate` NOT a key, **but the fallback effect has ZERO CloSubPalettes** - unchanged | 21 |
| **GENUINELY AFFECTED** | **21** |

> **CORRECTION to an earlier revision of this review.** A first pass reported 42 affected items
> and 71 affected creatures. That over-counted. The raw-ordinal branch lives *inside*
> `for (int i = 0; i < itemSubPal.CloSubPalettes.Count; i++)`, so an object is only affected
> when the effect actually in use has at least one `CloSubPalette`. Half the items and five of
> the creatures have a fallback effect with **zero**, meaning no subpalette is written at all -
> on master or on the branch - so they are untouched.
>
> **The Gelidite / Leikotha's Tears set is in that unaffected group** and should be removed from
> the affected list. Its clothing table `0x100005EF` has `ClothingSubPalEffects` key `[1]` whose
> effect has zero CloSubPalettes. The shard owner observing that its pieces "all look the same"
> is them rendering normally, not a symptom.

**Corrected totals: 66 creatures (225 placed instances) + 21 equippable items = 87 objects.**

The affected items, ordered by how much of the body they repaint (`parts` = `CloObjectEffects`
on the HumanMale setup - a 17-part effect replaces the entire body model):

| WCID | Name | Parts | master palette | current palette |
|---|---|---|---|---|
| 87188 | Borelean Jumpsuit | 17 | `0x0400197E` | `0x04000014` |
| 290500191 | Jester's PJ's | 17 | *(none - empty PaletteSet)* | `0x0400002E` |
| 290500317 / 696900215 | Island Mattekar Robe | 14 | `0x04000EDC` | `0x04000064` |
| 3000000001 | Hoory Mattekar Over-robe | 14 | `0x040005F2` | `0x04000064` |
| 99258429 | Plaguefang's Robe | 14 | `0x04001712` | `0x04000068` |
| **55790801** | **Flame Coat** | **7** | `0x040014BE` | `0x04000045` |
| 227190179 | Advanced Academy Coat | 6 | `0x04001083` | `0x04000064` |
| 3110177, 227199990, 227198888, 93000001, 420559, 290500620 | masks / helms | 1 | varies | varies |
| 5579199, 3998210, 290500501, 694200217, 420558, 290500618, 227190087 | flag / shirts / shields | 0 | varies | varies |

**Confirmed in-game by the shard owner:** equipping the **Flame Coat (55790801)** "changed the
colour of my entire character". That is this defect at its most visible - the coat's clothing
base replaces **7 body parts** (chest, abdomen, both upper and lower arms), so painting them
with `0x04000045` instead of `0x040014BE` recolours the whole torso and arms. The two 17-part
items would repaint the entire body model.

Severity is still cosmetic - no data, combat or economy impact - but "cosmetic" here can mean
a player's whole character changing colour, not a subtle tint.

### Recommended fix

Delete the raw-ordinal fallback in both places and let it fall through to the PaletteSet read,
which is what master did. Keep the `0x04xxxxxx` branch - that is the legitimate new capability
for pet palettes, and it matches 0 retail creatures and 0 retail items.

```csharp
// Creature_Networking.cs:227-230 (equipped items) and :308-311 (creatures) - REMOVE:
else if (palOption > 0 && !item.ClothingSubPalEffects.ContainsKey((uint)palOption))
{
    itemPal = (ushort)(palOption & 0xFFFF);
}
```

Optionally also restore `shade = Shade ?? 0.0f` at `Creature_Networking.cs:303` to match the
old default; that covers the remaining 28 creatures in the "different palette picked" group
(Undead Custodian 2001007, Ensnared Soul 47049). Worth checking 2001007 in-game first - if it
looks right, leave the 0.5 default alone.

**Revised severity: MEDIUM, with one confirmed defect to fix.** Cosmetic only - no data,
combat or economy impact - but it is a real regression against master affecting 66 creatures
and 21 equippable items (87 objects), and it is confirmed in-game. The fix above is four lines.

**What to do.** Spawn or visit **Undead Librarian (2001006)**, **Master Soldier (38845)** and
**Undead Custodian (2001007)** - one from each changed group. If they look right, close this
out. If they do not, the targeted fix is to drop the raw-id branch at
`Creature_Networking.cs:308-311` so an unmatched `PaletteTemplate` falls back to the table's
first SubPalEffect, and to restore `shade = Shade ?? 0.0f` at `Creature_Networking.cs:303` to
match the old default. Neither touches the pet recolour, which is served by the separate
`ApplyPaletteTemplateOverride` call on the biota path at `Creature_Networking.cs:143`.

**Correction to an earlier draft of this review:** I hypothesised that the biota early return
would catch most retail creatures, because `WeenieConverter.ConvertToBiota`
(`ACE.Entity/Adapter/WeenieConverter.cs:49-70`) copies `weenie_properties_palette`,
`_anim_part` and `_texture_map` into every runtime biota. The data says otherwise - only 21
creature weenies in your world carry any of those rows. The early return is essentially a
captured/bred-pet path, not a retail one.

**Good news on the related path:** `ApplyPaletteTemplateOverride` itself is safe. Players hit
it (their biota carries palette rows) but exit on the first line because `PaletteTemplate` is
null or a small enum value. Players are not at risk from that call.

---

### B2. Your stored config makes breeding either impossible or unbounded

Not a code defect - `PropertyManager.cs` - but it will decide whether launch day works.

**B2a. WITHDRAWN - this was wrong.** An earlier revision called the bond gate a launch blocker:
`pet_bond_enabled` defaults to `false` in code and `pet_breeding_min_bond` to 100, so on paper no
essence can ever reach the gate. **That reasoning used the code defaults without reading the
shard's stored values**, which is precisely the mistake CLAUDE.md warns about. Queried directly
(2026-09-20), this shard already stores `pet_bond_enabled = true` and `pet_breeding_min_bond = 1`.
Breeding is not blocked by bond. Changing the code default would not affect this shard either way,
since stored values win; it is worth doing only for fresh installs.

**B2a (replacement). The breeding area is set to the wrong landblock.** Stored values on this
shard:

| Property | Stored | Should be |
|---|---|---|
| `pet_breeding_allowed_landblock` | **364** = `0x016C`, the **Marketplace** | **314** = `0x013A`, the motel |
| `pet_breeding_allowed_variant` | *not stored* - code default 3 | 3, set explicitly |
| `pet_trace` | **true** | **false** |
| `pet_breeding_force_mutation` | **true** | false |

The portal delivers players to `0x013A` variant 3, but the ritual area is configured as the
Marketplace, so a breed cannot happen where the content is. `IsInMotelOrEncounter()` also returns
true there, which switches on own-pet beneficial-spell targeting and the up-to-8x heal scaling in
a public town.

`pet_trace = true` is the performance one: one log line per swing, cast, DoT tick, heal and death
for every player online, live right now.

**B2b. Nothing caps mutation growth - accepted as a design choice.** `pet_breeding_max_stat_mutations`
defaults to `0`, which the code reads as *uncapped* (`PetDevice_Breeding.cs:160`).
`pet_breeding_potency_hard_cap` and `pet_potency_max_stored` both default to `0`, and
`ResolvePotencyHardCap(0, 0)` returns 0, which `PotencyMutationStep` treats as no cap
(`PetDevice_Breeding.cs:479-503`). The shard owner has accepted uncapped linear growth - the
server's premise is infinite progression. Two things make that reasonable rather than reckless:

**It decelerates on its own, then goes flat.** `StatMutationChance`
(`PetDevice_Breeding.cs:432-436`) is
`clamp(max(minFloor, base / (1 + decay * inheritedCounts)) + incense, 0, 1)`. With base `0.05`,
decay `0.35` and floor `0.02`, the decayed term crosses the floor at about **4 inherited stat
mutations**, and from there the chance is pinned at a flat **2% per breed** forever - roughly
one mutation per 50 breeds. Unbounded, but genuinely slow, and the female's 4-hour cooldown is
the real rate limiter. **Keep decay at your 0.35; the code default of `0.0` removes the
deceleration entirely.**

**Incense becomes the entire late game.** Past the floor, two Exquisite Incense (0.10 each,
summed and clamped at 0.5) take the chance from 2% to **22%** - an 11x multiplier. Once a
lineage is a few mutations deep, incense is not a bonus, it is the only meaningful driver.
That is a strong pyreal sink at 2.5M each and looks intentional, but it does mean late-game
breeding economics are set by incense pricing, not by the base chance.

**On changing your mind later - the important asymmetry.** Mostly yes, but not uniformly:

| Lever | Retroactive? | Effect of changing it later |
|---|---|---|
| `pet_breeding_max_stat_mutations` | **No** | Checked only at breed time via `EligibleStatLines`. Adding a cap freezes further growth; pets already above it keep everything they have. **Safe to add later.** |
| `pet_breeding_potency_hard_cap` | **No** | `PotencyMutationStep` clamps to `max(0, hardCap - stored)`, so stored potency above a new cap simply stops growing. **Safe to add later.** |
| `pet_breeding_base_mutation_chance`, `_decay_rate`, `_min_floor` | **No** | Breed-time only. Changes future rolls, touches nothing already bred. **Safe.** |
| **the four stat steps** (`damage`, `dr`, `crit`, `vitality`) | **YES** | `CombatPet.Init:418-426` reads the step from `ServerConfig` **at summon time** and computes `mutDmg = count * step`. Lowering a step silently nerfs **every pet anyone has ever bred**, retroactively, on its next summon. |
| `pet_breeding_potency_mutation_step` | **No** | Potency is stored as a finished value, so its step only affects future mutations. |

So the lever to reach for if power creep ever needs reining in is **the cap, never the step**.
The stat steps are the one setting here that rewrites history.

**B2c. Test switches are live.** You told me `force_mutation` is `true` on your shard, with
base 0.2 and potency chance 0.5. Every breed currently mutates. RUNBOOK 7a and 7d.

**B2d. A `pet_breeding_allowed_landblock` of `0` means *everywhere*.** `MatchesBreedingArea`
returns `locValid = true` unconditionally when the allowed landblock is 0
(`PetDevice_Breeding.cs:27`). That is not only a breeding gate: `IsInMotelOrEncounter()` also
unlocks own-pet beneficial-spell targeting (`Player_Magic.cs:620, 656`) and up to **8x heal
scaling** (`WorldObject_Magic.cs:589`). A stored `0`, or your suspected `364` (Marketplace),
applies those rules well outside the motel. Verify it reads 314 / variant 3.

---

## HIGH

### H1. The pet rename can write a detached biota under a live, online object

`Source/ACE.Server/Controllers/PetNamingController.cs:322`

`RenameOnline` searches only `MyInventory | MyEquippedItems`. If the device is not found
there - it is in a chest, on the ground, mid-trade, or in a container the search does not
cover - it calls `RenameOffline(owner.Guid.Full, ...)` **for a player who is online**.
`RenameOffline` then does `GetBiota(skipCache: true)`, mutates the result and `SaveBiota`s it.

That reads a possibly-stale DB copy of an object the server currently has loaded, and writes
it back wholesale. Any property changed in memory since the last save is reverted, and the
live object is unaware. This is a narrow path but it is a genuine data-corruption route.

**Fix:** in `RenameOnline`, when the device is not in the owner's possession, return
`RenameResult.Mismatch("the device is not in the owner's possession")` and let the request
auto-deny. Do not fall through to the biota path while the owner is online.

### H2. A server restart during a guardian fight destroys a paid breed

`Source/ACE.Server/WorldObjects/PetDevice_Breeding.cs:644`

`pendingGuardianBreeds` is an in-process `ConcurrentDictionary`. By the time the guardian
spawns, the male's charge is spent, the female's cooldown is written, and the incense and
Offering are consumed. A restart drops the dictionary: both parents have paid and no baby is
ever created.

The developer guide documents this as known and unhandled, which is honest, but with the
guardian enabled it applies to **every mutated breed in flight** at shutdown. With a 90 second
timeout the window is small per breed but it is not rare across a shard.

**Cheapest mitigation, no code change:** make `@shutdown` announcements long enough (5+
minutes) that in-flight fights resolve, and never hard-kill the process with players in the
motel. **Proper fix, post-launch:** on shutdown, walk `pendingGuardianBreeds` and call
`CompleteBirth` for each entry (the timeout path already does exactly this).

I did confirm the related race is safe: `OnGuardianSlain`, `OnGuardianTimeout` and
`OnGuardianLost` all gate on `pendingGuardianBreeds.TryRemove(...)`
(`PetDevice_Breeding.cs:1541, 1621, 1648`), so **a double birth is not possible** even though
the three callbacks arrive on different threads.

---

## MEDIUM

### M1. Startup prewarm costs 40-80 MB that is never released
`Source/ACE.Server/Services/PetMutationService.cs:122` loops `0x04000001`..`0x04002500` -
9,472 `ReadFromDat<Palette>` calls at startup. `DatDatabase.ReadFromDat` caches every result
permanently in an unbounded `FileCache` (`DatDatabase.cs:145`), and a creature palette is 2048
colours. Given this repo already carries `LANDBLOCK_MEMORY_ANALYSIS.md` and
`MEMORY_OPTIMIZATION_SUMMARY.md`, that is worth knowing about before you measure it in anger.
It also adds real time to every startup. Not a blocker; measure it on first boot.

### M2. Portal icons without a UI effect changed appearance
`Source/ACE.Server/Services/IconService.cs:299`. The white-mask removal loop used to run for
**every** texture; it is now wrapped in `if (uiEffects.HasValue && uiEffects.Value != 0)`.
Icons with no UI effect keep opaque white where they previously became transparent. Portal
only - the game client is unaffected. Eyeball a few ordinary item icons (RUNBOOK 9.3).

### M3. Re-running the NPC section alone silently empties Ivo's shop
Section 2 of the consolidated file runs
`DELETE FROM weenie_properties_create_list WHERE object_Id = 78780201 AND destination_Type = 4`
(line 353 of the generated world SQL) and rebuilds the list with only the two kits. Sections 3
and 4 add the consumables afterwards. **Re-run the whole file, never one section.** I have put
this in the file header and in RUNBOOK step 5, and verification query V2 catches it (expects 9
rows).

### M4. The world SQL does not place any of the 15 NPCs
By design - the original patch explains there is no safe way to choose walkable coordinates
from SQL - but it means the deploy is not "run SQL and done". Until you run 15 `@createinst`
commands, Ivo does not exist in the world and nothing is purchasable. RUNBOOK step 8, with the
two mechanical placement rules (wings out of the ritual landcell; Bexley/Splotch/Fenwick within
60 m).

### M5. `RenameOffline` does synchronous database I/O on the world thread
Reached from `RenameOnline` (H1) via `WorldManager.ActionQueue`, which drains on the main
world thread immediately before landblock ticks (`WorldManager.cs:446`). A `GetBiota` +
`SaveBiota` round trip there stalls every landblock for its duration. Fixing H1 removes this
path entirely.

### M6. Unauthenticated portal endpoints do DAT reads and image encoding
`VisualizerController` has **no class-level `[Authorize]`** - the class is only `[ApiController]`
+ `[Route]` - and 19 of its 21 endpoints are `[AllowAnonymous]`. Several (`mesh/{wcid}.gltf`,
`texture/{id}.png`, `palette/{id}.png`, `texture-library`, `search-creatures`) do real DAT work
and image encoding in the game server process, with no rate limiting. If the portal is
reachable from the internet this is an unauthenticated CPU amplifier pointed at your shard.
The two write endpoints are correctly `[Authorize]` + `CanWrite()`, and `SaveScreenshot`
(`VisualizerController.cs:301`) is genuinely well hardened - PNG signature check, size cap,
file-count cap, filename sanitising and a path-traversal check. The read side is the gap.
Mitigation without code: keep the portal behind auth at the reverse proxy, or off the public
internet.

---

## LOW

| # | Finding | Location | Fix |
|---|---|---|---|
| L1 | Two files carry a UTF-8 BOM, against the project rule. `PetTailoring.cs` is new with one; `DeveloperContentCommands.cs` **had none on master and gained one on this branch**. (`AccessLevel.cs` correctly *lost* its BOM.) | `Entity/PetTailoring.cs:1`, `Command/Handlers/DeveloperContentCommands.cs:1` | Strip both BOMs. |
| L2 | `AccessLevel.User = Player` is a duplicate-value enum alias. Safe for ordinals, but `Enum.GetName`/`ToString()` on value 0 may now return `"User"` instead of `"Player"` - check nothing persists or parses the name. | `ACE.Entity/Enum/AccessLevel.cs:13` | Verify no round-trip through the string form; otherwise leave. |
| L3 | A fresh `ReaderWriterLockSlim` is allocated per rename, never disposed, and is not the lock that actually guards that biota - so it provides no mutual exclusion. | `Controllers/PetNamingController.cs:374` | Goes away with the H1 fix. |
| L4 | `CreditMaturityKills` now runs on **every** creature death server-wide. It early-returns when `pet_maturity_enabled` is false and is wrapped in try/catch, but with maturity on it walks `DamageHistory` and resolves each attacker by GUID. | `WorldObjects/Creature_Death.cs:84` | Fine; just know it is a new per-death cost. |
| L5 | `Content/sql/weenies/98760399-98760401 Pet Tailoring and Neutering Kits.sql` is byte-identical to the `Database/Updates/World` copy. Two files to keep in sync. | - | Delete one, or note which is canonical. |
| L6 | `.gitattributes` has `* text=auto` with no binary rule for the committed web bundle, so `wwwroot/assets/*.js` churns CRLF/LF on every checkout. Harmless but noisy (it is why the bundle "differs" from `dist`). | `.gitattributes:1` | Add `Source/ACE.Server/wwwroot/** -text`. |
| L6b | `VisualizerService.MergeCustomClothingBaseJson` reads clothing overrides from two **hardcoded absolute paths**, `C:\ACE\Mods\CustomClothingBase\json\` (406 files present on this machine) and `C:\Scripting\CustomClothingBase\json\`. Machine-specific; breaks on any other host and on Docker. Note it affects **only the web 3D showroom** - the game server's render path does not read these, so it cannot explain an in-game appearance difference. | `Services/VisualizerService.cs:707-710` | Move to a config value or a path relative to `DatFilesDirectory`. |
| L7 | Pre-existing, not this branch: `PropertyManager.cs` lines 454, 455, 475, 476, 502, 509, 519, 902, 948, 951 contain em-dashes, `x`-signs and ellipses in config descriptions, which `@showprops` renders **in the AC client**. | `Managers/PropertyManager.cs` | Optional cleanup; flagging because it violates the same rule the branch otherwise respects. |
| L8 | `DamageEvent` gained 8 fields (~40 bytes) allocated per swing for trace capture, even when tracing is off. | `Entity/DamageEvent.cs:168-187` | Negligible; noted for completeness. |

---

## Checked and clean

These were on your list of concerns. I looked and found nothing to fix.

**`DamageEvent` roll hoisting is behaviour-neutral.** `ThreadSafeRandom.Next(float, float)`
returns a **`double`** (`ACE.Common/ThreadSafeRandom.cs:31`), so the original comparisons were
already `float > double`. Storing the draw in a `double` field first and comparing against that
is exactly the same comparison - widening a float to double is exact, so `(double)a > (double)b`
iff `a > b`. `SchemeCRoll` keeps the original `(float)` cast in the same position
(`DamageEvent.cs:432`). No boundary case moves. The comment in the source explaining the double
choice is correct.

**`pet_trace` is genuinely free when off.** `PetTrace.Enabled` is
`ServerConfig.pet_trace.Value`, and `ConfigProperty<T>.Value` is a field read
(`PropertyManager.cs:45`), not a dictionary lookup. I checked all 63 non-`Enabled` call sites:
every one is behind `PetTrace.Enabled`, a local `var trace = PetTrace.Enabled`
(`PetDevice_Breeding.cs:678`), a `tr != null` from `Trace = PetTrace.Enabled ? new ... : null`
(`SpellProjectile.cs:411`), or `traceParts?.Add(...)` where C# null-conditional short-circuits
argument evaluation (`EnchantmentManager.cs:1798`). **No string is built before the guard.**
Default is `false`. The one intentional exception is `@pet-dump`, which always writes.

**The threading model is correct.** `WorldManager.ActionQueue.RunActions()` runs on the main
world thread and completes *before* `UpdateGameWorld()` ticks any landblock
(`WorldManager.cs:446-454`). So work queued there cannot race a landblock thread. Every
cross-object path on this branch - guardian timeout, deferred dismissal, the rename - uses it.
The pattern is right; H1/M5 are about what is *done* inside it, not where.

**Baby delivery cannot duplicate or lose the item.** The deferred path
(`PetDevice_Breeding.cs:1866-1879`) writes `ContainerId`/`OwnerId` to the winner and saves,
which is what the login inventory load expects; the baby is never dropped in the world. Winner
liveness is resolved through `PlayerManager.GetOnlinePlayer` rather than the captured `Player`
reference, which is the right call since `Session` is never nulled on logout
(`PetDevice_Breeding.cs:1812`). One object, one save, no branch that can create two.

**Consumables follow the transaction-first rule.** Every one of the seven checks its refusal
conditions, then `TryConsumeFromInventoryWithNetworking`, then writes the property
(`Player_Use.cs:176-500`). A trade-window guard already covers them generically at
`Player_Use.cs:125-133`, so the asymmetry I initially suspected against tailoring's explicit
check is not real - they are all protected.

**`CanBeDamagedBy` is wired into all three combat pipelines** as AGENTS.md rule 3 requires:
melee/missile via `Player_Combat.cs:360`, projectiles via `SpellProjectile.cs`, life magic via
`WorldObject_Magic.cs:625`. The base implementation returns `true`, so retail combat is
untouched.

**The SQL is safe.** 103 DELETEs and 15 UPDATEs, every one scoped to a WCID this branch owns.
Three `landblock_instance` touches total, one of which is a comment. No generated `landblock`
column is ever named in an INSERT. No range DELETEs. Nothing can reach retail content.

---

## What is still on you

I had no running server and no database, so these could not be checked from source:

1. **B1 - the appearance regression.** The single most important item. Needs eyes on real
   creatures in a real client. Nothing in the source tells you how many creature weenies have a
   `ClothingBase` the DAT accepts; only a query against your world DB or a walk around the
   world will.
2. **A real breed.** Two players, two opposite-sex essences past the level and bond gates, in
   one landcell. Every gate, the partner scan, the dance-sync window and the birth are
   untestable single-handed.
3. **The mating guardian fight.** Spawn placement, the damage gate, targeting, and all three
   resolutions (slain / timeout / lost). The timeout and lost paths in particular have never
   run under real conditions.
4. **Ritual-room landcell size.** RUNBOOK 8.3. If the room spans more than one landcell,
   breeding fails silently for players standing apart. Only `@breed-debug` in the actual room
   tells you.
5. **Load.** You said nothing on this branch has been load-tested. The per-death
   `CreditMaturityKills` (L4), the palette cache (M1) and the partner scan - which walks every
   online player, rate-limited to one scan per player per 2 seconds - are all fine in a
   handful-of-players test and unmeasured at population.
6. **Vendor pricing in the client.** V3 proves `AlternateCurrency` is gone from the database;
   only the client shows you what a player actually sees.
7. **Whether the portal is internet-facing** (M6), and whether your reverse proxy already
   requires auth.
8. **The template-export tool** (`@et` / `@export-template`, the 7879 block) is not on this
   branch and was not reviewed. I confirmed nothing here writes into `78790000`-`78799999`, so
   the reservation is intact and the two can be reviewed separately.

---

## Suggested order of work

1. Fix the `PaletteTemplate & 0xFFFF` branch (4 lines, two call sites) - confirmed in-game,
   affects 66 creatures and 21 equippable items (87 objects). Then H1 (a few
   lines, removes M5 too) and L1 (strip two BOMs). No code has been changed yet.
2. B2 is decided: bond on, gate 100, growth uncapped, decay stays at 0.35. Only B2c is left -
   turn the test switches off (RUNBOOK 7a).
3. Check Undead Custodian (2001007) to decide whether the shade 0 -> 0.5 default also needs
   reverting. Undead Librarian and Master Soldier are already checked.
4. If B1 is clean, run the RUNBOOK and ship. If not, fix it and re-check.
5. Post-launch: H2 (drain pending breeds on shutdown), M6 (portal auth), M1 (measure).
