# Savage Echo & Pet Potency — Developer & Content Guide

**Audience:** Engineers, GMs, content designers, and anyone tuning the feature — **no programming required** for the first half; code map at the end for implementers.

**Deep spec:** [PET_POTENCY_AND_STRAIN.md](PET_POTENCY_AND_STRAIN.md)  
**Player-facing:** [SAVAGE_ECHO_PLAYER_GUIDE.md](SAVAGE_ECHO_PLAYER_GUIDE.md)  
**Bond system:** [BONDED_COMBAT_PETS.md](BONDED_COMBAT_PETS.md)

---

## ELI5 — the whole system in one page

Imagine each **combat essence** is a **fighter** with two separate progress bars:

1. **Bond** (already existed) — “How well do we trust each other?” Grows when you fight together. Unlocks survivability and bond combat stats. Also acts like a **belt rank** that limits how much offensive training you can *use*.

2. **Potency** (new) — “How hard have we trained this body?” You buy training with **Savage Echo** crystals. Training is **stored on the essence forever**. But you only **use** part of it in combat until bond is high enough.

**Savage Echo** is the currency. You get it when your **pet actually helps kill things**. You spend it by using the stack on **your** bonded combat essence — like applying a catalyst to a charm.

**Active vs dormant:** If you paid for 50 training sessions but your bond only allows 10, you have **10 active** (damage boost now) and **40 dormant** (paid for, unlocks later as bond rises). No refund, no re-grind.

**Damage:** Each active level adds ~2% to every body-part damage value on the summoned pet. Resummon after spending.

**Bond Strain** (optional, off by default): While your combat pet is out, *you* might take a damage-rating penalty if active Potency is very high — a tradeoff so pet+DPS doesn’t scale without limit.

---

## What is implemented (checklist)

| Feature | Status |
|---------|--------|
| Savage Echo item (WCID 78780013) + world SQL | ✅ |
| Kill drops with fractional tier amounts + probabilistic award | ✅ |
| Spend Savage Echo on bonded combat essence | ✅ |
| Stored Potency on essence (property 9056) | ✅ |
| Active/dormant computed from bond + cap | ✅ |
| Body-part damage scaling at summon | ✅ |
| Appraisal block on combat essences | ✅ |
| ServerConfig tuning (`pet_potency_*`, `pet_residue_*`) | ✅ |
| Bond Strain on player DR | ✅ (hook exists; **`pet_strain_enabled` default false**) |
| Salvage Siphoned/Hollow essence → Savage Echo | ❌ **Not implemented** (config exists for future) |
| Remove temp `[Potency-Use-TRACE]` debug logging | ⚠️ **Strip before production push** |

**Master switch:** `pet_potency_enabled` defaults to **false**. Nothing works until an admin sets it true.

---

## Player journey (content view)

```
Bond combat essence → summon pet → kill mobs
        ↓
Savage Echo drops (RNG, pet share × tier)
        ↓
Use Savage Echo on YOUR attuned combat essence
        ↓
Stored Potency +1, echoes consumed (rising cost curve)
        ↓
Resummon pet → body parts scaled by ACTIVE potency
        ↓
Bond level-ups gradually unlock more dormant → active
```

**Capture/skin** is unchanged — appearance and damage type still come from monster capture. Potency does not replace bond XP or bond DR/CD.

---

## The three potency numbers (content must understand)

| Name | Stored on item? | Shown on appraisal? | Meaning |
|------|-----------------|---------------------|---------|
| **Stored** | Yes (`PetPotencyStored`, int 9056) | Yes | Total echo spend on this essence |
| **Active** | No (computed) | Yes | `min(stored, bondCap, hardCap)` — drives damage |
| **Dormant** | No (computed) | Yes | `stored - active` — waiting on bond |

**Bond cap formula (default divisor 10):**

```
bondCap = ceil(bondLevel / 10)   // with soft-start: at least 1 active when stored > 0 and bond >= 1
active  = min(stored, bondCap, 150)
```

**Damage multiplier (default):**

```
mult = 1 + active × 0.02     // +2% per active level → 50 active = ×2.0 damage
```

Applied in `CombatPet.Init` to each body part’s `DVal` (and optionally `DVar` if `pet_potency_scale_dvar` is true).

---

