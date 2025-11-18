using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

using log4net;

using ACE.Database.Models.Shard;
using ACE.Entity;
using ACE.Entity.Enum.Properties;

namespace ACE.Database
{
    public class ShardDatabaseWithCaching : ShardDatabase
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public TimeSpan PlayerBiotaRetentionTime { get; set; }
        public TimeSpan NonPlayerBiotaRetentionTime { get; set; }

        public ShardDatabaseWithCaching(TimeSpan playerBiotaRetentionTime, TimeSpan nonPlayerBiotaRetentionTime)
        {
            PlayerBiotaRetentionTime = playerBiotaRetentionTime;
            NonPlayerBiotaRetentionTime = nonPlayerBiotaRetentionTime;
        }


        private class CacheObject<T>
        {
            public DateTime LastSeen;
            public T CachedObject;
        }

        private readonly object biotaCacheMutex = new object();

        private readonly Dictionary<uint, CacheObject<Biota>> biotaCache = new Dictionary<uint, CacheObject<Biota>>();

        private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromMinutes(1);

        private DateTime lastMaintenanceInterval;

        /// <summary>
        /// Make sure this is called from within a lock(biotaCacheMutex)
        /// </summary>
        private void TryPerformMaintenance()
        {
            if (lastMaintenanceInterval + MaintenanceInterval > DateTime.UtcNow)
                return;

            var removals = new Collection<uint>();

            foreach (var kvp in biotaCache)
            {
                if (ObjectGuid.IsPlayer(kvp.Key))
                {
                    if (kvp.Value.LastSeen + PlayerBiotaRetentionTime < DateTime.UtcNow)
                        removals.Add(kvp.Key);
                }
                else
                {
                    if (kvp.Value.LastSeen + NonPlayerBiotaRetentionTime < DateTime.UtcNow)
                        removals.Add(kvp.Key);
                }
            }

            foreach (var removal in removals)
                biotaCache.Remove(removal);

            lastMaintenanceInterval = DateTime.UtcNow;
        }

        private void TryAddToCache(Biota biota)
        {
            lock (biotaCacheMutex)
            {
                if (ObjectGuid.IsPlayer(biota.Id))
                {
                    if (PlayerBiotaRetentionTime > TimeSpan.Zero)
                        biotaCache[biota.Id] = new CacheObject<Biota> {LastSeen = DateTime.UtcNow, CachedObject = biota};
                }
                else if (NonPlayerBiotaRetentionTime > TimeSpan.Zero)
                    biotaCache[biota.Id] = new CacheObject<Biota> {LastSeen = DateTime.UtcNow, CachedObject = biota};
            }
        }

        public List<uint> GetBiotaCacheKeys()
        {
            lock (biotaCacheMutex)
                return biotaCache.Keys.ToList();
        }


        public override Biota GetBiota(ShardDbContext context, uint id, bool skipCache = false)
        {
            lock (biotaCacheMutex)
            {
                TryPerformMaintenance();

                if (biotaCache.TryGetValue(id, out var cachedBiota))
                {
                    cachedBiota.LastSeen = DateTime.UtcNow;

                    // DEBUG: Check ContainerId in cached biota
                    uint? cachedContainerId = null;
                    if (cachedBiota.CachedObject.BiotaPropertiesIID != null)
                    {
                        var containerProp = cachedBiota.CachedObject.BiotaPropertiesIID.FirstOrDefault(p => p.Type == (ushort)PropertyInstanceId.Container);
                        if (containerProp != null)
                            cachedContainerId = containerProp.Value;
                    }

                    log.Debug($"[CACHE DEBUG] Returning cached biota {id} (0x{id:X8}) | Cached ContainerId={cachedContainerId} (0x{(cachedContainerId ?? 0):X8})");

                    return cachedBiota.CachedObject;
                }
            }

            var biota = base.GetBiota(context, id);

            if (biota != null && !skipCache)
            {
                // DEBUG: Check ContainerId in biota loaded from database
                uint? dbContainerId = null;
                if (biota.BiotaPropertiesIID != null)
                {
                    var containerProp = biota.BiotaPropertiesIID.FirstOrDefault(p => p.Type == (ushort)PropertyInstanceId.Container);
                    if (containerProp != null)
                        dbContainerId = containerProp.Value;
                }

                log.Debug($"[CACHE DEBUG] Loaded biota {id} (0x{id:X8}) from database | DB ContainerId={dbContainerId} (0x{(dbContainerId ?? 0):X8}) | Adding to cache");

                TryAddToCache(biota);
            }

            return biota;
        }

