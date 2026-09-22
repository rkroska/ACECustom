# Test plan for the three proposed fixes

Written **before** any code change, at `e4b5423d2`. Nothing has been modified yet.
Rollback for all of it is `git reset --hard e4b5423d2`.

Three independent changes. They touch different subsystems and can be applied, tested and
reverted separately - if you want to stage them, do A first, since it is the one you have
already seen fail in-game.

| | Change | Files | Lines | Risk |
|---|---|---|---|---|
| **A** | Remove the raw-ordinal palette branch | `Creature_Networking.cs` | ~8 | Low, bounded |
| **B** | Stop the pet rename writing a detached biota | `PetNamingController.cs` | ~4 | Low |
| **C** | Strip two UTF-8 BOMs | `PetTailoring.cs`, `DeveloperContentCommands.cs` | 0 (3 bytes each) | None |

Commands used below: `@create <wcid>` spawns in the world, `@ci <wcid>` puts one in your
inventory. Both are Developer access.

---

## Change A - remove the raw-ordinal palette branch

### What changes

Two call sites in `Source/ACE.Server/WorldObjects/Creature_Networking.cs`, both identical:

```csharp
// :227  equipped-item loop        and        :308  creature loop
else if (palOption > 0 && !item.ClothingSubPalEffects.ContainsKey((uint)palOption))
{
    itemPal = (ushort)(palOption & 0xFFFF);      // <- DELETE both blocks
}
```

Control then falls to the existing `else`, which reads the real `PaletteSet` and calls
`GetPaletteID(shade)` - what `master` does.

The `(palOption & 0xFF000000) == 0x04000000` branch immediately above **stays**. That is the
pet mutation palette path.

### What it can affect - and what it structurally cannot

The deleted branch only executes when `PaletteTemplate` is **not** a key in that object's
clothing table. Anything whose `PaletteTemplate` *is* a key never enters it. That is a property
of the control flow, not a sample.

| | Count | After the fix |
|---|---|---|
| Creatures on the raw-ordinal branch | 71 | 64 match master exactly; 7 still differ (shade 0.5, a separate issue) |
| Equipped items on the raw-ordinal branch | 21 | 21 match master exactly |
| Equipped items where `PaletteTemplate` IS a key | 2,165 | untouched |
| Everything else | - | untouched |

Total blast radius: **87 objects that currently render wrong - 66 creatures and 21 equippable
items.** 71 creatures enter the raw-ordinal branch, but 5 of them have a fallback effect with zero
CloSubPalettes, so no subpalette is written for them on master or the branch (the since-deleted REVIEW.md, the
correction under the equipped-item table).

### Automated coverage

**There is none.** No unit test exercises `CalculateObjDesc`. The build and the 284/8/9 result
confirm nothing broke *elsewhere*; they say nothing about this change. All validation is
in-game.

---

> **These tables are pass/fail only AFTER the fix is applied.** Readings taken on the current
> build are *baselines*. Record what you see now, apply the fix, and compare. (The Gelidite set is
> not affected - see the correction under A5-A7.)

### A1-A4. Creatures on the affected branch - these should get their colour back

```
@create 2001006
@create 2001011
@create 694200049
@create 22903
```

| WCID | Name | Placements | Expected |
|---|---|---|---|
| 2001006 | Undead Librarian | 105 | **Robe has its colour back** - this is the failure you reported |
| 2001011 | Mitey Knight | 55 | Renders correctly |
| 694200049 | Noodle | 31 | Renders correctly |
| 22903 | Obliterator | 7 | Renders correctly |

### A5-A7. The equipped-item half - CORRECTED LIST

> **Correction.** An earlier revision listed the Gelidite / Leikotha's Tears set here. That was
> wrong: its clothing table's fallback effect has **zero** `CloSubPalettes`, and the buggy code
> lives inside the loop over those, so no subpalette is written for it at all - on master or on
> the branch. Gelidite is **unaffected**. Its pieces looking identical to each other is normal
> rendering, not a symptom. The same applies to the Monster Fight Shirt (87153).
>
> True affected count: **21 items**, not 42.

The affected items ranked by how much of the body they repaint. `parts` is the number of
`CloObjectEffects` on the HumanMale setup - a 17-part effect replaces the whole body model,
which is why this can look like the entire character changing colour rather than one garment.

