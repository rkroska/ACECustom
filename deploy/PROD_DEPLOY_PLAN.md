# Pet Breeding / Ruggan's Annex - production deployment plan

Written 2026-09-21 from branch `feature/pet-breeding-motel`. Every fact below was checked against the
code and the test databases on that date.

## The two scripts

| Script | Database | What it does |
|---|---|---|
| `deploy/Annex-Prod-Bundle.sql` | `ace_world` | All 34 weenies + the 22 annex placements, copied from test |
| `deploy/Annex-Prod-Shard.sql` | `ace_shard` | The `pet_name_requests` table + the breeding settings prod needs |

Both are one paste each, safe to re-run, and end with verification queries.

**Do not run anything else.** Not the individual `Database/Updates/World` files, and not
`deploy/test-only/`, which is for test servers only. Several of the older world files would undo later
fixes if they ran after the bundle (feud openers come back, DJ Skulk goes deaf, the portal is re-placed).
The earlier `deploy/2026-09-19-PetBreeding-*.sql` scripts were deleted: the world one overwrote
98760399-98760401, which on prod are the Tyrannical Drudge generators, the Realm of Woe portal and
Doriathazaar. Nothing that can reach prod may touch those three ids.

Prod does not auto-apply them either: `AutoApplyDatabaseUpdates` reads `DatabaseSetupScripts/Updates`
from the build output, and this repo's `Database/Updates` files are not copied there.

### What the world bundle contains

- **Weenies (34), row for row from test**, nothing cloned from retail templates at import time:
  - 78780200-78780225 - the annex cast: Fenwick, Ivo, DJ Skulk, Gary, Mrs. Ruggan, Bexley and the
    Registry pets, Splotch and the Ward pets, both Sawato Bandits
  - 78780230-78780233, 78780240 - the paternity storyline: Mubb, Gorta, Mubb Junior, Denton and the
    hidden Scene Director
  - 78780250-78780255, 78780257 - breeding consumables and the Mutagenic Serum
  - 98760388 - the motel portal, now pointing at the annex
  - 78780258-78780260 - the neutering and tailoring kits
  - 78780261 - the annex exit portal, back to Prof. Ruggan
- **Left out, test-only:** Scene Tester 78780241 and Baby Candidates 78780234/78780235. The @et staging
  export 78790009 is outside the bundle's range.
- **Placements (22):** the annex (0x0106 variation 2), including the exit portal. Every placement gets a fresh guid above the
  highest one already used in that landblock on prod, so nothing can collide.
- **The portal's placement is not touched.** Prod already has 98760388 placed beside its own Prof.
  Ruggan (0xDB3B, a different spot than on test). The bundle replaces the portal weenie (new
  destination), and prod's placement keeps pointing at it.
- **The bundle never touches 98760399-98760401.** On prod those are other content: Tyrannical Drudge
  Gen (placed 65 times), the Realm of Woe portal and Doriathazaar. The pet kits moved to
  78780258-78780260 on 2026-09-21.

**Proof it matches test:** the bundle was run on test inside a rolled-back transaction, twice. All
2,387 rows (every property, emote, click line and placement) came out identical to live test.

**Checked against the prod dump** (`ace_only_2026-09-21_20-14.sql.gz`):
- Every table the bundle writes has identical columns on prod.
- None of 78780200-78780299 exist on prod.
- 98760388 on prod is the older "Seedy Motel" portal, which the bundle replaces on purpose.

### Regenerating

If anything changes on test, regenerate before deploying:

```
python deploy/export_annex_bundle.py
```

## Deployment steps

1. **Back up** `ace_world` and `ace_shard` on prod.
2. **Stop the server.**
3. **Deploy the new build** (x64 Release). It must include the colour-cycle code
   (`Creature_ShowcaseColour.cs`, `PropertyFloat.ShowcaseColourCycleSeconds` 9060). An older build still
   works; the Ward pets just hold one colour. It must also include the vendor purchase fix in
   `Vendor.cs` (the purchase total is summed as 64-bit and oversized purchases are refused). Without it,
   a modified client can buy 35+ Pet Tailoring Kits, or 15,000+ trade notes from any vendor, for a
   wrapped fraction of the price.
4. **Run the shard script** with `ace_shard` selected. Check its results:
   - V1: `table_Present = 1`, `index_Present = 1`
   - V2: `pet_breeding_allowed_landblock = 262`, `pet_breeding_allowed_variant = 2`
   - V3: read every row. Anything the script did not write overrides a code default.
   - V4: `pet_bond_enabled` must be 1 (see Q1).
