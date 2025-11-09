using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

using ACE.Database.Models.Shard;
using ACE.Entity;

namespace ACE.Database
{
    public class ShardDatabaseWithCaching : ShardDatabase
    {
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


        public override Biota GetBiota(ShardDbContext context, uint id, bool doNotAddToCache = false)
        {
            lock (biotaCacheMutex)
            {
                TryPerformMaintenance();

                if (biotaCache.TryGetValue(id, out var cachedBiota))
                {
                    cachedBiota.LastSeen = DateTime.UtcNow;

                    return cachedBiota.CachedObject;
                }
            }

            var biota = base.GetBiota(context, id);

            if (biota != null && !doNotAddToCache)
                TryAddToCache(biota);

            return biota;
        }

        public override Biota GetBiota(uint id, bool doNotAddToCache = false)
        {
            var shouldCache = ObjectGuid.IsPlayer(id) 
                ? PlayerBiotaRetentionTime > TimeSpan.Zero 
                : NonPlayerBiotaRetentionTime > TimeSpan.Zero;
            
            if (shouldCache)
            {
                using (var context = new ShardDbContext())
                {
                    var biota = GetBiota(context, id, doNotAddToCache);
                    return biota;
                }
            }

            return base.GetBiota(id, doNotAddToCache);
        }

        public override bool SaveBiota(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock)
        {
            CacheObject<Biota> cachedBiota;

            lock (biotaCacheMutex)
                biotaCache.TryGetValue(biota.Id, out cachedBiota);

            if (cachedBiota != null)
            {
                lock (biotaCacheMutex)
                    cachedBiota.LastSeen = DateTime.UtcNow;

                using (var context = new ShardDbContext())
                {
                    // Reload the biota from database with the new context to avoid concurrency issues
                    var existingBiota = base.GetBiota(context, biota.Id);
                    
                    if (existingBiota == null)
                        return false;

                    rwLock.EnterReadLock();
                    try
                    {
                        ACE.Database.Adapter.BiotaUpdater.UpdateDatabaseBiota(context, biota, existingBiota);
                    }
                    finally
                    {
                        rwLock.ExitReadLock();
                    }

                    var result = DoSaveBiota(context, existingBiota);
                    
                    // Update the cached copy with the fresh data (thread-safe)
                    if (result)
                    {
                        lock (biotaCacheMutex)
                            cachedBiota.CachedObject = existingBiota;
                    }
                    
                    return result;
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
                    TryAddToCache(existingBiota);

                    return true;
                }

                return false;
            }
        }

        public override bool RemoveBiota(uint id)
        {
            lock (biotaCacheMutex)
                biotaCache.Remove(id);

            return base.RemoveBiota(id);
        }
    }
}
