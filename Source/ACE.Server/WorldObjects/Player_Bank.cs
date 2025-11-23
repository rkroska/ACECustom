using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Entity.Actions;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using System;
using ACE.Common;
using ACE.Entity.Enum.Properties;
using Google.Protobuf.WellKnownTypes;
using MySqlX.XDevAPI;
using ACE.Server.Command.Handlers;
using ACE.Server.Factories;
using ACE.Server.Entity;
using ACE.Database.Models.Auth;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        private readonly object balanceLock = new object();

        // Banking system constants
        private const int PYREAL_MAX_STACK = 25000;
        private const int ENLIGHTENED_COIN_MAX_STACK = 25000;
        private const int WEAKLY_ENLIGHTENED_COIN_MAX_STACK = 25000;
        private const int TRADE_NOTE_MAX_STACK = 250;
        private const int MMD_TRADE_NOTE_MAX_STACK = 5000;

        /// <summary>
        /// Parses a number string with optional suffix (k, m, b, t, q).
        /// Supports: plain numbers (5, 1000), suffixed (10k, 1.5m), and decimals (1.55m, 1234k)
        /// </summary>
        /// <param name="input">String to parse (e.g., "10k", "1.5m", "1000")</param>
        /// <param name="value">Parsed value as long</param>
        /// <returns>True if parsing succeeded, false otherwise</returns>
        public static bool TryParseAmount(string input, out long value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim().ToLower();

            // Determine multiplier based on suffix
            long multiplier = 1;
            string numberPart = input;

            if (input.EndsWith("q"))
            {
                multiplier = 1_000_000_000_000_000L; // quadrillion
                numberPart = input.Substring(0, input.Length - 1);
            }
            else if (input.EndsWith("t"))
            {
                multiplier = 1_000_000_000_000L; // trillion
                numberPart = input.Substring(0, input.Length - 1);
            }
            else if (input.EndsWith("b"))
            {
                multiplier = 1_000_000_000L; // billion
                numberPart = input.Substring(0, input.Length - 1);
            }
            else if (input.EndsWith("m"))
            {
                multiplier = 1_000_000L; // million
                numberPart = input.Substring(0, input.Length - 1);
            }
            else if (input.EndsWith("k"))
            {
                multiplier = 1_000L; // thousand
                numberPart = input.Substring(0, input.Length - 1);
            }

            // Try to parse the numeric part (supports decimals)
            if (double.TryParse(numberPart, out double numericValue))
            {
                // Calculate final value and round to nearest long
                value = (long)Math.Round(numericValue * multiplier);
                return value >= 0; // Reject negative values
            }

            return false;
        }

        #region Bank Debugging Helpers

        /// <summary>
        /// Logs bank balance changes for debugging and audit purposes
        /// </summary>
        private void LogBankChange(string operation, string currencyType, long amount, long oldBalance, long newBalance, string details = "")
        {
            string message = $"[BANK_DEBUG] Player: {Name} | Operation: {operation} | Currency: {currencyType} | Amount: {amount:N0} | Old: {oldBalance:N0} | New: {newBalance:N0} | Details: {details}";
            log.Debug(message);
        }

        /// <summary>
        /// Logs item consumption for debugging
        /// </summary>
        private void LogItemConsumption(string operation, WorldObject item, bool success, string details = "")
        {
            string message = $"[BANK_DEBUG] Player: {Name} | Operation: {operation} | Item: {item.Name} (WCID: {item.WeenieClassId}) | Value: {item.Value ?? 0} | Success: {success} | Details: {details}";
            log.Debug(message);
        }

        /// <summary>
        /// Logs transfer operations for debugging
        /// </summary>
        private void LogTransfer(string operation, string currencyType, long amount, string targetPlayer, bool success, string details = "")
        {
            string message = $"[BANK_DEBUG] Player: {Name} | Operation: {operation} | Currency: {currencyType} | Amount: {amount:N0} | Target: {targetPlayer} | Success: {success} | Details: {details}";
            log.Debug(message);
        }

        /// <summary>
        /// Logs inventory scanning operations for performance debugging
        /// </summary>
        private void LogInventoryScan(string operation, int itemsFound, int containersScanned, long processingTimeMs, string details = "")
        {
            string message = $"[BANK_DEBUG] Player: {Name} | Operation: {operation} | Items Found: {itemsFound} | Containers Scanned: {containersScanned} | Processing Time: {processingTimeMs}ms | Details: {details}";
            log.Debug(message);
        }

        /// <summary>
        /// Helper method to log debug messages
        /// </summary>
        private void LogAndPrint(string message)
        {
            log.Debug(message);
        }

        #endregion

        /// <summary>
        /// Deposit all pyreals
        /// </summary>
        public void DepositPyreals(bool suppressChat = false)
        {
            LogAndPrint($"[BANK_DEBUG] Player: {Name} | Starting DepositPyreals operation");
            
            var pyrealsList = this.GetInventoryItemsOfWCID(273);
            long cash = 0;

            LogAndPrint($"[BANK_DEBUG] Player: {Name} | Found {pyrealsList.Count} pyreal items in inventory");

            foreach (var item in pyrealsList)
            {
                var itemValue = (long)(item.StackSize ?? 0);
                cash += itemValue;
                log.Debug($"[BANK_DEBUG] Player: {Name} | Pyreal item: {item.Name} (WCID: {item.WeenieClassId}) | StackSize: {item.StackSize} | Value: {itemValue}");
            }

            if (cash > 0)
            {
                long oldBalance = BankedPyreals ?? 0;
                // inner handles work; suppress its chat
                DepositPyreals(cash, true);
                long newBalance = BankedPyreals ?? 0;
                var actualDeposited = Math.Max(0, newBalance - oldBalance);
                
                // Update client-side coin value tracking after depositing pyreals
                UpdateCoinValue();
                
                LogBankChange("DepositPyreals", "Pyreals", actualDeposited, oldBalance, newBalance, $"Found {pyrealsList.Count} items");
                if (!suppressChat)
                {
                    var msg = actualDeposited > 0 ? $"Deposited {actualDeposited:N0} pyreals" : "No pyreals found to deposit";
                    Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.System));
                }
            }
            else
            {
                log.Debug($"[BANK_DEBUG] Player: {Name} | No pyreals found to deposit");
                if (!suppressChat)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat("No pyreals found to deposit", ChatMessageType.System));
                }
            }
        }
        /// <summary>
        /// Deposit specified amount of pyreals
        /// </summary>
        /// <param name="Amount"></param>
        public void DepositPyreals(long Amount, bool suppressChat = false)
        {
            log.Debug($"[BANK_DEBUG] Player: {Name} | Starting DepositPyreals(Amount: {Amount:N0}) operation");
            
            if (BankedPyreals == null)
            {
                BankedPyreals = 0;
                log.Info($"[BANK_DEBUG] Player: {Name} | Initialized BankedPyreals to 0");
            }
            
            long oldBalance = BankedPyreals ?? 0;
            log.Info($"[BANK_DEBUG] Player: {Name} | Current BankedPyreals: {oldBalance:N0} | Requested Amount: {Amount:N0}");
            
            lock (balanceLock)
            {
                var pyrealsList = this.GetInventoryItemsOfWCID(273);
                long totalDeposited = 0;
                var itemsToRemove = new List<WorldObject>();
                
                log.Debug($"[BANK_DEBUG] Player: {Name} | Found {pyrealsList.Count} pyreal items for specific amount deposit");

                foreach (var item in pyrealsList)
                {
                    log.Debug($"[BANK_DEBUG] Player: {Name} | Processing pyreal item: {item.Name} (WCID: {item.WeenieClassId}) | StackSize: {item.StackSize} | Remaining Amount: {Amount:N0}");
                    
                    var stackSize = (long)(item.StackSize ?? 0);
                    if (stackSize == PYREAL_MAX_STACK && Amount >= PYREAL_MAX_STACK) // full stacks
                    {
                        Amount -= PYREAL_MAX_STACK;
                        if (this.TryRemoveFromInventory(item.Guid, out var removedItem))
                        {
                            itemsToRemove.Add(removedItem);
                            BankedPyreals += PYREAL_MAX_STACK;
                            totalDeposited += PYREAL_MAX_STACK;
                            LogItemConsumption("DepositPyreals_FullStack", item, true, $"{PYREAL_MAX_STACK} pyreals");
                            log.Debug($"[BANK_DEBUG] Player: {Name} | Successfully deposited {PYREAL_MAX_STACK} pyreals | New BankedPyreals: {BankedPyreals:N0}");
                        }
                    }
                    else if (stackSize > 0 && Amount > 0)
                    {
                        var toConsume = (int)Math.Min(Amount, stackSize);
                        if (this.TryRemoveFromInventory(item.Guid, out var removedItem))
                        {
                            // For partial consumption, create a new item with remaining amount
                            if (toConsume < stackSize)
                            {
                                var remainingItem = WorldObjectFactory.CreateNewWorldObject(273);
                                remainingItem.SetStackSize((int)(stackSize - toConsume));
                                this.TryAddToInventory(remainingItem, out _, placementPosition: item.PlacementPosition ?? 0);
                                Session.Network.EnqueueSend(new GameMessageCreateObject(remainingItem));
                            }
                            
                            itemsToRemove.Add(removedItem);
                            Amount -= toConsume;
                            BankedPyreals += toConsume;
                            totalDeposited += toConsume;
                            LogItemConsumption("DepositPyreals_PartialStack", item, true, $"Amount: {toConsume:N0}");
                            log.Debug($"[BANK_DEBUG] Player: {Name} | Successfully deposited {toConsume:N0} pyreals | New BankedPyreals: {BankedPyreals:N0}");
                        }
                    }
                    if (Amount <= 0) break;
                }
                
                // Send batched network updates for removed items
                BatchRemoveItems(itemsToRemove);
                
                // Update client-side coin value tracking after removing pyreals
                UpdateCoinValue();
                
                long newBalance = BankedPyreals ?? 0;
                LogBankChange("DepositPyreals_Specific", "Pyreals", totalDeposited, oldBalance, newBalance, $"Processed {pyrealsList.Count} items");
                
                if (totalDeposited > 0 && !suppressChat)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {totalDeposited:N0} pyreals", ChatMessageType.System));
                }
                else if (!suppressChat)
                {
                    log.Debug($"[BANK_DEBUG] Player: {Name} | No pyreals were deposited");
                    Session.Network.EnqueueSend(new GameMessageSystemChat("No pyreals found to deposit", ChatMessageType.System));
                }
            }          
        }

        public void DepositLegendaryKeys(bool suppressChat = false)
        {
            if (BankedLegendaryKeys == null)
            {
                BankedLegendaryKeys = 0;
            }
            lock (balanceLock)
            {
                long totalDeposited = 0;
                var itemsToRemove = new List<WorldObject>();
                
                //int i = 0;
                var keysList = this.GetInventoryItemsOfWCID(48746);
                foreach (var item in keysList)
                {
                    if (this.TryRemoveFromInventory(item.Guid, out var removedItem))
                    {
                        BankedLegendaryKeys += 1;
                        totalDeposited += 1;
                        itemsToRemove.Add(removedItem);
                    }
                    else
                    {
                        break;
                    }
                }

                var rtwKeysList = this.GetInventoryItemsOfWCID(52010); //Rynthid Keys
                foreach (var rtw in rtwKeysList)
                {
                    if (this.TryRemoveFromInventory(rtw.Guid, out var removedRtw))
                    {
                        BankedLegendaryKeys += rtw.Structure ?? 5;
                        totalDeposited += rtw.Structure ?? 5;
                        itemsToRemove.Add(removedRtw);
                    }
                    else
                    {
                        break;
                    }
                }

                var durKeysList = this.GetInventoryItemsOfWCID(51954); //Durable legendary keys
                foreach (var dur in durKeysList)
                {
                    if (this.TryRemoveFromInventory(dur.Guid, out var removedDur))
                    {
                        BankedLegendaryKeys += dur.Structure ?? 10;
                        totalDeposited += dur.Structure ?? 10;
                        itemsToRemove.Add(removedDur);
                    }
                    else
                    {
                        break;
                    }
                }

                var legKeysList = this.GetInventoryItemsOfWCID(48748); //2-use Legendary keys
                foreach (var leg in legKeysList)
                {
                    if (this.TryRemoveFromInventory(leg.Guid, out var removedLeg))
                    {
                        BankedLegendaryKeys += leg.Structure ?? 2;
                        totalDeposited += leg.Structure ?? 2;
                        itemsToRemove.Add(removedLeg);
                    }
                    else
                    {
                        break;
                    }
                }

                var legeventKeysList = this.GetInventoryItemsOfWCID(500010); //25-use Legendary keys
                foreach (var legevent in legeventKeysList)
                {
                    if (this.TryRemoveFromInventory(legevent.Guid, out var removedLegevent))
                    {
                        BankedLegendaryKeys += legevent.Structure ?? 25;
                        totalDeposited += legevent.Structure ?? 25;
                        itemsToRemove.Add(removedLegevent);
                    }
                    else
                    {
                        break;
                    }
                }
                
                // Send batched network updates
                if (itemsToRemove.Count > 0)
                {
                    BatchRemoveItems(itemsToRemove);
                }
                
                if (!suppressChat)
                {
                    if (totalDeposited > 0)
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {totalDeposited:N0} legendary keys", ChatMessageType.System));
                    }
                    else
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("No legendary keys found to deposit", ChatMessageType.System));
                    }
                }
            }
        }

        public void DepositMythicalKeys(bool suppressChat = false)
        {
            if (BankedMythicalKeys == null)
            {
                BankedMythicalKeys = 0;
            }
            lock (balanceLock)
            {
                long totalDeposited = 0;
                var itemsToRemove = new List<WorldObject>();
                
                //int i = 0;
                var MythkeysList = this.GetInventoryItemsOfWCID(90000104);
                foreach (var item in MythkeysList)
                {
                    if (this.TryRemoveFromInventory(item.Guid, out var removedItem))
                    {
                        BankedMythicalKeys += 1;
                        totalDeposited += 1;
                        itemsToRemove.Add(removedItem);
                    }
                    else
                    {
                        break;
                    }
                }

                var FiveuseKeysList = this.GetInventoryItemsOfWCID(90000108); //5 use Keys
                foreach (var Fiveuse in FiveuseKeysList)
                {
                    if (this.TryRemoveFromInventory(Fiveuse.Guid, out var removedFiveuse))
                    {
                        BankedMythicalKeys += Fiveuse.Structure ?? 5;
                        totalDeposited += Fiveuse.Structure ?? 5;
                        itemsToRemove.Add(removedFiveuse);
                    }
                    else
                    {
                        break;
                    }
                }


                var durMythKeysList = this.GetInventoryItemsOfWCID(90000109); //Durable Mythical keys
                foreach (var durmyth in durMythKeysList)
                {
                    if (this.TryRemoveFromInventory(durmyth.Guid, out var removedDurmyth))
                    {
                        BankedMythicalKeys += durmyth.Structure ?? 10;
                        totalDeposited += durmyth.Structure ?? 10;
                        itemsToRemove.Add(removedDurmyth);
                    }
                    else
                    {
                        break;
                    }
                }

                var MythKeysList = this.GetInventoryItemsOfWCID(90000107); //2-use Mythical keys
                foreach (var Myth in MythKeysList)
                {
                    if (this.TryRemoveFromInventory(Myth.Guid, out var removedMyth))
                    {
                        BankedMythicalKeys += Myth.Structure ?? 2;
                        totalDeposited += Myth.Structure ?? 2;
                        itemsToRemove.Add(removedMyth);
                    }
                    else
                    {
                        break;
                    }
                }

                var MytheventKeysList = this.GetInventoryItemsOfWCID(90000110); //25-use Mythical keys
                foreach (var Mythevent in MytheventKeysList)
                {
                    if (this.TryRemoveFromInventory(Mythevent.Guid, out var removedMythevent))
                    {
                        BankedMythicalKeys += Mythevent.Structure ?? 25;
                        totalDeposited += Mythevent.Structure ?? 25;
                        itemsToRemove.Add(removedMythevent);
                    }
                    else
                    {
                        break;
                    }
                }
                
                // Send batched network updates
                if (itemsToRemove.Count > 0)
                {
                    BatchRemoveItems(itemsToRemove);
                }
                
                if (!suppressChat)
                {
                    if (totalDeposited > 0)
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {totalDeposited:N0} mythical keys", ChatMessageType.System));
                    }
                    else
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("No mythical keys found to deposit", ChatMessageType.System));
                    }
                }
            }
        }

        public void DepositPeas(bool suppressChat = false)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            LogAndPrint($"[BANK_DEBUG] Player: {Name} | Starting DepositPeas operation with performance improvements");
            
            if (BankedPyreals == null)
            {
                BankedPyreals = 0;
                log.Info($"[BANK_DEBUG] Player: {Name} | Initialized BankedPyreals to 0");
            }
            
            long oldBalance = BankedPyreals ?? 0;
            log.Info($"[BANK_DEBUG] Player: {Name} | Current BankedPyreals: {oldBalance:N0}");
            
            lock (balanceLock)
            {
                long totalDeposited = 0;
                
                // Single comprehensive scan: Get ALL items from main pack + side containers using iterative DFS
                var allItems = new List<WorldObject>();
                var processedContainers = new HashSet<ObjectGuid>(); // Prevent cycles
                var containerStack = new Stack<Container>();
                
                log.Debug($"[BANK_DEBUG] Player: {Name} | Starting single-pass inventory scan with DFS traversal");
                
                // Add main inventory
                allItems.AddRange(this.Inventory.Values);
                log.Debug($"[BANK_DEBUG] Player: {Name} | Added {this.Inventory.Values.Count} items from main inventory");
                
                // Initialize stack with top-level side containers
                var sideContainers = this.Inventory.Values.OfType<Container>();
                int containersAdded = 0;
                foreach (var container in sideContainers)
                {
                    if (container != null && !processedContainers.Contains(container.Guid))
                    {
                        containerStack.Push(container);
                        processedContainers.Add(container.Guid);
                        containersAdded++;
                    }
                }
                log.Debug($"[BANK_DEBUG] Player: {Name} | Added {containersAdded} side containers to DFS stack");
                
                // Iterative DFS: traverse arbitrarily nested containers
                int containersProcessed = 0;
                while (containerStack.Count > 0)
                {
                    var currentContainer = containerStack.Pop();
                    containersProcessed++;
                    
                    // Add items from current container
                    if (currentContainer.Inventory?.Values != null)
                    {
                        int itemsInContainer = currentContainer.Inventory.Values.Count;
                        allItems.AddRange(currentContainer.Inventory.Values);
                        log.Debug($"[BANK_DEBUG] Player: {Name} | Container {containersProcessed}: Added {itemsInContainer} items | Total items so far: {allItems.Count}");
                        
                        // Find nested containers and add to stack
                        var nestedContainers = currentContainer.Inventory.Values.OfType<Container>();
                        int nestedAdded = 0;
                        foreach (var nested in nestedContainers)
                        {
                            if (nested != null && !processedContainers.Contains(nested.Guid))
                            {
                                containerStack.Push(nested);
                                processedContainers.Add(nested.Guid);
                                nestedAdded++;
                            }
                        }
                        if (nestedAdded > 0)
                        {
                            log.Debug($"[BANK_DEBUG] Player: {Name} | Container {containersProcessed}: Added {nestedAdded} nested containers to stack");
                        }
                    }
                }
                
                log.Debug($"[BANK_DEBUG] Player: {Name} | DFS traversal complete | Total containers processed: {containersProcessed} | Total items collected: {allItems.Count}");
                
                // Filter in memory - no more scans!
                var pyreals = allItems.Where(i => i.WeenieClassId == 8330);
                var gold = allItems.Where(i => i.WeenieClassId == 8327);
                var silver = allItems.Where(i => i.WeenieClassId == 8331);
                var copper = allItems.Where(i => i.WeenieClassId == 8326);
                
                log.Debug($"[BANK_DEBUG] Player: {Name} | Currency filtering complete | Pyreals: {pyreals.Count()} | Gold: {gold.Count()} | Silver: {silver.Count()} | Copper: {copper.Count()}");
                
                // Process pyreals
                int pyrealsProcessed = 0;
                var itemsToRemove = new List<WorldObject>();
                foreach (var pyreal in pyreals)
                {
                    int val = pyreal.Value ?? 0;
                    if (val > 0)
                    {
                        bool success = this.TryRemoveFromInventory(pyreal.Guid, out var removedPyreal);
                        LogItemConsumption("DepositPeas_Pyreal", pyreal, success, $"Value: {val}");
                        
                        if (success)
                        {
                            BankedPyreals += val;
                            totalDeposited += val;
                            pyrealsProcessed++;
                            itemsToRemove.Add(removedPyreal);
                            log.Debug($"[BANK_DEBUG] Player: {Name} | Pyreal processed | Value: {val:N0} | Total deposited: {totalDeposited:N0}");
                        }
                    }
                }
                log.Debug($"[BANK_DEBUG] Player: {Name} | Pyreals processing complete | Processed: {pyrealsProcessed}/{pyreals.Count()}");

                // Process gold
                int goldProcessed = 0;
                foreach (var goldItem in gold)
                {
                    int val = goldItem.Value ?? 0;
                    if (val > 0)
                    {
                        bool success = this.TryRemoveFromInventory(goldItem.Guid, out var removedGold);
                        LogItemConsumption("DepositPeas_Gold", goldItem, success, $"Value: {val}");
                        
                        if (success)
                        {
                            BankedPyreals += val;
                            totalDeposited += val;
                            goldProcessed++;
                            itemsToRemove.Add(removedGold);
                            log.Debug($"[BANK_DEBUG] Player: {Name} | Gold processed | Value: {val:N0} | Total deposited: {totalDeposited:N0}");
                        }
                    }
                }
                log.Debug($"[BANK_DEBUG] Player: {Name} | Gold processing complete | Processed: {goldProcessed}/{gold.Count()}");

                // Process silver
                int silverProcessed = 0;
                foreach (var silverItem in silver)
                {
                    int val = silverItem.Value ?? 0;
                    if (val > 0)
                    {
                        bool success = this.TryRemoveFromInventory(silverItem.Guid, out var removedSilver);
                        LogItemConsumption("DepositPeas_Silver", silverItem, success, $"Value: {val}");
                        
                        if (success)
                        {
                            BankedPyreals += val;
                            totalDeposited += val;
                            silverProcessed++;
                            itemsToRemove.Add(removedSilver);
                            log.Debug($"[BANK_DEBUG] Player: {Name} | Silver processed | Value: {val:N0} | Total deposited: {totalDeposited:N0}");
                        }
                    }
                }
                log.Debug($"[BANK_DEBUG] Player: {Name} | Silver processing complete | Processed: {silverProcessed}/{silver.Count()}");

                // Process copper
                int copperProcessed = 0;
                foreach (var copperItem in copper)
                {
                    int val = copperItem.Value ?? 0;
                    if (val > 0)
                    {
                        bool success = this.TryRemoveFromInventory(copperItem.Guid, out var removedCopper);
                        LogItemConsumption("DepositPeas_Copper", copperItem, success, $"Value: {val}");
                        
                        if (success)
                        {
                            BankedPyreals += val;
                            totalDeposited += val;
                            copperProcessed++;
                            itemsToRemove.Add(removedCopper);
                            log.Debug($"[BANK_DEBUG] Player: {Name} | Copper processed | Value: {val:N0} | Total deposited: {totalDeposited:N0}");
                        }
                    }
                }
                log.Debug($"[BANK_DEBUG] Player: {Name} | Copper processing complete | Processed: {copperProcessed}/{copper.Count()}");
                
                // Send batched network updates
                if (itemsToRemove.Count > 0)
                {
                    BatchRemoveItems(itemsToRemove);
                    log.Debug($"[BANK_DEBUG] Player: {Name} | Sent batched network update for {itemsToRemove.Count} pea items");
                }
                
                // Update client-side coin value tracking after removing pyreal peas
                UpdateCoinValue();
                
                stopwatch.Stop();
                long newBalance = BankedPyreals ?? 0;
                
                LogBankChange("DepositPeas", "Pyreals", totalDeposited, oldBalance, newBalance, 
                    $"Performance: {stopwatch.ElapsedMilliseconds}ms | Items: {allItems.Count} | Containers: {containersProcessed}");
                
                LogInventoryScan("DepositPeas", allItems.Count, containersProcessed, stopwatch.ElapsedMilliseconds, 
                    $"Pyreals: {pyrealsProcessed} | Gold: {goldProcessed} | Silver: {silverProcessed} | Copper: {copperProcessed}");
                
                if (!suppressChat)
                {
                    if (totalDeposited > 0)
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {totalDeposited:N0} pyreal peas", ChatMessageType.System));
                    }
                    else
                    {
                        log.Debug($"[BANK_DEBUG] Player: {Name} | No pyreal peas were deposited");
                        Session.Network.EnqueueSend(new GameMessageSystemChat("No pyreal peas found to deposit", ChatMessageType.System));
                    }
                }
            }
        }

        public void DepositEnlightenedCoins(bool suppressChat = false)
        {
            if (BankedEnlightenedCoins == null)
            {
                BankedEnlightenedCoins = 0;
            }
            lock (balanceLock)
            {
                long totalDeposited = 0;
                //int i = 0;
                var EnlList = this.GetInventoryItemsOfWCID(300004);
                foreach (var coin in EnlList)
                {
                    int val = coin.Value ?? 0;
                    if (val > 0)
                    {
                        if (this.TryConsumeFromInventoryWithNetworking(coin))
                        {
                            BankedEnlightenedCoins += val;
                            totalDeposited += val;
                        }
                    }
                }
                
                if (!suppressChat)
                {
                    if (totalDeposited > 0)
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {totalDeposited:N0} enlightened coins", ChatMessageType.System));
                    }
                    else
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("No enlightened coins found to deposit", ChatMessageType.System));
                    }
                }
            }

            this.SavePlayerToDatabase();
        }

        public void DepositWeaklyEnlightenedCoins(bool suppressChat = false)
        {
            if (BankedWeaklyEnlightenedCoins == null)
            {
                BankedWeaklyEnlightenedCoins = 0;
            }
            lock (balanceLock)
            {
                long totalDeposited = 0;
                //int i = 0;
                var WeakEnlList = this.GetInventoryItemsOfWCID(300003);
                foreach (var coin in WeakEnlList)
                {
                    int val = coin.Value ?? 0;
                    if (val > 0)
                    {
                        if (this.TryConsumeFromInventoryWithNetworking(coin))
                        {
                            BankedWeaklyEnlightenedCoins += val;
                            totalDeposited += val;
                        }
                    }
                }
                
                if (!suppressChat)
                {
                    if (totalDeposited > 0)
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {totalDeposited:N0} weakly enlightened coins", ChatMessageType.System));
                    }
                    else
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("No weakly enlightened coins found to deposit", ChatMessageType.System));
                    }
                }
            }

            this.SavePlayerToDatabase();
        }


        /// <summary>
        /// Deposit all luminance
        /// </summary>
        public void DepositLuminance(bool suppressChat = false)
        {
            long availableLuminance = this.AvailableLuminance ?? 0;
            if (availableLuminance > 0)
            {
                // Let inner handle messaging
                DepositLuminance(availableLuminance, true);
            }
            else if (!suppressChat)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("No luminance available to deposit", ChatMessageType.System));
            }
        }

        /// <summary>
        /// Deposit specified amount of luminance
        /// </summary>
        /// <param name="Amount"></param>
        public void DepositLuminance(long Amount, bool suppressChat = false)
        {
            if (Amount <= 0)
            {
                if (!suppressChat)
                    Session.Network.EnqueueSend(new GameMessageSystemChat("Amount must be greater than 0.", ChatMessageType.System));
                return;
            }
            
            // Verify player has luminance flagged (earned through gameplay)
            if (!this.MaximumLuminance.HasValue || this.MaximumLuminance == 0)
            {
                if (!suppressChat)
                    Session.Network.EnqueueSend(new GameMessageSystemChat("You have not been luminance flagged yet. Complete the luminance quest first.", ChatMessageType.System));
                return;
            }
            
            // Initialize BankedLuminance only for flagged players
            if (BankedLuminance == null) { BankedLuminance = 0;}
            
            long actualAmount = 0;
            if (Amount <= this.AvailableLuminance)
            {
                lock(balanceLock)
                {                    
                    this.AvailableLuminance -= Amount;
                    BankedLuminance += Amount;
                    actualAmount = Amount;
                }                
            }
            else
            {
                lock(balanceLock)
                {
                    actualAmount = this.AvailableLuminance ?? 0;
                    BankedLuminance += actualAmount;
                    this.AvailableLuminance = 0;
                }
            }
            
            if (actualAmount > 0 && !suppressChat)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {actualAmount:N0} luminance", ChatMessageType.System));
            }
            else if (!suppressChat)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("No luminance available to deposit", ChatMessageType.System));
            }
            
            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableLuminance, this.AvailableLuminance ?? 0));
            //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLuminance, this.BankedLuminance ?? 0));
        }

        /// <summary>
        /// Deposits all trade notes
        /// </summary>
        public void DepositTradeNotes(bool suppressChat = false)
        {
            if (BankedPyreals == null)
            {
                BankedPyreals = 0;
            }
            lock(balanceLock)
            {
                long totalDeposited = 0;
                var notesList = this.GetTradeNotes();
                var itemsToRemove = new List<WorldObject>();
                
                foreach (var note in notesList)
                {
                    int val = note.Value ?? 0;
                    if (val > 0)
                    {
                        if (this.TryRemoveFromInventory(note.Guid, out var removedNote))
                        {
                            BankedPyreals += val;
                            totalDeposited += val;
                            itemsToRemove.Add(removedNote);
                        }
                    }                    
                }
                
                // Send batched network updates
                if (itemsToRemove.Count > 0)
                {
                    BatchRemoveItems(itemsToRemove);
                }
                
                // Update client-side coin value tracking after removing trade notes
                UpdateCoinValue();
                
                if (!suppressChat)
                {
                    if (totalDeposited > 0)
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {totalDeposited:N0} pyreals worth of trade notes", ChatMessageType.System));
                    }
                    else
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("No trade notes found to deposit", ChatMessageType.System));
                    }
                }
            }
        }

        /// <summary>
        /// Withdraw luminance from the bank
        /// </summary>
        public void WithdrawLuminance(long Amount)
        {
            if (Amount <= 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Amount must be greater than zero", ChatMessageType.System));
                return;
            }

            // Verify player has luminance flagged (earned through gameplay)
            if (!this.MaximumLuminance.HasValue || this.MaximumLuminance == 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("You have not been luminance flagged yet. Complete the luminance quest first.", ChatMessageType.System));
                return;
            }
            
            // Check if player has enough luminance banked
            if (BankedLuminance < Amount)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough luminance banked. Need {Amount:N0} but only have {BankedLuminance:N0}.", ChatMessageType.System));
                return;
            }

            // Check if player can hold the luminance (not exceed MaximumLuminance)
            long currentAvailable = this.AvailableLuminance ?? 0;
            long maxCanHold = (this.MaximumLuminance ?? 1500000) - currentAvailable;
            
            if (maxCanHold <= 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot hold any more luminance. You are at your maximum capacity of {this.MaximumLuminance:N0}.", ChatMessageType.System));
                return;
            }

            long actualWithdraw = Math.Min(Amount, maxCanHold);
            
                lock (balanceLock)
                {
                this.AvailableLuminance += actualWithdraw;
                BankedLuminance -= actualWithdraw;
                }
            
            if (actualWithdraw == Amount)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Withdrew {actualWithdraw:N0} luminance", ChatMessageType.System));
            }
            else
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Withdrew {actualWithdraw:N0} luminance (partial - insufficient capacity for remaining {Amount - actualWithdraw:N0})", ChatMessageType.System));
            }
            
            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableLuminance, this.AvailableLuminance ?? 0));
        }

        private void BatchRemoveItems(List<WorldObject> itemsToRemove)
        {
            if (itemsToRemove.Count == 0) return;
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            foreach (var item in itemsToRemove)
            {
                Session.Network.EnqueueSend(new GameMessageInventoryRemoveObject(item));
                item.Destroy();
            }
            
            stopwatch.Stop();
            log.Info($"[BANK_PERF] Player: {Name} | BatchRemoveItems completed in {stopwatch.ElapsedMilliseconds}ms | Removed {itemsToRemove.Count} items");
        }

        /// <summary>
        /// Creates pyreal coins in multiple stacks respecting maximum stack size limits.
        /// Uses batched network updates for optimal performance.
        /// </summary>
        /// <param name="Amount">Total amount of pyreals to create</param>
        /// <returns>Number of pyreals successfully created</returns>
        private long CreatePyreals(long Amount)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            long remaining = Amount;
            long successfullyCreated = 0;
            var createdItems = new List<WorldObject>();
            
            while (remaining > 0)
            {
                int stackSize = (int)Math.Min(remaining, (long)PYREAL_MAX_STACK);
                
                WorldObject smallCoins = WorldObjectFactory.CreateNewWorldObject(273);
                if (smallCoins == null)
                    break; // Can't create more items
                    
                smallCoins.SetStackSize(stackSize);
                
                // Create item without immediate networking
                var itemCreated = this.TryAddToInventory(smallCoins, out _);
                if (!itemCreated)
                {
                    log.Debug($"[BANK_DEBUG] Player: {Name} | Failed to create pyreal stack of {stackSize} - insufficient pack space");
                    break; // Stop creating, but keep what we've made so far
                }
                
                createdItems.Add(smallCoins);
                successfullyCreated += stackSize;
                
                // Only log every 10th stack to reduce log spam
                if (createdItems.Count % 10 == 0 || remaining - stackSize == 0)
                {
                    log.Debug($"[BANK_DEBUG] Player: {Name} | Created {createdItems.Count} pyreal stacks | Total: {successfullyCreated:N0} | Remaining: {remaining - stackSize:N0}");
                }
                
                remaining -= stackSize;
            }
            
            // Send batched network update for all created items
            if (createdItems.Count > 0)
            {
                foreach (var item in createdItems)
                {
                    Session.Network.EnqueueSend(new GameMessageCreateObject(item));
                }
                log.Debug($"[BANK_DEBUG] Player: {Name} | Sent batched network update for {createdItems.Count} pyreal stacks");
            }
            
            stopwatch.Stop();
            log.Info($"[BANK_PERF] Player: {Name} | CreatePyreals completed in {stopwatch.ElapsedMilliseconds}ms | Created: {successfullyCreated:N0} pyreals in {createdItems.Count} stacks");
            
            // Return the amount that was successfully created
            return successfullyCreated;
        }



        public void WithdrawPyreals(long Amount)
        {
            if (Amount <= 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Amount must be greater than zero", ChatMessageType.System));
                return;
            }

            LogAndPrint($"[BANK_DEBUG] Player: {Name} | Starting WithdrawPyreals operation | Amount: {Amount:N0} | Current BankedPyreals: {BankedPyreals:N0}");
            
            // Check if player has enough pyreals (outside lock for early exit)
            if (BankedPyreals < Amount)
            {
                log.Debug($"[BANK_DEBUG] Player: {Name} | Insufficient funds | Requested: {Amount:N0} | Available: {BankedPyreals:N0}");
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough pyreals banked. Need {Amount:N0} pyreals but only have {BankedPyreals:N0}.", ChatMessageType.System));
                return;
            }
            
            // Create pyreal coins for the requested amount (outside lock)
            log.Info($"[BANK_DEBUG] Player: {Name} | Creating {Amount:N0} pyreal coins");
            long successfullyCreated = CreatePyreals(Amount);
            log.Debug($"[BANK_DEBUG] Player: {Name} | Pyreal coin creation | Requested: {Amount:N0} | Successfully Created: {successfullyCreated:N0}");
            
            // Update balance atomically (only lock for balance mutation)
            if (successfullyCreated > 0)
            {
                long oldBalance;
                long newBalance;
                
                lock (balanceLock)
                {
                    oldBalance = BankedPyreals ?? 0;
                    BankedPyreals -= successfullyCreated;
                    newBalance = BankedPyreals ?? 0;
                    LogBankChange("WithdrawPyreals", "Pyreals", successfullyCreated, oldBalance, newBalance, $"Withdrew {successfullyCreated:N0} pyreals");
                }
                
                // Update client-side coin value tracking after adding pyreals
                UpdateCoinValue();
                
                // Send notifications outside of lock
                if (successfullyCreated == Amount)
                {
                    // Full withdrawal successful
                    log.Debug($"[BANK_DEBUG] Player: {Name} | Pyreal coins created successfully | Amount: {successfullyCreated:N0}");
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Withdrew {successfullyCreated:N0} pyreals", ChatMessageType.System));
                }
                else
                {
                    // Partial withdrawal due to pack space
                    long remaining = Amount - successfullyCreated;
                    log.Debug($"[BANK_DEBUG] Player: {Name} | Partial withdrawal | Created: {successfullyCreated:N0} | Remaining: {remaining:N0} (insufficient pack space)");
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Withdrew {successfullyCreated:N0} pyreals (partial - insufficient pack space for remaining {remaining:N0} pyreals)", ChatMessageType.System));
                }
            }
            else
            {
                log.Debug($"[BANK_DEBUG] Player: {Name} | Failed to create any pyreal coins - insufficient pack space");
                Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create pyreal coins - check pack space. Withdrawal cancelled.", ChatMessageType.System));
            }
        }

        private bool CreateLegendaryKey(uint weenieClassId, byte uses)
        {
            WorldObject key = WorldObjectFactory.CreateNewWorldObject(weenieClassId);
            if (key == null)
                return false;
            var itemCreated = this.TryCreateInInventoryWithNetworking(key);
            if (itemCreated)
            {
                BankedLegendaryKeys -= uses;
                return true;
            }
            return false;
        }

        public void WithdrawLegendaryKeys(long Amount)
        {
            if (Amount <= 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Amount must be greater than zero", ChatMessageType.System));
                return;
            }

            lock (balanceLock)
            {
                // Check if player has enough legendary keys
                if (BankedLegendaryKeys < Amount)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough legendary keys banked. Need {Amount:N0} keys but only have {BankedLegendaryKeys:N0}.", ChatMessageType.System));
                    return;
                }
                
                long remainingAmount = Amount;
                long totalWithdrawn = 0;
                int keys25Created = 0;
                int keys10Created = 0;
                int keys1Created = 0;
                while (remainingAmount >= 25)
                {
                    if (!CreateLegendaryKey(500010, 25)) 
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create 25-use legendary keys - check pack space. Withdrawal may be incomplete.", ChatMessageType.System));
                        break;
                    }
                    remainingAmount -= 25;
                    totalWithdrawn += 25;
                    keys25Created++;
                }
                while (remainingAmount >= 10)
                {
                    if (!CreateLegendaryKey(51954, 10)) 
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create 10-use legendary keys - check pack space. Withdrawal may be incomplete.", ChatMessageType.System));
                        break;
                    }
                    remainingAmount -= 10;
                    totalWithdrawn += 10;
                    keys10Created++;
                }
                while (remainingAmount > 0)
                {
                    if (!CreateLegendaryKey(48746, 1)) 
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create single-use legendary keys - check pack space. Withdrawal may be incomplete.", ChatMessageType.System));
                        break;
                    }
                    remainingAmount -= 1;
                    totalWithdrawn += 1;
                    keys1Created++;
                }
                
                // Concise summary message
                if (totalWithdrawn > 0)
                {
                    string summary = $"Withdrew {totalWithdrawn:N0} legendary keys";
                    
                    if (keys25Created > 0)
                    {
                        summary += $" - {keys25Created:N0} 25-use key(s)";
                    }
                    
                    if (keys10Created > 0)
                    {
                        summary += keys25Created > 0 ? $", {keys10Created:N0} 10-use key(s)" : $" - {keys10Created:N0} 10-use key(s)";
                    }
                    
                    if (keys1Created > 0)
                    {
                        summary += (keys25Created > 0 || keys10Created > 0) ? $", {keys1Created:N0} single-use key(s)" : $" - {keys1Created:N0} single-use key(s)";
                    }
                    
                    Session.Network.EnqueueSend(new GameMessageSystemChat(summary, ChatMessageType.System));
                }
                
                // Flag any discrepancies
                if (totalWithdrawn != Amount)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Warning: Requested {Amount:N0} legendary keys but only {totalWithdrawn:N0} was withdrawn.", ChatMessageType.System));
                }
            }
        }

        private bool CreateMythicalKey(uint weenieClassId, byte uses)
        {
            WorldObject key = WorldObjectFactory.CreateNewWorldObject(weenieClassId);
            if (key == null)
                return false;
            var itemCreated = this.TryCreateInInventoryWithNetworking(key);
            if (itemCreated)
            {
                BankedMythicalKeys -= uses;
                return true;
            }
            return false;
        }

        public void WithdrawMythicalKeys(long Amount)
        {
            if (Amount <= 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Amount must be greater than zero", ChatMessageType.System));
                return;
            }

            lock (balanceLock)
            {
                // Check if player has enough mythical keys
                if (BankedMythicalKeys < Amount)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough mythical keys banked. Need {Amount:N0} keys but only have {BankedMythicalKeys:N0}.", ChatMessageType.System));
                    return;
                }
                
                long remainingAmount = Amount;
                long totalWithdrawn = 0;
                int keys25Created = 0;
                int keys10Created = 0;
                int keys1Created = 0;
                while (remainingAmount >= 25)
                {
                    if (!CreateMythicalKey(90000110, 25)) 
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create 25-use mythical keys - check pack space. Withdrawal may be incomplete.", ChatMessageType.System));
                        break;
                    }
                    remainingAmount -= 25;
                    totalWithdrawn += 25;
                    keys25Created++;
                }
                while (remainingAmount >= 10)
                {
                    if (!CreateMythicalKey(90000109, 10)) 
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create 10-use mythical keys - check pack space. Withdrawal may be incomplete.", ChatMessageType.System));
                        break;
                    }
                    remainingAmount -= 10;
                    totalWithdrawn += 10;
                    keys10Created++;
                }
                while (remainingAmount > 0)
                {
                    if (!CreateMythicalKey(90000104, 1)) 
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create single-use mythical keys - check pack space. Withdrawal may be incomplete.", ChatMessageType.System));
                        break;
                    }
                    remainingAmount -= 1;
                    totalWithdrawn += 1;
                    keys1Created++;
                }
                
                // Concise summary message
                if (totalWithdrawn > 0)
                {
                    string summary = $"Withdrew {totalWithdrawn:N0} mythical keys";
                    
                    if (keys25Created > 0)
                    {
                        summary += $" - {keys25Created:N0} 25-use key(s)";
                    }
                    
                    if (keys10Created > 0)
                    {
                        summary += keys25Created > 0 ? $", {keys10Created:N0} 10-use key(s)" : $" - {keys10Created:N0} 10-use key(s)";
                    }
                    
                    if (keys1Created > 0)
                    {
                        summary += (keys25Created > 0 || keys10Created > 0) ? $", {keys1Created:N0} single-use key(s)" : $" - {keys1Created:N0} single-use key(s)";
                    }
                    
                    Session.Network.EnqueueSend(new GameMessageSystemChat(summary, ChatMessageType.System));
                }
                
                // Flag any discrepancies
                if (totalWithdrawn != Amount)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Warning: Requested {Amount:N0} mythical keys but only {totalWithdrawn:N0} was withdrawn.", ChatMessageType.System));
                }
            }
        }

        /// <summary>
        /// Creates enlightened coins in multiple stacks respecting maximum stack size limits.
        /// Uses batched network updates for optimal performance.
        /// </summary>
        /// <param name="Amount">Total amount of enlightened coins to create</param>
        /// <returns>Number of enlightened coins successfully created</returns>
        private long CreateEnlightenedCoins(long Amount)
        {
            long remaining = Amount;
            long successfullyCreated = 0;
            var createdItems = new List<WorldObject>();

            while (remaining > 0)
            {
                int stackSize = (int)Math.Min(remaining, (long)ENLIGHTENED_COIN_MAX_STACK);

                WorldObject wo = WorldObjectFactory.CreateNewWorldObject(300004);
                if (wo == null)
                    break; // Can't create more items

                wo.SetStackSize(stackSize);

                // Create item without immediate networking
                var itemCreated = this.TryAddToInventory(wo, out _);
                if (!itemCreated)
                {
                    log.Debug($"[BANK_DEBUG] Player: {Name} | Failed to create enlightened coin stack of {stackSize} - insufficient pack space");
                    break; // Stop creating, but keep what we've made so far
                }

                createdItems.Add(wo);
                successfullyCreated += stackSize;

                // Only log every 10th stack to reduce log spam
                if (createdItems.Count % 10 == 0 || remaining - stackSize == 0)
                {
                    log.Debug($"[BANK_DEBUG] Player: {Name} | Created {createdItems.Count} enlightened coin stacks | Total: {successfullyCreated:N0} | Remaining: {remaining - stackSize:N0}");
                }

                remaining -= stackSize;
            }

            // Send batched network update for all created items
            if (createdItems.Count > 0)
            {
                foreach (var item in createdItems)
                {
                    Session.Network.EnqueueSend(new GameMessageCreateObject(item));
                }
                log.Debug($"[BANK_DEBUG] Player: {Name} | Sent batched network update for {createdItems.Count} enlightened coin stacks");
            }

            // Return the amount that was successfully created
            return successfullyCreated;
        }

        public void WithdrawEnlightenedCoins(long Amount)
        {
            if (Amount <= 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Amount must be greater than zero", ChatMessageType.System));
                return;
            }

            // Check if player has enough enlightened coins (outside lock for early exit)
            if (BankedEnlightenedCoins < Amount)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough enlightened coins banked. Need {Amount:N0} coins but only have {BankedEnlightenedCoins:N0}.", ChatMessageType.System));
                return;
            }
            
            // Create enlightened coins (outside lock)
            long successfullyCreated = CreateEnlightenedCoins(Amount);
            
            // Update balance atomically (only lock for balance mutation)
            if (successfullyCreated > 0)
            {
                lock (balanceLock)
                {
                    BankedEnlightenedCoins -= successfullyCreated;
                }
                
                // Send notifications outside of lock
                if (successfullyCreated == Amount)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Withdrew {successfullyCreated:N0} enlightened coins", ChatMessageType.System));
                }
                else
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Withdrew {successfullyCreated:N0} enlightened coins (partial - insufficient pack space for remaining {Amount - successfullyCreated:N0} enlightened coins)", ChatMessageType.System));
                }
            }
            else
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create enlightened coins - check pack space. Withdrawal cancelled.", ChatMessageType.System));
            }
        }

        /// <summary>
        /// Creates weakly enlightened coins in multiple stacks respecting maximum stack size limits.
        /// Uses batched network updates for optimal performance.
        /// </summary>
        /// <param name="Amount">Total amount of weakly enlightened coins to create</param>
        /// <returns>Number of weakly enlightened coins successfully created</returns>
        private long CreateWeaklyEnlightenedCoins(long Amount)
        {
            long remaining = Amount;
            long successfullyCreated = 0;
            var createdItems = new List<WorldObject>();

            while (remaining > 0)
            {
                int stackSize = (int)Math.Min(remaining, (long)WEAKLY_ENLIGHTENED_COIN_MAX_STACK);

                WorldObject wo = WorldObjectFactory.CreateNewWorldObject(300003);
                if (wo == null)
                    break; // Can't create more items

                wo.SetStackSize(stackSize);

                // Create item without immediate networking
                var itemCreated = this.TryAddToInventory(wo, out _);
                if (!itemCreated)
                {
                    log.Debug($"[BANK_DEBUG] Player: {Name} | Failed to create weakly enlightened coin stack of {stackSize} - insufficient pack space");
                    break; // Stop creating, but keep what we've made so far
                }

                createdItems.Add(wo);
                successfullyCreated += stackSize;

                // Only log every 10th stack to reduce log spam
                if (createdItems.Count % 10 == 0 || remaining - stackSize == 0)
                {
                    log.Debug($"[BANK_DEBUG] Player: {Name} | Created {createdItems.Count} weakly enlightened coin stacks | Total: {successfullyCreated:N0} | Remaining: {remaining - stackSize:N0}");
                }

                remaining -= stackSize;
            }

            // Send batched network update for all created items
            if (createdItems.Count > 0)
            {
                foreach (var item in createdItems)
                {
                    Session.Network.EnqueueSend(new GameMessageCreateObject(item));
                }
                log.Debug($"[BANK_DEBUG] Player: {Name} | Sent batched network update for {createdItems.Count} weakly enlightened coin stacks");
            }

            // Return the amount that was successfully created
            return successfullyCreated;
        }

        public void WithdrawWeaklyEnlightenedCoins(long Amount)
        {
            if (Amount <= 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Amount must be greater than zero", ChatMessageType.System));
                return;
            }

            // Check if player has enough weakly enlightened coins (outside lock for early exit)
            if (BankedWeaklyEnlightenedCoins < Amount)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough weakly enlightened coins banked. Need {Amount:N0} coins but only have {BankedWeaklyEnlightenedCoins:N0}.", ChatMessageType.System));
                return;
            }
            
            // Create weakly enlightened coins (outside lock)
            long successfullyCreated = CreateWeaklyEnlightenedCoins(Amount);
            
            // Update balance atomically (only lock for balance mutation)
            if (successfullyCreated > 0)
            {
                lock (balanceLock)
                {
                    BankedWeaklyEnlightenedCoins -= successfullyCreated;
                }
                
                // Send notifications outside of lock
                if (successfullyCreated == Amount)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Withdrew {successfullyCreated:N0} weakly enlightened coins", ChatMessageType.System));
                }
                else
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Withdrew {successfullyCreated:N0} weakly enlightened coins (partial - insufficient pack space for remaining {Amount - successfullyCreated:N0} weakly enlightened coins)", ChatMessageType.System));
                }
            }
            else
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create weakly enlightened coins - check pack space. Withdrawal cancelled.", ChatMessageType.System));
            }
        }

        /// <summary>
        /// Withdraw specific trade note denominations
        /// </summary>
        /// <param name="denomination">Trade note denomination (i, v, x, l, c, d, m, md, mm, mmd)</param>
        /// <param name="count">Number of trade notes to withdraw (default 1)</param>
        public void WithdrawTradeNotes(string denomination, int count = 1)
        {
            if (string.IsNullOrWhiteSpace(denomination))
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Denomination cannot be empty", ChatMessageType.System));
                return;
            }
            
            if (count <= 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Count must be greater than 0", ChatMessageType.System));
                return;
            }

            if (count > 50000) // Reasonable limit to prevent abuse
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Count cannot exceed 50,000", ChatMessageType.System));
                return;
            }

            lock (balanceLock)
            {
                uint weenieId = 0;
                long noteValue = 0;
                string noteName = "";

                // Map denomination to weenie ID and value
                switch (denomination.Trim().ToLower())
                {
                    case "i":
                        weenieId = 2621; // tradenote100
                        noteValue = 100;
                        noteName = "100 pyreal";
                        break;
                    case "v":
                        weenieId = 2622; // tradenote500
                        noteValue = 500;
                        noteName = "500 pyreal";
                        break;
                    case "x":
                        weenieId = 2623; // tradenote1000
                        noteValue = 1000;
                        noteName = "1,000 pyreal";
                        break;
                    case "l":
                        weenieId = 2624; // tradenote5000
                        noteValue = 5000;
                        noteName = "5,000 pyreal";
                        break;
                    case "c":
                        weenieId = 2625; // tradenote10000
                        noteValue = 10000;
                        noteName = "10,000 pyreal";
                        break;
                    case "d":
                        weenieId = 2626; // tradenote50000
                        noteValue = 50000;
                        noteName = "50,000 pyreal";
                        break;
                    case "m":
                        weenieId = 2627; // tradenote100000
                        noteValue = 100000;
                        noteName = "100,000 pyreal";
                        break;
                    case "md":
                        weenieId = 20628; // tradenote150000
                        noteValue = 150000;
                        noteName = "150,000 pyreal";
                        break;
                    case "mm":
                        weenieId = 20629; // tradenote200000
                        noteValue = 200000;
                        noteName = "200,000 pyreal";
                        break;
                    case "mmd":
                        weenieId = 20630; // tradenote250000
                        noteValue = 250000;
                        noteName = "250,000 pyreal";
                        break;
                    default:
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid denomination. Use: i(100), v(500), x(1k), l(5k), c(10k), d(50k), m(100k), md(150k), mm(200k), mmd(250k)", ChatMessageType.System));
                        return;
                }

                long totalCost = noteValue * count;
                
                // Check if player has enough pyreals
                if (BankedPyreals < totalCost)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough pyreals banked. Need {totalCost:N0} pyreals for {count} {noteName} trade note(s) but only have {BankedPyreals:N0}.", ChatMessageType.System));
                    return;
                }

                var remaining = count;
                var created = 0;
                // Use different stack sizes based on denomination
                int maxStack = denomination.Trim().ToLower() == "mmd" ? MMD_TRADE_NOTE_MAX_STACK : TRADE_NOTE_MAX_STACK;
                // Validate against actual item data
                var probe = WorldObjectFactory.CreateNewWorldObject(weenieId);
                if (probe is Stackable stackable && stackable.MaxStackSize.HasValue)
                    maxStack = Math.Max(1, Math.Min(maxStack, stackable.MaxStackSize.Value));
                probe?.Destroy();
                while (remaining > 0)
                {
                    var batch = Math.Min(remaining, maxStack);
                    var note = WorldObjectFactory.CreateNewWorldObject(weenieId);
                    if (note == null)
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create trade notes. Withdrawal may be incomplete.", ChatMessageType.System));
                        break;
                    }
                    note.SetStackSize(batch);
                    if (!this.TryCreateInInventoryWithNetworking(note))
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to add trade notes to inventory - check pack space. Withdrawal may be incomplete.", ChatMessageType.System));
                        break;
                    }
                    remaining -= batch;
                    created += batch;
                }
                if (created > 0)
                {
                    var debited = created * noteValue;
                    BankedPyreals -= debited;
                    LogBankChange("WithdrawTradeNotes", "Pyreals", debited, (BankedPyreals ?? 0) + debited, BankedPyreals ?? 0, $"{created} × {denomination.ToUpper()}");
                    
                    // Update client-side coin value tracking after adding trade notes
                    UpdateCoinValue();
                    
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Withdrew {created} {denomination.ToUpper()} note(s) worth {debited:N0} pyreals", ChatMessageType.System));
                    if (created != count)
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Warning: Requested {count} notes but only {created} were created.", ChatMessageType.System));
                }
            }
        }

        private IPlayer FindTargetByName(string name)
        {
            return PlayerManager.FindByName(name);
        }

        public bool TransferPyreals(long Amount, string CharacterDestination)
        {
            LogAndPrint($"[BANK_DEBUG] Player: {Name} | Starting TransferPyreals operation | Amount: {Amount:N0} | Target: {CharacterDestination} | Current BankedPyreals: {BankedPyreals:N0}");
            
            // Validate amount
            if (Amount <= 0)
            {
                LogTransfer("TransferPyreals", "Pyreals", Amount, CharacterDestination, false, "Non-positive amount");
                Session.Network.EnqueueSend(new GameMessageSystemChat("Amount must be greater than 0.", ChatMessageType.System));
                return false;
            }
            // Check if player has enough pyreals to transfer
            if (BankedPyreals < Amount)
            {
                LogTransfer("TransferPyreals", "Pyreals", Amount, CharacterDestination, false, "Insufficient funds");
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough pyreals banked to transfer. Need {Amount:N0} but only have {BankedPyreals:N0}.", ChatMessageType.System));
                return false;
            }
            
            var tarplayer = FindTargetByName(CharacterDestination);
            if (tarplayer == null)
            {
                LogTransfer("TransferPyreals", "Pyreals", Amount, CharacterDestination, false, "Target player not found");
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Character '{CharacterDestination}' not found.", ChatMessageType.System));
                return false;
            }
            
            log.Info($"[BANK_DEBUG] Player: {Name} | Target player found | Name: {tarplayer.Name} | Type: {(tarplayer is OfflinePlayer ? "Offline" : "Online")}");
            
            long oldBalance = BankedPyreals ?? 0;
            long targetOldBalance = 0;
            
            // Get target player's current balance
            if (tarplayer is Player onlinePlayerCheck)
            {
                targetOldBalance = onlinePlayerCheck.BankedPyreals ?? 0;
            }
            else if (tarplayer is OfflinePlayer offlinePlayerCheck)
            {
                targetOldBalance = offlinePlayerCheck.BankedPyreals ?? 0;
            }
            
            try
            {
                if (tarplayer is OfflinePlayer)
                {
                    var offlinePlayer = tarplayer as OfflinePlayer;
                    log.Debug($"[BANK_DEBUG] Player: {Name} | Transferring to offline player | Target: {offlinePlayer.Name} | Target old balance: {offlinePlayer.BankedPyreals:N0}");
                    
                    lock (balanceLock)
                    {
                        // Deduct from sender
                        this.BankedPyreals -= Amount;
                    }
                    
                    // Add to offline receiver
                    if (offlinePlayer.BankedPyreals == null)
                    {
                        offlinePlayer.BankedPyreals = Amount;
                        log.Debug($"[BANK_DEBUG] Player: {Name} | Initialized target player's BankedPyreals to {Amount:N0}");
                    }
                    else
                    {
                        offlinePlayer.BankedPyreals += Amount;
                        log.Debug($"[BANK_DEBUG] Player: {Name} | Added {Amount:N0} to target player's BankedPyreals | New balance: {offlinePlayer.BankedPyreals:N0}");
                    }
                    offlinePlayer.SaveBiotaToDatabase();
                    
                    // Send confirmation to sender
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {Amount:N0} Pyreals to {offlinePlayer.Name} (offline)", ChatMessageType.System));
                }
                else
                {
                    var onlinePlayer = (Player)tarplayer;
                    log.Debug($"[BANK_DEBUG] Player: {Name} | Transferring to online player | Target: {onlinePlayer.Name} | Target old balance: {onlinePlayer.BankedPyreals:N0}");
                    
                    // Deadlock-safe double-lock using consistent ordering
                    object lockA = this.balanceLock;
                    object lockB = onlinePlayer.balanceLock;
                    bool sourceFirst = string.CompareOrdinal(this.Name, onlinePlayer.Name) <= 0;
                    var firstLock = sourceFirst ? lockA : lockB;
                    var secondLock = sourceFirst ? lockB : lockA;
                    
                    lock (firstLock)
                    {
                        lock (secondLock)
                        {
                            // Capture current balances
                            long srcOldBalance = this.BankedPyreals ?? 0;
                            long dstOldBalance = onlinePlayer.BankedPyreals ?? 0;
                            
                            // Perform atomic transfer
                            this.BankedPyreals = srcOldBalance - Amount;
                            onlinePlayer.BankedPyreals = dstOldBalance + Amount;
                            
                            log.Debug($"[BANK_DEBUG] Player: {Name} | Atomic transfer completed | Source: {srcOldBalance:N0} -> {this.BankedPyreals:N0} | Target: {dstOldBalance:N0} -> {onlinePlayer.BankedPyreals:N0}");
                        }
                    }
                    
                    // Send notification outside of locks
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Pyreals from {this.Name}", ChatMessageType.System));
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {Amount:N0} Pyreals to {onlinePlayer.Name}", ChatMessageType.System));
                }
                long newBalance = BankedPyreals ?? 0;
                long targetNewBalance = 0;
                
                // Get target player's new balance
                if (tarplayer is Player onlinePlayerNew)
                {
                    targetNewBalance = onlinePlayerNew.BankedPyreals ?? 0;
                }
                else if (tarplayer is OfflinePlayer offlinePlayerNew)
                {
                    targetNewBalance = offlinePlayerNew.BankedPyreals ?? 0;
                }
                
                LogBankChange("TransferPyreals_Source", "Pyreals", Amount, oldBalance, newBalance, $"Transferred to {tarplayer.Name}");
                LogBankChange("TransferPyreals_Target", "Pyreals", Amount, targetOldBalance, targetNewBalance, $"Received from {this.Name}");
                LogTransfer("TransferPyreals", "Pyreals", Amount, CharacterDestination, true, "Transfer completed successfully");
                
                log.Info($"[BANK_DEBUG] Player: {Name} | Transfer completed | Source new balance: {newBalance:N0} | Target new balance: {targetNewBalance:N0}");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[BANK_DEBUG] Player: {Name} | Transfer failed with exception | Error: {ex.Message}");
                LogTransfer("TransferPyreals", "Pyreals", Amount, CharacterDestination, false, $"Exception: {ex.Message}");
                return false;
            }
        }

        public bool TransferLegendaryKeys(long Amount, string CharacterDestination)
        {
            // Validate amount
            if (Amount <= 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Transfer amount must be greater than zero.", ChatMessageType.System));
                return false;
            }
            // Check if player has enough legendary keys to transfer
            if (BankedLegendaryKeys < Amount)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough legendary keys banked to transfer. Need {Amount:N0} but only have {BankedLegendaryKeys:N0}.", ChatMessageType.System));
                return false;
            }
            
            var tarplayer = FindTargetByName(CharacterDestination);
            if (tarplayer == null)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Character '{CharacterDestination}' not found.", ChatMessageType.System));
                return false;
            }
            else
            {
                if (tarplayer is OfflinePlayer)
                {
                    var offlinePlayer = tarplayer as OfflinePlayer;
                    
                    lock (balanceLock)
                    {
                        // Deduct from sender
                        this.BankedLegendaryKeys -= Amount;
                    }
                    
                    // Add to offline receiver
                    if (offlinePlayer.BankedLegendaryKeys == null)
                    {
                        offlinePlayer.BankedLegendaryKeys = Amount;
                    }
                    else
                    {
                        offlinePlayer.BankedLegendaryKeys += Amount;
                    }
                    offlinePlayer.SaveBiotaToDatabase();
                    
                    // Send confirmation to sender
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {Amount:N0} Legendary Keys to {offlinePlayer.Name} (offline)", ChatMessageType.System));
                }
                else
                {
                    var onlinePlayer = (Player)tarplayer;
                    
                    // Deadlock-safe double-lock using consistent ordering
                    object lockA = this.balanceLock;
                    object lockB = onlinePlayer.balanceLock;
                    bool sourceFirst = string.CompareOrdinal(this.Name, onlinePlayer.Name) <= 0;
                    var firstLock = sourceFirst ? lockA : lockB;
                    var secondLock = sourceFirst ? lockB : lockA;
                    
                    lock (firstLock)
                    {
                        lock (secondLock)
                        {
                            // Perform atomic transfer
                            this.BankedLegendaryKeys -= Amount;
                            onlinePlayer.BankedLegendaryKeys = (onlinePlayer.BankedLegendaryKeys ?? 0) + Amount;
                        }
                    }
                    
                    // Send notification outside of locks
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Legendary Keys from {this.Name}", ChatMessageType.System));
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {Amount:N0} Legendary Keys to {onlinePlayer.Name}", ChatMessageType.System));
                }
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLegendaryKeys, this.BankedLegendaryKeys ?? 0));
                
                // Log the transfer
                TransferLogger.LogBankTransfer(this, CharacterDestination, "Legendary Keys", Amount, TransferLogger.TransferTypeBankTransfer);
                
                return true;
            }
        }

        public bool TransferMythicalKeys(long Amount, string CharacterDestination)
        {
            // Validate amount
            if (Amount <= 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Transfer amount must be greater than zero.", ChatMessageType.System));
                return false;
            }
            // Check if player has enough mythical keys to transfer
            if (BankedMythicalKeys < Amount)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough mythical keys banked to transfer. Need {Amount:N0} but only have {BankedMythicalKeys:N0}.", ChatMessageType.System));
                return false;
            }
            
            var tarplayer = FindTargetByName(CharacterDestination);
            if (tarplayer == null)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Character '{CharacterDestination}' not found.", ChatMessageType.System));
                return false;
            }
            else
            {
                if (tarplayer is OfflinePlayer)
                {
                    var offlinePlayer = tarplayer as OfflinePlayer;
                    
                    lock (balanceLock)
                    {
                        // Deduct from sender
                        this.BankedMythicalKeys -= Amount;
                    }
                    
                    // Add to offline receiver
                    if (offlinePlayer.BankedMythicalKeys == null)
                    {
                        offlinePlayer.BankedMythicalKeys = Amount;
                    }
                    else
                    {
                        offlinePlayer.BankedMythicalKeys += Amount;
                    }
                    offlinePlayer.SaveBiotaToDatabase();
                    
                    // Send confirmation to sender
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {Amount:N0} Mythical Keys to {offlinePlayer.Name} (offline)", ChatMessageType.System));
                }
                else
                {
                    var onlinePlayer = (Player)tarplayer;
                    
                    // Deadlock-safe double-lock using consistent ordering
                    object lockA = this.balanceLock;
                    object lockB = onlinePlayer.balanceLock;
                    bool sourceFirst = string.CompareOrdinal(this.Name, onlinePlayer.Name) <= 0;
                    var firstLock = sourceFirst ? lockA : lockB;
                    var secondLock = sourceFirst ? lockB : lockA;
                    
                    lock (firstLock)
                    {
                        lock (secondLock)
                        {
                            // Perform atomic transfer
                            this.BankedMythicalKeys -= Amount;
                            onlinePlayer.BankedMythicalKeys = (onlinePlayer.BankedMythicalKeys ?? 0) + Amount;
                        }
                    }
                    
                    // Send notification outside of locks
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Mythical Keys from {this.Name}", ChatMessageType.System));
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {Amount:N0} Mythical Keys to {onlinePlayer.Name}", ChatMessageType.System));
                    // Persist to database for significant transfers (performance optimization)
                    if (Amount > 1)
                    {
                        onlinePlayer.SavePlayerToDatabase();
                    }
                }
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedMythicalKeys, this.BankedMythicalKeys ?? 0));
                // Persist to database for significant transfers (performance optimization)
                if (Amount > 1)
                {
                    this.SavePlayerToDatabase();
                }

                // Log the transfer
                TransferLogger.LogBankTransfer(this, CharacterDestination, "Mythical Keys", Amount, TransferLogger.TransferTypeBankTransfer);

                return true;
            }
        }

        public bool TransferLuminance(long Amount, string CharacterDestination)
        {
            // Validate amount
            if (Amount <= 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Transfer amount must be greater than zero.", ChatMessageType.System));
                return false;
            }
            // Check if player has enough luminance to transfer
            if (BankedLuminance < Amount)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough luminance banked to transfer. Need {Amount:N0} but only have {BankedLuminance:N0}.", ChatMessageType.System));
                return false;
            }
            
            var tarplayer = FindTargetByName(CharacterDestination);
            if (tarplayer == null)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Character '{CharacterDestination}' not found.", ChatMessageType.System));
                return false;
            }
            
            if (tarplayer is OfflinePlayer)
            {
                var offlinePlayer = tarplayer as OfflinePlayer;
                
                // Verify target player has luminance flagged (earned through gameplay)
                var targetMaxLuminance = offlinePlayer.GetProperty(PropertyInt64.MaximumLuminance);
                if (!targetMaxLuminance.HasValue || targetMaxLuminance == 0)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"{offlinePlayer.Name} has not been luminance flagged yet and cannot receive luminance transfers.", ChatMessageType.System));
                    return false;
                }
                
                if (offlinePlayer.BankedLuminance == null)
                {
                    offlinePlayer.BankedLuminance = 0;
                }

                lock (balanceLock)
                {
                    this.BankedLuminance -= Amount;
                }
                
                offlinePlayer.BankedLuminance += Amount;
                offlinePlayer.SaveBiotaToDatabase();
                
                // Send confirmation to sender
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {Amount:N0} Luminance to {offlinePlayer.Name} (offline)", ChatMessageType.System));
            }
            else
            {
                var onlinePlayer = (Player)tarplayer;
                
                // Verify target player has luminance flagged (earned through gameplay)
                if (!onlinePlayer.MaximumLuminance.HasValue || onlinePlayer.MaximumLuminance == 0)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"{onlinePlayer.Name} has not been luminance flagged yet and cannot receive luminance transfers.", ChatMessageType.System));
                    return false;
                }

                // No capacity check needed for banked luminance

                // Deadlock-safe double-lock using consistent ordering
                object lockA = this.balanceLock;
                object lockB = onlinePlayer.balanceLock;
                bool sourceFirst = string.CompareOrdinal(this.Name, onlinePlayer.Name) <= 0;
                var firstLock = sourceFirst ? lockA : lockB;
                var secondLock = sourceFirst ? lockB : lockA;
                
                lock (firstLock)
                {
                    lock (secondLock)
                    {
                        // Perform atomic transfer
                        this.BankedLuminance -= Amount;
                        onlinePlayer.BankedLuminance = (onlinePlayer.BankedLuminance ?? 0) + Amount;
                    }
                }
                
                    // Send notification outside of locks
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} luminance from {this.Name}", ChatMessageType.System));
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {Amount:N0} Luminance to {onlinePlayer.Name}", ChatMessageType.System));
                    
                    // Persist to database for significant transfers (performance optimization)
                    if (Amount > 100000)
                    {
                        onlinePlayer.SavePlayerToDatabase();
                    }
                }
                
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLuminance, this.BankedLuminance ?? 0));
                // Persist to database for significant transfers (performance optimization)
                if (Amount > 100000)
                {
                    this.SavePlayerToDatabase();
                }
            
            // Log the transfer
            TransferLogger.LogBankTransfer(this, CharacterDestination, "Luminance", Amount, TransferLogger.TransferTypeBankTransfer);
            
            return true;
        }
        public bool TransferEnlightenedCoins(long Amount, string CharacterDestination)
        {
            // Validate amount
            if (Amount <= 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Transfer amount must be greater than zero.", ChatMessageType.System));
                return false;
            }
            // Check if player has enough enlightened coins to transfer
            if (BankedEnlightenedCoins < Amount)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough enlightened coins banked to transfer. Need {Amount:N0} but only have {BankedEnlightenedCoins:N0}.", ChatMessageType.System));
                return false;
            }
            
            var tarplayer = FindTargetByName(CharacterDestination);
            if (tarplayer == null)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Character '{CharacterDestination}' not found.", ChatMessageType.System));
                return false;
            }
            else
            {
                if (tarplayer is OfflinePlayer)
                {
                    var offlinePlayer = tarplayer as OfflinePlayer;
                    
                    lock (balanceLock)
                    {
                        // Deduct from sender
                        this.BankedEnlightenedCoins -= Amount;
                    }
                    
                    // Add to offline receiver
                    if (offlinePlayer.BankedEnlightenedCoins == null)
                    {
                        offlinePlayer.BankedEnlightenedCoins = Amount;
                    }
                    else
                    {
                        offlinePlayer.BankedEnlightenedCoins += Amount;
                    }
                    offlinePlayer.SaveBiotaToDatabase();
                    
                    // Send confirmation to sender
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {Amount:N0} Enlightened Coins to {offlinePlayer.Name} (offline)", ChatMessageType.System));
                }
                else
                {
                    var onlinePlayer = (Player)tarplayer;
                    
                    // Deadlock-safe double-lock using consistent ordering
                    object lockA = this.balanceLock;
                    object lockB = onlinePlayer.balanceLock;
                    bool sourceFirst = string.CompareOrdinal(this.Name, onlinePlayer.Name) <= 0;
                    var firstLock = sourceFirst ? lockA : lockB;
                    var secondLock = sourceFirst ? lockB : lockA;
                    
                    lock (firstLock)
                    {
                        lock (secondLock)
                        {
                            // Perform atomic transfer
                            this.BankedEnlightenedCoins -= Amount;
                            onlinePlayer.BankedEnlightenedCoins = (onlinePlayer.BankedEnlightenedCoins ?? 0) + Amount;
                        }
                    }
                    
                    // Send notification outside of locks
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Enlightened Coins from {this.Name}", ChatMessageType.System));
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {Amount:N0} Enlightened Coins to {onlinePlayer.Name}", ChatMessageType.System));
                    // Persist to database for significant transfers (performance optimization)
                    if (Amount > 10)
                    {
                        onlinePlayer.SavePlayerToDatabase();
                    }
                }
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedEnlightenedCoins, this.BankedEnlightenedCoins ?? 0));
                // Persist to database for significant transfers (performance optimization)
                if (Amount > 10)
                {
                    this.SavePlayerToDatabase();
                }
                
                // Log the transfer
                TransferLogger.LogBankTransfer(this, CharacterDestination, "Enlightened Coins", Amount, TransferLogger.TransferTypeBankTransfer);
                
                return true;
            }
        }
        public bool TransferWeaklyEnlightenedCoins(long Amount, string CharacterDestination)
        {
            // Validate amount
            if (Amount <= 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Transfer amount must be greater than zero.", ChatMessageType.System));
                return false;
            }
            // Check if player has enough weakly enlightened coins to transfer
            if (BankedWeaklyEnlightenedCoins < Amount)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough weakly enlightened coins banked to transfer. Need {Amount:N0} but only have {BankedWeaklyEnlightenedCoins:N0}.", ChatMessageType.System));
                return false;
            }
            
            var tarplayer = FindTargetByName(CharacterDestination);
            if (tarplayer == null)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Character '{CharacterDestination}' not found.", ChatMessageType.System));
                return false;
            }
            else
            {
                if (tarplayer is OfflinePlayer)
                {
                    var offlinePlayer = tarplayer as OfflinePlayer;
                    
                    lock (balanceLock)
                    {
                        // Deduct from sender
                        this.BankedWeaklyEnlightenedCoins -= Amount;
                    }
                    
                    // Add to offline receiver
                    if (offlinePlayer.BankedWeaklyEnlightenedCoins == null)
                    {
                        offlinePlayer.BankedWeaklyEnlightenedCoins = Amount;
                    }
                    else
                    {
                        offlinePlayer.BankedWeaklyEnlightenedCoins += Amount;
                    }
                    offlinePlayer.SaveBiotaToDatabase();
                    
                    // Send confirmation to sender
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {Amount:N0} Weakly Enlightened Coins to {offlinePlayer.Name} (offline)", ChatMessageType.System));
                }
                else
                {
                    var onlinePlayer = (Player)tarplayer;
                    
                    // Deadlock-safe double-lock using consistent ordering
                    object lockA = this.balanceLock;
                    object lockB = onlinePlayer.balanceLock;
                    bool sourceFirst = string.CompareOrdinal(this.Name, onlinePlayer.Name) <= 0;
                    var firstLock = sourceFirst ? lockA : lockB;
                    var secondLock = sourceFirst ? lockB : lockA;
                    
                    lock (firstLock)
                    {
                        lock (secondLock)
                        {
                            // Perform atomic transfer
                            this.BankedWeaklyEnlightenedCoins -= Amount;
                            onlinePlayer.BankedWeaklyEnlightenedCoins = (onlinePlayer.BankedWeaklyEnlightenedCoins ?? 0) + Amount;
                        }
                    }
                    
                    // Send notification outside of locks
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Weakly Enlightened Coins from {this.Name}", ChatMessageType.System));
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {Amount:N0} Weakly Enlightened Coins to {onlinePlayer.Name}", ChatMessageType.System));
                    // Persist to database for significant transfers (performance optimization)
                    if (Amount > 10)
                    {
                        onlinePlayer.SavePlayerToDatabase();
                    }
                }
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedWeaklyEnlightenedCoins, this.BankedWeaklyEnlightenedCoins ?? 0));
                // Persist to database for significant transfers (performance optimization)
                if (Amount > 10)
                {
                    this.SavePlayerToDatabase();
                }
                
                // Log the transfer
                TransferLogger.LogBankTransfer(this, CharacterDestination, "Weakly Enlightened Coins", Amount, TransferLogger.TransferTypeBankTransfer);
                
                return true;
            }
        }
        public long? BankedLuminance
        {
            get => GetProperty(PropertyInt64.BankedLuminance) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedLuminance); else SetProperty(PropertyInt64.BankedLuminance, value.Value); }
        }

        public long? BankedPyreals
        {
            get => GetProperty(PropertyInt64.BankedPyreals) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedPyreals); else SetProperty(PropertyInt64.BankedPyreals, value.Value); }
        }

        public long? BankedLegendaryKeys
        {
            get => GetProperty(PropertyInt64.BankedLegendaryKeys) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedLegendaryKeys); else SetProperty(PropertyInt64.BankedLegendaryKeys, value.Value); }
        }
        public long? BankedEnlightenedCoins
        {
            get => GetProperty(PropertyInt64.BankedEnlightenedCoins) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedEnlightenedCoins); else SetProperty(PropertyInt64.BankedEnlightenedCoins, value.Value); }
        }
        public long? BankedWeaklyEnlightenedCoins
        {
            get => GetProperty(PropertyInt64.BankedWeaklyEnlightenedCoins) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedWeaklyEnlightenedCoins); else SetProperty(PropertyInt64.BankedWeaklyEnlightenedCoins, value.Value); }
        }
        public long? BankedMythicalKeys
        {
            get => GetProperty(PropertyInt64.BankedMythicalKeys) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedMythicalKeys); else SetProperty(PropertyInt64.BankedMythicalKeys, value.Value); }
        }
    }
}
