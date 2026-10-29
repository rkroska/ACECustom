using System;
using System.Globalization;

using log4net;

using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>The five item lines a Paragon Collector buys.</summary>
    public enum ParagonTrack
    {
        None = 0,
        Armor,
        Weapon,
        Clothing,
        Jewelry,
        Trinket,
    }

    /// <summary>What a finished item of one line looks like, and what the player must have done to sell it.</summary>
    public sealed class ParagonTrackRule
    {
        public ParagonTrack Track { get; init; }

        /// <summary>The line as a group, for refusals: "Paragon armor".</summary>
        public string Plural { get; init; }

        /// <summary>One item of the line, for thanks: "Paragon armor piece".</summary>
        public string Singular { get; init; }

        /// <summary>PropertyInt.ItemMaxLevel a finished item has (the top tier of the line).</summary>
        public int RequiredLevel { get; init; }

        /// <summary>Quest stamp the character must hold (at least one solve).</summary>
        public string Stamp { get; init; }

        /// <summary>Refusal when the stamp is missing.</summary>
        public string MissingStamp { get; init; }

        /// <summary>
        /// A tool whose cook_book targets ARE the line's items: every WCID the line's own gems or essences can be applied to.
        /// The collector only takes an item whose WCID has a cook_book row with this source. 0 = weapons, which the amber
        /// recipes accept by weapon type in code (RecipeManager_New) rather than by cook_book row.
        /// </summary>
        public uint TrackToolWcid { get; init; }

        /// <summary>Start of PropertyString.Use that the line's recipes write on every tier. Null = weapons (no Use text).</summary>
        public string UsePrefix { get; init; }
    }

    /// <summary>
    /// Paragon Collector NPC (PropertyBool.ParagonCollector, 2026-10-29). A player hands it a FINISHED item of one of five lines -
    /// Paragon armor, Paragon weapons, Illuminated clothing, Illuminated jewelry, Quiddity trinkets - and is offered luminance for
    /// it in a Yes/No popup. On Yes the item is destroyed and the luminance is banked as-is (GrantLuminance: no quest,
    /// enlightenment or enchantment multipliers). The item never leaves the player until they answer Yes.
    ///
    /// Everything that defines "finished" comes from the world DB scan of 2026-10-29 (ace_world, ILT copy):
    ///   Armor     ItemMaxLevel 50, Use "Enhanced with the powers of the Paragon...", a target of the Paragon Armor Corrupted
    ///             Extractor 64454071 (the same 259 WCIDs as every Gem of the Nth Tier Paragon for Armor; shields included).
    ///             Stamp ParagonArmorCompleted (Winternadir, at the 50th tier).
    ///   Weapon    ItemMaxLevel 50, EquipmentSetId >= 138 (ParagonMissile 138 / ParagonCaster 139 / ParagonMelee 140, set by
    ///             the 1st Tier amber; the Paragon Weapon Corrupted Siphon uses the same >= 138 test). Stamp ParagonEnlCompleted
    ///             (Summercrown, at the 50th tier).
    ///   Clothing  ItemMaxLevel 20, Use "Illuminated from the craftsmanship of the Tumerok...", a target of Corrupted Sheers
    ///             98760141 (the same 32 WCIDs as the Illuminated Clothing Gems). Stamp IlluminatedClothingCompleted.
    ///   Jewelry   ItemMaxLevel 15, same Use text, a target of the Corrupted Jewelers Graver 98760145 (the same 9 WCIDs as the
    ///             Illuminated Jewelry Gems). Stamp IlluminatedJewelryCompleted.
    ///   Trinket   ItemMaxLevel 6, Use "Quiddity Essence strength is level...", a target of Pure Quiddity Essence 98760169.
    ///             There is NO completion stamp for trinkets in the DB; AlignMyQuiddity (Zerivax, at the start of the line) is
    ///             the only trinket stamp, so it is the one required.
    /// </summary>
    public static class ParagonCollectorService
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>How long the sale popup stays open.</summary>
        public const double PopupSeconds = 30;

        /// <summary>Lowest EquipmentSetId a Paragon weapon carries (ParagonMissile).</summary>
        public const int ParagonWeaponSetFloor = 138;

        public const string IlluminatedUsePrefix = "Illuminated from the craftsmanship of the Tumerok";

        /// <summary>Use text a trinket gets at level 1 (Pure Quiddity Essence); every later level writes the Trinket UsePrefix.</summary>
        public const string TrinketAlignedUse = "Your Quiddity is now aligned.";

        public static readonly ParagonTrackRule Armor = new()
        {
            Track = ParagonTrack.Armor,
            Plural = "Paragon armor",
            Singular = "Paragon armor piece",
            RequiredLevel = 50,
            Stamp = "ParagonArmorCompleted",
            MissingStamp = "You have not taken a piece of armor through all 50 tiers of the Paragon yourself.",
            TrackToolWcid = 64454071,   // Paragon Armor Corrupted Extractor
            UsePrefix = "Enhanced with the powers of the Paragon",
        };

        public static readonly ParagonTrackRule Weapon = new()
        {
            Track = ParagonTrack.Weapon,
            Plural = "Paragon weapons",
            Singular = "Paragon weapon",
            RequiredLevel = 50,
            Stamp = "ParagonEnlCompleted",
            MissingStamp = "You have not taken a weapon through all 50 tiers of the Paragon yourself.",
            TrackToolWcid = 0,
            UsePrefix = null,
        };

        public static readonly ParagonTrackRule Clothing = new()
        {
            Track = ParagonTrack.Clothing,
            Plural = "Illuminated clothing",
            Singular = "piece of Illuminated clothing",
            RequiredLevel = 20,
            Stamp = "IlluminatedClothingCompleted",
            MissingStamp = "You have not finished illuminating clothing with the Tumerok yourself.",
            TrackToolWcid = 98760141,   // Corrupted Sheers
            UsePrefix = IlluminatedUsePrefix,
        };

        public static readonly ParagonTrackRule Jewelry = new()
        {
            Track = ParagonTrack.Jewelry,
            Plural = "Illuminated jewelry",
            Singular = "piece of Illuminated jewelry",
            RequiredLevel = 15,
            Stamp = "IlluminatedJewelryCompleted",
            MissingStamp = "You have not finished illuminating jewelry with the Tumerok yourself.",
            TrackToolWcid = 98760145,   // Corrupted Jewelers Graver
            UsePrefix = IlluminatedUsePrefix,
        };

        public static readonly ParagonTrackRule Trinket = new()
        {
            Track = ParagonTrack.Trinket,
            Plural = "Quiddity trinkets",
            Singular = "Quiddity trinket",
            RequiredLevel = 6,
            Stamp = "AlignMyQuiddity",
            MissingStamp = "You have not had your Quiddity aligned. Zerivax can tell you more.",
            TrackToolWcid = 98760169,   // Pure Quiddity Essence
            UsePrefix = "Quiddity Essence strength is level",
        };

        /// <summary>The rule for a line, or null for <see cref="ParagonTrack.None"/>.</summary>
        public static ParagonTrackRule RuleFor(ParagonTrack track) => track switch
        {
            ParagonTrack.Armor => Armor,
            ParagonTrack.Weapon => Weapon,
            ParagonTrack.Clothing => Clothing,
            ParagonTrack.Jewelry => Jewelry,
            ParagonTrack.Trinket => Trinket,
            _ => null,
        };

        /// <summary>
        /// Luminance paid for one finished item of the line, read live from ServerConfig (paragon_collector_lum_*). 0 or less
        /// means the collector refuses that line; <see cref="ParagonTrack.None"/> is always 0.
        /// </summary>
        public static long RewardFor(ParagonTrack track) => track switch
        {
            ParagonTrack.Armor => ServerConfig.paragon_collector_lum_armor.Value,
            ParagonTrack.Weapon => ServerConfig.paragon_collector_lum_weapon.Value,
            ParagonTrack.Clothing => ServerConfig.paragon_collector_lum_clothing.Value,
            ParagonTrack.Jewelry => ServerConfig.paragon_collector_lum_jewelry.Value,
            ParagonTrack.Trinket => ServerConfig.paragon_collector_lum_trinket.Value,
            _ => 0,
        };

        // ---------------------------------------------------------------- classification (pure)

        /// <summary>
        /// True for a melee weapon, missile launcher or caster - by weenie type, or by item type for the few Generic-typed
        /// missile weapons. Shields are armor, not weapons.
        /// </summary>
        public static bool IsWeapon(WeenieType weenieType, ItemType itemType)
        {
            if (weenieType == WeenieType.MeleeWeapon || weenieType == WeenieType.MissileLauncher || weenieType == WeenieType.Caster)
                return true;
            return (itemType & (ItemType.MeleeWeapon | ItemType.MissileWeapon | ItemType.Caster)) != 0;
        }

        /// <summary>
        /// Which line an item is on, or None. Says nothing about whether it is FINISHED (that is the level check).
        /// <paramref name="isTrackTarget"/> answers "does this item's WCID have a cook_book row with the line's tool".
        /// Pure: no database, no world.
        /// </summary>
        public static ParagonTrack Classify(WeenieType weenieType, ItemType itemType, int? equipmentSetId, string use, Func<ParagonTrackRule, bool> isTrackTarget)
        {
            if (IsWeapon(weenieType, itemType))
                return (equipmentSetId ?? 0) >= ParagonWeaponSetFloor ? ParagonTrack.Weapon : ParagonTrack.None;

            if (string.IsNullOrEmpty(use))
                return ParagonTrack.None;

            if (use.StartsWith(Armor.UsePrefix, StringComparison.Ordinal))
                return isTrackTarget(Armor) ? ParagonTrack.Armor : ParagonTrack.None;

            if (use.StartsWith(IlluminatedUsePrefix, StringComparison.Ordinal))
            {
                // jewelry and clothing share the Tumerok text; their target lists do not overlap
                if (isTrackTarget(Jewelry))
                    return ParagonTrack.Jewelry;
                if (isTrackTarget(Clothing))
                    return ParagonTrack.Clothing;
                return ParagonTrack.None;
            }

            if (use.StartsWith(Trinket.UsePrefix, StringComparison.Ordinal) || use.StartsWith(TrinketAlignedUse, StringComparison.Ordinal))
                return isTrackTarget(Trinket) ? ParagonTrack.Trinket : ParagonTrack.None;

            return ParagonTrack.None;
        }

        /// <summary>True when the item has reached the top tier of its line.</summary>
        public static bool IsFinished(ParagonTrackRule rule, int? itemMaxLevel) => rule != null && (itemMaxLevel ?? 0) >= rule.RequiredLevel;

        /// <summary>
        /// True when the line's tool (<see cref="ParagonTrackRule.TrackToolWcid"/>) has a cook_book row for this WCID, i.e. the
        /// line's own gems or essences can be applied to it. Misses are cached by the world database too. Always false for
        /// weapons, which have no tool.
        /// </summary>
        private static bool IsTrackTarget(ParagonTrackRule rule, uint wcid)
        {
            if (rule.TrackToolWcid == 0)
                return false;
            return DatabaseManager.World.GetCachedCookbook(rule.TrackToolWcid, wcid) != null;
        }

        /// <summary>Which line a world object is on, read from its own properties and the world DB cook_book.</summary>
        public static ParagonTrack Classify(WorldObject item)
        {
            return Classify(item.WeenieType, item.ItemType, item.GetProperty(PropertyInt.EquipmentSetId), item.GetProperty(PropertyString.Use),
                rule => IsTrackTarget(rule, item.WeenieClassId));
        }

        // ---------------------------------------------------------------- the collector

        /// <summary>Null when the collector would buy this item from this player right now, else an ASCII refusal.</summary>
        public static string CheckSale(Player player, WorldObject item, bool itemWasEquipped, out ParagonTrackRule rule, out long reward)
        {
            rule = RuleFor(Classify(item));
            reward = 0;

            if (rule == null)
                return "I collect only finished Paragon armor and weapons, and finished Illuminated clothing, Illuminated jewelry and Quiddity trinkets.";

            reward = RewardFor(rule.Track);
            if (reward <= 0)
                return $"I am not collecting {rule.Plural} right now.";

            if (player.QuestManager.GetCurrentSolves(rule.Stamp) < 1)
                return $"{rule.MissingStamp} Until you have, I will not take {rule.Plural} from you.";

            var level = item.ItemMaxLevel ?? 0;
            if (!IsFinished(rule, level))
                return $"Your {item.Name} has reached level {level}. I take {rule.Plural} only at level {rule.RequiredLevel}.";

            if (itemWasEquipped)
                return $"Take off your {item.Name} and hand it to me from your pack.";

            return null;
        }

        /// <summary>An item was handed to a Paragon Collector. It stays with the player unless they answer Yes.</summary>
        public static void HandleGive(Player player, WorldObject collector, WorldObject item, bool itemWasEquipped)
        {
            if (!ServerConfig.paragon_collector_enabled.Value)
            {
                Say(player, collector, "I am not collecting anything today. Come back another time.");
                return;
            }

            var refusal = CheckSale(player, item, itemWasEquipped, out _, out var reward);
            if (refusal != null)
            {
                Say(player, collector, refusal);
                return;
            }

            var itemGuid = item.Guid.Full;
            var itemName = item.Name;
            var amount = reward.ToString("N0", CultureInfo.InvariantCulture);
            var question = $"Give your {itemName} to {collector.Name} for {amount} luminance?\n\nThe {itemName} will be destroyed. This cannot be undone.";

            var sent = player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, (yes, timedOut) =>
            {
                if (!yes || timedOut)
                {
                    Say(player, collector, $"Keep it, then. Your {itemName} is still yours.");
                    return;
                }

                if (!ServerConfig.paragon_collector_enabled.Value)
                {
                    Say(player, collector, "I am not collecting anything today. Nothing was taken.");
                    return;
                }

                // Everything may have changed while the popup was open: find the item again, in the pack only, and re-check.
                var again = player.FindObject(itemGuid, Player.SearchLocations.MyInventory);
                if (again == null)
                {
                    Say(player, collector, $"Your {itemName} is no longer in your pack. Nothing was taken.");
                    return;
                }
                if (player.IsTrading && again.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
                {
                    Say(player, collector, $"Your {itemName} is in a trade window. Nothing was taken.");
                    return;
                }
                var stillRefused = CheckSale(player, again, false, out var ruleNow, out _);
                if (stillRefused != null)
                {
                    Say(player, collector, $"Nothing was taken. {stillRefused}");
                    return;
                }

                // pays the amount the popup showed
                var wcid = again.WeenieClassId;
                if (!player.TryConsumeFromInventoryWithNetworking(again))
                {
                    log.Error($"[ParagonCollector] {player.Name}: could not remove {itemName} (0x{itemGuid:X8}); no luminance granted");
                    Say(player, collector, $"Something went wrong. Your {itemName} was not taken.");
                    return;
                }

                player.GrantLuminance(reward, XpType.Quest, ShareType.None);
                player.SaveBiotaToDatabase();

                log.Info($"[ParagonCollector] {player.Name} sold {ruleNow.Track} {itemName} (0x{itemGuid:X8}, wcid {wcid}) to {collector.Name} for {reward} luminance");
                Say(player, collector, $"A fine {ruleNow.Singular}. {amount} luminance has been added to your bank.");
            }), question, PopupSeconds);

            if (!sent)
                Say(player, collector, "Answer the popup you already have open first.");
        }

        /// <summary>The collector tells the player <paramref name="text"/> (plain ASCII).</summary>
        private static void Say(Player player, WorldObject collector, string text)
        {
            player.Session.Network.EnqueueSend(new GameEventTell(collector, text, player, ChatMessageType.Tell));
        }
    }
}
