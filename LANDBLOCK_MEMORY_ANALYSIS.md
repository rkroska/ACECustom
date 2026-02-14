# Landblock Memory Management Analysis

## Overview
This document details the investigation into landblock memory management and the optimizations implemented to reduce memory footprint.

## System Architecture

### Key Components

#### 1. Landblock Dictionaries (LandblockManager.cs)
```csharp
// Line 41 - Master cache of ALL loaded landblocks
public static readonly ConcurrentDictionary<VariantCacheId, Landblock> landblocks;

// Line 46 - Currently active landblocks
public static readonly ConcurrentDictionary<VariantCacheId, Landblock> loadedLandblocks;

// Line 54 - Landblocks pending unload
private static readonly ConcurrentDictionary<VariantCacheId, Landblock> destructionQueue;
```

#### 2. Per-Landblock Collections (Landblock.cs)
```csharp
// Line 82 - All active world objects in landblock
private readonly ConcurrentDictionary<ObjectGuid, WorldObject> worldObjects;

// Line 88-91 - Sorted collections for performance
private readonly LinkedList<Creature> sortedCreaturesByNextTick;
private readonly LinkedList<WorldObject> sortedWorldObjectsByNextHeartbeat;
private readonly LinkedList<WorldObject> sortedGeneratorsByNextGeneratorUpdate;
private readonly LinkedList<WorldObject> sortedGeneratorsByNextRegeneration;

// Line 87 - Current players
public readonly List<Player> players;

// Line 102 - Adjacent landblock references
public List<Landblock> Adjacents;
```

## Landblock Lifecycle

### Loading
1. Player moves into new area or landblock is preloaded
2. `LandblockManager.GetLandblock()` called
3. New `Landblock` instance created if not cached
4. `.Init()` spawns async task to load world objects:
   - Static objects from database (buildings, NPCs, items)
   - Dynamic shard objects (corpses, player-created items)
   - Encounters (randomized outdoor monsters)
5. Objects added to `worldObjects` dictionary
6. Landblock added to `loadedLandblocks` and `landblocks`

### Active State
- Heartbeat every 5 seconds processes AI, physics, generators
- Objects maintained in sorted LinkedLists for efficient processing
- Adjacent landblocks loaded as needed for seamless world

### Dormancy
**Trigger:** 1 minute without players (line 141)
- AI ticking suppressed
- Physics processing reduced
- Still retained in memory

### Unloading
**Trigger:** 5 minutes of dormancy (line 146)
1. Added to `destructionQueue`
2. `UnloadLandblocks()` processes queue
3. `Landblock.Unload()` called:
   - Saves pending changes to database
   - Destroys all world objects
   - Clears `worldObjects` dictionary
   - Removes from `loadedLandblocks`
   - Removes from `landblocks` (line 688)

### Permaload
**Configuration:** Can be enabled per landblock
- Towns, dungeons, player housing areas
- Never enters destruction queue
- Remains loaded indefinitely

## Memory Issues Identified

### Issue 1: Collection References Not Cleared ⚠️
**Problem:** Collections in `Landblock` were not explicitly cleared on unload:
- `sortedCreaturesByNextTick`
- `sortedWorldObjectsByNextHeartbeat`
- `sortedGeneratorsByNextGeneratorUpdate`
- `sortedGeneratorsByNextRegeneration`
- `players`
- `Adjacents`

**Impact:**
- Held references to destroyed objects
- Created circular references between adjacent landblocks
- Delayed garbage collection
- Memory could grow during landblock churn (exploration, player movement)

**Fix Applied:**
```csharp
// In Landblock.Unload() - line 1381-1387
sortedCreaturesByNextTick.Clear();
sortedWorldObjectsByNextHeartbeat.Clear();
sortedGeneratorsByNextGeneratorUpdate.Clear();
sortedGeneratorsByNextRegeneration.Clear();
players.Clear();
Adjacents.Clear();
```

**Expected Impact:**
- Immediate release of object references
- Breaks circular reference chains
- Faster garbage collection of unloaded landblocks
- Reduced memory during high player movement

