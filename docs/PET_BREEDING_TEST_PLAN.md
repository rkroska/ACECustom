# 🧪 ACE Server & Web Portal - Pet Breeding & Visualizer Master Test Plan

## Overview & Scope
This test plan covers the exhaustive verification of all sub-systems introduced in the `feature/pet-breeding-motel` branch. The scope includes the new in-game Pet Breeding Engine (Seedy Motel mechanics, Mendelian stat inheritance, mutation logic, and cooldowns), Summoned Combat Pet stat scaling integration, the Web Portal Single Page Application (SPA) with 3D WebGL visualizations, and the associated automated unit tests and admin commands.

---

## 📋 Section 1: In-Game Pet Breeding Engine (Seedy Motel & C# Server)

### Test Case 1.1: Seedy Motel Dance Ritual Trigger & Particle Effects
- [ ] Ensure two compatible pets are placed in the Seedy Motel breeding zone.
- [ ] Initiate the breeding sequence.
- [ ] Verify that the Dance Ritual animation plays correctly for both pets.
- [ ] Verify that the correct particle effects spawn during the ritual.
- [ ] Confirm no server errors or exceptions are thrown during the animation phase.

### Test Case 1.2: Alpha Stud Stamina (10 Daily Charges & Reset)
- [ ] Designate a pet as the Alpha Stud.
- [ ] Breed the Alpha Stud 10 consecutive times.
- [ ] Verify that each breed consumes exactly 1 stamina charge.
- [ ] Attempt an 11th breed and verify it is correctly rejected due to lack of stamina.
- [ ] Wait for the daily reset (or force reset via admin command).
- [ ] Verify that the Alpha Stud's stamina correctly resets to 10 charges.

### Test Case 1.3: Non-Alpha Donor Cooldown (4-Hour Timer)
- [ ] Breed a Non-Alpha Donor pet.
- [ ] Verify that a 4-hour cooldown timer is immediately applied to the donor.
- [ ] Attempt to breed the donor again before the 4 hours expire and verify rejection.
- [ ] Wait 4 hours (or advance server time).
- [ ] Verify the donor can successfully breed again after the cooldown expires.

### Test Case 1.4: Mendelian 55/45 Stat Inheritance & Package-Deal Coupling (No Ghost Mutations)
- [ ] Perform a breed between two pets with known, distinct stats.
- [ ] Verify the offspring inherits stats using the 55% (Primary) / 45% (Secondary) Mendelian split.
- [ ] Verify that coupled stats (Package-Deals) inherit together seamlessly.
- [ ] Check the database/server logs to ensure no unintended "Ghost Mutations" occur during standard inheritance.

### Test Case 1.5: Option A Per-Stat 20 Mutation Cap Enforcement
- [ ] Force a pet's specific stat to reach 20 mutations.
- [ ] Attempt to breed and trigger a mutation for that specific stat.
- [ ] Verify that the mutation does not exceed the cap of 20.
- [ ] Verify that normal inheritance still proceeds correctly without exceeding the cap.

### Test Case 1.6: Fixed Step-Size Mutation Boosts (+3 Dmg, +3 DR, +2 Crit, +200 HP)
- [ ] Trigger a damage mutation and verify the stat increases by exactly +3.
- [ ] Trigger a Damage Reduction (DR) mutation and verify an increase of exactly +3.
- [ ] Trigger a Critical Hit (Crit) mutation and verify an increase of exactly +2.
- [ ] Trigger a Health Points (HP) mutation and verify an increase of exactly +200.

### Test Case 1.7: Independent 2.0% Potency Mutation Track (Soft-Cap 1000, Hard-Cap 2000)
- [ ] Perform multiple breeds to trigger the independent Potency mutation (2.0% base chance).
- [ ] Verify Potency increases appropriately upon a successful mutation.
- [ ] Force Potency to 1000 (Soft-Cap) and verify diminishing returns or appropriate soft-cap logic applies.
- [ ] Force Potency to 2000 (Hard-Cap) and verify it cannot increase further under any circumstance.

### Test Case 1.8: Double Mutation Jackpot (0.3% Chance)
- [ ] Run a bulk simulation or manipulate RNG to hit the 0.3% Double Mutation Jackpot.
- [ ] Verify that exactly two stats mutate simultaneously.
- [ ] Verify that both mutations apply their correct Fixed Step-Size boosts.
- [ ] Verify UI or system chat correctly notifies the player of the jackpot.