5. **Run the world bundle** with `ace_world` selected:
   `mysql ace_world < deploy/Annex-Prod-Bundle.sql` (batch mode stops at the first error, before COMMIT;
   never add `--force`). In Workbench, watch for
   red lines; if one appears, run `ROLLBACK` in the same tab. Check its results:
   - V1 = 34 weenies
   - V2 = 22 placements
   - V2b = at least one row (the portal is still placed)
   - V3 and V4 = no rows
6. **Start the server.**

## In-game checks after deploy

- [ ] Use the portal beside Prof. Ruggan. You should land at "The Drop", facing Fenwick.
- [ ] Use the Portal to Prof. Ruggan inside the annex. You should come out beside him, clear of the motel portal.
- [ ] `@fetchlong pet_breeding_allowed_landblock` shows 262 and `@fetchlong pet_breeding_allowed_variant` shows 2.
  (`@showprops` crashes on this codebase, a bug on master since 2025-12; use `@fetchlong` or `@petserverconfig`.)
- [ ] Click Fenwick. You should get the 14-line tutorial.
- [ ] Ivo sells 9 items at the MMD prices: incense 5 / 10 / 20, Draught, Catalyst, Offering and
  Neutering Kit 10 each, Serum 100, Tailoring Kit 500. The Serum and Tailoring Kit do not stack.
- [ ] Stand around for about 5 minutes and watch for a paternity scene.
- [ ] Ward pets and The Sawato Situation sparkle and change colour about once a minute.
- [ ] Breed two eligible pets in the annex: tier 100+, bond 100+.

## Settings

### Written by the shard script

| Setting | Value | Why |
|---|---|---|
| `pet_breeding_allowed_landblock` | 262 (0x0106) | Matches the code default since Q8; written so an older build is right too |
| `pet_breeding_allowed_variant` | 2 | As above |
| `pet_breeding_enabled` | true | Same as the default, written for clarity |
| `pet_breeding_guardian_enabled` | **true** | Default is false. See Q2. |
| `pet_breeding_force_mutation` | false | Test-only switch, pinned off |
| `pet_breeding_bypass_male_charges` | false | Test-only switch, pinned off |
| `pet_breeding_bypass_female_cooldown` | false | Test-only switch, pinned off |
| `pet_breeding_verbose_logging` | false | Players can spam it with a dance macro |
| `pet_trace` | false | Debug only; on for test |
| `pet_visual_packet_debug` | false | Debug only; on for test |
| `pet_breeding_mutation_decay_rate` | **0.1** | Default is 0 (flat). Gentle diminishing returns; see Q3. |

### Left at the code default on prod

Fenwick's tutorial quotes these numbers, and they match.

| Setting | Prod (code default) | Test currently | Notes |
|---|---|---|---|
| `pet_breeding_base_mutation_chance` | **0.05 (5%)** | 0.20 | Fenwick: "about one in twenty" |
| `pet_breeding_mutation_min_floor` | 0.02 | 0.02 | |
| `pet_breeding_potency_mutation_chance` | **0.03 (3%)** | 0.50 | |
| `pet_breeding_min_bond` | **100** | 1 | Needs `pet_bond_enabled` (Q1) |
| `pet_breeding_min_parent_level` | 100 (tier) | 100 | |
| `pet_breeding_cooldown_hours` | 4 | 4 | Fenwick: "four hours between litters" |
| `pet_breeding_male_max_charges` | 10 | 10 | Fenwick: "ten breedings a day" |
| `pet_breeding_male_charge_reset_hours` | 24 | 24 | |
| `pet_breeding_dance_sync_seconds` | 5 | 5 | Fenwick: "within five seconds" |
| `pet_breeding_dismiss_after_breed` | true | true | |
| `pet_breeding_allow_shiny` | false | false | Fenwick: "Shinies don't breed" |
| `pet_breeding_damage_mutation_step` | +10 damage rating | 10 | |
| `pet_breeding_dr_mutation_step` | +10 damage resist rating | 10 | |
| `pet_breeding_crit_mutation_step` | +5 crit rating | 5 | |
| `pet_breeding_vitality_mutation_step` | +50 max health | 50 | |
| `pet_breeding_potency_mutation_step` | +25 potency | 25 | |
| `pet_breeding_potency_soft_cap` / `hard_cap` | 1000 / 0 (uncapped) | same | |
| `pet_breeding_max_stat_mutations` | 0 (uncapped) | 0 | |
| `pet_breeding_guardian_timeout_seconds` | 90 | 90 | On timeout the birth still completes |
| `pet_breeding_guardian_health_mult` / `damage_mult` | 1.0 / 0.5 | same | |
| `pet_breeding_guardian_translucency` | 0.6 | 0.6 | |
| `pet_breeding_guardian_template_wcid` | 7 (Drudge Skulker) | 7 | |
| `pet_maturity_enabled` | true | true | |
| `pet_maturity_kills_required` | 300 | 300 | Fenwick: "three hundred kills" |
| `pet_maturity_stages` | 5 (Newborn ... Young Adult) | 5 | |
| `pet_maturity_min_damage_share` | 10% | 10% | |
| `pet_maturity_juvenile_scale` / `strength` | 0.5 / 0.5 | same | |
| `pet_maturity_imprint_on_summon` | true | true | Fenwick: "owns it forever" |
| `pet_sex_icon_underlay_enabled` | true | true | |
| `content_template_export_auto_import` / `_auto_discord` | true / true | same | @et on prod loads into prod `ace_world` and posts to Discord |