```
@ci 55790801
@ci 290500191
@ci 290500317
@ci 227190179
```

| WCID | Name | Parts | master picks | current picks |
|---|---|---|---|---|
| **55790801** | **Flame Coat** | **7** | `0x040014BE` | `0x04000045` |
| 290500191 | Jester's PJ's | 17 | *(empty PaletteSet - none)* | `0x0400002E` |
| 290500317 | Island Mattekar Robe | 14 | `0x04000EDC` | `0x04000064` |
| 3000000001 | Hoory Mattekar Over-robe | 14 | `0x040005F2` | `0x04000064` |
| 99258429 | Plaguefang's Robe | 14 | `0x04001712` | `0x04000068` |
| 227190179 | Advanced Academy Coat | 6 | `0x04001083` | `0x04000064` |
| 3110177 | Knath Valley Mask | 1 | `0x04001634` | `0x04000055` |
| 93000001 | Virindi Mask | 1 | `0x04001F7A` | `0x04000067` |
| 87188 | Borelean Jumpsuit | 17 | `0x0400197E` | `0x04000014` - only 3 body setups, may be invisible on you |

**55790801 Flame Coat is the primary test.** You have already seen it fail: equipping it
recoloured your entire character. After the fix it should pick `0x040014BE` - master's palette -
and look like an ordinary coat.

Jester's PJ's (290500191) is the second-best check: 17 parts, and master writes **no**
subpalette for it at all because its PaletteSet is empty. Post-fix it should lose the tint
entirely rather than gain a different one.

> **Confirmed in-game, pre-fix, by the shard owner:**
>
> - **55790801 Flame Coat** - recoloured the entire character. Its effect covers 7 body parts.
> - **290500317 Island Mattekar Robe** - changed head and hands, parts the robe does not cover.
>   This is expected and is strong confirmation: the robe uses **three different PaletteSets**
>   and master picks a different palette for each range (`0x04000EDC` over offsets 40-80,
>   `0x040005F1` over 80-92 and 116-128, `0x0400043A` over 96-108). The bug collapses all three
>   to the single palette `0x04000064`. Subpalettes are **palette index ranges, not body parts**,
>   so any part whose texture indexes into offsets 40-128 is repainted - including bare head and
>   hands the robe's model never replaces. The fix restores the three distinct palettes.
>
> **Coverage masks the symptom - this drives test selection.** The shard owner noted that
> Jester's PJ's covers the body head to toe, which is why nothing looked wrong on it. That is
> the general rule: a garment whose effect replaces every body part leaves no untouched skin to
> contrast against, so a wrong palette is invisible. The best detectors are **partial-coverage**
> items, where repainted ranges sit next to bare skin:
>
> | Parts | Examples | Diagnostic value |
> |---|---|---|
> | 17 | Jester's PJ's, Borelean Jumpsuit | **Poor** - full coverage hides it |
> | 6-14 | Flame Coat (7), Advanced Academy Coat (6), Mattekar robes (14) | **Best** - bare head/hands/legs next to repainted ranges |
> | 0-1 | masks, helms, shirts | **Good** - little geometry added, palette lands on the body |
>
> **CAVEAT - 290500191 Jester's PJ's shows nothing either way, and the fix still changes it.**
> That weenie carries `Shade = 1.66`, which is out of range: `PaletteSet.GetPaletteID` returns 0
> for `hue < 0 || hue > 1`. So master picks palette id `0` and writes a subpalette the client
> cannot resolve; the current branch picks `0x0400002E`, a real palette, which is the tint that
> looks right; and **after the fix nothing is written at all**, because the branch also added an
> `if (itemPal != 0)` guard that master does not have. Post-fix it will render like master - untinted
> on wire offsets 160-174, 14 palette slots, which on a full-coverage garment is very likely
> imperceptible for exactly the reason the current wrongness is. It is the **only one of the 21 affected items with an out-of-range Shade**. If the
> current look is preferred, fix the data (set `Shade` within 0..1, or add `46` as a key in the
> clothing table), not the code.

### A5b. Controls - affected-looking but actually unaffected

These *look* like they should be on the buggy branch (`PaletteTemplate` is not a key) but their
fallback effect has zero `CloSubPalettes`, so nothing is written either way. **They must look
exactly the same before and after.**

