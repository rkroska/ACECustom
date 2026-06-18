# Pet Potency, Essence Residue, and Bond Strain

Design spec for **offensive pet progression** on bonded combat essences: currency farming, potency investment, bond-gated activation, body-part damage scaling, and optional player damage tradeoff (Bond Strain).

Companion docs: [BONDED_COMBAT_PETS.md](BONDED_COMBAT_PETS.md), [ADMIN_PET_SUMMON_CHARMS.md](ADMIN_PET_SUMMON_CHARMS.md), [WCID_ALLOCATION_7878.md](WCID_ALLOCATION_7878.md), **[SAVAGE_ECHO_PLAYER_GUIDE.md](SAVAGE_ECHO_PLAYER_GUIDE.md)**, **[SAVAGE_ECHO_DEVELOPER_GUIDE.md](SAVAGE_ECHO_DEVELOPER_GUIDE.md)**.

**Status:** Implemented (kill drops, spend, scaling, appraisal). Salvage deferred; Bond Strain off by default.

---

## Summary (player-facing)

- **Capture** still provides appearance + (for combat) damage type on essences.
- **Bond** (existing) grows from pet combat on **this essence** — survivability **and** existing bond damage ratings stay as-is.
- **Potency** (new) is bought with **Essence Residue** currency and stored on the bonded combat essence.
- **Active potency** = how much of your stored training actually applies in combat — limited by **bond** and a **hard cap**.
- **Essence Residue** drops when your pet helps kill monsters; drop chance = pet’s damage share on that kill. Salvaging spare captured essences gives a **smaller** supplement.
- **Bond Strain:** while a **combat pet is summoned**, you take a **damage rating penalty** that scales with active potency (off by default at launch; hook ships with feature).

---

## Design decisions (locked)

| Topic | Decision |
|-------|----------|
| Bond offense (DR/CD from bond level) | **Keep as-is** — stacks with potency body-part scaling; monitor balance via Strain + live tuning |
| Currency trade | **Tradeable**, but **only the bonded owner** may spend it on **their** attuned essence |
| Bond → offense gate | **Soft start:** `ceil(bond ÷ 10)` offense cap (bond 1 → cap 1, bond 11 → cap 2, bond 900 → cap 90) |
| Active potency hard cap | **150** max active levels (×4.0 body-part mult at 2%/level before bond/other modifiers) |
| Drop model | **Pure RNG** per kill: `P(drop) = petDamageShare`, award full tier amount on success |
| Salvage | **Supplement only** — less value per hour than T10 pet farming |
| Hollow essence (78780006) salvage | **Yes** — slightly less than normal siphoned essence (78780004) |
| Bond Strain timing | Applies whenever a **combat pet is spawned** (idle in town included) |

---

## Core concepts

### 1) Progress lives on the **combat Pet Device** (essence)

Same as bond today:

- **Stored Potency** — currency spent on this essence (permanent on device).
- **Bond Level / Bond XP** — earned by fighting with this essence (existing).
- **Active Potency** — computed at summon / on each damage roll; not stored.

Bond and potency are **per essence**, not per character. A veteran’s bond 900 on their main essence does **not** apply to a newly skinned second essence.

### 2) Active potency formula

```
bondOffenseCap = bondLevel <= 0 ? 0 : ceil(bondLevel / 10)
                 // implementation: (bondLevel + 9) / 10  for integer bondLevel >= 1

activePotency  = min(storedPotency, bondOffenseCap, pet_potency_active_cap)
                 // pet_potency_active_cap default 150

dormantPotency = storedPotency - activePotency   // paid but not yet unlocked by bond
```

**Soft start:** bond 1 with stored potency 100 → active **1**, not 0.

**Hard cap:** even at bond 1,500, active potency never exceeds **150** (configurable).

### 3) Damage application (body parts)

At `CombatPet.Init`, after loading the summon weenie:

```
mult = 1 + activePotency * (pet_potency_damage_per_level / 100)   // default 2% per level

For each PropertiesBodyPart on the pet:
    DVal = round(DVal * mult)   // scale all parts; DVar unchanged ratio to DVal behavior follows from pipeline
```

- Applies to **all** body parts (melee/missile/capture-skin paths use different parts — all scale).
- **Does not** override elemental matchup (Fire vs Gromnie vs Armoredillo).
- **Stacks with** existing bond DR/CD on the pet, gear ratings, summon augs, etc.

