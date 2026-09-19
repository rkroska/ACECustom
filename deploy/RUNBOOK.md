# Pet Breeding deployment runbook

Branch: `feature/pet-breeding-motel` (119 commits ahead of `master`)
Deliverables in this directory:

| File | Target | What |
|---|---|---|
| `2026-09-19-PetBreeding-World.sql` | `ace_world` | 26 weenies, Ivo's shop, the motel portal + its placement |
| `2026-09-19-PetBreeding-Shard.sql` | `ace_shard` | the `pet_name_requests` table |
| `RUNBOOK.md` | you | this file |
| `REVIEW.md` | you | the pre-production review and its findings |

Read `REVIEW.md` before you start. One finding is marked BLOCKER and it is configuration,
not code: `pet_bond_enabled` is off on your shard while the bond gate is 100, so every breed
is refused until step 7c runs. The creature appearance change (B1) has been measured and is
down to a three-creature look-and-confirm in step 9.1.

Budget: about 45 minutes of hands-on work, of which 15 is the smoke test, plus however
long it takes you to place 15 NPCs (step 8). Placing the NPCs is the long pole and it
cannot be scripted.

---

## 0. Before the window opens

Do these while the server is still up and players are on. Nothing here changes anything.

1. **Record the config you are about to change.** These are the values a rollback puts back.
   ```
   @showprops
   ```
   Copy the whole output somewhere you will still have it tomorrow. In particular note the
   current stored values of every property in step 7; they override the code defaults, so
   what you see here is what the server is actually running.

2. **Check the breeding area is not set to "everywhere" or to the Marketplace.**
   ```
   @showprops
   ```
   `pet_breeding_allowed_landblock` must be `314` (0x013A) and `pet_breeding_allowed_variant`
   must be `3`. A stored `0` means *anywhere* - the whole world becomes the breeding area,
   and the own-pet healing bypass in the motel becomes a world-wide rule. A stored `364`
   (0x016C) is the Marketplace. Either is wrong; step 7 fixes it.

3. **Confirm Prof. Ruggan is placed.** The portal in section 5 of the world patch attaches
   itself to him. If he is not in `landblock_instance`, the portal weenie is created but
   never placed, silently.
   ```sql
   SELECT li.guid, li.obj_Cell_Id, li.variation_Id, s.value AS npc_Name
   FROM landblock_instance li
   JOIN weenie_properties_string s
     ON s.object_Id = li.weenie_Class_Id AND s.type = 1
   WHERE s.value IN ('Prof. Ruggan', 'Professor Ruggan');
   ```
   Zero rows is not fatal - you will place the portal by hand in step 8 - but know now.

4. **Note the next free static guid in the motel landblock**, for step 8.
   ```sql
   SELECT COALESCE(MAX(guid), 0x7013A000)
   FROM landblock_instance
   WHERE guid BETWEEN 0x7013A000 AND 0x7013AFFF;
   ```

5. **Announce the downtime.**

---

## 1. Stop the server

The build **will not replace the binaries while the server is running**. It fails with
`MSB3027 ... file is locked by ".NET Host"` and silently leaves the old DLLs in place, so
you end up testing the build you already had. I hit exactly this while reviewing.

```
@shutdown 60
```

Then confirm nothing still holds the output directory:

```powershell
Get-Process dotnet | Where-Object { $_.Path -like "*dotnet*" } | Select-Object Id, StartTime
```

Kill any straggler that has `Source\ACE.Server\bin\x64\Release\net10.0` open. Do not skip
this check.

---

## 2. Back up both databases

Non-negotiable. The shard backup is the only thing that can undo a bad breed.

```bash
mysqldump --single-transaction --routines --triggers ace_world > backup_ace_world_2026-09-19.sql
mysqldump --single-transaction --routines --triggers ace_shard > backup_ace_shard_2026-09-19.sql
```

Check both files are non-empty and end with `-- Dump completed`:

```bash
tail -1 backup_ace_world_2026-09-19.sql
tail -1 backup_ace_shard_2026-09-19.sql
```

Keep them until you are satisfied, which in practice means a week of live play, not an hour.

---

## 3. Build