```
@ci 30519
@ci 87153
```

| WCID | Name | Why |
|---|---|---|
| 30519 / 30514 / 30511 | Gelidite Breastplate / Greaves / Mitre | table `0x100005EF`, key `[1]`, zero CloSubPalettes |
| 87153 | Monster Fight Shirt | table `0x1000075E`, key `[61]`, zero CloSubPalettes |

Your baseline reading of these - all three Gelidite pieces looking the same, and the shirt
looking right - is the expected post-fix result too.

### A8-A10. Regression watch - these must look UNCHANGED

```
@create 1629
@create 11541
@create 22053
@create 10915
@create 38845
```

| WCID | Name | Placements | Why it is here |
|---|---|---|---|
| 1629 | Tusker Guard | 216 | Bucket B, verified *unchanged* by the DAT analysis |
| 11541 | Plated Tusker | 167 | same |
| 235 | Goldenback Tusker | 142 | same |
| 11 | Male Tusker | 134 | same |
| 1627 | Tusker Crimsonback | 134 | same |
| 1628 | Tusker Slave | 172 | same |
| 22053 | Assailer | 322 | most-placed creature in the whole candidate set |
| 22518 | Devastator | 259 | same |
| 10810 | Rampager | 246 | same |
| 10915 | Olthoi Grub | 118 | same |
| **38845** | **Master Soldier** | 56 | **You already checked this and it looked fine.** It is not on the removed branch, so if it moves, the fix reached further than it should. |

**Control armour - `PaletteTemplate` IS a key, so it cannot enter the removed branch:**

```
@ci 2033
@ci 2034
@ci 2037
```

| WCID | Name | `PaletteTemplate` |
|---|---|---|
| 2033 | Featherlight Plate Breastplate | 20 |
| 2034 | Featherlight Plate Bracers | 20 |
| 2037 | Featherlight Plate Hauberk | 20 |
| 2028 | Alatar's Platemail Hauberk | 20 |
| 1454 | Acid Yoroi Breastplate | 20 |

Wear one. It represents the 2,165 items that structurally cannot be affected.

### A11-A13. Pets - and the answer to "all or nothing, or every species?"

**You do not need to retest every species. Two tests cover it.** Here is why, from the code
rather than from assumption.

`PetDevice.ApplyVisualOverridesTo` (`PetDevice.cs:1213-1216`) force-clears the pet's
`ClothingBase`, `PaletteBase`, `PaletteTemplate` and `Shade` first, then at `:1294` sets
`pet.PaletteTemplate = VisualOverridePaletteTemplate.Value` only if the device has one. So a
pet's `PaletteTemplate` is never the summoned creature's own weenie value - it is always either
absent or whatever the device carries. Every writer of `VisualOverridePaletteTemplate`:

| Writer | Value written |
|---|---|
| `PetMutationService.cs:539` (mutation, serum, `@mutate_pet`) | a `0x04......` palette id |
| `PetDevice_Breeding.cs:1788` (bred baby with a mutation) | a `0x04......` palette id |
| `DeveloperCommands.cs:4584 / :4651` | a `0x04......` palette id |
| `PetDevice_Breeding.cs:1717` | inherited from the donor device |
| **`MonsterCapture.cs:879`** | **the captured creature's own `PaletteTemplate`** - can be a retail ordinal |

So:

- **Any mutated / bred / serum'd pet** carries a `0x04......` value, which takes the
  `(palOption & 0xFF000000) == 0x04000000` branch. That branch is **not being touched**, and
  the removed branch requires the value *not* to be `0x04......`. It therefore **cannot fire
  for a mutated pet of any species.** One test is sufficient.
- **A pet with no palette override at all** has `PaletteTemplate` removed entirely, so
  `palOption == 0` and the removed branch requires `palOption > 0`. Also cannot fire.
- **A captured, never-mutated pet** inherits the source creature's ordinal via
  `MonsterCapture.cs:879`. That *can* reach the removed branch - but only for the **same 71
  species already listed**, and for those the fix moves them from a bogus palette to master's
  behaviour. It is a correction, not a risk.