## Savage Echo economy (launch defaults)

### Earning — kill drops

On each creature death, for each combat pet in the damage log:

1. Compute **pet share** = pet damage ÷ mob total health (0–100%).
2. Skip if share too low, device missing, or (default) essence not bond-attuned.
3. Roll RNG: succeed if `random < share × drop_chance_mult`.
4. On success, **expected amount** = tier amount × shiny mult × global mult.
5. **Probabilistic round** to whole echoes (e.g. 0.3 → 30% chance of 1 echo).

**Tier expected amounts (double — use `/modifydouble`):**

| Config | Default | When used |
|--------|---------|-----------|
| `pet_residue_drop_default` | **0.3** | Below T9 / unknown tier |
| `pet_residue_drop_t9` | **0.8** | Creature death treasure tier ≥ 9 |
| `pet_residue_drop_t10` | **1.5** | Tier ≥ 10 |

**Why fractional?** Integer-only drops forced a choice between “1 echo per success” (too fast) or very low drop chance (empty kills feel bad). Fractional tiers + rounding give smooth averages with frequent small wins.

### Spending — potency upgrades

```
cost to go from stored L → L+1 = cost_base × (L + 1)^exponent
```

| Config | Default |
|--------|---------|
| `pet_potency_cost_base` | **20** |
| `pet_potency_cost_exponent` | **1.0** |

**Total echoes to reach stored N:** `cost_base × N × (N + 1) / 2`

| Stored N | Total echoes @ base 20 |
|----------|-------------------------|
| 25 | 6,500 |
| 50 (+100% dmg if fully active) | 25,500 |
| 150 | 226,500 |

**Important:** Reaching stored 50 does **not** mean +100% damage unless bond ≥ ~491 (active cap 50). Content messaging should emphasize **active**, not stored alone.

### Rough farm sanity (T10, launch defaults)

Assumptions: 430 kills/hr, 100% pet share, every kill passes drop roll:

- Expected per success ≈ **1.5** echoes → ~645 echoes/hr  
- Stored 50 cost ≈ 25,500 echoes → ~**40 hours** of perfect pet-solo T10 (before bond gate on active)

At **10% pet share**, divide by ~10 (~6.4 echoes/hr → hundreds of hours for stored 50). Real play sits between depending on party style.

---

## Live tuning (GM / admin)

All via in-game modify commands — **no restart** for most knobs.

### Turn the system on

```
/modifybool pet_potency_enabled true
```

### Drop rate (how fast echoes enter the world)

```
/modifydouble pet_residue_drop_default 0.3
/modifydouble pet_residue_drop_t9 0.8
/modifydouble pet_residue_drop_t10 1.5
/modifydouble pet_residue_drop_chance_mult 1.0    # global drop chance scaler
/modifydouble pet_residue_global_mult 1.0         # amount scaler after tier
/modifydouble pet_residue_drop_min_pet_share 0.0  # e.g. 0.1 = pet must do 10%+ damage
```

### Spend cost (how fast echoes leave the economy)

```
/modifylong pet_potency_cost_base 20
/modifydouble pet_potency_cost_exponent 1.0
```

### Power caps

```
/modifydouble pet_potency_damage_per_level 0.02   # 0.02 = 2%/level; resummon pet
/modifylong pet_potency_active_cap 150            # resummon pet
/modifylong pet_potency_bond_divisor 10           # lower = bond unlocks active faster; resummon
```

### Debug during playtests

```
/modifybool pet_potency_debug_chat true   # chat on drop: "You receive 1 Savage Echo (expected 0.35)."
/modifybool pet_potency_debug_log true    # server log
```

### Disable farm, test spend only

```
/modifybool pet_residue_drops_enabled false
```

Then GM-grant WCID **78780013** for spend/balance testing.

### Tuning philosophy

| Goal | Knob |
|------|------|
| Slower overall progression | ↑ `cost_base` and/or ↓ tier doubles |
| Less empty kills | ↑ tier doubles (fractional), avoid crushing `drop_chance_mult` alone |
| Pet must contribute | ↑ `drop_min_pet_share` |
| Veterans unlock damage sooner | ↓ `bond_divisor` (e.g. 10 → 5) |
| Cap endgame pet damage | ↓ `active_cap` or ↓ `damage_per_level` |
| Nerf player+pet combined DPS | Enable `pet_strain_enabled` |