From the repository root. The `-p:Platform=x64` matters: the server runs out of
`bin/x64/Release/net10.0`, and a plain `dotnet build -c Release` writes to `bin/Release/net10.0`
instead, which is a stale directory nothing launches from.

```bash
dotnet build Source/ACE.Server/ACE.Server.csproj -c Release -p:Platform=x64
```

This also runs `npm run build` in `Source/ACE.WebPortal/ClientApp` and copies the portal
bundle into both `bin/x64/Release/net10.0/wwwroot` and `Source/ACE.Server/wwwroot`. The
second copy writes into your working tree; that is by design and produces no git diff,
because the committed bundle and a fresh build differ only in line endings.

**Check the build output for `error CS`.** Ignore nothing. A clean run ends with
`0 Error(s)`. If you see `MSB3027`/`MSB3021` copy-lock errors, go back to step 1 - the
compile succeeded but the binaries were not replaced.

Confirm the DLL is actually new:

```powershell
Get-Item Source\ACE.Server\bin\x64\Release\net10.0\ACE.Server.dll | Select-Object LastWriteTime
```

Optionally re-run the tests. Expect **284 passed, 8 skipped, 9 failed**, and the 9 failures
must be exactly these environment-only ones: `GetCellTest`, `GetLandblockTest`,
`CanParseStarterGearJson`, `DatabaseManager_Initialize`, `WorldManager_Initialize`,
`CommandManager_Initialize`, `Defaults_TierLadder_CapAndWieldFloorRule`, `AttributeXP192`,
`Sphere_StepSphereDown`. Any other failure is a regression - stop.

```bash
dotnet test Source/ACE.Server.Tests/ACE.Server.Tests.csproj -c Debug
```

---

## 4. Run the shard SQL

Shard first: it only adds a table, so it is the safer of the two and it is independent of
the world patch.

```bash
mysql --abort-source-on-error ace_shard < deploy/2026-09-19-PetBreeding-Shard.sql
```

Read the four verification results it prints:

- **V1** 11 columns.
- **V2** `PRIMARY` on `id`, `IX_pet_name_req_status` on `(status, created_at)`.
- **V3** empty on a first install.
- **V4** `table_Present = 1`, `index_Present = 1`.

---

## 5. Run the world SQL

```bash
mysql --abort-source-on-error ace_world < deploy/2026-09-19-PetBreeding-World.sql
```

`--abort-source-on-error` is what makes this safe: the whole file is one transaction, so an
error stops the script before `COMMIT` and closing the connection rolls it back. If you paste
the file into MySQL Workbench instead, it will keep going past an error and you can end up
half-applied - and Workbench's safe-update mode is handled inside the file, so there is no
reason to use it here.

Read the seven verification results:

- **V1** 26 rows, every `status` = `OK`. Any `** MISSING **` means that section failed.
- **V2** exactly 9 shop rows: `78780250`-`78780255`, `78780257`, `98760399`, `98760400`.
  `stack_Size` must be `-1` (unlimited). **If this is empty or short, the section order was
  broken - re-run the whole file, in full, top to bottom.**
- **V3** zero rows. A row here means Ivo still carries `AlternateCurrency` and will price his
  entire shop in Stipends rather than pyreals.
- **V4** one row, `obj_Cell_Id` 20578990 (`0x013A02AE`), `variation_Id` 3.
- **V5** one row = the portal was placed next to Ruggan. **Zero rows means it was not** -
  place it by hand in step 8.
- **V6** zero rows.
- **V7** `created_Weenies = 26`, `ivo_Shop_Rows = 9`, `portal_Placements` 0 or 1.

---

## 6. Start the server

```
Source/ACE.Server/bin/x64/Release/net10.0/start_server.bat
```

Watch the startup log for:

- `Initializing PetMutationService...` - this scans about 9,500 DAT palette records before
  the world opens. It adds startup time and roughly 40-80 MB of resident memory that is
  never released (see REVIEW.md F5). If startup hangs here, that is where it is.
- `[WEB PORTAL] Visualizer Cache Eviction background worker started.`
- No `ERROR` lines.

---

## 7. Configuration

**Stored values in the shard database override the defaults in code.** Everything below is
an explicit `@modify`, including the ones that "match the default", because on your shard
they currently do not: you told me your live server is carrying test values.

