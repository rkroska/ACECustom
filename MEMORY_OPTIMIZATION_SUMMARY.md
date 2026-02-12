# Memory Optimization Summary

## Problem Statement
The ACE server was experiencing severe memory growth, eventually reaching ~28GB and causing throttling issues on the VM. The investigation revealed redundant object cloning in the database caching layer.

## Root Cause Identified

### Unnecessary Object Cloning in Treasure Material Caches
**Impact:** Moderate memory waste during cache initialization

Treasure material cache initialization was cloning objects unnecessarily:
- `CacheAllTreasureMaterialBase()` cloned every treasure material record
- `CacheAllTreasureMaterialColor()` cloned every color record  
- `CacheAllTreasureMaterialGroups()` cloned every material group record
- Cloning was originally added to avoid modifying EF-tracked entities during normalization
- Since normalization modifies probability values, the clone seemed necessary

## Solution Implemented

### Use .AsNoTracking() for Treasure Material and Quest Caches
**Files Modified:**
- `Source/ACE.Database/WorldDatabaseWithEntityCache.cs` (lines 942, 1067, 1142, 1218)
- `Source/ACE.Database/Extensions/WorldDbExtensions.cs` (DELETED - no longer needed)

**Changes:**
```csharp
// Before
var results = context.TreasureMaterialBase
    .Where(i => i.Probability > 0).ToList();
chances.Add(result.Clone());

// After
var results = context.TreasureMaterialBase.AsNoTracking()
    .Where(i => i.Probability > 0).ToList();
chances.Add(result);  // No clone needed
```

**Result:** 
- Eliminated 3 Clone() operations per treasure material record
- Reduced memory allocations during cache initialization
- Simplified code by removing unnecessary extension methods

## What About Biota Caching?

### Initial Attempt - AsNoTracking on Biota Queries (REVERTED)
We initially tried adding `.AsNoTracking()` to all `GetBiota()` queries, thinking this would eliminate duplicate memory usage from EF change tracking. However, this caused severe performance issues:

**Problems Encountered:**
- Very slow logins
- Very slow saves
- General performance degradation

**Why This Failed:**
The `GetBiota()` method is used in TWO different contexts:
1. **Reading for caching** - Where AsNoTracking would be beneficial
2. **Reading for updating** - Where tracking is REQUIRED

When `SaveBiota()` calls `GetBiota()` to load an existing entity:
- With tracking: EF knows about the entity and can efficiently update it
- With AsNoTracking: EF must attach the entity to the context, which is much slower

**The Key Insight:**
The biota caching in `ShardDatabaseWithCaching` already prevents long-term memory duplication:
1. `GetBiota()` loads the entity with tracking into a context
2. The result is cached in memory
3. The context is disposed, releasing the tracked entities
4. Only the cached copy remains in long-term memory

The EF change tracker only holds entities for the duration of the context lifetime (typically very short). Once the context is disposed, those tracked entities are eligible for garbage collection.

### Why We Don't Need AsNoTracking for Biotas

The memory issue we were trying to solve doesn't actually exist in the way we thought:

**Misconception:** EF tracking keeps entities in memory indefinitely
**Reality:** EF tracking is scoped to the DbContext lifetime

```csharp
// In ShardDatabaseWithCaching.GetBiota()
using (var context = new ShardDbContext())  // Context created
{
    var biota = base.GetBiota(context, id);  // Entity loaded and tracked
    // ... caching logic ...
}  // Context disposed - tracked entities released from EF change tracker

// Only the cached copy remains in memory
```

The context is short-lived (created and disposed within the method), so the tracking overhead is temporary and doesn't contribute to the 28GB memory growth.

## Expected Memory Savings

### Conservative Estimates from Treasure Material Optimization
- **During cache initialization:** Reduced temporary allocations by eliminating clones
- **Ongoing:** Slightly reduced memory footprint in treasure material caches
- **Code quality:** Cleaner, more maintainable code

### Why No Major Memory Reduction?
The 28GB memory usage is likely caused by other factors:
1. **Large number of cached entities** - The cache itself holds the data
2. **Static caches without eviction** - Some caches grow unbounded
3. **Landblock data** - World data kept in memory
4. **Player inventories** - Complex object graphs

The treasure material optimization is still valuable, but won't dramatically reduce the 28GB footprint. Further investigation into cache sizes, eviction policies, and landblock management would be needed for major memory reductions.

## Additional Opportunities (Not Implemented)

The following issues were identified but not addressed in this PR:

### 1. Unbounded Static Caches
**Files:** `Pet.cs`, `WorldObject_Magic.cs`, `EmoteManager.cs`
- Static dictionaries grow without limits
- Recommend: Add LRU eviction policy or size limits

### 2. Excessive List Materializations
**File:** `RecipeManager.cs` (lines 826-831)
- Recipe requirement checks materialize 6 lists per call
- Recommend: Use `FirstOrDefault()` or `Any()` instead of `ToList()`

### 3. String Concatenation in Loops
**File:** `WorldManager.cs`
- String concatenation in foreach loops creates many intermediate strings
- Recommend: Use `StringBuilder` for multi-line string building

## Lessons Learned

### Performance vs Memory Trade-offs
Adding `.AsNoTracking()` to queries seems like a pure win for memory, but:
- It changes EF behavior in subtle ways
- It can break update scenarios
- It can actually hurt performance in update-heavy code paths

### Context Lifetime Matters
Understanding DbContext lifetime is crucial:
- Short-lived contexts don't cause long-term memory issues from tracking
- Long-lived contexts (anti-pattern) would benefit from AsNoTracking
- The ACE codebase correctly uses short-lived contexts

### When to Use AsNoTracking
Use `.AsNoTracking()` when:
- ✅ Query results are read-only and never updated
- ✅ Results are cached or displayed without modification
- ✅ No relationships need to be loaded (or use Include() before AsNoTracking())
- ❌ NOT when the entity will be updated in the same or related context
- ❌ NOT when you need change tracking for audit/history purposes

## Conclusion

This PR implements a focused optimization for treasure material caching by eliminating unnecessary cloning. The initial attempt to optimize biota queries with AsNoTracking was reverted due to performance issues.

The key takeaway is that the EF change tracker in this codebase is not a significant source of long-term memory usage due to proper context management. The 28GB memory issue likely stems from the size and retention policies of the various caches themselves, not from EF tracking overhead.

Further memory optimization should focus on:
1. Cache eviction policies
2. Cache size limits
3. Reducing the amount of data loaded into caches
4. Better management of landblock and world data

