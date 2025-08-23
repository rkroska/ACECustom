using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using ACE.Database.Entity;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using System.Diagnostics;
using System.Linq;

namespace ACE.Database
{
    public class SerializedShardDatabase
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// This is the base database that SerializedShardDatabase is a wrapper for.
        /// </summary>
        public readonly ShardDatabase BaseDatabase;

        protected readonly Stopwatch stopwatch = new Stopwatch();

        private readonly BlockingCollection<Task> _queue = new BlockingCollection<Task>();
        private readonly BlockingCollection<Task> _readOnlyQueue = new BlockingCollection<Task>();

        private Thread _workerThread;
        private Thread _workerThreadReadOnly;

        internal SerializedShardDatabase(ShardDatabase shardDatabase)
        {
            BaseDatabase = shardDatabase;
        }

        public void Start()
        {
            _workerThread = new Thread(DoWork)
            {
                Name = "Serialized Shard Database - Saving"
            };
            _workerThreadReadOnly = new Thread(DoReadOnlyWork)
            {
                Name = "Serialized Shard Database - Reading"
            };
            _workerThread.Start();
            _workerThreadReadOnly.Start();
            stopwatch.Start();
        }

        public void Stop()
        {
            _queue.CompleteAdding();
            _readOnlyQueue.CompleteAdding();
            _workerThread.Join();
            _workerThreadReadOnly.Join();
        }

        public List<string> QueueReport()
        {
            return _queue.Select(x => x.AsyncState?.ToString() ?? "Unknown Task").ToList();
        }

        public List<string> ReadOnlyQueueReport()
        {
            return _readOnlyQueue.Select(x => x.AsyncState?.ToString() ?? "Unknown Task").ToList();
        }