### 7a. Turn the test switches OFF - do this first

Your live shard currently has `force_mutation` on, which makes every single breed mutate.

```
@modifybool pet_breeding_force_mutation false
@modifybool pet_breeding_bypass_male_charges false
@modifybool pet_breeding_bypass_female_cooldown false
@modifybool pet_breeding_verbose_logging false
@modifybool pet_trace false
@modifybool pet_visual_packet_debug false
@modifybool pet_combat_debug_follow_ai_console false
```

`pet_trace` in particular writes one log line per swing, cast, DoT tick, heal and death for
**everyone online, server-wide**. It is a test-shard tool. There is no second knob.

### 7b. The area

```
@modifylong pet_breeding_allowed_landblock 314
@modifylong pet_breeding_allowed_variant 3
```

`314` is `0x013A`. Set this even if `@showprops` already says 314. A stored `0` means
*anywhere*, which also switches on the motel's own-pet healing and beneficial-spell
targeting rules across the entire world.

### 7c. Gates

```
@modifybool pet_breeding_enabled true
@modifylong pet_breeding_min_parent_level 100
@modifylong pet_breeding_min_bond 100
@modifybool pet_bond_enabled true
```

**`pet_bond_enabled` is the interlock that will otherwise stop breeding dead.** It defaults
to `false` in code. Bond XP is only awarded when it is on; `PetBondLevel` falls back to 1;
gate 8 requires bond >= `pet_breeding_min_bond` (100). With bond disabled, no essence can ever
reach 100 and every breed attempt is refused.

Decided: **bond on, gate at 100.** The four commands above are the whole decision - run them
and read them back with `@showprops`.

### 7d. Mutation rates - these are the balance decision

Your stored values are test values. The code defaults are the tuned ones.

| Property | Your stored (test) | Code default | Command |
|---|---|---|---|
| `pet_breeding_base_mutation_chance` | 0.2 | **0.05** | `@modifydouble pet_breeding_base_mutation_chance 0.05` |
| `pet_breeding_potency_mutation_chance` | 0.5 | **0.03** | `@modifydouble pet_breeding_potency_mutation_chance 0.03` |
| `pet_breeding_mutation_decay_rate` | 0.35 | 0.0 | see below |
| `pet_breeding_mutation_min_floor` | ? | 0.02 | `@modifydouble pet_breeding_mutation_min_floor 0.02` |

**On decay: keep your 0.35, do not take the code default of 0.0.** The chance formula is
`base / (1 + decay * inheritedStatCounts)`. With `decay = 0` there is no diminishing return,
and with `pet_breeding_max_stat_mutations = 0` (uncapped) there is no ceiling either, so
bred lineages gain damage rating without bound forever. Your 0.35 is the safer number.

```
@modifydouble pet_breeding_base_mutation_chance 0.05
@modifydouble pet_breeding_potency_mutation_chance 0.03
@modifydouble pet_breeding_mutation_decay_rate 0.35
@modifydouble pet_breeding_mutation_min_floor 0.02
```

### 7e. Mutation steps and caps

```
@modifylong pet_breeding_damage_mutation_step 10
@modifylong pet_breeding_dr_mutation_step 10
@modifylong pet_breeding_crit_mutation_step 5
@modifylong pet_breeding_vitality_mutation_step 50
@modifylong pet_breeding_potency_mutation_step 25
@modifylong pet_breeding_potency_soft_cap 1000
@modifylong pet_breeding_potency_hard_cap 0
@modifylong pet_breeding_max_stat_mutations 0
```

Both caps are `0` = uncapped, and `pet_potency_max_stored` also defaults to `0`, so **nothing
limits mutation counts or stored potency**. That is the intended configuration here: uncapped
linear growth, in keeping with the server's infinite-progression premise.

Two things worth knowing about that choice:

- **Growth decelerates and then goes flat.** With base `0.05`, decay `0.35` and floor `0.02`,
  the chance hits the floor at about 4 inherited mutations and stays at a flat 2% per breed -
  roughly one mutation per 50 breeds. The 4-hour female cooldown is the real rate limiter.
- **Incense dominates the late game.** Two Exquisite Incense take that 2% to 22%. Past a few
  mutations, incense is the only meaningful driver, so late-game pacing is set by what you
  charge for it.