Reference hits (Fire pet, observed potency 0):

| Target | ~Hit today | ~Hit @ active 50 (×2) | ~Hit @ active 150 (×4) |
|--------|------------|------------------------|-------------------------|
| Azure Gromnie (T9, fire-weak) | 3,100 | 6,200 | 12,400 |
| Tainted Sea Mist (T10) | 4,000 | 8,000 | 16,000 |
| Cursed Armoredillo (T10, fire-resist) | 200 | 400 | 800 |

### 4) Essence Residue (currency item)

- **Stackable** tradeable item in pack.
- **WCID (proposed):** `78780013` (7878 spare block — confirm with `find_next_wcid_7878.sql` before SQL).
- **Spend:** use residue on **your** attuned combat essence (`PetBondAttunedCharacterId` must match).
- **Cannot** apply potency to another player’s essence or unbonded/tradeable blank devices.

---

## Earning currency

### Kill drops (primary)

On creature death, for each combat pet that contributed to `DamageHistory`:

```
petShare = petDamageOnMob / mobMaxHealth   // clamp 0..1, same family as bond XP share

if petShare <= 0 OR device not bond-attuned: skip

dropChance = petShare   // pure RNG; ServerConfig can apply global multiplier

if random() < dropChance:
    expected = baseAmountForTier(mob) * (shiny ? shinyMult : 1) * globalMult
    amount = probabilisticRound(expected)   // e.g. 0.35 → 35% chance of 1; 2.4 → 2 + 40% +1
    if amount > 0: award amount Savage Echo to pet owner
```

**Tier expected amount (defaults, tunable — fractional OK):**

| Loot tier | Default expected on successful drop |
|-----------|--------------------------------------|
| Below T9 / unknown | **0.3** |
| T9 (`DeathTreasureType` 3001 class) | **0.8** |
| T10 (3007 class) | **1.5** |

Use **`/modifydouble`** on `pet_residue_drop_*`. Values &lt; 1.0 mean “sometimes 1 echo” without cranking drop chance down to zero.

Map tier from creature `DeathTreasureType` / level band — exact mapping in implementation.

**Shiny:** multiply **expected amount** before rounding, not drop chance.

**Player-heavy farming:** at 430 kills/hr with 10% pet share on T10 → expected `430 × 0.10 × 1.5 ≈ 64.5` Savage Echo/hr (before shiny). Enable `pet_potency_debug_chat` to see `expected X.XX` vs actual award per kill.

### Salvage (supplement)

- **Normal siphoned essence** (78780004): salvage action → residue, value from captured creature level + shiny; **target ~40–60%** of equivalent farm hour on T9/T10.
- **Hollow essence** (78780006): same, **~75%** of normal salvage value.
- Cannot salvage an essence already applied to a device.
- Does **not** replace farming for serious potency goals.

---

## Spending currency (potency upgrades)

Cost to go from stored potency `L` → `L+1`:

```
cost(L → L+1) = pet_potency_cost_base * (L + 1)     // default cost_base = 20
```

Total currency to reach stored potency **N**:

```
total(N) = cost_base * N * (N + 1) / 2
```

Examples (`cost_base = 20`):

| Stored potency N | Total Savage Echo |
|------------------|-------------------|
| 25 | 6,500 |
| 50 | 25,500 |
| 100 | 101,000 |
| 150 | 226,500 |

**Use action:** target attuned combat essence with residue in pack → consume currency → increment `PetPotencyStored` → save device → appraise sync.

---

## Bond Strain (player tradeoff)

While `player.CurrentActivePet is CombatPet` and player is alive:

```
if !pet_strain_enabled: strain = 0
else if activePotency <= pet_strain_potency_threshold: strain = 0   // default threshold 50
else:
    strain = (activePotency - threshold) * pet_strain_per_potency_level   // default 1.0 DR per level

effectivePlayerDR = GetDamageRating() - strain   // hook at end of Player.GetDamageRating()
```

| Active potency | Strain (−DR) @ defaults | Player damage mult |
|----------------|-------------------------|---------------------|
| ≤ 50 | 0 | 100% |
| 75 | −25 | ~80% |
| 100 | −50 | ~67% |
| 125 | −75 | ~57% |
| 150 | −100 | ~50% |

