using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;

using log4net;

using Microsoft.EntityFrameworkCore;

using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Pyreal ledger (owner 2026-10-02): finds pyreals that appear without a reason.
    ///
    /// 1. Every change to BankedPyreals is caught at the property setter (WorldObject / OfflinePlayer SetProperty)
    ///    and rolled up per character, per hour, per source ("VendorSell" + vendor, "Transfer" + other character, ...).
    ///    Callers say where a change comes from with <see cref="Begin"/>; anything else is recorded as "Unattributed".
    /// 2. Currency items (coins, trade notes, peas) entering or leaving a character are rolled up the same way, so a
    ///    character who deposits more notes than they ever received stands out.
    /// 3. Each character's live balance and last SAVED balance are persisted every few seconds. At startup the balance
    ///    loaded from the database is compared with them: equal to live = clean, equal to saved = a crash rolled back
    ///    the unsaved changes (a rolled-back debit whose other side was saved is a likely dupe), neither = the balance
    ///    was changed outside the game.
    ///
    /// Nothing here changes game behavior. Record calls never throw and never touch the database; a background timer
    /// writes batches.
    /// </summary>
    public static class PyrealLedger
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // ---- sources ---------------------------------------------------------------------------------------------

        public const string SrcUnattributed = "Unattributed";
        public const string SrcVendorSell = "VendorSell";
        public const string SrcVendorBuy = "VendorBuy";
        public const string SrcDeposit = "Deposit";
        public const string SrcWithdraw = "Withdraw";
        public const string SrcTransfer = "Transfer";
        public const string SrcEmote = "Emote";
        public const string SrcCommand = "Command";
        public const string SrcGive = "Give";
        public const string SrcGiveNpc = "GiveNPC";
        public const string SrcTrade = "Trade";
        public const string SrcLoot = "Loot";
        public const string SrcPlayerCorpse = "PlayerCorpse";
        public const string SrcGround = "Ground";
        public const string SrcChest = "Chest";
        public const string SrcRecipe = "Recipe";
        public const string SrcKillReward = "KillReward";
        public const string SrcPetDevice = "PetDevice";
        public const string SrcClap = "Clap";
        public const string SrcBankClamp = "BankClamp";
        public const string SrcCopyChar = "CopyChar";
        public const string SrcDeath = "Death";
        /// <summary>Records nothing: an item going back to where it came from after a failed action.</summary>
        public const string SrcIgnore = "(ignore)";

        // ---- flags (pyreal_ledger_flags.flag) ----------------------------------------------------------------------

        /// <summary>A crash rolled back a debit whose other side (an offline transfer recipient, withdrawn notes) was already saved.</summary>
        public const string FlagLikelyDupe = "LikelyDupe";
        /// <summary>A crash rolled back debits: the character kept pyreals they had spent or sent.</summary>
        public const string FlagRollbackGain = "RollbackGain";
        /// <summary>A crash rolled back credits: the character lost pyreals. Informational.</summary>
        public const string FlagRollbackLoss = "RollbackLoss";
        /// <summary>The balance loaded at startup matches neither the last live nor the last saved balance.</summary>
        public const string FlagUnexplained = "Unexplained";
        /// <summary>The balance changed without passing through the ledger (a write that bypassed SetProperty).</summary>
        public const string FlagBypass = "Bypass";
        /// <summary>A currency item was deposited for more than its face value.</summary>
        public const string FlagTampered = "Tampered";
        /// <summary>A bank change with no source scope (an unknown code path); includes a stack trace.</summary>
        public const string FlagUnattributed = "Unattributed";
        /// <summary>@copychar created a character holding a balance.</summary>
        public const string FlagCopyChar = "CopyChar";

        public const byte KindBank = 1;
        public const byte KindItemIn = 2;
        public const byte KindItemOut = 3;
        /// <summary>Any item sold to a vendor: detail_key "itemWcid:vendorWcid", units = stack size, amount_in = payout.</summary>
        public const byte KindSold = 4;

        // ---- currency items ----------------------------------------------------------------------------------------

        private static readonly HashSet<uint> currencyWcids = new HashSet<uint>
        {
            273,                                            // Pyreal
            2621, 2622, 2623, 2624, 2625, 2626, 2627,       // Trade Note (100) .. (100,000)
            20628, 20629, 20630,                            // Trade Note (150,000) .. (250,000)
            8326, 8327, 8328, 8329, 8330, 8331,             // Copper, Gold, Iron, Lead, Pyreal, Silver Pea
        };

        public static bool IsCurrency(uint wcid) => currencyWcids.Contains(wcid);

        private static readonly Dictionary<uint, long> faceValueCache = new Dictionary<uint, long>();

        /// <summary>The weenie's Value for one unit, read from the world database (no hardcoded denominations).</summary>
        public static long FaceValue(uint wcid)
        {
            lock (faceValueCache)
            {
                if (faceValueCache.TryGetValue(wcid, out var cached))
                    return cached;
            }

            long value = 0;
            try
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
                if (weenie?.PropertiesInt != null && weenie.PropertiesInt.TryGetValue(PropertyInt.Value, out var v))
                    value = v;
            }
            catch (Exception ex)
            {
                log.Warn($"[PyrealLedger] FaceValue({wcid}) failed: {ex.Message}");
            }

            lock (faceValueCache)
                faceValueCache[wcid] = value;

            return value;
        }

        // ---- ambient source scope ----------------------------------------------------------------------------------

        internal sealed class SourceContext
        {
            public string Source;
            public string Key;
            public string Name;
            /// <summary>When set, Key and Name come from this object, read only if something is recorded in the scope.</summary>
            public WorldObject Subject;

            public string GetKey() => Key ??= Subject?.WeenieClassId.ToString() ?? "";

            public string GetName()
            {
                if (Name != null)
                    return Name;

                var name = Subject?.Name ?? "";
                if (Subject is Vendor vendor)
                    name = $"{name} (buys at {vendor.BuyPrice ?? 1:0.###}x)";

                return Name = name;
            }
            public uint ActorGuid;
            public string ActorName;
            public bool CounterpartSaved;
            public SourceContext Previous;
        }

        [ThreadStatic]
        private static SourceContext current;

        /// <summary>Restores the previous source when disposed. Scopes nest.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly SourceContext previous;
            private readonly bool active;

            internal Scope(SourceContext previous)
            {
                this.previous = previous;
                active = true;
            }

            public void Dispose()
            {
                if (active)
                    current = previous;
            }
        }

        /// <summary>
        /// Labels every bank and currency-item change made on this thread until disposed.
        /// <paramref name="actor"/> is the character performing the action; for a change to the actor the detail is
        /// <paramref name="key"/>/<paramref name="name"/> (the vendor, the recipient...), and for a change to anyone else
        /// (a transfer recipient) the detail is the actor. <paramref name="counterpartSaved"/> marks an action whose other
        /// side is saved to the database immediately, so losing the actor's debit in a crash would duplicate pyreals.
        /// </summary>
        public static Scope Begin(string source, string key = "", string name = "", WorldObject actor = null, bool counterpartSaved = false)
        {
            if (!ScopesActive)
                return default;

            var ctx = new SourceContext
            {
                Source = source,
                Key = key ?? "",
                Name = name ?? "",
                ActorGuid = actor?.Guid.Full ?? 0,
                ActorName = actor?.Name ?? "",
                CounterpartSaved = counterpartSaved,
                Previous = current,
            };

            var scope = new Scope(current);
            current = ctx;
            return scope;
        }

        /// <summary>
        /// Like <see cref="Begin"/>, labeled with a world object (the NPC running an emote, the vendor bought from). Its
        /// wcid and name are read only if something is recorded inside the scope, so this is cheap on paths that run
        /// constantly and almost never touch currency (every emote action).
        /// </summary>
        public static Scope BeginFor(string source, WorldObject subject, WorldObject actor = null, bool counterpartSaved = false)
        {
            if (!ScopesActive)
                return default;

            var ctx = new SourceContext
            {
                Source = source,
                Subject = subject,
                ActorGuid = actor?.Guid.Full ?? 0,
                ActorName = actor?.Name ?? "",
                CounterpartSaved = counterpartSaved,
                Previous = current,
            };

            var scope = new Scope(current);
            current = ctx;
            return scope;
        }

        /// <summary>Scopes do nothing (no allocation, no property reads) while the ledger is off.</summary>
        private static bool ScopesActive => initialized || ForceScopesForTests;

        /// <summary>Lets unit tests exercise scopes without a running ledger.</summary>
        internal static bool ForceScopesForTests;

        /// <summary>The source label open on this thread, or null. For tests.</summary>
        internal static string CurrentSource => current?.Source;

        /// <summary>Begins a scope only when no other scope is open on this thread (a fallback label such as a chat command).</summary>
        public static Scope BeginIfNone(string source, string key = "", string name = "", WorldObject actor = null)
        {
            if (current != null)
                return default;

            return Begin(source, key, name, actor);
        }

        // ---- state -------------------------------------------------------------------------------------------------

        internal readonly struct PendingNote
        {
            public readonly string Text;
            /// <summary>The balance right after this change; a save that wrote this balance included it.</summary>
            public readonly long After;
            /// <summary>A debit whose other side is saved immediately: losing it in a crash duplicates pyreals.</summary>
            public readonly bool Risk;

            public PendingNote(string text, long after)
            {
                Text = text;
                After = after;
                Risk = text != null && text.StartsWith(DupeRiskMarker);
            }
        }

        /// <summary>Prefix of a pending note (and of the persisted notes text) that marks a dupe risk. See <see cref="Classify"/>.</summary>
        internal const string DupeRiskMarker = "!";

        /// <summary>How many pending notes are written out as text; the rest only keep their balance and risk bit.</summary>
        internal const int MaxNoteTexts = 12;

        /// <summary>Unsaved changes kept per character. A save normally clears them every few minutes.</summary>
        internal const int MaxPendingChanges = 512;

        internal sealed class CharState
        {
            public uint CharId;
            public uint AccountId;
            public string Name;
            public long Live;
            public long Saved;
            public DateTime SavedUtc;
            /// <summary>Changes since the last save, oldest first.</summary>
            public readonly List<PendingNote> PendingNotes = new List<PendingNote>();
            /// <summary>More than <see cref="MaxPendingChanges"/> unsaved changes: the newest were not kept individually.</summary>
            public bool PendingOverflow;
            /// <summary>One of the changes dropped by <see cref="PendingOverflow"/> was a dupe risk.</summary>
            public bool OverflowRisk;
            public bool Dirty;
            /// <summary>Holds an unsaved dupe-risk debit: write the row at the next opportunity, not the next timer tick.</summary>
            public bool Urgent;
            public DateTime LastBypassFlagHour;
            public DateTime LastUnattributedFlagHour;

            public bool HasDupeRisk => OverflowRisk || PendingNotes.Any(n => n.Risk);

            public void AddPending(string text, long after)
            {
                var note = new PendingNote(text, after);

                if (PendingNotes.Count < MaxPendingChanges)
                    PendingNotes.Add(note);
                else
                {
                    PendingOverflow = true;
                    OverflowRisk |= note.Risk;
                }
            }

            /// <summary>
            /// The persisted notes. The dupe-risk signal is written first as its own token, so it survives both the cap
            /// on note texts and the column's length limit.
            /// </summary>
            public string NotesText
            {
                get
                {
                    var text = string.Join("|", PendingNotes.Take(MaxNoteTexts).Select(n => n.Text));
                    if (PendingOverflow || PendingNotes.Count > MaxNoteTexts)
                        text += "|...";

                    return HasDupeRisk ? DupeRiskMarker + "risk|" + text : text;
                }
            }

            /// <summary>
            /// A save wrote <paramref name="savedBalance"/>: drop the notes it included (up to the last note that ended at
            /// that balance). If no note ended there the save happened before any of them, so all stay pending.
            /// Matching is by value: if the balance returns to an earlier value (-50 then +50) a save from before both
            /// changes looks like a save after both, and the notes are dropped. A crash right then is classified Clean.
            /// </summary>
            public void ApplySave(long savedBalance)
            {
                Saved = savedBalance;
                SavedUtc = DateTime.UtcNow;

                if (savedBalance == Live)
                {
                    PendingNotes.Clear();
                    PendingOverflow = false;
                    OverflowRisk = false;
                    Urgent = false;
                    return;
                }

                // changes dropped by the overflow have no balance to match against, so their risk bit stays set until a
                // save of the live balance (above) shows that everything was written
                for (var i = PendingNotes.Count - 1; i >= 0; i--)
                {
                    if (PendingNotes[i].After == savedBalance)
                    {
                        PendingNotes.RemoveRange(0, i + 1);
                        break;
                    }
                }

                Urgent = HasDupeRisk;
            }
        }

        private sealed class Bucket
        {
            public uint AccountId;
            public string CharName;
            public string DetailName;
            public long Events;
            public long AmountIn;
            public long AmountOut;
            public long Units;
        }

        private readonly struct BucketKey : IEquatable<BucketKey>
        {
            public readonly DateTime Hour;
            public readonly uint CharId;
            public readonly byte Kind;
            public readonly string Source;
            public readonly string DetailKey;

            public BucketKey(DateTime hour, uint charId, byte kind, string source, string detailKey)
            {
                Hour = hour;
                CharId = charId;
                Kind = kind;
                Source = source;
                DetailKey = detailKey;
            }

            public bool Equals(BucketKey other) => Hour == other.Hour && CharId == other.CharId && Kind == other.Kind
                && string.Equals(Source, other.Source, StringComparison.Ordinal) && string.Equals(DetailKey, other.DetailKey, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is BucketKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Hour, CharId, Kind, Source, DetailKey);
        }

        private sealed class FlagRow
        {
            public DateTime Utc;
            public uint CharId;
            public uint AccountId;
            public string CharName;
            public string Flag;
            public long Amount;
            public long Expected;
            public long Actual;
            public string Detail;
        }

        private static readonly object sync = new object();
        private static readonly Dictionary<uint, CharState> states = new Dictionary<uint, CharState>();
        private static Dictionary<BucketKey, Bucket> buckets = new Dictionary<BucketKey, Bucket>();
        private static List<FlagRow> pendingFlags = new List<FlagRow>();

        private static volatile bool initialized;
        private static Timer flushTimer;
        private static int flushRunning;
        private static DateTime lastBucketFlush = DateTime.MinValue;

        private const int FlushIntervalMs = 5000;
        private const int BucketFlushSeconds = 60;
        private const int MaxDetail = 1000;

        public static bool IsInitialized => initialized;

        public static DateTime? LedgerStartUtc { get; private set; }

        private static DateTime HourOf(DateTime utc) => new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);

        private static string Truncate(string s, int max) => s == null ? "" : (s.Length <= max ? s : s.Substring(0, max));

        private static uint AccountIdOf(WorldObject wo)
        {
            if (wo is Player p)
                return p.Account?.AccountId ?? 0;
            return 0;
        }

        private static CharState GetOrCreateState(uint charId, uint accountId, string name, long balanceIfNew)
        {
            if (!states.TryGetValue(charId, out var state))
            {
                state = new CharState { CharId = charId, AccountId = accountId, Name = name, Live = balanceIfNew, Saved = balanceIfNew, SavedUtc = DateTime.UtcNow, Dirty = true };
                states[charId] = state;
            }
            else
            {
                if (accountId != 0)
                    state.AccountId = accountId;
                if (!string.IsNullOrEmpty(name))
                    state.Name = name;
            }

            return state;
        }

        private static void AddToBucket(DateTime now, uint charId, uint accountId, string charName, byte kind, string source, string detailKey, string detailName, long amountIn, long amountOut, long units)
        {
            var key = new BucketKey(HourOf(now), charId, kind, source, Truncate(detailKey, 64));
            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = new Bucket { AccountId = accountId, CharName = charName, DetailName = Truncate(detailName, 128) };
                buckets[key] = bucket;
            }

            bucket.Events++;
            bucket.AmountIn += amountIn;
            bucket.AmountOut += amountOut;
            bucket.Units += units;
        }

        private static void AddFlag(uint charId, uint accountId, string charName, string flag, long amount, long expected, long actual, string detail)
        {
            pendingFlags.Add(new FlagRow
            {
                Utc = DateTime.UtcNow,
                CharId = charId,
                AccountId = accountId,
                CharName = charName ?? "",
                Flag = flag,
                Amount = amount,
                Expected = expected,
                Actual = actual,
                Detail = Truncate(detail, MaxDetail),
            });
        }

        // ---- hooks -------------------------------------------------------------------------------------------------

        /// <summary>
        /// True when a write of <paramref name="property"/> must go through <see cref="TrackedWrite"/>. One enum compare
        /// first: this sits on the SetProperty(PropertyInt64) path that luminance and XP use on every kill.
        /// </summary>
        public static bool Tracks(PropertyInt64 property) => property == PropertyInt64.BankedPyreals && initialized;

        /// <summary>
        /// Performs a BankedPyreals write for a player and records it. The read of the old value, the write and the record
        /// happen under one lock, so overlapping writes are recorded in the order they were applied. <paramref name="write"/>
        /// always runs, even if recording fails. Lock order is ledger lock, then biota lock; nothing takes them the other way.
        /// </summary>
        public static void TrackedWrite(uint charId, string name, uint accountId, long newValue, Func<long> read, Action write)
        {
            var ctx = current;

            lock (sync)
            {
                long oldValue;
                try
                {
                    oldValue = read();
                }
                catch (Exception ex)
                {
                    log.Error($"[PyrealLedger] read failed for {name}: {ex.Message}");
                    write();
                    return;
                }

                write();

                if (oldValue == newValue)
                    return;

                try
                {
                    RecordBankChangeLocked(ctx, charId, name, accountId, oldValue, newValue);
                }
                catch (Exception ex)
                {
                    log.Error($"[PyrealLedger] record failed for {name}: {ex.Message}");
                }
            }
        }

        /// <summary>Overload for online players and other WorldObjects.</summary>
        public static void TrackedWrite(WorldObject owner, long newValue, Func<long> read, Action write)
        {
            if (owner is not Player)
            {
                write();
                return;
            }

            TrackedWrite(owner.Guid.Full, owner.Name, AccountIdOf(owner), newValue, read, write);
        }

        private static void RecordBankChangeLocked(SourceContext ctx, uint charId, string name, uint accountId, long oldValue, long newValue)
        {
            var delta = newValue - oldValue;
            var source = ctx?.Source ?? SrcUnattributed;
            var isActor = ctx == null || ctx.ActorGuid == 0 || ctx.ActorGuid == charId;
            var detailKey = ctx == null ? "" : (isActor ? ctx.GetKey() : ctx.ActorGuid.ToString());
            var detailName = ctx == null ? "" : (isActor ? ctx.GetName() : ctx.ActorName);
            var now = DateTime.UtcNow;

            var state = GetOrCreateState(charId, accountId, name, oldValue);

            if (state.Live != oldValue)
            {
                // reads and writes are serialized under this lock, so this only happens when the balance was changed
                // by something that bypassed SetProperty
                var hour = HourOf(now);
                if (state.LastBypassFlagHour != hour)
                {
                    state.LastBypassFlagHour = hour;
                    AddFlag(charId, state.AccountId, name, FlagBypass, oldValue - state.Live, state.Live, oldValue,
                        $"Balance was {oldValue:N0} before a {source} change but the ledger last saw {state.Live:N0}");
                }
            }

            state.Live = newValue;
            state.Dirty = true;

            var risk = isActor && delta < 0 && ctx != null && ctx.CounterpartSaved;
            state.AddPending($"{(risk ? DupeRiskMarker : "")}{source}:{(string.IsNullOrEmpty(detailName) ? detailKey : detailName)}:{delta}", newValue);

            if (risk)
            {
                // The other side of this debit is already saved. If the server dies before this row is written, the
                // restart check has nothing to compare against and the dupe goes unseen, so write it a moment from
                // now instead of at the next 5 second tick.
                state.Urgent = true;
                NudgeFlush();
            }

            AddToBucket(now, charId, state.AccountId, name, KindBank, source, detailKey, detailName, Math.Max(0, delta), Math.Max(0, -delta), 0);

            if (ctx == null)
            {
                // a change nobody labeled: once per character per hour, store the code path so developers can label it
                var hour = HourOf(now);
                if (state.LastUnattributedFlagHour != hour)
                {
                    state.LastUnattributedFlagHour = hour;
                    AddFlag(charId, state.AccountId, name, FlagUnattributed, delta, oldValue, newValue, TrimTrace(Environment.StackTrace));
                }
            }
        }

        private static string TrimTrace(string trace)
        {
            // keep only ACE frames, minus the ledger itself
            var lines = trace.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.StartsWith("at ACE.") && !l.Contains("PyrealLedger") && !l.Contains("SetProperty") && !l.Contains("RemoveProperty") && !l.Contains("set_BankedPyreals"))
                .Select(l => { var i = l.IndexOf(" in "); return i > 0 ? l.Substring(0, i) : l; })
                .Take(8);

            return string.Join(" <- ", lines);
        }

        /// <summary>
        /// A currency item stack entered (<paramref name="incoming"/>) or left a character's possession.
        /// The amount is valued at face value, so a tampered item does not inflate the flow.
        /// </summary>
        public static void OnCurrencyItem(Player player, uint wcid, long units, bool incoming, string source = null, string detailKey = null, string detailName = null)
        {
            if (!initialized || player == null || units <= 0 || !IsCurrency(wcid))
                return;

            try
            {
                var ctx = current;
                var src = source ?? ctx?.Source ?? SrcUnattributed;
                if (src == SrcIgnore)
                    return;

                if (source == null && ctx != null)
                {
                    var isActor = ctx.ActorGuid == 0 || ctx.ActorGuid == player.Guid.Full;
                    detailKey ??= isActor ? ctx.GetKey() : ctx.ActorGuid.ToString();
                    detailName ??= isActor ? ctx.GetName() : ctx.ActorName;
                }

                var value = FaceValue(wcid) * units;

                lock (sync)
                {
                    AddToBucket(DateTime.UtcNow, player.Guid.Full, player.Account?.AccountId ?? 0, player.Name, incoming ? KindItemIn : KindItemOut,
                        src, $"{wcid}:{detailKey ?? ""}", detailName ?? "", incoming ? value : 0, incoming ? 0 : value, units);
                }
            }
            catch (Exception ex)
            {
                log.Error($"[PyrealLedger] OnCurrencyItem failed: {ex.Message}");
            }
        }

        public static void OnCurrencyItem(Player player, WorldObject item, long units, bool incoming, string source = null, string detailKey = null, string detailName = null)
        {
            if (item == null)
                return;

            OnCurrencyItem(player, item.WeenieClassId, units, incoming, source, detailKey, detailName);
        }

        /// <summary>
        /// A currency item is about to be deposited (credited at item.Value). Records the item leaving and flags it when
        /// its Value is above face value for its stack size.
        /// </summary>
        public static void OnDepositItem(Player player, WorldObject item, long credited)
        {
            if (!initialized || player == null || item == null)
                return;

            var units = (long)(item.StackSize ?? 1);
            OnCurrencyItem(player, item.WeenieClassId, units, false, SrcDeposit, "", "");

            try
            {
                var face = FaceValue(item.WeenieClassId) * units;
                if (face > 0 && credited > face)
                {
                    lock (sync)
                        AddFlag(player.Guid.Full, player.Account?.AccountId ?? 0, player.Name, FlagTampered, credited - face, face, credited,
                            $"{item.Name} (wcid {item.WeenieClassId}, 0x{item.Guid.Full:X8}) x{units} deposited for {credited:N0}, face value {face:N0}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"[PyrealLedger] OnDepositItem failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Items sold to a vendor, one row per item type, vendor, character and hour. This is where duplicated stacks turn
        /// into pyreals, so the page can show who sold how many of what.
        /// </summary>
        public static void OnItemsSold(Player player, Vendor vendor, IReadOnlyCollection<WorldObject> items)
        {
            if (!initialized || player == null || vendor == null || items == null || items.Count == 0)
                return;

            try
            {
                var sales = new List<(uint wcid, long units, long payout)>(items.Count);
                foreach (var item in items)
                {
                    if (item == null)
                        continue;

                    sales.Add((item.WeenieClassId, item.StackSize ?? 1, vendor.GetBuyCost(item)));

                    // coins, notes and peas sold to a vendor also leave the character as currency
                    OnCurrencyItem(player, item.WeenieClassId, item.StackSize ?? 1, false);
                }

                var now = DateTime.UtcNow;
                var vendorKey = vendor.WeenieClassId.ToString();
                var accountId = player.Account?.AccountId ?? 0;

                lock (sync)
                {
                    foreach (var (wcid, units, payout) in sales)
                        AddToBucket(now, player.Guid.Full, accountId, player.Name, KindSold, SrcVendorSell, $"{wcid}:{vendorKey}", vendor.Name, payout, 0, units);
                }
            }
            catch (Exception ex)
            {
                log.Error($"[PyrealLedger] OnItemsSold failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Moves between a player and the world (pickup, drop, chest, corpse). Records currency items crossing the boundary
        /// of <paramref name="player"/>'s possessions.
        /// </summary>
        public static void OnItemMoved(Player player, WorldObject item, long units, Container fromRoot, Container toRoot)
        {
            if (!initialized || player == null || item == null)
                return;

            try
            {
                if (IsCurrency(item.WeenieClassId))
                    RecordItemMoved(player, item, units, fromRoot, toRoot);
            }
            catch (Exception ex)
            {
                log.Error($"[PyrealLedger] OnItemMoved failed: {ex.Message}");
            }
        }

        private static void RecordItemMoved(Player player, WorldObject item, long units, Container fromRoot, Container toRoot)
        {
            var fromMe = fromRoot == player;
            var toMe = toRoot == player;
            if (fromMe == toMe)
                return;

            var other = fromMe ? toRoot : fromRoot;
            string source;
            string key = "";
            string name = "";

            if (other == null)
                source = SrcGround;
            else if (other is Corpse corpse)
            {
                var playerCorpse = corpse.VictimId.HasValue && new ObjectGuid(corpse.VictimId.Value).IsPlayer();
                source = playerCorpse ? SrcPlayerCorpse : SrcLoot;
                key = playerCorpse ? corpse.VictimId.Value.ToString() : "";
                name = corpse.Name;
            }
            else if (other is Player otherPlayer)
            {
                source = SrcGive;
                key = otherPlayer.Guid.Full.ToString();
                name = otherPlayer.Name;
            }
            else
            {
                source = SrcChest;
                key = other.WeenieClassId.ToString();
                name = other.Name;
            }

            OnCurrencyItem(player, item.WeenieClassId, units, toMe, source, key, name);
        }

        /// <summary>A save of a biota holding BankedPyreals completed; <paramref name="savedBalance"/> is the value written.</summary>
        public static void OnBiotaSaved(uint biotaId, long savedBalance)
        {
            if (!initialized)
                return;

            try
            {
                lock (sync)
                {
                    if (!states.TryGetValue(biotaId, out var state))
                    {
                        // not touched since startup and not loaded at startup (a character created since)
                        state = GetOrCreateState(biotaId, 0, null, savedBalance);
                    }

                    if (state.Saved == savedBalance && state.PendingNotes.Count == 0)
                        return;

                    state.ApplySave(savedBalance);
                    state.Dirty = true;
                }

                // This runs on the shard save thread, so it must not do database work of its own: a slow ledger write
                // would hold up every save queued behind it. Ask the flush timer to write the row a moment from now
                // instead. If the server dies inside that moment the database holds the new balance while the row
                // still has the old one, which the next startup reports as Unexplained with a "not clean" note.
                NudgeFlush();
            }
            catch (Exception ex)
            {
                log.Error($"[PyrealLedger] OnBiotaSaved failed: {ex.Message}");
            }
        }

        /// <summary>@copychar wrote a new character biota directly; give it a baseline and a flag.</summary>
        public static void OnCharacterCopied(uint newCharId, string newName, uint accountId, long balance, string sourceName, string adminName)
        {
            if (!initialized)
                return;

            try
            {
                lock (sync)
                {
                    var state = GetOrCreateState(newCharId, accountId, newName, balance);
                    state.Live = balance;
                    state.Saved = balance;
                    state.Dirty = true;

                    if (balance != 0)
                    {
                        AddFlag(newCharId, accountId, newName, FlagCopyChar, balance, 0, balance, $"Copied from {sourceName} by {adminName}");
                        AddToBucket(DateTime.UtcNow, newCharId, accountId, newName, KindBank, SrcCopyChar, "", sourceName, Math.Max(0, balance), Math.Max(0, -balance), 0);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"[PyrealLedger] OnCharacterCopied failed: {ex.Message}");
            }
        }

        // ---- startup reconciliation --------------------------------------------------------------------------------

        public enum Classification
        {
            Clean,
            RollbackGain,
            RollbackLoss,
            LikelyDupe,
            Unexplained,
        }

        /// <summary>
        /// Classifies a balance loaded from the database against the persisted live and saved balances.
        /// Pure; unit tested.
        /// </summary>
        public static Classification Classify(long loaded, long live, long saved, string pendingNotes)
        {
            if (loaded == live)
                return Classification.Clean;

            if (loaded == saved)
            {
                // the changes after the last save were lost: live - saved is what was lost
                if (live < saved)
                    return (pendingNotes ?? "").Split('|').Any(n => n.StartsWith(DupeRiskMarker)) ? Classification.LikelyDupe : Classification.RollbackGain;

                return Classification.RollbackLoss;
            }

            return Classification.Unexplained;
        }

        private sealed class PersistedState
        {
            public long Live;
            public long Saved;
            public string Notes;
        }

        /// <summary>
        /// Creates tables, compares every character's loaded balance with the persisted state, flags differences and
        /// starts the flush timer. Call after PlayerManager.Initialize and before the world starts.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                EnsureTables();

                var persisted = LoadStates(out var meta);
                // a baseline is complete only once every character's row was written; until then, write the baseline
                // again rather than compare against missing rows (which would flag every balance as Unexplained)
                var firstRun = !meta.ContainsKey("baseline_complete");
                var previousShutdownClean = meta.TryGetValue("clean_shutdown", out var cs) && cs == "1";

                LedgerStartUtc = !meta.ContainsKey("started_utc") ? DateTime.UtcNow : (DateTime.TryParse(meta["started_utc"], null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var started) ? started : DateTime.UtcNow);

                var counts = new Dictionary<Classification, int>();
                var players = PlayerManager.GetAllOffline();

                lock (sync)
                {
                    foreach (var player in players)
                    {
                        var charId = player.Guid.Full;
                        var loaded = player.GetProperty(PropertyInt64.BankedPyreals) ?? 0;
                        var accountId = player.Account?.AccountId ?? 0;

                        var state = new CharState { CharId = charId, AccountId = accountId, Name = player.Name, Live = loaded, Saved = loaded, SavedUtc = DateTime.UtcNow };
                        states[charId] = state;

                        if (firstRun)
                        {
                            state.Dirty = loaded != 0;
                            continue;
                        }

                        if (!persisted.TryGetValue(charId, out var row))
                            row = new PersistedState { Live = 0, Saved = 0, Notes = "" };

                        var result = Classify(loaded, row.Live, row.Saved, row.Notes);
                        counts[result] = counts.TryGetValue(result, out var c) ? c + 1 : 1;

                        state.Dirty = !persisted.ContainsKey(charId) ? loaded != 0 : (row.Live != loaded || row.Saved != loaded || !string.IsNullOrEmpty(row.Notes));

                        if (result == Classification.Clean)
                            continue;

                        var crashNote = previousShutdownClean ? "previous shutdown was clean" : "previous shutdown was NOT clean (crash or kill)";
                        switch (result)
                        {
                            case Classification.LikelyDupe:
                            case Classification.RollbackGain:
                                AddFlag(charId, accountId, player.Name, result == Classification.LikelyDupe ? FlagLikelyDupe : FlagRollbackGain, loaded - row.Live, row.Live, loaded,
                                    $"Unsaved changes lost at restart ({crashNote}). Lost: {row.Notes}");
                                break;
                            case Classification.RollbackLoss:
                                AddFlag(charId, accountId, player.Name, FlagRollbackLoss, loaded - row.Live, row.Live, loaded,
                                    $"Unsaved credits lost at restart ({crashNote}). Lost: {row.Notes}");
                                break;
                            case Classification.Unexplained:
                                AddFlag(charId, accountId, player.Name, FlagUnexplained, loaded - row.Live, row.Live, loaded,
                                    $"Balance changed outside the game: last live {row.Live:N0}, last saved {row.Saved:N0}, loaded {loaded:N0} ({crashNote})");
                                break;
                        }
                    }
                }

                meta["clean_shutdown"] = "0";
                if (!meta.ContainsKey("started_utc"))
                    meta["started_utc"] = LedgerStartUtc.Value.ToString("o");
                SaveMeta(meta);

                ShardDatabase.BankedPyrealsSaved = OnBiotaSaved;
                initialized = true;

                var statesWritten = FlushStates();
                FlushFlags();

                if (firstRun)
                {
                    if (statesWritten)
                        SaveMeta(new Dictionary<string, string> { ["baseline_complete"] = "1" });
                    else
                        log.Warn("[PyrealLedger] Baseline rows were not all written; the next startup will write the baseline again instead of comparing balances.");
                }

                flushTimer = new Timer(_ => TimerTick(), null, FlushIntervalMs, FlushIntervalMs);

                var summary = string.Join(", ", counts.Select(kv => $"{kv.Key}={kv.Value}"));
                log.Info($"[PyrealLedger] Initialized: {players.Count} characters, {(firstRun ? "first run (baseline written)" : summary)}, previous shutdown {(previousShutdownClean ? "clean" : "not clean")}");
            }
            catch (Exception ex)
            {
                log.Error($"[PyrealLedger] Initialize failed, ledger disabled: {ex}");
                initialized = false;
                ShardDatabase.BankedPyrealsSaved = null;
            }
        }

        /// <summary>
        /// Call at startup when the ledger is switched off. Balances change unrecorded while it is off, so the stored
        /// rows would be stale: forget the baseline, and the next enabled start writes a fresh one instead of flagging
        /// every changed balance as Unexplained. Does nothing if the ledger has never run.
        /// </summary>
        public static void InvalidateBaseline()
        {
            try
            {
                using var ctx = new ShardDbContext();
                var con = OpenConnection(ctx);

                using (var check = NewCommand(con))
                {
                    check.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'pyreal_ledger_meta'";
                    if (Convert.ToInt64(check.ExecuteScalar()) == 0)
                        return;
                }

                using var cmd = NewCommand(con);
                cmd.CommandText = "DELETE FROM `pyreal_ledger_meta` WHERE `k` = 'baseline_complete'";
                if (cmd.ExecuteNonQuery() > 0)
                    log.Info("[PyrealLedger] Ledger is off: baseline cleared, the next enabled start will write a new one.");
            }
            catch (Exception ex)
            {
                log.Error($"[PyrealLedger] InvalidateBaseline failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Switches the ledger off while the server runs (@pyrealledger off): final flush, then every hook returns at
        /// its first line and BankedPyreals writes take the original path again. The baseline is cleared because
        /// balances change unrecorded from here on. It cannot be switched back on without a restart.
        /// </summary>
        public static bool StopLive()
        {
            if (!initialized)
                return false;

            Shutdown();
            InvalidateBaseline();
            log.Warn("[PyrealLedger] Switched off by an admin. Recording resumes at the next server start if ServerConfig pyreal_ledger is true.");
            return true;
        }

        /// <summary>
        /// Final flush. Call after the shard save queue has drained. Returns false if any final write failed; the
        /// shutdown marker is then left at "not clean", so the next startup's flags say the stored rows may be behind.
        /// The result is informational: the ledger never holds up or changes a server shutdown.
        /// </summary>
        public static bool Shutdown()
        {
            if (!initialized)
                return true;

            var allWritten = false;

            try
            {
                flushTimer?.Dispose();
                flushTimer = null;

                // wait for an in-flight timer flush
                var waited = 0;
                while (Interlocked.CompareExchange(ref flushRunning, 1, 0) != 0 && waited < 10000)
                {
                    Thread.Sleep(50);
                    waited += 50;
                }

                // run all three even if one fails
                var bucketsWritten = FlushBuckets(force: true);
                var statesWritten = FlushStates();
                var flagsWritten = FlushFlags();

                if (bucketsWritten && statesWritten && flagsWritten)
                {
                    SaveMeta(new Dictionary<string, string> { ["clean_shutdown"] = "1" });
                    allWritten = true;
                    log.Info("[PyrealLedger] Final flush complete");
                }
                else
                    log.Warn($"[PyrealLedger] Final flush incomplete (hourly rows {(bucketsWritten ? "ok" : "FAILED")}, balances {(statesWritten ? "ok" : "FAILED")}, flags {(flagsWritten ? "ok" : "FAILED")}). " +
                             "The shutdown stays marked not clean, so the next startup notes that on anything it flags.");
            }
            catch (Exception ex)
            {
                log.Error($"[PyrealLedger] Shutdown flush failed: {ex}");
            }
            finally
            {
                initialized = false;
                ShardDatabase.BankedPyrealsSaved = null;
                Interlocked.Exchange(ref flushRunning, 0);
            }

            return allWritten;
        }

        private static void TimerTick()
        {
            if (Interlocked.CompareExchange(ref flushRunning, 1, 0) != 0)
                return;

            // cleared before the flush reads the rows: a nudge that arrives during the flush schedules another one
            Interlocked.Exchange(ref nudgePending, 0);

            try
            {
                FlushStates();
                FlushFlags();

                if ((DateTime.UtcNow - lastBucketFlush).TotalSeconds >= BucketFlushSeconds)
                    FlushBuckets(force: false);

                if ((DateTime.UtcNow - lastCleanup).TotalHours >= CleanupIntervalHours)
                {
                    lastCleanup = DateTime.UtcNow;
                    StartCleanup();
                }
            }
            catch (Exception ex)
            {
                log.Error($"[PyrealLedger] Flush failed: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref flushRunning, 0);
            }
        }

        // ---- retention ---------------------------------------------------------------------------------------------

        private const int CleanupIntervalHours = 6;
        private const int CleanupBatchRows = 5000;
        private static DateTime lastCleanup = DateTime.MinValue;
        private static int cleanupRunning;

        /// <summary>
        /// Deletes hourly rows and flags older than ServerConfig pyreal_ledger_retention_days on a pool thread, so the
        /// flush timer is not held up. State rows (one per character) and the meta table are kept.
        /// </summary>
        private static void StartCleanup()
        {
            var days = ServerConfig.pyreal_ledger_retention_days.Value;
            if (days <= 0)
                return;

            if (Interlocked.CompareExchange(ref cleanupRunning, 1, 0) != 0)
                return;

            var cutoff = DateTime.UtcNow.AddDays(-days);

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var hourly = DeleteOlderThan("pyreal_ledger_hourly", "hour_utc", cutoff);
                    var flags = DeleteOlderThan("pyreal_ledger_flags", "utc", cutoff);

                    if (hourly > 0 || flags > 0)
                        log.Info($"[PyrealLedger] Retention ({days} days): deleted {hourly:N0} hourly rows and {flags:N0} flags older than {cutoff:yyyy-MM-dd}");
                }
                catch (Exception ex)
                {
                    log.Error($"[PyrealLedger] Retention cleanup failed: {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref cleanupRunning, 0);
                }
            });
        }

        /// <summary>Deletes in batches so no single statement holds locks on the table for long.</summary>
        private static long DeleteOlderThan(string table, string column, DateTime cutoff)
        {
            long total = 0;

            using var ctx = new ShardDbContext();
            var con = OpenConnection(ctx);

            while (true)
            {
                using var cmd = NewCommand(con);
                cmd.CommandText = $"DELETE FROM `{table}` WHERE `{column}` < @cutoff LIMIT {CleanupBatchRows}";
                cmd.CommandTimeout = 30;
                AddParam(cmd, "@cutoff", cutoff);

                var deleted = cmd.ExecuteNonQuery();
                total += deleted;

                if (deleted < CleanupBatchRows)
                    return total;

                Thread.Sleep(200);
            }
        }

        // ---- database ----------------------------------------------------------------------------------------------

        private static DbConnection OpenConnection(ShardDbContext ctx)
        {
            var con = ctx.Database.GetDbConnection();
            if (con.State != ConnectionState.Open)
                con.Open();
            return con;
        }

        private const int CommandTimeoutSeconds = 10;

        /// <summary>A command with a short timeout, so a locked or slow ledger table cannot hold a thread for long.</summary>
        private static DbCommand NewCommand(DbConnection con)
        {
            var cmd = con.CreateCommand();
            cmd.CommandTimeout = CommandTimeoutSeconds;
            return cmd;
        }

        private static void AddParam(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private static void Execute(DbConnection con, string sql)
        {
            using var cmd = NewCommand(con);
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private static void EnsureTables()
        {
            using var ctx = new ShardDbContext();
            var con = OpenConnection(ctx);

            Execute(con, @"
CREATE TABLE IF NOT EXISTS `pyreal_ledger_hourly` (
  `hour_utc`     datetime         NOT NULL,
  `char_id`      int unsigned     NOT NULL,
  `kind`         tinyint unsigned NOT NULL,
  `source`       varchar(32)      NOT NULL,
  `detail_key`   varchar(64)      NOT NULL,
  `account_id`   int unsigned     NOT NULL DEFAULT 0,
  `char_name`    varchar(64)      NOT NULL DEFAULT '',
  `detail_name`  varchar(128)     NOT NULL DEFAULT '',
  `events`       bigint           NOT NULL DEFAULT 0,
  `amount_in`    bigint           NOT NULL DEFAULT 0,
  `amount_out`   bigint           NOT NULL DEFAULT 0,
  `units`        bigint           NOT NULL DEFAULT 0,
  PRIMARY KEY (`hour_utc`, `char_id`, `kind`, `source`, `detail_key`),
  KEY `ix_char_kind_hour` (`char_id`, `kind`, `hour_utc`),
  KEY `ix_account_kind_hour` (`account_id`, `kind`, `hour_utc`),
  KEY `ix_kind_source_hour` (`kind`, `source`, `hour_utc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");

            Execute(con, @"
CREATE TABLE IF NOT EXISTS `pyreal_ledger_state` (
  `char_id`        int unsigned  NOT NULL,
  `account_id`     int unsigned  NOT NULL DEFAULT 0,
  `char_name`      varchar(64)   NOT NULL DEFAULT '',
  `live_balance`   bigint        NOT NULL DEFAULT 0,
  `saved_balance`  bigint        NOT NULL DEFAULT 0,
  `saved_utc`      datetime      NULL,
  `pending_notes`  varchar(1000) NOT NULL DEFAULT '',
  `updated_utc`    datetime      NOT NULL,
  PRIMARY KEY (`char_id`),
  KEY `ix_account` (`account_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");

            Execute(con, @"
CREATE TABLE IF NOT EXISTS `pyreal_ledger_flags` (
  `id`          bigint        NOT NULL AUTO_INCREMENT,
  `utc`         datetime      NOT NULL,
  `char_id`     int unsigned  NOT NULL,
  `account_id`  int unsigned  NOT NULL DEFAULT 0,
  `char_name`   varchar(64)   NOT NULL DEFAULT '',
  `flag`        varchar(32)   NOT NULL,
  `amount`      bigint        NOT NULL DEFAULT 0,
  `expected`    bigint        NOT NULL DEFAULT 0,
  `actual`      bigint        NOT NULL DEFAULT 0,
  `detail`      varchar(1000) NOT NULL DEFAULT '',
  PRIMARY KEY (`id`),
  KEY `ix_utc` (`utc`),
  KEY `ix_char` (`char_id`, `utc`),
  KEY `ix_account` (`account_id`, `utc`),
  KEY `ix_flag` (`flag`, `utc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");

            Execute(con, @"
CREATE TABLE IF NOT EXISTS `pyreal_ledger_meta` (
  `k` varchar(64)  NOT NULL,
  `v` varchar(255) NOT NULL,
  PRIMARY KEY (`k`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
        }

        private static Dictionary<uint, PersistedState> LoadStates(out Dictionary<string, string> meta)
        {
            var result = new Dictionary<uint, PersistedState>();
            meta = new Dictionary<string, string>();

            using var ctx = new ShardDbContext();
            var con = OpenConnection(ctx);

            using (var cmd = NewCommand(con))
            {
                cmd.CommandText = "SELECT `k`, `v` FROM `pyreal_ledger_meta`";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    meta[reader.GetString(0)] = reader.GetString(1);
            }

            using (var cmd = NewCommand(con))
            {
                cmd.CommandText = "SELECT `char_id`, `live_balance`, `saved_balance`, `pending_notes` FROM `pyreal_ledger_state`";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    result[Convert.ToUInt32(reader.GetValue(0))] = new PersistedState { Live = reader.GetInt64(1), Saved = reader.GetInt64(2), Notes = reader.GetString(3) };
            }

            return result;
        }

        private static void SaveMeta(Dictionary<string, string> meta)
        {
            using var ctx = new ShardDbContext();
            var con = OpenConnection(ctx);

            foreach (var kv in meta)
            {
                using var cmd = NewCommand(con);
                cmd.CommandText = "INSERT INTO `pyreal_ledger_meta` (`k`, `v`) VALUES (@k, @v) ON DUPLICATE KEY UPDATE `v` = VALUES(`v`)";
                AddParam(cmd, "@k", kv.Key);
                AddParam(cmd, "@v", kv.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private const int BatchRows = 250;

        /// <summary>Serializes state row writes so a later snapshot is never overwritten by an earlier one.</summary>
        private static readonly object stateWriteLock = new object();

        private const int UrgentFlushDelayMs = 250;
        private static int nudgePending;

        /// <summary>
        /// Brings the next timer flush forward. Only the first nudge after a flush moves the timer, so a steady stream
        /// of nudges cannot keep pushing the flush back. Never throws, never blocks.
        /// </summary>
        private static void NudgeFlush()
        {
            if (Interlocked.CompareExchange(ref nudgePending, 1, 0) != 0)
                return;

            try
            {
                flushTimer?.Change(UrgentFlushDelayMs, FlushIntervalMs);
            }
            catch (Exception)
            {
                // shutting down; Shutdown() does the final flush
            }
        }

        /// <summary>Writes every dirty state row. Returns false if the write failed.</summary>
        private static bool FlushStates()
        {
            lock (stateWriteLock)
                return FlushStatesSerialized();
        }

        private static bool FlushStatesSerialized()
        {
            List<(uint id, uint acct, string name, long live, long saved, DateTime savedUtc, string notes)> rows;

            lock (sync)
            {
                rows = new List<(uint, uint, string, long, long, DateTime, string)>();

                foreach (var s in states.Values)
                {
                    if (!s.Dirty)
                        continue;
                    s.Dirty = false;
                    rows.Add((s.CharId, s.AccountId, s.Name ?? "", s.Live, s.Saved, s.SavedUtc, Truncate(s.NotesText, MaxDetail)));
                }
            }

            if (rows.Count == 0)
                return true;

            try
            {
                using var ctx = new ShardDbContext();
                var con = OpenConnection(ctx);
                var now = DateTime.UtcNow;

                foreach (var batch in rows.Chunk(BatchRows))
                {
                    using var cmd = NewCommand(con);
                    var sb = new StringBuilder("INSERT INTO `pyreal_ledger_state` (`char_id`,`account_id`,`char_name`,`live_balance`,`saved_balance`,`saved_utc`,`pending_notes`,`updated_utc`) VALUES ");
                    for (var i = 0; i < batch.Length; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append($"(@c{i},@a{i},@n{i},@l{i},@s{i},@su{i},@p{i},@u)");
                        AddParam(cmd, $"@c{i}", batch[i].id);
                        AddParam(cmd, $"@a{i}", batch[i].acct);
                        AddParam(cmd, $"@n{i}", Truncate(batch[i].name, 64));
                        AddParam(cmd, $"@l{i}", batch[i].live);
                        AddParam(cmd, $"@s{i}", batch[i].saved);
                        AddParam(cmd, $"@su{i}", batch[i].savedUtc);
                        AddParam(cmd, $"@p{i}", batch[i].notes);
                    }
                    AddParam(cmd, "@u", now);
                    sb.Append(" ON DUPLICATE KEY UPDATE `account_id`=VALUES(`account_id`),`char_name`=VALUES(`char_name`),`live_balance`=VALUES(`live_balance`)," +
                              "`saved_balance`=VALUES(`saved_balance`),`saved_utc`=VALUES(`saved_utc`),`pending_notes`=VALUES(`pending_notes`),`updated_utc`=VALUES(`updated_utc`)");
                    cmd.CommandText = sb.ToString();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // mark them dirty again so the next tick retries
                lock (sync)
                {
                    foreach (var r in rows)
                        if (states.TryGetValue(r.id, out var s))
                            s.Dirty = true;
                }
                log.Error($"[PyrealLedger] FlushStates failed ({rows.Count} rows), will retry: {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>Returns false if the write failed (the rows are kept for the next flush).</summary>
        private static bool FlushBuckets(bool force)
        {
            Dictionary<BucketKey, Bucket> toWrite;

            lock (sync)
            {
                if (buckets.Count == 0)
                {
                    lastBucketFlush = DateTime.UtcNow;
                    return true;
                }
                toWrite = buckets;
                buckets = new Dictionary<BucketKey, Bucket>();
            }

            try
            {
                using var ctx = new ShardDbContext();
                var con = OpenConnection(ctx);

                // one transaction for every chunk: a failure rolls all of them back, so the retry below cannot add
                // an already written chunk a second time
                using var tx = con.BeginTransaction();

                foreach (var batch in toWrite.Chunk(BatchRows))
                {
                    using var cmd = NewCommand(con);
                    cmd.Transaction = tx;
                    var sb = new StringBuilder("INSERT INTO `pyreal_ledger_hourly` (`hour_utc`,`char_id`,`kind`,`source`,`detail_key`,`account_id`,`char_name`,`detail_name`,`events`,`amount_in`,`amount_out`,`units`) VALUES ");
                    for (var i = 0; i < batch.Length; i++)
                    {
                        var (k, b) = (batch[i].Key, batch[i].Value);
                        if (i > 0) sb.Append(',');
                        sb.Append($"(@h{i},@c{i},@k{i},@s{i},@d{i},@a{i},@n{i},@dn{i},@e{i},@i{i},@o{i},@un{i})");
                        AddParam(cmd, $"@h{i}", k.Hour);
                        AddParam(cmd, $"@c{i}", k.CharId);
                        AddParam(cmd, $"@k{i}", k.Kind);
                        AddParam(cmd, $"@s{i}", k.Source);
                        AddParam(cmd, $"@d{i}", k.DetailKey ?? "");
                        AddParam(cmd, $"@a{i}", b.AccountId);
                        AddParam(cmd, $"@n{i}", Truncate(b.CharName, 64));
                        AddParam(cmd, $"@dn{i}", b.DetailName ?? "");
                        AddParam(cmd, $"@e{i}", b.Events);
                        AddParam(cmd, $"@i{i}", b.AmountIn);
                        AddParam(cmd, $"@o{i}", b.AmountOut);
                        AddParam(cmd, $"@un{i}", b.Units);
                    }
                    sb.Append(" ON DUPLICATE KEY UPDATE `events`=`events`+VALUES(`events`),`amount_in`=`amount_in`+VALUES(`amount_in`)," +
                              "`amount_out`=`amount_out`+VALUES(`amount_out`),`units`=`units`+VALUES(`units`),`char_name`=VALUES(`char_name`),`detail_name`=VALUES(`detail_name`)");
                    cmd.CommandText = sb.ToString();
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                lastBucketFlush = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                // nothing was committed (the transaction rolled back), so merge everything back for the next flush
                lock (sync)
                {
                    foreach (var kv in toWrite)
                    {
                        if (buckets.TryGetValue(kv.Key, out var existing))
                        {
                            existing.Events += kv.Value.Events;
                            existing.AmountIn += kv.Value.AmountIn;
                            existing.AmountOut += kv.Value.AmountOut;
                            existing.Units += kv.Value.Units;
                        }
                        else
                            buckets[kv.Key] = kv.Value;
                    }
                }
                log.Error($"[PyrealLedger] FlushBuckets failed ({toWrite.Count} rows), will retry: {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>Returns false if the write failed (the flags are kept for the next flush).</summary>
        private static bool FlushFlags()
        {
            List<FlagRow> toWrite;

            lock (sync)
            {
                if (pendingFlags.Count == 0)
                    return true;
                toWrite = pendingFlags;
                pendingFlags = new List<FlagRow>();
            }

            try
            {
                using var ctx = new ShardDbContext();
                var con = OpenConnection(ctx);

                // one transaction for every chunk: a failure rolls all of them back, so the retry below cannot add
                // an already written chunk a second time
                using var tx = con.BeginTransaction();

                foreach (var batch in toWrite.Chunk(BatchRows))
                {
                    using var cmd = NewCommand(con);
                    cmd.Transaction = tx;
                    var sb = new StringBuilder("INSERT INTO `pyreal_ledger_flags` (`utc`,`char_id`,`account_id`,`char_name`,`flag`,`amount`,`expected`,`actual`,`detail`) VALUES ");
                    for (var i = 0; i < batch.Length; i++)
                    {
                        var f = batch[i];
                        if (i > 0) sb.Append(',');
                        sb.Append($"(@t{i},@c{i},@a{i},@n{i},@f{i},@am{i},@e{i},@ac{i},@d{i})");
                        AddParam(cmd, $"@t{i}", f.Utc);
                        AddParam(cmd, $"@c{i}", f.CharId);
                        AddParam(cmd, $"@a{i}", f.AccountId);
                        AddParam(cmd, $"@n{i}", Truncate(f.CharName, 64));
                        AddParam(cmd, $"@f{i}", f.Flag);
                        AddParam(cmd, $"@am{i}", f.Amount);
                        AddParam(cmd, $"@e{i}", f.Expected);
                        AddParam(cmd, $"@ac{i}", f.Actual);
                        AddParam(cmd, $"@d{i}", f.Detail ?? "");
                    }
                    cmd.CommandText = sb.ToString();
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                lock (sync)
                    pendingFlags.InsertRange(0, toWrite);
                log.Error($"[PyrealLedger] FlushFlags failed ({toWrite.Count} rows), will retry: {ex.Message}");
                return false;
            }

            return true;
        }
    }
}