| # | Test | Expected |
|---|---|---|
| A11 | Summon any **mutated / bred** pet you already have | Colour unchanged from today. Covers every species at once. |
| A12 | `@mutate_pet` on a throwaway essence, re-summon | New colour applies, as before. |
| A13 | Use a Mutagenic Serum on a throwaway essence, re-summon | New colour applies, as before. |
| A14 | *(only if you have one)* A **captured, unmutated** pet of one of the 71 species | Colour may *improve*. Not a regression. |

If A11 is correct, every other species is correct by construction.

### A15. The delta nobody has measured

All 5,480 creatures in the rewritten block get uncovered body parts appended from
`baseSetup.Parts`, which master did not do. That is geometry, not palettes - none of the DAT
analysis covers it, and change A does not alter it. While running A1-A10, glance at each model
for **missing limbs, doubled heads, or floating parts**. That is the cheapest coverage
available and it is currently the weakest-evidenced claim in the review.

### Known residual - not fixed by A

Seven creatures still differ from master afterwards because the shade default stays `0.5`:
**Zealot King (3001004), Winternadir (64454100), A'nekshay Dave (115850002), V'xia (64454073),
Hea Trelye (64454700), Hea Xi'for (64454725), V'exallia (98760139)**. All low placement counts.

Separately, `@create 2001007` (**Undead Custodian**, 46 placements) represents a different group
of 28 creatures where a different palette is picked. **Check it during this pass.** If it looks
wrong, reverting `shade = Shade ?? 0.0f` at `Creature_Networking.cs:303` fixes it and the seven
above together.

---

## Change B - stop the pet rename writing a detached biota

### What changes

`Source/ACE.Server/Controllers/PetNamingController.cs:322`, inside `RenameOnline`:

```csharp
if (device == null)
{
    // current: falls through to a direct shard-DB biota edit while the owner is ONLINE
    return RenameOffline(owner.Guid.Full, petGuid, oldName, newName);

    // proposed:
    return RenameResult.Mismatch("the device is not in the owner's possession");
}
```

### B2 - what "side pack" means, and why it is the test that matters

A **side pack** is a container inside your main pack - a satchel, pouch or sack that you open
as its own window. Most players keep essences in one rather than loose in the main pack.

`RenameOnline` looks the device up with
`FindObject(petGuid, SearchLocations.MyInventory | SearchLocations.MyEquippedItems)`. The
question that decides whether this fix is safe is whether `MyInventory` looks *inside* side
packs, or only at the top level.

It recurses. `Container.GetInventoryItem` (`Container.cs:344-359`) is documented
"check all containers in our possession in main inventory or any side packs", and it searches
`Inventory` first, then loops over `GetCachedSideContainers()` calling itself for each.

**Why this matters:** my change turns "device not found" into an auto-deny. If the search did
*not* cover side packs, every player who keeps essences in a pouch would suddenly have their
rename requests denied - that would be a bad regression introduced by a fix. It does cover
them, so B2 is confirming that in practice rather than trusting my reading.

### B5 - what it actually is, step by step

This is the one deliberate **behaviour change** in the fix, so it is worth being precise.

**The situation.** A player requests a rename, and before an admin approves it, the essence
leaves their character - dropped on the ground, put in a storage chest, sold, or handed to
someone else. The player is still logged in.

**What happens today.** `RenameOnline` cannot find the device in the player's inventory or
equipment, so it calls `RenameOffline(owner.Guid.Full, ...)`. That function:

1. reads the biota straight from the shard database with `skipCache: true`,
2. edits the name on that detached copy,
3. writes the whole biota back with `SaveBiota`.

The server has that object loaded in memory somewhere else - on the landblock, in a chest, in
another player's pack. The in-memory copy knows nothing about the write. Whichever copy saves
last wins, so any property changed in memory since the last save can be silently reverted. It
also runs synchronous database I/O on the world thread, stalling every landblock for the
duration.

**What happens after the fix.** The approval returns **HTTP 409** with the reason
"the device is not in the owner's possession", and the request row is auto-denied. Nothing is
written. The player can request again once the essence is back on their character.

**How to reproduce it.**

1. Put a combat pet essence in your main pack.
2. `@pet-name Testname`.
3. **Drop the essence on the ground** (or put it in a chest).
4. Open `/pet-names` on the portal and click Approve.
5. Expect: the request is denied with that reason, HTTP 409, and nothing is renamed.
6. Pick the essence back up and confirm its name is untouched.

