# 🧪 ACE Server & Web Portal - Pet Breeding & Visualizer Master Test Plan

This document contains **exact step-by-step instructions** with in-game admin commands (`@create`, `@set`, `@appraise`), expected chat messages, and web portal verification steps. Check off each box (`[x]`) as you test!

---

## 📋 Section 1: In-Game Pet Breeding Engine (Seedy Motel & C# Server)

### Test 1.1: Seedy Motel Breeding Ritual Trigger & Animation
- [ ] **Step 1**: Log in two characters (Player A and Player B) or use 2 accounts in the game client.
- [ ] **Step 2**: Teleport both characters inside the Seedy Motel (`@teleport 0x00000000` or portal).
- [ ] **Step 3**: Give Player A an Alpha Stud pet device and Player B a Non-Alpha pet device.
- [ ] **Step 4**: Have both players target each other's pet and type `/dance` in chat.
- [ ] **Verification**: Confirm `WeddingBliss` particles play on both parents, `VisionUpWhite` plays on both players, and local chat announces: *"Congratulations! A baby pet has been born!"*

---

### Test 1.2: Alpha Stud Stamina (10 Daily Charges)
- [ ] **Step 1**: Target an Alpha Stud pet device in inventory.
- [ ] **Step 2**: Run `@appraise` on the device and verify chat output shows `Role: [Alpha Stud] (10 / 10 Daily Charges)`.
- [ ] **Step 3**: Perform 10 consecutive breeds with different donors.
- [ ] **Step 4**: Run `@appraise` after 10 breeds and confirm charges drop to `(0 / 10 Daily Charges)`.
- [ ] **Step 5**: Attempt an 11th breed with the Alpha Stud.
- [ ] **Verification**: Confirm transient error message appears: *"[Device Name] has exhausted its 10 daily Alpha Breeding Charges. Rest for 24h or use an Alpha Stamina Tonic."*

---

### Test 1.3: Non-Alpha Donor Cooldown (4-Hour Timer)
- [ ] **Step 1**: Breed a Non-Alpha Donor pet device once.
- [ ] **Step 2**: Run `@appraise` on the donor device immediately after breeding.
- [ ] **Verification**: Confirm chat output shows `Role: [Non-Alpha Donor] (Cooldown: 3h 59m remaining)`.
- [ ] **Step 3**: Attempt to breed the same donor pet again right away.
- [ ] **Verification**: Confirm breeding fails with message: *"Non-Alpha Donor is on cooldown."*
- [ ] **Step 4** *(Admin Fast-Forward)*: Run `@set Float PetNextBreedingTime 0` on the donor device.
- [ ] **Verification**: Run `@appraise` again and confirm donor status resets to `Role: [Non-Alpha Donor] (Ready to breed)`.

---

### Test 1.4: Mendelian 55/45 Stat Inheritance & Package-Deal Coupling
- [ ] **Step 1**: Spawn Parent A with high damage rating: `@create 25749` then `@set Int DamageRating 30`.
- [ ] **Step 2**: Spawn Parent B with low damage rating: `@create 25749` then `@set Int DamageRating 10`.
- [ ] **Step 3**: Perform 10 breeds between Parent A and Parent B.
- [ ] **Step 4**: Run `@appraise` on all 10 birthed baby devices.
- [ ] **Verification**: Confirm roughly 5–6 babies inherit Parent A's high stat (30 Damage) and 4–5 inherit Parent B's low stat (10 Damage). Confirm mutation counts are coupled 1-to-1 with the inherited stat (no ghost mutation counts like 30+10=40).

---

### Test 1.5: Per-Stat 20 Mutation Cap Enforcement
- [ ] **Step 1**: Create a pet device with 20 Damage mutations: `@set Int PetMutDamageRating 60` and `@set Int DamageRating 60`.
- [ ] **Step 2**: Breed this pet repeatedly until a stat mutation rolls.
- [ ] **Verification**: Confirm Damage Rating never exceeds 60 (+60 bonus at 20 cap). Any new stat mutation rolls onto eligible uncapped stats (DR, Crit, or HP).

---

### Test 1.6: Fixed Step-Size Mutation Boosts (+3 Dmg, +3 DR, +2 Crit, +200 HP)
- [ ] **Step 1**: Set server config to force mutations on every breed: `@setconfig pet_breeding_force_mutation true`.
- [ ] **Step 2**: Perform 5 breeds and appraise the babies.
- [ ] **Verification**: Confirm stat boosts are strictly fixed step sizes:
  - Damage Rating boost is ALWAYS **+3**
  - Damage Resist Rating boost is ALWAYS **+3**
  - Crit Rating boost is ALWAYS **+2**
  - Vitality / Health boost is ALWAYS **+200 HP**
- [ ] **Step 3**: Reset force mutation config: `@setconfig pet_breeding_force_mutation false`.

---