### Issue 2: Adjacent Landblock Graph (Potential)
**Observation:** Adjacent landblocks maintain references to each other
- Creates interconnected web of 9 landblocks (center + 8 adjacent)
- Even if center unloads, adjacents may hold reference

**Current Mitigation:** 
- Adjacents list now cleared on unload
- References are weak (List, not strong parent-child relationship)

**No Further Action Needed:** Clearing Adjacents list is sufficient

## What Was NOT an Issue

### Entity Framework Tracking ✅
**Initial Hypothesis:** EF change tracking causes memory duplication

**Reality:** 
- EF tracking is scoped to DbContext lifetime
- Contexts are short-lived (created and disposed within methods)
- Tracked entities released when context disposes
- Only cached copies persist long-term

**Lesson:** Understanding scope is critical when analyzing memory issues

### Landblock Dictionary Management ✅
**Verified:** 
- `landblocks` dictionary properly removes entries on unload (line 688)
- `loadedLandblocks` dictionary properly removes entries (line 686)
- No evidence of dictionary growing unbounded

### Unload Mechanism ✅
**Verified:**
- Unload triggers work correctly (1 min dormancy, 5 min until unload)
- World objects properly destroyed and removed
- Database saves occur before unload
- Physics objects properly cleaned up

## Performance Characteristics

### Memory per Landblock (Estimated)
- **Small landblock** (dungeon corridor): 100KB - 500KB
  - Few objects, simple geometry
- **Medium landblock** (outdoor area): 500KB - 2MB
  - Terrain, vegetation, creatures
- **Large landblock** (town, complex dungeon): 2MB - 10MB
  - Many objects, NPCs, buildings, items

### Typical Server Load
- **Low activity:** 20-50 loaded landblocks = 10-100MB
- **Medium activity:** 100-200 loaded landblocks = 50-400MB
- **High activity:** 300-500 loaded landblocks = 150-1GB

### Permaload Impact
- Preloaded landblocks: Can be 50-100+ landblocks
- Never unload, permanently in memory
- Can account for significant baseline: 100-500MB

## Recommendations for Further Optimization

### 1. Review Permaload Configuration
**Action:** Audit preloaded landblocks to ensure only essential areas are permaloaded

**Check:** `Config.js` - `PreloadedLandblocks` section
```json
{
  "Id": "E74EFFFF",
  "Description": "Hebian-To (Global Events)",
  "Permaload": true,
  "IncludeAdjacents": true,
  "Enabled": true
}
```

**Consider:**
- Are all permaloaded blocks still necessary?
- Can `IncludeAdjacents` be false for some?
- Can some blocks use dynamic loading instead?

### 2. Adjust Unload Timing (Cautiously)
**Current:** 5 minutes until unload

**Options:**
- Reduce to 3-4 minutes for low-population areas
- Keep 5+ minutes for high-traffic areas
- Dynamic adjustment based on server load

**Risk:** More frequent unload/reload cycles can impact performance

### 3. Monitor Landblock Statistics
**Add Metrics:**
- Count of loaded landblocks
- Memory per landblock (approximate)
- Landblock churn rate (loads/unloads per hour)
- Average dormancy time before unload

### 4. Optimize Static Object Loading
**Observation:** Each landblock loads all static objects from database

**Potential:** 
- Pre-cache static object templates
- Share immutable object data between landblocks
- Only instantiate necessary dynamic state

**Complexity:** High - requires significant refactoring

## Conclusion

The landblock management system is well-designed with proper unload mechanisms. The main issue identified was incomplete cleanup of collections on unload, which has been fixed.

The 28GB memory usage is likely due to:
1. **Cache sizes** - Entity caches, world object templates
2. **Permaload configuration** - Many permanently loaded landblocks
3. **Player/NPC object graphs** - Complex inventory hierarchies
4. **Physics/rendering data** - Geometry, textures, collision data

Further optimization should focus on:
- Auditing permaload configuration
- Reviewing cache eviction policies
- Monitoring actual memory usage patterns

The collection cleanup fix is a good defensive improvement that should help GC reclaim memory more efficiently.