**If you ever need to rein power in, change the cap, never the step.** The four stat steps in
7e above are read at *summon* time (`CombatPet.Init:418`), so lowering one retroactively nerfs
every pet anyone has ever bred. Adding a cap later only halts further growth and leaves
existing pets alone. Both of these are safe to add at any point:

```
@modifylong pet_breeding_max_stat_mutations 10
@modifylong pet_breeding_potency_hard_cap 2000
```

### 7f. Charges, cooldown, dance window

```
@modifylong   pet_breeding_male_max_charges 10
@modifydouble pet_breeding_male_charge_reset_hours 24.0
@modifydouble pet_breeding_cooldown_hours 4.0
@modifydouble pet_breeding_dance_sync_seconds 5.0
@modifybool   pet_breeding_dismiss_after_breed true
@modifybool   pet_breeding_allow_shiny false
```

### 7g. The mating guardian

Defaults to **off** in code; your shard has it **on**. It is a headline feature, so leaving
it on is reasonable - but it is also the least-tested path on the branch (see REVIEW.md F3:
a server restart mid-fight drops the pending breed and the parents have already paid).

```
@modifybool   pet_breeding_guardian_enabled true
@modifydouble pet_breeding_guardian_timeout_seconds 90.0
@modifylong   pet_breeding_guardian_template_wcid 7
@modifydouble pet_breeding_guardian_health_mult 1.0
@modifydouble pet_breeding_guardian_damage_mult 0.5
@modifydouble pet_breeding_guardian_translucency 0.6
```

### 7h. Maturity

```
@modifybool   pet_maturity_enabled true
@modifylong   pet_maturity_kills_required 300
@modifylong   pet_maturity_stages 5
@modifystring pet_maturity_stage_names Newborn,Whelp,Juvenile,Adolescent,Young Adult
@modifydouble pet_maturity_min_damage_share 0.10
@modifydouble pet_maturity_juvenile_scale 0.5
@modifydouble pet_maturity_juvenile_strength 0.5
@modifybool   pet_maturity_imprint_on_summon true
```

### 7i. Essence sex icons

```
@modifybool pet_sex_icon_underlay_enabled true
@modifylong pet_sex_icon_underlay_male 100670255
@modifylong pet_sex_icon_underlay_female 100670253
```

### 7j. Read it all back

```
@showprops
```

Check every value above landed. `@modifydouble` on a property declared `long` (or the
reverse) is silently a no-op in some builds - do not assume, read it back.

---

## 8. Place the content (the manual step)

**The world SQL creates the 15 NPCs but does not place any of them.** There is no safe way
to choose walkable coordinates inside the motel from SQL. Until you do this, Ivo does not
exist in the world and nothing on his shop list can be bought.

1. Clear the caches so the new weenies are visible without another restart:
   ```
   @clearcache
   ```
2. Portal into the motel (or teleport to `0x013A02AE` variation 3).
3. **Pick the ritual room first, and verify it is a single landcell.** Stand in it, run
   `@breed-debug`, note the `Cell=0x........`, then walk the whole room re-running it. If the
   cell id changes, the room spans more than one landcell and two players standing on
   opposite sides of it will fail to breed with no error message. Pick a different room.
4. Walk to each mark and place:
   ```
   @createinst 78780200     The Drop           Fenwick, Kennel Intern
   @createinst 78780201     Quartermaster nook Ivo, Ruggan's Quartermaster
   @createinst 78780202     Ritual Floor       DJ Skulk
   @createinst 78780203     Ritual Floor       Gary
   @createinst 78780204     The Drop (rare)    Mrs. Ruggan
   @createinst 78780210     Registry wing      Bexley, Keeper of the Registry
   @createinst 78780211     Registry wing      Registered Browerk, Champion Line
   @createinst 78780212     Registry wing      Certified Shreth, Third Generation
   @createinst 78780213     Registry wing      Pedigreed Ursuin (Papers Pending)
   @createinst 78780214     Registry wing      Drudge Skulker of Record
   @createinst 78780220     Ward wing          Splotch
   @createinst 78780221     Ward wing          The Teal Incident
   @createinst 78780222     Ward wing          Ursuin, Unregistered
   @createinst 78780223     Ward wing          Nine-Colour Shreth
   @createinst 78780224     Ward wing          Subject Twelve
   ```
   Use `@nudge` and `@rotate` to adjust; both write straight back to `landblock_instance`.

   Two placement rules that come from the mechanics, not from taste:
   - **Keep the Registry and Ward wings out of the ritual room's landcell.** Anything
     summoned in that cell is a breeding candidate.
   - **Keep Bexley and Splotch within 60 m of each other and of Fenwick**, or their
     `LocalSignal` emote chains stop resolving. 60 m is the `HearLocalSignalsRadius` the
     patch sets on all three.

