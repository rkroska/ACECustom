using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// ITEM BOUNTIES (owner 2026-11-10; design: ilt-dev docs\design\item-bounties.md). A bounty is an ITEM: it names a tier,
    /// a target, a kill count and a reward (PropertyInt.ItemBounty*). Used ON the player's Bounty Ledger (rework 2026-11-12,
    /// see below), it starts in the character's lowest free bounty slot, #1 to #4 (item_bounty_max_active). The slots live on
    /// the CHARACTER (PropertyString.ItemBountySlots); the cap and the choice are the point: a player holds as many bounty
    /// items as they like and picks which ones are going.
    ///
    /// REWORK 2026-11-12 (players found the jewelry confusing, and wanted no commands to remember): a bounty used to be used
    /// ON a ring or bracelet to fill an R1, R2, B1 or B2 slot, but the jewelry was only a stand-in - which ring, whether it
    /// was worn, ring vs bracelet never mattered, yet players took the bounty to live on that ring. Now:
    ///   - the BOUNTY LEDGER (item_bounty_ledger_wcid, its own section below) is the one thing players use: a bounty used on
    ///     it starts (at once in its own tier or region, or a town; in another tier or region the first use says where its
    ///     kills count and a second use confirms); double-clicked, it opens as a book with a page per bounty and its progress;
    ///     appraised, it lists them. Nothing is stored on it, and one is handed over whenever a player with bounties has none;
    ///   - a bounty used on anything else (the player, a ring) shows its terms and how to start it. With ledgers off, or for
    ///     a bounty the client still treats as a plain double-click, two uses start it: the first shows its terms, the slot it
    ///     takes and any warning, a second within item_bounty_confirm_seconds starts it;
    ///   - new bounties get the ItemUseable / TargetType for this (OnNewItem), the ones in a player's pack at login
    ///     (NormalizeHeldBounties), and appraisal shows the current description (AppraisalText) without writing the item;
    ///   - four plain slots #1-#4 instead of two kinds: a stored R1, R2, B1, B2 reads as #1, #2, #3, #4 with its progress
    ///     (ParseSlotKey) and is written back under the new key on the next save.
    ///
    /// Owner rules:
    ///   - starting never overwrites a bounty: every slot full = refused, the item is kept. The only way to replace a bounty
    ///     is /bounty abandon (remove), which forfeits it;
    ///   - slot numbers never shift: #2 stays #2 when #1 completes; a new bounty takes the lowest free slot;
    ///   - only the character's OWN kills count (the kill's top damager, a pet's kill for its owner - the bounty rule), and
    ///     only kills in EXACTLY the bounty's tier; a Species bounty only its creature type. The tier dip applies;
    ///   - completion pays automatically, into the pack; a full pack holds the reward (BountyManager's held rewards);
    ///   - every confirmation is in the CONSOLE, never a popup: custom clients may not show popups. Abandoning a bounty asks
    ///     for /bounty confirm with a fresh 4-digit code that expires (item_bounty_confirm_seconds), so a macro, a typo or an
    ///     old command can never forfeit one. Starting one on the ledger is deliberate (two clicks); in another tier or
    ///     region, and for any other start, it takes a second use.
    ///
    /// REGION bounties (owner 2026-11-10): a bounty whose weenie names a region (PropertyString.ItemBountyRegion: "vod"
    /// Valley of Death, "t10" Thaelaryn Island - ZoneControlManager.BountyRegions, below T11) instead of a tier. Only kills
    /// there count; there is no hit gate and no tier dip ("T11+ is fundamentally different": every kill counts in full,
    /// for everyone); it drops only there, at the region's own odds (/zonecontrol regionbounty &lt;key&gt; itemodds N).
    ///
    /// REWARDS are content data: any item WCID and amount (ItemBountyRewardWcid / Amount). MYSTERY bounties (owner
    /// 2026-11-10, PropertyInt.ItemBountyMystery): flag 1 hides the kill count (rolled from the weenie's CURRENT range only
    /// when applied, so a buyer can't see it); flag 2 hides the reward, rolled when the bounty is DONE from the Zone Control
    /// reward table the weenie names (PropertyString.ItemBountyRewardTable, /zonecontrol bountytable) as it is then - the
    /// admins can re-weight or pull a reward live for every mystery bounty already out there ("if people are like, mystery
    /// bounties suck, then we could turn that knob"). The place - the tier or region - is never hidden, so nobody trades
    /// for a bounty they can't use.
    ///
    /// Threading: like BountyManager, every read and write of a player's slots happens on that player's own thread
    /// (LandblockManager.RunOnThreadFor); the use-with-target handler already runs there.
    ///
    /// Every string here reaches the game client, so all of it is plain ASCII (CLAUDE.md): "->", never an arrow glyph.
    /// </summary>
    public static class ItemBounties
    {
        /// <summary>A bounty's kind: any kill in the tier counts.</summary>
        public const int KindSweep = 1;
        /// <summary>A bounty's kind: only kills of one CreatureType count.</summary>
        public const int KindSpecies = 2;

        /// <summary>The most bounty slots a character can ever have: item_bounty_max_active is held to 1..MaxSlots.</summary>
        public const int MaxSlots = 8;

        /// <summary>Every slot key there can be, in the order /bounty lists them: "1".."8", shown to players as #1..#8. Read-only.</summary>
        public static readonly IReadOnlyList<string> SlotKeys
            = Array.AsReadOnly(Enumerable.Range(1, MaxSlots).Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray());

        /// <summary>
        /// The slot keys from before the 2026-11-12 rework, in the order they now read as #1-#4: R1, R2 (rings), B1, B2
        /// (bracelets). A stored or typed one still works (ParseSlotKey).
        /// </summary>
        internal static readonly string[] LegacySlotKeys = { "R1", "R2", "B1", "B2" };

        /// <summary>The lowest and highest tier a bounty can name.</summary>
        public const int MinTier = 11, MaxTier = 25;

        /// <summary>ItemBountyMystery flag: the kill count is rolled (and shown) only when the bounty is started.</summary>
        public const int MysteryKills = 1;
        /// <summary>ItemBountyMystery flag: the reward is rolled from the reward table only when the bounty is done.</summary>
        public const int MysteryReward = 2;

        /// <summary>One possible reward of a Zone Control reward table (BountyRewardEntry), as the roll sees it.</summary>
        internal readonly record struct RewardOption(uint Wcid, int Amount, int Weight);

        /// <summary>The option a roll <paramref name="u"/> in [0, 1) lands on, by weight; null for an empty table.</summary>
        internal static RewardOption? PickReward(IReadOnlyList<RewardOption> table, double u)
        {
            var at = WeightedPick(table.Select(o => o.Weight).ToList(), u);
            return at < 0 ? null : table[at];
        }

        // ── Terms and slots (pure; unit-tested) ──────────────────────────────

        /// <summary>One active bounty in one slot. Progress is a decimal because of the tier dip.</summary>
        public sealed class Slot
        {
            public string Key;            // "1".."8", shown as #1..#8 (R1, R2, B1, B2 before the 2026-11-12 rework)
            public int Tier;
            public int Kind;              // KindSweep / KindSpecies
            public uint Target;           // CreatureType, Species only
            public int Kills;
            public double Progress;
            public uint RewardWcid;
            public int RewardAmount = 1;
            public long AppliedUnix;      // when it started; with Name, tells two bounties apart (an abandon confirms against it)
            public string Name = "";      // the item's name when it was started
            public string TargetName = "";// "Banderlings"; empty for Sweep
            public string Region = "";    // a REGION bounty's BountyRegions key ("vod", "t10"; Tier is 0); empty for a tier bounty
            public int Mystery;           // ItemBountyMystery flags the bounty item carried
            public string RewardTableName = "";// a MYSTERY reward's Zone Control table: rolled from it as it is WHEN DONE

            // Only while the terms are read off an ITEM, never stored in a slot: the item's WCID (its weenie's CURRENT kill
            // band is the one a hidden count rolls from) and a hidden kill count's range as the item carried it.
            public uint BountyWcid;
            public int KillsMin, KillsMax;

            /// <summary>True for a region bounty (kills in a region below T11 count, not a tier).</summary>
            public bool IsRegion => !string.IsNullOrEmpty(Region);

            /// <summary>True when the reward is a mystery: rolled from a Zone Control reward table when the bounty is done.</summary>
            public bool IsMysteryReward => (Mystery & MysteryReward) != 0;

            /// <summary>What a remove request was asked about: the same bounty is still in the slot.</summary>
            public string Identity => AppliedUnix.ToString(CultureInfo.InvariantCulture) + "/" + Name;
        }

        /// <summary>A region's name ("Valley of Death"), or its key when it is no longer one of BountyRegions.</summary>
        internal static string RegionName(string key) => ZoneControlManager.FindBountyRegion(key)?.Name ?? key ?? "";

        /// <summary>A region's short label for /bounty and the tracker: "VoD", "T10" (players call Thaelaryn Island T10).</summary>
        internal static string RegionShort(string key) => (key ?? "").ToLowerInvariant() switch
        {
            "vod" => "VoD",
            "t10" => "T10",
            var other => other.ToUpperInvariant(),
        };

        /// <summary>Where a bounty's kills count: "Tier 15" / "Valley of Death".</summary>
        internal static string PlaceName(Slot s) => s.IsRegion ? RegionName(s.Region) : $"Tier {s.Tier}";

        /// <summary>The same, short: "T15" / "VoD".</summary>
        internal static string PlaceShort(Slot s) => s.IsRegion ? RegionShort(s.Region) : $"T{s.Tier}";

        /// <summary>
        /// A slot key "1".."8" from what a player typed or a character stored: 2, #2, slot 2, slot #2, bounty 2 - and the names
        /// from before the rework, which read as #1-#4: r1 / ring 1 -> "1", r2 / ring2 -> "2", b1 / bracelet 1 -> "3", b2 ->
        /// "4" (case and spaces ignored). Null if it isn't a slot.
        /// </summary>
        internal static string ParseSlotKey(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

            // before the rework: R1, R2 (rings), B1, B2 (bracelets) - the same bounties, now #1-#4
            var legacy = t.StartsWith("ring") ? "r" + t.Substring(4) : t.StartsWith("bracelet") ? "b" + t.Substring(8) : t;
            var at = Array.IndexOf(LegacySlotKeys, legacy.ToUpperInvariant());
            if (at >= 0) return SlotKeys[at];

            foreach (var word in SlotWords)
                if (t.StartsWith(word)) t = t.Substring(word.Length);
            return SlotKeys.Contains(t) ? t : null;
        }

        /// <summary>The words ParseSlotKey skips before a slot number ("slot 2", "bounty #2"), in the order it skips them.</summary>
        private static readonly string[] SlotWords = { "slot", "bounty", "#" };

        /// <summary>How many bounties a character can have going at once: item_bounty_max_active, held to 1..MaxSlots.</summary>
        internal static int MaxActive(long setting) => (int)Math.Clamp(setting, 1, MaxSlots);

        /// <summary>The live cap (item_bounty_max_active).</summary>
        internal static int MaxActiveNow => MaxActive(ServerConfig.item_bounty_max_active.Value);

        /// <summary>
        /// The slot a new bounty takes: the lowest free one of #1..#<paramref name="max"/>; null while <paramref name="max"/>
        /// or more bounties are going. A bounty in a slot above the cap (the cap was lowered after it started) still counts
        /// toward it, keeps counting kills and pays out.
        /// </summary>
        internal static string LowestFree(IReadOnlyDictionary<string, Slot> slots, int max)
        {
            max = Math.Clamp(max, 1, MaxSlots);
            if (slots.Count >= max) return null;
            return SlotKeys.Take(max).FirstOrDefault(k => !slots.ContainsKey(k));
        }

        /// <summary>
        /// The slots /bounty, the tracker and BOUNTYDATA show, in order: #1..#<paramref name="max"/>, then any slot above it
        /// that still holds a bounty. <paramref name="max"/> 0 = only the slots holding one.
        /// </summary>
        internal static List<string> ShownKeys(IReadOnlyDictionary<string, Slot> slots, int max)
            => SlotKeys.Where((k, i) => i < max || slots.ContainsKey(k)).ToList();

        /// <summary>
        /// The character's slots from PropertyString.ItemBountySlots. Bad entries are skipped, never thrown. An entry stored
        /// before the 2026-11-12 rework (R1, R2, B1, B2) reads as #1-#4 with everything it held (ParseSlotKey); the next save
        /// writes it back under its new key.
        /// </summary>
        internal static Dictionary<string, Slot> ParseSlots(string raw)
        {
            var map = new Dictionary<string, Slot>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw)) return map;

            // An entry that can't be read is skipped - and logged, because the next save rewrites the slots without it.
            void Reject(string part, string why) => log.Warn($"[ItemBounty] slot entry dropped ({why}): \"{part}\"");
            foreach (var part in raw.Split(';'))
            {
                if (part.Length == 0) continue;
                var f = part.Split('|');
                if (f.Length < 11) { Reject(part, "too few fields"); continue; }
                var key = ParseSlotKey(f[0]);
                if (key == null || map.ContainsKey(key)) { Reject(part, key == null ? "unknown slot" : "duplicate slot"); continue; }

                var s = new Slot { Key = key, Name = f[9], TargetName = f[10] };
                if (!int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out s.Tier)
                    || !int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out s.Kind)
                    || !uint.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out s.Target)
                    || !int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out s.Kills) || s.Kills <= 0
                    || !uint.TryParse(f[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out s.RewardWcid))
                {
                    Reject(part, "unreadable number");
                    continue;
                }
                if (!double.TryParse(f[5], NumberStyles.Float, CultureInfo.InvariantCulture, out s.Progress) || !double.IsFinite(s.Progress)) s.Progress = 0;
                if (!int.TryParse(f[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out s.RewardAmount)) s.RewardAmount = 1;
                long.TryParse(f[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out s.AppliedUnix);
                s.Region = f.Length > 11 ? f[11].Trim().ToLowerInvariant() : "";   // field 12 (2026-11-10): older entries have none
                // fields 13-14 (mystery bounties, 2026-11-10): the Zone Control reward table's name and the flags. A fixed reward
                // needs its WCID, a mystery reward its table's name; without either nothing could ever pay.
                s.RewardTableName = f.Length > 12 ? f[12].Trim().ToLowerInvariant() : "";
                if (f.Length > 13) int.TryParse(f[13], NumberStyles.Integer, CultureInfo.InvariantCulture, out s.Mystery);
                if (s.IsMysteryReward ? !ZoneControlManager.IsValidRewardTableName(s.RewardTableName) : s.RewardWcid == 0)
                {
                    Reject(part, s.IsMysteryReward ? "no valid reward table name" : "no reward");
                    continue;
                }
                // the same caps as everywhere else: a hand-edited slot can't pay more, or ask more, than any bounty can
                s.Kills = Math.Min(s.Kills, BountyManager.MaxKills);
                s.RewardAmount = Math.Clamp(s.RewardAmount, 1, BountyManager.MaxAmount);
                s.Progress = Math.Clamp(s.Progress, 0, s.Kills);
                map[key] = s;
            }
            return map;
        }

        /// <summary>The slots as PropertyString.ItemBountySlots stores them, in slot order.</summary>
        internal static string FormatSlots(IReadOnlyDictionary<string, Slot> slots)
        {
            var sb = new StringBuilder();
            foreach (var key in SlotKeys)
            {
                if (!slots.TryGetValue(key, out var s)) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(key).Append('|')
                  .Append(s.Tier.ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append(s.Kind.ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append(s.Target.ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append(s.Kills.ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append(s.Progress.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                  .Append(s.RewardWcid.ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append(s.RewardAmount.ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append(s.AppliedUnix.ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append(Clean(s.Name)).Append('|')
                  .Append(Clean(s.TargetName)).Append('|')
                  .Append(Clean(s.Region)).Append('|')
                  .Append(Clean(s.RewardTableName)).Append('|')
                  .Append(s.Mystery.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Whether a kill counts toward a slot: a tier bounty, a kill in exactly its tier (<paramref name="killTier"/>, 0 for
        /// no T11+ zone); a region bounty, a kill in its region (<paramref name="killRegion"/>, a BountyRegions key or null);
        /// and for a Species bounty, its creature type. The tier dip and who made the kill are checked by the caller.
        /// </summary>
        internal static bool Matches(Slot s, int killTier, string killRegion, uint? creatureType)
        {
            if (s == null) return false;
            var here = s.IsRegion
                ? killRegion != null && string.Equals(killRegion, s.Region, StringComparison.OrdinalIgnoreCase)
                : killTier >= MinTier && killTier == s.Tier;
            if (!here) return false;
            if (s.Kind == KindSpecies) return creatureType.HasValue && creatureType.Value == s.Target;
            return s.Kind == KindSweep;
        }

        /// <summary>A bounty's terms read off its item, or null if the item isn't a complete, valid bounty.</summary>
        internal static Slot TermsOf(WorldObject item)
        {
            if (item == null) return null;
            var tier = item.GetProperty(PropertyInt.ItemBountyTier) ?? 0;
            var kind = item.GetProperty(PropertyInt.ItemBountyKind) ?? KindSweep;
            var target = item.GetProperty(PropertyInt.ItemBountyTarget) ?? 0;
            var kills = item.GetProperty(PropertyInt.ItemBountyKills) ?? 0;
            var reward = item.GetProperty(PropertyInt.ItemBountyRewardWcid) ?? 0;
            var amount = item.GetProperty(PropertyInt.ItemBountyRewardAmount) ?? 1;

            // A REGION bounty names a region instead of a tier (its tier is ignored); an unknown region key is broken terms.
            var region = (item.GetProperty(PropertyString.ItemBountyRegion) ?? "").Trim().ToLowerInvariant();
            if (region.Length > 0)
            {
                if (ZoneControlManager.FindBountyRegion(region) == null) return null;
                tier = 0;
            }
            else if (tier < MinTier || tier > MaxTier) return null;

            // MYSTERY bounties (owner 2026-11-10): a hidden kill count is rolled at apply from its range; a hidden reward is
            // rolled when the bounty is done from the Zone Control table the item NAMES (never a list on the item). The place
            // above is never hidden.
            var mystery = item.GetProperty(PropertyInt.ItemBountyMystery) ?? 0;
            var killsMin = item.GetProperty(PropertyInt.ItemBountyKillsMin) ?? 0;
            var killsMax = item.GetProperty(PropertyInt.ItemBountyKillsMax) ?? 0;
            if (kills <= 0 && !((mystery & MysteryKills) != 0 && (killsMin > 0 || killsMax > 0))) return null;
            if (kills > BountyManager.MaxKills) return null;

            var tableName = "";
            if ((mystery & MysteryReward) != 0)
            {
                tableName = (item.GetProperty(PropertyString.ItemBountyRewardTable) ?? "").Trim().ToLowerInvariant();
                if (!ZoneControlManager.IsValidRewardTableName(tableName)) return null;   // a mystery reward must name its table
                reward = 0;
            }
            else if (reward <= 0) return null;

            if (kind != KindSweep && kind != KindSpecies) return null;
            if (kind == KindSpecies && target <= 0) return null;

            // the weenie's own words ("Banderlings"), else the species as the salvage bags print it ("Gear Knight")
            var targetName = kind == KindSpecies
                ? (item.GetProperty(PropertyString.ItemBountyTargetName) ?? ACE.Server.Entity.GearEssences.SpeciesName((CreatureType)target))
                : "";

            return new Slot
            {
                Tier = tier, Kind = kind, Target = (uint)Math.Max(0, target), Kills = Math.Max(0, kills),
                RewardWcid = (uint)Math.Max(0, reward), RewardAmount = Math.Clamp(amount, 1, BountyManager.MaxAmount),
                Name = item.Name ?? "Bounty", TargetName = targetName, Region = region,
                Mystery = mystery, RewardTableName = tableName, BountyWcid = item.WeenieClassId, KillsMin = killsMin, KillsMax = killsMax,
            };
        }

        /// <summary>Where a bounty counts, as a reward table's place suffix: "t15" for a tier, the region key ("vod") for a region.</summary>
        internal static string PlaceKey(Slot s) => s.IsRegion ? s.Region : "t" + s.Tier.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// The rewards a mystery bounty can pay right now: its Zone Control table as it is NOW - the place's own table first
        /// ("mystery.t15"), else the base one ("mystery") - only entries that can be made into a carryable item. Empty when
        /// there is none: the admins control mystery rewards completely, so nothing older is ever paid instead.
        /// <paramref name="tableUsed"/> names the table read (for the log).
        /// </summary>
        private static List<RewardOption> MysteryOptions(Slot s, out string tableUsed)
        {
            var live = ZoneControlManager.ResolveBountyRewardTable(s.RewardTableName, PlaceKey(s), e => BountyManager.IsGivableItem(e.Wcid));
            tableUsed = live?.Name ?? s.RewardTableName;
            return live.HasValue
                ? live.Value.Table.Valid.Select(e => new RewardOption(e.Wcid, e.Amount, e.Weight)).Where(o => BountyManager.IsGivableItem(o.Wcid)).ToList()
                : new List<RewardOption>();
        }

        /// <summary>
        /// True when an ITEM's kill count is still hidden - the mystery flag 1, or no count yet - so it is rolled from
        /// KillsMin..KillsMax (or the count the weenie carries, when it has no range) when the bounty is applied. Asked of
        /// an item's terms only: an applied slot keeps the flag but has its rolled count.
        /// </summary>
        internal static bool KillsHidden(Slot t) => t.Kills <= 0 || (t.Mystery & MysteryKills) != 0;

        /// <summary>A uniform roll in [0, 1) for RollKills, PickReward and WeightedPick (one place, one precision).</summary>
        private static double NextUnit() => ThreadSafeRandom.Next(0, 999_999_999) / 1_000_000_000.0;

        /// <summary>
        /// A kill count rolled between <paramref name="min"/> and <paramref name="max"/> (swapped if backwards, held to
        /// 1..MaxKills) by <paramref name="u"/> in [0, 1): a round number - to 50 from 1,000 up, to 10 from 100 up - and
        /// never outside the range.
        /// </summary>
        internal static int RollKills(int min, int max, double u)
        {
            if (min > max) (min, max) = (max, min);
            min = Math.Clamp(min, 1, BountyManager.MaxKills);
            max = Math.Clamp(max, min, BountyManager.MaxKills);

            var raw = min + Math.Clamp(u, 0, 0.999999) * (max - min + 1);
            var step = max >= 1000 ? 50 : max >= 100 ? 10 : 1;
            var rounded = (int)Math.Round(Math.Floor(raw) / step) * step;
            return Math.Clamp(rounded, min, max);
        }

        /// <summary>
        /// A new item (WorldObjectFactory.CreateNewWorldObject - /ci, loot, an NPC's Give, a vendor). For a bounty, the kill
        /// count is rolled now, once, from its RANGE (ItemBountyKillsMin / Max) so the item keeps it when traded (owner
        /// 2026-11-10: bounties are named for their reward, and the task varies) - unless the count is a mystery, rolled when
        /// it starts instead. The item gets the use of the current mode (MakeUsable, rework 2026-11-12: used on the Bounty
        /// Ledger, or a plain double-click with ledgers off) whatever its weenie says, and the description then states the
        /// terms. Anything that isn't a bounty returns after three lookups in
        /// <paramref name="weenie"/>'s own dictionary (the cached weenie it was made from: no biota lock); a KillsMin / Max of
        /// 0 or less counts as unset, as everywhere else.
        /// </summary>
        public static void OnNewItem(WorldObject wo, ACE.Entity.Models.Weenie weenie = null)
        {
            try
            {
                if (wo == null) return;
                var props = weenie?.PropertiesInt;
                if (weenie != null && (props == null || !(props.ContainsKey(PropertyInt.ItemBountyKills)
                        || props.ContainsKey(PropertyInt.ItemBountyKillsMin) || props.ContainsKey(PropertyInt.ItemBountyKillsMax))))
                    return;

                var min = wo.GetProperty(PropertyInt.ItemBountyKillsMin) ?? 0;
                var max = wo.GetProperty(PropertyInt.ItemBountyKillsMax) ?? 0;
                var hasRange = min > 0 || max > 0;
                if (!hasRange && (wo.GetProperty(PropertyInt.ItemBountyKills) ?? 0) <= 0)
                    return;

                var mystery = wo.GetProperty(PropertyInt.ItemBountyMystery) ?? 0;
                if ((mystery & MysteryKills) == 0 && hasRange)
                    wo.SetProperty(PropertyInt.ItemBountyKills, RollKills(min > 0 ? min : max, max > 0 ? max : min, NextUnit()));

                // how the client uses it (rework 2026-11-12): on the Bounty Ledger, or a plain double-click with ledgers off -
                // whatever the weenie says (a weenie made for the old use-on-a-ring targets jewelry)
                MakeUsable(wo);

                var terms = TermsOf(wo);
                if (terms != null)
                    wo.SetProperty(PropertyString.LongDesc, Describe(terms));
            }
            catch (Exception ex)
            {
                log.Error($"[ItemBounty] OnNewItem: {ex}");
            }
        }

        /// <summary>
        /// A bounty item's description, from its terms: what to kill, where, the reward, and how to start it - on the Bounty
        /// Ledger, or with two double-clicks when ledgers are off. A mystery kill count or reward says so (and when it is
        /// revealed); the place is always stated.
        /// </summary>
        internal static string Describe(Slot t)
        {
            var what = t.Kind == KindSpecies ? t.TargetName : "creatures";
            var place = PlaceName(t);
            var task = KillsHidden(t)
                ? $"Kill a mystery number of {what} in {place}: the number is revealed when you start the bounty."
                : $"Kill {t.Kills:N0} {what} in {place}.";
            var reward = t.IsMysteryReward
                ? "Reward: a mystery, revealed when the bounty is done and paid to your pack automatically."
                : $"Reward: {RewardText(t)}, paid to your pack automatically when done.";
            var start = LedgersOn
                ? "To start it, double-click it and then click your Bounty Ledger (no ledger yet? click yourself instead and one is added to your pack)."
                : "Double-click it twice to start it: the first shows its terms, the second starts it.";
            return $"{task} Only your own kills count, and only in {place}. {reward} {start} "
                + $"You can have up to {MaxActiveNow} bounties going at once{(LedgersOn ? "; your Bounty Ledger shows them." : "; /bounty shows them.")}";
        }

        /// <summary>
        /// Appraisal (AppraiseInfo): a bounty item's description from its terms as they are now, or null for anything else (and
        /// for a bounty whose terms aren't complete yet, which keeps its own). Display only - the item is never written - so a
        /// bounty made before the 2026-11-12 rework, whose stored description still says to use it on a ring or bracelet,
        /// reads the current words.
        /// </summary>
        public static string DescribeItem(WorldObject wo)
        {
            try
            {
                if (!IsBountyItem(wo))
                    return null;
                var terms = TermsOf(wo);
                return terms != null ? Describe(terms) : null;
            }
            catch (Exception ex)
            {
                log.Error($"[ItemBounty] DescribeItem: {ex}");
                return null;
            }
        }

        /// <summary>
        /// A vendor sale (Vendor.ItemProfileToWorldObjects): the bought copy of a bounty asks the kill count the copy on
        /// display shows - each copy rolls its own when it is made, so without this a buyer could appraise one count and
        /// get another. Anything that isn't a bounty is left as it is.
        /// </summary>
        public static void CopyRolledTerms(WorldObject shown, WorldObject bought)
        {
            if (shown == null || bought == null || shown.WeenieClassId != bought.WeenieClassId)
                return;
            var kills = shown.GetProperty(PropertyInt.ItemBountyKills);
            if (!kills.HasValue || kills.Value <= 0)
                return;
            bought.SetProperty(PropertyInt.ItemBountyKills, kills.Value);
            var desc = shown.GetProperty(PropertyString.LongDesc);
            if (desc != null)
                bought.SetProperty(PropertyString.LongDesc, desc);
        }

        /// <summary>
        /// True for an item that carries bounty terms - a kill count, or a kill range still to roll (a mystery count) - so the
        /// use hooks (Player_Use: a double-click, or a use on any target) route it here.
        /// </summary>
        public static bool IsBountyItem(WorldObject wo)
            => wo != null && ((wo.GetProperty(PropertyInt.ItemBountyKills) ?? 0) > 0
                || (wo.GetProperty(PropertyInt.ItemBountyKillsMin) ?? 0) > 0 || (wo.GetProperty(PropertyInt.ItemBountyKillsMax) ?? 0) > 0);

        /// <summary>"640 of 1,000 kills" / "100 of 300 Banderlings".</summary>
        internal static string ProgressText(Slot s)
            => $"{TierRewardScale.FormatProgress(s.Progress)} of {s.Kills:N0} {(s.Kind == KindSpecies ? s.TargetName : "kills")}";

        /// <summary>The words players see for a mystery reward, in /bounty, the tracker and the messages.</summary>
        public const string MysteryRewardText = "Mystery reward";

        /// <summary>"Bag of Rebirth" / "3 Bag of Rebirth" / "Mystery reward".</summary>
        internal static string RewardText(Slot s)
        {
            if (s.IsMysteryReward) return MysteryRewardText;
            var name = RoomAssignManager.ItemName(s.RewardWcid);
            return s.RewardAmount > 1 ? $"{s.RewardAmount:N0} {name}" : name;
        }

        /// <summary>
        /// True when the bounty's reward can be paid; otherwise tells the player the bounty is kept and why, and logs it.
        /// Asked on every use of a bounty (the first, and the second that starts it), so a slot is never filled by a bounty
        /// that can't pay.
        /// </summary>
        private static bool RewardReadyOrTell(Player player, WorldObject item, Slot terms)
        {
            var problem = RewardProblem(terms, out var detail);
            if (problem == null)
                return true;
            Tell(player, $"The {item.Name}'s {problem}. Keep it, and please tell an admin.");
            log.Warn($"[ItemBounty] {player.Name}: {item.Name} (wcid {item.WeenieClassId}) can't be started: {detail}.");
            return false;
        }

        /// <summary>
        /// Why the terms' reward can't be paid as things stand (null = it can): a fixed reward that isn't a carryable item,
        /// or a mystery whose Zone Control table has nothing that can be paid at the bounty's place. Checked before a bounty
        /// starts, so nobody fills a slot that could never pay. <paramref name="detail"/> is for the log.
        /// </summary>
        private static string RewardProblem(Slot t, out string detail)
        {
            if (!t.IsMysteryReward)
            {
                detail = $"reward {t.RewardWcid} isn't a carryable item";
                return BountyManager.IsGivableItem(t.RewardWcid) ? null : "reward can't be made";
            }
            detail = $"mystery reward table '{t.RewardTableName}' has nothing that can be paid at {PlaceKey(t)}";
            return MysteryOptions(t, out _).Count > 0 ? null : "mystery reward isn't set up yet";
        }

        /// <summary>The /bounty line for one slot: "#1: 640 of 1,000 kills in T15 -> Bag of Rebirth" or "#4: empty".</summary>
        internal static string SlotLine(string key, Slot s)
            => s == null ? $"#{key}: empty" : $"#{key}: {ProgressText(s)} in {PlaceShort(s)} -> {RewardText(s)}";

        /// <summary>
        /// "kill 1,000 creatures in Tier 15" / "kill a mystery number of Banderlings in Valley of Death (revealed when it
        /// starts)": the task of a bounty not started yet, for the line a first double-click shows.
        /// </summary>
        internal static string TaskText(Slot t)
        {
            var what = t.Kind == KindSpecies ? t.TargetName : "creatures";
            return KillsHidden(t)
                ? $"kill a mystery number of {what} in {PlaceName(t)} (revealed when it starts)"
                : $"kill {t.Kills:N0} {what} in {PlaceName(t)}";
        }

        // ── Starting (Player_Use: a bounty item double-clicked, or used on anything) ──

        /// <summary>A start waiting for its second use: the bounty item's guid and when the offer runs out (unix seconds).</summary>
        private readonly record struct ArmedStart(uint ItemGuid, double ExpiresAt);

        /// <summary>
        /// Player guid -> the bounty a first use armed (its terms shown, or the warning that its kills don't count where the
        /// player stands): their next use of it in time starts it. Set and taken on that player's thread (HandleUse); dropped
        /// at logout (OnLogout). Holds no Player object, so nothing outlives a session through it.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, ArmedStart> _armed = new();

        /// <summary>
        /// A bounty item used: double-clicked (Player.HandleActionUseItem, <paramref name="target"/> null), or used on
        /// something (HandleActionUseWithTarget). On the player's thread.
        ///
        /// With ledgers on (item_bounty_ledger_wcid, 2026-11-12) a bounty is used ON the Bounty Ledger:
        ///   - on the player's own ledger it starts at once where its kills count, or in a town - double-clicking the bounty
        ///     and then clicking the ledger is deliberate enough. Standing in ANOTHER tier or region (probably a mistake, as
        ///     the ring start judged it) the first use says where its kills count, and a second use on the ledger within
        ///     item_bounty_confirm_seconds starts it: a click where the ring start asked for a typed /bounty confirm code;
        ///   - on anything else (the player, a ring as before the rework, a book) it shows the bounty and how to start it, and
        ///     hands the player a ledger if they have none. In another tier or region that preview carries the warning, so
        ///     the next use on the ledger starts it without asking again.
        /// TWO USES start it everywhere else: with ledgers off, for a plain double-click (a bounty whose ItemUseable the
        /// client still has from before the ledger), and when no ledger could be handed over (a full pack, its weenie
        /// missing), so nobody is ever stuck. The first use shows the bounty; a second use of the same item within
        /// item_bounty_confirm_seconds starts it - starting uses up a tradeable item for good, so one stray click never does.
        /// Every check runs again on every use; anything that stops a start says why and keeps the bounty in the pack.
        /// </summary>
        public static void HandleUse(Player player, WorldObject item, WorldObject target = null)
        {
            try
            {
                if (player == null || item == null) return;

                if (!ServerConfig.item_bounty_enabled.Value)
                {
                    _armed.TryRemove(player.Guid.Full, out _);
                    Tell(player, "Bounties can't be started right now.");
                    return;
                }

                // No ledger yet: one FIRST, whatever then happens to this bounty - all slots full, a tier they can't fight yet -
                // since using a bounty (on themselves, as its description says) is how a player gets one.
                var ledger = FindLedger(player);
                var granted = false;
                if (ledger == null && LedgersOn)
                {
                    ledger = GrantLedger(player, atLogin: false);
                    granted = ledger != null;
                }
                if (granted)
                    Tell(player, "A Bounty Ledger was added to your pack: it keeps track of your bounties - double-click it to read them. To start a bounty, double-click the bounty and then click the ledger.");

                if (!CanStartOrTell(player, item, out var terms, out var slots, out var free))
                {
                    _armed.TryRemove(player.Guid.Full, out _);
                    return;
                }

                // a bounty whose use the client has from before (picked up since login from a house chest or a corpse: login
                // fixes only what the player carries, NormalizeHeldBounties) is fixed now and sent to the client again, so its
                // cursor takes the ledger from the next double-click on - the preview below says to click the ledger
                if (MakeUsable(item))
                    player.Session?.Network.EnqueueSend(new GameMessageUpdateObject(item));

                if (LedgersOn && target != null)
                {
                    var note = StandingNote(player, terms);

                    // on the player's own ledger: started - at once where its kills count, or in a town. Standing in another
                    // tier or region the first use says so and arms it; a second use on the ledger in time starts it.
                    if (IsLedger(target) && player.FindObject(target.Guid.Full, Player.SearchLocations.MyInventory) != null)
                    {
                        if (note != null && !TakeArmed(player, item))
                        {
                            var seconds = Arm(player, item);
                            Tell(player, $"{note} To start it anyway, use it on your Bounty Ledger again within {seconds} seconds.");
                            return;
                        }
                        _armed.TryRemove(player.Guid.Full, out _);
                        Commit(player, item, terms, free);
                        return;
                    }

                    // on anything else (themselves, a ring): what it is, and how to start it. In another tier or region the
                    // preview's note is the warning, so it arms the bounty: the next use on the ledger starts it.
                    if (ledger != null)
                    {
                        if (note != null)
                            Arm(player, item);
                        else
                            _armed.TryRemove(player.Guid.Full, out _);
                        Preview(player, item, terms, note);
                        if (!granted)
                            Tell(player, $"To start it, double-click it and then click your Bounty Ledger. It goes in as bounty #{free} ({slots.Count} of {MaxActiveNow} going).");
                        return;
                    }
                    // no ledger could be handed over (a full pack, its weenie missing): the two uses below still start it
                }

                StartOnSecondUse(player, item, terms, slots, free, target == null ? "Double-click it again" : "Use it again");
            }
            catch (Exception ex)
            {
                log.Error($"[ItemBounty] HandleUse: {ex}");
            }
        }

        /// <summary>
        /// The two-use start: the second use of the SAME bounty within item_bounty_confirm_seconds starts it; any other use
        /// shows it and waits for that second use. <paramref name="again"/> names the gesture ("Double-click it again").
        /// </summary>
        private static void StartOnSecondUse(Player player, WorldObject item, Slot terms, Dictionary<string, Slot> slots, string free, string again)
        {
            if (TakeArmed(player, item))
            {
                Commit(player, item, terms, free);
                return;
            }

            var seconds = Arm(player, item);
            Preview(player, item, terms, StandingNote(player, terms));
            Tell(player, $"{again} within {seconds} seconds to start it as bounty #{free} ({slots.Count} of {MaxActiveNow} going). "
                + "Once started it is used up - it can't be traded or put back - and abandoning it forfeits it.");
        }

        /// <summary>Arms <paramref name="item"/> for this player (<see cref="Arm(uint, uint)"/>).</summary>
        private static int Arm(Player player, WorldObject item) => Arm(player.Guid.Full, item.Guid.Full);

        /// <summary>
        /// Arms a bounty for a player: their next use of the SAME bounty within item_bounty_confirm_seconds starts it
        /// (<see cref="TakeArmed(uint, uint)"/>). Replaces whatever was armed for them before. Returns the seconds it stays
        /// armed. By guid, so the tests can drive it without a Player.
        /// </summary>
        internal static int Arm(uint playerGuid, uint itemGuid)
        {
            var seconds = ConfirmSeconds;
            _armed[playerGuid] = new ArmedStart(itemGuid, Time.GetUnixTime() + seconds);
            return seconds;
        }

        /// <summary>Takes this player's arm when it is for <paramref name="item"/> (<see cref="TakeArmed(uint, uint)"/>).</summary>
        private static bool TakeArmed(Player player, WorldObject item) => TakeArmed(player.Guid.Full, item.Guid.Full);

        /// <summary>
        /// True, once, when <paramref name="itemGuid"/> is the bounty armed for this player and its time hasn't run out. The
        /// arm is taken, so one arm starts at most one bounty; an arm for another bounty is left alone.
        /// </summary>
        internal static bool TakeArmed(uint playerGuid, uint itemGuid)
        {
            return _armed.TryGetValue(playerGuid, out var armed) && armed.ItemGuid == itemGuid && armed.ExpiresAt >= Time.GetUnixTime()
                && _armed.TryRemove(new KeyValuePair<uint, ArmedStart>(playerGuid, armed));
        }

        /// <summary>
        /// What a bounty is before it starts: the task and reward, then <paramref name="note"/> (StandingNote: where its kills
        /// count, when the player stands somewhere they don't), if any.
        /// </summary>
        private static void Preview(Player player, WorldObject item, Slot terms, string note)
        {
            Tell(player, $"{item.Name} - {TaskText(terms)} -> {RewardText(terms)}. Only your own kills there count.");
            if (note != null)
                Tell(player, note);
        }

        /// <summary>
        /// Whether <paramref name="player"/> can start <paramref name="item"/> now: it is in their pack and not in a trade
        /// window, the character can take bounties, its terms are complete and its reward can be paid, they can fight its
        /// tier and still get credit there, and a slot is free (<paramref name="free"/>). When not, tells them why - the bounty
        /// is kept - and returns false. On the player's thread.
        /// </summary>
        private static bool CanStartOrTell(Player player, WorldObject item, out Slot terms, out Dictionary<string, Slot> slots, out string free)
        {
            terms = null;
            slots = null;
            free = null;

            // the bounty from the pack - not one lying on the ground or in a corpse - and not one offered in a trade (both use
            // hooks refuse a traded item before they get here; checked again so HandleUse is safe on its own)
            if (player.FindObject(item.Guid.Full, Player.SearchLocations.MyInventory) == null)
            {
                Tell(player, $"The {item.Name} must be in your pack to start it.");
                return false;
            }
            if (player.IsTrading && item.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            {
                Tell(player, $"The {item.Name} is in your trade window. Take it out of the trade to start it.");
                return false;
            }
            if (player.IsMule || player.IsOlthoiPlayer)
            {
                Tell(player, "This character can't take bounties.");
                return false;
            }

            terms = TermsOf(item);
            if (terms == null)
            {
                Tell(player, $"The {item.Name} is missing its bounty terms. Please tell an admin.");
                log.Warn($"[ItemBounty] {player.Name}: {item.Name} (wcid {item.WeenieClassId}) has incomplete bounty terms.");
                return false;
            }
            if (!RewardReadyOrTell(player, item, terms))
                return false;
            if (!CanTake(player, terms, out var why))
            {
                Tell(player, why);
                return false;
            }

            slots = LoadSlots(player);
            return FreeSlotOrTell(player, slots, out free);
        }

        /// <summary>
        /// "Note: you are in Tier 14 - only kills in Tier 15 count for this bounty." when the player stands in a place that
        /// counts for bounties (a T11+ tier, or a bounty region) other than the bounty's; null there, or anywhere else (a town).
        /// It only informs: a bounty started in town or another tier counts as soon as the player hunts in its place.
        /// </summary>
        private static string StandingNote(Player player, Slot terms)
        {
            var (standTier, standRegion) = StandingPlace(player);
            var here = standRegion != null ? RegionName(standRegion) : standTier != 0 ? $"Tier {standTier}" : null;
            if (here == null)
                return null;
            var counts = terms.IsRegion
                ? standRegion != null && string.Equals(standRegion, terms.Region, StringComparison.OrdinalIgnoreCase)
                : standRegion == null && standTier == terms.Tier;
            return counts ? null : $"Note: you are in {here} - only kills in {PlaceName(terms)} count for this bounty.";
        }

        /// <summary>
        /// Whether this player could ever progress the bounty: they can hit its tier, and the tier dip leaves credit. A region
        /// bounty always can (owner 2026-11-10: no gate and no dip below T11 - every kill there counts in full).
        /// </summary>
        private static bool CanTake(Player player, Slot terms, out string why)
        {
            why = null;
            if (terms.IsRegion)
                return true;

            if (ServerConfig.zc_tier_hit_gate_enabled.Value)
            {
                var canFight = TierHitGate.PlayerTier(player);
                if (canFight < terms.Tier)
                {
                    why = $"You can't fight Tier {terms.Tier} monsters yet" + (canFight >= MinTier ? $" (you can fight up to Tier {canFight})" : "")
                        + ". Keep the bounty or trade it.";
                    return false;
                }
            }

            if (!(TierRewardScale.CreditFor(player, terms.Tier) > 0))
            {
                why = $"Tier {terms.Tier} kills are too far below your tier to count (/tierdip). Keep the bounty or trade it.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// The slot a new bounty takes (<see cref="LowestFree"/> under item_bounty_max_active); when the character already has
        /// that many going, lists them, says how to make room and returns false.
        /// </summary>
        private static bool FreeSlotOrTell(Player player, Dictionary<string, Slot> slots, out string free)
        {
            var max = MaxActiveNow;
            free = LowestFree(slots, max);
            if (free != null)
                return true;

            var going = SlotKeys.Where(slots.ContainsKey).Select(k => $"#{k} {slots[k].Name} ({ProgressText(slots[k])})");
            Tell(player, $"You have {slots.Count} bounties going and can have at most {max}: {string.Join("; ", going)}. "
                + "Finish one, or abandon one with /bounty abandon <#> (it forfeits that bounty), to start this one. It stays in your pack.");
            return false;
        }

        /// <summary>
        /// Starts the bounty in the slot: re-checked, everything that can fail worked out first (a mystery kill count), then
        /// the item consumed, then the slot written and the character saved at once (as UseLumToken does), so neither a
        /// failure nor a crash can leave both the item and the slot - or neither.
        /// </summary>
        private static void Commit(Player player, WorldObject item, Slot terms, string key)
        {
            var slots = LoadSlots(player);
            if (slots.ContainsKey(key) || slots.Count >= MaxActiveNow)
            {
                Tell(player, $"Bounty slot #{key} is no longer free. Nothing was started.");
                return;
            }

            // a MYSTERY kill count is rolled now and revealed in the line below - from the bounty weenie's CURRENT band, so a
            // band the admins change also reaches the mystery bounties already out there (else the band the item carried).
            // Rolled BEFORE the item is consumed: a failure here keeps the bounty in the pack.
            var revealed = KillsHidden(terms);
            if (revealed)
            {
                var (lo, hi) = CurrentKillBand(terms);
                terms.Kills = RollKills(lo, hi, NextUnit());
            }

            if (!player.TryConsumeFromInventoryWithNetworking(item, 1))
            {
                Tell(player, $"The {item.Name} could not be used. Nothing was started.");
                return;
            }

            terms.Key = key;
            terms.Progress = 0;
            terms.AppliedUnix = (long)Time.GetUnixTime();
            slots[key] = terms;
            SaveSlots(player, slots);
            player.SavePlayerToDatabase();

            Tell(player, $"Bounty #{key} started: {(revealed ? "the mystery count is " : "")}{terms.Kills:N0} {(terms.Kind == KindSpecies ? terms.TargetName : "kills")} in {PlaceName(terms)} -> {RewardText(terms)}. "
                + $"{slots.Count} of {MaxActiveNow} bounties going; {(LedgersOn ? "double-click your Bounty Ledger to see your progress." : "/bounty shows your progress.")}");
            var note = StandingNote(player, terms);
            if (note != null)
                Tell(player, note);
            log.Info($"[ItemBounty] start {player.Name} #{key} {PlaceShort(terms)} kind={terms.Kind} target={terms.Target} kills={terms.Kills}{(revealed ? " (mystery count)" : "")} "
                + (terms.IsMysteryReward ? $"reward=mystery(table {terms.RewardTableName})" : $"reward={terms.RewardWcid}x{terms.RewardAmount}") + $" \"{terms.Name}\"");
        }

        /// <summary>
        /// A hidden kill count's band: the bounty weenie's current ItemBountyKillsMin / Max (world cache), else the band the
        /// item carried. A missing end takes the other one's value; with no range at all, the fixed count the item carries.
        /// </summary>
        private static (int Lo, int Hi) CurrentKillBand(Slot terms)
        {
            var w = terms.BountyWcid != 0 ? ACE.Database.DatabaseManager.World.GetCachedWeenie(terms.BountyWcid) : null;
            int Live(PropertyInt p, int carried) => w?.PropertiesInt != null && w.PropertiesInt.TryGetValue(p, out var v) && v > 0 ? v : carried;
            var min = Live(PropertyInt.ItemBountyKillsMin, terms.KillsMin);
            var max = Live(PropertyInt.ItemBountyKillsMax, terms.KillsMax);
            if (min <= 0 && max <= 0)
                return (terms.Kills, terms.Kills);   // no range anywhere: the count the weenie carries, kept hidden until now
            return (min > 0 ? min : max, max > 0 ? max : min);
        }

        /// <summary>The Zone Control tier the player stands in (11-25), or 0 outside every T11+ zone (a town, say).</summary>
        private static int StandingTier(Player player)
        {
            var tier = ZoneControlManager.GetEffectiveVariation(player);
            if (tier < MinTier || tier > MaxTier || player.Location == null) return 0;
            var area = ZoneControlManager.ResolveWinnerForLocation((ushort)player.Location.Landblock, player.Location.Variation ?? 0);
            return area != null ? tier : 0;
        }

        /// <summary>Where the player stands, as bounties see it: a T11+ tier, else a bounty region's key, else (0, null).</summary>
        private static (int Tier, string Region) StandingPlace(Player player)
        {
            var tier = StandingTier(player);
            return tier != 0 ? (tier, null) : (0, ZoneControlManager.BountyRegionAt(player));
        }

        // ── The kill ─────────────────────────────────────────────────────────

        /// <summary>
        /// Creature death (Creature_Death.OnDeath), on the victim's thread. Only kills that pay XP or luminance, in a T11+
        /// Zone Control zone or a bounty region below T11 (Valley of Death, Thaelaryn Island); the credit goes to the top
        /// damager's own thread.
        /// </summary>
        public static void OnCreatureKilled(Creature victim)
        {
            try
            {
                if (victim == null || victim is Player || victim is Pet || !ServerConfig.item_bounty_enabled.Value)
                    return;

                var killTier = TierRewardScale.KillTier(victim);
                var killRegion = killTier == 0 ? ZoneControlManager.BountyRegionAt(victim) : null;
                if ((killTier == 0 && killRegion == null) || !BountyManager.PaysBounty(victim))
                    return;

                // The ONLINE player, never the weak reference the damage history holds (the BountyManager rule).
                var attacker = victim.DamageHistory.TopDamager?.TryGetPetOwnerOrAttacker() as Player;
                var player = attacker != null ? PlayerManager.GetOnlinePlayer(attacker.Guid) : null;
                if (player == null || !ReferenceEquals(player, attacker) || player.IsLoggingOut || player.IsMule || player.IsOlthoiPlayer)
                    return;
                // most killers carry no bounty: nothing to queue on their thread
                if (string.IsNullOrEmpty(player.GetProperty(PropertyString.ItemBountySlots)))
                    return;

                var creatureType = (uint?)victim.CreatureType;
                LandblockManager.RunOnThreadFor(player, ActionType.ItemBounty_Credit, () => Credit(player, killTier, killRegion, creatureType));
            }
            catch (Exception ex)
            {
                log.Error($"[ItemBounty] OnCreatureKilled: {ex}");
            }
        }

        /// <summary>
        /// The player-thread half of a kill: count it in every matching slot, save, then pay what it completed. A T11+ kill
        /// counts at the tier dip's rate; a region kill (<paramref name="killRegion"/>) counts in full. Where the KILL happened
        /// decides, not where the player stands (like Prestige Coins and kill XP: a pet's or DoT's kill that lands after its
        /// owner moved still counts). Every kill also retries a done mystery bounty that is still waiting for its reward
        /// table (TakeDue).
        /// </summary>
        private static void Credit(Player player, int killTier, string killRegion, uint? creatureType)
        {
            try
            {
                if (player.IsLoggingOut || !ReferenceEquals(PlayerManager.GetOnlinePlayer(player.Guid), player))
                    return;

                var raw = player.GetProperty(PropertyString.ItemBountySlots);
                if (string.IsNullOrEmpty(raw))
                    return;

                var slots = ParseSlots(raw);
                // a done mystery waiting for its reward table takes no more kills (it is already at its count)
                var matching = slots.Values.Where(s => !IsWaitingMystery(s) && Matches(s, killTier, killRegion, creatureType)).ToList();
                var credit = matching.Count == 0 ? 0 : killRegion != null ? 1.0 : TierRewardScale.CreditFor(player, killTier);
                if (!(credit > 0))
                    matching.Clear();
                if (matching.Count == 0 && !slots.Values.Any(IsWaitingMystery))
                    return;

                var due = new List<Slot>();
                var reachedNow = new HashSet<string>();
                foreach (var s in matching)
                {
                    var before = s.Progress;
                    var (after, done) = TierRewardScale.AddBountyProgress(s.Progress, credit, s.Kills);
                    if (done)
                    {
                        if (before < s.Kills) reachedNow.Add(s.Key);
                        s.Progress = s.Kills;
                        due.Add(s);
                        continue;
                    }

                    s.Progress = after;
                    var half = s.Kills / 2.0;
                    if (s.Kills >= 4 && before < half && after >= half)
                        Tell(player, $"Bounty #{s.Key} is halfway: {ProgressText(s)} in {PlaceShort(s)}.");
                }
                // a done mystery still waiting for its reward table retries on every counted kill, wherever it is
                foreach (var s in slots.Values)
                    if (IsWaitingMystery(s) && !due.Contains(s))
                        due.Add(s);

                var pay = TakeDue(player, slots, due, reachedNow);

                // Saved BEFORE anything is handed over: a failure while giving can never pay the same bounty twice.
                SaveSlots(player, slots);
                PayOut(player, pay, slots.Count);
            }
            catch (Exception ex)
            {
                log.Error($"[ItemBounty] Credit: {ex}");
            }
        }

        /// <summary>A mystery bounty that reached its count but couldn't be paid yet (its reward table had nothing to give).</summary>
        private static bool IsWaitingMystery(Slot s) => s.IsMysteryReward && s.Progress >= s.Kills;

        /// <summary>One completed bounty's payout: the slot it came from and the reward chosen for it.</summary>
        private readonly record struct Payout(Slot Slot, uint Wcid, int Amount, string Note);

        /// <summary>
        /// The done bounties that can be paid, each removed from <paramref name="slots"/> (the caller saves, then pays). A
        /// fixed reward always can. A MYSTERY reward is rolled NOW from its Zone Control table as it is now; when the table
        /// has nothing to give, the bounty stays done in its slot and is paid on a later try - the admins keep full control,
        /// nothing older is paid instead. The player hears about the wait once: when the bounty first reached its count
        /// (<paramref name="reachedNow"/>).
        /// </summary>
        private static List<Payout> TakeDue(Player player, Dictionary<string, Slot> slots, List<Slot> due, HashSet<string> reachedNow)
        {
            var pay = new List<Payout>();
            foreach (var s in due)
            {
                if (!s.IsMysteryReward)
                {
                    pay.Add(new Payout(s, s.RewardWcid, s.RewardAmount, ""));
                    slots.Remove(s.Key);
                    continue;
                }

                var options = MysteryOptions(s, out var tableUsed);
                var pick = PickReward(options, NextUnit());
                if (pick.HasValue)
                {
                    pay.Add(new Payout(s, pick.Value.Wcid, pick.Value.Amount, $" (mystery roll from table {tableUsed}: weight {pick.Value.Weight} of {options.Sum(o => (long)o.Weight)})"));
                    slots.Remove(s.Key);
                }
                else if (reachedNow.Contains(s.Key))
                {
                    Tell(player, $"Bounty #{s.Key} is done, but its mystery reward isn't set up yet. It stays done in slot #{s.Key} and pays as soon as it is; please tell an admin.");
                    log.Warn($"[ItemBounty] {player.Name} {s.Key} \"{s.Name}\" is done but mystery reward table '{s.RewardTableName}' has nothing to pay at {PlaceKey(s)} - waiting");
                }
            }
            return pay;
        }

        /// <summary>
        /// Pays each payout through BountyManager (into the pack, or held when it's full) and says the slot is free, with how
        /// many bounties are still going (<paramref name="goingAfter"/>, counted after the paid ones left their slots).
        /// </summary>
        private static void PayOut(Player player, List<Payout> pay, int goingAfter)
        {
            foreach (var p in pay)
            {
                var s = p.Slot;
                try
                {
                    var line = $"Bounty #{s.Key} complete! ({s.Name})" + (s.IsMysteryReward ? " The mystery reward is revealed:" : "");
                    log.Info($"[ItemBounty] complete {player.Name} #{s.Key} {PlaceShort(s)} kills={s.Kills} reward={p.Wcid}x{p.Amount}{p.Note} \"{s.Name}\"");
                    BountyManager.PayItemBounty(player, p.Wcid, p.Amount, line);
                    Tell(player, $"Slot #{s.Key} is free for a new bounty ({goingAfter} of {MaxActiveNow} going).");
                }
                catch (Exception ex)
                {
                    log.Error($"[ItemBounty] pay {s.Key} \"{s.Name}\" to {player.Name}: {ex}");
                }
            }
        }

        /// <summary>
        /// Pays the owner's done mysteries that were waiting for their reward table, if it has something now. On the
        /// owner's thread. Called when they look at /bounty, so a fixed table pays without another kill.
        /// </summary>
        private static void PayWaitingMysteries(Player owner)
        {
            var slots = LoadSlots(owner);
            var waiting = slots.Values.Where(IsWaitingMystery).ToList();
            if (waiting.Count == 0)
                return;
            var pay = TakeDue(owner, slots, waiting, new HashSet<string>());
            if (pay.Count == 0)
                return;
            SaveSlots(owner, slots);
            PayOut(owner, pay, slots.Count);
        }

        // ── /bounty (BountyCommands) ─────────────────────────────────────────

        /// <summary>
        /// The /bounty lines for a character's bounties, sent to <paramref name="viewer"/>: a header with how many are going
        /// out of how many they can have, one line per slot (#1..#cap, and any slot above the cap still holding one), then how
        /// to start and abandon one; one short line when none is going. Runs on the character's thread. When the owner looks,
        /// a done mystery bounty that was waiting for its reward table is paid first, if the table has something now. With
        /// item_bounty_enabled off, only the slots holding a bounty show (nothing counts or pays meanwhile).
        /// </summary>
        public static void ShowSlots(Player viewer, Player owner)
        {
            if (viewer == null || owner == null) return;
            LandblockManager.RunOnThreadFor(owner, ActionType.ItemBounty_Command, () =>
            {
                try
                {
                    var on = ServerConfig.item_bounty_enabled.Value;
                    var own = ReferenceEquals(viewer, owner);
                    // a done mystery waiting for its reward table pays now if the table has something again
                    if (on && own)
                        PayWaitingMysteries(owner);

                    var slots = LoadSlots(owner);
                    if (!on && slots.Count == 0) return;

                    var max = MaxActiveNow;
                    var whose = own ? "Your" : owner.Name + "'s";
                    if (slots.Count == 0)
                    {
                        Tell(viewer, own
                            ? $"Your bounties: none going (you can have up to {max}). "
                                + (LedgersOn ? "To start one, double-click the bounty and then click your Bounty Ledger." : "Double-click a bounty in your pack to start one.")
                            : $"{whose} bounties: none going.");
                        return;
                    }

                    Tell(viewer, $"{whose} bounties: {slots.Count} of {max} going.");
                    if (!on)
                        Tell(viewer, "Bounties are paused by the admins: nothing counts or pays until they are back on.");
                    foreach (var key in ShownKeys(slots, on ? max : 0))
                    {
                        slots.TryGetValue(key, out var s);
                        Tell(viewer, SlotLine(key, s));
                    }
                    if (own)
                        Tell(viewer, (LedgersOn
                                ? "Your Bounty Ledger shows all of this: double-click it. To start a bounty, double-click it and then click the ledger."
                                : "To start a bounty, double-click it in your pack.")
                            + " To abandon one (forfeits it): /bounty abandon <#>, e.g. /bounty abandon 2.");
                }
                catch (Exception ex)
                {
                    log.Error($"[ItemBounty] ShowSlots: {ex}");
                }
            });
        }

        /// <summary>The /bounty data and ItemBountyDisplay format version; bump it when a field is added or moved, or a field's
        /// values change meaning.
        /// v2 (2026-11-10): placeShort and placeName added at the end (region bounties: tier 0, place "VoD").
        /// v3 (2026-11-12, the Bounty Ledger rework): the same 16 fields in the same order; the slot keys are "1".."8" (shown as
        /// #1..#8) instead of R1, R2, B1, B2, and the header lists the slots that exist - #1 up to item_bounty_max_active
        /// (default 4), then any slot above it still holding a bounty.</summary>
        public const string DataVersion = "v3";

        /// <summary>
        /// One slot as the tracker reads it: "1|tier|kind|progress|kills|targetName|rewardWcid|amount|icon|overlay|underlay|
        /// bountyName|rewardName|rewardUse|placeShort|placeName", or "4|empty". kind: 1 Sweep, 2 Species; icon / overlay /
        /// underlay: the reward weenie's icon layers (0 = none); tier is 0 for a region bounty, whose place is e.g. "VoD" /
        /// "Valley of Death" ("T15" / "Tier 15" for a tier bounty). '|' and ';' never appear inside a field.
        /// </summary>
        internal static string DisplayEntry(string key, Slot s)
        {
            if (s == null)
                return key + "|empty";

            // a MYSTERY reward has no item yet: no icon ids (the tracker draws its own mark), the words "Mystery reward"
            var weenie = s.IsMysteryReward ? null : ACE.Database.DatabaseManager.World.GetCachedWeenie(s.RewardWcid);
            uint Did(PropertyDataId id) => weenie?.PropertiesDID != null && weenie.PropertiesDID.TryGetValue(id, out var v) ? v : 0;
            string Str(PropertyString id) => weenie?.PropertiesString != null && weenie.PropertiesString.TryGetValue(id, out var v) ? v : "";

            var fields = new[]
            {
                key,
                s.Tier.ToString(CultureInfo.InvariantCulture),
                s.Kind.ToString(CultureInfo.InvariantCulture),
                s.Progress.ToString("0.##", CultureInfo.InvariantCulture),
                s.Kills.ToString(CultureInfo.InvariantCulture),
                s.TargetName,
                s.RewardWcid.ToString(CultureInfo.InvariantCulture),
                s.RewardAmount.ToString(CultureInfo.InvariantCulture),
                Did(PropertyDataId.Icon).ToString(CultureInfo.InvariantCulture),
                Did(PropertyDataId.IconOverlay).ToString(CultureInfo.InvariantCulture),
                Did(PropertyDataId.IconUnderlay).ToString(CultureInfo.InvariantCulture),
                s.Name,
                s.IsMysteryReward ? MysteryRewardText : RoomAssignManager.ItemName(s.RewardWcid),
                s.IsMysteryReward ? "A mystery: revealed when the bounty is done." : Str(PropertyString.Use),
                PlaceShort(s),
                PlaceName(s),
            };
            return string.Join("|", fields.Select(f => (f ?? "").Replace('|', '/').Replace(';', ',').Replace("\r", "").Replace("\n", " ")));
        }

        /// <summary>
        /// The whole ItemBountyDisplay value: "v3|1,2,3,4" (the format version, then the slots that exist: ShownKeys), then
        /// one DisplayEntry per existing slot, all joined with ';'.
        /// </summary>
        internal static string BuildDisplay(IReadOnlyDictionary<string, Slot> slots)
        {
            var keys = ShownKeys(slots, MaxActiveNow);
            var parts = new List<string> { DataVersion + "|" + string.Join(",", keys) };
            foreach (var key in keys)
            {
                slots.TryGetValue(key, out var s);
                parts.Add(DisplayEntry(key, s));
            }
            return string.Join(";", parts);
        }

        /// <summary>
        /// Sends the character's ItemBountyDisplay to their own client when it changed (or always, with
        /// <paramref name="force"/>): a property update, so the client stores it silently - no chat line - and the Bounty
        /// Tracker reads it. Also stored on the character, so the login packet ([SendOnLogin]) carries the latest value.
        /// On the player's thread.
        /// </summary>
        private static void PushDisplay(Player player, IReadOnlyDictionary<string, Slot> slots, bool force)
        {
            var text = BuildDisplay(slots);
            if (!force && text == player.GetProperty(PropertyString.ItemBountyDisplay))
                return;

            player.SetProperty(PropertyString.ItemBountyDisplay, text);
            player.Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyString(player, PropertyString.ItemBountyDisplay, text));
        }

        /// <summary>
        /// /bounty sync: sends the character's ItemBountyDisplay to their client again, with NO chat output - the Bounty
        /// Tracker asks for it when it loads. /bounty itself stays the text view for players without the window.
        /// </summary>
        public static void Sync(Player player)
        {
            if (player == null) return;
            LandblockManager.RunOnThreadFor(player, ActionType.ItemBounty_Command, () =>
            {
                try
                {
                    PushDisplay(player, LoadSlots(player), force: true);
                }
                catch (Exception ex)
                {
                    log.Error($"[ItemBounty] Sync: {ex}");
                }
            });
        }

        /// <summary>
        /// /bounty data: the slots as machine-readable lines for the Bounty Tracker UB script, which reads and hides them, so
        /// it never has to scrape the human /bounty text (that mmo_meta_handler also reads). One line per slot that exists
        /// (ShownKeys), then an END line:
        ///   BOUNTYDATA 1|tier|kind|progress|kills|targetName|rewardWcid|amount|icon|overlay|underlay|bountyName|rewardName|rewardUse|placeShort|placeName
        ///   BOUNTYDATA 4|empty
        ///   BOUNTYDATA END|v3|1,2,3,4   (the format version, then the slots that exist)
        /// kind: 1 Sweep, 2 Species. icon / overlay / underlay: the reward weenie's icon layers (0 = none). '|' never appears
        /// inside a field.
        /// </summary>
        public static void SendData(Player player)
        {
            if (player == null) return;
            LandblockManager.RunOnThreadFor(player, ActionType.ItemBounty_Command, () =>
            {
                try
                {
                    var slots = LoadSlots(player);
                    var exist = ShownKeys(slots, MaxActiveNow);
                    foreach (var key in exist)
                    {
                        slots.TryGetValue(key, out var s);
                        Tell(player, "BOUNTYDATA " + DisplayEntry(key, s));
                    }
                    Tell(player, $"BOUNTYDATA END|{DataVersion}|{string.Join(",", exist)}");
                }
                catch (Exception ex)
                {
                    log.Error($"[ItemBounty] SendData: {ex}");
                }
            });
        }

        /// <summary>
        /// /bounty abandon (or remove, drop): asks the requester to confirm in the console. Nothing is abandoned until /bounty
        /// confirm with the code. <paramref name="owner"/> is the requester (a player) or another player (an admin's request).
        /// </summary>
        public static void RequestRemove(Player requester, Player owner, string key)
        {
            if (requester == null || owner == null) return;
            LandblockManager.RunOnThreadFor(owner, ActionType.ItemBounty_Command, () =>
            {
                try
                {
                    var slots = LoadSlots(owner);
                    var whose = ReferenceEquals(requester, owner) ? "" : owner.Name + "'s ";
                    if (!slots.TryGetValue(key, out var s))
                    {
                        Tell(requester, $"{(whose.Length > 0 ? whose + "bounty" : "Bounty")} #{key} is empty. Nothing to abandon.");
                        return;
                    }

                    Ask(requester, new Pending
                    {
                        Target = owner, SlotKey = key, Identity = s.Identity,
                        What = $"abandon {whose}bounty #{key}",
                    },
                    $"You are about to abandon {whose}bounty #{key}: {s.Name} -> {RewardText(s)} ({ProgressText(s)} in {PlaceShort(s)}). "
                    + "The bounty and its reward will be FORFEITED and cannot be recovered.");
                }
                catch (Exception ex)
                {
                    log.Error($"[ItemBounty] RequestRemove: {ex}");
                }
            });
        }

        // ── Console confirmation (abandoning a bounty) ───────────────────────

        /// <summary>
        /// One abandon waiting for /bounty confirm &lt;code&gt;: who asked, whose bounty it forfeits, and which one. (Before the
        /// 2026-11-12 rework, starting a bounty outside its place asked for a code too; it now asks for a second use on the
        /// Bounty Ledger instead - HandleUse.)
        /// </summary>
        private sealed class Pending
        {
            public Player Requester;      // who must confirm (a relog makes a new Player object: an old request no longer matches)
            public Player Target;         // whose slots it changes
            public string Code;
            public int Token;
            public double ExpiresAt;
            public string What;           // "abandon bounty #2" - for the expiry line
            public string SlotKey;
            public string Identity;       // the bounty asked about: a different one in the slot by then is never abandoned
        }

        /// <summary>Requester guid -> their one waiting request.</summary>
        private static readonly ConcurrentDictionary<uint, Pending> _pending = new();
        private static int _tokenSeq;

        /// <summary>
        /// The confirmation window in seconds (item_bounty_confirm_seconds, held to 10..600): for typing /bounty confirm after
        /// an abandon, and for the second use that starts a bounty (on the ledger in another tier or region, or without one).
        /// </summary>
        private static int ConfirmSeconds => (int)Math.Clamp(ServerConfig.item_bounty_confirm_seconds.Value, 10, 600);

        /// <summary>Stores the request (replacing any older one), prints the question and the code, and schedules the expiry line.</summary>
        private static void Ask(Player requester, Pending p, string question)
        {
            var now = Time.GetUnixTime();
            foreach (var kv in _pending)
                if (kv.Value.ExpiresAt < now)
                    _pending.TryRemove(kv);

            var seconds = ConfirmSeconds;
            p.Requester = requester;
            p.Code = ThreadSafeRandom.Next(1000, 9999).ToString(CultureInfo.InvariantCulture);
            p.Token = Interlocked.Increment(ref _tokenSeq);
            p.ExpiresAt = now + seconds;

            var replaced = _pending.TryGetValue(requester.Guid.Full, out var old) && old.ExpiresAt >= now;
            _pending[requester.Guid.Full] = p;

            if (replaced)
                Tell(requester, $"(Your earlier request to {old.What} was cancelled; this one replaces it.)");
            Tell(requester, $"{question} To confirm, type /bounty confirm {p.Code} within {seconds} seconds. To cancel, do nothing or type /bounty cancel.");

            // the expiry line runs on the WORLD queue, not the requester's (a player's queue stops when they log out, which
            // would leave the entry - and its Player objects - in _pending); OnLogout drops a leaver's entries at once
            var token = p.Token;
            var chain = new ActionChain();
            chain.AddDelaySeconds(seconds + 0.5);
            chain.AddAction(WorldManager.ActionQueue, ActionType.ItemBounty_ConfirmExpired, () =>
            {
                if (_pending.TryGetValue(requester.Guid.Full, out var cur) && cur.Token == token && _pending.TryRemove(new KeyValuePair<uint, Pending>(requester.Guid.Full, cur)))
                    Tell(requester, $"Your request to {cur.What} expired. Nothing was changed.");
            });
            chain.EnqueueChain();
        }

        /// <summary>
        /// A player leaving (Player.FinalizeLogout): drops their waiting confirmation, any admin request aimed at them, and a
        /// bounty armed for its second use, so no Player object outlives its session in <see cref="_pending"/>
        /// and a start never carries over a relog.
        /// </summary>
        public static void OnLogout(Player player)
        {
            if (player == null) return;
            _armed.TryRemove(player.Guid.Full, out _);
            _ledgerTried.TryRemove(player.Guid.Full, out _);
            _ledgerOpened.TryRemove(player.Guid.Full, out _);
            _pending.TryRemove(player.Guid.Full, out _);
            foreach (var kv in _pending)
                if (ReferenceEquals(kv.Value.Target, player))
                    _pending.TryRemove(kv);
        }

        /// <summary>/bounty confirm code: runs the requester's waiting request if the code matches and it hasn't expired.</summary>
        public static void Confirm(Player requester, string code)
        {
            if (requester == null) return;

            var now = Time.GetUnixTime();
            if (!_pending.TryGetValue(requester.Guid.Full, out var p) || !ReferenceEquals(p.Requester, requester) || p.ExpiresAt < now)
            {
                Tell(requester, "No request is waiting for confirmation. Nothing was changed.");
                return;
            }
            if (!string.Equals((code ?? "").Trim(), p.Code, StringComparison.Ordinal))
            {
                Tell(requester, $"That code doesn't match your request to {p.What}. Nothing was changed. The code is in the line that asked you to confirm.");
                return;
            }
            if (!_pending.TryRemove(new KeyValuePair<uint, Pending>(requester.Guid.Full, p)))
            {
                Tell(requester, "No request is waiting for confirmation. Nothing was changed.");
                return;
            }

            var target = p.Target;
            if (target == null || target.IsLoggingOut || !ReferenceEquals(PlayerManager.GetOnlinePlayer(target.Guid), target))
            {
                Tell(requester, "That character is no longer online. Nothing was changed.");
                return;
            }

            LandblockManager.RunOnThreadFor(target, ActionType.ItemBounty_Command, () =>
            {
                try
                {
                    DoRemove(p);
                }
                catch (Exception ex)
                {
                    log.Error($"[ItemBounty] Confirm: {ex}");
                }
            });
        }

        /// <summary>/bounty cancel: drops the requester's waiting request, if any.</summary>
        public static void Cancel(Player requester)
        {
            if (requester == null) return;
            if (_pending.TryRemove(requester.Guid.Full, out var p) && p.ExpiresAt >= Time.GetUnixTime())
                Tell(requester, $"Cancelled: your request to {p.What}. Nothing was changed.");
            else
                Tell(requester, "No request was waiting. Nothing was changed.");
        }

        /// <summary>A confirmed abandon, on the owner's thread: only if the SAME bounty is still in the slot.</summary>
        private static void DoRemove(Pending p)
        {
            var owner = p.Target;
            var slots = LoadSlots(owner);
            if (!slots.TryGetValue(p.SlotKey, out var s) || s.Identity != p.Identity)
            {
                Tell(p.Requester, $"Slot #{p.SlotKey} no longer holds that bounty (it may have completed). Nothing was abandoned.");
                return;
            }

            slots.Remove(p.SlotKey);
            SaveSlots(owner, slots);
            owner.SavePlayerToDatabase();

            var what = $"{s.Name} -> {RewardText(s)} forfeited ({ProgressText(s)}). Slot #{p.SlotKey} is free ({slots.Count} of {MaxActiveNow} going).";
            if (ReferenceEquals(p.Requester, owner))
            {
                Tell(owner, $"Bounty #{p.SlotKey} abandoned: {what}");
            }
            else
            {
                Tell(p.Requester, $"{owner.Name}'s bounty #{p.SlotKey} abandoned: {what}");
                Tell(owner, $"An admin ({p.Requester.Name}) removed your bounty #{p.SlotKey}: {what}");
                PlayerManager.BroadcastToAuditChannel(p.Requester, $"{p.Requester.Name} removed {owner.Name}'s bounty #{p.SlotKey}: {s.Name} ({ProgressText(s)}).");
            }
            log.Info($"[ItemBounty] abandon {owner.Name} #{p.SlotKey} by {p.Requester.Name} \"{s.Name}\" at {s.Progress.ToString("0.##", CultureInfo.InvariantCulture)}/{s.Kills}");
        }

        // ── Monster drops (owner 2026-11-10) ─────────────────────────────────
        //
        // Like the salvage bags (GearEssences.TryRollDrop): ONE roll per T11+ zone kill against the zone's bounty_odds
        // (1 in N, per rank: regular / leader / boss through the Ranks rows) decides WHETHER a bounty drops. On a hit, the
        // pool of the kill's place (its tier; PoolAt / WeightAt under "Places and pools" below) picks WHICH one by weight:
        // each weenie's own ItemBountyDropWeight in its own tier band by default, which a Zone Control place weight
        // (/zonecontrol bountyweight) replaces there, adds in another tier, or switches off. So Zone Control owns the total
        // flow (one live dial), and each bounty only its share; adding a bounty shares the drops instead of adding more. A
        // bounty weenie with no ItemBountyTier takes the kill's tier when it drops.
        //
        // REGION bounties (ItemBountyRegion) drop the same way in their region below T11, against the region's own odds
        // (BountyConfig.ItemBountyOdds, /zonecontrol regionbounty <key> itemodds N): one roll per kill there, then the
        // region's pool by weight. They never drop in a tier, and tier bounties never drop in a region.

        /// <summary>One droppable bounty weenie. Region: its BountyRegions key (a region bounty), else "" (a tier bounty).</summary>
        internal readonly record struct DropEntry(uint Wcid, int Weight, int MinTier, int MaxTier, bool FixedTier, string Name, string Region = "");

        // ── Places and pools (owner 2026-11-10: a weight PER PLACE - "heavily weighted in tier 16 ... really rare" in 15) ──
        //
        // A place is a tier "t11".."t25" or a bounty region key ("vod", "t10"). Each place has its own POOL: the bounties that
        // can drop there, each with its weight there. A bounty's weenie gives its own weight in its own places (its tier
        // band, its fixed tier or its region); Zone Control (/zonecontrol bountyweight) sets a weight per place, live, and an
        // off switch. A bounty can only ever be put where it can work: a fixed-tier bounty in its tier, a region bounty in
        // its region, a tier bounty in any tier - never a tier bounty in a region or the other way round.

        /// <summary>A tier's place key: 15 -> "t15".</summary>
        internal static string TierPlace(int tier) => "t" + tier.ToString(CultureInfo.InvariantCulture);

        /// <summary>The tier a place names ("t15" -> 15, only 11..25), else false (a region, or not a place).</summary>
        internal static bool TryPlaceTier(string place, out int tier)
        {
            tier = 0;
            return place != null && place.Length == 3 && (place[0] == 't' || place[0] == 'T')
                && int.TryParse(place.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out tier) && tier >= MinTier && tier <= MaxTier;
        }

        /// <summary>Every place, in order: t11..t25, then the bounty regions ("vod", "t10").</summary>
        internal static IReadOnlyList<string> AllPlaces { get; } =
            Enumerable.Range(MinTier, MaxTier - MinTier + 1).Select(TierPlace).Concat(ZoneControlManager.BountyRegions.Select(r => r.Key)).ToList();

        /// <summary>A place as players read it: "T15", "VoD", "T10".</summary>
        internal static string PlaceLabel(string place)
            => TryPlaceTier(place, out var t) ? "T" + t.ToString(CultureInfo.InvariantCulture) : RegionShort(place);

        /// <summary>
        /// Whether a bounty can be in <paramref name="place"/>'s pool at all: a region bounty only in its region; a tier bounty
        /// in any tier, a fixed-tier one only in its own tier.
        /// </summary>
        internal static bool CanDropAt(DropEntry e, string place)
        {
            if (!string.IsNullOrEmpty(e.Region))
                return string.Equals(e.Region, place, StringComparison.OrdinalIgnoreCase);
            return TryPlaceTier(place, out var tier) && (!e.FixedTier || tier == e.MinTier);
        }

        /// <summary>Whether <paramref name="place"/> is one of the bounty's OWN places (where its weenie's weight applies).</summary>
        internal static bool NaturalAt(DropEntry e, string place)
        {
            if (!CanDropAt(e, place)) return false;
            if (!string.IsNullOrEmpty(e.Region)) return true;
            TryPlaceTier(place, out var tier);
            return tier >= e.MinTier && tier <= e.MaxTier;
        }

        /// <summary>
        /// A bounty's weight in <paramref name="place"/>'s pool: 0 when it is off or can't drop there; else the place's
        /// Zone Control weight when one is set (0 = out of that pool); else its own weight in its own places, 0 elsewhere.
        /// <paramref name="set"/> is its Zone Control entry, or null.
        /// </summary>
        internal static int WeightAt(DropEntry e, string place, BountyPlaceWeights set)
        {
            if (set?.Off == true || !CanDropAt(e, place)) return 0;
            if (set?.Places != null && set.Places.TryGetValue(place, out var w)) return Math.Max(0, w);
            return NaturalAt(e, place) ? Math.Max(0, e.Weight) : 0;
        }

        /// <summary>
        /// One place's pool: every bounty with a weight above 0 there, with that weight. <paramref name="sets"/> is Zone
        /// Control's per-place weights (null = the weenies' own weights alone).
        /// </summary>
        internal static List<(DropEntry Entry, int Weight)> PoolAt(IEnumerable<DropEntry> table, string place, IReadOnlyDictionary<uint, BountyPlaceWeights> sets)
        {
            var pool = new List<(DropEntry Entry, int Weight)>();
            if (table == null || string.IsNullOrEmpty(place)) return pool;
            foreach (var e in table)
            {
                BountyPlaceWeights set = null;
                sets?.TryGetValue(e.Wcid, out set);
                var w = WeightAt(e, place, set);
                if (w > 0) pool.Add((e, w));
            }
            return pool;
        }

        /// <summary>The TIER bounties that drop at <paramref name="tier"/> by their weenies' own weights (no Zone Control).</summary>
        internal static List<DropEntry> Eligible(IEnumerable<DropEntry> table, int tier)
            => PoolAt(table, TierPlace(tier), null).Select(p => p.Entry).ToList();

        /// <summary>The REGION bounties that drop in <paramref name="region"/> by their weenies' own weights (no Zone Control).</summary>
        internal static List<DropEntry> EligibleInRegion(IEnumerable<DropEntry> table, string region)
            => PoolAt(table, region, null).Select(p => p.Entry).ToList();

        /// <summary>
        /// Places named by an admin, in <see cref="AllPlaces"/> order: "t15", a run "t11-t15", a comma list "t11,t13,vod", or
        /// a group "tiers" / "regions" / "all" (several tokens may be given). Null with <paramref name="error"/> on anything
        /// else - nothing is ever guessed.
        /// </summary>
        internal static List<string> ParsePlaces(IEnumerable<string> tokens, out string error)
        {
            error = null;
            var picked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parts = (tokens ?? Enumerable.Empty<string>()).SelectMany(t => (t ?? "").Split(',')).Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0);
            foreach (var part in parts)
            {
                if (part == "all") { picked.UnionWith(AllPlaces); continue; }
                if (part == "tiers") { picked.UnionWith(AllPlaces.Where(x => TryPlaceTier(x, out _))); continue; }
                if (part == "regions") { picked.UnionWith(AllPlaces.Where(x => !TryPlaceTier(x, out _))); continue; }
                var dash = part.IndexOf('-');
                if (dash > 0)
                {
                    if (!TryPlaceTier(part[..dash], out var lo) || !TryPlaceTier(part[(dash + 1)..], out var hi) || lo > hi)
                    {
                        error = $"'{part}' isn't a tier run: write it low to high, e.g. t11-t15.";
                        return null;
                    }
                    for (var t = lo; t <= hi; t++) picked.Add(TierPlace(t));
                    continue;
                }
                if (TryPlaceTier(part, out _) || ZoneControlManager.FindBountyRegion(part) != null) { picked.Add(part); continue; }
                error = $"'{part}' isn't a place: t11-t25, {string.Join(", ", ZoneControlManager.BountyRegions.Select(r => r.Key))}, a run like t11-t15, or tiers / regions / all.";
                return null;
            }
            if (picked.Count == 0)
            {
                error = "Name the places: e.g. t15, t11-t15, t11,t13,vod, tiers, regions or all.";
                return null;
            }
            return AllPlaces.Where(picked.Contains).ToList();
        }

        /// <summary>Places as one short token, runs joined: [t11, t12, t13, t15, vod] -> "t11-t13,t15,vod" (the editor's form).</summary>
        internal static string PlacesToken(IEnumerable<string> places)
        {
            var order = AllPlaces;
            var idx = (places ?? Enumerable.Empty<string>()).Select(x => IndexOfPlace(order, x)).Where(i => i >= 0).Distinct().OrderBy(i => i).ToList();
            var parts = new List<string>();
            for (var i = 0; i < idx.Count; i++)
            {
                var j = i;
                while (j + 1 < idx.Count && idx[j + 1] == idx[j] + 1 && TryPlaceTier(order[idx[j + 1]], out _) && TryPlaceTier(order[idx[i]], out _)) j++;
                parts.Add(j > i ? order[idx[i]] + "-" + order[idx[j]] : order[idx[i]]);
                i = j;
            }
            return string.Join(",", parts);
        }

        /// <summary>The index of a place in <paramref name="order"/> (case-insensitive), or -1.</summary>
        private static int IndexOfPlace(IReadOnlyList<string> order, string place)
        {
            for (var i = 0; i < order.Count; i++)
                if (string.Equals(order[i], place, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        /// <summary>The index <paramref name="u"/> in [0, 1) lands on when each entry has a slice its weight wide.</summary>
        internal static int WeightedPick(IReadOnlyList<int> weights, double u)
        {
            long total = weights.Sum(w => (long)Math.Max(0, w));
            if (total <= 0) return -1;
            var roll = Math.Clamp(u, 0, 0.999999999) * total;
            for (var i = 0; i < weights.Count; i++)
            {
                roll -= Math.Max(0, weights[i]);
                if (roll < 0) return i;
            }
            for (var i = weights.Count - 1; i >= 0; i--)   // floating-point tail: never a zero-weight entry
                if (weights[i] > 0) return i;
            return -1;
        }

        /// <summary>
        /// A droppable bounty weenie's entry - with the WEENIE's own drop weight (Zone Control's per-place weights are applied
        /// at each roll, PoolAt) - or null when it can't drop: broken terms, or no weight of its own unless
        /// <paramref name="overridden"/> (Zone Control gives it a weight somewhere).
        /// </summary>
        internal static DropEntry? EntryFor(ACE.Entity.Models.Weenie w, bool overridden = false)
        {
            if (w?.PropertiesInt == null) return null;
            int Int(PropertyInt p, int dflt) => w.PropertiesInt.TryGetValue(p, out var v) ? v : dflt;

            var weight = Math.Max(0, Int(PropertyInt.ItemBountyDropWeight, 0));
            if (weight <= 0 && !overridden) return null;
            // a reward: one fixed item, or a mystery naming its Zone Control reward table
            string table = null;
            w.PropertiesString?.TryGetValue(PropertyString.ItemBountyRewardTable, out table);
            var mysteryReward = (Int(PropertyInt.ItemBountyMystery, 0) & MysteryReward) != 0;
            if (mysteryReward ? !ZoneControlManager.IsValidRewardTableName((table ?? "").Trim().ToLowerInvariant()) : Int(PropertyInt.ItemBountyRewardWcid, 0) <= 0) return null;
            // the same terms TermsOf asks of a made item, so a bounty nobody could apply never drops
            var kills = Int(PropertyInt.ItemBountyKills, 0);
            if (kills <= 0 && Int(PropertyInt.ItemBountyKillsMin, 0) <= 0 && Int(PropertyInt.ItemBountyKillsMax, 0) <= 0) return null;
            if (kills > BountyManager.MaxKills) return null;
            var kind = Int(PropertyInt.ItemBountyKind, KindSweep);
            if (kind != KindSweep && kind != KindSpecies) return null;
            if (kind == KindSpecies && Int(PropertyInt.ItemBountyTarget, 0) <= 0) return null;

            string name = null;
            w.PropertiesString?.TryGetValue(PropertyString.Name, out name);
            name ??= "WCID " + w.WeenieClassId;

            // a region bounty: drops in its region only (an unknown region key can't drop anywhere)
            string region = null;
            w.PropertiesString?.TryGetValue(PropertyString.ItemBountyRegion, out region);
            region = (region ?? "").Trim().ToLowerInvariant();
            if (region.Length > 0)
                return ZoneControlManager.FindBountyRegion(region) == null ? null : new DropEntry(w.WeenieClassId, weight, 0, 0, true, name, region);

            var tier = Int(PropertyInt.ItemBountyTier, 0);
            var fixedTier = tier >= MinTier && tier <= MaxTier;
            var min = fixedTier ? tier : Math.Clamp(Int(PropertyInt.ItemBountyDropTierMin, MinTier), MinTier, MaxTier);
            var max = fixedTier ? tier : Math.Clamp(Int(PropertyInt.ItemBountyDropTierMax, MaxTier), min, MaxTier);
            return new DropEntry(w.WeenieClassId, weight, min, max, fixedTier, name);
        }

        private static volatile List<DropEntry> _dropTable = new();
        private static double _dropTableBuiltAt = double.NegativeInfinity;
        private static int _dropTableBuilding;
        private const double DropTableRefreshSeconds = 300;

        /// <summary>
        /// Rebuilds the drop table on the next roll (in the background) - after a Zone Control weight change, so a bounty
        /// that only Zone Control gives a weight joins it.
        /// </summary>
        public static void InvalidateDropTable() => _dropTableBuiltAt = double.NegativeInfinity;

        /// <summary>The background read in flight or last finished (/bounty droptable waits for it).</summary>
        private static System.Threading.Tasks.Task _dropTableBuild = System.Threading.Tasks.Task.CompletedTask;

        /// <summary>
        /// The droppable bounties: every world weenie with an ItemBountyDropWeight, and every one Zone Control gives a weight,
        /// read off the DB in the BACKGROUND - never on a landblock or the world thread - at server start (WarmDropTable),
        /// then every 5 minutes, and after a Zone Control weight change. A roll always uses the table as last read.
        /// </summary>
        internal static List<DropEntry> DropTable()
        {
            var now = Time.GetUnixTime();
            if (now - _dropTableBuiltAt > DropTableRefreshSeconds && Interlocked.Exchange(ref _dropTableBuilding, 1) == 0)
            {
                _dropTableBuiltAt = now;
                _dropTableBuild = System.Threading.Tasks.Task.Run(BuildDropTable);
            }
            return _dropTable;
        }

        /// <summary>
        /// Starts the first read of the drop table (server start, once the world database is up), so the first kill whose
        /// bounty roll hits after a restart already has the table.
        /// </summary>
        public static void WarmDropTable()
        {
            InvalidateDropTable();
            DropTable();
        }

        /// <summary>One read of the droppable bounties (a background task): the DB's weighted weenies plus Zone Control's.</summary>
        private static void BuildDropTable()
        {
            try
            {
                List<uint> ids;
                using (var ctx = new ACE.Database.Models.World.WorldDbContext())
                    ids = ctx.WeeniePropertiesInt.Where(p => p.Type == (ushort)PropertyInt.ItemBountyDropWeight && p.Value > 0).Select(p => p.ObjectId).Distinct().ToList();
                // plus every bounty Zone Control gives a weight (/zonecontrol bountyweight), even one with none of its own
                var sets = ZoneControlManager.BountyPlaceWeightSnapshot();
                static bool Gives(BountyPlaceWeights set) => set?.Places != null && set.Places.Values.Any(w => w > 0);
                var list = new List<DropEntry>();
                foreach (var id in ids.Union(sets.Where(kv => Gives(kv.Value)).Select(kv => kv.Key)))
                {
                    var entry = EntryFor(ACE.Database.DatabaseManager.World.GetCachedWeenie(id), sets.TryGetValue(id, out var set) && Gives(set));
                    if (entry.HasValue) list.Add(entry.Value);
                    else log.Warn($"[ItemBounty] drop table: weenie {id} has a drop weight but no complete bounty terms - skipped");
                }
                _dropTable = list;
            }
            catch (Exception ex)
            {
                log.Error($"[ItemBounty] drop table: {ex}");
            }
            finally
            {
                Interlocked.Exchange(ref _dropTableBuilding, 0);
            }
        }

        /// <summary>
        /// A bounty whose weenie has no tier takes <paramref name="tier"/> (the kill's, or an admin's /bounty make), and its
        /// description is rewritten with the full terms.
        /// </summary>
        internal static void StampTier(WorldObject item, int tier)
        {
            item.SetProperty(PropertyInt.ItemBountyTier, tier);
            var terms = TermsOf(item);
            if (terms != null)
                item.SetProperty(PropertyString.LongDesc, Describe(terms));
        }

        /// <summary>
        /// The kill's bounty roll (Creature_Death, next to the salvage bag roll): null, or a new bounty item for the corpse.
        /// <paramref name="zoneLoot"/> is the kill's rank-resolved zone profile (bounty_odds per rank).
        /// </summary>
        public static WorldObject TryRollDrop(ZoneScaling.EvaluatedProfile zoneLoot, Creature killed, string killerName)
        {
            if (!ServerConfig.item_bounty_enabled.Value || zoneLoot == null)
                return null;

            var denom = ZoneStatResolver.OddsToInt(zoneLoot.Get(ZoneScaling.ZoneStat.BountyOdds, 0.0));   // 0 = unset: never
            // the region roll's and the kill credit's gate too: a kill that pays no XP or luminance drops no bounty
            if (denom < 1 || ThreadSafeRandom.Next(1, denom) != 1 || !BountyManager.PaysBounty(killed))
                return null;

            var tier = TierRewardScale.KillTier(killed);
            if (tier == 0)
                return null;

            var pool = PoolAt(DropTable(), TierPlace(tier), ZoneControlManager.BountyPlaceWeightSnapshot());
            var at = WeightedPick(pool.Select(c => c.Weight).ToList(), NextUnit());
            if (at < 0)
                return null;

            var (pick, pickWeight) = pool[at];
            var item = Factories.WorldObjectFactory.CreateNewWorldObject(pick.Wcid);   // the kill count rolls here (OnNewItem)
            if (item == null)
            {
                log.Warn($"[ItemBounty] drop: rolled wcid {pick.Wcid} but it has no weenie");
                return null;
            }
            if (!pick.FixedTier)
                StampTier(item, tier);

            log.Info($"[ItemBounty] drop {killerName ?? "(unknown)"} killed {killed?.Name} ({killed?.WeenieClassId}) T{tier} -> {item.Name} ({pick.Wcid}) "
                + $"kills={item.GetProperty(PropertyInt.ItemBountyKills)?.ToString(CultureInfo.InvariantCulture) ?? "mystery"} odds 1 in {denom}, weight {pickWeight} of {pool.Sum(c => (long)c.Weight)}");
            return item;
        }

        /// <summary>
        /// A REGION kill's bounty roll (Creature_Death, after the treasure, for every corpse): null unless the kill is in a
        /// bounty region below T11 (Valley of Death, Thaelaryn Island), pays a bounty (XP, not a critter), and hits the
        /// region's ItemBountyOdds; then one of the region's bounty weenies, by drop weight.
        /// </summary>
        public static WorldObject TryRollRegionDrop(Creature killed, string killerName)
        {
            if (!ServerConfig.item_bounty_enabled.Value || killed == null || killed is Player)
                return null;

            var region = ZoneControlManager.BountyRegionAt(killed);
            if (region == null)
                return null;
            var denom = ZoneControlManager.RegionItemBountyOdds(region);   // 0 = none drop there
            if (denom < 1 || !BountyManager.PaysBounty(killed) || ThreadSafeRandom.Next(1, denom) != 1)
                return null;

            var pool = PoolAt(DropTable(), region, ZoneControlManager.BountyPlaceWeightSnapshot());
            var at = WeightedPick(pool.Select(c => c.Weight).ToList(), NextUnit());
            if (at < 0)
                return null;

            var (pick, pickWeight) = pool[at];
            var item = Factories.WorldObjectFactory.CreateNewWorldObject(pick.Wcid);   // the kill count rolls here (OnNewItem)
            if (item == null)
            {
                log.Warn($"[ItemBounty] drop: rolled wcid {pick.Wcid} but it has no weenie");
                return null;
            }

            log.Info($"[ItemBounty] drop {killerName ?? "(unknown)"} killed {killed.Name} ({killed.WeenieClassId}) in {RegionShort(region)} -> {item.Name} ({pick.Wcid}) "
                + $"kills={item.GetProperty(PropertyInt.ItemBountyKills)?.ToString(CultureInfo.InvariantCulture) ?? "mystery"} odds 1 in {denom}, weight {pickWeight} of {pool.Sum(c => (long)c.Weight)}");
            return item;
        }

        /// <summary>
        /// /bounty droptable (Developer): re-reads the droppable bounties in the background (the DB read never runs on the
        /// world thread), then - on the viewer's thread - lists each tier's and region's shares with the weights as they are
        /// now, and the Zone Control weight overrides in force.
        /// </summary>
        public static void ShowDropTable(Player viewer)
        {
            if (viewer == null) return;
            InvalidateDropTable();
            DropTable();
            var build = _dropTableBuild;
            System.Threading.Tasks.Task.Run(async () =>
            {
                try { await build.WaitAsync(TimeSpan.FromSeconds(30)); }
                catch (Exception ex) { log.Warn($"[ItemBounty] /bounty droptable: the read didn't finish ({ex.Message}); showing the table as last read"); }
                LandblockManager.RunOnThreadFor(viewer, ActionType.ItemBounty_Command, () => ShowDropTableNow(viewer));
            });
        }

        /// <summary>The /bounty droptable lines from the table as last read (see <see cref="ShowDropTable"/>).</summary>
        private static void ShowDropTableNow(Player viewer)
        {
            var table = DropTable();
            var sets = ZoneControlManager.BountyPlaceWeightSnapshot();
            var pools = AllPlaces.Select(place => (Place: place, Pool: PoolAt(table, place, sets))).Where(x => x.Pool.Count > 0).ToList();
            if (pools.Count == 0)
            {
                Tell(viewer, "No bounty drops anywhere: none has a drop weight (its weenie's ItemBountyDropWeight, or /zonecontrol bountyweight <wcid> <weight> <places>).");
                return;
            }
            if (sets.Count > 0)
                Tell(viewer, $"Zone Control sets drop weights for {sets.Count} bount{(sets.Count == 1 ? "y" : "ies")}: /zonecontrol bountyweight list shows them.");

            Tell(viewer, "When a kill's bounty roll hits, each bounty's share of that place's pool (/zonecontrol bountypool <place> for one pool):");
            foreach (var (place, pool) in pools)
            {
                var total = (double)pool.Sum(x => (long)x.Weight);
                var shares = string.Join(", ", pool.OrderByDescending(x => x.Weight).Select(x => $"{x.Entry.Name} {100 * x.Weight / total:0.#}%"));
                if (TryPlaceTier(place, out _))
                {
                    Tell(viewer, $"{PlaceLabel(place)}: {shares}");
                    continue;
                }
                // the regions below T11: their own odds (regionbounty <key> itemodds N)
                var odds = ZoneControlManager.RegionItemBountyOdds(place);
                Tell(viewer, $"{RegionName(place)} ({(odds > 0 ? $"1 in {odds:N0} kills" : $"none drop: /zonecontrol regionbounty {place} itemodds N")}): {shares}");
            }
        }

        // ── /zonecontrol bountyweight and bountypool (ZoneControlCommands) ─────

        /// <summary>One /zonecontrol bountyweight change: the reply lines, the audit line (null = nothing changed), or the refusal.</summary>
        public sealed class WeightEditResult
        {
            /// <summary>What to tell the admin, in order (the new state, its bw line, then the undo lines).</summary>
            public List<string> Lines { get; } = new();

            /// <summary>The audit-channel line, old -> new; null when nothing changed or the change was refused.</summary>
            public string Audit { get; set; }

            /// <summary>Why nothing was changed (bad input, a place it can't drop at); null when the change was made.</summary>
            public string Error { get; set; }
        }

        /// <summary>A bounty weenie's own ItemBountyDropWeight (world cache), 0 when it has none or doesn't exist.</summary>
        private static int OwnWeight(uint wcid)
        {
            var w = ACE.Database.DatabaseManager.World.GetCachedWeenie(wcid);
            return w?.PropertiesInt != null && w.PropertiesInt.TryGetValue(PropertyInt.ItemBountyDropWeight, out var v) ? Math.Max(0, v) : 0;
        }

        /// <summary>
        /// "bw 99985001 own=10 off=no t15=1 t16=1000 # Bounty of Rebirth": one bounty's live state as the Bounty Editor loads
        /// it (Drop pools tab, "Load live state"). Places in <see cref="AllPlaces"/> order; only the ones Zone Control sets.
        /// </summary>
        internal static string BwLine(uint wcid, int own, BountyPlaceWeights set, string name)
        {
            var sb = new StringBuilder();
            sb.Append("bw ").Append(wcid.ToString(CultureInfo.InvariantCulture))
              .Append(" own=").Append(Math.Max(0, own).ToString(CultureInfo.InvariantCulture))
              .Append(" off=").Append(set?.Off == true ? "yes" : "no");
            if (set?.Places != null)
                foreach (var place in AllPlaces)
                    if (set.Places.TryGetValue(place, out var w))
                        sb.Append(' ').Append(place).Append('=').Append(w.ToString(CultureInfo.InvariantCulture));
            return sb.Append(" # ").Append(Clean(name)).ToString();
        }

        /// <summary>A bounty's Zone Control state in words, for the audit line: "off, t15=1, t16=1000" / "its own weights".</summary>
        internal static string WeightsText(BountyPlaceWeights set)
        {
            var parts = new List<string>();
            if (set?.Off == true) parts.Add("OFF");
            if (set?.Places != null)
                parts.AddRange(AllPlaces.Where(set.Places.ContainsKey).Select(x => x + "=" + set.Places[x].ToString(CultureInfo.InvariantCulture)));
            return parts.Count == 0 ? "its own weights" : string.Join(", ", parts);
        }

        /// <summary>
        /// The commands that put a bounty's weights back from <paramref name="after"/> to <paramref name="before"/>: the off
        /// switch, then one absolute line per value ("clear" where no weight was set), places joined the editor's way.
        /// </summary>
        internal static List<string> UndoLines(uint wcid, BountyPlaceWeights before, BountyPlaceWeights after)
        {
            var lines = new List<string>();
            var id = wcid.ToString(CultureInfo.InvariantCulture);
            if ((before?.Off == true) != (after?.Off == true))
                lines.Add($"/zonecontrol bountyweight {id} {(before?.Off == true ? "off" : "on")}");
            var byValue = new Dictionary<string, List<string>>();
            var order = new List<string>();
            foreach (var place in AllPlaces)
            {
                int? b = before?.Places != null && before.Places.TryGetValue(place, out var bv) ? bv : null;
                int? a = after?.Places != null && after.Places.TryGetValue(place, out var av) ? av : null;
                if (b == a) continue;
                var key = b.HasValue ? b.Value.ToString(CultureInfo.InvariantCulture) : "clear";
                if (!byValue.TryGetValue(key, out var list)) { byValue[key] = list = new List<string>(); order.Add(key); }
                list.Add(place);
            }
            foreach (var key in order)
                lines.Add($"/zonecontrol bountyweight {id} {key} {PlacesToken(byValue[key])}");
            return lines;
        }

        /// <summary>Where a bounty can drop at all, in words (the reason a place is refused).</summary>
        private static string WhereItCanDrop(DropEntry e)
        {
            if (!string.IsNullOrEmpty(e.Region))
                return $"a {RegionName(e.Region)} bounty only drops in {e.Region}";
            if (e.FixedTier)
                return $"it always has Tier {e.MinTier.ToString(CultureInfo.InvariantCulture)}, so it only drops in {TierPlace(e.MinTier)}";
            return "a tier bounty drops in tiers (t11-t25), never in a region";
        }

        /// <summary>
        /// /zonecontrol bountyweight list: one <see cref="BwLine"/> per droppable bounty and per bounty Zone Control sets,
        /// by WCID - the live state the Bounty Editor loads.
        /// </summary>
        public static List<string> WeightListLines()
        {
            var sets = ZoneControlManager.BountyPlaceWeightSnapshot();
            var lines = new List<string>();
            foreach (var wcid in DropTable().Select(e => e.Wcid).Union(sets.Keys).Distinct().OrderBy(w => w))
            {
                sets.TryGetValue(wcid, out var set);
                lines.Add(BwLine(wcid, OwnWeight(wcid), set, RoomAssignManager.ItemName(wcid)));
            }
            return lines;
        }

        /// <summary>
        /// /zonecontrol bountyweight wcid: one bounty in every place it can drop - its weight there, where that comes from,
        /// and its share of that pool - then its bw line. Null when the weenie isn't a bounty that could drop.
        /// </summary>
        public static List<string> WeightViewLines(uint wcid)
        {
            var entry = EntryFor(ACE.Database.DatabaseManager.World.GetCachedWeenie(wcid), overridden: true);
            if (!entry.HasValue) return null;
            var e = entry.Value;
            var sets = ZoneControlManager.BountyPlaceWeightSnapshot();
            sets.TryGetValue(wcid, out var set);
            var table = DropTable().Where(x => x.Wcid != wcid).Append(e).ToList();

            var home = !string.IsNullOrEmpty(e.Region) ? RegionName(e.Region)
                : e.FixedTier ? $"Tier {e.MinTier}" : $"the tier it drops in (its own band T{e.MinTier}-T{e.MaxTier})";
            var lines = new List<string> { $"{e.Name} ({wcid}): {home}, its own weight {e.Weight:N0}{(set?.Off == true ? ". OFF: it drops nowhere until /zonecontrol bountyweight " + wcid + " on" : "")}." };
            var parts = new List<string>();
            foreach (var place in AllPlaces.Where(x => CanDropAt(e, x)))
            {
                var hasSet = set?.Places != null && set.Places.ContainsKey(place);
                if (!hasSet && !NaturalAt(e, place)) continue;
                var w = WeightAt(e, place, set);
                var pool = PoolAt(table, place, sets);
                var total = pool.Sum(x => (long)x.Weight);
                var source = hasSet ? (NaturalAt(e, place) ? "set" : "added") : "own";
                parts.Add(w > 0 && total > 0 ? $"{PlaceLabel(place)} {w:N0} ({source}, {100.0 * w / total:0.#}%)" : $"{PlaceLabel(place)} 0 ({source})");
            }
            lines.Add(parts.Count > 0 ? "  " + string.Join(" | ", parts) : "  It is in no pool.");
            lines.Add(BwLine(wcid, e.Weight, set, e.Name));
            return lines;
        }

        /// <summary>
        /// /zonecontrol bountyweight wcid (weight | clear) places, and wcid (off | on | reset): changes one bounty's Zone Control
        /// weights. A weight is refused where the bounty can't drop (a tier bounty in a region, a region bounty in a tier, a
        /// fixed-tier bounty in another tier) - except under a group (all / tiers / regions), which just skips those. Saved at
        /// once; the reply gives the new state and the undo commands. The caller checks Admin access and posts the audit.
        /// </summary>
        public static WeightEditResult EditWeights(uint wcid, string action, IReadOnlyList<string> placeTokens)
        {
            var r = new WeightEditResult();
            var id = wcid.ToString(CultureInfo.InvariantCulture);
            action = (action ?? "").Trim().ToLowerInvariant();
            placeTokens ??= Array.Empty<string>();
            var name = RoomAssignManager.ItemName(wcid);
            var weenie = ACE.Database.DatabaseManager.World.GetCachedWeenie(wcid);
            var entry = EntryFor(weenie, overridden: true);

            int? value = null;
            var clear = action == "clear";
            var switchAction = action == "off" || action == "on" || action == "reset";
            if (switchAction)
            {
                if (placeTokens.Count > 0) { r.Error = $"'{action}' takes no places: /zonecontrol bountyweight {id} {action}"; return r; }
            }
            else if (!clear)
            {
                if (!int.TryParse(action, NumberStyles.None, CultureInfo.InvariantCulture, out var v) || v > BountyRewardTable.MaxWeight)
                {
                    r.Error = $"'{action}' isn't a weight: a whole number 0-{BountyRewardTable.MaxWeight:N0}, clear, off, on or reset.";
                    return r;
                }
                value = v;
            }
            // a weight only on a bounty that could drop (the drop table's own test); clear / on / reset always work, to clean
            // up. off needs a bounty too - or an entry to switch, or one the drop table still holds - so a mistyped WCID in an
            // emergency is refused instead of looking done while the real bounty keeps dropping.
            if (action == "off" && !entry.HasValue && !ZoneControlManager.BountyPlaceWeightSnapshot().ContainsKey(wcid) && !DropTable().Any(x => x.Wcid == wcid))
            {
                r.Error = weenie == null ? $"No weenie {id}: nothing to turn off. Nothing changed."
                    : $"{name} ({id}) isn't a bounty that could drop (incomplete bounty terms), so there is nothing to turn off. Nothing changed.";
                return r;
            }
            if (value.HasValue && !entry.HasValue)
            {
                r.Error = weenie == null ? $"No weenie {id}. Nothing changed."
                    : $"{name} ({id}) could never drop as a bounty: it needs complete bounty terms - a reward (or a valid mystery table name), a kill count or range, a known region if it names one. Nothing changed.";
                return r;
            }

            List<string> places = null;
            var skipped = new List<string>();
            if (value.HasValue || clear)
            {
                places = ParsePlaces(placeTokens, out var placeError);
                if (places == null) { r.Error = placeError; return r; }
                if (value.HasValue)
                {
                    var bad = places.Where(x => !CanDropAt(entry.Value, x)).ToList();
                    if (bad.Count > 0)
                    {
                        var grouped = placeTokens.SelectMany(t => (t ?? "").Split(',')).Any(t => t.Trim().ToLowerInvariant() is "all" or "tiers" or "regions");
                        if (!grouped)
                        {
                            r.Error = $"{name} ({id}) can't drop at {string.Join(", ", bad.Select(PlaceLabel))}: {WhereItCanDrop(entry.Value)}. Nothing changed.";
                            return r;
                        }
                        skipped = bad;
                        places = places.Except(bad).ToList();
                        if (places.Count == 0) { r.Error = $"{name} ({id}) can't drop at any of those places: {WhereItCanDrop(entry.Value)}. Nothing changed."; return r; }
                    }
                }
            }

            var after = ZoneControlManager.EditBountyPlaceWeights(wcid, set =>
            {
                switch (action)
                {
                    case "off": set.Off = true; break;
                    case "on": set.Off = false; break;
                    case "reset": set.Off = false; set.Places.Clear(); break;
                    case "clear": foreach (var x in places) set.Places.Remove(x); break;
                    default: foreach (var x in places) set.Places[x] = value.Value; break;
                }
                return null;
            }, out var before, out _);
            InvalidateDropTable();   // a bounty only Zone Control gives a weight joins the table on the next roll

            if (WeightsText(before) == WeightsText(after))
            {
                r.Lines.Add($"{name} ({id}): nothing changed ({WeightsText(after)}).");
                return r;
            }

            var token = places != null ? PlacesToken(places) : "";
            r.Lines.Add(action switch
            {
                "off" => $"{name} ({id}) is OFF: it drops nowhere (its weights are kept for when it is back on).",
                "on" => $"{name} ({id}) is on again: it drops by its weights.",
                "reset" => $"{name} ({id}): every Zone Control weight removed - its own weight {OwnWeight(wcid):N0} in its own places again.",
                "clear" => $"{name} ({id}): back to its own weight at {token}.",
                _ => $"{name} ({id}): weight {value.Value:N0} at {token}{(value.Value == 0 ? " - out of those pools" : "")}."
                    + (skipped.Count > 0 ? $" Skipped {string.Join(", ", skipped.Select(PlaceLabel))}: {WhereItCanDrop(entry.Value)}." : ""),
            });
            if (entry.HasValue && after.Off == false && places != null)
            {
                var table = DropTable().Where(x => x.Wcid != wcid).Append(entry.Value).ToList();
                var sets = ZoneControlManager.BountyPlaceWeightSnapshot();
                var shares = places.Select(x =>
                {
                    var pool = PoolAt(table, x, sets);
                    var total = pool.Sum(y => (long)y.Weight);
                    var w = WeightAt(entry.Value, x, after);
                    return total > 0 ? $"{PlaceLabel(x)} {100.0 * w / total:0.#}%" : $"{PlaceLabel(x)} EMPTY";
                });
                r.Lines.Add("  Its share of each pool now: " + string.Join(", ", shares) + ".");
            }
            r.Lines.Add(BwLine(wcid, OwnWeight(wcid), after, name));
            foreach (var undo in UndoLines(wcid, before, after))
                r.Lines.Add("  Undo: " + undo);
            r.Audit = $"bountyweight {name} ({id}) {action}{(token.Length > 0 ? " " + token : "")}: {WeightsText(before)} -> {WeightsText(after)}";
            return r;
        }

        /// <summary>
        /// /zonecontrol bountypool place: one place's pool - every bounty that can drop there with its weight, share and
        /// where the weight comes from (its own / set for that place / added there), and for a region "1 in N kills" from
        /// its itemodds. Null with <paramref name="error"/> when the text isn't exactly one place.
        /// </summary>
        public static List<string> PoolLines(string placeText, out string error)
        {
            var parsed = ParsePlaces(new[] { placeText }, out error);
            if (parsed == null) return null;
            if (parsed.Count != 1) { error = "Name one place: t11-t25, " + string.Join(", ", ZoneControlManager.BountyRegions.Select(x => x.Key)) + "."; return null; }

            var place = parsed[0];
            var sets = ZoneControlManager.BountyPlaceWeightSnapshot();
            var pool = PoolAt(DropTable(), place, sets).OrderByDescending(x => x.Weight).ToList();
            var total = pool.Sum(x => (long)x.Weight);
            var region = !TryPlaceTier(place, out var tier);
            var odds = region ? ZoneControlManager.RegionItemBountyOdds(place) : 0;
            var lines = new List<string>
            {
                pool.Count == 0
                    ? $"{PlaceLabel(place)} pool: EMPTY - when a kill's bounty roll hits here, nothing drops. Add one: /zonecontrol bountyweight <wcid> <weight> {place}"
                    : $"{PlaceLabel(place)} pool: {pool.Count} bount{(pool.Count == 1 ? "y" : "ies")}, total weight {total:N0}.",
            };
            foreach (var (e, w) in pool)
            {
                sets.TryGetValue(e.Wcid, out var set);
                var source = set?.Places != null && set.Places.ContainsKey(place) ? (NaturalAt(e, place) ? $"set for {PlaceLabel(place)}" : $"added to {PlaceLabel(place)}") : "its own weight";
                var perKill = region && odds > 0 ? $", 1 in {Math.Round((double)odds * total / w):N0} kills" : "";
                lines.Add($"  {e.Name} ({e.Wcid}): {w:N0} = {100.0 * w / total:0.#}% ({source}){perKill}");
            }
            lines.Add(region
                ? (odds > 0 ? $"A {RegionName(place)} kill rolls for a bounty 1 in {odds:N0} (/zonecontrol regionbounty {place} itemodds)." : $"No {RegionName(place)} kill rolls for a bounty yet: /zonecontrol regionbounty {place} itemodds N.")
                : $"How often a T{tier} kill rolls for a bounty: bounty_odds, per zone and rank (/zonecontrol default {tier} show).");
            return lines;
        }

        /// <summary>
        /// /bounty make wcid [tier] (Developer): a bounty into the admin's pack, made the way loot makes it (its kill count
        /// rolled; a mystery count or reward left for later) and - for a bounty that takes the kill's
        /// tier - stamped with <paramref name="tier"/> (default: where the admin stands, else 11). A region bounty ignores
        /// the tier. The reply names the item's guid, its place and its terms (a mystery's range and choices, for testing).
        /// </summary>
        public static void Make(Player admin, uint wcid, int? tier)
        {
            if (admin == null) return;
            LandblockManager.RunOnThreadFor(admin, ActionType.ItemBounty_Command, () =>
            {
                try
                {
                    var item = Factories.WorldObjectFactory.CreateNewWorldObject(wcid);
                    if (item == null) { Tell(admin, $"No weenie {wcid}."); return; }
                    if (!IsBountyItem(item)) { item.Destroy(); Tell(admin, $"{wcid} ({item.Name}) isn't a bounty (no kill count)."); return; }

                    var isRegion = !string.IsNullOrWhiteSpace(item.GetProperty(PropertyString.ItemBountyRegion));
                    var fixedTier = item.GetProperty(PropertyInt.ItemBountyTier) is int ft && ft >= MinTier && ft <= MaxTier;
                    if (isRegion)
                    {
                        if (tier.HasValue)
                            Tell(admin, $"{item.Name} is a region bounty (its weenie names the region); the tier was ignored.");
                    }
                    else if (!fixedTier)
                    {
                        var t = tier ?? (StandingTier(admin) is var st && st != 0 ? st : MinTier);
                        StampTier(item, Math.Clamp(t, MinTier, MaxTier));
                    }
                    else if (tier.HasValue && tier.Value != item.GetProperty(PropertyInt.ItemBountyTier))
                        Tell(admin, $"{item.Name} always has Tier {item.GetProperty(PropertyInt.ItemBountyTier)} (its weenie sets it); made at that tier.");

                    var terms = TermsOf(item);
                    if (terms == null) { item.Destroy(); Tell(admin, $"{wcid} ({item.Name}) has incomplete bounty terms (tier or region, kills, reward)."); return; }

                    if (!admin.TryCreateInInventoryWithNetworking(item)) { item.Destroy(); Tell(admin, "No room in your pack."); return; }
                    // the guid lets a test script use the bounty (use <bounty> twice starts it)
                    var killsText = KillsHidden(terms) ? $"mystery kills ({terms.KillsMin:N0}-{terms.KillsMax:N0}, rolled when started)" : $"{terms.Kills:N0} kills";
                    var rewardText = RewardText(terms);
                    if (terms.IsMysteryReward)
                    {
                        var options = MysteryOptions(terms, out var tableUsed);
                        rewardText = options.Count > 0
                            ? $"mystery reward from Zone Control table {tableUsed} ({options.Count} possible now, rolled when done)"
                            : $"mystery reward, but table '{terms.RewardTableName}' has nothing to pay at {PlaceKey(terms)} yet: /zonecontrol bountytable {terms.RewardTableName} add <wcid> <amount> <weight>";
                    }
                    Tell(admin, $"Made {item.Name} (0x{item.Guid.Full:X8}): {PlaceName(terms)}, {killsText}, {rewardText}.");
                    // a tradeable item made from nothing: on the record, like @ci
                    PlayerManager.BroadcastToAuditChannel(admin, $"{admin.Name} made bounty {item.Name} ({wcid}, 0x{item.Guid.Full:X8}) with /bounty make: {PlaceShort(terms)}.");
                    log.Info($"[ItemBounty] make {admin.Name} {item.Name} ({wcid}) 0x{item.Guid.Full:X8} {PlaceShort(terms)} kills={terms.Kills}");
                }
                catch (Exception ex)
                {
                    log.Error($"[ItemBounty] Make: {ex}");
                }
            });
        }

        // ── The Bounty Ledger (2026-11-12) ──────────────────────────────────
        //
        // One thing players look at and act on, instead of commands. The ledger is a BOOK, attuned STICKY and bonded so no
        // player can get rid of it - no drop, trade, give (not even to an NPC that takes anything), sale, container outside
        // the pack, salvage or mana drain, and it stays through death (its weenie is item_bounty_ledger_wcid; 0 = no ledgers):
        //   - a bounty used ON it starts: double-click the bounty, then click the ledger (HandleUse) - at once in its own tier
        //     or region or a town; standing in another tier or region, after a second use on the ledger;
        //   - double-clicked, it opens as a book: a summary page, then one page per bounty with a progress bar - written from
        //     the reader's slots every time it opens, as the Monster-Dex does, never stored on the item (OpenLedger); one chat
        //     line sums it up as well, for a client that draws no book;
        //   - appraised by the player carrying it, its description lists the bounties going and their progress (AppraisalText).
        // Nothing lives ON the ledger - the slots stay on the character - so even one an admin deletes loses nothing: a new one
        // is handed over the next time a bounty is used, and at login while bounties are going (OnLogin).

        /// <summary>The Bounty Ledger's weenie (item_bounty_ledger_wcid); 0 = no ledgers.</summary>
        internal static uint LedgerWcid
        {
            get
            {
                var v = ServerConfig.item_bounty_ledger_wcid.Value;
                return v > 0 && v <= uint.MaxValue ? (uint)v : 0;
            }
        }

        /// <summary>True while ledgers are on (item_bounty_ledger_wcid names a weenie).</summary>
        internal static bool LedgersOn => LedgerWcid != 0;

        /// <summary>True for a Bounty Ledger: an item made from item_bounty_ledger_wcid.</summary>
        public static bool IsLedger(WorldObject wo) => wo != null && LedgersOn && wo.WeenieClassId == LedgerWcid;

        /// <summary>A Bounty Ledger in the player's pack (main pack or a side pack), or null.</summary>
        private static WorldObject FindLedger(Player player)
            => LedgersOn ? player.GetInventoryItemsOfWCID(LedgerWcid).FirstOrDefault() : null;

        /// <summary>Player guid -> when a ledger was last tried for them outside login (GrantLedger's throttle).</summary>
        private static readonly ConcurrentDictionary<uint, double> _ledgerTried = new();

        /// <summary>
        /// At most one ledger attempt per player this often outside login: a forged client repeating bounty uses can't make the
        /// server build and destroy a ledger (or log a warning) per packet. Long enough for that, short enough that a player
        /// who just made room in a full pack is not kept waiting.
        /// </summary>
        private const double LedgerRetrySeconds = 3;

        /// <summary>1 once the missing-ledger-weenie warning has been logged: once per server run, not once per use.</summary>
        private static int _ledgerMissingLogged;

        /// <summary>
        /// A new Bounty Ledger into the player's pack, or null: ledgers off, a character that can't take bounties, tried for
        /// them within LedgerRetrySeconds, the ledger's weenie missing (logged once: import the Bounty Ledger SQL, or set
        /// item_bounty_ledger_wcid 0), or no room (the player is told, except at login). At login (<paramref name="atLogin"/>)
        /// it is added without networking - SendSelf sends the pack right after; otherwise it is created in the pack with
        /// networking.
        /// </summary>
        private static WorldObject GrantLedger(Player player, bool atLogin)
        {
            var wcid = LedgerWcid;
            if (wcid == 0 || player == null || player.IsMule || player.IsOlthoiPlayer)
                return null;

            if (!atLogin)
            {
                var now = Time.GetUnixTime();
                if (_ledgerTried.TryGetValue(player.Guid.Full, out var last) && now - last < LedgerRetrySeconds)
                    return null;
                _ledgerTried[player.Guid.Full] = now;
            }

            var ledger = Factories.WorldObjectFactory.CreateNewWorldObject(wcid);
            if (ledger == null)
            {
                if (Interlocked.Exchange(ref _ledgerMissingLogged, 1) == 0)
                    log.Warn($"[ItemBounty] ledger: weenie {wcid} (item_bounty_ledger_wcid) doesn't exist - import the Bounty Ledger SQL and @clearcache, or set item_bounty_ledger_wcid 0. Until then bounties start with two uses. (Logged once.)");
                return null;
            }
            if (!(atLogin ? player.TryAddToInventory(ledger) : player.TryCreateInInventoryWithNetworking(ledger)))
            {
                ledger.Destroy();
                if (!atLogin)
                    Tell(player, "There is no room in your pack for a Bounty Ledger, which keeps track of your bounties. Make room to get one.");
                return null;
            }
            log.Info($"[ItemBounty] ledger: given to {player.Name}{(atLogin ? " at login" : "")}");
            return ledger;
        }

        /// <summary>
        /// The ItemUseable a bounty carries, which decides what the client does when it is double-clicked: with ledgers on, a
        /// cursor to use it ON something (the ledger starts it; the player themself or a ring get the how-to); with ledgers
        /// off, a plain use (the two-use start).
        /// </summary>
        internal static Usable BountyUseable => LedgersOn ? Usable.SourceContainedTargetSelfOrContained : Usable.Contained;

        /// <summary>
        /// What the client lets a bounty be used on with ledgers on: the ledger (a book: Writable), the player themself (no
        /// ledger yet? one is handed over) and jewelry (the habit from before the rework). The client refuses anything else.
        /// </summary>
        internal const ItemType BountyTargets = ItemType.Writable | ItemType.Creature | ItemType.Jewelry;

        /// <summary>
        /// Gives a bounty item the ItemUseable above - and, with ledgers on, the TargetType - whatever its weenie says; true
        /// when anything changed. At creation (OnNewItem), at login for the bounties a player carries (OnLogin), and when a
        /// bounty is used (HandleUse), for one that was elsewhere at login.
        /// </summary>
        internal static bool MakeUsable(WorldObject wo)
        {
            var changed = false;
            var useable = BountyUseable;
            if (wo.ItemUseable != useable)
            {
                wo.ItemUseable = useable;
                changed = true;
            }
            if (LedgersOn && wo.TargetType != BountyTargets)
            {
                wo.TargetType = BountyTargets;
                changed = true;
            }
            return changed;
        }

        /// <summary>One page of the ledger: its title (the book draws it as the page's author line) and its text.</summary>
        internal readonly record struct LedgerPage(string Title, string Text);

        /// <summary>A dashed line across a book page (the Monster-Dex's width).</summary>
        private const string Rule = "-----------------------------\n";

        /// <summary>
        /// The ledger's pages for a character's slots: a summary, then one page per bounty going, in slot order. Pure - the
        /// tests read it; OpenLedger sends it. <paramref name="on"/>: item_bounty_enabled.
        /// </summary>
        internal static List<LedgerPage> BuildLedger(IReadOnlyDictionary<string, Slot> slots, int max, bool on)
        {
            var pages = new List<LedgerPage> { new("Bounty Ledger", SummaryPage(slots, max, on)) };
            foreach (var key in SlotKeys)
                if (slots.TryGetValue(key, out var s))
                    pages.Add(new($"Bounty #{key}", BountyPage(key, s)));
            return pages;
        }

        /// <summary>The ledger's first page: how many are going, one line per slot ("#2  T15  64%  640/1,000"), and how to add one.</summary>
        internal static string SummaryPage(IReadOnlyDictionary<string, Slot> slots, int max, bool on)
        {
            var sb = new StringBuilder();
            sb.Append($"Bounties going: {slots.Count} of {max}\n").Append(Rule);
            if (!on)
                sb.Append("Paused by the admins: nothing counts or pays until they are back on.\n").Append(Rule);
            foreach (var key in ShownKeys(slots, max))
            {
                slots.TryGetValue(key, out var s);
                sb.Append(s == null ? $"#{key}  empty\n" : $"#{key}  {PlaceShort(s)}  {Percent(s)}%  {ShortProgress(s)}\n");
            }
            sb.Append(Rule);
            if (slots.Count > 0)
                sb.Append("Turn the page for each bounty.\n\n");
            sb.Append("To add a bounty: double-click it, then click this ledger.");
            return sb.ToString();
        }

        /// <summary>One bounty's page: its name, what and where to kill, the progress with a bar, the reward, and how to drop it.</summary>
        internal static string BountyPage(string key, Slot s)
        {
            var sb = new StringBuilder();
            sb.Append(s.Name).Append('\n').Append(Rule);
            sb.Append("Kill: ").Append(s.Kind == KindSpecies ? s.TargetName : "any creature").Append('\n');
            sb.Append("Where: ").Append(PlaceName(s)).Append('\n');
            sb.Append("Progress: ").Append(ProgressText(s)).Append('\n');
            sb.Append(ProgressBar(s)).Append(' ').Append(Percent(s).ToString(CultureInfo.InvariantCulture)).Append("%\n");
            sb.Append("Reward: ").Append(s.IsMysteryReward ? "a mystery, revealed when it is done" : RewardText(s)).Append('\n');
            if (s.AppliedUnix > 0 && s.AppliedUnix < 253_402_300_800)   // a readable date (a hand-edited slot could hold anything)
                sb.Append("Started: ").Append(DateTimeOffset.FromUnixTimeSeconds(s.AppliedUnix).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('\n');
            if (IsWaitingMystery(s))
                sb.Append("Done! Its mystery reward is paid as soon as the admins set it up.\n");
            sb.Append(Rule);
            sb.Append(s.IsRegion
                ? $"Only your own kills in {PlaceName(s)} count, each one in full.\n"
                : $"Only your own kills in {PlaceName(s)} count (the tier dip applies).\n");
            sb.Append($"\nTo abandon it (forfeits it):\n/bounty abandon {key}");
            return sb.ToString();
        }

        /// <summary>How far a bounty is, 0-100: rounded down, so 100 only once it is done.</summary>
        internal static int Percent(Slot s)
        {
            if (s == null || s.Kills <= 0) return 0;
            if (s.Progress >= s.Kills) return 100;
            return Math.Clamp((int)Math.Floor(100.0 * s.Progress / s.Kills), 0, 99);
        }

        /// <summary>"[############--------]": twenty cells, one per 5%, plain ASCII.</summary>
        internal static string ProgressBar(Slot s)
        {
            const int cells = 20;
            var filled = Math.Clamp(Percent(s) * cells / 100, 0, cells);
            return "[" + new string('#', filled) + new string('-', cells - filled) + "]";
        }

        /// <summary>"640/1,000": the progress and the kill count, short.</summary>
        internal static string ShortProgress(Slot s) => $"{TierRewardScale.FormatProgress(s.Progress)}/{s.Kills:N0}";

        /// <summary>
        /// The chat line opening the ledger also prints, for a client that draws no book: "Bounty Ledger: 2 of 4 going - #1
        /// T16 4%, #2 T15 64%." or, with none going, how to add one.
        /// </summary>
        internal static string LedgerChatLine(IReadOnlyDictionary<string, Slot> slots, int max)
        {
            if (slots.Count == 0)
                return $"Bounty Ledger: none going (you can have up to {max}). To add one, double-click the bounty, then click the ledger.";
            var parts = SlotKeys.Where(slots.ContainsKey).Select(k => $"#{k} {PlaceShort(slots[k])} {Percent(slots[k])}%");
            return $"Bounty Ledger: {slots.Count} of {max} going - {string.Join(", ", parts)}.";
        }

        /// <summary>
        /// The ledger's description for the player carrying it: one line per slot with the progress and the reward, then how
        /// to use it. <paramref name="on"/>: item_bounty_enabled.
        /// </summary>
        internal static string LedgerDescription(IReadOnlyDictionary<string, Slot> slots, int max, bool on)
        {
            var sb = new StringBuilder();
            sb.Append($"Your bounties: {slots.Count} of {max} going.\n");
            if (!on)
                sb.Append("Paused by the admins: nothing counts or pays until they are back on.\n");
            foreach (var key in ShownKeys(slots, max))
            {
                slots.TryGetValue(key, out var s);
                sb.Append(s == null ? $"#{key} empty\n" : $"#{key} {PlaceShort(s)}: {ProgressText(s)} ({Percent(s)}%) -> {RewardText(s)}\n");
            }
            sb.Append("\nTo add a bounty, double-click it and then click this ledger. Double-click the ledger to read a page for each bounty.");
            return sb.ToString();
        }

        /// <summary>
        /// Appraisal (AppraiseInfo): the description to show instead of the stored one - a bounty's own terms (DescribeItem), or
        /// a ledger's summary of the examiner's bounties when the examiner carries it - or null to keep the stored text.
        /// Display only: nothing is written. On the examiner's thread.
        /// </summary>
        public static string AppraisalText(WorldObject wo, Player examiner)
        {
            if (!IsLedger(wo))
                return DescribeItem(wo);
            try
            {
                if (examiner == null || examiner.FindObject(wo.Guid.Full, Player.SearchLocations.MyInventory) == null)
                    return null;
                return LedgerDescription(LoadSlots(examiner), MaxActiveNow, ServerConfig.item_bounty_enabled.Value);
            }
            catch (Exception ex)
            {
                log.Error($"[ItemBounty] AppraisalText: {ex}");
                return null;
            }
        }

        /// <summary>Player guid -> when their ledger was last opened (OpenLedger's throttle).</summary>
        private static readonly ConcurrentDictionary<uint, double> _ledgerOpened = new();

        /// <summary>The least time between two ledger opens for one player, in seconds.</summary>
        private const double LedgerReopenSeconds = 1;

        /// <summary>
        /// A Bounty Ledger opened (Book.ActOnUse, Player.ReadBook): its pages written from the reader's bounties and sent as
        /// the book's contents - nothing stored on the item, as the Monster-Dex does - with as many pages as it has, so the
        /// client offers no blank page to write on. One chat line sums it up too. A done mystery waiting for its reward table
        /// is paid first if it can be now, as /bounty does. On the reader's thread.
        /// </summary>
        public static void OpenLedger(Player player, WorldObject ledger, int maxChars)
        {
            try
            {
                if (player?.Session == null || ledger == null) return;

                // once a second at most: the book-data request (0x00AA, Player.ReadBook) has no throttle of its own, and each
                // open builds the pages, sends them and a chat line, and may pay a waiting mystery. A legit double-click and a
                // client re-request a moment later lose nothing: the first answer already carried the pages.
                var now = Time.GetUnixTime();
                if (_ledgerOpened.TryGetValue(player.Guid.Full, out var last) && now - last < LedgerReopenSeconds)
                    return;
                _ledgerOpened[player.Guid.Full] = now;

                if (maxChars < 100) maxChars = 1000;   // as the Monster-Dex: an unset page size reads 0

                var on = ServerConfig.item_bounty_enabled.Value;
                if (on)
                    PayWaitingMysteries(player);

                var slots = LoadSlots(player);
                var max = MaxActiveNow;
                var pages = BuildLedger(slots, max, on)
                    .Select(p => new ACE.Entity.Models.PropertiesBookPageData
                    {
                        AuthorId = 0xFFFFFFFF,
                        AuthorName = p.Title,
                        AuthorAccount = "",
                        IgnoreAuthor = false,
                        PageText = p.Text.Length > maxChars ? p.Text.Substring(0, maxChars) : p.Text,
                    })
                    .ToList();
                player.Session.Network.EnqueueSend(new ACE.Server.Network.GameEvent.Events.GameEventBookDataResponse(
                    player.Session, ledger.Guid.Full, maxChars, pages.Count, pages, "", 0xFFFFFFFF, "", false));
                Tell(player, LedgerChatLine(slots, max));
            }
            catch (Exception ex)
            {
                log.Error($"[ItemBounty] OpenLedger: {ex}");
            }
        }

        // ── Login ────────────────────────────────────────────────────────────

        /// <summary>
        /// Login (Player.PlayerEnterWorld), BEFORE SendSelf sends the character and its pack to the client: the bounties a
        /// player carries are given the ItemUseable of the current mode (NormalizeHeldBounties), the Bounty Tracker's stored
        /// display is brought to the current format (RefreshStoredDisplay), and a player with bounties - going, or in the pack -
        /// but no Bounty Ledger is given one (GiveLedgerAtLogin).
        /// </summary>
        public static void OnLogin(Player player)
        {
            var held = NormalizeHeldBounties(player);
            RefreshStoredDisplay(player);
            GiveLedgerAtLogin(player, held);
        }

        /// <summary>
        /// At login, before the pack is sent: a Bounty Ledger for a player who has bounties going or carries
        /// <paramref name="heldBounties"/> bounty items and has no ledger - the players from before ledgers existed, and
        /// anyone who lost theirs. Added without networking (the pack goes out with it); a full pack just waits for the next
        /// bounty use.
        /// </summary>
        private static void GiveLedgerAtLogin(Player player, int heldBounties)
        {
            try
            {
                if (!LedgersOn || player == null || player.IsMule || player.IsOlthoiPlayer)
                    return;
                if (heldBounties == 0 && string.IsNullOrEmpty(player.GetProperty(PropertyString.ItemBountySlots)))
                    return;
                if (FindLedger(player) != null)
                    return;
                if (GrantLedger(player, atLogin: true) != null)
                    Tell(player, "A Bounty Ledger was added to your pack: double-click it to see your bounties and their progress. To start a bounty, double-click it and then click the ledger.");
            }
            catch (Exception ex)
            {
                log.Error($"[ItemBounty] GiveLedgerAtLogin: {ex}");
            }
        }

        /// <summary>
        /// The character's stored ItemBountyDisplay rebuilt in the current format (v3, slots #1..#cap), when it has one and it
        /// differs - a character that used bounties before the rework stored a v2 one with R1..B2. Set on the character only,
        /// nothing is sent: at login the property goes out with the character ([SendOnLogin]).
        /// </summary>
        private static void RefreshStoredDisplay(Player player)
        {
            try
            {
                var stored = player?.GetProperty(PropertyString.ItemBountyDisplay);
                if (stored == null)
                    return;
                var current = BuildDisplay(LoadSlots(player));
                if (current != stored)
                    player.SetProperty(PropertyString.ItemBountyDisplay, current);
            }
            catch (Exception ex)
            {
                log.Error($"[ItemBounty] RefreshStoredDisplay: {ex}");
            }
        }

        /// <summary>
        /// Login (OnLogin, BEFORE the pack is sent to the client - the client keeps an item's ItemUseable from when it was
        /// first told about it): every bounty the player carries gets the ItemUseable and TargetType of the current mode
        /// (MakeUsable), as OnNewItem gives new ones - a bounty from before the 2026-11-12 rework targets jewelry. Only what the
        /// player carries (GetAllPossessions); a bounty kept elsewhere (a house chest, a corpse) is fixed the first time it is
        /// used (HandleUse), which also sends it to the client again. Returns how many bounty items the player carries.
        /// </summary>
        public static int NormalizeHeldBounties(Player player)
        {
            var held = 0;
            try
            {
                if (player == null)
                    return 0;
                var changed = 0;
                foreach (var item in player.GetAllPossessions())
                {
                    if (item == null || !IsBountyItem(item))
                        continue;
                    held++;
                    if (MakeUsable(item))
                        changed++;
                }
                if (changed > 0)
                    log.Info($"[ItemBounty] login {player.Name}: {changed} bounty item(s) given the current use ({BountyUseable})");
            }
            catch (Exception ex)
            {
                log.Error($"[ItemBounty] NormalizeHeldBounties: {ex}");
            }
            return held;
        }

        // ── Storage ──────────────────────────────────────────────────────────

        /// <summary>The character's slots as stored (PropertyString.ItemBountySlots); entries that can't be read are left out.</summary>
        private static Dictionary<string, Slot> LoadSlots(Player player)
            => ParseSlots(player.GetProperty(PropertyString.ItemBountySlots));

        /// <summary>Stores the slots, then pushes the tracker's display to the client if it changed (every apply, counted kill,
        /// completion and removal comes through here).</summary>
        private static void SaveSlots(Player player, IReadOnlyDictionary<string, Slot> slots)
        {
            var text = FormatSlots(slots);
            if (text.Length == 0)
                player.RemoveProperty(PropertyString.ItemBountySlots);
            else if (text != player.GetProperty(PropertyString.ItemBountySlots))
                player.SetProperty(PropertyString.ItemBountySlots, text);

            try
            {
                PushDisplay(player, slots, force: false);
            }
            catch (Exception ex)
            {
                log.Error($"[ItemBounty] PushDisplay: {ex}");   // the window can always resync; the slots are saved
            }
        }

        /// <summary>A name as a field: the separators the slot string uses are taken out.</summary>
        private static string Clean(string s) => (s ?? "").Replace('|', ' ').Replace(';', ' ').Trim();

        /// <summary>One console (broadcast chat) line to the player, if they are connected.</summary>
        private static void Tell(Player player, string text)
            => player?.Session?.Network.EnqueueSend(new GameMessageSystemChat(text, ChatMessageType.Broadcast));

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }
}
