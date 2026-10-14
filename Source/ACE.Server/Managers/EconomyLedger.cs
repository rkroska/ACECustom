using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;

using log4net;

using Microsoft.EntityFrameworkCore;

using ACE.Database.Models.Shard;
using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Economy ledger (owner 2026-10-10): how much of each currency exists on the server, and what creates and removes it.
    ///
    /// 1. Supply: once a day every character's banked balance and the currency items in their packs are added up into
    ///    one row per currency (economy_supply_daily). Read-only on game data.
    /// 2. Flow: every change to a banked currency other than pyreals, and every currency item entering or leaving a
    ///    character, is rolled up per day, character, source and detail (economy_flow_daily). Pyreal flows are not
    ///    recorded here; the page reads them from the pyreal ledger. Sources are the pyreal ledger's scopes
    ///    (<see cref="PyrealLedger.Begin"/>), so one label covers every currency a code path touches.
    ///
    /// Nothing here changes game behavior. The balance write always happens first and on its normal path; recording
    /// never throws and never touches the database. A background timer writes batches.
    /// </summary>
    public static class EconomyLedger
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // ---- sources added for currencies other than pyreals -------------------------------------------------------

        public const string SrcKill = "Kill";
        public const string SrcAllegiance = "Allegiance";
        public const string SrcEnlighten = "Enlighten";
        public const string SrcPowerball = "Powerball";
        public const string SrcFellowship = "Fellowship";
        public const string SrcQuest = "Quest";
        public const string SrcOtherLuminance = "Other";
        /// <summary>The blackjack commands. A bet is placed by one command and paid by another, so they share one label and net to the house edge.</summary>
        public const string SrcCasino = "Casino";
        /// <summary>Spending one use of a key on a chest or door; the detail is what was unlocked.</summary>
        public const string SrcKeyUse = "KeyUse";

        private static readonly HashSet<string> casinoCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "blackjack", "hit", "stand", "double", "split", "fblackjack", "fhit", "fstand", "fdouble", "fsplit",
        };

        /// <summary>Label for luminance granted with no scope open (see Player.AddLuminance).</summary>
        public static string SourceFor(ACE.Entity.Enum.XpType xpType)
        {
            switch (xpType)
            {
                case ACE.Entity.Enum.XpType.Kill: return SrcKill;
                case ACE.Entity.Enum.XpType.Fellowship: return SrcFellowship;
                case ACE.Entity.Enum.XpType.Allegiance: return SrcAllegiance;
                case ACE.Entity.Enum.XpType.Quest: return SrcQuest;
                default: return SrcOtherLuminance;
            }
        }

        /// <summary>
        /// Scope for a luminance grant (see Player.AddLuminance). A fellowship share is always "Fellowship", whether it
        /// is paid inside the killer's kill scope or later on the member's own thread; the creature is kept when known.
        /// Anything else keeps the scope that is open, or is labeled by its type when none is.
        /// </summary>
        public static PyrealLedger.Scope BeginLuminance(ACE.Entity.Enum.XpType xpType)
        {
            if (!flowTracking)
                return default;

            if (xpType == ACE.Entity.Enum.XpType.Fellowship)
            {
                var ctx = PyrealLedger.CurrentContext;
                return ctx != null && ctx.Source == SrcKill
                    ? PyrealLedger.Begin(SrcFellowship, ctx.GetKey(), ctx.GetName())
                    : PyrealLedger.Begin(SrcFellowship);
            }

            return PyrealLedger.BeginIfNone(SourceFor(xpType));
        }

        /// <summary>The /bank command: a move between a character's packs and bank, or between characters.</summary>
        public const string SrcBank = "Bank";

        /// <summary>Sources that move currency without creating or destroying any.</summary>
        private static readonly HashSet<string> internalSources = new HashSet<string>(StringComparer.Ordinal)
        {
            SrcBank,
            PyrealLedger.SrcDeposit, PyrealLedger.SrcWithdraw, PyrealLedger.SrcTransfer, PyrealLedger.SrcTrade, PyrealLedger.SrcGive,
            PyrealLedger.SrcGround, PyrealLedger.SrcChest, PyrealLedger.SrcPlayerCorpse, PyrealLedger.SrcDeath, PyrealLedger.SrcOpening,
        };

        public static bool IsInternalSource(string source) => source != null && internalSources.Contains(source);

        /// <summary>
        /// The source a row is netted under. An item handed to an NPC is recorded as GiveNPC and what the NPC does in
        /// return as Emote, both with the NPC as detail; they are two halves of one exchange. Netted apart, a luminance
        /// token turned in for its own value would show as a faucet and an equal sink.
        /// </summary>
        public static string NetSource(string source) => source == PyrealLedger.SrcGiveNpc ? PyrealLedger.SrcEmote : source;

        /// <summary>The same rule as <see cref="NetSource"/>, as SQL over a `source` column.</summary>
        public const string NetSourceSql = "CASE WHEN `source` = 'GiveNPC' THEN 'Emote' ELSE `source` END";

        public static IEnumerable<string> InternalSources => internalSources;

        public const byte FormBank = 1;
        public const byte FormItem = 2;

        // ---- currencies --------------------------------------------------------------------------------------------

        public sealed class Currency
        {
            public int Index;
            public string Key;
            public string Name;
            /// <summary>Banked balance properties on the character (the second one is a legacy balance still spendable).</summary>
            public PropertyInt64[] Bank;
            /// <summary>Item wcids that hold this currency.</summary>
            public uint[] Items = Array.Empty<uint>();
            /// <summary>Key items: the amount is the remaining uses (Structure), not the stack size.</summary>
            public bool ItemsUseStructure;
            /// <summary>Flows come from the pyreal ledger's tables, not from this class.</summary>
            public bool FromPyrealLedger;
            /// <summary>Amount of currency one unit of an item holds (for keys: the uses of a full key).</summary>
            public Func<uint, long> UnitValue = _ => 1;
        }

        // every key the bank accepts (Player_Bank DepositLegendaryKeys / DepositMythicalKeys), with the uses of a full one
        private static readonly Dictionary<uint, long> legendaryKeyUses = new Dictionary<uint, long> { [500010] = 25, [51954] = 10, [52010] = 5, [48748] = 2, [48746] = 1 };
        private static readonly Dictionary<uint, long> mythicalKeyUses = new Dictionary<uint, long> { [90000110] = 25, [90000109] = 10, [90000108] = 5, [90000107] = 2, [90000104] = 1 };
        private static readonly uint[] lumTokens = { 227190187, 300101211, 867530211, 867530194, 64454972 };

        public static readonly Currency[] Currencies = BuildCurrencies();

        private static Currency[] BuildCurrencies()
        {
            var list = new[]
            {
                new Currency
                {
                    Key = "pyreals", Name = "Pyreals", Bank = new[] { PropertyInt64.BankedPyreals },
                    Items = PyrealLedger.CurrencyWcids.ToArray(), FromPyrealLedger = true, UnitValue = PyrealLedger.FaceValue,
                },
                new Currency
                {
                    Key = "luminance", Name = "Luminance", Bank = new[] { PropertyInt64.BankedLuminance, PropertyInt64.AvailableLuminance },
                    Items = lumTokens, UnitValue = Player.LumTokenValue,
                },
                new Currency { Key = "enlightened-coins", Name = "Enlightened Coins", Bank = new[] { PropertyInt64.BankedEnlightenedCoins }, Items = new uint[] { 300004 } },
                new Currency { Key = "weakly-enlightened-coins", Name = "Weakly Enlightened Coins", Bank = new[] { PropertyInt64.BankedWeaklyEnlightenedCoins }, Items = new uint[] { 300003 } },
                new Currency { Key = "prestige-coins", Name = "Prestige Coins", Bank = new[] { PropertyInt64.BankedPrestigeCoins }, Items = new[] { Player.PRESTIGE_COIN_WCID } },
                new Currency
                {
                    Key = "legendary-keys", Name = "Legendary Keys", Bank = new[] { PropertyInt64.BankedLegendaryKeys },
                    Items = legendaryKeyUses.Keys.ToArray(), ItemsUseStructure = true, UnitValue = w => legendaryKeyUses.TryGetValue(w, out var u) ? u : 1,
                },
                new Currency
                {
                    Key = "mythical-keys", Name = "Mythical Keys", Bank = new[] { PropertyInt64.BankedMythicalKeys },
                    Items = mythicalKeyUses.Keys.ToArray(), ItemsUseStructure = true, UnitValue = w => mythicalKeyUses.TryGetValue(w, out var u) ? u : 1,
                },
                // bankable since the auto-deposit work of 2026-10-13; spent on enlightenments, pack first then bank
                new Currency { Key = "enlightenment-tokens", Name = "Enlightenment Tokens", Bank = new[] { PropertyInt64.BankedEnlightenmentTokens }, Items = new[] { Player.ENLIGHTENMENT_TOKEN_WCID } },
            };

            for (var i = 0; i < list.Length; i++)
                list[i].Index = i;

            return list;
        }

        public static Currency Find(string key) => Currencies.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

        /// <summary>The currency a banked property belongs to when its flow is recorded here, or null.</summary>
        private static Currency FlowCurrencyOf(PropertyInt64 property)
        {
            switch (property)
            {
                case PropertyInt64.BankedLuminance:
                case PropertyInt64.AvailableLuminance:
                    return Currencies[1];
                case PropertyInt64.BankedEnlightenedCoins:
                    return Currencies[2];
                case PropertyInt64.BankedWeaklyEnlightenedCoins:
                    return Currencies[3];
                case PropertyInt64.BankedPrestigeCoins:
                    return Currencies[4];
                case PropertyInt64.BankedLegendaryKeys:
                    return Currencies[5];
                case PropertyInt64.BankedMythicalKeys:
                    return Currencies[6];
                case PropertyInt64.BankedEnlightenmentTokens:
                    return Currencies[7];
                default:
                    return null;
            }
        }

        private static readonly Dictionary<uint, Currency> flowItemCurrency = Currencies
            .Where(c => !c.FromPyrealLedger)
            .SelectMany(c => c.Items.Select(w => (w, c)))
            .ToDictionary(x => x.w, x => x.c);

        private static readonly Dictionary<uint, long> unitValueCache = new Dictionary<uint, long>();

        /// <summary>Amount of currency in one unit of an item. Lum token values come from the world database, so they are cached.</summary>
        internal static long UnitValueOf(Currency currency, uint wcid)
        {
            lock (unitValueCache)
            {
                if (unitValueCache.TryGetValue(wcid, out var cached))
                    return cached;
            }

            long value = 0;
            try
            {
                value = currency.UnitValue(wcid);
            }
            catch (Exception ex)
            {
                log.Warn($"[EconomyLedger] UnitValue({wcid}) failed: {ex.Message}");
            }

            lock (unitValueCache)
                unitValueCache[wcid] = value;

            return value;
        }

        // ---- state -------------------------------------------------------------------------------------------------

        private sealed class Bucket
        {
            public uint AccountId;
            public string CharName;
            public string DetailName;
            public long Events;
            public long AmountIn;
            public long AmountOut;
        }

        private readonly struct BucketKey : IEquatable<BucketKey>
        {
            public readonly DateTime Day;
            public readonly int Currency;
            public readonly uint CharId;
            public readonly byte Form;
            public readonly string Source;
            public readonly string DetailKey;

            public BucketKey(DateTime day, int currency, uint charId, byte form, string source, string detailKey)
            {
                Day = day;
                Currency = currency;
                CharId = charId;
                Form = form;
                Source = source;
                DetailKey = detailKey;
            }

            public bool Equals(BucketKey other) => Day == other.Day && Currency == other.Currency && CharId == other.CharId && Form == other.Form
                && string.Equals(Source, other.Source, StringComparison.Ordinal) && string.Equals(DetailKey, other.DetailKey, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is BucketKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Day, Currency, CharId, Form, Source, DetailKey);
        }

        private static readonly object sync = new object();
        private static Dictionary<BucketKey, Bucket> buckets = new Dictionary<BucketKey, Bucket>();

        /// <summary>
        /// Per currency: everything recorded in minus everything recorded out since the last supply snapshot, internal
        /// moves included (a move between two characters cancels; a move into a housing chest does not, and the supply
        /// count does not see that chest either). The snapshot compares it with how much the supply actually changed.
        /// </summary>
        private static readonly long[] netSinceSnapshot = new long[Currencies.Length];

        /// <summary>
        /// The same, for banked balances only. A snapshot taken with economy_snapshot_items off counts no items, so a
        /// deposit (item out, bank in) raises its supply; only the bank side of the flows can be compared with it.
        /// </summary>
        private static readonly long[] netBankSinceSnapshot = new long[Currencies.Length];

        /// <summary>True while <see cref="netSinceSnapshot"/> holds every change since the last snapshot (recording was never off in between).</summary>
        private static bool netCovered;

        /// <summary>Whether the previous supply snapshot counted pack items (null = not known). Two snapshots counted differently cannot be compared.</summary>
        private static bool? lastSnapshotCountedItems;

        /// <summary>Set when Shutdown begins: no new snapshot may start.</summary>
        private static volatile bool shuttingDown;

        private static volatile bool flowTracking;
        private static volatile bool tablesReady;
        private static Timer timer;
        private static int tickRunning;
        private static int snapshotRunning;
        private static DateTime lastCleanup = DateTime.MinValue;
        private static DateTime lastSnapshotDay = DateTime.MinValue;

        private const int TickMs = 60000;
        private const int CleanupIntervalHours = 6;
        private const int CleanupBatchRows = 5000;
        private const int BatchRows = 200;
        /// <summary>A character counts as active when it logged in within this many days.</summary>
        public const int ActiveDays = 30;

        /// <summary>True while currency changes are being recorded.</summary>
        public static bool FlowTracking => flowTracking;

        public static bool TablesReady => tablesReady;

        public static DateTime? FlowTrackingSinceUtc { get; private set; }

        public static DateTime? LastSnapshotUtc { get; private set; }

        public static bool SnapshotRunning => snapshotRunning != 0;

        // ---- hooks -------------------------------------------------------------------------------------------------

        /// <summary>
        /// True when a write of <paramref name="property"/> should be reported to <see cref="OnBalanceChanged"/>. One
        /// volatile read first: this sits on the SetProperty(PropertyInt64) path that XP uses on every kill.
        /// </summary>
        public static bool Tracks(PropertyInt64 property) => flowTracking && FlowCurrencyOf(property) != null;

        /// <summary>
        /// A banked currency balance was written (the write is already done). Records the difference under the source
        /// scope open on this thread. Staff characters are not recorded. Never throws.
        /// </summary>
        public static void OnBalanceChanged(uint charId, string name, uint accountId, uint accessLevel, PropertyInt64 property, long before, long after)
        {
            if (!flowTracking || before == after || accessLevel > 0)
                return;

            try
            {
                var currency = FlowCurrencyOf(property);
                if (currency == null)
                    return;

                ResolveSource(PyrealLedger.CurrentContext, charId, null, null, null, out var source, out var detailKey, out var detailName);
                if (source == PyrealLedger.SrcIgnore)
                    return;

                var delta = after - before;
                Add(currency.Index, charId, accountId, name, FormBank, source, detailKey, detailName, Math.Max(0, delta), Math.Max(0, -delta));
            }
            catch (Exception ex)
            {
                log.Error($"[EconomyLedger] OnBalanceChanged failed: {ex.Message}");
            }
        }

        /// <summary>True when an item of this wcid entering or leaving a character is recorded here.</summary>
        public static bool TracksItem(uint wcid) => flowTracking && flowItemCurrency.ContainsKey(wcid);

        /// <summary>
        /// A currency item stack entered (<paramref name="incoming"/>) or left a character's possession. Called from the
        /// pyreal ledger's item hooks with the same arguments. Keys are counted as full keys. Never throws.
        /// </summary>
        public static void OnItem(Player player, uint wcid, long units, bool incoming, string source, string detailKey, string detailName)
        {
            if (!flowTracking || player == null || units <= 0 || !flowItemCurrency.TryGetValue(wcid, out var currency))
                return;

            try
            {
                if ((player.Account?.AccessLevel ?? 0) > 0)
                    return;

                var charId = player.Guid.Full;
                ResolveSource(PyrealLedger.CurrentContext, charId, source, detailKey, detailName, out var src, out var key, out var name);
                if (src == PyrealLedger.SrcIgnore)
                    return;

                var amount = UnitValueOf(currency, wcid) * units;
                if (amount <= 0)
                    return;

                Add(currency.Index, charId, player.Account?.AccountId ?? 0, player.Name, FormItem, src, key, name, incoming ? amount : 0, incoming ? 0 : amount);
            }
            catch (Exception ex)
            {
                log.Error($"[EconomyLedger] OnItem failed: {ex.Message}");
            }
        }

        /// <summary>
        /// One use of a key was spent on <paramref name="target"/>. A key is a currency whose amount is its remaining
        /// uses, so this is the real sink; the item itself disappearing when the last use is gone is not (the caller
        /// runs that under <see cref="PyrealLedger.SrcIgnore"/>). Never throws.
        /// </summary>
        public static void OnKeyUse(Player player, uint keyWcid, WorldObject target)
        {
            if (!flowTracking || player == null || !flowItemCurrency.TryGetValue(keyWcid, out var currency) || !currency.ItemsUseStructure)
                return;

            try
            {
                if ((player.Account?.AccessLevel ?? 0) > 0)
                    return;

                Add(currency.Index, player.Guid.Full, player.Account?.AccountId ?? 0, player.Name, FormItem, SrcKeyUse,
                    target?.WeenieClassId.ToString() ?? "", target?.Name ?? "", 0, 1);
            }
            catch (Exception ex)
            {
                log.Error($"[EconomyLedger] OnKeyUse failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Works out the label for a change to <paramref name="charId"/>: an explicit source wins, then the scope open on
        /// this thread, then Unattributed. A chat command scope for /bank becomes <see cref="SrcBank"/>.
        /// </summary>
        internal static void ResolveSource(PyrealLedger.SourceContext ctx, uint charId, string explicitSource, string explicitKey, string explicitName,
            out string source, out string detailKey, out string detailName)
        {
            if (explicitSource != null)
            {
                source = explicitSource;
                detailKey = explicitKey ?? "";
                detailName = explicitName ?? "";
                return;
            }

            if (ctx == null)
            {
                source = PyrealLedger.SrcUnattributed;
                detailKey = explicitKey ?? "";
                detailName = explicitName ?? "";
                return;
            }

            var isActor = ctx.ActorGuid == 0 || ctx.ActorGuid == charId;
            source = ctx.Source;
            detailKey = explicitKey ?? (isActor ? ctx.GetKey() : ctx.ActorGuid.ToString());
            detailName = explicitName ?? (isActor ? ctx.GetName() : ctx.ActorName);

            if (source != PyrealLedger.SrcCommand)
                return;

            var command = ctx.GetKey();

            if (IsBankCommand(command))
            {
                source = SrcBank;
                if (isActor)
                {
                    detailKey = "";
                    detailName = "";
                }
            }
            else if (casinoCommands.Contains(command))
            {
                // every player at the table, whoever typed the command
                source = SrcCasino;
                detailKey = "blackjack";
                detailName = "Blackjack";
            }
            else if (string.Equals(command, "clap", StringComparison.OrdinalIgnoreCase))
            {
                // the coins are credited before the command opens its own Clap scope
                source = PyrealLedger.SrcClap;
                detailKey = "";
                detailName = "";
            }
        }

        private static bool IsBankCommand(string command) =>
            string.Equals(command, "bank", StringComparison.OrdinalIgnoreCase) || string.Equals(command, "b", StringComparison.OrdinalIgnoreCase);

        private static string Truncate(string s, int max) => s == null ? "" : (s.Length <= max ? s : s.Substring(0, max));

        private static void Add(int currency, uint charId, uint accountId, string charName, byte form, string source, string detailKey, string detailName, long amountIn, long amountOut)
        {
            var key = new BucketKey(DateTime.UtcNow.Date, currency, charId, form, Truncate(source, 32), Truncate(detailKey, 64));

            lock (sync)
            {
                if (!buckets.TryGetValue(key, out var bucket))
                {
                    bucket = new Bucket { AccountId = accountId, CharName = charName ?? "", DetailName = Truncate(detailName, 128) };
                    buckets[key] = bucket;
                }

                bucket.Events++;
                bucket.AmountIn += amountIn;
                bucket.AmountOut += amountOut;

                netSinceSnapshot[currency] += amountIn - amountOut;
                if (form == FormBank)
                    netBankSinceSnapshot[currency] += amountIn - amountOut;
            }
        }

        /// <summary>
        /// Hands over the net recorded flow per currency since the last snapshot (all of it, and the banked side alone)
        /// and starts a new period. Null when the period was not fully recorded (recording was off for part of it, or
        /// this is the first snapshot).
        /// </summary>
        internal static (long[] net, long[] bankNet)? TakeNetSinceSnapshot()
        {
            lock (sync)
            {
                (long[], long[])? taken = netCovered ? ((long[])netSinceSnapshot.Clone(), (long[])netBankSinceSnapshot.Clone()) : null;

                Array.Clear(netSinceSnapshot, 0, netSinceSnapshot.Length);
                Array.Clear(netBankSinceSnapshot, 0, netBankSinceSnapshot.Length);
                netCovered = flowTracking;

                return taken;
            }
        }

        /// <summary>The period cannot be trusted any more (recording stopped, or a snapshot that took the totals failed).</summary>
        private static void BreakNetCoverage()
        {
            lock (sync)
                netCovered = false;
        }

        /// <summary>Rows waiting to be written. For tests.</summary>
        internal static List<(string currency, uint charId, byte form, string source, string detailKey, long amountIn, long amountOut)> PendingForTests()
        {
            lock (sync)
                return buckets.Select(kv => (Currencies[kv.Key.Currency].Key, kv.Key.CharId, kv.Key.Form, kv.Key.Source, kv.Key.DetailKey, kv.Value.AmountIn, kv.Value.AmountOut)).ToList();
        }

        /// <summary>Turns recording on with no tables and no timer, and drops anything pending. For tests.</summary>
        internal static void ResetForTests(bool tracking)
        {
            lock (sync)
            {
                buckets = new Dictionary<BucketKey, Bucket>();
                Array.Clear(netSinceSnapshot, 0, netSinceSnapshot.Length);
                Array.Clear(netBankSinceSnapshot, 0, netBankSinceSnapshot.Length);
                netCovered = false;
            }
            flowTracking = tracking;
        }

        // ---- lifecycle ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Creates the tables and starts the timer that writes flow rows and takes the daily supply snapshot. Call after
        /// PlayerManager.Initialize. <paramref name="trackFlows"/> is ServerConfig economy_ledger.
        /// </summary>
        public static void Initialize(bool trackFlows)
        {
            try
            {
                EnsureTables();
                tablesReady = true;

                var meta = LoadMeta();
                if (meta.TryGetValue("last_snapshot_utc", out var snap) && TryParseUtc(snap, out var snapUtc))
                {
                    LastSnapshotUtc = snapUtc;
                    lastSnapshotDay = snapUtc.Date;
                }

                if (meta.TryGetValue("snapshot_items", out var mode))
                    lastSnapshotCountedItems = mode == "1" ? true : mode == "0" ? false : null;

                shuttingDown = false;

                if (trackFlows)
                    StartFlowTracking(meta);
                else
                {
                    // recording is off for this run: whatever a clean stop saved no longer describes an unbroken period
                    SaveMeta(new Dictionary<string, string> { ["flow_resume_ok"] = "0", ["net_valid"] = "0" });
                }

                timer = new Timer(_ => TimerTick(), null, 15000, TickMs);

                log.Info($"[EconomyLedger] Initialized: flow tracking {(flowTracking ? "on" : "off")}, last supply snapshot {(LastSnapshotUtc.HasValue ? LastSnapshotUtc.Value.ToString("yyyy-MM-dd HH:mm") + " UTC" : "never")}");
            }
            catch (Exception ex)
            {
                log.Error($"[EconomyLedger] Initialize failed, economy ledger disabled: {ex}");
                flowTracking = false;
                tablesReady = false;
            }
        }

        private static void StartFlowTracking(Dictionary<string, string> meta)
        {
            // "tracking since" restarts whenever recording was off in between, so the page never implies full coverage
            // (flow_resume_ok is only "1" after a shutdown that was recording and wrote every pending row)
            var continued = meta.TryGetValue("flow_resume_ok", out var ok) && ok == "1";

            FlowTrackingSinceUtc = continued && meta.TryGetValue("flow_since_utc", out var since) && TryParseUtc(since, out var sinceUtc) ? sinceUtc : DateTime.UtcNow;

            // the running net since the last snapshot was saved by a clean shutdown; anything else starts uncovered
            lock (sync)
            {
                Array.Clear(netSinceSnapshot, 0, netSinceSnapshot.Length);
                Array.Clear(netBankSinceSnapshot, 0, netBankSinceSnapshot.Length);
                netCovered = false;

                if (continued && meta.TryGetValue("net_valid", out var valid) && valid == "1")
                {
                    netCovered = true;
                    foreach (var currency in Currencies)
                    {
                        if (meta.TryGetValue("net_" + currency.Key, out var text) && long.TryParse(text, out var net))
                            netSinceSnapshot[currency.Index] = net;
                        if (meta.TryGetValue("netbank_" + currency.Key, out var bankText) && long.TryParse(bankText, out var bankNet))
                            netBankSinceSnapshot[currency.Index] = bankNet;
                    }
                }
            }

            // while running, the stored state says "not resumable": only a clean stop turns it back on
            if (!SaveMeta(new Dictionary<string, string>
                {
                    ["flow_since_utc"] = FlowTrackingSinceUtc.Value.ToString("o"),
                    ["flow_resume_ok"] = "0",
                    ["net_valid"] = "0",
                }))
                log.Warn("[EconomyLedger] Could not mark flow tracking as running; if the server crashes, the next start may wrongly continue the previous tracking period.");

            flowTracking = true;
        }

        /// <summary>Switches flow recording on or off while the server runs (@economyledger on|off). Snapshots keep running.</summary>
        public static bool SetFlowTracking(bool on)
        {
            if (!tablesReady || on == flowTracking)
                return false;

            try
            {
                if (on)
                {
                    StartFlowTracking(new Dictionary<string, string>());
                    log.Warn("[EconomyLedger] Flow tracking switched on by an admin.");
                }
                else
                {
                    flowTracking = false;
                    BreakNetCoverage();

                    // not at the same time as the timer's flush: two upserts over the same rows can deadlock in MySQL.
                    // If the timer holds it, its own flush (or the next one) writes the rows.
                    if (Interlocked.CompareExchange(ref tickRunning, 1, 0) == 0)
                    {
                        try
                        {
                            FlushBuckets();
                        }
                        finally
                        {
                            Interlocked.Exchange(ref tickRunning, 0);
                        }
                    }

                    log.Warn("[EconomyLedger] Flow tracking switched off by an admin.");
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[EconomyLedger] SetFlowTracking failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Final flush. Call after the shard save queue has drained. Never holds up or changes a shutdown.</summary>
        public static void Shutdown()
        {
            if (!tablesReady)
                return;

            // from here on no new snapshot starts
            shuttingDown = true;

            // true once this method owns the flush; a timer callback still running keeps its own ownership
            var acquired = false;

            try
            {
                timer?.Dispose();
                timer = null;

                for (var waited = 0; waited <= 10000; waited += 50)
                {
                    if (Interlocked.CompareExchange(ref tickRunning, 1, 0) == 0)
                    {
                        acquired = true;
                        break;
                    }

                    Thread.Sleep(50);
                }

                var wasTracking = flowTracking;
                flowTracking = false;

                if (!acquired)
                {
                    // The timer's flush is still writing. A second flush over the same rows can deadlock in MySQL, so
                    // the rows are left to it, and the stop is not recorded as clean: the next start begins a new
                    // tracking period rather than claim an unbroken one.
                    log.Warn("[EconomyLedger] A flush was still running at shutdown; the final flush was skipped and the next start begins a new tracking period.");
                    return;
                }

                // a restart that loses no rows keeps the "tracking since" date and the running net since the last snapshot
                if (FlushBuckets() && wasTracking)
                {
                    var meta = new Dictionary<string, string> { ["flow_clean_stop_utc"] = DateTime.UtcNow.ToString("o"), ["flow_resume_ok"] = "1" };

                    // A snapshot still in flight has already taken the running net but not yet saved its supply row.
                    // What is left in the counters then does not line up with any saved snapshot, so it is not kept.
                    var snapshotInFlight = Volatile.Read(ref snapshotRunning) != 0;

                    lock (sync)
                    {
                        meta["net_valid"] = netCovered && !snapshotInFlight ? "1" : "0";
                        foreach (var currency in Currencies)
                        {
                            meta["net_" + currency.Key] = netSinceSnapshot[currency.Index].ToString();
                            meta["netbank_" + currency.Key] = netBankSinceSnapshot[currency.Index].ToString();
                        }
                    }

                    // one transaction: either the whole resumable state is stored, or none of it and the start is fresh
                    if (!SaveMeta(meta))
                        log.Warn("[EconomyLedger] The clean-stop state could not be saved; the next start begins a new tracking period.");
                }

                log.Info("[EconomyLedger] Final flush complete");
            }
            catch (Exception ex)
            {
                log.Error($"[EconomyLedger] Shutdown flush failed: {ex}");
            }
            finally
            {
                tablesReady = false;
                if (acquired)
                    Interlocked.Exchange(ref tickRunning, 0);
            }
        }

        private static void TimerTick()
        {
            if (Interlocked.CompareExchange(ref tickRunning, 1, 0) != 0)
                return;

            try
            {
                FlushBuckets();

                if (ServerConfig.economy_snapshots.Value && SnapshotDue(DateTime.UtcNow, lastSnapshotDay, LastSnapshotUtc.HasValue, ServerConfig.economy_snapshot_hour_utc.Value))
                    StartSnapshot("daily");

                if ((DateTime.UtcNow - lastCleanup).TotalHours >= CleanupIntervalHours)
                {
                    lastCleanup = DateTime.UtcNow;
                    StartCleanup();
                }
            }
            catch (Exception ex)
            {
                log.Error($"[EconomyLedger] Tick failed: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref tickRunning, 0);
            }
        }

        // ---- supply snapshot ---------------------------------------------------------------------------------------

        /// <summary>
        /// True when the daily snapshot should run: none yet today (UTC) and the configured quiet hour has been reached.
        /// A server that has never taken one does not wait for the hour. Pure; unit tested.
        /// </summary>
        internal static bool SnapshotDue(DateTime nowUtc, DateTime lastDay, bool everTaken, long hourUtc)
        {
            if (lastDay == nowUtc.Date)
                return false;

            return !everTaken || nowUtc.Hour >= Math.Clamp(hourUtc, 0, 23);
        }

        public sealed class SupplyRow
        {
            public string Currency;
            public long Banked;
            public long Items;
            public long Holders;
            public long ActiveSupply;
            public long ActiveHolders;
            public long Top1PctAmount;
            public long LargestHolder;
            public long AdminSupply;
            /// <summary>Net recorded flow since the previous snapshot, or null when that period was not fully recorded.</summary>
            public long? FlowNet;
        }

        /// <summary>
        /// Starts a supply snapshot on a pool thread. Returns false if one is already running or the tables are not
        /// ready. Today's row for each currency is replaced, so running it more than once a day is safe.
        /// </summary>
        public static bool StartSnapshot(string reason)
        {
            if (!tablesReady || shuttingDown)
                return false;

            if (Interlocked.CompareExchange(ref snapshotRunning, 1, 0) != 0)
                return false;

            // set before the work so a failing snapshot is retried tomorrow, not every minute
            lastSnapshotDay = DateTime.UtcNow.Date;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var started = DateTime.UtcNow;

                    // taken right before the balances are read, so both describe the same moment (the pack count
                    // that follows takes a minute or two; item moves in that window are small timing noise)
                    var includeItems = ServerConfig.economy_snapshot_items.Value;
                    var net = TakeNetSinceSnapshot();
                    var rows = ComputeSupply(includeItems);

                    // The flows are compared with how much the supply changed since the previous snapshot, so both
                    // snapshots must have been counted the same way; after a change of economy_snapshot_items the
                    // first snapshot has nothing valid to compare with.
                    var sameCounting = lastSnapshotCountedItems == includeItems;

                    foreach (var row in rows)
                    {
                        var currency = Find(row.Currency);
                        // Pyreal flows live in the pyreal ledger, which has its own checks. Keys are left out too: a key
                        // moving between a pack and the bank is recorded as a full key on one side and its remaining
                        // uses on the other, so the two would never agree.
                        if (net.HasValue && sameCounting && currency != null && !currency.FromPyrealLedger && !currency.ItemsUseStructure)
                        {
                            // a supply without items only moves with the banked side of the flows
                            row.FlowNet = (includeItems ? net.Value.net : net.Value.bankNet)[currency.Index];
                        }
                    }

                    SaveSupply(started.Date, rows);

                    LastSnapshotUtc = started;
                    lastSnapshotCountedItems = includeItems;
                    SaveMeta(new Dictionary<string, string> { ["last_snapshot_utc"] = started.ToString("o"), ["snapshot_items"] = includeItems ? "1" : "0" });

                    log.Info($"[EconomyLedger] Supply snapshot ({reason}) written in {(DateTime.UtcNow - started).TotalSeconds:N1} s: " +
                             string.Join(", ", rows.Select(r => $"{r.Currency}={r.Banked + r.Items:N0}")));

                    // who gained an enlightenment or an aug since the last snapshot (has its own error handling)
                    EconomyProgress.Update();
                }
                catch (Exception ex)
                {
                    // the running net was already taken, so the next snapshot's period is incomplete
                    BreakNetCoverage();
                    log.Error($"[EconomyLedger] Supply snapshot failed: {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref snapshotRunning, 0);
                }
            });

            return true;
        }

        /// <summary>One character's holdings of one currency, as input to <see cref="Summarize"/>.</summary>
        internal readonly struct Holding
        {
            public readonly long Banked;
            public readonly long Items;
            public readonly bool Active;
            public readonly bool Admin;

            public Holding(long banked, long items, bool active, bool admin)
            {
                Banked = banked;
                Items = items;
                Active = active;
                Admin = admin;
            }
        }

        /// <summary>
        /// Adds up one currency. Staff characters only count toward AdminSupply. Negative balances count as zero.
        /// The top 1% is the richest 1% of holders, rounded up (at least one). Pure; unit tested.
        /// </summary>
        internal static SupplyRow Summarize(string currency, IEnumerable<Holding> holdings)
        {
            var row = new SupplyRow { Currency = currency };
            var totals = new List<long>();

            foreach (var h in holdings)
            {
                var banked = Math.Max(0, h.Banked);
                var items = Math.Max(0, h.Items);
                var total = banked + items;
                if (total == 0)
                    continue;

                if (h.Admin)
                {
                    row.AdminSupply += total;
                    continue;
                }

                row.Banked += banked;
                row.Items += items;
                row.Holders++;
                totals.Add(total);

                if (h.Active)
                {
                    row.ActiveSupply += total;
                    row.ActiveHolders++;
                }
            }

            if (totals.Count > 0)
            {
                totals.Sort((a, b) => b.CompareTo(a));
                row.LargestHolder = totals[0];

                var top = (int)Math.Ceiling(totals.Count / 100.0);
                for (var i = 0; i < top; i++)
                    row.Top1PctAmount += totals[i];
            }

            return row;
        }

        private static List<SupplyRow> ComputeSupply(bool includeItems)
        {
            var activeSince = DateTimeOffset.UtcNow.AddDays(-ActiveDays).ToUnixTimeSeconds();
            var players = PlayerManager.GetAllPlayers();

            // char -> (admin, active)
            var info = new Dictionary<uint, (bool admin, bool active)>(players.Count);
            // per currency: char -> banked
            var banked = Currencies.Select(_ => new Dictionary<uint, long>()).ToArray();

            foreach (var player in players)
            {
                var charId = player.Guid.Full;
                var admin = (player.Account?.AccessLevel ?? 0) > 0;
                var active = player is Player || (player.GetProperty(PropertyFloat.LoginTimestamp) ?? 0) >= activeSince;
                info[charId] = (admin, active);

                foreach (var currency in Currencies)
                {
                    long value = 0;
                    foreach (var property in currency.Bank)
                        value += Math.Max(0, player.GetProperty(property) ?? 0);

                    if (value != 0)
                        banked[currency.Index][charId] = value;
                }
            }

            var items = Currencies.Select(_ => new Dictionary<uint, long>()).ToArray();
            if (includeItems)
                LoadItemHoldings(items);

            var rows = new List<SupplyRow>();
            foreach (var currency in Currencies)
            {
                var chars = new HashSet<uint>(banked[currency.Index].Keys);
                chars.UnionWith(items[currency.Index].Keys);

                rows.Add(Summarize(currency.Key, chars.Select(c =>
                {
                    // an item holder PlayerManager does not know is a deleted character: not part of the supply
                    if (!info.TryGetValue(c, out var who))
                        return default;

                    banked[currency.Index].TryGetValue(c, out var b);
                    items[currency.Index].TryGetValue(c, out var i);
                    return new Holding(b, i, who.active, who.admin);
                })));
            }

            return rows;
        }

        /// <summary>Pause between the per-item queries of the pack count, so its disk reads are spread out.</summary>
        private const int ItemQueryPauseMs = 3000;

        /// <summary>
        /// Currency items in every character's main pack and side packs, straight from the shard database (read-only).
        /// Items in housing chests or on the ground are not counted.
        ///
        /// The query plan is already all primary key lookups (measured 2026-10-11), but it touches about 100,000 pages
        /// across the biota property tables: 22 to 51 seconds when they are not in MySQL's buffer pool. So it runs one
        /// item type at a time with a pause in between, once a day on a pool thread, instead of as one burst.
        /// </summary>
        private static void LoadItemHoldings(Dictionary<uint, long>[] items)
        {
            var byWcid = new Dictionary<uint, Currency>();
            foreach (var currency in Currencies)
                foreach (var wcid in currency.Items)
                    byWcid[wcid] = currency;

            if (byWcid.Count == 0)
                return;

            var first = true;

            foreach (var (wcid, currency) in byWcid.OrderBy(kv => kv.Key))
            {
                if (!first)
                    Thread.Sleep(ItemQueryPauseMs);
                first = false;

                // a connection per query: none is held open across the pauses
                using var ctx = new ShardDbContext();
                var con = OpenConnection(ctx);

                using var cmd = con.CreateCommand();
                cmd.CommandTimeout = 300;
                // an item's container is the character, or a side pack whose container is the character
                cmd.CommandText =
                    "SELECT ch.`id`, COUNT(*), SUM(COALESCE(st.`value`, 1)), COUNT(su.`value`), COALESCE(SUM(su.`value`), 0) " +
                    "FROM `biota` b " +
                    "JOIN `biota_properties_i_i_d` c ON c.`object_Id` = b.`id` AND c.`type` = 2 " +
                    "LEFT JOIN `biota_properties_i_i_d` c2 ON c2.`object_Id` = c.`value` AND c2.`type` = 2 " +
                    "JOIN `character` ch ON ch.`id` = COALESCE(c2.`value`, c.`value`) " +
                    "LEFT JOIN `biota_properties_int` st ON st.`object_Id` = b.`id` AND st.`type` = 12 " +
                    "LEFT JOIN `biota_properties_int` su ON su.`object_Id` = b.`id` AND su.`type` = 92 " +
                    "WHERE b.`weenie_Class_Id` = @wcid AND ch.`is_Deleted` = 0 " +
                    "GROUP BY ch.`id`";
                AddParam(cmd, "@wcid", wcid);

                var unit = UnitValueOf(currency, wcid);
                var held = items[currency.Index];

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var charId = Convert.ToUInt32(reader.GetValue(0));
                    var count = Convert.ToInt64(reader.GetValue(1));
                    var stack = Convert.ToInt64(reader.GetValue(2));
                    var withUses = Convert.ToInt64(reader.GetValue(3));
                    var uses = Convert.ToInt64(reader.GetValue(4));

                    // a key holds its remaining uses; a key with no uses stored is a full one
                    var amount = currency.ItemsUseStructure ? uses + (count - withUses) * unit : stack * unit;
                    if (amount <= 0)
                        continue;

                    held[charId] = held.TryGetValue(charId, out var existing) ? existing + amount : amount;
                }
            }
        }

        private static void SaveSupply(DateTime day, List<SupplyRow> rows)
        {
            using var ctx = new ShardDbContext();
            var con = OpenConnection(ctx);
            using var tx = con.BeginTransaction();

            foreach (var row in rows)
            {
                using var cmd = NewCommand(con);
                cmd.Transaction = tx;
                cmd.CommandText =
                    "INSERT INTO `economy_supply_daily` (`day_utc`,`currency`,`banked`,`items`,`holders`,`active_supply`,`active_holders`,`top1pct_amount`,`largest_holder`,`admin_supply`,`flow_net`,`taken_utc`) " +
                    "VALUES (@d,@c,@b,@i,@h,@as,@ah,@t,@l,@ad,@fn,@u) ON DUPLICATE KEY UPDATE `banked`=VALUES(`banked`),`items`=VALUES(`items`),`holders`=VALUES(`holders`)," +
                    "`active_supply`=VALUES(`active_supply`),`active_holders`=VALUES(`active_holders`),`top1pct_amount`=VALUES(`top1pct_amount`)," +
                    "`largest_holder`=VALUES(`largest_holder`),`admin_supply`=VALUES(`admin_supply`),`taken_utc`=VALUES(`taken_utc`)," +
                    // a second snapshot on the same day: the day's row covers the whole stretch since the previous day's
                    // snapshot, so the two nets add up, and one incomplete stretch makes the day incomplete
                    "`flow_net`=IF(`flow_net` IS NULL OR VALUES(`flow_net`) IS NULL, NULL, `flow_net` + VALUES(`flow_net`))";
                AddParam(cmd, "@d", day);
                AddParam(cmd, "@c", row.Currency);
                AddParam(cmd, "@b", row.Banked);
                AddParam(cmd, "@i", row.Items);
                AddParam(cmd, "@h", row.Holders);
                AddParam(cmd, "@as", row.ActiveSupply);
                AddParam(cmd, "@ah", row.ActiveHolders);
                AddParam(cmd, "@t", row.Top1PctAmount);
                AddParam(cmd, "@l", row.LargestHolder);
                AddParam(cmd, "@ad", row.AdminSupply);
                AddParam(cmd, "@fn", row.FlowNet);
                AddParam(cmd, "@u", DateTime.UtcNow);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        // ---- flow rows ---------------------------------------------------------------------------------------------

        /// <summary>Returns false if the write failed (the rows are kept for the next flush).</summary>
        private static bool FlushBuckets()
        {
            Dictionary<BucketKey, Bucket> toWrite;

            lock (sync)
            {
                if (buckets.Count == 0)
                    return true;
                toWrite = buckets;
                buckets = new Dictionary<BucketKey, Bucket>();
            }

            try
            {
                using var ctx = new ShardDbContext();
                var con = OpenConnection(ctx);

                // one transaction for every chunk: a failure rolls all of them back, so the retry below cannot add an
                // already written chunk a second time
                using var tx = con.BeginTransaction();

                foreach (var batch in toWrite.Chunk(BatchRows))
                {
                    using var cmd = NewCommand(con);
                    cmd.Transaction = tx;
                    var sb = new StringBuilder("INSERT INTO `economy_flow_daily` (`day_utc`,`currency`,`char_id`,`form`,`source`,`detail_key`,`account_id`,`char_name`,`detail_name`,`events`,`amount_in`,`amount_out`) VALUES ");
                    for (var i = 0; i < batch.Length; i++)
                    {
                        var (k, b) = (batch[i].Key, batch[i].Value);
                        if (i > 0) sb.Append(',');
                        sb.Append($"(@d{i},@cu{i},@c{i},@f{i},@s{i},@k{i},@a{i},@n{i},@dn{i},@e{i},@i{i},@o{i})");
                        AddParam(cmd, $"@d{i}", k.Day);
                        AddParam(cmd, $"@cu{i}", Currencies[k.Currency].Key);
                        AddParam(cmd, $"@c{i}", k.CharId);
                        AddParam(cmd, $"@f{i}", k.Form);
                        AddParam(cmd, $"@s{i}", k.Source);
                        AddParam(cmd, $"@k{i}", k.DetailKey ?? "");
                        AddParam(cmd, $"@a{i}", b.AccountId);
                        AddParam(cmd, $"@n{i}", Truncate(b.CharName, 64));
                        AddParam(cmd, $"@dn{i}", b.DetailName ?? "");
                        AddParam(cmd, $"@e{i}", b.Events);
                        AddParam(cmd, $"@i{i}", b.AmountIn);
                        AddParam(cmd, $"@o{i}", b.AmountOut);
                    }
                    sb.Append(" ON DUPLICATE KEY UPDATE `events`=`events`+VALUES(`events`),`amount_in`=`amount_in`+VALUES(`amount_in`)," +
                              "`amount_out`=`amount_out`+VALUES(`amount_out`),`char_name`=VALUES(`char_name`),`detail_name`=VALUES(`detail_name`)");
                    cmd.CommandText = sb.ToString();
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                // put them back so the next flush retries
                lock (sync)
                {
                    foreach (var kv in toWrite)
                    {
                        if (buckets.TryGetValue(kv.Key, out var existing))
                        {
                            existing.Events += kv.Value.Events;
                            existing.AmountIn += kv.Value.AmountIn;
                            existing.AmountOut += kv.Value.AmountOut;
                        }
                        else
                            buckets[kv.Key] = kv.Value;
                    }
                }

                log.Error($"[EconomyLedger] Flush failed ({toWrite.Count} rows), will retry: {ex.Message}");
                return false;
            }
        }

        // ---- retention ---------------------------------------------------------------------------------------------

        private static int cleanupRunning;

        /// <summary>
        /// Deletes flow rows older than ServerConfig economy_ledger_retention_days on a pool thread, in small batches.
        /// Supply snapshots are one small row per currency per day and are kept.
        /// </summary>
        private static void StartCleanup()
        {
            var days = ServerConfig.economy_ledger_retention_days.Value;
            if (days <= 0)
                return;

            if (Interlocked.CompareExchange(ref cleanupRunning, 1, 0) != 0)
                return;

            var cutoff = DateTime.UtcNow.Date.AddDays(-days);

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    long total = 0;

                    using var ctx = new ShardDbContext();
                    var con = OpenConnection(ctx);

                    while (true)
                    {
                        using var cmd = NewCommand(con);
                        cmd.CommandText = $"DELETE FROM `economy_flow_daily` WHERE `day_utc` < @cutoff LIMIT {CleanupBatchRows}";
                        cmd.CommandTimeout = 30;
                        AddParam(cmd, "@cutoff", cutoff);

                        var deleted = cmd.ExecuteNonQuery();
                        total += deleted;

                        if (deleted < CleanupBatchRows)
                            break;

                        Thread.Sleep(200);
                    }

                    if (total > 0)
                        log.Info($"[EconomyLedger] Retention ({days} days): deleted {total:N0} flow rows older than {cutoff:yyyy-MM-dd}");
                }
                catch (Exception ex)
                {
                    log.Error($"[EconomyLedger] Retention cleanup failed: {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref cleanupRunning, 0);
                }
            });
        }

        // ---- database ----------------------------------------------------------------------------------------------

        private static DbConnection OpenConnection(ShardDbContext ctx)
        {
            var con = ctx.Database.GetDbConnection();
            if (con.State != ConnectionState.Open)
                con.Open();
            return con;
        }

        /// <summary>A command with a short timeout, so a locked or slow table cannot hold a thread for long.</summary>
        private static DbCommand NewCommand(DbConnection con)
        {
            var cmd = con.CreateCommand();
            cmd.CommandTimeout = 10;
            return cmd;
        }

        private static void AddParam(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private static bool TryParseUtc(string text, out DateTime utc) =>
            DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out utc);

        private static void EnsureTables()
        {
            using var ctx = new ShardDbContext();
            var con = OpenConnection(ctx);

            void Execute(string sql)
            {
                using var cmd = NewCommand(con);
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }

            Execute(@"
CREATE TABLE IF NOT EXISTS `economy_supply_daily` (
  `day_utc`         date         NOT NULL,
  `currency`        varchar(32)  NOT NULL,
  `banked`          bigint       NOT NULL DEFAULT 0,
  `items`           bigint       NOT NULL DEFAULT 0,
  `holders`         bigint       NOT NULL DEFAULT 0,
  `active_supply`   bigint       NOT NULL DEFAULT 0,
  `active_holders`  bigint       NOT NULL DEFAULT 0,
  `top1pct_amount`  bigint       NOT NULL DEFAULT 0,
  `largest_holder`  bigint       NOT NULL DEFAULT 0,
  `admin_supply`    bigint       NOT NULL DEFAULT 0,
  `flow_net`        bigint       NULL,
  `taken_utc`       datetime     NOT NULL,
  PRIMARY KEY (`day_utc`, `currency`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");

            Execute(@"
CREATE TABLE IF NOT EXISTS `economy_flow_daily` (
  `day_utc`      date             NOT NULL,
  `currency`     varchar(32)      NOT NULL,
  `char_id`      int unsigned     NOT NULL,
  `form`         tinyint unsigned NOT NULL,
  `source`       varchar(32)      NOT NULL,
  `detail_key`   varchar(64)      NOT NULL,
  `account_id`   int unsigned     NOT NULL DEFAULT 0,
  `char_name`    varchar(64)      NOT NULL DEFAULT '',
  `detail_name`  varchar(128)     NOT NULL DEFAULT '',
  `events`       bigint           NOT NULL DEFAULT 0,
  `amount_in`    bigint           NOT NULL DEFAULT 0,
  `amount_out`   bigint           NOT NULL DEFAULT 0,
  PRIMARY KEY (`day_utc`, `currency`, `char_id`, `form`, `source`, `detail_key`),
  KEY `ix_currency_day` (`currency`, `day_utc`),
  KEY `ix_char_day` (`char_id`, `day_utc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");

            Execute(@"
CREATE TABLE IF NOT EXISTS `economy_meta` (
  `k` varchar(64)  NOT NULL,
  `v` varchar(255) NOT NULL,
  PRIMARY KEY (`k`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
        }

        private static Dictionary<string, string> LoadMeta()
        {
            var meta = new Dictionary<string, string>();

            using var ctx = new ShardDbContext();
            var con = OpenConnection(ctx);
            using var cmd = NewCommand(con);
            cmd.CommandText = "SELECT `k`, `v` FROM `economy_meta`";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                meta[reader.GetString(0)] = reader.GetString(1);

            return meta;
        }

        /// <summary>
        /// Writes every value in one transaction, so a failure leaves the stored state as it was and never half
        /// updated. Returns false if nothing was written. Never throws.
        /// </summary>
        private static bool SaveMeta(Dictionary<string, string> meta)
        {
            try
            {
                using var ctx = new ShardDbContext();
                var con = OpenConnection(ctx);
                using var tx = con.BeginTransaction();

                foreach (var kv in meta)
                {
                    using var cmd = NewCommand(con);
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT INTO `economy_meta` (`k`,`v`) VALUES (@k,@v) ON DUPLICATE KEY UPDATE `v`=VALUES(`v`)";
                    AddParam(cmd, "@k", kv.Key);
                    AddParam(cmd, "@v", kv.Value);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[EconomyLedger] SaveMeta failed: {ex.Message}");
                return false;
            }
        }
    }
}