**Lifecycle:** strain is computed per hit from live `CurrentActivePet` + device potency/bond — **no enchantment**. Pet death / stow / logout → `CurrentActivePet = null` → strain gone immediately.

**Default at launch:** `pet_strain_enabled = false` — enable when high potency pets warrant it.

---

## Scenario matrix

| Event | Residue drop? | Active potency | Strain |
|-------|---------------|----------------|--------|
| No pet out | — | — | Off |
| Combat pet summoned | — | From device | On if enabled + above threshold |
| Pet fighting, 30% damage share | 30% chance × tier base | Live | On |
| Pet dies | — | — | **Off** |
| Stow essence | — | — | **Off** |
| Logout | — | — | **Off** (pet destroyed) |
| Passive pet only | N/A | N/A | **Off** |
| Player dead, pet alive | — | — | **Off** (recommended) |
| Spend potency while pet out | — | Updates next hit | May increase |
| Bond level-up while pet out | — | May increase active | May increase strain |
| Wrong element mob | Low hit damage | Same mult applies | Same strain |

---

## Property & content IDs (proposed)

### Biota properties (persisted on item / player)

| What | Kind | ID | Writable | Assessment |
|------|------|-----|----------|------------|
| Stored potency on essence | `PropertyInt` | **9056** `PetPotencyStored` | Server (spend residue) | Yes — show stored/active/dormant |
| Bond fields (existing) | | 9047, 9050–9053 | Existing bond XP path | Yes |
| Essence Residue stack count | `Structure` / stack | on WCID | Loot / salvage / spend | Item name + count |

No separate property for **active potency** or **dormant** — computed at runtime from stored + bond + ServerConfig.

Optional future: `PropertyFloat` **9057** `PetResidueDropBank` on **player** if probabilistic rounding feels too streaky (not in v1).

### World content

| What | WCID | Notes |
|------|------|-------|
| Essence Residue (currency) | **78780013** | Confirm free in DB before SQL |
| Salvage: reuse essence use path or new `IsEssenceSalvageable` bool on 78780004/78780006 | — | Implementation choice |

Add enum + assessment in `PropertyInt.cs`, `AppraiseInfo.cs`, `PetDevice.cs`.

---

## Live tuning — ServerConfig registry

All knobs live in `PropertyManager.cs` beside existing `pet_bond_*` / `pet_combat_*` entries. Tune in-game with **`/modifybool`**, **`/modifylong`**, **`/modifydouble`** — no restart.

Implementation rule: **read ServerConfig at point of use**, not cached on item at creation (same pattern as `pet_combat_rating_mult_damage`).

### Master switches

| Property | Type | Default | Effect when changed |
|----------|------|---------|---------------------|
| `pet_potency_enabled` | bool | **false** | **Instant.** Gates drops, spend, body-part scaling, strain. Off = system inert. |
| `pet_residue_drops_enabled` | bool | **true** | **Instant.** Kill drops only (spend/salvage can stay on if desired). |
| `pet_residue_salvage_enabled` | bool | **true** | **Instant.** Salvage actions on 78780004/78780006. |
| `pet_strain_enabled` | bool | **false** | **Instant.** Player DR penalty while combat pet spawned. |

### Potency — damage & activation

| Property | Type | Default | Effect when changed |
|----------|------|---------|---------------------|
| `pet_potency_damage_per_level` | double | **0.02** | **Resummon pet.** +fraction of DVal per active level (0.02 = +2%/level). Clamped e.g. 0–0.25 server-side. |
| `pet_potency_active_cap` | long | **150** | **Resummon pet.** Hard max on active potency. 0 = treat as unlimited. |
| `pet_potency_bond_divisor` | long | **10** | **Resummon pet.** `bondOffenseCap = ceil(bond / divisor)`. Lower = bond unlocks offense faster. |
| `pet_potency_bond_offense_min_active` | long | **1** | **Resummon pet.** When stored > 0 and bond ≥ 1, active cap is at least this (soft start). Set 0 to allow zero active at bond 1. |
| `pet_potency_scale_dvar` | bool | **false** | **Resummon pet.** If true, also multiply body-part `DVar` by potency mult (wider hit variance). Default false = DVal only. |

### Potency — economy (spend cost)