### Test 1.7: Independent 2.0% Potency Mutation Track (Soft-Cap 1000, Hard-Cap 2000)
- [ ] **Step 1**: Spawn a pet device with 980 Potency: `@set Int PetPotencyStored 980`.
- [ ] **Step 2**: Breed until a Potency mutation rolls (or set `pet_breeding_potency_chance` high for testing).
- [ ] **Verification**: Confirm Potency increases by **+20** up to 1,000.
- [ ] **Step 3**: Set pet Potency to 1,500: `@set Int PetPotencyStored 1500`.
- [ ] **Step 4**: Trigger a Potency mutation above 1,000 Potency.
- [ ] **Verification**: Confirm diminishing step applies (+5 Potency per hit above 1,000). Confirm hard cap prevents Potency from ever exceeding 2,000.

---

### Test 1.8: Double Mutation Jackpot (0.3% Chance)
- [ ] **Step 1**: Perform breeding until both normal stat and Potency mutations hit on the same breed.
- [ ] **Verification**: Confirm server console logs `🌟 [DOUBLE MUTATION JACKPOT!]` and baby gains both a stat mutation (+3 Dmg / +200 HP) AND a Potency mutation (+20 Potency) simultaneously!

---

### Test 1.9: Master 0x04 DAT Palette Assignment on Mutation
- [ ] **Step 1**: Perform a breed that triggers a color mutation.
- [ ] **Step 2**: Run `@appraise` on the baby pet device.
- [ ] **Verification**: Confirm `PaletteBase` is assigned a master 2,048-color DAT Palette ID starting with `0x04......` (e.g. `0x040001BE`), and the pet's 3D mesh renders with the rare mutated color palette!

---

### Test 1.10: Inventory Placement & Full Inventory Drop (Attunement)
- [ ] **Step 1**: Ensure player has empty inventory space and complete a breed.
- [ ] **Verification**: Confirm baby pet device appears in inventory and is marked attuned.
- [ ] **Step 2**: Fill inventory completely with pyreals/items so 0 slots remain.
- [ ] **Step 3**: Complete another breed.
- [ ] **Verification**: Confirm chat message states inventory was full, baby pet drops to the ground at player's feet, and the ground item is correctly attuned to the player.

---

## 📋 Section 2: Summoned Combat Pet Stat Scaling (CombatPet.cs)

### Test 2.1: Mutated Rating Application at Summon Time
- [ ] **Step 1**: Take a pet device with +15 Damage Rating and +10 Crit Rating in inventory.
- [ ] **Step 2**: Use the pet device to summon the active combat pet into the world.
- [ ] **Step 3**: Run `@appraise` on the active summoned combat pet entity.
- [ ] **Verification**: Confirm combat pet's active stats include the +15 Damage Rating and +10 Crit Rating bonuses!

---

### Test 2.2: Mutated Vitality HP Boost Application
- [ ] **Step 1**: Take a pet device with +1,000 Vitality (+1,000 HP from 5 HP mutations).
- [ ] **Step 2**: Use the device to summon the pet.
- [ ] **Verification**: Confirm summoned pet's Max HP (`Health.MaxValue`) is 1,000 points higher than an unmutated pet of the same level!

---

### Test 2.3: Potency Scaling in Combat
- [ ] **Step 1**: Summon a pet with 1,000 Potency.
- [ ] **Step 2**: Have the pet attack an enemy creature.
- [ ] **Verification**: Confirm pet spell tier / body part damage scales up according to `PetPotency.cs` formulas.

---

## 📋 Section 3: Web Portal SPA & 3D Visualizer (React & WebGL)

### Test 3.1: Pet Breeding Simulator Parity
- [ ] **Step 1**: Open Web Portal at `http://localhost:5001` or `http://76.237.151.184:5001`.
- [ ] **Step 2**: Navigate to **Pet Breeding Calculator** tab.
- [ ] **Step 3**: Click **Simulate Breeding Ritual**.
- [ ] **Verification**: Confirm terminal debug log displays step-by-step stat inheritance, 55/45 rolls, mutation checks, and DAT palette resolution matching C# server output.

---

### Test 3.2: 3D WebGL Model Preview & Palette Swatches
- [ ] **Step 1**: Select any birthed baby in the Breeding Simulator results list.
- [ ] **Verification**: Confirm 3D WebGL viewport renders the creature model with active rotation controls and displays color swatch HEX codes.

---

### Test 3.3: HTTP Safe Clipboard Copy Fallback
- [ ] **Step 1**: Open web portal over plain HTTP IP address: `http://76.237.151.184:5001`.
- [ ] **Step 2**: Click **Copy @create In-Game Command** or **Copy Full Debug Log**.
- [ ] **Verification**: Confirm green checkmark appears, text copies to system clipboard, and **NO browser console error** (`navigator.clipboard is undefined`) is thrown!

---

## 📋 Section 4: Automated Unit Tests & Admin Commands

### Test 4.1: Unit Test Suite Execution
- [ ] **Step 1**: Open PowerShell terminal in `Source` directory.
- [ ] **Step 2**: Run `dotnet test ACE.Server.Tests\ACE.Server.Tests.csproj --filter "FullyQualifiedName~PetBreedingTests"`.
- [ ] **Verification**: Confirm all 21 unit tests pass (`Passed: 21, Failed: 0`).

---

### Test 4.2: Admin Commands Verification
- [ ] **Step 1**: Execute `@create 25749 1 0x040001BE 0.5` in-game.
- [ ] **Verification**: Confirm pet device spawns with specified WCID and 0x04 DAT Palette ID!