        public override Biota GetBiota(uint id, bool skipCache = false)
        {
            if (ObjectGuid.IsPlayer(id))
            {
                if (PlayerBiotaRetentionTime > TimeSpan.Zero)
                {
                    using (var context = new ShardDbContext())
                    {
                        var biota = GetBiota(context, id, skipCache); // This will add the result into the caches
                        return biota;
                    }
                }
            }
            else if (NonPlayerBiotaRetentionTime > TimeSpan.Zero)
            {
                using (var context = new ShardDbContext())
                {
                    var biota = GetBiota(context, id, skipCache); // This will add the result into the caches
                    return biota;
                }
            }

            return base.GetBiota(id, skipCache);
        }

        // Caller is responsible for holding this `rwLock` the entire time the biota is being read from
        // or written back to the shard database. It keeps the world-thread mutations (inventory updates,
        // physics ticks, etc.) from racing with the serialization work done here so we don't stamp over
        // in-flight changes or capture a half-mutated object graph.
        public override bool SaveBiota(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock)
        {
            CacheObject<Biota> cachedBiota;

            lock (biotaCacheMutex)
                biotaCache.TryGetValue(biota.Id, out cachedBiota);

            if (cachedBiota != null)
            {
                cachedBiota.LastSeen = DateTime.UtcNow;

                using (var context = new ShardDbContext())
                {
                    var existingBiota = base.GetBiota(context, biota.Id);

                    rwLock.EnterReadLock();
                    try
                    {
                        if (existingBiota == null)
                        {
                            existingBiota = ACE.Database.Adapter.BiotaConverter.ConvertFromEntityBiota(biota);
                            context.Biota.Add(existingBiota);
                        }
                        else
                        {
                            ACE.Database.Adapter.BiotaUpdater.UpdateDatabaseBiota(context, biota, existingBiota);
                        }
                    }
                    finally
                    {
                        rwLock.ExitReadLock();
                    }

                    if (DoSaveBiota(context, existingBiota))
                    {
                        // Invalidate cache entry - Entity Framework objects may have stale data
                        // after SaveChanges() without explicit reload. Removing from cache ensures
                        // next load will fetch fresh data from database.
                        lock (biotaCacheMutex)
                        {
                            biotaCache.Remove(biota.Id);
                        }
                        
                        log.Debug($"[CACHE DEBUG] Invalidated cache entry for biota {biota.Id} (0x{biota.Id:X8}) after save - will reload from database on next access");
                        
                        return true;
                    }

                    return false;
                }
            }

            // Biota does not exist in the cache
            using (var context = new ShardDbContext())
            {
                var existingBiota = base.GetBiota(context, biota.Id);

                rwLock.EnterReadLock();
                try
                {
                    if (existingBiota == null)
                    {
                        existingBiota = ACE.Database.Adapter.BiotaConverter.ConvertFromEntityBiota(biota);

                        context.Biota.Add(existingBiota);
                    }
                    else
                    {
                        ACE.Database.Adapter.BiotaUpdater.UpdateDatabaseBiota(context, biota, existingBiota);
                    }
                }
                finally
                {
                    rwLock.ExitReadLock();
                }

                if (DoSaveBiota(context, existingBiota))
                {
                    // Don't cache immediately after save - Entity Framework objects may have stale data
                    // after SaveChanges() without explicit reload. Next load will fetch fresh data.
                    log.Debug($"[CACHE DEBUG] Saved biota {biota.Id} (0x{biota.Id:X8}) not in cache - will be cached on next load from database");
                    
                    return true;
                }

                return false;
            }
        }

        public override bool SaveBiotasInParallel(IEnumerable<(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock)> biotas)
        {
            var biotaList = biotas.ToList();
            
            // Call base implementation to save to database
            var result = base.SaveBiotasInParallel(biotas);
            
            if (result)
            {
                // Invalidate cache entries for all saved biotas
                // Entity Framework objects may have stale data after SaveChanges() without explicit reload
                lock (biotaCacheMutex)
                {
                    foreach (var (biota, _) in biotaList)
                    {
                        if (biotaCache.Remove(biota.Id))
                        {
                            log.Debug($"[CACHE DEBUG] Invalidated cache entry for biota {biota.Id} (0x{biota.Id:X8}) after batch save - will reload from database on next access");
                        }
                    }
                }
            }
            
            return result;
        }

        public override bool RemoveBiota(uint id)
        {
            lock (biotaCacheMutex)
                biotaCache.Remove(id);

            return base.RemoveBiota(id);
        }
    }
}