| Property | Type | Default | Effect when changed |
|----------|------|---------|---------------------|
| `pet_potency_cost_base` | long | **20** | **Instant.** Next upgrade uses new cost; does not refund past spend. |
| `pet_potency_cost_exponent` | double | **1.0** | **Instant.** Cost for L→L+1 = `cost_base × (L+1)^exponent`. 1.0 = linear-quadratic default; 1.2 = steeper late game. |
| `pet_potency_max_stored` | long | **0** | **Instant.** Cap stored potency (0 = unlimited). Blocks new spends above cap. |

### Essence Residue — kill drops

| Property | Type | Default | Effect when changed |
|----------|------|---------|---------------------|
| `pet_residue_drop_t9` | double | **0.8** | **Instant.** Expected Savage Echo on successful T9 drop (probabilistically rounded). |
| `pet_residue_drop_t10` | double | **1.5** | **Instant.** Expected on successful T10 drop. |
| `pet_residue_drop_default` | double | **0.3** | **Instant.** Fallback when tier unknown / below T9. |
| `pet_residue_drop_chance_mult` | double | **1.0** | **Instant.** Multiplies pet damage share before RNG (1.0 = honest; 2.0 = double drop chance). |
| `pet_residue_drop_min_pet_share` | double | **0.0** | **Instant.** Skip drop roll if pet share below this (0.05 = 5% min). |
| `pet_residue_drop_max_pet_share` | double | **1.0** | **Instant.** Cap share used for drop chance (anti-exploit vs low-HP mobs). |
| `pet_residue_shiny_mult` | double | **5.0** | **Instant.** Multiplies drop **amount** when killed creature is shiny variant. |
| `pet_residue_global_mult` | double | **1.0** | **Instant.** Multiplies final awarded amount (event buffs / nerfs). |
| `pet_residue_require_bond_attuned` | bool | **true** | **Instant.** Only drop for pets from bond-attuned combat essences. |

**Tier mapping (implementation):** derive tier from creature `DeathTreasureType` (e.g. 3001→T9, 3007→T10) with optional override table in code; expose mis-map fixes via drop defaults above before adding per-tier longs.

### Essence Residue — salvage

| Property | Type | Default | Effect when changed |
|----------|------|---------|---------------------|
| `pet_residue_salvage_base` | long | **5** | **Instant.** Flat component of salvage yield. |
| `pet_residue_salvage_per_creature_level` | double | **0.05** | **Instant.** `base + floor(level × per_level)` before mults. |
| `pet_residue_salvage_mult` | double | **0.5** | **Instant.** Global salvage nerf (supplement vs farm). |
| `pet_residue_hollow_mult` | double | **0.75** | **Instant.** Hollow (78780006) × this vs normal salvage. |
| `pet_residue_salvage_shiny_mult` | double | **5.0** | **Instant.** From `CapturedCreatureVariant` on essence. |

### Bond Strain (player penalty)

| Property | Type | Default | Effect when changed |
|----------|------|---------|---------------------|
| `pet_strain_enabled` | bool | **false** | **Instant.** |
| `pet_strain_potency_threshold` | long | **50** | **Instant.** No strain at or below this **active** potency. |
| `pet_strain_per_potency_level` | double | **1.0** | **Instant.** −DR per active level above threshold. |
| `pet_strain_max_rating` | long | **0** | **Instant.** Cap strain magnitude (0 = no cap). |
| `pet_strain_while_player_dead` | bool | **false** | **Instant.** If false, no strain when player dead (recommended). |
| `pet_strain_combat_pet_only` | bool | **true** | **Instant.** Passive pets never apply strain. |

### Debug / ops (default off)

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `pet_potency_debug_chat` | bool | false | Owner chat on drop, spend, active potency change |
| `pet_potency_debug_log` | bool | false | Server log: drop rolls, salvage, spend costs |
| `pet_strain_debug_chat` | bool | false | Owner chat showing strain DR applied per hit (spammy) |

---

## Live tuning — admin quick reference

