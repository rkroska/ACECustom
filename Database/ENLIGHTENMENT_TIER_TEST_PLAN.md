# Enlightenment tier system — test plan

Target enlightenment **T** = player’s current `Enlightenment + 1` (the level they are attempting to gain). All bands in `config_enlightenment_tier` are expressed in terms of **T**.

## Preconditions

- On startup the server runs `EnlightenmentTierManager.EnsureTableCreated()` (same DDL/seed as `Database/Updates/Shard/2026-03-28-00-Config-Enlightenment-Tier.sql`). You can still run that SQL manually if you prefer migrations in source control only.
- `/reload-enlightenment-tiers` reapplies create-if-missing + empty-table seed, then reloads rows.
- Confirm startup log: `Loaded N enlightenment tier row(s) from database.` If the table is missing or invalid, the server uses **compiled defaults** and logs a warning; fix the table before production.

## 1. Configuration / validation

| Step | Action | Expected |
|------|--------|----------|
| 1.1 | Query `SELECT * FROM config_enlightenment_tier ORDER BY sort_order` | Six rows after seed; last row `max_target_enl` IS NULL |
| 1.2 | Introduce a gap (e.g. set one row’s `max_target_enl` so next `min_target_enl` is not `max+1`) and `/reload-enlightenment-tiers` | Reload fails; fallback to compiled defaults; audit message indicates failure |
| 1.3 | Set only two of three `lum_step_*` columns on a row | Reload fails validation |
| 1.4 | Set `item_wcid` with NULL `item_count_target_minus` | Reload fails validation |
| 1.5 | Set `item_wcid` with empty `item_label` | Reload fails validation |
| 1.6 | Restore valid table and `/reload-enlightenment-tiers` | Success message; `LoadedFromDatabase` behavior restored |

## 2. Luminance cost (formula parity)

Use a test character; compare **expected** `ceil(T * lum_base_per_target * multiplier)` to the message shown when luminance is too low (or log / DB).

| T | Expected band | Notes |
|---|----------------|-------|
| 1–50 | 0 | Rows 1–2 use `lum_base_per_target = 0` |
| 51 | `51 × 100_000_000` | Token tier, paid lum starts |
| 150 | `150 × 100_000_000` | End of 51–150 band |
| 151 | `151 × 1_000_000_000` | Medallion band |
| 300 | `300 × 1_000_000_000` | End of 151–300 |
| 301 | `ceil(301 × 2_000_000_000 × 1.0)` | First sigil sub-band (301–324); step multiplier steps=0 |
| 324 | `ceil(324 × 2_000_000_000 × 1.0)` | Same formula as 301 |
| 325 | Same formula as 324 by default | Row 6 seed matches row 5 until you change SQL |
| 351 | `ceil(351 × 2_000_000_000 × 1.5)` | `over=51`, `steps=(51-1)/50=1`, increment 0.5 |

Regression: **`enl_50_base_lum_cost` / `enl_150_base_lum_cost` / `enl_300_base_lum_cost` are no longer read** for enlightenment. Changing them with `/modifylong` must **not** change enlightenment cost; change `lum_base_per_target` (and step columns) in the table instead.

## 3. Items and counts

Required stacks = `max(0, T - item_count_target_minus)` with default `item_count_target_minus = 5`.

| Current enl | T | Expected consumable | Expected count |
|-------------|---|---------------------|----------------|
| 5 | 6 | Tokens (WCID 300000) | 1 |
| 149 | 150 | Tokens | 145 |
| 150 | 151 | Medallions (90000217) | 146 |
| 300 | 301 | Sigils (300101189) | 296 |
| 323 | 324 | Sigils | 319 |
| 324 | 325 | Sigils (row 6; change WCID in DB to test a new item) | 320 |

After a successful enlighten, inventory loses exactly that many of the tier’s `item_wcid` (and luminance is spent).

## 4. Quest gates

| T range | `quest_stamp` (seed) | Failure when not stamped |
|---------|----------------------|---------------------------|
| 151–300 | `ParagonEnlCompleted` | `quest_failure_message` for weapon paragon |
| 301+ | `ParagonArmorCompleted` | Armor paragon message |

Verify: character **without** stamp cannot pass validation; with stamp can (other reqs met).

## 5. Unchanged enlightenment rules (regression)

These still live in `Enlightenment.cs` and should behave as before:

- Level ≥ `275 + current Enlightenment`
- 25 free pack slots, no vitae, non-combat, not in dungeon, not teleport-busy, 10s after portal
- Luminance augs total 65 for T > 10
- Society master for T > 30
- Society removal still governed by `enl_removes_society`
- Post-enlighten: level 1, skills reset, spells dispelled, sigil/token consumption, etc.

## 6. Runtime reload

- With players online, run `/reload-enlightenment-tiers` after a **valid** SQL edit.
- Immediately attempt enlighten (or re-open validation): new cost/item/quest rules apply without binary restart.

## 7. Custom 325+ update (content)

1. `UPDATE config_enlightenment_tier SET lum_base_per_target = …, item_wcid = …, item_label = '…', quest_stamp = …, quest_failure_message = … WHERE min_target_enl = 325;` (adjust columns as needed).
2. `/reload-enlightenment-tiers`
3. Re-run sections **2–4** for T = 325 and a few higher values.

## 8. Adding a future breakpoint (e.g. 350)

1. Narrow the open-ended row: `UPDATE config_enlightenment_tier SET max_target_enl = 349 WHERE min_target_enl = 325 AND max_target_enl IS NULL;` (example).
2. `INSERT` a new row with `min_target_enl = 350`, `max_target_enl = NULL`, and desired lum/item/quest columns.
3. Ensure `max` of previous row + 1 = `min` of new row.
4. `/reload-enlightenment-tiers` — must succeed with no validation error.

## 9. Edge cases

- **T above 10 000**: Compiled validation only checks 1–10 000; extending tiers far beyond should still use contiguous bands; spot-check a high T manually.
- **Missing table / empty table**: Server should start using compiled defaults; enlighten should still work (parity with old behavior).
- **Concurrent enlighten**: Two players enlightening with different T should resolve different rows independently (read-only cache after load).

---

**Sign-off:** Complete sections 1–6 before shipping; use 7–8 for content updates; 9 as time permits.