        private void DoReadOnlyWork()
        {
            while (!_readOnlyQueue.IsAddingCompleted)
            {
                try
                {
                    Task t;

                    bool tasked = _readOnlyQueue.TryTake(out t);
                    try
                    {
                        if (!tasked)
                        {
                            // no task to process, continue
                            continue;
                        }   
                        t.Start();
                    }
                    catch (Exception e)
                    {
                        log.Error($"[DATABASE] DoReadOnlyWork task failed with exception: {e}");
                    }                   
                }
                catch (ObjectDisposedException)
                {
                    // the _queue has been disposed, we're good
                    break;
                }
                catch (InvalidOperationException)
                {
                    // _queue is empty and CompleteForAdding has been called -- we're done here
                    break;
                }
                catch (NullReferenceException)
                {
                    break;
                }
            }
        }
        private void DoWork()
        {
            while (!_queue.IsAddingCompleted)
            {
                try
                {

                    Task t;
                    bool tasked = _queue.TryTake(out t);

                    try
                    {
                        if (!tasked)
                        {
                            continue; // no task to process, continue
                        }
                        stopwatch.Restart();
                        t.Start();
                        if (t.AsyncState != null && (t.AsyncState.ToString().Contains("GetPossessedBiotasInParallel") ||
                                                        t.AsyncState.ToString().Contains("GetMaxGuidFoundInRange") ||
                                                        t.AsyncState.ToString().Contains("GetSequenceGaps") ||
                                                        t.AsyncState.ToString().Contains("GetInventoryInParallel") ||
                                                        t.AsyncState.ToString().Contains("GetCharacter")))
                        {
                            //continue on background thread
                        }
                        else
                        {
                            t.Wait();
                        }
                            
                        if (stopwatch.Elapsed.Seconds >= 5)
                        {
                            log.Error(
                                $"Task: {t.AsyncState?.ToString()} taken {stopwatch.ElapsedMilliseconds}ms, queue: {_queue.Count}");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[DATABASE] DoWork task failed with exception: {ex}");
                        // perhaps add failure callbacks?
                        // swallow for now.  can't block other db work because 1 fails.
                    }
                    
                }
                catch (ObjectDisposedException)
                {
                    // the _queue has been disposed, we're good
                    break;
                }
                catch (InvalidOperationException)
                {
                    // _queue is empty and CompleteForAdding has been called -- we're done here
                    break;
                }
                catch (NullReferenceException)
                {
                    break;
                }
            }
        }


        public int QueueCount => _queue.Count;

        public void GetCurrentQueueWaitTime(Action<TimeSpan> callback)
        {
            var initialCallTime = DateTime.UtcNow;

            _queue.Add(new Task((x) =>
            {
                callback?.Invoke(DateTime.UtcNow - initialCallTime);
            }, "GetCurrentQueueWaitTime"));
        }


        /// <summary>
        /// Will return uint.MaxValue if no records were found within the range provided.
        /// </summary>
        public void GetMaxGuidFoundInRange(uint min, uint max, Action<uint> callback)
        {
            _readOnlyQueue.Add(new Task((x) =>
            {
                var result = BaseDatabase.GetMaxGuidFoundInRange(min, max);
                callback?.Invoke(result);
            }, "GetMaxGuidFoundInRange: " + min));
        }

        /// <summary>
        /// This will return available id's, in the form of sequence gaps starting from min.<para />
        /// If a gap is just 1 value wide, then both start and end will be the same number.
        /// </summary>
        public void GetSequenceGaps(uint min, uint limitAvailableIDsReturned, Action<List<(uint start, uint end)>> callback)
        {
            _readOnlyQueue.Add(new Task((x) =>
            {
                var result = BaseDatabase.GetSequenceGaps(min, limitAvailableIDsReturned);
                callback?.Invoke(result);
            }, "GetSequenceGaps: " + min));
        }


        public void SaveBiota(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock, Action<bool> callback)
        {
            _queue.Add(new Task((x) =>
            {
                var result = BaseDatabase.SaveBiota(biota, rwLock);
                callback?.Invoke(result);
            }, "SaveBiota: " + biota.Id));
        }


        public void SaveBiotasInParallel(IEnumerable<(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock)> biotas, Action<bool> callback, string sourceTrace)
        {
            _queue.Add(new Task((x) =>
            {
                var result = BaseDatabase.SaveBiotasInParallel(biotas);
                callback?.Invoke(result);
            }, "SaveBiotasInParallel " + sourceTrace));
        }

        public void RemoveBiota(uint id, Action<bool> callback)
        {
            _queue.Add(new Task((x) =>
            {
                var result = BaseDatabase.RemoveBiota(id);
                callback?.Invoke(result);
            }, "RemoveBiota: " + id));
        }

        public void RemoveBiota(uint id, Action<bool> callback, Action<TimeSpan, TimeSpan> performanceResults)
        {
            var initialCallTime = DateTime.UtcNow;

            _queue.Add(new Task( (x) =>
            {
                var taskStartTime = DateTime.UtcNow;
                var result = BaseDatabase.RemoveBiota(id);
                var taskCompletedTime = DateTime.UtcNow;
                callback?.Invoke(result);
                performanceResults?.Invoke(taskStartTime - initialCallTime, taskCompletedTime - taskStartTime);
            }, "RemoveBiota2:" + id));
        }

        public void RemoveBiotasInParallel(IEnumerable<uint> ids, Action<bool> callback, Action<TimeSpan, TimeSpan> performanceResults)
        {
            var initialCallTime = DateTime.UtcNow;

            _queue.Add(new Task((x) =>
            {
                var taskStartTime = DateTime.UtcNow;
                var result = BaseDatabase.RemoveBiotasInParallel(ids);
                var taskCompletedTime = DateTime.UtcNow;
                callback?.Invoke(result);
                performanceResults?.Invoke(taskStartTime - initialCallTime, taskCompletedTime - taskStartTime);
            }, "RemoveBiotasInParallel: " +ids.Count()));
        }


        public void GetPossessedBiotasInParallel(uint id, Action<PossessedBiotas> callback)
        {
            _readOnlyQueue.Add(new Task((x) =>
            {
                var c = BaseDatabase.GetPossessedBiotasInParallel(id);
                callback?.Invoke(c);
            }, "GetPossessedBiotasInParallel: " + id));
        }

        public void GetInventoryInParallel(uint parentId, bool includedNestedItems, Action<List<Biota>> callback)
        {
            _readOnlyQueue.Add(new Task((x) =>
            {
                var c = BaseDatabase.GetInventoryInParallel(parentId, includedNestedItems);
                callback?.Invoke(c);
            }, "GetInventoryInParallel: " + parentId));

        }


        public void IsCharacterNameAvailable(string name, Action<bool> callback)
        {
            _readOnlyQueue.Add(new Task((x) =>
            {
                var result = BaseDatabase.IsCharacterNameAvailable(name);
                callback?.Invoke(result);
            }, "IsCharacterNameAvailable: " + name));
        }

        public void GetCharacters(uint accountId, bool includeDeleted, Action<List<Character>> callback)
        {
            _readOnlyQueue.Add(new Task((x) =>
            {
                var result = BaseDatabase.GetCharacters(accountId, includeDeleted);
                callback?.Invoke(result);
            }, "GetCharacters: " + accountId ));
        }

        public void GetLoginCharacters(uint accountId, bool includeDeleted, Action<List<LoginCharacter>> callback)
        {
            _readOnlyQueue.Add(new Task((x) =>
            {
                var result = BaseDatabase.GetCharacterListForLogin(accountId, includeDeleted);
                callback?.Invoke(result);
            }, "GetCharacterListForLogin: " + accountId));
        }

        public void GetCharacter(uint characterId, Action<Character> callback)
        {
            _readOnlyQueue.Add(new Task((x) =>
            {
                var result = BaseDatabase.GetCharacter(characterId);
                callback?.Invoke(result);
            }, "GetCharacter: " + characterId));
        }

        public Character GetCharacterSynchronous(uint characterId)
        {
            return BaseDatabase.GetCharacter(characterId);            
        }
        
        public void SaveCharacter(Character character, ReaderWriterLockSlim rwLock, Action<bool> callback)
        {
            _queue.Add(new Task((x) =>
            {
                var result = BaseDatabase.SaveCharacter(character, rwLock);
                callback?.Invoke(result);
            }, "SaveCharacter: " + character.Id));
        }

        public bool SaveCharacterSynchronous(Character character, ReaderWriterLockSlim rwLock)
        {
            return BaseDatabase.SaveCharacter(character, rwLock);
        }

        public void RenameCharacter(Character character, string newName, ReaderWriterLockSlim rwLock, Action<bool> callback)
        {
            _queue.Add(new Task((x) =>
            {
                var result = BaseDatabase.RenameCharacter(character, newName, rwLock);
                callback?.Invoke(result);
            }, "RenameCharacter: " + character.Id));
        }

        public void SetCharacterAccessLevelByName(string name, AccessLevel accessLevel, Action<uint> callback)
        {
            // TODO
            throw new NotImplementedException();
        }


        public void AddCharacterInParallel(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim biotaLock, IEnumerable<(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock)> possessions, Character character, ReaderWriterLockSlim characterLock, Action<bool> callback)
        {
            _queue.Add(new Task((x) =>
            {
                var result = BaseDatabase.AddCharacterInParallel(biota, biotaLock, possessions, character, characterLock);
                callback?.Invoke(result);
            }, "AddCharacterInParallel: " + character.Id));
        }
    }
}