---

## Content & SQL reference

| Asset | ID / location |
|-------|----------------|
| Savage Echo (player name) | WCID **78780013** |
| Internal/code alias | Essence Residue |
| World SQL | `Database/Updates/World/2026-06-16-00-Essence-Residue.sql` |
| Stored Potency property | `PropertyInt.PetPotencyStored` = **9056** |
| Item use | `ItemUseable` SourceContainedTargetContained; `TargetType` Misc (128) |
| Weenie type | 44 (CraftTool — inherits Stackable use handlers) |

**Spend rules (for support tickets):**

- Source: Savage Echo stack in player inventory  
- Target: PetDevice, combat only, bond-attuned, `PetBondAttunedCharacterId` = player  
- Consumes echoes by WCID from pack (lowest stack rules per inventory code)

**Appraisal example block** (combat essence):

```
Potency: 25 stored (10 active, 15 dormant)
Body Training: +20% damage from potency (active)
```

---

## Bond Strain (optional live feature)

When `pet_strain_enabled` is true and active Potency > threshold (default 50):

```
player strain = (active - 50) × 1.0   // subtract from player GetDamageRating while combat pet summoned
```

Default **off**. Enable after observing high-potency pets on live. Document for players before turning on.

---

## Code map (programmers)

| Responsibility | Location |
|----------------|----------|
| Math (active, cost, rounding) | `Source/ACE.Server/Entity/PetPotencyMath.cs` |
| Drops, spend, appraisal text | `Source/ACE.Server/Entity/PetPotency.cs` |
| ServerConfig registry | `Source/ACE.Server/Managers/PropertyManager.cs` |
| Kill hook | `Source/ACE.Server/WorldObjects/Creature_Death.cs` |
| Use Savage Echo on essence | `Stackable.cs`, `CraftTool.cs`, `Player_Use.cs` (target bypass) |
| Potency on device | `PetDevice.cs` (`PetPotencyStored`, sync to client) |
| Damage apply | `CombatPet.cs` `Init` → `PetPotency.ApplyBodyPartPotencyScaling` |
| Bond strain | `Creature_Rating.cs` → `PetPotency.GetBondStrainRating` |
| Appraisal | `AppraiseInfo.cs` |
| Unit tests | `Source/ACE.Server.Tests/PetPotencyFormulaTests.cs` |

**Drop pipeline (code):** `TryAwardResidueOnKill` → `GetResidueDropAmountForCreature` → multiply shiny/global → `PetPotencyMath.RoundResidueDropAmount` → `TryAwardResidueToPlayer`.

**Spend pipeline:** `HandleActionUseWithTarget` → `CraftTool.HandleActionUseOnTarget` → `PetPotency.TrySpendResidueOnEssence`.

---

## QA scenarios

| Test | Expected |
|------|----------|
| Potency disabled | No drops, spend says not enabled |
| Use echo on unbonded essence | Error: must be bond-attuned |
| Use echo on another player’s bonded essence | Error: attuned to you only |
| Use echo on non-combat device | Error: combat pet only |
| Spend + resummon | Appraisal stored/active updates; pet hits harder |
| Bond up with dormant | Active rises without more spend |
| Low-tier mob farm | Low echo rate vs T10 |
| `pet_residue_drops_enabled false` | No drops; spend still works |
| Debug chat on | Drop lines show expected vs awarded |

---

## Player comms template (patch note)

> **Savage Echo & Potency** — While fighting with a **bonded combat pet**, you may receive **Savage Echo** crystals when your pet helps slay creatures (more contribution = better odds). Use Savage Echo on **your** attuned combat essence to raise **Potency** (stored training). **Active** Potency — what actually increases pet damage — is limited by your **bond** with that essence (~1 active level per 10 bond levels, up to 150). Resummon your pet after training. Savage Echo is tradeable; only the bond owner can apply it to that essence.

Adjust numbers in player guide if you change defaults before publish.

---

## Related

- [WCID_ALLOCATION_7878.md](WCID_ALLOCATION_7878.md) — ID block for 7878 custom content  
- [PET_POTENCY_AND_STRAIN.md](PET_POTENCY_AND_STRAIN.md) — full design spec and config tables  