5. If world-SQL check **V5** returned zero rows, place the portal by hand too, wherever you
   want players to enter from:
   ```
   @createinst 98760388
   ```
6. Export the placements so they survive a world refresh, and commit the result:
   ```
   @export-sql 313A
   ```
   Without this, a world reload from the base SQL wipes every placement you just made.

---

## 9. Smoke test (15 minutes)

Do these in order. Stop at the first failure.

### Regression checks first - these matter more than the feature

1. **The three creatures whose appearance actually changed.** B1 has been measured against
   the world DB and the DATs: exactly 298 weenies change, and these are one from each
   affected group. Spawn or visit them and check they look sane:
   ```
   @create 2001006      Undead Librarian   (PaletteTemplate used as a literal subpalette id)
   @create 38845        Master Soldier     (gains subpalettes it never had)
   @create 2001007      Undead Custodian   (different palette picked)
   ```
   You are looking for a creature that is obviously the wrong colour, or has body parts
   missing or doubled. Everything else - Tuskers, Assailer, Devastator, Rampager, Olthoi
   Grub, drudges, olthoi, shreth, ursuin and players - was verified unchanged and does not
   need checking.
   If something looks wrong: `@modifybool pet_visual_packet_debug true`, re-log to force a
   redraw, read the `[CREATURE PACKET DEBUG]` lines, then turn it straight back off.
2. **Combat is unchanged.** Kill half a dozen ordinary monsters with melee, with a bow and
   with war magic. Damage numbers should look normal. Take some hits.
3. **The web portal loads** and its existing pages still work. Check a couple of item icons
   on an existing page - the icon renderer changed (REVIEW.md F6) and icons without a UI
   effect may now show white where they used to show transparent.

### Then the feature

4. **Vendor.** Find Ivo, open his shop. Nine items. Prices in **pyreals, not Stipends**.
   Buy one Lesser Courtship Incense.
5. **Consumable.** Use the incense on a combat pet essence in your pack. Expect the
   confirmation message, the incense consumed, and `+2.5%` on the essence's ID panel.
6. **ID panel.** Appraise a combat pet essence. The breeding block must render as clean
   text: no music-note glyphs at line ends, no boxes, no mojibake.
7. **Neutering kit.** Buy one, use it on a *throwaway* essence. This is permanent and cannot
   be undone - do not test it on anything you care about.
8. **Tailoring.** Buy a tailoring kit, extract from a throwaway essence (the source essence
   is consumed), then apply the filled kit to another essence. Summon it and confirm the
   look transferred and the stats did not.
9. **Breeding.** You need a second player. Both summon opposite-sex combat pets that meet
   the level and bond gates, stand in the ritual room, both `/dance`. Watch for the ritual
   message, the guardian if it spawns, and the baby landing in one owner's pack.
   If it refuses, `@breed-debug` tells you which gate said no.
10. **Pet naming.** `@pet-name Testname` on a summoned pet, then approve it from the portal
    at `/pet-names`. Confirm the rename reaches the client.
11. **Breeding simulator.** Open the portal's Pet Breeding Calculator. Its config panel must
    say **LIVE SERVER**, not `FALLBACK DEFAULTS`, and the numbers must match what you set in
    step 7.

### Leave it running

Watch `ACE_Log.txt` for 10 minutes with players on. You are looking for `[PetBreeding]`,
`[PetMaturity]` or `[PetTailoring]` errors, and for any sign that log volume has jumped -
if it has, `pet_trace` is still on.

---

## 10. Rollback

### Code

