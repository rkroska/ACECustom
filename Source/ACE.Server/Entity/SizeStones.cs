using System;

using log4net;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Enlarging and Shrinking Stones (2026-10-10): two consumables that step how big something held in the hands is
    /// drawn - a weapon, shield, wand or orb - a tenth at a time, between 70% and 140% of its normal size. Built like
    /// the Solidifying and Fading Tinctures (Player_Use.cs).
    ///
    /// The stone only writes a level on the item (PropertyFloat.ItemSizeLevel). The item's own DefaultScale is never
    /// touched: the level is multiplied in where the size is sent to clients (WorldObject.ShownObjScale), so tailoring,
    /// which copies DefaultScale, can never carry a resize onto another item and stack it past the limits.
    ///
    /// Every string here reaches the game client, so all of it is plain ASCII (CLAUDE.md).
    /// </summary>
    public static class SizeStones
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const string Tag = "[SizeStone]";

        /// <summary>Sold by Ivo (docs/WCID_ALLOCATION_7878.md).</summary>
        public const uint EnlargingStoneWcid = 78780265;
        public const uint ShrinkingStoneWcid = 78780266;

        public const double Step = 0.1;
        public const double MinLevel = 0.7;
        public const double MaxLevel = 1.4;

        public static bool IsSizeStone(WorldObject item)
            => item != null && (item.WeenieClassId == EnlargingStoneWcid || item.WeenieClassId == ShrinkingStoneWcid);

        /// <summary>
        /// The level one more stone would give, or false when <paramref name="current"/> is already at that end. Levels
        /// are counted in whole tenths, so ten steps up and ten steps down always land back on exactly 1.0.
        /// </summary>
        public static bool TryStep(double current, bool enlarge, out double next)
        {
            var tenths = Tenths(current) + (enlarge ? 1 : -1);
            next = tenths / 10.0;
            return tenths >= Tenths(MinLevel) && tenths <= Tenths(MaxLevel);
        }

        private static int Tenths(double level) => (int)Math.Round(Clamp(level) * 10.0, MidpointRounding.AwayFromZero);

        private static double Clamp(double level) => double.IsNaN(level) ? 1.0 : Math.Clamp(level, MinLevel, MaxLevel);

        /// <summary>The item's size level: 1.0 when no stone has been used on it. Always inside the limits.</summary>
        public static double LevelOf(WorldObject item) => Tenths(item.GetProperty(PropertyFloat.ItemSizeLevel) ?? 1.0) / 10.0;

        /// <summary>
        /// The scale to send to clients for an item whose own scale is <paramref name="ownScale"/> and whose stored size
        /// level is <paramref name="level"/>. With no level (or a level of 1.0) it is the item's own scale, unchanged.
        /// A stored level outside the limits is drawn at the nearest limit.
        /// </summary>
        public static float? ShownScale(float? ownScale, double? level)
        {
            if (!level.HasValue)
                return ownScale;

            var tenths = Tenths(level.Value);
            if (tenths == 10)
                return ownScale;

            // no scale, or one too small to be sent, is drawn by the client at 1.0
            var own = ownScale.HasValue && Math.Abs(ownScale.Value) >= 0.001f ? ownScale.Value : 1.0f;
            return own * (tenths / 10.0f);
        }

        /// <summary>Something held in the hands - a weapon, shield, wand or orb - and not a stack.</summary>
        public static bool CanResize(WorldObject target)
            => target != null
                && ((target.ValidLocations ?? EquipMask.None) & EquipMask.Selectable) != 0
                && (target.MaxStackSize ?? 1) <= 1;

        /// <summary>"Size: 130% of normal." for the ID panel, or null for an item at its normal size.</summary>
        public static string BuildAppraisalLine(WorldObject item)
        {
            if (item == null || !item.GetProperty(PropertyFloat.ItemSizeLevel).HasValue)
                return null;

            var level = LevelOf(item);
            return Tenths(level) == 10 ? null : $"Size: {Percent(level)} of normal.";
        }

        private static string Percent(double level) => $"{Tenths(level) * 10}%";

        /// <summary>A player used a size stone on <paramref name="target"/> (Player.HandleActionUseWithTarget).</summary>
        public static void HandleUse(Player player, WorldObject stone, WorldObject target)
        {
            var enlarge = stone.WeenieClassId == EnlargingStoneWcid;

            if (player.FindObject(target.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) == null)
            {
                player.SendTransientError($"The {target.Name} must be in your pack or in your hands.");
                return;
            }

            if (((target.ValidLocations ?? EquipMask.None) & EquipMask.Selectable) == 0)
            {
                player.SendTransientError($"The {stone.Name} only works on something you hold in your hands: a weapon, shield, wand or orb.");
                return;
            }

            if (!CanResize(target))
            {
                player.SendTransientError($"The {stone.Name} cannot be used on a stack.");
                return;
            }

            var current = LevelOf(target);
            if (!TryStep(current, enlarge, out var next))
            {
                player.SendTransientError(enlarge
                    ? $"The {target.Name} is already as large as it can be made ({Percent(MaxLevel)}). The stone was not used."
                    : $"The {target.Name} is already as small as it can be made ({Percent(MinLevel)}). The stone was not used.");
                return;
            }

            if (!player.TryConsumeFromInventoryWithNetworking(stone, 1))
            {
                player.SendTransientError($"Failed to consume the {stone.Name}.");
                return;
            }

            if (Tenths(next) == 10)
                target.RemoveProperty(PropertyFloat.ItemSizeLevel);
            else
                target.SetProperty(PropertyFloat.ItemSizeLevel, next);
            target.ChangesDetected = true;
            target.SaveBiotaToDatabase();

            log.Info($"{Tag} {player.Name} (0x{player.Guid.Full:X8}) used {stone.Name} on {target.Name} (0x{target.Guid.Full:X8}, wcid {target.WeenieClassId}): size level {current:0.0} -> {next:0.0}");

            // the client only learns an item's size from a full object description
            RecipeManager.UpdateObj(player, target);

            player.PlayParticleEffect(enlarge ? PlayScript.EnchantUpPurple : PlayScript.EnchantDownPurple, player.Guid);
            player.SendMessage(Tenths(next) == 10
                ? $"The {target.Name} is back to its normal size. Wield it again if you do not see the change."
                : $"The {target.Name} is now {Percent(next)} of its normal size. Wield it again if you do not see the change.");
        }
    }
}
