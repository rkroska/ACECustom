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

        // Given a task, returns the string key that uniquely identifies it, or throws an exception if a string key does not exist.
        private static string GetUniqueTaskKey(Task t)
        {
            string key = t.AsyncState as string;
            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("UniqueQueue keying failed: Task.AsyncState must be a non-null, non-empty string.");
            }
            return key;
        }

        /// <summary>
        /// This is the base database that SerializedShardDatabase is a wrapper for.
        /// </summary>
        public readonly ShardDatabase BaseDatabase;

        protected readonly Stopwatch stopwatch = new Stopwatch();

        private readonly BlockingCollection<Task> _readOnlyQueue = new BlockingCollection<Task>();
        private readonly UniqueQueue<Task, string> _uniqueQueue = new(t => GetUniqueTaskKey(t));
        private bool _workerThreadRunning = true;

        // Parallel read processing
        private readonly int _readOnlyThreadCount;
        private Thread[] _readOnlyWorkerThreads;

        private Thread _workerThread;

        internal SerializedShardDatabase(ShardDatabase shardDatabase)
        {
            BaseDatabase = shardDatabase;
            
            // Configure thread count based on CPU cores, capped between 4 and 16
            // This allows read operations to scale with hardware while preventing thread explosion
            _readOnlyThreadCount = Math.Clamp(Environment.ProcessorCount, 4, 16);
        }

        public void Start()
        {
            // Start multiple read-only worker threads for parallel processing
            _readOnlyWorkerThreads = new Thread[_readOnlyThreadCount];
            for (int i = 0; i < _readOnlyThreadCount; i++)
            {
                _readOnlyWorkerThreads[i] = new Thread(DoReadOnlyWork)
                {
                    Name = $"Serialized Shard Database - Reading [{i + 1}/{_readOnlyThreadCount}]"
                };
                _readOnlyWorkerThreads[i].Start();
            }

            _workerThread = new Thread(DoSaves)
            {
                Name = "Serialized Shard Database - Character Saves"
            };

            _workerThread.Start();
            stopwatch.Start();
            
            log.Info($"[DATABASE] Started {_readOnlyThreadCount} parallel read-only worker threads");
        }

        public void Stop()
        {
            _workerThreadRunning = false;
            _readOnlyQueue.CompleteAdding();
            
            // Wait for all read-only threads to complete
            // Null-check in case Stop() is called before Start()
            if (_readOnlyWorkerThreads != null)
            {
                foreach (var thread in _readOnlyWorkerThreads)
                {
                    // Null-check each thread and ensure it's alive before joining
                    if (thread != null && thread.IsAlive)
                    {
                        thread.Join();
                    }
                }
            }
            
            // Null-check and ensure worker thread is alive before joining
            if (_workerThread != null && _workerThread.IsAlive)
            {
                _workerThread.Join();
            }
        }

        public List<string> QueueReport()
        {
            return _uniqueQueue.ToArray().Select(t => t.AsyncState as string ?? $"Task#{t.Id}").ToList();
        }

        public List<string> ReadOnlyQueueReport()
        {
            return _readOnlyQueue.Select(t => t.AsyncState as string ?? $"Task#{t.Id}").ToList();
        }

        private void DoReadOnlyWork()
        {
            while (!_readOnlyQueue.IsAddingCompleted)
            {
                try
                {
                    Task t;

                    // Use blocking TryTake with timeout to avoid busy-waiting
                    // This allows thread to sleep when no work is available
                    if (!_readOnlyQueue.TryTake(out t, 100))
                    {
                        // No task available within timeout, continue to check completion status
                        continue;
                    }
                    
                    if (t == null)
                    {
                        log.Warn("[DATABASE] DoReadOnlyWork received null task, skipping");
                        continue;
                    }

                    try
                    {
                        t.Start();
                    }
                    catch (ObjectDisposedException ex)
                    {
                        log.Error($"[DATABASE] DoReadOnlyWork task failed to start (task was disposed): {ex.Message}");
                    }
                    catch (InvalidOperationException ex)
                    {
                        log.Error($"[DATABASE] DoReadOnlyWork task failed to start (task may have already been started): {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[DATABASE] DoReadOnlyWork task failed with unexpected exception: {ex.Message}");
                        log.Error($"[DATABASE] Stack trace: {ex.StackTrace}");
                    }
                }
                catch (ObjectDisposedException)
                {
                    // the _readOnlyQueue has been disposed, we're good
                    break;
                }
                catch (InvalidOperationException)
                {
                    // _readOnlyQueue is empty and CompleteForAdding has been called -- we're done here
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Timeout occurred, continue to check completion status
                    continue;
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] DoReadOnlyWork unexpected exception in main loop: {ex.Message}");
                    log.Error($"[DATABASE] Stack trace: {ex.StackTrace}");
                    
                    // Brief sleep to prevent tight error loop
                    try
                    {
                        Thread.Sleep(100);
                    }
                    catch
                    {
                        // If we can't even sleep, break out
                        break;
                    }
                }
            }
        }
        private void DoSaves()
        {
            while (_workerThreadRunning || _uniqueQueue.Count > 0)
            {
                try
                {
                    if (_uniqueQueue.Count == 0)
                    {
                        Thread.Sleep(10); //thread sleep to avoid busy waiting
                        continue;
                    }
                    Task t = _uniqueQueue.Dequeue();

                    try
                    {
                        if (t == null)
                        {
                            continue; // no task to process, continue
                        }
                        stopwatch.Restart();
                        t.Start();

                        t.Wait();
                        

                        if (stopwatch.ElapsedMilliseconds >= 5000)
                        {
                            log.Error(
                                $"Task: {t.AsyncState as string ?? $"Task#{t.Id}"} taken {stopwatch.ElapsedMilliseconds}ms, queue: {_uniqueQueue.Count}");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[DATABASE] DoCharacterSaves task failed with exception: {ex}");
                        // perhaps add failure callbacks?
                        // swallow for now.  can't block other db work because 1 fails.
                    }

                }
                catch (ObjectDisposedException)
                {
                    // the _uniqueQueue has been disposed, we're good
                    break;
                }
                catch (InvalidOperationException)
                {
                    // _uniqueQueue is empty and CompleteForAdding has been called -- we're done here
                    if(!_workerThreadRunning)
                    {
                        log.Info("[DATABASE] DoSaves: No more tasks to process, exiting.");
                        break;
                    }
                    else
                    {
                        log.Warn("[DATABASE] DoSaves: Queue is empty but worker thread is still running.");
                        continue; // keep waiting for more tasks
                    }
                }
                catch (NullReferenceException)
                {
                    break;
                }
            }
        }


        public int QueueCount => _uniqueQueue.Count;

        public void GetCurrentQueueWaitTime(Action<TimeSpan> callback)
        {
            var initialCallTime = DateTime.UtcNow;
            // Use unique key per request to prevent callback dropping
            var taskId = $"GetCurrentQueueWaitTime:{initialCallTime.Ticks}";

            _uniqueQueue.Enqueue(new Task((x) =>
            {
                callback?.Invoke(DateTime.UtcNow - initialCallTime);
            }, taskId));
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
            _uniqueQueue.Enqueue(new Task((x) =>
            {
                var result = BaseDatabase.SaveBiota(biota, rwLock);
                callback?.Invoke(result);
            }, "SaveBiota: " + biota.Id));
        }


        public void SaveBiotasInParallel(IEnumerable<(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock)> biotas, Action<bool> callback, string sourceTrace)
        {
            _uniqueQueue.Enqueue(new Task((x) =>
            {
                var result = BaseDatabase.SaveBiotasInParallel(biotas);
                callback?.Invoke(result);
            }, "SaveBiotasInParallel " + sourceTrace));
        }

        public void RemoveBiota(uint id, Action<bool> callback)
        {
            _uniqueQueue.Enqueue(new Task((x) =>
            {
                var result = BaseDatabase.RemoveBiota(id);
                callback?.Invoke(result);
            }, "RemoveBiota: " + id));
        }

        public void RemoveBiota(uint id, Action<bool> callback, Action<TimeSpan, TimeSpan> performanceResults)
        {
            var initialCallTime = DateTime.UtcNow;

            _uniqueQueue.Enqueue(new Task( (x) =>
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
            // Create a deterministic key so repeated calls for the same set coalesce
            var idKey = "RemoveBiotasInParallel:" + string.Join(",", ids.OrderBy(i => i));

            _uniqueQueue.Enqueue(new Task((x) =>
            {
                var taskStartTime = DateTime.UtcNow;
                var result = BaseDatabase.RemoveBiotasInParallel(ids);
                var taskCompletedTime = DateTime.UtcNow;
                callback?.Invoke(result);
                performanceResults?.Invoke(taskStartTime - initialCallTime, taskCompletedTime - taskStartTime);
            }, idKey));
        }


        public void GetPossessedBiotasInParallel(uint id, Action<PossessedBiotas> callback)
        {
            if (callback == null)
            {
                log.Error($"[DATABASE] GetPossessedBiotasInParallel called with null callback for character 0x{id:X8}");
                return;
            }

            try
            {
                _readOnlyQueue.Add(new Task((x) =>
                {
                    PossessedBiotas result = null;
                    bool loadSucceeded = false;
                    
                    try
                    {
                        result = BaseDatabase.GetPossessedBiotasInParallel(id);
                        loadSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[DATABASE] GetPossessedBiotasInParallel task failed for character 0x{id:X8}: {ex.Message}");
                        log.Error($"[DATABASE] Stack trace: {ex.StackTrace}");
                        result = new PossessedBiotas(new List<Biota>(), new List<Biota>());
                    }
                    
                    // Invoke callback exactly once with either successful result or empty fallback
                    try
                    {
                        callback.Invoke(result);
                    }
                    catch (Exception callbackEx)
                    {
                        log.Error($"[DATABASE] GetPossessedBiotasInParallel callback invocation failed for character 0x{id:X8}: {callbackEx.Message}");
                    }
                }, "GetPossessedBiotasInParallel: " + id));
            }
            catch (InvalidOperationException ex)
            {
                log.Error($"[DATABASE] Failed to enqueue GetPossessedBiotasInParallel task for 0x{id:X8} (queue may be completing): {ex.Message}");
                
                // Try to invoke callback with empty data exactly once
                try
                {
                    callback.Invoke(new PossessedBiotas(new List<Biota>(), new List<Biota>()));
                }
                catch (Exception callbackEx)
                {
                    log.Error($"[DATABASE] Fallback callback invocation failed: {callbackEx.Message}");
                }
            }
        }

        public void GetInventoryInParallel(uint parentId, bool includedNestedItems, Action<List<Biota>> callback)
        {
            if (callback == null)
            {
                log.Error($"[DATABASE] GetInventoryInParallel called with null callback for parent 0x{parentId:X8}");
                return;
            }

            try
            {
                _readOnlyQueue.Add(new Task((x) =>
                {
                    List<Biota> result = null;
                    bool loadSucceeded = false;
                    
                    try
                    {
                        result = BaseDatabase.GetInventoryInParallel(parentId, includedNestedItems);
                        loadSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[DATABASE] GetInventoryInParallel task failed for parent 0x{parentId:X8}: {ex.Message}");
                        log.Error($"[DATABASE] Stack trace: {ex.StackTrace}");
                        result = new List<Biota>();
                    }
                    
                    // Invoke callback exactly once with either successful result or empty fallback
                    try
                    {
                        callback.Invoke(result);
                    }
                    catch (Exception callbackEx)
                    {
                        log.Error($"[DATABASE] GetInventoryInParallel callback invocation failed for parent 0x{parentId:X8}: {callbackEx.Message}");
                    }
                }, "GetInventoryInParallel: " + parentId));
            }
            catch (InvalidOperationException ex)
            {
                log.Error($"[DATABASE] Failed to enqueue GetInventoryInParallel task for 0x{parentId:X8} (queue may be completing): {ex.Message}");
                
                // Try to invoke callback with empty list exactly once
                try
                {
                    callback.Invoke(new List<Biota>());
                }
                catch (Exception callbackEx)
                {
                    log.Error($"[DATABASE] Fallback callback invocation failed: {callbackEx.Message}");
                }
            }
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
            _uniqueQueue.Enqueue(new Task((x) =>
            {
                var result = BaseDatabase.SaveCharacter(character, rwLock);
                callback?.Invoke(result);
            }, "SaveCharacter: " + character.Id));
        }

        public void RenameCharacter(Character character, string newName, ReaderWriterLockSlim rwLock, Action<bool> callback)
        {
            _uniqueQueue.Enqueue(new Task((x) =>
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
            _uniqueQueue.Enqueue(new Task((x) =>
            {
                var result = BaseDatabase.AddCharacterInParallel(biota, biotaLock, possessions, character, characterLock);
                callback?.Invoke(result);
            }, "AddCharacterInParallel: " + character.Id));
        }

        /// <summary>
        /// Queues offline player saves to be processed in the background
        /// </summary>
        public void QueueOfflinePlayerSaves(Action<bool> callback = null)
        {
            // Use a stable key so multiple enqueues collapse to the most recent
            var taskId = "OfflinePlayerSaves";
            _uniqueQueue.Enqueue(new Task((x) =>
            {
                var success = false;
                var startTime = DateTime.UtcNow;
                try
                {
                    // Import the PlayerManager to avoid circular dependencies
                    var playerManagerType = Type.GetType("ACE.Server.Managers.PlayerManager, ACE.Server");
                    if (playerManagerType == null)
                    {
                        log.Warn("[DATABASE] PlayerManager type not found; offline save skipped.");
                    }
                    else
                    {
                        // Call the existing SaveOfflinePlayersWithChanges method directly
                        var saveMethod = playerManagerType.GetMethod(
                            "PerformOfflinePlayerSaves",
                            System.Reflection.BindingFlags.Public 
                              | System.Reflection.BindingFlags.Static 
                              | System.Reflection.BindingFlags.NonPublic,
                            null,
                            Type.EmptyTypes,
                            null);
                        
                        if (saveMethod == null)
                        {
                            log.Warn("[DATABASE] PerformOfflinePlayerSaves method not found on PlayerManager; offline save skipped.");
                        }
                        else
                        {
                            log.Info("[DATABASE] Calling PlayerManager.PerformOfflinePlayerSaves() via reflection");
                            saveMethod.Invoke(null, null);
                            success = true;
                            log.Info("[DATABASE] Successfully called PerformOfflinePlayerSaves");
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] Offline player save task failed with exception: {ex}");
                }
                finally
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    if (elapsed.TotalMilliseconds >= 5000)
                    {
                        log.Warn($"[DATABASE] OfflinePlayerSaves task took {elapsed.TotalMilliseconds:N0}ms to complete");
                    }
                    callback?.Invoke(success);
                }
            }, taskId));
        }


    }
}
