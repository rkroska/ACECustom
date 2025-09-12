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

        #region Bank Debugging Helpers

        /// <summary>
        /// Logs bank balance changes for debugging and audit purposes
        /// </summary>
        private void LogBankChange(string operation, string currencyType, long amount, long oldBalance, long newBalance, string details = "")
        {
            string message = $"[BANK_DEBUG] Player: {Name} | Operation: {operation} | Currency: {currencyType} | Amount: {amount:N0} | Old: {oldBalance:N0} | New: {newBalance:N0} | Details: {details}";
            log.Info(message);
            Console.WriteLine(message);
        }

        /// <summary>
        /// Logs item consumption for debugging
        /// </summary>
        private void LogItemConsumption(string operation, WorldObject item, bool success, string details = "")
        {
            string message = $"[BANK_DEBUG] Player: {Name} | Operation: {operation} | Item: {item.Name} (WCID: {item.WeenieClassId}) | Value: {item.Value ?? 0} | Success: {success} | Details: {details}";
            log.Info(message);
            Console.WriteLine(message);
        }

        /// <summary>
        /// Logs transfer operations for debugging
        /// </summary>
        private void LogTransfer(string operation, string currencyType, long amount, string targetPlayer, bool success, string details = "")
        {
            string message = $"[BANK_DEBUG] Player: {Name} | Operation: {operation} | Currency: {currencyType} | Amount: {amount:N0} | Target: {targetPlayer} | Success: {success} | Details: {details}";
            log.Info(message);
            Console.WriteLine(message);
        }

        /// <summary>
        /// Logs inventory scanning operations for performance debugging
        /// </summary>
        private void LogInventoryScan(string operation, int itemsFound, int containersScanned, long processingTimeMs, string details = "")
        {
            string message = $"[BANK_DEBUG] Player: {Name} | Operation: {operation} | Items Found: {itemsFound} | Containers Scanned: {containersScanned} | Processing Time: {processingTimeMs}ms | Details: {details}";
            log.Info(message);
            Console.WriteLine(message);
        }

        /// <summary>
        /// Helper method to log and print to console
        /// </summary>
        private void LogAndPrint(string message)
        {
            log.Info(message);
            Console.WriteLine(message);
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
                long itemValue = (long)item.StackSize;
                cash += itemValue;
                log.Info($"[BANK_DEBUG] Player: {Name} | Pyreal item: {item.Name} (WCID: {item.WeenieClassId}) | StackSize: {item.StackSize} | Value: {itemValue}");
            }

            if (cash > 0)
            {
                long oldBalance = BankedPyreals ?? 0;
                DepositPyreals(cash, suppressChat);
                long newBalance = BankedPyreals ?? 0;
                
                LogBankChange("DepositPyreals", "Pyreals", cash, oldBalance, newBalance, $"Found {pyrealsList.Count} items");
                if (!suppressChat)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {cash:N0} pyreals", ChatMessageType.System));
                }
            }
            else
            {
                log.Info($"[BANK_DEBUG] Player: {Name} | No pyreals found to deposit");
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
            log.Info($"[BANK_DEBUG] Player: {Name} | Starting DepositPyreals(Amount: {Amount:N0}) operation");
            
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
                
                log.Info($"[BANK_DEBUG] Player: {Name} | Found {pyrealsList.Count} pyreal items for specific amount deposit");

                foreach (var item in pyrealsList)
                {
                    log.Info($"[BANK_DEBUG] Player: {Name} | Processing pyreal item: {item.Name} (WCID: {item.WeenieClassId}) | StackSize: {item.StackSize} | Remaining Amount: {Amount:N0}");
                    
                    if (item.StackSize == 25000 && Amount >= 25000) //full stacks
                    {
                        Amount -= 25000;
                        bool success = this.TryConsumeFromInventoryWithNetworking(item);
                        LogItemConsumption("DepositPyreals_FullStack", item, success, $"25000 pyreals");
                        
                        if (success)
                        {
                            BankedPyreals += 25000;
                            totalDeposited += 25000;
                            log.Info($"[BANK_DEBUG] Player: {Name} | Successfully deposited 25000 pyreals | New BankedPyreals: {BankedPyreals:N0}");
                        }
                    }
                    else if (Amount >= item.StackSize)
                    {
                        bool success = this.TryConsumeFromInventoryWithNetworking(item, (int)Amount);
                        LogItemConsumption("DepositPyreals_PartialStack", item, success, $"Amount: {Amount:N0}");
                        
                        if (success)
                        {
                            long consumedAmount = item.StackSize ?? 0;
                            Amount -= consumedAmount;
                            BankedPyreals += consumedAmount;
                            totalDeposited += consumedAmount;
                            log.Info($"[BANK_DEBUG] Player: {Name} | Successfully deposited {consumedAmount:N0} pyreals | New BankedPyreals: {BankedPyreals:N0}");
                        }
                    }
                }
                
                long newBalance = BankedPyreals ?? 0;
                LogBankChange("DepositPyreals_Specific", "Pyreals", totalDeposited, oldBalance, newBalance, $"Processed {pyrealsList.Count} items");
                
                if (totalDeposited > 0 && !suppressChat)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {totalDeposited:N0} pyreals", ChatMessageType.System));
                }
                else if (!suppressChat)
                {
                    log.Info($"[BANK_DEBUG] Player: {Name} | No pyreals were deposited");
                    Session.Network.EnqueueSend(new GameMessageSystemChat("No pyreals found to deposit", ChatMessageType.System));
                }
            }          
        }

        public void DepositLegendaryKeys()
        {
            if (BankedLegendaryKeys == null)
            {
                BankedLegendaryKeys = 0;
            }
            lock (balanceLock)
            {
                long totalDeposited = 0;
                //int i = 0;
                var keysList = this.GetInventoryItemsOfWCID(48746);
                foreach (var item in keysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(item))
                    {
                        BankedLegendaryKeys += 1;
                        totalDeposited += 1;
                    }
                    else
                    {
                        break;
                    }
                }

                var rtwKeysList = this.GetInventoryItemsOfWCID(52010); //Rynthid Keys
                foreach (var rtw in rtwKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(rtw))
                    {
                        BankedLegendaryKeys += rtw.Structure ?? 5;
                        totalDeposited += rtw.Structure ?? 5;
                    }
                    else
                    {
                        break;
                    }
                }

                var durKeysList = this.GetInventoryItemsOfWCID(51954); //Durable legendary keys
                foreach (var dur in durKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(dur))
                    {
                        BankedLegendaryKeys += dur.Structure ?? 10;
                        totalDeposited += dur.Structure ?? 10;
                    }
                    else
                    {
                        break;
                    }
                }

                var legKeysList = this.GetInventoryItemsOfWCID(48748); //2-use Legendary keys
                foreach (var leg in legKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(leg))
                    {
                        BankedLegendaryKeys += leg.Structure ?? 2;
                        totalDeposited += leg.Structure ?? 2;
                    }
                    else
                    {
                        break;
                    }
                }

                var legeventKeysList = this.GetInventoryItemsOfWCID(500010); //25-use Legendary keys
                foreach (var legevent in legeventKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(legevent))
                    {
                        BankedLegendaryKeys += legevent.Structure ?? 25;
                        totalDeposited += legevent.Structure ?? 25;
                    }
                    else
                    {
                        break;
                    }
                }
                
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

        public void DepositMythicalKeys()
        {
            if (BankedMythicalKeys == null)
            {
                BankedMythicalKeys = 0;
            }
            lock (balanceLock)
            {
                long totalDeposited = 0;
                //int i = 0;
                var MythkeysList = this.GetInventoryItemsOfWCID(90000104);
                foreach (var item in MythkeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(item))
                    {
                        BankedMythicalKeys += 1;
                        totalDeposited += 1;
                    }
                    else
                    {
                        break;
                    }
                }

                var FiveuseKeysList = this.GetInventoryItemsOfWCID(90000108); //5 use Keys
                foreach (var Fiveuse in FiveuseKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(Fiveuse))
                    {
                        BankedMythicalKeys += Fiveuse.Structure ?? 5;
                        totalDeposited += Fiveuse.Structure ?? 5;
                    }
                    else
                    {
                        break;
                    }
                }


                var durMythKeysList = this.GetInventoryItemsOfWCID(90000109); //Durable Mythical keys
                foreach (var durmyth in durMythKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(durmyth))
                    {
                        BankedMythicalKeys += durmyth.Structure ?? 10;
                        totalDeposited += durmyth.Structure ?? 10;
                    }
                    else
                    {
                        break;
                    }
                }

                var MythKeysList = this.GetInventoryItemsOfWCID(90000107); //2-use Mythical keys
                foreach (var Myth in MythKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(Myth))
                    {
                        BankedMythicalKeys += Myth.Structure ?? 2;
                        totalDeposited += Myth.Structure ?? 2;
                    }
                    else
                    {
                        break;
                    }
                }

                var MytheventKeysList = this.GetInventoryItemsOfWCID(90000110); //25-use Mythical keys
                foreach (var Mythevent in MytheventKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(Mythevent))
                    {
                        BankedMythicalKeys += Mythevent.Structure ?? 25;
                        totalDeposited += Mythevent.Structure ?? 25;
                    }
                    else
                    {
                        break;
                    }
                }
                
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

        public void DepositPeas()
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
                
                log.Info($"[BANK_DEBUG] Player: {Name} | Starting single-pass inventory scan with DFS traversal");
                
                // Add main inventory
                allItems.AddRange(this.Inventory.Values);
                log.Info($"[BANK_DEBUG] Player: {Name} | Added {this.Inventory.Values.Count} items from main inventory");
                
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
                log.Info($"[BANK_DEBUG] Player: {Name} | Added {containersAdded} side containers to DFS stack");
                
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
                        log.Info($"[BANK_DEBUG] Player: {Name} | Container {containersProcessed}: Added {itemsInContainer} items | Total items so far: {allItems.Count}");
                        
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
                            log.Info($"[BANK_DEBUG] Player: {Name} | Container {containersProcessed}: Added {nestedAdded} nested containers to stack");
                        }
                    }
                }
                
                log.Info($"[BANK_DEBUG] Player: {Name} | DFS traversal complete | Total containers processed: {containersProcessed} | Total items collected: {allItems.Count}");
                
                // Filter in memory - no more scans!
                var pyreals = allItems.Where(i => i.WeenieClassId == 8330);
                var gold = allItems.Where(i => i.WeenieClassId == 8327);
                var silver = allItems.Where(i => i.WeenieClassId == 8331);
                var copper = allItems.Where(i => i.WeenieClassId == 8326);
                
                log.Info($"[BANK_DEBUG] Player: {Name} | Currency filtering complete | Pyreals: {pyreals.Count()} | Gold: {gold.Count()} | Silver: {silver.Count()} | Copper: {copper.Count()}");
                
                // Process pyreals
                int pyrealsProcessed = 0;
                foreach (var pyreal in pyreals)
                {
                    int val = pyreal.Value ?? 0;
                    if (val > 0)
                    {
                        bool success = this.TryConsumeFromInventoryWithNetworking(pyreal);
                        LogItemConsumption("DepositPeas_Pyreal", pyreal, success, $"Value: {val}");
                        
                        if (success)
                        {
                            BankedPyreals += val;
                            totalDeposited += val;
                            pyrealsProcessed++;
                            log.Info($"[BANK_DEBUG] Player: {Name} | Pyreal processed | Value: {val:N0} | Total deposited: {totalDeposited:N0}");
                        }
                    }
                }
                log.Info($"[BANK_DEBUG] Player: {Name} | Pyreals processing complete | Processed: {pyrealsProcessed}/{pyreals.Count()}");

                // Process gold
                int goldProcessed = 0;
                foreach (var goldItem in gold)
                {
                    int val = goldItem.Value ?? 0;
                    if (val > 0)
                    {
                        bool success = this.TryConsumeFromInventoryWithNetworking(goldItem);
                        LogItemConsumption("DepositPeas_Gold", goldItem, success, $"Value: {val}");
                        
                        if (success)
                        {
                            BankedPyreals += val;
                            totalDeposited += val;
                            goldProcessed++;
                            log.Info($"[BANK_DEBUG] Player: {Name} | Gold processed | Value: {val:N0} | Total deposited: {totalDeposited:N0}");
                        }
                    }
                }
                log.Info($"[BANK_DEBUG] Player: {Name} | Gold processing complete | Processed: {goldProcessed}/{gold.Count()}");

                // Process silver
                int silverProcessed = 0;
                foreach (var silverItem in silver)
                {
                    int val = silverItem.Value ?? 0;
                    if (val > 0)
                    {
                        bool success = this.TryConsumeFromInventoryWithNetworking(silverItem);
                        LogItemConsumption("DepositPeas_Silver", silverItem, success, $"Value: {val}");
                        
                        if (success)
                        {
                            BankedPyreals += val;
                            totalDeposited += val;
                            silverProcessed++;
                            log.Info($"[BANK_DEBUG] Player: {Name} | Silver processed | Value: {val:N0} | Total deposited: {totalDeposited:N0}");
                        }
                    }
                }
                log.Info($"[BANK_DEBUG] Player: {Name} | Silver processing complete | Processed: {silverProcessed}/{silver.Count()}");

                // Process copper
                int copperProcessed = 0;
                foreach (var copperItem in copper)
                {
                    int val = copperItem.Value ?? 0;
                    if (val > 0)
                    {
                        bool success = this.TryConsumeFromInventoryWithNetworking(copperItem);
                        LogItemConsumption("DepositPeas_Copper", copperItem, success, $"Value: {val}");
                        
                        if (success)
                        {
                            BankedPyreals += val;
                            totalDeposited += val;
                            copperProcessed++;
                            log.Info($"[BANK_DEBUG] Player: {Name} | Copper processed | Value: {val:N0} | Total deposited: {totalDeposited:N0}");
                        }
                    }
                }
                log.Info($"[BANK_DEBUG] Player: {Name} | Copper processing complete | Processed: {copperProcessed}/{copper.Count()}");
                
                stopwatch.Stop();
                long newBalance = BankedPyreals ?? 0;
                
                LogBankChange("DepositPeas", "Pyreals", totalDeposited, oldBalance, newBalance, 
                    $"Performance: {stopwatch.ElapsedMilliseconds}ms | Items: {allItems.Count} | Containers: {containersProcessed}");
                
                LogInventoryScan("DepositPeas", allItems.Count, containersProcessed, stopwatch.ElapsedMilliseconds, 
                    $"Pyreals: {pyrealsProcessed} | Gold: {goldProcessed} | Silver: {silverProcessed} | Copper: {copperProcessed}");
                
                if (totalDeposited > 0)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {totalDeposited:N0} pyreal peas", ChatMessageType.System));
                }
                else
                {
                    log.Info($"[BANK_DEBUG] Player: {Name} | No pyreal peas were deposited");
                    Session.Network.EnqueueSend(new GameMessageSystemChat("No pyreal peas found to deposit", ChatMessageType.System));
                }
            }
        }

        public void DepositEnlightenedCoins()
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
                
                if (totalDeposited > 0)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {totalDeposited:N0} enlightened coins", ChatMessageType.System));
                }
                else
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat("No enlightened coins found to deposit", ChatMessageType.System));
                }
            }

            this.SavePlayerToDatabase();
        }

        public void DepositWeaklyEnlightenedCoins()
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
                
                if (totalDeposited > 0)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {totalDeposited:N0} weakly enlightened coins", ChatMessageType.System));
                }
                else
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat("No weakly enlightened coins found to deposit", ChatMessageType.System));
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
                DepositLuminance(availableLuminance, suppressChat);
                if (!suppressChat)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {availableLuminance:N0} luminance", ChatMessageType.System));
                }
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
        public void DepositTradeNotes()
        {
            if (BankedPyreals == null)
            {
                BankedPyreals = 0;
            }
            lock(balanceLock)
            {
                long totalDeposited = 0;
                var notesList = this.GetTradeNotes();
                foreach (var note in notesList)
                {
                    int val = note.Value ?? 0;
                    if (val > 0)
                    {
                        if (this.TryConsumeFromInventoryWithNetworking(note))
                        {
                            BankedPyreals += val;
                            totalDeposited += val;
                        }
                    }                    
                }
                
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

        /// <summary>
        /// Withdraw luminance from the bank
        /// </summary>
        public void WithdrawLuminance(long Amount)
        {
            if (!this.MaximumLuminance.HasValue || this.MaximumLuminance == 0)
            {
                this.MaximumLuminance = 1500000;
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.MaximumLuminance, this.MaximumLuminance ?? 0));
            }
            
            // Check if player has enough luminance
            if (BankedLuminance < Amount)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough luminance banked. Need {Amount:N0} but only have {BankedLuminance:N0}.", ChatMessageType.System));
                return;
            }

            if (Amount <= this.BankedLuminance)
            {
                lock (balanceLock)
                {
                    this.AvailableLuminance += Amount;
                    BankedLuminance -= Amount;
                }
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Withdrew {Amount:N0} luminance", ChatMessageType.System));
            }
            else
            {
                lock (balanceLock)
                {
                    long actualAmount = this.BankedLuminance ?? 0;
                    this.AvailableLuminance += actualAmount;
                    BankedLuminance = 0;
                }
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Withdrew {this.BankedLuminance ?? 0:N0} luminance (requested {Amount:N0})", ChatMessageType.System));
            }
            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableLuminance, this.AvailableLuminance ?? 0));
            //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLuminance, this.BankedLuminance ?? 0));
        }

        private bool CreatePyreals(int Amount)
        {
            WorldObject smallCoins = WorldObjectFactory.CreateNewWorldObject(273);
            if (smallCoins == null)
                return false;
            smallCoins.SetStackSize(Amount);
            var itemCreated = this.TryCreateInInventoryWithNetworking(smallCoins);
            if (itemCreated)
            {
                // Note: BankedPyreals is already decremented in WithdrawPyreals
                // No need to decrement again here
                return true;
            }
            return false;
        }



        public void WithdrawPyreals(long Amount)
        {
            LogAndPrint($"[BANK_DEBUG] Player: {Name} | Starting WithdrawPyreals operation | Amount: {Amount:N0} | Current BankedPyreals: {BankedPyreals:N0}");
            
            lock (balanceLock)
            {
                // Check if player has enough pyreals
                if (BankedPyreals < Amount)
                {
                    log.Info($"[BANK_DEBUG] Player: {Name} | Insufficient funds | Requested: {Amount:N0} | Available: {BankedPyreals:N0}");
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough pyreals banked. Need {Amount:N0} pyreals but only have {BankedPyreals:N0}.", ChatMessageType.System));
                    return;
                }
                
                long oldBalance = BankedPyreals ?? 0;
                
                // First, create trade notes for very large amounts to make them manageable
                long remainingAmount = Amount;
                long totalWithdrawn = 0;
                int tradeNotesCreated = 0;
                int pyrealsCreated = 0;
                
                // Create 250,000 trade notes for amounts >= 250,000
                if (remainingAmount >= 250000)
                {
                    int tradeNoteCount = (int)(remainingAmount / 250000);
                    log.Info($"[BANK_DEBUG] Player: {Name} | Creating {tradeNoteCount} trade notes (250k each) | Remaining amount: {remainingAmount:N0}");
                    
                    var tradeNote = WorldObjectFactory.CreateNewWorldObject(20630); // tradenote250000
                    if (tradeNote == null)
                    {
                        log.Error($"[BANK_DEBUG] Player: {Name} | Failed to create trade note WorldObject");
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create trade notes. Withdrawal cancelled.", ChatMessageType.System));
                        return;
                    }
                    tradeNote.SetStackSize(tradeNoteCount);
                    
                    bool success = this.TryCreateInInventoryWithNetworking(tradeNote);
                    log.Info($"[BANK_DEBUG] Player: {Name} | Trade note creation | Count: {tradeNoteCount} | Success: {success}");
                    
                    if (success)
                    {
                        long tradeNoteValue = tradeNoteCount * 250000;
                        remainingAmount -= tradeNoteValue;
                        BankedPyreals -= tradeNoteValue;
                        totalWithdrawn += tradeNoteValue;
                        tradeNotesCreated = tradeNoteCount;
                        
                        log.Info($"[BANK_DEBUG] Player: {Name} | Trade notes created successfully | Value: {tradeNoteValue:N0} | New BankedPyreals: {BankedPyreals:N0}");
                    }
                    else
                    {
                        log.Info($"[BANK_DEBUG] Player: {Name} | Failed to add trade notes to inventory - insufficient pack space");
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to add trade notes to inventory - check pack space. Withdrawal cancelled.", ChatMessageType.System));
                        return;
                    }
                }
                
                // For any remaining amount, create pyreal coins
                if (remainingAmount > 0)
                {
                    log.Info($"[BANK_DEBUG] Player: {Name} | Creating {remainingAmount:N0} pyreal coins for remaining amount");
                    
                    bool success = CreatePyreals((int)remainingAmount);
                    log.Info($"[BANK_DEBUG] Player: {Name} | Pyreal coin creation | Amount: {remainingAmount:N0} | Success: {success}");
                    
                    if (success)
                    {
                        totalWithdrawn += remainingAmount;
                        pyrealsCreated = (int)remainingAmount;
                        log.Info($"[BANK_DEBUG] Player: {Name} | Pyreal coins created successfully | Amount: {remainingAmount:N0}");
                    }
                    else
                    {
                        log.Info($"[BANK_DEBUG] Player: {Name} | Failed to create pyreal coins - insufficient pack space");
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create pyreal coins - check pack space. Withdrawal may be incomplete.", ChatMessageType.System));
                    }
                }
                
                long newBalance = BankedPyreals ?? 0;
                LogBankChange("WithdrawPyreals", "Pyreals", totalWithdrawn, oldBalance, newBalance, 
                    $"Trade Notes: {tradeNotesCreated} | Pyreals: {pyrealsCreated} | Requested: {Amount:N0}");
                
                // Concise summary message
                if (totalWithdrawn > 0)
                {
                    string summary = "";
                    
                    if (tradeNotesCreated > 0 && pyrealsCreated > 0)
                    {
                        summary = $"Withdrew {tradeNotesCreated:N0} trade note(s) worth {tradeNotesCreated * 250000:N0} pyreals and {pyrealsCreated:N0} pyreals";
                    }
                    else if (tradeNotesCreated > 0)
                    {
                        summary = $"Withdrew {tradeNotesCreated:N0} trade note(s) worth {tradeNotesCreated * 250000:N0} pyreals";
                    }
                    else if (pyrealsCreated > 0)
                    {
                        summary = $"Withdrew {pyrealsCreated:N0} pyreals";
                    }
                    else
                    {
                        summary = $"Withdrew {totalWithdrawn:N0} pyreals";
                    }
                    
                    log.Info($"[BANK_DEBUG] Player: {Name} | Withdrawal summary: {summary}");
                    Session.Network.EnqueueSend(new GameMessageSystemChat(summary, ChatMessageType.System));
                }
                
                // Flag any discrepancies
                if (totalWithdrawn != Amount)
                {
                    log.Warn($"[BANK_DEBUG] Player: {Name} | Withdrawal discrepancy | Requested: {Amount:N0} | Withdrawn: {totalWithdrawn:N0}");
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Warning: Requested {Amount:N0} pyreals but only {totalWithdrawn:N0} was withdrawn.", ChatMessageType.System));
                }
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
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create 25-use legendary keys - check pack space. Withdrawal cancelled.", ChatMessageType.System));
                        return;
                    }
                    remainingAmount -= 25;
                    totalWithdrawn += 25;
                    keys25Created++;
                }
                while (remainingAmount >= 10)
                {
                    if (!CreateLegendaryKey(51954, 10)) 
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create 10-use legendary keys - check pack space. Withdrawal cancelled.", ChatMessageType.System));
                        return;
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
                        summary += $" and {keys10Created:N0} 10-use key(s)";
                    }
                    
                    if (keys1Created > 0)
                    {
                        summary += $" and {keys1Created:N0} single-use key(s)";
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
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create 25-use mythical keys - check pack space. Withdrawal cancelled.", ChatMessageType.System));
                        return;
                    }
                    remainingAmount -= 25;
                    totalWithdrawn += 25;
                    keys25Created++;
                }
                while (remainingAmount >= 10)
                {
                    if (!CreateMythicalKey(90000109, 10)) 
                    {
                        Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create 10-use mythical keys - check pack space. Withdrawal cancelled.", ChatMessageType.System));
                        return;
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
                        summary += $" and {keys10Created:N0} 10-use key(s)";
                    }
                    
                    if (keys1Created > 0)
                    {
                        summary += $" and {keys1Created:N0} single-use key(s)";
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

        private bool CreateEnlightenedCoins(int Amount)
        {
            WorldObject wo = WorldObjectFactory.CreateNewWorldObject(300004);
            if (wo == null)
                return false;
            wo.SetStackSize(Amount);
            var itemCreated = this.TryCreateInInventoryWithNetworking(wo);
            if (itemCreated)
            {
                BankedEnlightenedCoins -= Amount;
                return true;
            }
            return false;
        }

        public void WithdrawEnlightenedCoins(long Amount)
        {
            lock (balanceLock)
            {
                // Check if player has enough enlightened coins
                if (BankedEnlightenedCoins < Amount)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough enlightened coins banked. Need {Amount:N0} coins but only have {BankedEnlightenedCoins:N0}.", ChatMessageType.System));
                    return;
                }
                
                if (CreateEnlightenedCoins((int)Amount))
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Withdrew {Amount:N0} enlightened coins", ChatMessageType.System));
                }
                else
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create enlightened coins - check pack space. Withdrawal cancelled.", ChatMessageType.System));
                }
            }
        }

        private bool CreateWeaklyEnlightenedCoins(int Amount)
        {
            WorldObject wo = WorldObjectFactory.CreateNewWorldObject(300003);
            if (wo == null)
                return false;
            wo.SetStackSize(Amount);
            var itemCreated = this.TryCreateInInventoryWithNetworking(wo);
            if (itemCreated)
            {
                BankedWeaklyEnlightenedCoins -= Amount;
                return true;
            }
            return false;
        }

        public void WithdrawWeaklyEnlightenedCoins(long Amount)
        {
            lock (balanceLock)
            {
                // Check if player has enough weakly enlightened coins
                if (BankedWeaklyEnlightenedCoins < Amount)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"You don't have enough weakly enlightened coins banked. Need {Amount:N0} coins but only have {BankedWeaklyEnlightenedCoins:N0}.", ChatMessageType.System));
                    return;
                }
                
                if (CreateWeaklyEnlightenedCoins((int)Amount))
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Withdrew {Amount:N0} weakly enlightened coins", ChatMessageType.System));
                }
                else
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create weakly enlightened coins - check pack space. Withdrawal cancelled.", ChatMessageType.System));
                }
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

            if (count > 1000) // Reasonable limit to prevent abuse
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Count cannot exceed 1,000", ChatMessageType.System));
                return;
            }

            lock (balanceLock)
            {
                uint weenieId = 0;
                long noteValue = 0;
                string noteName = "";

                // Map denomination to weenie ID and value
                switch (denomination.ToLower())
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
                        weenieId = 20626; // tradenote50000
                        noteValue = 50000;
                        noteName = "50,000 pyreal";
                        break;
                    case "m":
                        weenieId = 20627; // tradenote100000
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

                // Create the trade note(s)
                var tradeNote = WorldObjectFactory.CreateNewWorldObject(weenieId);
                if (tradeNote == null)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to create trade notes. Withdrawal cancelled.", ChatMessageType.System));
                    return;
                }

                tradeNote.SetStackSize(count);
                if (this.TryCreateInInventoryWithNetworking(tradeNote))
                {
                    BankedPyreals -= totalCost;
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Withdrew {count} {denomination.ToUpper()} notes worth {totalCost:N0} pyreals", ChatMessageType.System));
                }
                else
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to add trade notes to inventory - check pack space. Withdrawal cancelled.", ChatMessageType.System));
                }
            }
        }

        private IPlayer FindTargetByName(string name)
        {
            return PlayerManager.FindFirstPlayerByName(name);
        }

        public bool TransferPyreals(long Amount, string CharacterDestination)
        {
            LogAndPrint($"[BANK_DEBUG] Player: {Name} | Starting TransferPyreals operation | Amount: {Amount:N0} | Target: {CharacterDestination} | Current BankedPyreals: {BankedPyreals:N0}");
            
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
                    log.Info($"[BANK_DEBUG] Player: {Name} | Transferring to offline player | Target: {offlinePlayer.Name} | Target old balance: {offlinePlayer.BankedPyreals:N0}");
                    
                    if (offlinePlayer.BankedPyreals == null)
                    {
                        offlinePlayer.BankedPyreals = Amount;
                        log.Info($"[BANK_DEBUG] Player: {Name} | Initialized target player's BankedPyreals to {Amount:N0}");
                    }
                    else
                    {
                        offlinePlayer.BankedPyreals += Amount;
                        log.Info($"[BANK_DEBUG] Player: {Name} | Added {Amount:N0} to target player's BankedPyreals | New balance: {offlinePlayer.BankedPyreals:N0}");
                    }
                }
                else
                {
                    var onlinePlayer = tarplayer as Player;
                    log.Info($"[BANK_DEBUG] Player: {Name} | Transferring to online player | Target: {onlinePlayer.Name} | Target old balance: {onlinePlayer.BankedPyreals:N0}");
                    
                    if (onlinePlayer.BankedPyreals == null)
                    {
                        onlinePlayer.BankedPyreals = Amount;
                        log.Info($"[BANK_DEBUG] Player: {Name} | Initialized target player's BankedPyreals to {Amount:N0}");
                    }
                    else
                    {
                        onlinePlayer.BankedPyreals += Amount;
                        log.Info($"[BANK_DEBUG] Player: {Name} | Added {Amount:N0} to target player's BankedPyreals | New balance: {onlinePlayer.BankedPyreals:N0}");
                    }
                    
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Pyreal from {this.Name}", ChatMessageType.System));
                }
                
                this.BankedPyreals -= Amount;
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
                    if (offlinePlayer.BankedLegendaryKeys == null)
                    {
                        offlinePlayer.BankedLegendaryKeys = Amount;
                    }
                    else
                    {
                        offlinePlayer.BankedLegendaryKeys += Amount;
                    }
                }
                else
                {
                    var onlinePlayer = tarplayer as Player;
                    if (onlinePlayer.BankedLegendaryKeys == null)
                    {
                        onlinePlayer.BankedLegendaryKeys = Amount;
                    }
                    else
                    {
                        onlinePlayer.BankedLegendaryKeys += Amount;
                    }
                    //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedLegendaryKeys, onlinePlayer.BankedLegendaryKeys ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Legendary Keys from {this.Name}", ChatMessageType.System));
                }
                this.BankedLegendaryKeys -= Amount;
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLegendaryKeys, this.BankedLegendaryKeys ?? 0));
                return true;
            }
        }

        public bool TransferMythicalKeys(long Amount, string CharacterDestination)
        {
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
                    if (offlinePlayer.BankedMythicalKeys == null)
                    {
                        offlinePlayer.BankedMythicalKeys = Amount;
                    }
                    else
                    {
                        offlinePlayer.BankedMythicalKeys += Amount;
                    }
                }
                else
                {
                    var onlinePlayer = tarplayer as Player;
                    if (onlinePlayer.BankedMythicalKeys == null)
                    {
                        onlinePlayer.BankedMythicalKeys = Amount;
                    }
                    else
                    {
                        onlinePlayer.BankedMythicalKeys += Amount;
                    }
                    //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedMythicalKeys, onlinePlayer.BankedMythicalKeys ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Mythical Keys from {this.Name}", ChatMessageType.System));
                    if (Amount > 1)
                    {
                        onlinePlayer.SavePlayerToDatabase();
                    }
                }
                this.BankedMythicalKeys -= Amount;
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedMythicalKeys, this.BankedMythicalKeys ?? 0));
                if (Amount > 1)
                {
                    this.SavePlayerToDatabase();
                }

                return true;
            }
        }

        public bool TransferLuminance(long Amount, string CharacterDestination)
        {
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
            else
            {
                if (tarplayer is OfflinePlayer)
                {
                    var offlinePlayer = tarplayer as OfflinePlayer;
                    if (offlinePlayer.BankedLuminance == null)
                    {
                        return false;
                    }
                    if (offlinePlayer.BankedLuminance == 0) 
                    {
                        return false;
                    }

                    this.BankedLuminance -= Amount;
                    (offlinePlayer).BankedLuminance += Amount;
                }
                else
                {
                    var onlinePlayer = tarplayer as Player;
                    if (!onlinePlayer.MaximumLuminance.HasValue)
                    {
                        return false;
                    }
                    this.BankedLuminance -= Amount;
                    onlinePlayer.BankedLuminance += Amount;
                    //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedLuminance, onlinePlayer.BankedLuminance ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Luminance from {this.Name}", ChatMessageType.System));
                    if (Amount > 100000)
                    {
                        onlinePlayer.SavePlayerToDatabase();
                    }
                }
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLuminance, this.BankedLuminance ?? 0));
                if (Amount > 100000)
                {
                    this.SavePlayerToDatabase();
                }
                return true;
            }          
        }
        public bool TransferEnlightenedCoins(long Amount, string CharacterDestination)
        {
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
                    if (offlinePlayer.BankedEnlightenedCoins == null)
                    {
                        offlinePlayer.BankedEnlightenedCoins = Amount;
                    }
                    else
                    {
                        offlinePlayer.BankedEnlightenedCoins += Amount;
                    }
                }
                else
                {
                    var onlinePlayer = tarplayer as Player;
                    if (onlinePlayer.BankedEnlightenedCoins == null)
                    {
                        onlinePlayer.BankedEnlightenedCoins = Amount;
                    }
                    else
                    {
                        onlinePlayer.BankedEnlightenedCoins += Amount;
                    }
                    //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedEnlightenedCoins, onlinePlayer.BankedEnlightenedCoins ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Enlightened Coins from {this.Name}", ChatMessageType.System));
                    if (Amount > 10)
                    {
                        onlinePlayer.SavePlayerToDatabase();
                    }

                }
                this.BankedEnlightenedCoins -= Amount;
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedEnlightenedCoins, this.BankedEnlightenedCoins ?? 0));
                if (Amount > 10)
                {
                    this.SavePlayerToDatabase();
                }
                return true;
            }
        }
        public bool TransferWeaklyEnlightenedCoins(long Amount, string CharacterDestination)
        {
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
                    if (offlinePlayer.BankedWeaklyEnlightenedCoins == null)
                    {
                        offlinePlayer.BankedWeaklyEnlightenedCoins = Amount;
                    }
                    else
                    {
                        offlinePlayer.BankedWeaklyEnlightenedCoins += Amount;
                    }
                }
                else
                {
                    var onlinePlayer = tarplayer as Player;
                    if (onlinePlayer.BankedWeaklyEnlightenedCoins == null)
                    {
                        onlinePlayer.BankedWeaklyEnlightenedCoins = Amount;
                    }
                    else
                    {
                        onlinePlayer.BankedWeaklyEnlightenedCoins += Amount;
                    }
                    //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedWeaklyEnlightenedCoins, onlinePlayer.BankedWeaklyEnlightenedCoins ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Weakly Enlightened Coins from {this.Name}", ChatMessageType.System));
                    if (Amount > 10)
                    {
                        onlinePlayer.SavePlayerToDatabase();
                    }

                }
                this.BankedWeaklyEnlightenedCoins -= Amount;
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedWeaklyEnlightenedCoins, this.BankedWeaklyEnlightenedCoins ?? 0));
                if (Amount > 10)
                {
                    this.SavePlayerToDatabase();
                }
            }
            return true;
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
