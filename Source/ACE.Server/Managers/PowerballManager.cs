using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using log4net;

using Microsoft.EntityFrameworkCore;

using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.Managers
{
    public static class PowerballManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        // ─────────────────────────────────────────────────────────────
        //  Constants
        // ─────────────────────────────────────────────────────────────
        public  const  double SinkTaxPct   = 35.0;   // % of gross payout sunk from economy
        private const  int    NumberPool   = 20;      // balls numbered 1..20
        private const  int    WhiteBalls   = 3;       // how many white balls drawn

        // Payout tier definitions: (white balls matched, powerball matched, label)
        private static readonly (int whites, bool pb, string label)[] TierDef =
        {
            (3, true,  "3 + Powerball"),   // Tier 1 – jackpot
            (3, false, "3 white balls"),   // Tier 2
            (2, true,  "2 + Powerball"),   // Tier 3
            (2, false, "2 white balls"),   // Tier 4
            (1, true,  "1 + Powerball"),   // Tier 5
            (1, false, "1 white ball"),    // Tier 6
            (0, true,  "Powerball only"),  // Tier 7
        };

        // Pool % each tier claims from the jackpot pool (must sum to 100)
        private static readonly double[] TierPct = { 50, 15, 15, 10, 5, 3, 2 };

        // ─────────────────────────────────────────────────────────────
        //  In-memory state
        // ─────────────────────────────────────────────────────────────
        private static bool     _initialized;
        private static int      _currentDrawId;
        private static long     _jackpotPool;          // accumulated from ticket sales
        private static DateTime _nextDrawTime = DateTime.MinValue;
        private static DateTime _lastTick     = DateTime.MinValue;

        private static readonly List<PbTicket> _tickets     = new();
        private static readonly List<PbTicket> _testTickets = new();
        private static readonly object         _lock        = new();

        // ─────────────────────────────────────────────────────────────
        //  Data types
        // ─────────────────────────────────────────────────────────────
        public sealed class PbTicket
        {
            public long   Id       { get; set; }
            public int    DrawId   { get; set; }
            public uint   CharId   { get; set; }
            public string CharName { get; set; } = "";
            public int    N1       { get; set; }   // white ball 1 (sorted ascending)
            public int    N2       { get; set; }   // white ball 2
            public int    N3       { get; set; }   // white ball 3
            public int    Pb       { get; set; }   // powerball
            public bool   IsTest   { get; set; }

            public HashSet<int> WhiteSet => new() { N1, N2, N3 };

            /// <summary>Compact display: [ 4  9 17] PB:12</summary>
            public string FormatTicket() => $"[{N1,2} {N2,2} {N3,2}] PB:{Pb,2}";
        }

        public sealed class WinnerSummary
        {
            public string Name     { get; set; } = "";
            public long   GrossWon { get; set; }
            public long   NetWon   { get; set; }
            /// <summary>Tier label → count of winning tickets in that tier.</summary>
            public Dictionary<string, int> TierCounts { get; } = new();
            public bool HasJackpot => TierCounts.ContainsKey("3 + Powerball");
        }

        public sealed class DrawResult
        {
            public int  DrawId      { get; set; }
            public int  W1          { get; set; }
            public int  W2          { get; set; }
            public int  W3          { get; set; }
            public int  Pb          { get; set; }
            public long JackpotPool { get; set; }   // pool before draw
            public long Rollover    { get; set; }   // pool after draw (unclaimed tiers)
            public int  TicketCount { get; set; }
            public bool IsTest      { get; set; }
            public Dictionary<uint, WinnerSummary> Winners { get; } = new();
            public Dictionary<uint, (string Name, int TicketCount)> PlayerTicketStats { get; } = new();
        }

        // ─────────────────────────────────────────────────────────────
        //  Initialize
        // ─────────────────────────────────────────────────────────────
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            EnsureTablesExist();
            LoadStateFromDb();
            LoadTicketsFromDb();

            // Settle any draw that was due while the server was offline
            if (_nextDrawTime != DateTime.MinValue && DateTime.UtcNow >= _nextDrawTime
                && ServerConfig.powerball_enabled.Value)
            {
                log.Info("[Powerball] Overdue draw detected on startup \u2014 settling now.");
                var result = ExecuteDraw(isTestMode: false);
                if (result != null)
                    BroadcastDrawResults(result);
            }

            CalculateNextDrawTime();
            SaveStateToDb();   // persist updated next_draw_time

            log.Info($"[Powerball] Initialized. DrawId={_currentDrawId}, " +
                     $"Pool={FormatLum(_jackpotPool)}, Tickets={_tickets.Count}, " +
                     $"NextDraw={_nextDrawTime:yyyy-MM-dd HH:mm} UTC");
        }

        // ─────────────────────────────────────────────────────────────
        //  Tick — rate-limited to once per 60 s (called from WorldManager)
        // ─────────────────────────────────────────────────────────────
        public static void Tick()
        {
            if (!_initialized) return;

            var now = DateTime.UtcNow;
            if ((now - _lastTick).TotalSeconds < 60) return;
            _lastTick = now;

            if (!ServerConfig.powerball_enabled.Value) return;

            if (_nextDrawTime != DateTime.MinValue && now >= _nextDrawTime)
            {
                var result = ExecuteDraw(isTestMode: false);
                CalculateNextDrawTime();
                SaveStateToDb();   // persist updated next_draw_time

                if (result != null)
                    BroadcastDrawResults(result);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Buy tickets
        // ─────────────────────────────────────────────────────────────
        public static (bool ok, string msg) BuyTickets(WorldObjects.Player player, int count)
        {
            if (!ServerConfig.powerball_enabled.Value)
                return (false, "[Powerball] The Powerball lottery is not currently active.");

            if (count < 1 || count > 500)
                return (false, "[Powerball] You may buy between 1 and 500 tickets per purchase.");

            var price = ServerConfig.powerball_ticket_price.Value;
            if (price <= 0)
                return (false, "[Powerball] Ticket price is misconfigured.");

            long totalCost;
            try   { totalCost = checked(price * count); }
            catch (OverflowException) { return (false, "[Powerball] Ticket price is misconfigured."); }

            var banked = player.BankedLuminance ?? 0;
            if (banked < totalCost)
                return (false,
                    $"[Powerball] Insufficient banked luminance. " +
                    $"You have {FormatLum(banked)} but need {FormatLum(totalCost)} " +
                    $"for {count} ticket{(count == 1 ? "" : "s")} at {FormatLum(price)} each.");

            // Generate tickets in memory first
            var newTickets = new List<PbTicket>(count);
            int drawId;

            lock (_lock)
            {
                drawId = _currentDrawId;
                for (int i = 0; i < count; i++)
                {
                    var t = GenerateTicket(player.Guid.Full, player.Name, drawId);
                    _tickets.Add(t);
                    newTickets.Add(t);
                }
            }

            // Persist tickets before touching player balance — roll back on failure
            bool saved = SaveTicketsToDb(newTickets, isTest: false);
            if (!saved)
            {
                lock (_lock)
                {
                    foreach (var t in newTickets)
                        _tickets.Remove(t);
                }
                return (false, "[Powerball] A database error occurred. Please try again.");
            }

            // Tickets safely persisted — now commit the deduction
            player.BankedLuminance = banked - totalCost;
            player.SaveBiotaToDatabase(enqueueSave: true);

            lock (_lock) { _jackpotPool += totalCost; }
            SaveStateToDb();

            return (true, "");
        }

        private static PbTicket GenerateTicket(uint charId, string charName, int drawId)
        {
            // Draw 3 unique white balls (sorted), then 1 powerball from 1..NumberPool
            var pool   = Enumerable.Range(1, NumberPool).ToList();
            var whites = new int[WhiteBalls];
            for (int i = 0; i < WhiteBalls; i++)
            {
                int idx  = RandomNumberGenerator.GetInt32(pool.Count);
                whites[i] = pool[idx];
                pool.RemoveAt(idx);
            }
            Array.Sort(whites);
            int pb = RandomNumberGenerator.GetInt32(1, NumberPool + 1);

            return new PbTicket
            {
                DrawId   = drawId,
                CharId   = charId,
                CharName = charName,
                N1       = whites[0],
                N2       = whites[1],
                N3       = whites[2],
                Pb       = pb,
                IsTest   = false,
            };
        }

        // ─────────────────────────────────────────────────────────────
        //  Execute draw
        // ─────────────────────────────────────────────────────────────
        public static DrawResult? ExecuteDraw(bool isTestMode)
        {
            List<PbTicket> drawTickets;
            long jackpotPool;
            int  drawId;

            lock (_lock)
            {
                drawTickets = isTestMode
                    ? _tickets.Concat(_testTickets).ToList()
                    : new List<PbTicket>(_tickets);
                jackpotPool = _jackpotPool;
                drawId      = _currentDrawId;
            }

            if (drawTickets.Count == 0)
            {
                log.Info("[Powerball] Draw skipped — no tickets in pool.");
                if (!isTestMode)
                {
                    lock (_lock) { _currentDrawId++; }
                    SaveStateToDb();
                }
                return null;
            }

            // Draw winning numbers
            var pool    = Enumerable.Range(1, NumberPool).ToList();
            var winNums = new int[WhiteBalls];
            for (int i = 0; i < WhiteBalls; i++)
            {
                int idx   = RandomNumberGenerator.GetInt32(pool.Count);
                winNums[i] = pool[idx];
                pool.RemoveAt(idx);
            }
            Array.Sort(winNums);
            int winPb  = RandomNumberGenerator.GetInt32(1, NumberPool + 1);
            var winSet = new HashSet<int>(winNums);

            var price = ServerConfig.powerball_ticket_price.Value;
            var effectivePool = isTestMode ? (jackpotPool + _testTickets.Count * price) : jackpotPool;

            var result = new DrawResult
            {
                DrawId      = drawId,
                W1          = winNums[0],
                W2          = winNums[1],
                W3          = winNums[2],
                Pb          = winPb,
                JackpotPool = effectivePool,
                TicketCount = drawTickets.Count,
                IsTest      = isTestMode,
            };

            foreach (var ticket in drawTickets)
            {
                if (!result.PlayerTicketStats.TryGetValue(ticket.CharId, out var stats))
                    result.PlayerTicketStats[ticket.CharId] = (ticket.CharName, 1);
                else
                    result.PlayerTicketStats[ticket.CharId] = (stats.Name, stats.TicketCount + 1);
            }

            // Bucket each ticket into the highest tier it matches
            var tierTickets = new List<List<PbTicket>>(TierDef.Length);
            for (int t = 0; t < TierDef.Length; t++) tierTickets.Add(new());

            foreach (var ticket in drawTickets)
            {
                var matched = ticket.WhiteSet.Intersect(winSet).Count();
                var pbMatch = ticket.Pb == winPb;

                for (int t = 0; t < TierDef.Length; t++)
                {
                    if (matched == TierDef[t].whites && pbMatch == TierDef[t].pb)
                    {
                        tierTickets[t].Add(ticket);
                        break;
                    }
                }
            }

            // Calculate payouts and accumulate winner summaries
            long rollover = 0;
            for (int t = 0; t < TierDef.Length; t++)
            {
                var tier    = tierTickets[t];
                var tierPot = (long)(effectivePool * TierPct[t] / 100.0);

                if (tier.Count == 0)
                {
                    rollover += tierPot;
                    continue;
                }

                // Divide tier pot equally among winning tickets (not unique players)
                var grossPerTicket = tierPot / tier.Count;
                var netPerTicket   = (long)(grossPerTicket * (1.0 - SinkTaxPct / 100.0));
                var label          = TierDef[t].label;

                foreach (var ticket in tier)
                {
                    if (!result.Winners.TryGetValue(ticket.CharId, out var summary))
                    {
                        summary = new WinnerSummary { Name = ticket.CharName };
                        result.Winners[ticket.CharId] = summary;
                    }
                    summary.GrossWon += grossPerTicket;
                    summary.NetWon   += netPerTicket;
                    summary.TierCounts.TryGetValue(label, out var cnt);
                    summary.TierCounts[label] = cnt + 1;
                }
            }

            result.Rollover = rollover;

            if (!isTestMode)
            {
                // 1. Advance state and persist FIRST — prevents draw replay on crash
                lock (_lock)
                {
                    _jackpotPool   = rollover;
                    _currentDrawId = drawId + 1;
                    _tickets.Clear();
                }
                SaveStateToDb();

                // 2. Award payouts to real players
                foreach (var kvp in result.Winners)
                    AwardPayout(kvp.Key, kvp.Value);

                // 3. Record history and clean up tickets
                SaveHistoryToDb(result);
                ClearTicketsFromDb(drawId);
            }
            else
            {
                // Clear test tickets but leave real pool alone
                lock (_lock) { _testTickets.Clear(); }
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────
        //  Payout helpers
        // ─────────────────────────────────────────────────────────────
        private static void AwardPayout(uint charId, WinnerSummary summary)
        {
            if (summary.NetWon <= 0) return;

            var online = PlayerManager.GetOnlinePlayer(charId);
            if (online != null)
            {
                online.BankedLuminance = (online.BankedLuminance ?? 0) + summary.NetWon;
                online.SaveBiotaToDatabase(enqueueSave: true);
                SendWinNotification(online.Session, summary);
            }
            else
            {
                var offline = PlayerManager.GetOfflinePlayer(charId);
                if (offline != null)
                {
                    offline.BankedLuminance = (offline.BankedLuminance ?? 0) + summary.NetWon;
                    offline.SaveBiotaToDatabase(enqueueSave: true);
                }
                else
                {
                    log.Warn($"[Powerball] Could not find player '{summary.Name}' (ID {charId}) to award {FormatLum(summary.NetWon)}.");
                }
            }
        }

        private static void SendWinNotification(Network.Session? session, WinnerSummary summary)
        {
            if (session == null) return;

            var taxPct = (int)SinkTaxPct;
            void Send(string msg) =>
                session.Network.EnqueueSend(
                    new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

            if (summary.TierCounts.Count == 1)
            {
                // Single-tier win
                var (label, count) = summary.TierCounts.First();

                if (count == 1)
                {
                    // Exactly one winning ticket — canonical 4-line format
                    Send($"Congratulations! Your ticket matched {label}.");
                    Send(summary.HasJackpot
                        ? $"You won the jackpot of {FormatLum(summary.GrossWon)}!"
                        : $"You won {FormatLum(summary.GrossWon)}!");
                    Send($"A {taxPct}% tax was applied.");
                    Send($"{FormatLum(summary.NetWon)} has been banked.");
                }
                else
                {
                    // Multiple tickets all in the same tier
                    Send($"Congratulations! {count} of your tickets matched {label}.");
                    Send(summary.HasJackpot
                        ? $"You won the jackpot of {FormatLum(summary.GrossWon)} combined!"
                        : $"You won {FormatLum(summary.GrossWon)} combined!");
                    Send($"A {taxPct}% tax was applied.");
                    Send($"{FormatLum(summary.NetWon)} has been banked.");
                }
            }
            else
            {
                // Multiple tiers — bulleted summary
                var totalTickets = summary.TierCounts.Values.Sum();
                Send($"Congratulations! Your tickets won {totalTickets} prize{(totalTickets == 1 ? "" : "s")} this draw!");
                foreach (var kvp in summary.TierCounts.OrderBy(x => Array.FindIndex(TierDef, d => d.label == x.Key)))
                    Send($"  - {kvp.Value}x {kvp.Key}");
                Send($"A {taxPct}% tax was applied to all winnings.");
                Send($"{FormatLum(summary.NetWon)} has been banked in total.");
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Broadcasts
        // ─────────────────────────────────────────────────────────────
        private static void BroadcastDrawResults(DrawResult result)
        {
            BroadcastToAll($"[Lottery Announcer] The weekly Powerball Draw #{result.DrawId} is complete!");
            BroadcastToAll($"[Lottery Announcer] Tonights winning numbers: [{result.W1:D2}]-[{result.W2:D2}]-[{result.W3:D2}]  [PB: {result.Pb:D2}]");

            var jackpotWinners = result.Winners.Values.Where(w => w.HasJackpot).ToList();

            if (jackpotWinners.Count > 0)
            {
                foreach (var w in jackpotWinners)
                    BroadcastToAll(
                        $"[Lottery Announcer] GRAND JACKPOT WINNER! Congratulations to {w.Name} for matching " +
                        $"all 3 balls plus the Powerball and winning {FormatLum(w.NetWon)}!");
            }
            else
            {
                BroadcastToAll("[Lottery Announcer] No one matched all 3 balls plus the Powerball! The jackpot rolls over to the next draw!");

                var top = result.Winners.Values
                    .OrderByDescending(w => w.NetWon)
                    .Take(5)
                    .ToList();

                if (top.Count > 0)
                {
                    BroadcastToAll("[Lottery Announcer] Congratulations to this week's top winners:");
                    foreach (var w in top)
                        BroadcastToAll($"  - {w.Name} won {FormatLum(w.NetWon)}!");
                }
            }
        }

        private static void BroadcastToAll(string message)
        {
            foreach (var p in PlayerManager.GetAllOnline())
                p.Session?.Network.EnqueueSend(
                    new GameMessageSystemChat(message, ChatMessageType.Broadcast));
        }

        // ─────────────────────────────────────────────────────────────
        //  Queries
        // ─────────────────────────────────────────────────────────────
        public static long GetJackpotPool()     => _jackpotPool;
        public static int  GetCurrentDrawId()   => _currentDrawId;
        public static int  GetTicketCount()     => _tickets.Count;
        public static int  GetTestTicketCount() => _testTickets.Count;

        public static List<PbTicket> GetPlayerTickets(uint charId)
        {
            lock (_lock)
                return _tickets.Where(t => t.CharId == charId).ToList();
        }

        public static string GetNextDrawTimeFormatted()
        {
            if (_nextDrawTime == DateTime.MinValue) return "Unknown";
            var remaining = _nextDrawTime - DateTime.UtcNow;
            if (remaining.TotalSeconds <= 0) return "Imminent";
            if (remaining.TotalDays >= 1)   return $"{(int)remaining.TotalDays}d {remaining.Hours}h";
            if (remaining.TotalHours >= 1)  return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
            return $"{(int)remaining.TotalMinutes}m";
        }

        // ─────────────────────────────────────────────────────────────
        //  Info / display strings
        // ─────────────────────────────────────────────────────────────
        public static string GetInfoBlock(WorldObjects.Player? player)
        {
            var price = FormatLum(ServerConfig.powerball_ticket_price.Value);
            var sb    = new StringBuilder();
            sb.Append("[Powerball] ======================================\n");
            sb.Append($"  Draw #{_currentDrawId} | Next Draw: {GetNextDrawTimeFormatted()}\n");
            sb.Append($"  Jackpot Pool : {FormatLum(_jackpotPool)}  |  Tickets Sold: {_tickets.Count}\n");
            sb.Append($"  Ticket Price : {price} banked luminance\n");
            sb.Append("  ------------------------------------------\n");
            sb.Append("  Payout Tiers (35% tax applied to all winnings):\n");
            sb.Append("    3 + Powerball  -> Jackpot (50% of pool)\n");
            sb.Append("    3 white balls  -> 2nd    (15% of pool)\n");
            sb.Append("    2 + Powerball  -> 3rd    (15% of pool)\n");
            sb.Append("    2 white balls  -> 4th    (10% of pool)\n");
            sb.Append("    1 + Powerball  -> 5th    ( 5% of pool)\n");
            sb.Append("    1 white ball   -> 6th    ( 3% of pool)\n");
            sb.Append("    Powerball only -> 7th    ( 2% of pool)\n");
            sb.Append("  ------------------------------------------\n");

            if (player != null)
            {
                var mine = GetPlayerTickets(player.Guid.Full);
                sb.Append($"  Your tickets this draw: {mine.Count}\n");
            }

            sb.Append("  Commands: /pb buy <qty>  |  /pb tickets  |  /pb help\n");
            sb.Append("[Powerball] ======================================");
            return sb.ToString();
        }

        public static string BuildTicketDisplay(uint charId)
        {
            var tickets = GetPlayerTickets(charId);
            if (tickets.Count == 0)
                return $"[Powerball] You have no tickets for Draw #{_currentDrawId}. Use /pb buy <qty> to buy tickets.";

            var sb = new StringBuilder();
            sb.Append($"[Powerball] Your {tickets.Count} ticket{(tickets.Count == 1 ? "" : "s")} for Draw #{_currentDrawId}:\n");

            if (tickets.Count < 50)
            {
                // Individual lines
                for (int i = 0; i < tickets.Count; i++)
                    sb.Append($"  #{i + 1,3}: {tickets[i].FormatTicket()}\n");
            }
            else
            {
                // 5 per line
                for (int i = 0; i < tickets.Count; i += 5)
                {
                    var chunk = tickets.Skip(i).Take(5).Select(t => t.FormatTicket());
                    sb.Append("  " + string.Join("  |  ", chunk) + "\n");
                }
            }

            return sb.ToString().TrimEnd();
        }

        // ─────────────────────────────────────────────────────────────
        //  Test helpers (dev commands)
        // ─────────────────────────────────────────────────────────────
        public static void AddTestTickets(int count)
        {
            lock (_lock)
            {
                // Simulate 150 unique players
                // 15% Whales (22 players): 500-1500 tickets
                // 50% Middle (75 players): 50-150 tickets
                // 35% Low (53 players): 1-20 tickets
                int whaleCount = 22;
                int midCount   = 75;
                int lowCount   = 53;

                int playerId = 1;

                // 1. Whales
                for (int p = 0; p < whaleCount; p++)
                {
                    int ticketsToBuy = RandomNumberGenerator.GetInt32(500, 1501);
                    GenerateTicketsForPlayer($"TestWhale{playerId}", (uint)(900000 + playerId), ticketsToBuy);
                    playerId++;
                }

                // 2. Middle-tier
                for (int p = 0; p < midCount; p++)
                {
                    int ticketsToBuy = RandomNumberGenerator.GetInt32(50, 151);
                    GenerateTicketsForPlayer($"TestPlayer{playerId}", (uint)(900000 + playerId), ticketsToBuy);
                    playerId++;
                }

                // 3. Low-tier
                for (int p = 0; p < lowCount; p++)
                {
                    int ticketsToBuy = RandomNumberGenerator.GetInt32(1, 21);
                    GenerateTicketsForPlayer($"TestMinnow{playerId}", (uint)(900000 + playerId), ticketsToBuy);
                    playerId++;
                }
            }
        }

        private static void GenerateTicketsForPlayer(string name, uint charId, int ticketCount)
        {
            for (int i = 0; i < ticketCount; i++)
            {
                var pool   = Enumerable.Range(1, NumberPool).ToList();
                var whites = new int[WhiteBalls];
                for (int j = 0; j < WhiteBalls; j++)
                {
                    int idx  = RandomNumberGenerator.GetInt32(pool.Count);
                    whites[j] = pool[idx];
                    pool.RemoveAt(idx);
                }
                Array.Sort(whites);
                int pb = RandomNumberGenerator.GetInt32(1, NumberPool + 1);

                _testTickets.Add(new PbTicket
                {
                    DrawId   = _currentDrawId,
                    CharId   = charId,
                    CharName = name,
                    N1       = whites[0],
                    N2       = whites[1],
                    N3       = whites[2],
                    Pb       = pb,
                    IsTest   = true,
                });
            }
        }

        public static void ClearTestTickets()
        {
            lock (_lock) { _testTickets.Clear(); }
        }

        public static string BuildTestTicketList()
        {
            lock (_lock)
            {
                if (_testTickets.Count == 0)
                    return "[Powerball] No test tickets in pool.";

                var sb = new StringBuilder();
                sb.Append($"[Powerball] {_testTickets.Count} test ticket(s):\n");
                foreach (var t in _testTickets.Take(20))
                    sb.Append($"  {t.CharName}: {t.FormatTicket()}\n");
                if (_testTickets.Count > 20)
                    sb.Append($"  ... and {_testTickets.Count - 20} more\n");
                return sb.ToString().TrimEnd();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Result display (used by dev commands)
        // ─────────────────────────────────────────────────────────────
        public static string BuildDrawResultText(DrawResult result)
        {
            var sb = new StringBuilder();
            var tag = result.IsTest ? "[TEST DRAW] " : "";
            sb.Append("==========================================\n");
            sb.Append($"{tag}Powerball Draw #{result.DrawId}\n");
            sb.Append($"  [Lottery Announcer] Tonights winning numbers: [{result.W1:D2}]-[{result.W2:D2}]-[{result.W3:D2}]  [PB: {result.Pb:D2}]\n");
            sb.Append("  ------------------------------------------\n");
            sb.Append($"  Pool    : {FormatLum(result.JackpotPool)}  |  Tickets: {result.TicketCount}  |  Winners: {result.Winners.Count}\n");

            var price = ServerConfig.powerball_ticket_price.Value;
            var playerStats = new List<(string Name, long Spent, long NetWon, long Profit)>();

            foreach (var kvp in result.PlayerTicketStats)
            {
                var charId = kvp.Key;
                var name = kvp.Value.Name;
                var spent = kvp.Value.TicketCount * price;
                long netWon = 0;
                if (result.Winners.TryGetValue(charId, out var summary))
                {
                    netWon = summary.NetWon;
                }
                playerStats.Add((name, spent, netWon, netWon - spent));
            }

            var winnersList = playerStats.Where(x => x.NetWon > 0).OrderByDescending(x => x.NetWon).Take(10).ToList();

            if (winnersList.Count > 0)
            {
                sb.Append("  Top Winners:\n");
                foreach (var stat in winnersList)
                {
                    sb.Append($"    {stat.Name}: Won {FormatLum(stat.NetWon)}\n");
                }
            }
            else
            {
                sb.Append("  Top Winners:\n    None\n");
            }

            sb.Append($"  Rollover : {FormatLum(result.Rollover)}\n");
            if (result.IsTest) sb.Append("  [No payouts awarded - test draw]\n");
            sb.Append("==========================================");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────
        private static void CalculateNextDrawTime()
        {
            var drawDay  = (int)Math.Clamp(ServerConfig.powerball_draw_day_of_week.Value, 0, 6);
            var drawHour = (int)Math.Clamp(ServerConfig.powerball_draw_hour_utc.Value, 0, 23);
            var now      = DateTime.UtcNow;
            var candidate = new DateTime(now.Year, now.Month, now.Day, drawHour, 0, 0, DateTimeKind.Utc);
            while ((int)candidate.DayOfWeek != drawDay || candidate <= now)
                candidate = candidate.AddDays(1);
            _nextDrawTime = candidate;
        }

        public static string FormatLum(long v)
        {
            var isNegative = v < 0;
            var absV = Math.Abs(v);
            string formatted;

            if (absV >= 1_000_000_000_000L) formatted = $"{absV / 1_000_000_000_000.0:F1}T";
            else if (absV >= 1_000_000_000L)     formatted = $"{absV / 1_000_000_000.0:F1}B";
            else if (absV >= 1_000_000L)         formatted = $"{absV / 1_000_000.0:F1}M";
            else if (absV >= 1_000L)             formatted = $"{absV / 1_000.0:F1}k";
            else                                 formatted = absV.ToString("N0");

            return isNegative ? $"-{formatted}" : formatted;
        }

        // ─────────────────────────────────────────────────────────────
        //  DB helpers
        // ─────────────────────────────────────────────────────────────
        private static void AddParam(System.Data.Common.DbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private static void EnsureTablesExist()
        {
            try
            {
                using var ctx = new ShardDbContext();
                var con = ctx.Database.GetDbConnection();
                if (con.State != ConnectionState.Open) con.Open();
                using var cmd = con.CreateCommand();

                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS `powerball_state` (
  `id`              int     NOT NULL DEFAULT 1,
  `current_draw_id` int     NOT NULL DEFAULT 1,
  `jackpot_pool`    bigint  NOT NULL DEFAULT 0,
  `next_draw_time`  datetime DEFAULT NULL,
  `last_draw_time`  datetime DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS `powerball_tickets` (
  `id`             bigint      NOT NULL AUTO_INCREMENT,
  `draw_id`        int         NOT NULL,
  `character_id`   bigint      NOT NULL,
  `character_name` varchar(64) NOT NULL,
  `n1` tinyint NOT NULL, `n2` tinyint NOT NULL, `n3` tinyint NOT NULL, `pb` tinyint NOT NULL,
  `is_test`    tinyint(1) NOT NULL DEFAULT 0,
  `created_at` datetime   NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `IX_pb_tickets_draw_char` (`draw_id`, `character_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS `powerball_history` (
  `id`           bigint    NOT NULL AUTO_INCREMENT,
  `draw_id`      int       NOT NULL,
  `drawn_n1`     tinyint   NOT NULL, `drawn_n2` tinyint NOT NULL,
  `drawn_n3`     tinyint   NOT NULL, `drawn_pb` tinyint NOT NULL,
  `jackpot_pool` bigint    NOT NULL,
  `ticket_count` int       NOT NULL DEFAULT 0,
  `rollover`     bigint    NOT NULL DEFAULT 0,
  `winner_count` int       NOT NULL DEFAULT 0,
  `jackpot_won`  tinyint(1) NOT NULL DEFAULT 0,
  `draw_time`    datetime  NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `UQ_pb_history_draw_id` (`draw_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
                cmd.ExecuteNonQuery();

                // Seed state row (INSERT IGNORE is idempotent)
                cmd.CommandText =
                    "INSERT IGNORE INTO `powerball_state` (`id`, `current_draw_id`, `jackpot_pool`) VALUES (1, 1, 0)";
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                log.Error("[Powerball] EnsureTablesExist failed", ex);
            }
        }

        private static void LoadStateFromDb()
        {
            try
            {
                using var ctx = new ShardDbContext();
                var con = ctx.Database.GetDbConnection();
                if (con.State != ConnectionState.Open) con.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText =
                    "SELECT `current_draw_id`, `jackpot_pool`, `next_draw_time` " +
                    "FROM `powerball_state` WHERE `id` = 1 LIMIT 1";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    _currentDrawId = reader.GetInt32(0);
                    _jackpotPool   = reader.GetInt64(1);
                    if (!reader.IsDBNull(2))
                        _nextDrawTime = DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc);
                }
            }
            catch (Exception ex)
            {
                log.Error("[Powerball] LoadStateFromDb failed", ex);
            }
        }

        private static void SaveStateToDb()
        {
            try
            {
                using var ctx = new ShardDbContext();
                var con = ctx.Database.GetDbConnection();
                if (con.State != ConnectionState.Open) con.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText =
                    "UPDATE `powerball_state` SET `current_draw_id` = @did, `jackpot_pool` = @jp, " +
                    "`next_draw_time` = @ndt, `last_draw_time` = NOW() WHERE `id` = 1";
                AddParam(cmd, "@did", _currentDrawId);
                AddParam(cmd, "@jp",  _jackpotPool);
                AddParam(cmd, "@ndt",
                    _nextDrawTime == DateTime.MinValue ? (object)DBNull.Value : _nextDrawTime);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                log.Error("[Powerball] SaveStateToDb failed", ex);
            }
        }

        private static void LoadTicketsFromDb()
        {
            try
            {
                using var ctx = new ShardDbContext();
                var con = ctx.Database.GetDbConnection();
                if (con.State != ConnectionState.Open) con.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText =
                    "SELECT `id`, `character_id`, `character_name`, `n1`, `n2`, `n3`, `pb`, `is_test` " +
                    "FROM `powerball_tickets` WHERE `draw_id` = @did";
                AddParam(cmd, "@did", _currentDrawId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var t = new PbTicket
                    {
                        Id       = reader.GetInt64(0),
                        DrawId   = _currentDrawId,
                        CharId   = (uint)reader.GetInt64(1),
                        CharName = reader.GetString(2),
                        N1       = reader.GetInt32(3),
                        N2       = reader.GetInt32(4),
                        N3       = reader.GetInt32(5),
                        Pb       = reader.GetInt32(6),
                        IsTest   = reader.GetBoolean(7),
                    };
                    if (t.IsTest) _testTickets.Add(t);
                    else          _tickets.Add(t);
                }
            }
            catch (Exception ex)
            {
                log.Error("[Powerball] LoadTicketsFromDb failed", ex);
            }
        }

        private static bool SaveTicketsToDb(List<PbTicket> tickets, bool isTest)
        {
            if (tickets.Count == 0) return true;
            try
            {
                using var ctx = new ShardDbContext();
                var con = ctx.Database.GetDbConnection();
                if (con.State != ConnectionState.Open) con.Open();
                using var cmd = con.CreateCommand();
                using var txn = con.BeginTransaction();
                cmd.Transaction = txn;

                cmd.CommandText =
                    "INSERT INTO `powerball_tickets` " +
                    "(`draw_id`, `character_id`, `character_name`, `n1`, `n2`, `n3`, `pb`, `is_test`) " +
                    "VALUES (@did, @cid, @cname, @n1, @n2, @n3, @pb, @istest)";

                var pDid = cmd.CreateParameter(); pDid.ParameterName = "@did"; cmd.Parameters.Add(pDid);
                var pCid = cmd.CreateParameter(); pCid.ParameterName = "@cid"; cmd.Parameters.Add(pCid);
                var pCname = cmd.CreateParameter(); pCname.ParameterName = "@cname"; cmd.Parameters.Add(pCname);
                var pN1 = cmd.CreateParameter(); pN1.ParameterName = "@n1"; cmd.Parameters.Add(pN1);
                var pN2 = cmd.CreateParameter(); pN2.ParameterName = "@n2"; cmd.Parameters.Add(pN2);
                var pN3 = cmd.CreateParameter(); pN3.ParameterName = "@n3"; cmd.Parameters.Add(pN3);
                var pPb = cmd.CreateParameter(); pPb.ParameterName = "@pb"; cmd.Parameters.Add(pPb);
                var pIsTest = cmd.CreateParameter(); pIsTest.ParameterName = "@istest"; cmd.Parameters.Add(pIsTest);

                try
                {
                    foreach (var t in tickets)
                    {
                        pDid.Value = t.DrawId;
                        pCid.Value = (long)t.CharId;
                        pCname.Value = t.CharName ?? (object)DBNull.Value;
                        pN1.Value = t.N1;
                        pN2.Value = t.N2;
                        pN3.Value = t.N3;
                        pPb.Value = t.Pb;
                        pIsTest.Value = isTest ? 1 : 0;

                        cmd.ExecuteNonQuery();
                    }
                    txn.Commit();
                }
                catch
                {
                    txn.Rollback();
                    throw;
                }
                return true;
            }
            catch (Exception ex)
            {
                log.Error("[Powerball] SaveTicketsToDb failed", ex);
                return false;
            }
        }

        private static void ClearTicketsFromDb(int drawId)
        {
            try
            {
                using var ctx = new ShardDbContext();
                var con = ctx.Database.GetDbConnection();
                if (con.State != ConnectionState.Open) con.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "DELETE FROM `powerball_tickets` WHERE `draw_id` = @did";
                AddParam(cmd, "@did", drawId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                log.Error("[Powerball] ClearTicketsFromDb failed", ex);
            }
        }

        private static void SaveHistoryToDb(DrawResult result)
        {
            try
            {
                using var ctx = new ShardDbContext();
                var con = ctx.Database.GetDbConnection();
                if (con.State != ConnectionState.Open) con.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText =
                    "INSERT IGNORE INTO `powerball_history` " +
                    "(`draw_id`, `drawn_n1`, `drawn_n2`, `drawn_n3`, `drawn_pb`, " +
                    "`jackpot_pool`, `ticket_count`, `rollover`, `winner_count`, `jackpot_won`) " +
                    "VALUES (@did, @n1, @n2, @n3, @pb, @jp, @tc, @ro, @wc, @jw)";
                AddParam(cmd, "@did", result.DrawId);
                AddParam(cmd, "@n1",  result.W1);
                AddParam(cmd, "@n2",  result.W2);
                AddParam(cmd, "@n3",  result.W3);
                AddParam(cmd, "@pb",  result.Pb);
                AddParam(cmd, "@jp",  result.JackpotPool);
                AddParam(cmd, "@tc",  result.TicketCount);
                AddParam(cmd, "@ro",  result.Rollover);
                AddParam(cmd, "@wc",  result.Winners.Count);
                AddParam(cmd, "@jw",  result.Winners.Values.Any(w => w.HasJackpot) ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                log.Error("[Powerball] SaveHistoryToDb failed", ex);
            }
        }
    }
}
