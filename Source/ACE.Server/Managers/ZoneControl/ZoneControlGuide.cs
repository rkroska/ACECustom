using System.Collections.Generic;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// The "How Zone Control works" page on the portal: the ideas an admin needs before changing any stat. Plain ASCII.
    ///
    /// KEEP IT TRUE: each section describes code - ZoneControlManager.ResolveStatSource (which layer wins),
    /// ResolveRankForCreature / ResolveRankForWcid (ranks), EvaluatedProfile.GetT (T25 anchors), ZoneStatEditor (edits and the
    /// log), ZoneSpawnScaler (spawn-time stats). Change the matching section when that code changes.
    /// </summary>
    public static class ZoneControlGuide
    {
        public sealed class Section
        {
            public string Title { get; init; }
            public string[] Paragraphs { get; init; }
        }

        public static readonly IReadOnlyList<Section> Sections = new[]
        {
            new Section
            {
                Title = "What Zone Control is",
                Paragraphs = new[]
                {
                    "A zone is a set of landblocks at one variation. While the zone is enabled, every monster spawned inside it is governed "
                    + "by the zone's stats instead of (or on top of) its weenie's own numbers. A disabled zone changes nothing.",
                    "Variations 11 to 25 are the Zone Control tiers (T11 to T25). Negative variations are rift instances and never "
                    + "inherit a tier Default.",
                    "An exempt monster (or everything from an exempt generator) is left alone entirely: weenie stats, no zone stats.",
                },
            },
            new Section
            {
                Title = "Layers: which value wins",
                Paragraphs = new[]
                {
                    "A stat can be set on several layers. For each stat separately, the most specific layer that sets it wins:",
                    "1. The monster override (a zone's per-WCID bucket). 2. The zone's rank row for the monster's rank. 3. The zone's "
                    + "Default row. 4. The tier Default's rank row. 5. The tier Default's Default row. 6. For tiers 12 and up, the v11 "
                    + "Default (the anchor) rank row, then its Default row.",
                    "So a tier Default is the baseline for every zone at that tier, and a zone only overrides the stats it sets. The "
                    + "Source column on the stats page names the layer the value in effect comes from.",
                    "If no layer sets a stat, the game uses its normal behaviour - usually the monster's own weenie value. Each stat's "
                    + "help says what 'not set' means for it.",
                },
            },
            new Section
            {
                Title = "Rank rows",
                Paragraphs = new[]
                {
                    "Every layer has four rows: Default, Regular, Leader and Boss. A monster's rank comes from the zone's per-monster "
                    + "rank setting, or else from its weenie's Regular / Leader / Boss flag. A monster with no rank reads only the "
                    + "Default rows.",
                    "On a layer, the monster's rank row is read before the Default row. Use the Default row for values every rank "
                    + "shares and the rank rows for the differences (for example more Health on Bosses).",
                    "A monster override has no rank rows: one monster type is one rank.",
                },
            },
            new Section
            {
                Title = "T25 anchors (stats ending in _t25)",
                Paragraphs = new[]
                {
                    "Some stats come in pairs: the stat itself is its value at T11 and the same name ending in _t25 is its value at "
                    + "T25. With both set, tiers 12 to 24 sit on the straight line between them. Without the _t25 key, the T11 value "
                    + "applies at every tier. A _t25 key on its own does nothing.",
                },
            },
            new Section
            {
                Title = "Growth and tier pins",
                Paragraphs = new[]
                {
                    "A stored stat can carry growth or pinned per-tier values, written by in-game commands. The stats page marks such "
                    + "a stat 'has growth or pins'. Saving a value on the page replaces it with one flat number, so the page asks "
                    + "first. A revert from History puts the growth and pins back.",
                },
            },
            new Section
            {
                Title = "Turned off and retired stats",
                Paragraphs = new[]
                {
                    "'Turned off here' means the zone switched the stat off with /zonecontrol togglestat: it counts as not set in "
                    + "this zone even though a tier Default sets it. Turn it back on in game with togglestat on (or clear).",
                    "'Retired' marks a key that is stored but no longer read by the game. It does nothing; clear it to tidy up.",
                },
            },
            new Section
            {
                Title = "When a change takes effect",
                Paragraphs = new[]
                {
                    "Every edit is saved at once and survives a restart. When the game picks it up depends on the stat, and each "
                    + "stat's help says which:",
                    "Spawn: stamped on the monster when it spawns (attributes, vitals, level...). Monsters already standing keep their "
                    + "old values until they die and respawn.",
                    "Live: read on every hit or cast, so monsters already standing use the new value at once.",
                    "Loot: rolled when a corpse is created. Corpses on the ground and items already looted keep what they rolled.",
                    "Kill: read when a kill is credited.",
                },
            },
            new Section
            {
                Title = "Undoing a change: the change log",
                Paragraphs = new[]
                {
                    "Every stat set or clear is logged with who made it, when, where, and the whole value before and after - from "
                    + "this page and from the in-game set, clearstat, default set and default clearstat commands.",
                    "History lists the changes for the zone or tier you are viewing. Revert puts back the exact old value (growth and "
                    + "tier pins included), or clears the stat if it was not set before. A revert is itself logged and can be "
                    + "reverted. If the stat changed again since, the revert is refused rather than overwriting the newer change.",
                    "Every other edit that saves Zone Control is logged too, as a whole part: one zone, one tier Default, or one "
                    + "server-wide list (gear lock spots, allowed procs, monster damage multipliers, the zone-wide and region bounties, "
                    + "bounty reward tables, item bounty drop weights, ladder versions). That covers enable and disable, landblocks, togglestat, tier pins and curves, props, "
                    + "appearance, modifiers, currency, bounties, copyfrom, clone, rename, delete and resetmob. History shows which "
                    + "fields moved inside the part. Revert puts the whole part back as it was, and is refused if the part changed "
                    + "again since. Turn on Show every change to see the server-wide lists, which belong to no zone.",
                    "Two cases need care. A rename is logged as the old zone removed and the new one created, so revert both together. "
                    + "Ladder versions are logged but cannot be reverted, because gear compares them. Rift instances are never saved, "
                    + "so they are not logged.",
                    "Clearing a stat is not the same as setting it to 0: clear makes the next layer apply, while 0 is a real value.",
                },
            },
        };
    }
}
