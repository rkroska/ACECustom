# Savage Echo & Pet Potency — Player Guide

**What this is:** A long-term upgrade path for **bonded combat pets**. You earn **Savage Echo** crystals while your pet helps you fight, then spend them on **your** attuned combat essence to raise **Potency** — extra damage from body training.

**What this is not:** A replacement for bond level, capture skins, or gear on the essence. Bond still controls survivability and bond combat bonuses. Potency is an extra offensive layer on top.

---

## Quick start

1. **Bond** a combat pet essence to your character (existing bond system).
2. Fight with that pet summoned — Savage Echo may drop when your pet helps kill creatures.
3. Select **Savage Echo** in your pack → **Use** → click **your** bonded combat essence.
4. Each successful use raises **stored Potency** by 1 and consumes echoes from your pack.
5. **Resummon** your pet so the new damage applies.

The server must have potency enabled (`pet_potency_enabled` — admins turn this on).

---

## Savage Echo (currency)

| | |
|---|---|
| **Item name** | Savage Echo |
| **Looks like** | Stackable crystals in your inventory |
| **Tradeable?** | Yes — you can buy, sell, or trade stacks |
| **Who can spend it?** | Only **you**, and only on **your** bond-attuned **combat** essence |

You cannot apply Savage Echo to another player’s essence, a blank/unbonded essence, or a non-combat pet device.

---

## How you earn Savage Echo

### Fighting (main source)

When a creature dies, the server checks how much damage **your combat pet** did on that kill.

- The more your pet contributed, the **more likely** you are to get a drop on that kill.
- If your pet did almost none of the work, you probably get nothing.
- Your essence must be **bond-attuned** to you for kill drops to count.

**Better mobs tend to pay better** (by loot tier). High-tier hunting grounds are the intended farm; weak mobs pay very little.

Drops are partly random — you will not get an echo on every single kill even when your pet fights hard. Over many kills, the average evens out.

**Shiny creatures** (when they exist on the mob) can multiply the amount on a successful drop.

### Salvage (planned supplement)

The design includes salvaging spare **Siphoned** or **Hollow** captured essences for a smaller amount of Savage Echo. **This is not live yet** — check patch notes / ask staff if salvage has been added.

---

## How you spend Savage Echo

1. Put Savage Echo in your inventory.
2. **Use** the stack (same as “use on target” items like catalysts).
3. Click your **bonded combat pet essence**.
4. The server takes echoes from your pack and adds **+1 stored Potency** on that essence.

**Cost goes up each level** — the first point is cheapest; later points cost more echoes. You do not set the price in a shop; the server calculates it automatically.

After spending, **resummon your pet** to apply the new damage.

---

## Potency: stored, active, and dormant

Think of Potency like **gym training paid for in advance**, with **belt rank** deciding how much of that training you can use in a real fight.

| Term | Plain meaning |
|------|----------------|
| **Stored Potency** | Total training you have **paid for** on this essence (permanent on the item). |
| **Active Potency** | How much of that training **actually boosts damage right now**. |
| **Dormant Potency** | Training you already paid for but **cannot use yet** until your **bond** is high enough. |

### Bond unlocks active Potency

Rough rule: **every 2 bond levels unlocks 1 active Potency level**, with a minimum of **1 active** once you have any stored Potency and at least bond 1.

| Bond level | Max active Potency (approx.) |
|------------|------------------------------|
| 2 | 1 |
| 20 | 10 |
| 50 | 25 |
| 100 | 50 |
| 200 | 100 |
| 300+ | **150**, **200**, etc. (no hard cap by default) |

**Example:** You spend echoes until you have **50 stored**, but your bond is only **20**. You have **10 active** and **40 dormant**. Your pet gets the damage boost for 10 levels now; the other 40 "turn on" automatically as bond grows — you do not pay again.

### Damage boost

Each **active** Potency level adds about **+2% damage** to all of your pet’s body-part hits (default server setting).

| Active Potency | Extra pet damage |
|----------------|------------------|
| 10 | +20% |
| 25 | +50% |
| 50 | +100% (double damage) |
| 100 | +200% |
| 150 (cap) | +300% |

This scales **every** body part (melee, missile, etc.). It does **not** ignore resistances — fire-weak mobs still hurt more than fire-resistant ones.

Potency **stacks with** bond damage bonuses and gear on the essence. It does not replace them.

---

## Reading your essence (appraisal)

When you assess a bonded **combat** essence, you may see something like:

```text
Potency: 25 stored (10 active, 15 dormant)
Bond: 20
Body Training: +20% damage from potency (active)
```

If **Bond Strain** is enabled on the server (optional feature), high active Potency can show a line about reduced **your** damage rating while the combat pet is out. This is **off by default** on most launches.

---

## Per-essence progress

Bond and Potency live on the **essence item**, not on your character.

- A new skinned essence starts at **0** Potency even if your main essence is maxed.
- Bond on essence A does not help essence B.
- Plan which essence is your “main” before investing heavily.

---

## Tips

- **Resummon after training** — damage updates when the pet is created, not mid-fight.
- **Bond and farm together** — dormant Potency is not wasted; it unlocks as bond rises.
- **Pet needs to fight** — letting your character do all the damage means fewer echoes.
- **Higher tier hunting** — better expected echoes per successful drop than low-level mobs.
- **Trade echoes, train yourself** — echoes are tradeable; only the bond owner can apply them to that essence.

---

## FAQ

**Why didn’t I get Savage Echo on that kill?**  
Drop chance scales with pet damage share. Low contribution, unbonded essence, potency disabled server-side, or bad RNG.

**I spent echoes but damage didn’t change.**  
Check **active** not just **stored** on appraisal. Resummon the pet. Confirm `pet_potency_enabled` is on (ask staff).

**Can I power-level Potency on an alt’s essence?**  
No. Spend is locked to the character bonded to that essence.

**Is there a max?**  
Stored: unlimited by default (server can cap). Active: **150** cap at default settings.

**Does Potency work on passive pets?**  
No — **combat** pet essences only.

---

## Related guides

- [Bonded combat pets](BONDED_COMBAT_PETS.md) — bonding, XP, bond combat bonuses  
- [Monster capture](BONDED_COMBAT_PETS.md) — skins and damage type on essences  

*Numbers in this guide match launch defaults; staff may tune rates with server config without a client patch.*