```bash
# Enable full system
/modifybool pet_potency_enabled true

# Double farm rate (drop chance, not amount)
/modifydouble pet_residue_drop_chance_mult 2.0

# Make potency 50 grind half the currency
/modifylong pet_potency_cost_base 5

# Soften bond gate (bond 500 → cap 100 instead of 50)
/modifylong pet_potency_bond_divisor 5

# Cap offense lower for a trial week
/modifylong pet_potency_active_cap 100

# Enable strain for high-potency players
/modifybool pet_strain_enabled true
/modifylong pet_strain_potency_threshold 50
/modifydouble pet_strain_per_potency_level 1.0

# Event: double residue weekend
/modifydouble pet_residue_global_mult 2.0
```

### What requires **resummon** vs **instant**

| Category | Resummon combat pet? | Why |
|----------|----------------------|-----|
| Damage per level, active cap, bond divisor | **Yes** | Applied in `CombatPet.Init` on body parts |
| Drop / salvage / spend costs | **No** | Next kill or next click |
| Bond Strain | **No** | Read each `GetDamageRating()` call |
| `pet_potency_enabled` false | **No** | Stops new behavior; existing pet may keep scaled parts until resummon — acceptable |

After changing damage/cap/divisor, broadcast: *“Resummon your combat pet for potency changes to apply.”*

---

## ServerConfig → formula wiring

Centralize in `PetDevice` (or static `PetPotency` helper):

```csharp
// Active potency (resummon + strain + appraisal)
static int GetBondOffenseCap(int bondLevel)
{
    if (bondLevel <= 0) return 0;
    var cap = (bondLevel + divisor - 1) / divisor;  // ceil(bond/divisor)
    if (storedPotency > 0 && bondLevel >= 1)
        cap = Math.Max(cap, (int)ServerConfig.pet_potency_bond_offense_min_active.Value);
    return cap;
}

static int GetActivePotency(PetDevice device)
{
    if (!ServerConfig.pet_potency_enabled.Value) return 0;
    var stored = device.PetPotencyStored ?? 0;
    var bond = device.PetBondLevel ?? 0;
    var cap = GetBondOffenseCap(bond);
    var activeCap = ServerConfig.pet_potency_active_cap.Value;
    if (activeCap > 0) cap = Math.Min(cap, (int)activeCap);
    return Math.Min(stored, cap);
}

static long GetUpgradeCost(int currentStored)
{
    var base_ = ServerConfig.pet_potency_cost_base.Value;
    var exp = ServerConfig.pet_potency_cost_exponent.Value;
    return (long)Math.Round(base_ * Math.Pow(currentStored + 1, exp));
}

static float GetBodyPartDamageMult(int activePotency)
{
    return 1.0f + activePotency * (float)ServerConfig.pet_potency_damage_per_level.Value;
}

static int GetBondStrainRating(Player player, PetDevice device)
{
    if (!ServerConfig.pet_strain_enabled.Value) return 0;
    if (player.IsDead && !ServerConfig.pet_strain_while_player_dead.Value) return 0;
    if (player.CurrentActivePet is not CombatPet) return 0;
    var active = GetActivePotency(device);
    var threshold = (int)ServerConfig.pet_strain_potency_threshold.Value;
    if (active <= threshold) return 0;
    var strain = (int)Math.Round((active - threshold) * ServerConfig.pet_strain_per_potency_level.Value);
    var max = (int)ServerConfig.pet_strain_max_rating.Value;
    if (max > 0) strain = Math.Min(strain, max);
    return strain;
}
```

---

## PropertyManager.cs placement (implementation note)

Add new `ConfigProperty` fields in `PropertyManager.cs` immediately after existing `pet_bond_*` block (~line 493) under comment:

`// Pet potency / essence residue / bond strain (custom)`

Mirror naming: `pet_potency_*`, `pet_residue_*`, `pet_strain_*` — consistent grep for admins.

---

## Tuning scenarios (cheat sheet)

| Goal | Knobs |
|------|-------|
| Potency 50 takes ~2× longer | ↑ `pet_potency_cost_base` or ↓ `pet_residue_drop_*` / `pet_residue_drop_chance_mult` |
| Veterans unlock offense faster | ↓ `pet_potency_bond_divisor` (10→5) |
| Cap endgame pet damage | ↓ `pet_potency_active_cap` or ↓ `pet_potency_damage_per_level` |
| Pet-only farmers earn more | ↑ `pet_residue_drop_chance_mult` or ↑ tier drop amounts |
| Reduce player+pet double DPS | ↑ `pet_strain_per_potency_level` or ↓ `pet_strain_potency_threshold` |
| Disable farm, test spend only | `pet_residue_drops_enabled false`, GM grant residue |
| Salvage not worth skipping farm | ↓ `pet_residue_salvage_mult` |

