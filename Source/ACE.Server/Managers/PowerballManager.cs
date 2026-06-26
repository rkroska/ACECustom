using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
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

        private static readonly Random _rng = new();

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
            CalculateNextDrawTime();

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

            var price     = ServerConfig.powerball_ticket_price.Value;
            var totalCost = price * count;
            var banked    = player.BankedLuminance ?? 0;

            if (banked < totalCost)
                return (false,
                    $"[Powerball] Insufficient banked luminance. " +
                    $"You have {FormatLum(banked)} but need {FormatLum(totalCost)} " +
                    $"for {count} ticket{(count == 1 ? "" : "s")} at {FormatLum(price)} each.");

            // Deduct up-front before generating tickets
            player.BankedLuminance = banked - totalCost;
            player.SaveBiotaToDatabase(enqueueSave: true);

            var newTickets = new List<PbTicket>(count);
            int drawId;

            lock (_lock)
            {
                drawId        = _currentDrawId;
                _jackpotPool += totalCost;

                for (int i = 0; i < count; i++)
                {
                    var t = GenerateTicket(player.Guid.Full, player.Name, drawId);
                    _tickets.Add(t);
                    newTickets.Add(t);
                }
            }

            SaveTicketsToDb(newTickets, isTest: false);
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
                int idx  = _rng.Next(pool.Count);
                whites[i] = pool[idx];
                pool.RemoveAt(idx);
            }
            Array.Sort(whites);
            int pb = _rng.Next(1, NumberPool + 1);

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
                int idx   = _rng.Next(pool.Count);
                winNums[i] = pool[idx];
                pool.RemoveAt(idx);
            }
            Array.Sort(winNums);
            int winPb  = _rng.Next(1, NumberPool + 1);
            var winSet = new HashSet<int>(winNums);

            var result = new DrawResult
            {
                DrawId      = drawId,
                W1          = winNums[0],
                W2          = winNums[1],
                W3          = winNums[2],
                Pb          = winPb,
                JackpotPool = jackpotPool,
                TicketCount = drawTickets.Count,
                IsTest      = isTestMode,
            };

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
                var tierPot = (long)(jackpotPool * TierPct[t] / 100.0);

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
                // Award payouts to real players
                foreach (var kvp in result.Winners)
                    AwardPayout(kvp.Key, kvp.Value);

                lock (_lock)
                {
                    _jackpotPool   = rollover;
                    _currentDrawId = drawId + 1;
                    _tickets.Clear();
                }

                SaveHistoryToDb(result);
                SaveStateToDb();
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
                    Send($"  \u2022 {kvp.Value}\u00d7 {kvp.Key}");
                Send($"A {taxPct}% tax was applied to all winnings.");
                Send($"{FormatLum(summary.NetWon)} has been banked in total.");
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Broadcasts
        // ─────────────────────────────────────────────────────────────
        private static void BroadcastDrawResults(DrawResult result)
        {
            BroadcastToAll(
                $"[Lottery] Powerball Draw #{result.DrawId} — Winning numbers: [{result.W1} {result.W2} {result.W3}] PB:{result.Pb}");

            var jackpotWinners = result.Winners.Values.Where(w => w.HasJackpot).ToList();

            if (jackpotWinners.Count > 0)
            {
                foreach (var w in jackpotWinners)
                    BroadcastToAll(
                        $"[Lottery] Grand Jackpot Won! Congratulations to {w.Name} for matching " +
                        $"3 + Powerball and winning {FormatLum(w.GrossWon)} ({FormatLum(w.NetWon)} after taxes)!");
            }
            else
            {
                BroadcastToAll("[Lottery] No jackpot winner this week. The jackpot rolls over!");

                var top = result.Winners.Values
                    .OrderByDescending(w => w.NetWon)
                    .Take(5)
                    .ToList();

                if (top.Count > 0)
                {
                    BroadcastToAll("[Lottery] This week's top winners:");
                    foreach (var w in top)
                        BroadcastToAll($"- {w.Name} ({FormatLum(w.NetWon)})");
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
            sb.AppendLine("[Powerball] \u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550");
            sb.AppendLine($"  Draw #{_currentDrawId} | Next Draw: {GetNextDrawTimeFormatted()}");
            sb.AppendLine($"  Jackpot Pool : {FormatLum(_jackpotPool)}  |  Tickets Sold: {_tickets.Count}");
            sb.AppendLine($"  Ticket Price : {price} banked luminance");
            sb.AppendLine("  \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");
            sb.AppendLine("  Payout Tiers (35% tax applied to all winnings):");
            sb.AppendLine("    3 + Powerball  \u2192 Jackpot (50% of pool)");
            sb.AppendLine("    3 white balls  \u2192 2nd    (15% of pool)");
            sb.AppendLine("    2 + Powerball  \u2192 3rd    (15% of pool)");
            sb.AppendLine("    2 white balls  \u2192 4th    (10% of pool)");
            sb.AppendLine("    1 + Powerball  \u2192 5th    ( 5% of pool)");
            sb.AppendLine("    1 white ball   \u2192 6th    ( 3% of pool)");
            sb.AppendLine("    Powerball only \u2192 7th    ( 2% of pool)");
            sb.AppendLine("  \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");

            if (player != null)
            {
                var mine = GetPlayerTickets(player.Guid.Full);
                sb.AppendLine($"  Your tickets this draw: {mine.Count}");
            }

            sb.AppendLine("  Commands: /pb buy <qty>  |  /pb tickets  |  /pb help");
            sb.Append("[Powerball] \u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550");
            return sb.ToString();
        }

        public static string BuildTicketDisplay(uint charId)
        {
            var tickets = GetPlayerTickets(charId);
            if (tickets.Count == 0)
                return $"[Powerball] You have no tickets for Draw #{_currentDrawId}. Use /pb buy <qty> to buy tickets.";

            var sb = new StringBuilder();
            sb.AppendLine($"[Powerball] Your {tickets.Count} ticket{(tickets.Count == 1 ? "" : "s")} for Draw #{_currentDrawId}:");

            if (tickets.Count < 50)
            {
                // Individual lines
                for (int i = 0; i < tickets.Count; i++)
                    sb.AppendLine($"  #{i + 1,3}: {tickets[i].FormatTicket()}");
            }
            else
            {
                // 5 per line
                for (int i = 0; i < tickets.Count; i += 5)
                {
                    var chunk = tickets.Skip(i).Take(5).Select(t => t.FormatTicket());
                    sb.AppendLine("  " + string.Join("  |  ", chunk));
                }
            }

            return sb.ToString().TrimEnd();
        }

        // ─────────────────────────────────────────────────────────────
        //  Test helpers (dev commands)
        // ─────────────────────────────────────────────────────────────
        public static void AddTestTickets(int count)
        {
            var rng   = new Random();
            var names = new[] { "TestBot1", "TestBot2", "TestBot3", "TestBot4", "TestBot5",
                                "TestBot6", "TestBot7", "TestBot8", "TestBot9", "TestBot10" };

            lock (_lock)
            {
                for (int i = 0; i < count; i++)
                {
                    var pool   = Enumerable.Range(1, NumberPool).ToList();
                    var whites = new int[WhiteBalls];
                    for (int j = 0; j < WhiteBalls; j++)
                    {
                        int idx  = rng.Next(pool.Count);
                        whites[j] = pool[idx];
                        pool.RemoveAt(idx);
                    }
                    Array.Sort(whites);
                    int pb = rng.Next(1, NumberPool + 1);

                    _testTickets.Add(new PbTicket
                    {
                        DrawId   = _currentDrawId,
                        CharId   = (uint)(900000 + (i % 10)),
                        CharName = names[i % names.Length],
                        N1       = whites[0],
                        N2       = whites[1],
                        N3       = whites[2],
                        Pb       = pb,
                        IsTest   = true,
                    });
                }
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
                sb.AppendLine($"[Powerball] {_testTickets.Count} test ticket(s):");
                foreach (var t in _testTickets.Take(20))
                    sb.AppendLine($"  {t.CharName}: {t.FormatTicket()}");
                if (_testTickets.Count > 20)
                    sb.AppendLine($"  ... and {_testTickets.Count - 20} more");
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
            sb.AppendLine($"\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550");
            sb.AppendLine($"{tag}Powerball Draw #{result.DrawId}");
            sb.AppendLine($"  Winning : [{result.W1} {result.W2} {result.W3}] PB:{result.Pb}");
            sb.AppendLine($"  Pool    : {FormatLum(result.JackpotPool)}  |  Tickets: {result.TicketCount}  |  Winners: {result.Winners.Count}");

            if (result.Winners.Count > 0)
            {
                sb.AppendLine("  Winners:");
                foreach (var kvp in result.Winners.OrderByDescending(x => x.Value.NetWon))
                {
                    var w    = kvp.Value;
                    var tags = string.Join(", ", w.TierCounts.Select(t => t.Value > 1 ? $"{t.Value}\u00d7 {t.Key}" : t.Key));
                    sb.AppendLine($"    {w.Name}: {FormatLum(w.NetWon)} net  [{tags}]");
                }
            }
            else
            {
                sb.AppendLine("  No winners this draw.");
            }

            sb.AppendLine($"  Rollover : {FormatLum(result.Rollover)}");
            if (result.IsTest) sb.AppendLine("  [No payouts awarded — test draw]");
            sb.Append("\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550");
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
            if (v >= 1_000_000_000_000L) return $"{v / 1_000_000_000_000.0:F1}T";
            if (v >= 1_000_000_000L)     return $"{v / 1_000_000_000.0:F1}B";
            if (v >= 1_000_000L)         return $"{v / 1_000_000.0:F1}M";
            if (v >= 1_000L)             return $"{v / 1_000.0:F1}k";
            return v.ToString("N0");
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
                    "SELECT `current_draw_id`, `jackpot_pool` FROM `powerball_state` WHERE `id` = 1 LIMIT 1";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    _currentDrawId = reader.GetInt32(0);
                    _jackpotPool   = reader.GetInt64(1);
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

        private static void SaveTicketsToDb(List<PbTicket> tickets, bool isTest)
        {
            if (tickets.Count == 0) return;
            try
            {
                using var ctx = new ShardDbContext();
                var con = ctx.Database.GetDbConnection();
                if (con.State != ConnectionState.Open) con.Open();
                using var cmd = con.CreateCommand();

                foreach (var t in tickets)
                {
                    cmd.CommandText =
                        "INSERT INTO `powerball_tickets` " +
                        "(`draw_id`, `character_id`, `character_name`, `n1`, `n2`, `n3`, `pb`, `is_test`) " +
                        "VALUES (@did, @cid, @cname, @n1, @n2, @n3, @pb, @istest)";
                    cmd.Parameters.Clear();
                    AddParam(cmd, "@did",    t.DrawId);
                    AddParam(cmd, "@cid",    (long)t.CharId);
                    AddParam(cmd, "@cname",  t.CharName);
                    AddParam(cmd, "@n1",     t.N1);
                    AddParam(cmd, "@n2",     t.N2);
                    AddParam(cmd, "@n3",     t.N3);
                    AddParam(cmd, "@pb",     t.Pb);
                    AddParam(cmd, "@istest", isTest ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                log.Error("[Powerball] SaveTicketsToDb failed", ex);
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