Before the fix, step 4 would have reported success and rewritten the database row underneath
the live object on the ground.

### Cost of the change

A request that would previously have been force-renamed via a direct DB write now auto-denies
and the player re-requests. Given the search covers main pack, every side pack and equipped
items, `device == null` means the essence genuinely is not on the character - so denying is the
correct answer in every case I can construct.

### Automated coverage

None. No test covers the controller.

### B - validation table

| # | Step | Expected |
|---|---|---|
| B1 | `@pet-name Testname`, essence in **main pack**, approve on `/pet-names` | Renamed. Name updates on the item, and on the pet if summoned. |
| B2 | Same, essence inside a **side pack / pouch** | **Renamed.** The case the fix must not break. |
| B3 | Same, with the pet **summoned** | Renamed; the live pet's name updates too. |
| B4 | Request, then **log out**, then approve | Renamed via the offline biota path, which this fix does not touch. Log back in to confirm. |
| B5 | Request, then **drop the essence**, then approve while still online | **Auto-denied, HTTP 409.** See above. |
| B6 | Request, rename it by other means, then approve | Auto-denied, "now named X, not Y". Unchanged. |
| B7 | Approve the same request twice quickly, or from two admin sessions | Second gets "already been reviewed". Unchanged - the atomic claim is untouched. |

Watch the server log during B5 for `[PetNaming]` errors and any tick stall.

---

## Change C - strip two UTF-8 BOMs

Three bytes (`EF BB BF`) removed from the start of `PetTailoring.cs` and
`DeveloperContentCommands.cs`. No source characters change.

### Impact: none, with evidence rather than theory

The reasonable worry is encoding: without a BOM a C# compiler may fall back to the system ANSI
codepage and corrupt non-ASCII characters.

- `PetTailoring.cs` has **zero** non-ASCII bytes after the BOM. Stripping makes it pure 7-bit
  ASCII, which decodes identically under every encoding. Risk-free.
- `DeveloperContentCommands.cs` has **6** non-ASCII bytes after the BOM: `U+2192` at line 1210
  and `U+2013` at line 1889, both in `@createinst` help text.

For the second file the evidence is decisive: **`master` ships this exact file with no BOM and
those same characters, and it builds and runs today.** Roslyn tries strict UTF-8 first and only
falls back to ANSI when the bytes are not valid UTF-8; this file is valid UTF-8. Stripping the
BOM returns it to master's encoding state.

### Optional extra, your call

Those two characters violate the ASCII rule and **do reach the AC client** - `@createinst` help
is printed in-game by `CommandHandlerHelper.WriteOutputInfo`, where they render as garbage
glyphs. They are pre-existing on master, not from this branch. Replacing `U+2192` with `->` and
`U+2013` with `-` fixes two real display bugs. **Not included unless you say so.**

### C - validation table

| # | Step | Expected |
|---|---|---|
| C1 | `git diff --stat` | 0 changed lines for both files - a byte-only change. |
| C2 | Build x64 Release | `0 Error(s)`. |
| C3 | `head -c3 <file> \| xxd -p` | ASCII bytes, not `efbbbf`. |
| C4 | In-game `@createinst` with no args | Help prints. With the optional extra, the arrow shows as `->`. |
| C5 | Use a Pet Tailoring Kit: extract, then apply | Works as before. `PetTailoring.cs` is the tailoring implementation and must be exercised once after recompiling it. |

---

## Shared validation for any subset

1. **Stop the server**, then
   `dotnet build Source/ACE.Server/ACE.Server.csproj -c Release -p:Platform=x64`.
   Expect `0 Error(s)`. `MSB3027` copy-lock means the server is still running.
2. `dotnet test Source/ACE.Server.Tests/ACE.Server.Tests.csproj -c Debug`. Expect
   **284 passed, 8 skipped, 9 failed**, the 9 being the known environment-only set. Anything
   else is a regression - stop.
3. Start the server, watch startup for `ERROR`.
4. `@clearcache`, then run the tables above.

## Rollback

| Scope | Command |
|---|---|
| Everything | `git reset --hard e4b5423d2` |
| One file | `git checkout e4b5423d2 -- <file>` |
| Live, without a rebuild | Not possible - none of the three is config-gated. |

None of A, B or C writes anything to a database, so a rollback is complete: there is no
persisted side effect to clean up.