Expected residue/hr (T10, 430 kills/hr, 10% pet share):

```
430 × min(share, max_share) × drop_chance_mult × drop_t10 × global_mult
= 430 × 0.10 × 1.0 × 1.5 × 1.0 ≈ 64.5/hr
```

Hours to stored potency 50: `25500 / 64.5 ≈ 395 hr` at 10% share on T10 (before salvage). Full +100% damage also requires bond 491+ for 50 active.

---

## ServerConfig (legacy summary table)

See **Live tuning — ServerConfig registry** above for the full list. Core defaults unchanged from planning:

| Property | Type | Default |
|----------|------|---------|
| `pet_potency_enabled` | bool | false |
| `pet_potency_damage_per_level` | double | 0.02 |
| `pet_potency_active_cap` | long | 150 |
| `pet_potency_cost_base` | long | 20 |
| `pet_potency_bond_divisor` | long | 10 |
| `pet_residue_drop_default` / `t9` / `t10` | double | 0.3 / 0.8 / 1.5 |
| `pet_strain_enabled` | bool | false |
| `pet_strain_potency_threshold` | long | 50 |
| `pet_strain_per_potency_level` | double | 1.0 |


## Appraisal (player UX)

On **combat essence** (identify / assess):

```
Potency: 100 stored (75 active, 25 dormant)
Bond: 750
Body Training: +150% damage from potency (active)
Bond Strain: −25 damage rating while pet summoned   [if strain enabled and active]
```

On **Essence Residue** stack: count + “Use on your bonded combat essence to increase Potency.”

---

## Implementation touchpoints

| Area | File / hook |
|------|-------------|
| Residue drop on kill | `Creature_Death.cs` (parallel to bond XP loop) |
| Salvage use | New handler on 78780004 / 78780006 or dedicated salvage NPC |
| Spend residue → potency | `PetDevice` or `MonsterCapture`-adjacent use handler |
| Body-part scale | `CombatPet.Init` after weenie load |
| Active potency helper | `PetDevice.GetActivePotency()` |
| Bond Strain | `Player.GetDamageRating()` override path in `Creature_Rating.cs` |
| Appraisal | `AppraiseInfo.cs` |
| World SQL | `Database/Updates/World/…-Essence-Residue.sql` |
| WCID map | `WCID_ALLOCATION_7878.md` |

---

## Ship order (recommended)

1. **Property + residue item SQL** (no gameplay).
2. **Potency spend + body-part scaling** (strain off, drops off) — internal testing.
3. **Kill drops + salvage** — economy live.
4. **Appraisal strings**.
5. **Bond Strain** — enable after observing potency 50+ on live.

Do **not** remove bond DR/CD (per locked decision); watch combined DPS and enable strain when needed.

---

## Migration

- Existing bonded combat essences: `PetPotencyStored = 0`.
- No bond or potency reset.

---

## Player-facing patch note (draft)

“When you fight with a bonded combat pet, Essence Residue may drop — more likely when your pet contributed heavily to the kill. Spend residue on **your** attuned combat essence to raise **Potency** (stored training). **Active** potency is limited by your **bond** with that essence (every 10 bond levels unlock 1 active level, soft start at bond 1) and caps at 150. Active potency increases all of your pet’s body-part damage. Spare captured essences can be salvaged for a small amount of residue. At high active potency, **Bond Strain** may reduce your outgoing damage while your combat pet is summoned.”

---

## Related tuning reference (Schneeblytest logs, Jun 2026)

- Fire missile pet, potency 0: Gromnie ~3,100/hit; Wisp ~4,000/hit; Armoredillo ~200/hit.
- T10 farm ~430 kills/hr (player pace); pet share varies — 10% share ≈ 64.5 expected Savage Echo/hr at 1.5/T10 drop (launch defaults).
- Top bond levels on server ~675–1,109 within first month — bond divisor 10 + cap 150 required for offense gate.

Interactive charts: `canvases/bond-potency-damage-chart.canvas.tsx`, `canvases/pet-potency-calibrator.canvas.tsx`.