Stop the server, restore the previous `bin/x64/Release/net10.0` directory (keep a copy of it
before step 3 if you want this to be quick), or:

```bash
git checkout master
dotnet build Source/ACE.Server/ACE.Server.csproj -c Release -p:Platform=x64
```

This reverses every code-side change, including the creature appearance path, because none
of it is persisted.

### Configuration

Every `@modify` is reversible with another `@modify` back to the value you recorded in
step 0. To take breeding out of service without touching anything else:

```
@modifybool pet_breeding_enabled false
```

That is the fastest single lever you have and it should be your first move if something goes
wrong in front of players.

### World SQL

Fully reversible - it only ever touched rows it created.

```sql
SET SQL_SAFE_UPDATES = 0;
START TRANSACTION;

DELETE FROM `landblock_instance` WHERE `weenie_Class_Id` IN (
  98760388,
  78780200,78780201,78780202,78780203,78780204,
  78780210,78780211,78780212,78780213,78780214,
  78780220,78780221,78780222,78780223,78780224);

DELETE FROM `weenie` WHERE `class_Id` IN (
  78780200,78780201,78780202,78780203,78780204,
  78780210,78780211,78780212,78780213,78780214,
  78780220,78780221,78780222,78780223,78780224,
  78780250,78780251,78780252,78780253,78780254,78780255,78780257,
  98760388,98760399,98760400,98760401);

COMMIT;
```

The `weenie_*` foreign keys are `ON DELETE CASCADE`, so that clears every property row too.
Then `@clearcache` or restart.

Note what this does *not* undo: the pre-7878 ids `98760410`-`98760412`, `98760415` and
`98760418` that section 3 retired. They were already orphans; if you genuinely need them
back, restore `ace_world` from the step 2 backup instead.

### Shard SQL

```sql
DROP TABLE IF EXISTS `pet_name_requests`;
```

This throws away any pending and reviewed rename requests. The renames themselves are
already written into the device biotas and are not undone by this.

---

## 11. What cannot be rolled back

Once players touch the feature, the following are permanent short of restoring `ace_shard`
from the step 2 backup - which costs you every unrelated thing that happened since.

| Not reversible | Why |
|---|---|
| **Baby essences created by a breed** | A new biota with a fresh dynamic GUID in a player's inventory. Deleting the world weenies does not remove it. |
| **A neutered essence** | `PetNeutered` is a one-way flag by design. There is no un-neuter tool. |
| **An essence consumed by tailoring extract** | `HandleExtract` destroys the source essence to make the filled kit. |
| **Consumables used** | Incense, draughts, catalysts and offerings are consumed on use and their property is written to the device. |
| **Mutagenic Serum recolours** | Overwrites `VisualOverridePaletteTemplate`/`Base` and deletes `CapturedObjDescPalettes`. The previous colour is not stored anywhere. |
| **Pyreals spent at Ivo** | Ordinary vendor sale. |
| **Maturity kills, imprinting and bond attunement** | `PetBondAttuned`, `PetBondAttunedCharacterId`, `Attuned` and `Bonded` bind an essence to one character on first summon. |
| **Approved pet renames** | Written into the device biota; the request row is only an audit trail. |
| **A breed interrupted by a server restart** | In-memory `pendingGuardianBreeds` is lost. Charges and cooldown were already spent, the baby was never created. Known, documented, not handled (REVIEW.md F3). |

Everything else - the code, the config, the world rows - is reversible.

---

## 12. If you have to abort mid-deploy

| Failed at | Do this |
|---|---|
| Step 3 (build) | Nothing has changed. Restart the old server. |
| Step 4 (shard SQL) | `DROP TABLE pet_name_requests;` then restart the old server. |
| Step 5 (world SQL) | With `--abort-source-on-error` the transaction never committed and `ace_world` is untouched. Verify with V7 (expect `created_Weenies = 0`), then restart the old server. |
| Step 6 (server will not start) | Read `ACE_Log.txt`. Restore the previous `bin` directory and start that. The SQL is harmless with old binaries: the new weenies simply sit unused. |
| Step 9 (smoke test fails) | `@modifybool pet_breeding_enabled false` first, so players cannot reach the feature, then decide between a config fix and a full code rollback. |
