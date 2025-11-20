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
                if (kvp.Value == null || kvp.Value.CachedObject == null)
                {
                    removals.Add(kvp.Key);
                    continue;
                }

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
                    return cachedBiota.CachedObject;
                }
            }

            var biota = base.GetBiota(context, id);

            if (biota != null && !skipCache)
                TryAddToCache(biota);

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
                        InvalidateBiotaCache(biota.Id);
                        return true;
                    }

                    return false;
                }
            }

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
                    InvalidateBiotaCache(biota.Id);
                    return true;
                }

                return false;
            }
        }

        public override bool SaveBiotasInParallel(IEnumerable<(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock)> biotas)
        {
            var biotaList = biotas.ToList();
            var result = base.SaveBiotasInParallel(biotaList);

            if (result)
            {
                lock (biotaCacheMutex)
                {
                    foreach (var (biota, _) in biotaList)
                        biotaCache.Remove(biota.Id);
                }
            }

            return result;
        }

        public override bool RemoveBiota(uint id)
        {
            InvalidateBiotaCache(id);
            return base.RemoveBiota(id);
        }

        /// <summary>
        /// Invalidates the biota cache for the specified ID without removing it from the database.
        /// This is useful when we're about to save new data and want to prevent stale cache from being used.
        /// </summary>
        public void InvalidateBiotaCache(uint id)
        {
            lock (biotaCacheMutex)
                biotaCache.Remove(id);
        }
    }
}
