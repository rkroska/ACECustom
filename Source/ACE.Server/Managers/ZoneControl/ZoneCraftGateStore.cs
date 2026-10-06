using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

using log4net;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Server.Factories;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>Which matrix COLUMN a target item sits in. The owner has ruled that jewelry, trinkets
    /// and cloaks all read as "Armor" in the plugin UI, but they stay SEPARATE here so a rule can still
    /// single one of them out - collapsing them at the data layer would make that impossible to author
    /// later without a store migration.</summary>
    public enum CraftItemClass
    {
        Weapon = 0,
        Armor = 1,
        Shield = 2,
        Jewelry = 3,
        Cloak = 4,
    }

    /// <summary>A matrix cell. <see cref="Auto"/> is the default and is NEVER stored - it means "no
    /// opinion, fall through to the downgrade rule" (layer 2).</summary>
    public enum CraftRuleMode
    {
        Auto = 0,
        Allow = 1,
        Deny = 2,
    }

    /// <summary>One authored cell: (material x item class) -> Allow / Deny. Persisted shape; the wire
    /// and the JSON both use the NAMES, never the ordinals, so reordering either enum is safe.</summary>
    public class CraftRule
    {
        /// <summary>PropertyInt.MaterialType (131) of the SALVAGE item.</summary>
        public int Material { get; set; }
        public string ItemType { get; set; }
        public string Mode { get; set; }
    }

    /// <summary>
    /// LAYER 1 of the T11+ crafting gate (Craft_Gate_Plan_2026-08-24.md): the authorable
    /// (item type x salvage material) matrix that sits ABOVE the downgrade rule in
    /// <see cref="ZoneCraftGate"/>. It also holds LAYER 0, the blocked-component list.
    ///
    ///   0. COMPONENTS explicit source-WCID block -> refuse, stop
    ///   1. MATRIX     explicit Allow / Deny      -> obey it, stop
    ///   2. DOWNGRADE  layer 2, unchanged         -> "a weaker imbue cant go on, but not imbued could"
    ///   3. DEFAULT    allow
    ///
    /// The matrix is SPARSE: only non-Auto cells are stored, so an untouched install persists `{}` and
    /// behaves exactly as the deployed layer-2-only gate does.
    ///
    /// PERSISTENCE follows <see cref="ZoneControlManager"/> exactly - one JSON blob in
    /// ace_shard.config_properties_string, and <see cref="Load"/> builds into LOCALS and commits only
    /// after the read and the parse have both succeeded. That ordering is not cosmetic: the zone store
    /// was fixed on 2026-08-23 because clearing the live collections before the read meant a DB blip
    /// emptied memory, and the next edit then wrote that empty store straight over the real one.
    /// </summary>
    public static class ZoneCraftGateStore
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const string StoreKey = "craftgate_data";

        /// <summary>The matrix column order the wire publishes, and the order `craft list` prints.
        /// APPEND-ONLY - the plugin renders columns from this list by NAME.</summary>
        public static readonly CraftItemClass[] Columns =
        {
            CraftItemClass.Weapon,
            CraftItemClass.Armor,
            CraftItemClass.Shield,
            CraftItemClass.Jewelry,
            CraftItemClass.Cloak,
        };

        /// <summary>The crafting components blocked at MinTier+ out of the box (owner 2026-08-25):
        ///
        ///   719220045  Fine Bandit Blade Hilt   Add +0.175 CriticalMultiplier, Add +0.25 CriticalFrequency
        ///   719220085  Finely Oiled Bowstring   Add +1.15  CriticalMultiplier, Add +0.25 CriticalFrequency
        ///
        /// WHY THESE NEED A LAYER OF THEIR OWN. Both write with ModificationOperation.Add, and layer 2 is
        /// a DOWNGRADE rule - it can never refuse an Add, because an Add always increases. Layer 1 is
        /// keyed by the salvage's MaterialType and NEITHER driver declares one (verified 2026-08-25: the
        /// only ints on both weenies are ItemType 128 and 25), so the matrix cannot index them either.
        /// Without layer 0 nothing in the gate reaches them and the bonuses stack, unbounded, on gear the
        /// T11+ loot pipeline already finished.
        ///
        /// Stored and matched by SOURCE WCID, never by recipe id. The hilt drives EIGHT recipes
        /// (527870063-67, 527870095-97) and the bowstring THREE (527870116-118) - only 527870063,
        /// 527870096 and 527870118 write crit, but listing recipe ids would mean an eleven-row table that
        /// a ninth hilt recipe walks straight past. One WCID closes every current and future recipe the
        /// component feeds.</summary>
        public static readonly uint[] DefaultBlockedComponents = { 719220045u, 719220085u };

        /// <summary>One candidate for layer 0: a source WCID and the GROUP the plugin files it under.
        ///
        /// WHY A CATALOG EXISTS AT ALL. The store records only what IS blocked - an ALLOWED component is
        /// simply absent from <see cref="Store.Components"/>. That is fine for the gate, which only ever
        /// asks "is this one blocked?", but it means the plugin has nothing to draw an UNCHECKED row
        /// from. The toggle UI needs the CANDIDATES, not just the current answer.
        ///
        /// WHY ONLY (Wcid, Group) AND NO DISPLAY TEXT. This table is published on the [[ZCCG]] wire
        /// (ZoneControlCommands.BuildCraftGatePayload; chunked since 2026-10-05, ~300 rows). The 49
        /// names alone measure 907 characters and a prose description per row would add ~4.7 KB to that
        /// single line. So the wire carries wcid~group (~1 KB) and the PLUGIN owns the display name and
        /// the one-line explanation, compiled in - the same split the cantrip catalog already uses.
        ///
        /// The DB names could not have served anyway, which is the other half of the reason: TWELVE of
        /// these weenies are named exactly "Foolproof" and three more are named "Salvage" (measured
        /// 2026-08-26). A UI built on wire names would show twelve identical rows.
        ///
        /// GROUPING IS EDITORIAL, not derived. It cuts across data_Id, ItemType and recipe id in ways no
        /// query reproduces, so it is authored here. A wrong auto-group is worse than no group.</summary>
        public readonly struct ComponentCatalogEntry
        {
            public readonly uint Wcid;
            public readonly string Group;
            public ComponentCatalogEntry(uint wcid, string group) { Wcid = wcid; Group = group; }
        }

        /// <summary>Every component the owner has decided is a layer-0 CANDIDATE, with its group.
        ///
        /// This is NOT the blocked set - it is the menu. Membership in the blocked set is
        /// <see cref="Store.Components"/>, published separately as `components=`. A WCID can be blocked
        /// without being here (someone typed `craft components add`), and the plugin MUST still render
        /// it - under "Other" - or the block would be invisible in the UI.
        ///
        /// Source: Component_Block_WCIDs_2026-08-25.md, applied 2026-08-26 as the 47-WCID list plus the
        /// two entries in <see cref="DefaultBlockedComponents"/>.</summary>
        public static readonly ComponentCatalogEntry[] ComponentCatalog =
        {
            // The two originals: crit bonuses written with ModificationOperation.Add, which layer 2 is
            // structurally incapable of refusing and layer 1 cannot index (they declare no MaterialType).
            new ComponentCatalogEntry(719220045u, "Default"),      // Fine Bandit Blade Hilt
            new ComponentCatalogEntry(719220085u, "Default"),      // Finely Oiled Bowstring

            // Elemental rends: 7 elements x 4 drivers (Salvaged, 100-bag, Foolproof, alt-Foolproof).
            new ComponentCatalogEntry(21086u, "Rend"), new ComponentCatalogEntry(30260u, "Rend"),
            new ComponentCatalogEntry(30104u, "Rend"), new ComponentCatalogEntry(36628u, "Rend"),
            new ComponentCatalogEntry(21054u, "Rend"), new ComponentCatalogEntry(29577u, "Rend"),
            new ComponentCatalogEntry(30099u, "Rend"), new ComponentCatalogEntry(36624u, "Rend"),
            new ComponentCatalogEntry(21048u, "Rend"), new ComponentCatalogEntry(29574u, "Rend"),
            new ComponentCatalogEntry(30097u, "Rend"), new ComponentCatalogEntry(36622u, "Rend"),
            new ComponentCatalogEntry(21037u, "Rend"), new ComponentCatalogEntry(29571u, "Rend"),
            new ComponentCatalogEntry(30094u, "Rend"), new ComponentCatalogEntry(36619u, "Rend"),
            new ComponentCatalogEntry(21069u, "Rend"), new ComponentCatalogEntry(29580u, "Rend"),
            new ComponentCatalogEntry(30102u, "Rend"), new ComponentCatalogEntry(36626u, "Rend"),
            new ComponentCatalogEntry(21039u, "Rend"), new ComponentCatalogEntry(29572u, "Rend"),
            new ComponentCatalogEntry(30095u, "Rend"), new ComponentCatalogEntry(36620u, "Rend"),
            new ComponentCatalogEntry(21056u, "Rend"), new ComponentCatalogEntry(29578u, "Rend"),
            new ComponentCatalogEntry(30100u, "Rend"), new ComponentCatalogEntry(36625u, "Rend"),

            // Armor Rending - the Sunstone family. Fraction of the target's armour IGNORED.
            new ComponentCatalogEntry(21079u, "ArmorRend"),
            new ComponentCatalogEntry(30103u, "ArmorRend"),
            new ComponentCatalogEntry(36627u, "ArmorRend"),

            // The one imbue recipe shard-wide with NO requirement of any kind - it can overwrite freely.
            new ComponentCatalogEntry(3110315u, "Combo"),          // Vial of Armor Rend

            new ComponentCatalogEntry(21064u, "Nether"), new ComponentCatalogEntry(60000u, "Nether"),
            new ComponentCatalogEntry(300011u, "Nether"), new ComponentCatalogEntry(64454645u, "Nether"),

            // ILT proc converters: each writes a rend AND ProcSpell AND ResistanceModifier +2 as an ADD.
            new ComponentCatalogEntry(527870013u, "Inscription"), new ComponentCatalogEntry(527870019u, "Inscription"),
            new ComponentCatalogEntry(527870020u, "Inscription"), new ComponentCatalogEntry(527870021u, "Inscription"),
            new ComponentCatalogEntry(527870022u, "Inscription"), new ComponentCatalogEntry(527870023u, "Inscription"),
            new ComponentCatalogEntry(527870024u, "Inscription"), new ComponentCatalogEntry(527870031u, "Inscription"),

            // T10 vendor proc tools (owner 2026-10-06: "these should already be there? If not, get them there"): each writes a
            // ProcSpell + ProcSpellRate (5-15 pct) onto jewelry (Inscriptions) / armor (Paragon gems) - the same base wcids T11 drops
            // are built on, so unblocked they put an OLD proc on T11 gear, where the low-tier proc gate cannot see it.
            new ComponentCatalogEntry(227001u, "ProcInscription"),   // Inscription of Acid -> Acid Streak I
            new ComponentCatalogEntry(227002u, "ProcInscription"),   // Inscription of Flame -> Flame Streak I
            new ComponentCatalogEntry(227003u, "ProcInscription"),   // Inscription of Frost -> Frost Streak I
            new ComponentCatalogEntry(227004u, "ProcInscription"),   // Inscription of Lightning -> Lightning Streak I
            new ComponentCatalogEntry(227005u, "ProcInscription"),   // Inscription of Force -> Force Streak I
            new ComponentCatalogEntry(227006u, "ProcInscription"),   // Inscription of Blade -> Whirling Blade Streak I
            new ComponentCatalogEntry(227007u, "ProcInscription"),   // Inscription of Shock Wave -> Shock Wave Streak I
            new ComponentCatalogEntry(227008u, "ProcInscription"),   // Inscription of Nether -> Nether Streak I
            new ComponentCatalogEntry(227009u, "ProcInscription"),   // Inscription of Healing -> Heal Self I
            new ComponentCatalogEntry(227010u, "ProcInscription"),   // Inscription of Acid Vulnerability -> Acid Vulnerability Other I
            new ComponentCatalogEntry(227011u, "ProcInscription"),   // Inscription of Fire Vulnerability -> Fire Vulnerability Other I
            new ComponentCatalogEntry(227012u, "ProcInscription"),   // Inscription of Cold Vulnerability -> Cold Vulnerability Other I
            new ComponentCatalogEntry(227013u, "ProcInscription"),   // Inscription of Lightning Vulnerability -> Lightning Vulnerability Other I
            new ComponentCatalogEntry(227014u, "ProcInscription"),   // Inscription of Piercing Vulnerability -> Piercing Vulnerability Other I
            new ComponentCatalogEntry(227015u, "ProcInscription"),   // Inscription of Blade Vulnerability -> Blade Vulnerability Other I
            new ComponentCatalogEntry(227016u, "ProcInscription"),   // Inscription of Bludgeoning Vulnerability -> Bludgeoning Vulnerability Other I
            new ComponentCatalogEntry(227017u, "ProcInscription"),   // Inscription of Imperil -> Imperil Other I
            new ComponentCatalogEntry(227018u, "ProcInscription"),   // Inscription of Weakening -> Weakening Curse I
            new ComponentCatalogEntry(64454515u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Breath of Renewal
            new ComponentCatalogEntry(64454516u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Robustification
            new ComponentCatalogEntry(64454517u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Mana Blast
            new ComponentCatalogEntry(64454518u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Incantation of Nullify Item Magic
            new ComponentCatalogEntry(64454519u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Incantation of Nullify Life Magic Self
            new ComponentCatalogEntry(64454520u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Incantation of Nullify Creature Magic Self
            new ComponentCatalogEntry(64454521u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Armor Breach
            new ComponentCatalogEntry(64454522u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Arcane Pyramid
            new ComponentCatalogEntry(64454523u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Blade Arc I
            new ComponentCatalogEntry(64454524u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Thousand Fists
            new ComponentCatalogEntry(64454525u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Torrential Acid
            new ComponentCatalogEntry(64454526u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Flame Chain
            new ComponentCatalogEntry(64454527u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Spectral Flame
            new ComponentCatalogEntry(64454528u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Violet Rain
            new ComponentCatalogEntry(64454529u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Incantation of Corrosion
            new ComponentCatalogEntry(64454530u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Incantation of Corruption
            new ComponentCatalogEntry(64454531u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Incantation of Destructive Curse
            new ComponentCatalogEntry(64454532u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Incantation of Festering Curse
            new ComponentCatalogEntry(64454533u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Incantation of Weakening Curse
            new ComponentCatalogEntry(64454534u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Incantation of Brittlemail
            new ComponentCatalogEntry(64454535u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Incantation of Spirit Loather
            new ComponentCatalogEntry(64454536u, "ParagonGem"),   // Gem of the 50th Tier Paragon for Armor -> Incantation of Blood Loather
            new ComponentCatalogEntry(64454537u, "ParagonGem"),   // Clouded 50th Tier Paragon Armor Gem -> Breath of Renewal
            new ComponentCatalogEntry(64454538u, "ParagonGem"),   // Clouded 50th Tier Paragon Armor Gem -> Robustification
            new ComponentCatalogEntry(64454539u, "ParagonGem"),   // Clouded 50th Tier Paragon Armor Gem -> Mana Blast
            new ComponentCatalogEntry(64454540u, "ParagonGem"),   // Clouded 50th Tier Paragon Armor Gem -> Incantation of Nullify Item Magic
            new ComponentCatalogEntry(64454541u, "ParagonGem"),   // Clouded 50th Tier Paragon Armor Gem -> Incantation of Nullify Life Magic Self
            new ComponentCatalogEntry(64454542u, "ParagonGem"),   // Clouded 50th Tier Paragon Armor Gem -> Incantation of Nullify Creature Magic Self
            new ComponentCatalogEntry(64454543u, "ParagonGem"),   // Clouded 50th Tier Paragon Armor Gem -> Armor Breach
            new ComponentCatalogEntry(64454544u, "ParagonGem"),   // Clouded 50th Tier Paragon Armor Gem -> Arcane Pyramid
            new ComponentCatalogEntry(64454545u, "ParagonGem"),   // Clouded 50th Tier Paragon Armor Gem -> Blade Arc I
            new ComponentCatalogEntry(64454546u, "ParagonGem"),   // Clouded 50th Tier Paragon Armor Gem -> Thousand Fists
            new ComponentCatalogEntry(64454547u, "ParagonGem"),   // Clouded 50th Tier Paragon Armor Gem -> Torrential Acid
            new ComponentCatalogEntry(64454548u, "ParagonGem"),   // Clouded 50th Tier Paragon Armor Gem -> Flame Chain
            new ComponentCatalogEntry(64454549u, "ParagonGem"),   // Clouded 50th Tier Paragon Armor Gem -> Spectral Flame

            // Split arrows - both write SplitArrowCount +1 as an ADD, repeatable to 10.
            new ComponentCatalogEntry(21085u, "Split"),            // Salvaged White Quartz (also Cleaving)
            new ComponentCatalogEntry(21081u, "Split"),            // Salvaged Tiger Eye

            new ComponentCatalogEntry(227190065u, "GemBag"),       // Bag of Abyssal-Touched Gems

            // EVERY tinker and imbue in the game (owner 2026-10-05). Generated from the world DB (every
            // TinkeringMaterial salvage + every Foolproof) plus the non-salvage items that change T11 gear
            // (ZoneControl\T11_Tinker_Matrix_2026-10-05.md). Candidates only - nothing here is blocked until
            // the owner ticks it (or its group) in GM Tools > Crafting > Components.
            new ComponentCatalogEntry(29582u, "ArmorRend"),           // Sunstone - Salvaged, 100-bag

            new ComponentCatalogEntry(30096u, "Imbue"),               // Black Opal - Foolproof
            new ComponentCatalogEntry(36621u, "Imbue"),               // Black Opal - Foolproof (alt)
            new ComponentCatalogEntry(21040u, "Imbue"),               // Black Opal - Salvaged
            new ComponentCatalogEntry(29573u, "Imbue"),               // Black Opal - Salvaged, 100-bag
            new ComponentCatalogEntry(30098u, "Imbue"),               // Fire Opal - Foolproof
            new ComponentCatalogEntry(36623u, "Imbue"),               // Fire Opal - Foolproof (alt)
            new ComponentCatalogEntry(21049u, "Imbue"),               // Fire Opal - Salvaged
            new ComponentCatalogEntry(29575u, "Imbue"),               // Fire Opal - Salvaged, 100-bag
            new ComponentCatalogEntry(30101u, "Imbue"),               // Peridot - Foolproof
            new ComponentCatalogEntry(36634u, "Imbue"),               // Peridot - Foolproof (alt)
            new ComponentCatalogEntry(21066u, "Imbue"),               // Peridot - Salvaged
            new ComponentCatalogEntry(30105u, "Imbue"),               // Yellow Topaz - Foolproof
            new ComponentCatalogEntry(36635u, "Imbue"),               // Yellow Topaz - Foolproof (alt)
            new ComponentCatalogEntry(21088u, "Imbue"),               // Yellow Topaz - Salvaged
            new ComponentCatalogEntry(30106u, "Imbue"),               // Zircon - Foolproof
            new ComponentCatalogEntry(36636u, "Imbue"),               // Zircon - Foolproof (alt)
            new ComponentCatalogEntry(21089u, "Imbue"),               // Zircon - Salvaged

            new ComponentCatalogEntry(21034u, "Tinker"),              // Agate - Salvaged
            new ComponentCatalogEntry(20980u, "Tinker"),              // Alabaster - Salvaged
            new ComponentCatalogEntry(21035u, "Tinker"),              // Amber - Salvaged
            new ComponentCatalogEntry(70737u, "Tinker"),              // Amber - Salvaged (alt)
            new ComponentCatalogEntry(21036u, "Tinker"),              // Amethyst - Salvaged
            new ComponentCatalogEntry(20981u, "Tinker"),              // Armoredillo Hide - Salvaged
            new ComponentCatalogEntry(21038u, "Tinker"),              // Azurite - Salvaged
            new ComponentCatalogEntry(21041u, "Tinker"),              // Bloodstone - Salvaged
            new ComponentCatalogEntry(21042u, "Tinker"),              // Brass - Salvaged
            new ComponentCatalogEntry(36570u, "Tinker"),              // Brass - Salvaged (alt)
            new ComponentCatalogEntry(20982u, "Tinker"),              // Bronze - Salvaged
            new ComponentCatalogEntry(21043u, "Tinker"),              // Carnelian - Salvaged
            new ComponentCatalogEntry(20983u, "Tinker"),              // Ceramic - Salvaged
            new ComponentCatalogEntry(21044u, "Tinker"),              // Citrine - Salvaged
            new ComponentCatalogEntry(21045u, "Tinker"),              // Copper - Salvaged
            new ComponentCatalogEntry(64454630u, "Tinker"),           // Corrupted Brass Salvage
            new ComponentCatalogEntry(64454634u, "Tinker"),           // Corrupted Granite Salvage
            new ComponentCatalogEntry(64454640u, "Tinker"),           // Corrupted Green Garnet Salvage
            new ComponentCatalogEntry(64454632u, "Tinker"),           // Corrupted Iron Salvage
            new ComponentCatalogEntry(64454638u, "Tinker"),           // Corrupted Mahogany Salvage
            new ComponentCatalogEntry(64454636u, "Tinker"),           // Corrupted Velvet Salvage
            new ComponentCatalogEntry(21046u, "Tinker"),              // Diamond - Salvaged
            new ComponentCatalogEntry(70738u, "Tinker"),              // Diamond - Salvaged (alt)
            new ComponentCatalogEntry(21047u, "Tinker"),              // Ebony - Salvaged
            new ComponentCatalogEntry(20984u, "Tinker"),              // Gold - Salvaged
            new ComponentCatalogEntry(20985u, "Tinker"),              // Granite - Salvaged
            new ComponentCatalogEntry(33620u, "Tinker"),              // Granite - Salvaged (alt)
            new ComponentCatalogEntry(29576u, "Tinker"),              // Granite - Salvaged, 100-bag
            new ComponentCatalogEntry(21050u, "Tinker"),              // Green Garnet - Salvaged
            new ComponentCatalogEntry(36571u, "Tinker"),              // Green Garnet - Salvaged (alt)
            new ComponentCatalogEntry(21051u, "Tinker"),              // Green Jade - Salvaged
            new ComponentCatalogEntry(21052u, "Tinker"),              // Gromnie Hide - Salvaged
            new ComponentCatalogEntry(41777u, "Tinker"),              // Gromnie Hide - Salvaged (alt)
            new ComponentCatalogEntry(21053u, "Tinker"),              // Hematite - Salvaged
            new ComponentCatalogEntry(30092u, "Tinker"),              // Infinite Ivory
            new ComponentCatalogEntry(30093u, "Tinker"),              // Infinite Leather
            new ComponentCatalogEntry(20986u, "Tinker"),              // Iron - Salvaged
            new ComponentCatalogEntry(36572u, "Tinker"),              // Iron - Salvaged (alt)
            new ComponentCatalogEntry(21055u, "Tinker"),              // Ivory - Salvaged
            new ComponentCatalogEntry(21057u, "Tinker"),              // Lapis Lazuli - Salvaged
            new ComponentCatalogEntry(21058u, "Tinker"),              // Lavender Jade - Salvaged
            new ComponentCatalogEntry(21059u, "Tinker"),              // Leather - Salvaged
            new ComponentCatalogEntry(20987u, "Tinker"),              // Linen - Salvaged
            new ComponentCatalogEntry(20988u, "Tinker"),              // Mahogany - Salvaged
            new ComponentCatalogEntry(29579u, "Tinker"),              // Mahogany - Salvaged, 100-bag
            new ComponentCatalogEntry(21060u, "Tinker"),              // Malachite - Salvaged
            new ComponentCatalogEntry(21061u, "Tinker"),              // Marble - Salvaged
            new ComponentCatalogEntry(21062u, "Tinker"),              // Moonstone - Salvaged
            new ComponentCatalogEntry(34965u, "Tinker"),              // Mucor-altered Mahogany
            new ComponentCatalogEntry(87422u, "Tinker"),              // Mucor-altered Opal
            new ComponentCatalogEntry(20989u, "Tinker"),              // Oak - Salvaged
            new ComponentCatalogEntry(21063u, "Tinker"),              // Obsidian - Salvaged
            new ComponentCatalogEntry(21065u, "Tinker"),              // Opal - Salvaged
            new ComponentCatalogEntry(36574u, "Tinker"),              // Opal - Salvaged (alt)
            new ComponentCatalogEntry(20990u, "Tinker"),              // Pine - Salvaged
            new ComponentCatalogEntry(21067u, "Tinker"),              // Porcelain - Salvaged
            new ComponentCatalogEntry(21068u, "Tinker"),              // Pyreal - Salvaged
            new ComponentCatalogEntry(41772u, "Tinker"),              // Pyreal - Salvaged (alt)
            new ComponentCatalogEntry(21070u, "Tinker"),              // Red Jade - Salvaged
            new ComponentCatalogEntry(20991u, "Tinker"),              // Reedshark Hide - Salvaged
            new ComponentCatalogEntry(64454628u, "Tinker"),           // Refined White Jade Salvage
            new ComponentCatalogEntry(21071u, "Tinker"),              // Rose Quartz - Salvaged
            new ComponentCatalogEntry(21072u, "Tinker"),              // Ruby - Salvaged
            new ComponentCatalogEntry(70741u, "Tinker"),              // Ruby - Salvaged (alt)
            new ComponentCatalogEntry(21073u, "Tinker"),              // Sandstone - Salvaged
            new ComponentCatalogEntry(43946u, "Tinker"),              // Sandstone - Salvaged (alt)
            new ComponentCatalogEntry(21074u, "Tinker"),              // Sapphire - Salvaged
            new ComponentCatalogEntry(70736u, "Tinker"),              // Sapphire - Salvaged (alt)
            new ComponentCatalogEntry(20992u, "Tinker"),              // Satin - Salvaged
            new ComponentCatalogEntry(21075u, "Tinker"),              // Serpentine - Salvaged
            new ComponentCatalogEntry(21076u, "Tinker"),              // Silk - Salvaged
            new ComponentCatalogEntry(21077u, "Tinker"),              // Silver - Salvaged
            new ComponentCatalogEntry(21078u, "Tinker"),              // Smoky Quartz - Salvaged
            new ComponentCatalogEntry(20993u, "Tinker"),              // Steel - Salvaged
            new ComponentCatalogEntry(33621u, "Tinker"),              // Steel - Salvaged (alt)
            new ComponentCatalogEntry(9920993u, "Tinker"),            // Steel - Salvaged (alt)
            new ComponentCatalogEntry(29581u, "Tinker"),              // Steel - Salvaged, 100-bag
            new ComponentCatalogEntry(21080u, "Tinker"),              // Teak - Salvaged
            new ComponentCatalogEntry(21082u, "Tinker"),              // Tourmaline - Salvaged
            new ComponentCatalogEntry(21083u, "Tinker"),              // Turquoise - Salvaged
            new ComponentCatalogEntry(20994u, "Tinker"),              // Velvet - Salvaged
            new ComponentCatalogEntry(36573u, "Tinker"),              // Velvet - Salvaged (alt)
            new ComponentCatalogEntry(21084u, "Tinker"),              // White Jade - Salvaged
            new ComponentCatalogEntry(20995u, "Tinker"),              // Wool - Salvaged
            new ComponentCatalogEntry(21087u, "Tinker"),              // Yellow Garnet - Salvaged

            new ComponentCatalogEntry(53016u, "Amber"),               // Corrupted Amber: Bracers of the Corrupted Heart
            new ComponentCatalogEntry(53021u, "Amber"),               // Corrupted Amber: Breastplate of the Corrupted Soul
            new ComponentCatalogEntry(53017u, "Amber"),               // Corrupted Amber: Gauntlets of the Corrupted Heart
            new ComponentCatalogEntry(53022u, "Amber"),               // Corrupted Amber: Girth of the Corrupted Soul
            new ComponentCatalogEntry(53023u, "Amber"),               // Corrupted Amber: Greaves of the Corrupted Soul
            new ComponentCatalogEntry(53018u, "Amber"),               // Corrupted Amber: Helm of the Corrupted Heart
            new ComponentCatalogEntry(53019u, "Amber"),               // Corrupted Amber: Pauldrons of the Corrupted Heart
            new ComponentCatalogEntry(53020u, "Amber"),               // Corrupted Amber: Sollerets of the Corrupted Heart
            new ComponentCatalogEntry(53024u, "Amber"),               // Corrupted Amber: Tassets of the Corrupted Soul
            new ComponentCatalogEntry(53452u, "Amber"),               // Corrupted Amber: Weapon of the Corrupted Heart
            new ComponentCatalogEntry(53453u, "Amber"),               // Corrupted Amber: Weapon of the Corrupted Soul
            new ComponentCatalogEntry(53066u, "Amber"),               // Empowered Amber: Bracers of Life
            new ComponentCatalogEntry(53067u, "Amber"),               // Empowered Amber: Breastplate of Life
            new ComponentCatalogEntry(53068u, "Amber"),               // Empowered Amber: Gauntlets of Life
            new ComponentCatalogEntry(53069u, "Amber"),               // Empowered Amber: Girth of Life
            new ComponentCatalogEntry(53070u, "Amber"),               // Empowered Amber: Greaves of Life
            new ComponentCatalogEntry(53071u, "Amber"),               // Empowered Amber: Helm of Life
            new ComponentCatalogEntry(53072u, "Amber"),               // Empowered Amber: Pauldrons of Life
            new ComponentCatalogEntry(53440u, "Amber"),               // Empowered Amber: Shield Reinforcement
            new ComponentCatalogEntry(53073u, "Amber"),               // Empowered Amber: Sollerets of Life
            new ComponentCatalogEntry(53074u, "Amber"),               // Empowered Amber: Tassets of Life
            new ComponentCatalogEntry(53147u, "Amber"),               // Guardian of Ash
            new ComponentCatalogEntry(53155u, "Amber"),               // Luminous Amber of the 10th Tier Paragon
            new ComponentCatalogEntry(53156u, "Amber"),               // Luminous Amber of the 11th Tier Paragon
            new ComponentCatalogEntry(53157u, "Amber"),               // Luminous Amber of the 12th Tier Paragon
            new ComponentCatalogEntry(53158u, "Amber"),               // Luminous Amber of the 13th Tier Paragon
            new ComponentCatalogEntry(53159u, "Amber"),               // Luminous Amber of the 14th Tier Paragon
            new ComponentCatalogEntry(53160u, "Amber"),               // Luminous Amber of the 15th Tier Paragon
            new ComponentCatalogEntry(53161u, "Amber"),               // Luminous Amber of the 16th Tier Paragon
            new ComponentCatalogEntry(53162u, "Amber"),               // Luminous Amber of the 17th Tier Paragon
            new ComponentCatalogEntry(53163u, "Amber"),               // Luminous Amber of the 18th Tier Paragon
            new ComponentCatalogEntry(53164u, "Amber"),               // Luminous Amber of the 19th Tier Paragon
            new ComponentCatalogEntry(53145u, "Amber"),               // Luminous Amber of the 1st Tier Paragon
            new ComponentCatalogEntry(53165u, "Amber"),               // Luminous Amber of the 20th Tier Paragon
            new ComponentCatalogEntry(53166u, "Amber"),               // Luminous Amber of the 21st Tier Paragon
            new ComponentCatalogEntry(53167u, "Amber"),               // Luminous Amber of the 22nd Tier Paragon
            new ComponentCatalogEntry(53168u, "Amber"),               // Luminous Amber of the 23rd Tier Paragon
            new ComponentCatalogEntry(53169u, "Amber"),               // Luminous Amber of the 24th Tier Paragon
            new ComponentCatalogEntry(53170u, "Amber"),               // Luminous Amber of the 25th Tier Paragon
            new ComponentCatalogEntry(53171u, "Amber"),               // Luminous Amber of the 26th Tier Paragon
            new ComponentCatalogEntry(53172u, "Amber"),               // Luminous Amber of the 27th Tier Paragon
            new ComponentCatalogEntry(53173u, "Amber"),               // Luminous Amber of the 28th Tier Paragon
            new ComponentCatalogEntry(53174u, "Amber"),               // Luminous Amber of the 29th Tier Paragon
            new ComponentCatalogEntry(53146u, "Amber"),               // Luminous Amber of the 2nd Tier Paragon
            new ComponentCatalogEntry(53175u, "Amber"),               // Luminous Amber of the 30th Tier Paragon
            new ComponentCatalogEntry(53176u, "Amber"),               // Luminous Amber of the 31st Tier Paragon
            new ComponentCatalogEntry(53177u, "Amber"),               // Luminous Amber of the 32nd Tier Paragon
            new ComponentCatalogEntry(53178u, "Amber"),               // Luminous Amber of the 33rd Tier Paragon
            new ComponentCatalogEntry(53179u, "Amber"),               // Luminous Amber of the 34th Tier Paragon
            new ComponentCatalogEntry(53180u, "Amber"),               // Luminous Amber of the 35th Tier Paragon
            new ComponentCatalogEntry(53181u, "Amber"),               // Luminous Amber of the 36th Tier Paragon
            new ComponentCatalogEntry(53182u, "Amber"),               // Luminous Amber of the 37th Tier Paragon
            new ComponentCatalogEntry(53183u, "Amber"),               // Luminous Amber of the 38th Tier Paragon
            new ComponentCatalogEntry(53184u, "Amber"),               // Luminous Amber of the 39th Tier Paragon
            new ComponentCatalogEntry(53148u, "Amber"),               // Luminous Amber of the 3rd Tier Paragon
            new ComponentCatalogEntry(53185u, "Amber"),               // Luminous Amber of the 40th Tier Paragon
            new ComponentCatalogEntry(53186u, "Amber"),               // Luminous Amber of the 41st Tier Paragon
            new ComponentCatalogEntry(53187u, "Amber"),               // Luminous Amber of the 42nd Tier Paragon
            new ComponentCatalogEntry(53188u, "Amber"),               // Luminous Amber of the 43rd Tier Paragon
            new ComponentCatalogEntry(53189u, "Amber"),               // Luminous Amber of the 44th Tier Paragon
            new ComponentCatalogEntry(53190u, "Amber"),               // Luminous Amber of the 45th Tier Paragon
            new ComponentCatalogEntry(53191u, "Amber"),               // Luminous Amber of the 46th Tier Paragon
            new ComponentCatalogEntry(53192u, "Amber"),               // Luminous Amber of the 47th Tier Paragon
            new ComponentCatalogEntry(53193u, "Amber"),               // Luminous Amber of the 48th Tier Paragon
            new ComponentCatalogEntry(53194u, "Amber"),               // Luminous Amber of the 49th Tier Paragon
            new ComponentCatalogEntry(53149u, "Amber"),               // Luminous Amber of the 4th Tier Paragon
            new ComponentCatalogEntry(53195u, "Amber"),               // Luminous Amber of the 50th Tier Paragon
            new ComponentCatalogEntry(53150u, "Amber"),               // Luminous Amber of the 5th Tier Paragon
            new ComponentCatalogEntry(53151u, "Amber"),               // Luminous Amber of the 6th Tier Paragon
            new ComponentCatalogEntry(53152u, "Amber"),               // Luminous Amber of the 7th Tier Paragon
            new ComponentCatalogEntry(53153u, "Amber"),               // Luminous Amber of the 8th Tier Paragon
            new ComponentCatalogEntry(53154u, "Amber"),               // Luminous Amber of the 9th Tier Paragon
            new ComponentCatalogEntry(53293u, "Amber"),               // Luminous Amber: Bracers of Thunderous Blows
            new ComponentCatalogEntry(53297u, "Amber"),               // Luminous Amber: Breastplate of the Bulwark
            new ComponentCatalogEntry(53299u, "Amber"),               // Luminous Amber: Gauntlets of the Storm
            new ComponentCatalogEntry(53298u, "Amber"),               // Luminous Amber: Girth of the Bulwark
            new ComponentCatalogEntry(53295u, "Amber"),               // Luminous Amber: Greaves of the Tower
            new ComponentCatalogEntry(53301u, "Amber"),               // Luminous Amber: Helm of Healing
            new ComponentCatalogEntry(53294u, "Amber"),               // Luminous Amber: Pauldrons of Thunderous Blows
            new ComponentCatalogEntry(53441u, "Amber"),               // Luminous Amber: Shield Fortification
            new ComponentCatalogEntry(53300u, "Amber"),               // Luminous Amber: Sollerets of the Storm
            new ComponentCatalogEntry(53296u, "Amber"),               // Luminous Amber: Tassets of the Tower

            new ComponentCatalogEntry(44636u, "Special"),             // A'nekshay Slayer Stone
            new ComponentCatalogEntry(64454900u, "Special"),          // Admin Only Turquiose Gem
            new ComponentCatalogEntry(64454901u, "Special"),          // Admin Only White Quartz Gem
            new ComponentCatalogEntry(64454902u, "Special"),          // Admin Only Yellow Garnet Gem
            new ComponentCatalogEntry(34042u, "Special"),             // Black Skull of Xikma
            new ComponentCatalogEntry(23850u, "Special"),             // Brilliant Shard
            new ComponentCatalogEntry(23855u, "Special"),             // Charged Shard
            new ComponentCatalogEntry(23854u, "Special"),             // Chilled Shard
            new ComponentCatalogEntry(227190160u, "Special"),         // Concentrated Ember Essence
            new ComponentCatalogEntry(64454512u, "Special"),          // DH Fix of the 48th Tier Paragon for Armor
            new ComponentCatalogEntry(98854501u, "Special"),          // Dev Fix Gem of the 48th Tier Paragon for Armor
            new ComponentCatalogEntry(99954501u, "Special"),          // Dev Fix Gem of the 48th Tier Paragon for Armor
            new ComponentCatalogEntry(227190088u, "Special"),         // Fetish of the Corrupt Idols
            new ComponentCatalogEntry(27795u, "Special"),             // Fetish of the Dark Idols
            new ComponentCatalogEntry(52757u, "Special"),             // Gauntlet Brutality Amplification
            new ComponentCatalogEntry(52758u, "Special"),             // Gauntlet Defense Amplification
            new ComponentCatalogEntry(94454996u, "Special"),          // Gem of Paragon Proc Swap
            new ComponentCatalogEntry(94454995u, "Special"),          // Gem of Paragon Self Proc
            new ComponentCatalogEntry(35492u, "Special"),             // Gem of Spectral Force
            new ComponentCatalogEntry(53305u, "Special"),             // Gem of Verdant Force
            new ComponentCatalogEntry(64454101u, "Special"),          // Gem of the 1st Tier Paragon for Armor
            new ComponentCatalogEntry(644541011u, "Special"),         // Gem of the 1st Tier Paragon for Armor
            new ComponentCatalogEntry(33688u, "Special"),             // Greater Mukkir Slayer Stone
            new ComponentCatalogEntry(23856u, "Special"),             // Hardened Shard
            new ComponentCatalogEntry(53415u, "Special"),             // Horizon's Edge Amplification
            new ComponentCatalogEntry(90000075u, "Special"),          // Imbued Bloodstone Shard
            new ComponentCatalogEntry(32937u, "Special"),             // Lucky White Rabbit's Foot
            new ComponentCatalogEntry(35491u, "Special"),             // Maelstrom of Souls Gem
            new ComponentCatalogEntry(36631u, "Special"),             // Magic Defense Weapon Augmentation
            new ComponentCatalogEntry(41494u, "Special"),             // Major Item Tinkering Armature
            new ComponentCatalogEntry(41497u, "Special"),             // Major Item Tinkering Armature
            new ComponentCatalogEntry(41499u, "Special"),             // Major Item Tinkering Armature
            new ComponentCatalogEntry(41502u, "Special"),             // Major Item Tinkering Armature
            new ComponentCatalogEntry(71420u, "Special"),             // Major Item Tinkering Armature
            new ComponentCatalogEntry(71421u, "Special"),             // Major Item Tinkering Armature
            new ComponentCatalogEntry(41493u, "Special"),             // Minor Item Tinkering Armature
            new ComponentCatalogEntry(41501u, "Special"),             // Minor Item Tinkering Armature
            new ComponentCatalogEntry(41506u, "Special"),             // Minor Item Tinkering Armature
            new ComponentCatalogEntry(71425u, "Special"),             // Minor Item Tinkering Armature
            new ComponentCatalogEntry(71426u, "Special"),             // Minor Item Tinkering Armature
            new ComponentCatalogEntry(71427u, "Special"),             // Minor Item Tinkering Armature
            new ComponentCatalogEntry(36633u, "Special"),             // Missile Defense Weapon Augmentation
            new ComponentCatalogEntry(64454642u, "Special"),          // Mist of the Abyss
            new ComponentCatalogEntry(41492u, "Special"),             // Moderate Item Tinkering Armature
            new ComponentCatalogEntry(41498u, "Special"),             // Moderate Item Tinkering Armature
            new ComponentCatalogEntry(41500u, "Special"),             // Moderate Item Tinkering Armature
            new ComponentCatalogEntry(71422u, "Special"),             // Moderate Item Tinkering Armature
            new ComponentCatalogEntry(71423u, "Special"),             // Moderate Item Tinkering Armature
            new ComponentCatalogEntry(71424u, "Special"),             // Moderate Item Tinkering Armature
            new ComponentCatalogEntry(52756u, "Special"),             // Nature's Wrath Amplification
            new ComponentCatalogEntry(23852u, "Special"),             // Plated Shard
            new ComponentCatalogEntry(98760169u, "Special"),          // Pure Quiddity Essence
            new ComponentCatalogEntry(23849u, "Special"),             // Scored Shard
            new ComponentCatalogEntry(23853u, "Special"),             // Seared Shard
            new ComponentCatalogEntry(719220022u, "Special"),         // Shadow Armor Infusion
            new ComponentCatalogEntry(719220032u, "Special"),         // Shadow Damage Infusion
            new ComponentCatalogEntry(719220074u, "Special"),         // Shadow Nether Infusion
            new ComponentCatalogEntry(719220037u, "Special"),         // Shadow Precision Infusion
            new ComponentCatalogEntry(719220065u, "Special"),         // Shadow Protection Infusion
            new ComponentCatalogEntry(719220029u, "Special"),         // Shadow Vitality Infusion
            new ComponentCatalogEntry(23851u, "Special"),             // Solid Shard
            new ComponentCatalogEntry(42038u, "Special"),             // Spectral Skull
            new ComponentCatalogEntry(3110215u, "Special"),           // Vial of Flamma Spirit
            new ComponentCatalogEntry(3110214u, "Special"),           // Vial of Olthoi Spit
        };

        /// <summary>The catalog, for the wire. Deliberately NOT filtered against the blocked set - the
        /// plugin needs every candidate so it can draw the unticked rows.</summary>
        public static IEnumerable<ComponentCatalogEntry> ComponentCatalogRows() => ComponentCatalog;

        private class Store
        {
            /// <summary>Master switch. False bypasses the WHOLE gate (layer 2 included), so the gate can
            /// be turned off without clearing authored rules.</summary>
            [DefaultValue(true)]
            public bool Enabled { get; set; } = true;

            /// <summary>The tier at which the gate starts applying. Replaces the hardcoded
            /// LootGenerationFactory.ZoneLootSetMinTier comparison; 11 is still the default.</summary>
            [DefaultValue(LootGenerationFactory.ZoneLootSetMinTier)]
            public int MinTier { get; set; } = LootGenerationFactory.ZoneLootSetMinTier;

            /// <summary>Non-Auto cells only. Null (not []) when empty, so an untouched store is `{}`.</summary>
            public List<CraftRule> Rules { get; set; }

            /// <summary>LAYER 0's toggle. Defaults ON: the owner asked for these components to be
            /// blocked, so the requested behaviour has to be what a fresh store does - a default of OFF
            /// would mean the block only exists once somebody remembers to type a verb. Flipping it off
            /// is one command and needs no rebuild, which is the whole point of it being a toggle.</summary>
            [DefaultValue(true)]
            public bool BlockComponents { get; set; } = true;

            /// <summary>The blocked source WCIDs. NULL means "never authored - use
            /// <see cref="DefaultBlockedComponents"/>"; a non-null list (INCLUDING an empty one) is the
            /// owner's own list and is used verbatim. That distinction is why this is not a hardcode:
            /// clearing the list to [] really does block nothing, and it survives a restart.</summary>
            public List<uint> Components { get; set; }
        }

        /// <summary>Serializer settings that make the sparse store actually sparse: a store at every
        /// default serializes to `{}`.</summary>
        private static readonly JsonSerializerSettings SparseJson = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
        };

        private static readonly object _lock = new object();
        private static volatile bool _initialized;

        // ── lock-free read snapshot ──
        // IsBlocked runs on the crafting path; it reads these volatiles with no lock. Mutations rebuild
        // a fresh dictionary under _lock and swap it in, so a reader sees the old map or the new one,
        // never a half-written one.
        private static volatile Dictionary<(int Material, CraftItemClass Class), CraftRuleMode> _rules
            = new Dictionary<(int, CraftItemClass), CraftRuleMode>();
        private static volatile bool _enabled = true;
        private static volatile int _minTier = LootGenerationFactory.ZoneLootSetMinTier;

        // Layer 0. _components is always the EFFECTIVE set (the built-in default until the owner edits
        // it); _componentsAuthored only decides whether Save writes the list or writes null, so a store
        // that has never been touched stays `{}` and keeps tracking the default if it ever changes.
        private static volatile bool _blockComponents = true;
        private static volatile HashSet<uint> _components = new HashSet<uint>(DefaultBlockedComponents);
        private static volatile bool _componentsAuthored;

        #region init / persistence

        public static void EnsureLoaded() => EnsureInitialized();

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            lock (_lock)
            {
                if (_initialized)
                    return;

                // Load() is atomic (build-then-commit): a failure leaves whatever was already loaded
                // untouched, which on first init is the safe all-Auto default.
                try { Load(); }
                catch (Exception ex) { log.Error($"ZoneCraftGateStore: failed to load {StoreKey}; keeping the current matrix ({_rules.Count} rule(s)). {ex}"); }

                _initialized = true;
            }
        }

        /// <summary>Re-read the store from the shard. Same atomic build-then-commit as
        /// ZoneControlManager.Load - nothing live is disturbed until the parse has succeeded.</summary>
        public static void Reload()
        {
            lock (_lock)
            {
                Load();
                _initialized = true;
            }
        }

        private static void Load()
        {
            string json = null;
            if (DatabaseManager.ShardConfig.StringExists(StoreKey))
                json = DatabaseManager.ShardConfig.GetString(StoreKey)?.Value;

            var store = string.IsNullOrWhiteSpace(json)
                ? new Store()
                : (JsonConvert.DeserializeObject<Store>(json) ?? new Store());

            // ── build into locals; nothing live is disturbed if any of this throws ──
            var rules = new Dictionary<(int, CraftItemClass), CraftRuleMode>();
            if (store.Rules != null)
            {
                foreach (var r in store.Rules)
                {
                    if (r == null) continue;
                    if (!Enum.TryParse<CraftItemClass>(r.ItemType, true, out var cls)) continue;
                    if (!Enum.TryParse<CraftRuleMode>(r.Mode, true, out var mode)) continue;
                    if (mode == CraftRuleMode.Auto) continue;   // sparse: Auto is never stored
                    rules[(r.Material, cls)] = mode;
                }
            }
            var minTier = store.MinTier > 0 ? store.MinTier : LootGenerationFactory.ZoneLootSetMinTier;

            // Null = never authored, so track the built-in default. An authored EMPTY list is honoured as
            // an empty list - "block nothing" has to be expressible, or the toggle is the only off switch.
            var componentsAuthored = store.Components != null;
            var components = new HashSet<uint>();
            foreach (var c in store.Components ?? new List<uint>(DefaultBlockedComponents))
                if (c != 0) components.Add(c);

            // ── commit: from here on nothing can throw ──
            _rules = rules;
            _enabled = store.Enabled;
            _minTier = minTier;
            _blockComponents = store.BlockComponents;
            _components = components;
            _componentsAuthored = componentsAuthored;
        }

        private static void Save()
        {
            var list = _rules.Count == 0
                ? null
                : _rules.OrderBy(kv => kv.Key.Material).ThenBy(kv => (int)kv.Key.Class)
                        .Select(kv => new CraftRule
                        {
                            Material = kv.Key.Material,
                            ItemType = kv.Key.Class.ToString(),
                            Mode = kv.Value.ToString(),
                        }).ToList();

            var store = new Store
            {
                Enabled = _enabled,
                MinTier = _minTier,
                Rules = list,
                BlockComponents = _blockComponents,
                // null when untouched, so an install that never edited the list keeps following the
                // default pair rather than freezing today's copy of it into the shard.
                Components = _componentsAuthored ? _components.OrderBy(w => w).ToList() : null,
            };
            var jsonOut = JsonConvert.SerializeObject(store, SparseJson);

            if (DatabaseManager.ShardConfig.StringExists(StoreKey))
                DatabaseManager.ShardConfig.SaveString(new ConfigPropertiesString { Key = StoreKey, Value = jsonOut, Description = "T11+ crafting gate matrix (JSON)" });
            else
                DatabaseManager.ShardConfig.AddString(StoreKey, jsonOut, "T11+ crafting gate matrix (JSON)");
        }

        #endregion

        #region read

        /// <summary>Master switch. False = the whole gate is bypassed, layer 2 included.</summary>
        public static bool Enabled
        {
            get { EnsureInitialized(); return _enabled; }
        }

        /// <summary>The tier the gate starts applying at (default 11).</summary>
        public static int MinTier
        {
            get { EnsureInitialized(); return _minTier; }
        }

        /// <summary>The authored cell for (material, item class), or Auto when nothing is authored.</summary>
        public static CraftRuleMode GetMode(int material, CraftItemClass cls)
        {
            EnsureInitialized();
            return _rules.TryGetValue((material, cls), out var m) ? m : CraftRuleMode.Auto;
        }

        /// <summary>Every authored (non-Auto) cell, in a stable order.</summary>
        public static List<(int Material, CraftItemClass Class, CraftRuleMode Mode)> ListRules()
        {
            EnsureInitialized();
            return _rules.OrderBy(kv => kv.Key.Material).ThenBy(kv => (int)kv.Key.Class)
                         .Select(kv => (kv.Key.Material, kv.Key.Class, kv.Value)).ToList();
        }

        public static int RuleCount { get { EnsureInitialized(); return _rules.Count; } }

        /// <summary>LAYER 0's toggle. False leaves the blocked list intact but stops consulting it, so the
        /// owner can turn the block off for an evening without losing what was authored.</summary>
        public static bool BlockComponents
        {
            get { EnsureInitialized(); return _blockComponents; }
        }

        /// <summary>Is this SALVAGE weenie on the blocked list? Membership only - the caller applies the
        /// toggle, so `craft components` can still show the list while the block is off.</summary>
        public static bool IsBlockedComponent(uint wcid)
        {
            EnsureInitialized();
            return _components.Contains(wcid);
        }

        /// <summary>The blocked source WCIDs, ascending.</summary>
        public static List<uint> BlockedComponents()
        {
            EnsureInitialized();
            return _components.OrderBy(w => w).ToList();
        }

        /// <summary>True while the list is still the built-in default (nothing added or removed). Only
        /// used to say so in the verb output and on the wire - the decision never branches on it.</summary>
        public static bool ComponentsAreDefault { get { EnsureInitialized(); return !_componentsAuthored; } }

        #endregion

        #region write

        /// <summary>Author one cell. Auto REMOVES the row (the matrix stays sparse). Returns false when
        /// nothing changed, so a caller can say so instead of writing the shard for no reason.</summary>
        public static bool SetMode(int material, CraftItemClass cls, CraftRuleMode mode)
        {
            EnsureInitialized();
            lock (_lock)
            {
                var next = new Dictionary<(int, CraftItemClass), CraftRuleMode>(_rules);
                var had = next.TryGetValue((material, cls), out var cur);
                if (mode == CraftRuleMode.Auto)
                {
                    if (!had) return false;
                    next.Remove((material, cls));
                }
                else
                {
                    if (had && cur == mode) return false;
                    next[(material, cls)] = mode;
                }
                _rules = next;
                Save();
                return true;
            }
        }

        public static void SetEnabled(bool on)
        {
            EnsureInitialized();
            lock (_lock) { _enabled = on; Save(); }
        }

        public static void SetMinTier(int tier)
        {
            EnsureInitialized();
            if (tier < 1)
            {
                log.Warn($"ZoneCraftGateStore.SetMinTier({tier}) ignored: the tier must be 1 or more (Load rejects less).");
                return;
            }
            lock (_lock) { _minTier = tier; Save(); }
        }

        public static void SetBlockComponents(bool on)
        {
            EnsureInitialized();
            lock (_lock) { _blockComponents = on; Save(); }
        }

        /// <summary>Add one source WCID to the blocked list. Any edit marks the list AUTHORED, so it stops
        /// tracking the built-in default from that moment on - `components reset` is the way back.
        /// Returns false when nothing changed, so the caller can say so instead of writing the shard.</summary>
        public static bool AddComponent(uint wcid)
        {
            EnsureInitialized();
            lock (_lock)
            {
                if (_components.Contains(wcid))
                    return false;
                var next = new HashSet<uint>(_components) { wcid };
                _components = next;
                _componentsAuthored = true;
                Save();
                return true;
            }
        }

        public static bool RemoveComponent(uint wcid)
        {
            EnsureInitialized();
            lock (_lock)
            {
                if (!_components.Contains(wcid))
                    return false;
                var next = new HashSet<uint>(_components);
                next.Remove(wcid);
                _components = next;
                _componentsAuthored = true;
                Save();
                return true;
            }
        }

        /// <summary>The catalog WCIDs filed under <paramref name="group"/> (case-insensitive). Empty when no
        /// such group exists.</summary>
        public static List<uint> CatalogGroup(string group)
            => ComponentCatalog.Where(e => string.Equals(e.Group, group, StringComparison.OrdinalIgnoreCase))
                               .Select(e => e.Wcid).ToList();

        /// <summary>True when <paramref name="wcid"/> is in the catalog (the plugin labels those itself).</summary>
        public static bool InCatalog(uint wcid) => CatalogWcids.Contains(wcid);

        private static readonly HashSet<uint> CatalogWcids = new HashSet<uint>(ComponentCatalog.Select(e => e.Wcid));

        /// <summary>Add or remove many WCIDs with ONE shard write (a catalog group can be ~100 rows, and
        /// AddComponent saves per call). Returns how many actually changed; 0 writes nothing.</summary>
        public static int SetComponents(IEnumerable<uint> wcids, bool blocked)
        {
            EnsureInitialized();
            lock (_lock)
            {
                var next = new HashSet<uint>(_components);
                var changed = 0;
                foreach (var w in wcids)
                    if (blocked ? next.Add(w) : next.Remove(w))
                        changed++;
                if (changed == 0)
                    return 0;
                _components = next;
                _componentsAuthored = true;
                Save();
                return changed;
            }
        }

        /// <summary>Forget the authored list and follow <see cref="DefaultBlockedComponents"/> again.</summary>
        public static void ResetComponents()
        {
            EnsureInitialized();
            lock (_lock)
            {
                _components = new HashSet<uint>(DefaultBlockedComponents);
                _componentsAuthored = false;
                Save();
            }
        }

        #endregion

        #region item classification

        /// <summary>Which matrix column this target sits in, or null when the item is not something the
        /// matrix has a column for (a container, a component, a gem) - such a target skips layer 1 and
        /// falls through to the downgrade rule exactly as before.
        ///
        /// ORDER MATTERS. Cloak is tested before Jewelry and before the armour-level test because a
        /// cloak carries an ArmorLevel and EquipMask.Jewelry includes EquipMask.Cloak; shields are
        /// tested before armour for the same reason. This mirrors GetZoneLootDisplayOrder
        /// (LootGenerationFactory_ZoneSet.cs), which is the classification the loot pipeline already
        /// uses, so a piece lands in the same bucket on both sides.</summary>
        public static CraftItemClass? Classify(WorldObject wo)
        {
            if (wo == null)
                return null;

            if (wo is MeleeWeapon || wo is MissileLauncher || wo is Missile || wo is Caster)
                return CraftItemClass.Weapon;

            if (wo.IsShield)
                return CraftItemClass.Shield;

            if (ACE.Server.Entity.Cloak.IsCloak(wo))
                return CraftItemClass.Cloak;

            if (wo.ItemType == ItemType.Jewelry)
                return CraftItemClass.Jewelry;

            if ((wo.ArmorLevel ?? 0) > 0 || wo is Clothing)
                return CraftItemClass.Armor;

            return null;
        }

        #endregion

        #region material naming + catalog

        /// <summary>Parse a material given by NAME (BlackOpal, "black opal", black_opal) or by numeric
        /// MaterialType id. Name is the owner-facing form; the id is what the store and the wire carry.</summary>
        public static bool TryParseMaterial(string s, out int material)
        {
            material = 0;
            if (string.IsNullOrWhiteSpace(s))
                return false;

            var t = s.Trim();
            if (int.TryParse(t, out var num) && num > 0 && Enum.IsDefined(typeof(MaterialType), (uint)num))
            {
                material = num;
                return true;
            }

            var squashed = t.Replace(" ", "").Replace("_", "").Replace("-", "");
            if (Enum.TryParse<MaterialType>(squashed, true, out var mt) && mt != MaterialType.Unknown)
            {
                material = (int)mt;
                return true;
            }
            return false;
        }

        public static string MaterialName(int material)
        {
            var name = Enum.GetName(typeof(MaterialType), (uint)material);
            return string.IsNullOrEmpty(name) ? "Material " + material : name;
        }

        /// <summary>One row of the material catalog the plugin renders as matrix rows: the salvage
        /// material, its name, and (when it is a stock salvage whose recipe carries a mapped mutation
        /// DataId) the imbue it applies.</summary>
        public readonly struct ImbueMaterial
        {
            public readonly int Material;
            public readonly string Name;
            public readonly ImbuedEffectType Effect;

            public ImbueMaterial(int material, string name, ImbuedEffectType effect)
            {
                Material = material; Name = name; Effect = effect;
            }
        }

        private static volatile List<ImbueMaterial> _catalog;

        /// <summary>The materials that carry an imbue (salvage_Type 2) recipe - 33 of the 78 MaterialType
        /// values as of 2026-08-24. Read once from the world DB and cached: this is static content, and
        /// the only callers are the admin verbs and the wire payload.
        ///
        /// Query equivalent:
        ///   SELECT DISTINCT wi.value FROM cook_book cb JOIN recipe r ON r.id=cb.recipe_Id
        ///   JOIN weenie_properties_int wi ON wi.object_Id=cb.source_W_C_I_D AND wi.type=131
        ///   WHERE r.salvage_Type=2
        ///
        /// A DB failure returns an EMPTY list rather than throwing - the matrix does not depend on this,
        /// it is a naming convenience for the UI, and a craft decision must never fail on it.</summary>
        public static List<ImbueMaterial> ImbueMaterials()
        {
            var cached = _catalog;
            if (cached != null)
                return cached;

            var rows = new List<ImbueMaterial>();
            try
            {
                using var ctx = new WorldDbContext();

                // material id -> the mutation DataIds of every salvage_Type 2 recipe that salvage feeds
                var pairs = ctx.CookBook
                    .Join(ctx.Recipe.Where(r => r.SalvageType == 2), cb => cb.RecipeId, r => r.Id,
                          (cb, r) => new { cb.SourceWCID, r.Id })
                    .Join(ctx.WeeniePropertiesInt.Where(wi => wi.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.MaterialType),
                          x => x.SourceWCID, wi => wi.ObjectId, (x, wi) => new { Material = wi.Value, RecipeId = x.Id })
                    .Distinct().AsNoTracking().ToList();

                var recipeIds = pairs.Select(p => p.RecipeId).Distinct().ToList();
                var dataIds = ctx.RecipeMod.Where(m => recipeIds.Contains(m.RecipeId) && m.DataId != 0)
                    .Select(m => new { m.RecipeId, m.DataId }).AsNoTracking().ToList()
                    .GroupBy(m => m.RecipeId).ToDictionary(g => g.Key, g => g.Select(m => (uint)m.DataId).ToList());

                foreach (var g in pairs.GroupBy(p => p.Material).OrderBy(g => g.Key))
                {
                    var effect = ImbuedEffectType.Undef;
                    foreach (var rid in g.Select(p => p.RecipeId))
                    {
                        if (!dataIds.TryGetValue(rid, out var dids)) continue;
                        foreach (var did in dids)
                            if (ZoneCraftGate.TryGetImbueForDataId(did, out var e)) { effect = e; break; }
                        if (effect != ImbuedEffectType.Undef) break;
                    }
                    rows.Add(new ImbueMaterial(g.Key, MaterialName(g.Key), effect));
                }
                _catalog = rows;   // cache only a successful read; a transient DB failure must not pin an empty catalog
            }
            catch (Exception ex)
            {
                log.Warn($"ZoneCraftGateStore: could not read the imbue material catalog; the matrix is unaffected. {ex.Message}");
                rows.Clear();
            }

            return rows;
        }

        #endregion
    }
}