No existing setting's default changed on this branch. It only adds 51 new settings.

## Questions for discussion

- **Q1. Is `pet_bond_enabled` on in prod?** **Answered from the prod dump: yes** (and
  `pet_potency_enabled` is on too). Prod also has no stored `pet_breeding_*` settings, so the shard
  script's values apply cleanly.
- **Q2. Mating guardian on or off?** **Answered: on.** The code default is off. The script turns it on because it is on
  in test and Fenwick's tutorial describes it ("Its spirit stands up and the two parents have to put
  it down"). Known accepted issue H2: a server restart during a guardian fight loses that breed.
- **Q3. Mutation decay.** **Answered: 0.1**, written by the shard script. Chance per litter is
  5% / (1 + 0.1 x mutations carried), floored at 2%: 5% at 0, 3.3% at 5, 2.5% at 10, floor at ~15.
  About 290 litters to a 10-mutation line without incense (200 at decay 0, 420 at 0.35, judged too steep).
- **Q4. The annex has no exit.** Variation 2 holds only the annex NPCs; the retail dungeon's Surface
  Portal is in the base layer and does not appear there. Players must recall out. Add an exit portal
  (for example back to Prof. Ruggan)? **Done:** 78780261 at cell 0x01060179, landing at 0xDB3B0019
  (80.28, 18.18) beside Prof. Ruggan. Both spots were stood on in game.
- **Q5. Portal arrival point.** Set to "The Drop" in cell 0x01060186 (35.18, -19.76), beside Fenwick,
  where the Scene Tester stood on test. **Answered: the spot is right**; the facing was turned 180
  degrees on 2026-09-21 so players land facing Fenwick, then moved to a spot and facing stood on in
  game: (35.98, -20.04).
- **Q6. Does prod have Prof. Ruggan (694201298) at 0xDB3B?** **Answered from the prod dump: yes**,
  with the motel portal already placed beside him. The bundle updates the portal's destination and
  leaves that placement where it is.
- **Q7. Two Nine-Colour Shreths are placed; the Teal Incident and Registered Browerk are not.** The
  bundle ships the layout exactly as it is on test. **Answered: intentional.**
- **Q8. The code default for the breeding location still says 0x013A variation 3.** The shard script
  overrides it, so prod is fine. **Done:** the default is now 0x0106 variation 2.
- **Q9. Older docs still describe 0x013A:** `docs/PET_BREEDING_ANNEX_DESIGN.md` and comments in the
  older annex SQL. The superseded deploy scripts, `RUNBOOK.md` and `REVIEW.md` were deleted on
  2026-09-21; the current docs are `docs/PET_BREEDING_TECHNICAL_DESIGN.md`, `PET_BREEDING_REFERENCE.md`
  and `PET_BREEDING_PLAYER_GUIDE.md`. **Answered: leave the old docs as they are** (they carry
  superseded banners).
- **Q10. Which branch does the prod build come from?** **Answered: `feature/pet-breeding-motel`.**

## Rollback

- **World:** the bundle deletes and re-creates only its own weenies and placements. To undo it, restore
  the `ace_world` backup.
- **Shard:** the `pet_name_requests` table is harmless if left in place. For the settings, restore the
  backup or delete the rows the script wrote.
