using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>When a stat's new value reaches the game. Drives the "when it takes effect" and "how to undo" text.</summary>
    public enum ZoneStatTiming
    {
        /// <summary>Stamped on the monster when it spawns. Monsters already standing keep their old value until they respawn.</summary>
        Spawn,
        /// <summary>Read on every hit or cast, so it applies at once to monsters already standing.</summary>
        Live,
        /// <summary>Rolled when the corpse is created.</summary>
        Loot,
        /// <summary>Read when a kill is credited.</summary>
        Kill,
        /// <summary>Read when a player uses a Salvage Bag (2026-11-26).</summary>
        Use,
    }

    /// <summary>Help for one stat, or for a family of stats matched by <see cref="Pattern"/>. Every string is plain ASCII: the
    /// in-game help prints the same text to the AC client.</summary>
    public sealed class ZoneStatHelpEntry
    {
        /// <summary>The stat key, or for a family a readable name such as "relief_aug_x1..x4".</summary>
        public string Key { get; init; }
        /// <summary>Set for a family: matches every stat key the entry covers.</summary>
        public Regex Pattern { get; init; }

        /// <summary>One line: what the setting is.</summary>
        public string Summary { get; init; }
        /// <summary>How it works, in a few sentences: what reads it and how the number is used.</summary>
        public string Details { get; init; }
        /// <summary>What kind of number it is: an absolute value that replaces the monster's own, a multiplier, a rating, a chance...</summary>
        public string Shape { get; init; }
        /// <summary>What the game does when nothing sets it.</summary>
        public string WhenUnset { get; init; }
        public string Raise { get; init; }
        public string Lower { get; init; }
        /// <summary>Sensible values, and anything the server clamps.</summary>
        public string Range { get; init; }
        public ZoneStatTiming Timing { get; init; }
        /// <summary>Anything an admin must know before changing it: traps, interactions, a value that turns something off.</summary>
        public string Caution { get; init; }
        public string[] Related { get; init; } = Array.Empty<string>();

        public bool Covers(string stat) =>
            Pattern != null ? Pattern.IsMatch(stat) : string.Equals(Key, stat, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Admin help for every Zone Control stat: the portal's Zone Control page and `/zonecontrol help &lt;stat&gt;` both read it, so
    /// the two cannot disagree. Entries live in one partial file per stat group (ZoneStatHelp.&lt;Group&gt;.cs).
    ///
    /// KEEP IT TRUE: every sentence must match the code that reads the stat. When a stat's reader changes, change its entry in the
    /// same commit. A stat in <see cref="CompleteGroups"/> without an entry fails ZoneStatHelpTests.
    /// </summary>
    public static partial class ZoneStatHelp
    {
        /// <summary>Groups whose every stat has an entry. ZoneStatHelpTests holds these groups to full coverage; add a group here
        /// once its partial file is written and checked against the code.</summary>
        public static readonly IReadOnlyCollection<string> CompleteGroups = new HashSet<string>(StringComparer.Ordinal)
        {
            "Attributes and vitals",
            "Attack",
            "Loot",
            "Specials and cloaks",
            "Armor and jewelry lines",
            "Weapon cards",
            "Salvage bags",
            "Kill rewards",
            "Archetypes",
            "Relief and aug curves",
            "Debuffs and DoTs",
            "Defense",
        };

        private static readonly List<ZoneStatHelpEntry> _entries = new List<ZoneStatHelpEntry>();
        private static readonly object _buildLock = new object();
        private static volatile bool _built;

        public static IReadOnlyList<ZoneStatHelpEntry> Entries
        {
            get
            {
                EnsureBuilt();
                return _entries;
            }
        }

        // One per group file. A new group file declares its method here and adds its call in EnsureBuilt.
        static partial void AddAttributes(List<ZoneStatHelpEntry> e);
        static partial void AddAttack(List<ZoneStatHelpEntry> e);
        static partial void AddLootB(List<ZoneStatHelpEntry> e);
        static partial void AddLootA(List<ZoneStatHelpEntry> e);
        static partial void AddSpecials(List<ZoneStatHelpEntry> e);
        static partial void AddArmorB(List<ZoneStatHelpEntry> e);
        static partial void AddArmorA(List<ZoneStatHelpEntry> e);
        static partial void AddWeaponB2(List<ZoneStatHelpEntry> e);
        static partial void AddWeaponB1(List<ZoneStatHelpEntry> e);
        static partial void AddWeaponA(List<ZoneStatHelpEntry> e);
        static partial void AddRewardsBags(List<ZoneStatHelpEntry> e);
        static partial void AddArchetypes(List<ZoneStatHelpEntry> e);
        static partial void AddReliefB(List<ZoneStatHelpEntry> e);
        static partial void AddReliefA(List<ZoneStatHelpEntry> e);
        static partial void AddDebuffs(List<ZoneStatHelpEntry> e);
        static partial void AddDefense(List<ZoneStatHelpEntry> e);

        private static void EnsureBuilt()
        {
            if (_built)
                return;
            lock (_buildLock)
            {
                if (_built)
                    return;
                AddAttributes(_entries);
                AddAttack(_entries);
                AddLootB(_entries);
                AddLootA(_entries);
                AddSpecials(_entries);
                AddArmorB(_entries);
                AddArmorA(_entries);
                AddWeaponB2(_entries);
                AddWeaponB1(_entries);
                AddWeaponA(_entries);
                AddRewardsBags(_entries);
                AddArchetypes(_entries);
                AddReliefB(_entries);
                AddReliefA(_entries);
                AddDebuffs(_entries);
                AddDefense(_entries);
                _built = true;
            }
        }

        public const string AnchorSuffix = "_t25";

        /// <summary>The entry for a stat. A "&lt;stat&gt;_t25" key shares its base stat's entry (it is that stat's T25 anchor).</summary>
        public static ZoneStatHelpEntry For(string stat, out bool isAnchor)
        {
            isAnchor = false;
            if (string.IsNullOrWhiteSpace(stat))
                return null;
            var key = stat.Trim();
            var found = Find(key);
            if (found != null || !key.EndsWith(AnchorSuffix, StringComparison.OrdinalIgnoreCase))
                return found;
            isAnchor = true;
            return Find(key.Substring(0, key.Length - AnchorSuffix.Length));
        }

        public static ZoneStatHelpEntry For(string stat) => For(stat, out _);

        private static ZoneStatHelpEntry Find(string key) =>
            Entries.FirstOrDefault(e => e.Pattern == null && e.Covers(key)) ?? Entries.FirstOrDefault(e => e.Pattern != null && e.Covers(key));

        public const string AnchorNote =
            "This is the T25 anchor of the stat. The stat itself is the T11 value; with both set, tiers 12-24 sit on the straight line "
            + "between them. Without this key the T11 value applies at every tier. On its own (no T11 value) it does nothing.";

        public static string TakesEffect(ZoneStatTiming timing) => timing switch
        {
            ZoneStatTiming.Spawn => "When a monster spawns. Monsters already standing keep the value they spawned with until they die and respawn.",
            ZoneStatTiming.Live => "At once. The game reads it on every hit or cast, so monsters already standing use the new value on their next one.",
            ZoneStatTiming.Loot => "When a corpse is created. Corpses already on the ground and items already looted keep what they rolled.",
            ZoneStatTiming.Kill => "On the next kill credited after the change.",
            ZoneStatTiming.Use => "The next time a player uses a bag. Bags already used keep what they did.",
            _ => "",
        };

        public static string HowToUndo(ZoneStatTiming timing)
        {
            var text = "Open History and press Revert on the change: it puts back the exact old value, growth and tier pins included. "
                + "Or Clear it (X) to remove it from this row, so the next layer down applies again (the Source column shows which). "
                + "Both are logged, and a revert can itself be reverted.";
            return timing switch
            {
                ZoneStatTiming.Spawn => text + " Monsters already standing keep the changed value until they respawn.",
                ZoneStatTiming.Loot => text + " Items that already dropped with the changed value keep it.",
                _ => text,
            };
        }

        /// <summary>The help as chat lines for `/zonecontrol help &lt;stat&gt;`. Plain ASCII, no carriage returns.</summary>
        public static List<string> ChatLines(string stat)
        {
            var entry = For(stat, out var anchor);
            var lines = new List<string>();
            if (entry == null)
                return lines;

            lines.Add("== " + stat.Trim().ToLowerInvariant() + ": " + entry.Summary);
            if (anchor)
                lines.Add("  " + AnchorNote);
            void Add(string label, string text)
            {
                if (!string.IsNullOrWhiteSpace(text))
                    lines.Add("  " + label + ": " + text);
            }
            Add("How it works", entry.Details);
            Add("Value", entry.Shape);
            Add("Not set", entry.WhenUnset);
            Add("Raise it", entry.Raise);
            Add("Lower it", entry.Lower);
            Add("Range", entry.Range);
            Add("Careful", entry.Caution);
            Add("Takes effect", TakesEffect(entry.Timing));
            var key = stat.Trim().ToLowerInvariant();
            // the same row the value was set on: repeat its --rank or --wcid, or the clear lands on the Default row and the override stays
            Add("Undo", "/zonecontrol clearstat <zone> " + key + " (or /zonecontrol default <variation> clearstat " + key
                + ") removes it from that row - add the same --rank <rank> or --wcid <id> you set it with, or it clears the Default row"
                + " instead. Or set the old value again. On the portal, History > Revert puts back the exact old value.");
            if (entry.Related.Length > 0)
                Add("See also", string.Join(", ", entry.Related));
            return lines;
        }
    }
}
