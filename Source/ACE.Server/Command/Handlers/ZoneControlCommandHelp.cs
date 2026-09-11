using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// THE registry of every chat command the ZoneControl plugin sends (owner 2026-09-03: a GM Tools >
    /// Chat Commands tab "that we keep updated"). The plugin renders this list; it never carries its own.
    /// `/zonecontrol help` prints it as prose, `/zonecontrol help --wire` emits one [[ZCHELP]] row per entry.
    ///
    /// KEEP IT UPDATED: when a verb is added, removed or re-shaped in ZoneControlCommands.cs (or any other
    /// command the plugin sends), edit the matching row here in the same change. A row is
    /// (Group, Command, Usage, What it does) - one line each, plain ASCII, no '~' (the wire separator).
    /// </summary>
    // NOTE for the next audit: `cantrip` is a deliberate silent alias of `modifier` and is not listed.
    public static class ZoneControlCommandHelp
    {
        public readonly struct Entry
        {
            public readonly string Group, Command, Usage, Description;
            public Entry(string group, string command, string usage, string description)
            { Group = group; Command = command; Usage = usage; Description = description; }
        }

        private static Entry E(string g, string c, string u, string d) => new Entry(g, c, u, d);

        public static readonly Entry[] All =
        {
            // ── Zones ──────────────────────────────────────────────────────────
            E("Zones", "/zonecontrol create", "<name...> <variation|here> [here|<hex>]", "Create a zone at a variation; multi-word names are fine (create Tou Tou here)."),
            E("Zones", "/zonecontrol delete", "<name>", "Delete a zone and everything it authors."),
            E("Zones", "/zonecontrol rename", "<old> <new...>", "Rename a zone."),
            E("Zones", "/zonecontrol enable", "<name>", "Turn a zone on (its stats, props and effects apply)."),
            E("Zones", "/zonecontrol disable", "<name>", "Turn a zone off without deleting it."),
            E("Zones", "/zonecontrol setvar", "<name> <variation>", "Move a zone to another variation (11+ = prestige layers)."),
            E("Zones", "/zonecontrol clonezone", "<zone> <variation|lo-hi>", "Clone a zone to other variations, e.g. clonezone Tou Tou 12-25."),
            E("Zones", "/zonecontrol addlb", "<name> <hex|here> [more...]", "Add landblocks to a zone (comma lists ok: F559,F55A)."),
            E("Zones", "/zonecontrol removelb", "<name> <hex>", "Remove one landblock from a zone."),
            E("Zones", "/zonecontrol boundary", "<name> <on|off|show>", "Bounded zones keep players inside their landblocks at that variation."),
            E("Zones", "/zonecontrol arealist", "", "List every zone with its variation and state (the plugin's zone list)."),
            E("Zones", "/zonecontrol list", "", "Compact one-line-per-zone listing for chat."),
            E("Zones", "/zonecontrol here", "", "Which zone governs where you stand, and at what variation."),
            E("Zones", "/zonecontrol show", "<name> [--wcid <id>]", "Print a zone's authored stats (or one monster's merged view)."),
            E("Zones", "/zonecontrol reload", "", "Re-read the whole Zone Control store from the shard DB."),

            // ── Sync / plugin plumbing ─────────────────────────────────────────
            E("Plugin Sync", "/zonecontrol get", "[<name>] [--wcid <id>]", "One-shot [[ZC]] sync of a zone (bare get = session state only)."),
            E("Plugin Sync", "/zonecontrol sync", "on <name> [--wcid <id>] | off", "Start / stop the 2 s live sync stream the plugin window reads."),
            E("Plugin Sync", "/zonecontrol mobs", "<name>", "The zone's monster list (every creature reachable from placed generators)."),
            E("Plugin Sync", "/zonecontrol mobinfo", "<wcid>", "A weenie's base stats for the plugin's hints."),
            E("Plugin Sync", "/zonecontrol propsof", "<wcid> <i|f|b|l>:<id>,...", "A weenie's own values for a list of properties (the Props tab's hints)."),
            E("Plugin Sync", "/zonecontrol selinfo", "", "What you have selected in game (wcid, name, creature or not)."),
            E("Plugin Sync", "/zonecontrol findmob", "<text or wcid>", "Search creature weenies by name or id."),
            E("Plugin Sync", "/zonecontrol help", "[--wire]", "This list. --wire feeds the Chat Commands tab."),

            // ── Stats + ranks ─────────────────────────────────────────────────
            E("Stats", "/zonecontrol set", "<name> <stat> <value> [--wcid <id>] [--rank regular|leader|boss]", "Author a stat on the zone, one monster's bucket, or a rank row."),
            E("Stats", "/zonecontrol clearstat", "<name> <stat> [--wcid <id>] [--rank r]", "Unset a stat at that scope (it inherits again)."),
            E("Stats", "/zonecontrol togglestat", "<name> <stat> <on|off|clear> | list", "Zone-scope only: OFF makes a stat count as unset here even if the tier authors it."),
            E("Stats", "/zonecontrol default", "<var> <show|set|clearstat|clear|list> ...", "The Tier Default layer every zone at that variation inherits. set/clearstat take --rank."),
            E("Stats", "/zonecontrol defaultget", "<variation>", "One-shot [[ZCD]] sync of a Tier Default."),
            E("Stats", "/zonecontrol tier", "<stat> <t11value> <t25value> [--curve augs|linear] | show <stat> | clear <stat> | curves", "Author all 15 Tier Defaults of a stat from a T11..T25 curve."),
            E("Stats", "/zonecontrol simstats", "<name> --wcid <id>", "Rank-aware effective offense numbers for the Curves simulator."),
            E("Stats", "/zonecontrol resetmob", "<zone> --wcid <id>", "Remove ALL of one monster's overrides: stats, props, loot, appearance."),

            // ── Master switches ───────────────────────────────────────────────
            E("Exempt", "/zonecontrol exempt", "<zone> <on|off> --wcid <id>", "Master switch per monster: exempt = the zone leaves it alone entirely."),
            E("Exempt", "/zonecontrol exempt", "<zone> <on|off> --gen <generator wcid>", "Master switch per generator: everything it spawns here is ungoverned (nested too)."),
            E("Exempt", "/zonecontrol exempt", "<zone> list", "Print both exempt sets for a zone."),

            // ── Props + body parts ────────────────────────────────────────────
            E("Props", "/zonecontrol prop", "<name> <int|int64|float|bool> <idOrName> <value> [--wcid <id>]", "Stamp a weenie property onto every governed spawn (or one monster). Applies on respawn."),
            E("Props", "/zonecontrol clearprop", "<name> <int|int64|float|bool> <idOrName> [--wcid <id>]", "Stop stamping that property."),
            E("Body Parts", "/zonecontrol part", "<name> <part> <armor|damage|variance|dmgtype> <value> [--wcid <id>]", "Override one body part's armor / damage / variance / damage type."),
            E("Body Parts", "/zonecontrol clearpart", "<name> <part> [field] [--wcid <id>]", "Clear a body-part override (no field = all four)."),
            E("Body Parts", "/zonecontrol partsof", "<wcid | sel>", "List a monster's body parts with their weenie values."),
            E("Body Parts", "/zonecontrol listparts", "<wcid | 0xSetupId>", "List the parts a setup carries."),

            // ── Appearance ────────────────────────────────────────────────────
            E("Appearance", "/zonecontrol appearance", "<name> <palette|shade|scale|translucency|shiny> <value> [--wcid <id>]", "Cosmetic layer for the zone or one monster; separate from stats."),
            E("Appearance", "/zonecontrol appearance", "<name> animpart <index> <gfxObjHex> [--wcid <id>]", "Swap one model part."),
            E("Appearance", "/zonecontrol appearance", "<zone> name <new name> --wcid <id>", "Display name for one monster in this zone."),
            E("Appearance", "/zonecontrol clearappearance", "<name> [field] [--wcid <id>]", "Clear the cosmetic layer (no field = everything)."),
            E("Appearance", "/zonecontrol copylook", "<name> <donorWcid> [--wcid <id>]", "Copy a donor weenie's look onto the zone default or one monster."),
            E("Appearance", "/zonecontrol becomemob", "<donorWcid> --wcid <targetWcid>", "The target becomes a full copy of the donor; keeps its name and scale."),
            E("Appearance", "/zonecontrol clonemob", "<zone> <newWcid> [new display name] --wcid <srcWcid>", "Mint a new weenie from an existing monster."),
            E("Appearance", "/zonecontrol bakemob", "<zone> --wcid <id>", "Write the monster's current look into its weenie permanently."),
            E("Appearance", "/zonecontrol previewmob", "<wcid> [distance]", "Spawn a short-lived preview of a weenie in front of you."),
            E("Appearance", "/zonecontrol draftslot", "<zone> [release]", "Reserve / release the Look Lab's draft weenie slot."),
            E("Appearance", "/zonecontrol copydraft", "<zone> <destWcid>", "Save the drafted look onto the destination's zone appearance."),
            E("Appearance", "/zonecontrol seticon", "<wcid> <iconDid|clear> [icon|overlay|overlay2|underlay]", "Set or clear a weenie's icon layers."),

            // ── Spells + effects ──────────────────────────────────────────────
            E("Spells", "/zonecontrol spell", "<name> <off|on|add|chance|remove|list> [spellId] [chancePct] [--wcid <id>]", "Zone-added spells and cast chances, per zone or per monster."),
            E("Effects", "/zonecontrol effect", "<name> [show | dot on|off | dmg <n> | type <t> | interval <s> | suppress on|off | suppress prodigal on|off | suppress regen <pct>]", "Zone-wide DoT and suppression effects."),

            // ── Loot ──────────────────────────────────────────────────────────
            E("Loot", "/zonecontrol currency", "<name> <add|remove|list> [itemWcid] [amount] [chance] [direct|corpse] [--wcid <id>]", "Bonus currency drops per kill."),
            E("Loot", "/zonecontrol modifier", "<name> <add|remove|list|catalog|band|slots|special|chance> [args] [--wcid <id>]", "Modifier (cantrip) pool, bands, slots, specials and chances."),
            E("Loot", "/zonecontrol modifier default", "<variation> <band|slots|special|chance> ...", "The same, on a Tier Default."),
            E("Loot", "/zonecontrol weaponcard", "<name> <chance_stat> <on|off|clear> | list", "Weapon card on/off per zone (default <var> weaponcard ... for a tier)."),
            E("Loot", "/zonecontrol craft", "<material> <itemtype> auto|allow|deny | list | get | materials | components ... | enabled true|false | mintier <tier> | test <material> <itemtype>", "Crafting rules for the zone's drops."),
            E("Loot", "/zonecontrol ladder", "status | apply [tier|all] | migrate [here|<player>] [--dry] | show", "Armor-level ladder: status, re-price, migrate."),

            // ── Generators + territory ────────────────────────────────────────
            E("Generators", "/zonecontrol genlist", "[<zone>]", "Placed generators in a zone (or where you stand) with counts; feeds the Generators tab."),
            E("Generators", "/zonecontrol geninfo", "<wcid>", "One generator's knobs: delay, radius, stagger, init, max."),
            E("Generators", "/zonecontrol genedit", "<wcid> delay|radius|stagger|init|max <value>", "Edit a generator weenie (ace_world) and clear its cache."),
            E("Generators", "/bossgroup", "status [--wire] | set <group> delay|radius <v> | clear <group> delay|radius | enable|disable <group> | respawn <group>", "Boss groups: generators sharing a BossGroupId. Overrides are runtime (shard store)."),
            E("Territory", "/zonecontrol survey", "<name> [lbHex]", "Per-landblock survey: generators and creatures reachable."),
            E("Territory", "/zonecontrol terrain", "<name> <hex> <type|clear>", "Terrain override for a landblock (spawn redirection)."),
            E("Territory", "/zonecontrol quests", "<name>", "The zone's quest registry rows."),

            // ── Readiness ─────────────────────────────────────────────────────
            E("Readiness", "/zonecontrol mobcheck", "[<zone>] [<wcid>]", "Is this monster ready in this zone? No wcid = the creature you have selected."),
            E("Readiness", "/zonecontrol mobcheckget", "[<zone>] [--wcid <id>]", "Machine twin of mobcheck ([[ZCMC]]) for the Readiness tab."),

            // ── Weapon scaling ────────────────────────────────────────────────
            E("Weapon Scaling", "/weaponscale", "show | enable on|off | tier <t> cap|minwield|minwieldtriune|minwieldskillcharm <n> | tier add|remove <t> | script <name> kmin|kmax|variance <v> | script <name> ladder <anchorS>|clear | script <name> grade <S..F-> <k> | script add|remove <name> | grade ... | kc min|max <v> | sync on|off | reset | reload | tighten", "Weapon aug-scaling config (the plugin's Weapons > Scaling panel)."),

            // ── Character forge ───────────────────────────────────────────────
            E("Character", "/testchar", "T0 | set attrs|vitals|level|enl|augs|charms|raugs <csv> | apply skills,spells,aetheria,manastone | extra <keys> | save | report | gems | charms | print <wcid>", "Test-character builder behind the Character tab."),
            E("Character", "/asforge", "<piece|suit|jewel|all> [tier 10-25] [cards:...] | premade <tier 10-25> <avg|bis> [force]", "Forge test armor / jewelry; premade = the 18-piece suit at a tier's Avg or BiS."),
            E("Character", "/wsforge", "<class|all> [quality 0-1000] [element] [tier 10-25] [cards:key=val,...] [bag] [force]", "Forge one Weapon Scaling test weapon (or a full set)."),

            // Retail admin / session commands (@cloak, @smite, /createliveops, /clearcache ...) are NOT listed
            // here (owner 2026-09-03: "not zc stuff - just normal admin commands").
        };

        /// <summary>Groups in first-seen order, for the prose form.</summary>
        public static IEnumerable<string> Groups => All.Select(e => e.Group).Distinct();

        private static string Wire(string s) => (s ?? "").Replace('~', '-');

        /// <summary>One [[ZCHELP]] line per entry: "[[ZCHELP]]i~n~group~command~usage~description" - '~' separated
        /// so usage strings keep their '|' alternatives readable; the plugin splits on '~' at most 6 ways.</summary>
        public static IEnumerable<string> WireLines()
        {
            var n = All.Length;
            for (var i = 0; i < n; i++)
            {
                var e = All[i];
                yield return new StringBuilder("[[ZCHELP]]").Append(i).Append('~').Append(n).Append('~')
                    .Append(Wire(e.Group)).Append('~').Append(Wire(e.Command)).Append('~')
                    .Append(Wire(e.Usage)).Append('~').Append(Wire(e.Description)).ToString();
            }
        }
    }
}