### Test Case 1.9: Master 0x04 DAT Palette Assignment on Mutation
- [ ] Trigger a mutation during breeding.
- [ ] Verify that the offspring is correctly assigned the Master 0x04 DAT Palette.
- [ ] Confirm visually that the new palette reflects accurately on the pet's model in-game.

### Test Case 1.10: Inventory Placement & Full Inventory Ground Drop (Attunement)
- [ ] Complete a breeding cycle with open inventory slots.
- [ ] Verify the offspring pet item is placed directly into the player's inventory.
- [ ] Verify the pet item is correctly attuned to the breeder.
- [ ] Fill the player's inventory completely.
- [ ] Complete another breeding cycle.
- [ ] Verify the offspring pet item drops to the ground.
- [ ] Verify the ground-dropped pet retains correct attunement to the breeder.

---

## 📋 Section 2: Summoned Combat Pet Stat Scaling (CombatPet.cs)

### Test Case 2.1: Mutated Rating Application at Summon Time (Damage, DR, Crit, etc.)
- [ ] Summon a highly mutated combat pet.
- [ ] Verify the base Damage applies the mutated ratings properly.
- [ ] Verify Damage Reduction (DR) and Crit Chance reflect the inherited/mutated stats.
- [ ] Engage in combat to confirm actual damage output aligns with the calculated mutated ratings.

### Test Case 2.2: Mutated Vitality HP Boost Application
- [ ] Check the HP of an unmutated pet.
- [ ] Summon a pet with maxed Vitality/HP mutations.
- [ ] Verify the maximum HP pool reflects the +200 HP per mutation step increments accurately.

### Test Case 2.3: Potency Scaling (Spell Level & Body Part Damage Scaling)
- [ ] Summon a pet with high Potency.
- [ ] Verify that casted spell levels scale up correctly based on the Potency value.
- [ ] Verify that physical body part damage scales correctly according to Potency modifiers.

---

## 📋 Section 3: Web Portal SPA & 3D Visualizer (React & WebGL)

### Test Case 3.1: Pet Breeding Simulator Parity with C# Engine
- [ ] Input two parent pets into the Web Portal Breeding Simulator.
- [ ] Run the simulation.
- [ ] Verify the simulated offspring stats exactly match the expected Mendelian math from the C# Engine.
- [ ] Verify mutation probabilities (2.0% Potency, 0.3% Jackpot) are represented accurately in the SPA.

### Test Case 3.2: 3D WebGL Model Rendering & DAT Palette Swatch Display
- [ ] Load a pet profile on the Web Portal.
- [ ] Verify the 3D WebGL model renders correctly in the browser.
- [ ] Verify the 0x04 DAT Palette colors display accurately on the model.
- [ ] Verify the UI swatch correctly identifies and displays the HEX/RGB values of the applied palette.

### Test Case 3.3: Safe HTTP/HTTPS Clipboard Copy Fallback (http://76.237.151.184:5001)
- [ ] Access the portal via the non-HTTPS URL: `http://76.237.151.184:5001`.
- [ ] Attempt to copy a pet build/link to the clipboard.
- [ ] Verify the safe fallback copy mechanism works without throwing Secure Context (HTTPS) errors.
- [ ] Paste the copied text to verify data integrity.

### Test Case 3.4: Speed Curation & Texture Quality Scoring Engine
- [ ] Load a heavy pet model with high-resolution textures.
- [ ] Monitor load times and verify they fall within acceptable SPA performance thresholds.
- [ ] Check the Texture Quality Scoring Engine output in the portal UI.
- [ ] Verify the scoring engine correctly penalizes or rewards based on texture optimization standards.

---

## 📋 Section 4: Automated Unit Tests & Admin Commands

### Test Case 4.1: ACE.Server.Tests Unit Test Suite Execution
- [ ] Run the complete `ACE.Server.Tests` suite locally or via CI/CD.
- [ ] Verify all Pet Breeding related unit tests pass successfully.
- [ ] Check test coverage to ensure Mendelian inheritance and mutation logic are fully covered.
- [ ] Review logs for any flaky tests or warnings.

### Test Case 4.2: Admin Commands (@create, @set, @appraise)
- [ ] Use `@create` to spawn a custom pet item in-game.
- [ ] Use `@set` to manually modify a pet's mutation counts, stamina, and cooldown timers.
- [ ] Verify the `@set` command correctly clamps values to the defined caps (e.g., max 20 per stat).
- [ ] Use `@appraise` on a pet and verify the output accurately lists all hidden genetics, current mutations, Potency, and cooldown status.
