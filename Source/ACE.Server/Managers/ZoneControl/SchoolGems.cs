using System;
using System.Collections.Generic;
using System.Linq;

using log4net;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// Attack school augmentations from kills (owner 2026-11-01 as gems; one stat since 2026-11-22; floor 2,000, catch-up, no
    /// weekly stop, no drip gate and straight onto the count since 2026-11-24). War / Void / Melee / Missile, T11-T25:
    ///   - a kill at the player's own tier that pays XP or lum, credited to the top damager (a pet's kill counts for its owner):
    ///     TriuneEarning.Credit calls GrantOnKill on the player's thread, which rolls each school once and adds +1 straight onto
    ///     the school's LumAug count, with a chat line - the way the Creature / Item / Life rewards grow. Since 2026-11-24 there is
    ///     no gem to loot or use (until then a gem dropped in the top damager's corpse; a player who skipped looting got none);
    ///   - an independent roll per school while it is not yet at this tier's ceiling (T11-T14 the Item / Life ladder's end:
    ///     2,500 ... 4,000; T15+ base + the summed Points of T15..tier: 4,500 at T15 ... 9,500 at T25), from 2,000 at every tier
    ///     (GemFloor): the catch-up (2026-11-24) - a school below the luminance cap rolls too, so a player can build a second
    ///     school while hunting with their first. Until then a school needed the tier's floor (T15+: the 4,000 luminance cap)
    ///     first. Each school has its own chance (GrantChance / TriuneEarning.SchoolGemChance): while it catches up (more than
    ///     500 below the tier's ceiling) the full first-band chance all week; within the last 500 the weekly kill bands of the
    ///     hunting rewards, on the tier's own gem weeks (4 at the floor pace, 2.5 at the ceiling pace) - slower past the first
    ///     band, but never stopped for the week (the Creature / Item / Life rewards still stop past the second band).
    /// Gems earned before 2026-11-24 (Gem of War / Void / Melee / Missile Augmentation) keep working:
    ///   - each is stamped with its earner (AllowedActivator + "Earned by ...") and its tier (PropertyInt.EarnedGemTier);
    ///   - used by anyone else: consumed, nothing added, a line about who really earned it;
    ///   - used by the earner: +1 while the school is under the gem's OWN tier's ceiling (T15 gems keep working in T16 up to
    ///     4,500); a gem that can never be used again crumbles; under the floor (2,000) = refused and kept;
    ///   - picked up off a corpse by the earner when the school + the carried earned gems of that school at that tier or lower
    ///     already fill that tier's ceiling: it crumbles at once.
    /// Off unless /zonecontrol triune gems on - the one switch for every T11+ tier (until 2026-11-24 T11-T14 also needed the
    /// drip). Pace settings live in TriuneEarning's store.
    /// </summary>
    public static class SchoolGems
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public sealed class School
        {
            /// <summary>The school as players read it: "War" -> "Your War augmentations grow to 4,501."</summary>
            public string Name;
            public uint GemWcid;
            /// <summary>The gem's name since 2026-11-22 (world SQL renames the weenie; held EARNED copies are renamed at login).</summary>
            public string GemName;
            /// <summary>The ONE count (owner 2026-11-22): luminance bought, kills and gems all add here.</summary>
            public PropertyInt64 AugCount;
        }

        public static readonly School[] Schools =
        {
            new School { Name = "War",     GemWcid = 777700052, GemName = "Gem of War Augmentation",     AugCount = PropertyInt64.LumAugWarCount },
            new School { Name = "Void",    GemWcid = 777700057, GemName = "Gem of Void Augmentation",    AugCount = PropertyInt64.LumAugVoidCount },
            new School { Name = "Melee",   GemWcid = 777700062, GemName = "Gem of Melee Augmentation",   AugCount = PropertyInt64.LumAugMeleeCount },
            new School { Name = "Missile", GemWcid = 777700067, GemName = "Gem of Missile Augmentation", AugCount = PropertyInt64.LumAugMissileCount },
        };

        public static School ForGem(uint wcid) => Schools.FirstOrDefault(s => s.GemWcid == wcid);

        /// <summary>A school's count (the one stat).</summary>
        public static long Total(Player p, School s) => p.GetProperty(s.AugCount) ?? 0;

        /// <summary>The school's count as power and the gates read it: the stored count plus a retired charm still held (not
        /// moved yet; 0 once moved). Floors and ceilings test this; a gem still writes Total + 1.</summary>
        public static long Power(Player p, School s) => s.AugCount switch
        {
            PropertyInt64.LumAugWarCount => p.EffectiveWarAugCount,
            PropertyInt64.LumAugVoidCount => p.EffectiveVoidAugCount,
            PropertyInt64.LumAugMeleeCount => p.EffectiveMeleeAugCount,
            PropertyInt64.LumAugMissileCount => p.EffectiveMissileAugCount,
            _ => Total(p, s),
        };

        /// <summary>Do the attack schools roll on kills at <paramref name="tier"/>? Every T11+ tier while the gems switch is on.
        /// Until 2026-11-24 T11-T14 also needed the drip; the drip now switches only the Creature / Item / Life rewards there.</summary>
        public static bool GemsDropAt(TriuneEarning.Config cfg, int tier) => cfg.GemsEnabled && tier >= TriuneEarning.DripMinTier;

        /// <summary>The earned stamp of a gem: (earner guid, tier), or null for a gem that was never earned (an old vendor gem).</summary>
        public static (uint Earner, int Tier)? Stamp(WorldObject gem)
        {
            var tier = gem?.GetProperty(PropertyInt.EarnedGemTier) ?? 0;
            var earner = gem?.AllowedActivator ?? 0;
            return tier > 0 && earner != 0 ? (earner, tier) : null;
        }

        // ---- pure rules (tested) ----------------------------------------------------------------------------------------

        /// <summary>Does a school roll a gem at this tier? At the tier's floor, still under its ceiling.</summary>
        public static bool Qualifies(long count, long floor, long ceiling) => count >= floor && count < ceiling;

        /// <summary>True when the attack schools roll here and at least one of this player's can still grow at this tier.</summary>
        public static bool CanEarnAny(Player player, TriuneEarning.Config cfg, int tier)
        {
            if (!GemsDropAt(cfg, tier))
                return false;
            var ceiling = TriuneEarning.GemCeiling(cfg, tier);
            var floor = TriuneEarning.GemFloor(cfg, tier);
            return Schools.Any(s => Qualifies(Power(player, s), floor, ceiling));   // the same count the drop and the use test
        }

        /// <summary>True when the attack schools roll here and at least one of this player's that can still grow is within 500
        /// of the tier's ceiling, where the weekly bands slow it down (a school catching up keeps its full chance all week).</summary>
        public static bool AnySlowsDown(Player player, TriuneEarning.Config cfg, int tier)
        {
            if (!GemsDropAt(cfg, tier))
                return false;
            var ceiling = TriuneEarning.GemCeiling(cfg, tier);
            var floor = TriuneEarning.GemFloor(cfg, tier);
            return Schools.Any(s =>
            {
                var count = Power(player, s);
                return Qualifies(count, floor, ceiling) && TriuneEarning.GemSlowsDown(count, ceiling);
            });
        }

        /// <summary>
        /// Room a newly picked-up gem would have: its tier's ceiling minus the highest total the earned gems already carried at
        /// that tier or lower can still reach. Used lowest tier first (the best order), each tier's gems add +1 only up to that
        /// tier's own ceiling - so a spent lower-tier gem, or the part of a pile past its ceiling, takes no room.
        /// </summary>
        /// <param name="carriedByTier">Earned gems carried at the picked-up gem's tier or lower: (tier, its ceiling, how many).</param>
        public static long PickupRoom(long pickedCeiling, long total, IEnumerable<(int Tier, long Ceiling, int Count)> carriedByTier)
            => PickupRoom(pickedCeiling, total, carriedByTier.Select(c => (c.Tier, 0L, c.Ceiling, c.Count)));

        /// <summary>
        /// As above, with each carried tier's floor: a pile counts only once the total reached so far meets its floor (the gem's
        /// use refuses below it), so gems that cannot be used never make a usable pickup crumble. The best order is used: each
        /// step takes the usable pile with the LOWEST ceiling, then looks again - so a pile skipped for its floor counts once
        /// another pile has lifted the total to it (floors and ceilings come from separate settings and need not climb with
        /// the tier).
        /// </summary>
        /// <param name="carriedByTier">(tier, its floor, its ceiling, how many).</param>
        public static long PickupRoom(long pickedCeiling, long total, IEnumerable<(int Tier, long Floor, long Ceiling, int Count)> carriedByTier)
        {
            var reach = total;
            var piles = carriedByTier.Where(c => c.Count > 0).OrderBy(c => c.Ceiling).ThenBy(c => c.Tier).ToList();
            while (true)
            {
                var i = piles.FindIndex(c => reach >= c.Floor && reach < c.Ceiling);
                if (i < 0)
                    break;   // nothing left that can add (the rest are full or still below their floor)
                reach = Math.Min(piles[i].Ceiling, reach + piles[i].Count);
                piles.RemoveAt(i);
            }
            return pickedCeiling - reach;
        }

        // ---- the kill (the player's thread, from TriuneEarning.Credit) ----------------------------------------------------

        /// <summary>
        /// One school's chance to grow by one on this kill (pure, tested): 0 outside [floor, ceiling); otherwise the full first-band
        /// chance while the school is catching up (more than 500 below the ceiling), the weekly bands within the last 500
        /// (TriuneEarning.SchoolGemChance).
        /// </summary>
        public static double GrantChance(long count, long floor, long ceiling, TriuneTierRow row, double killsBefore)
            => Qualifies(count, floor, ceiling) ? TriuneEarning.SchoolGemChance(row, killsBefore, count, ceiling) : 0.0;

        /// <summary>
        /// Rolls each attack school once for this kill and adds +1 straight onto the count of every school that hits (owner
        /// 2026-11-24: no gem to loot or use any more), with one chat line each - the way the Creature / Item / Life rewards
        /// grow. Called by TriuneEarning.Credit on the player's own thread for an own-tier kill it counts, after the rewards;
        /// <paramref name="killsBefore"/> = the week's kills before this one. Gems off, or a tier without gems: nothing.
        /// </summary>
        public static void GrantOnKill(Player player, TriuneEarning.Config cfg, int tier, TriuneTierRow row, double killsBefore)
        {
            if (player == null || row == null || row.Points <= 0 || !GemsDropAt(cfg, tier))
                return;

            var ceiling = TriuneEarning.GemCeiling(cfg, tier);
            var floor = TriuneEarning.GemFloor(cfg, tier);
            foreach (var school in Schools)
            {
                // each school's own chance: the full first-band chance all week while it catches up, the weekly slowdown only
                // within the last 500 below the tier's ceiling, never a stop
                var chance = GrantChance(Power(player, school), floor, ceiling, row, killsBefore);
                if (!(chance > 0) || ThreadSafeRandom.Next(0.0f, 1.0f) >= chance)
                    continue;

                // the stored count + 1 (Power also adds a held charm: never written back)
                var newCount = Total(player, school) + 1;
                player.SetProperty(school.AugCount, newCount);
                player.Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, school.AugCount, newCount));
                player.SendMessage($"Your {school.Name} augmentations grow to {newCount:N0}.", ChatMessageType.Broadcast);
            }
        }

        // ---- pickup (the player's thread) -------------------------------------------------------------------------------

        /// <summary>
        /// Called when a player takes an item off a corpse. True = the gem crumbled (it was destroyed and the player told):
        /// their own earned gem that could add nothing, because the school plus the earned gems of that school they already
        /// carry at that tier or lower fill the tier's ceiling.
        /// </summary>
        public static bool CrumbleOnPickup(Player player, WorldObject item, Container itemRootOwner)
        {
            try
            {
                var school = ForGem(item.WeenieClassId);
                var stamp = Stamp(item);
                if (school == null || stamp == null || stamp.Value.Earner != player.Guid.Full || !TriuneEarning.Current.GemsEnabled)
                    return false;

                if (AugMove.AwaitsMove(player))
                    return false;   // old counters not moved yet: never destroy a gem on a count about to change

                var cfg = TriuneEarning.Current;
                var ceiling = TriuneEarning.GemCeiling(cfg, stamp.Value.Tier);
                var total = Power(player, school);
                var carried = player.GetInventoryItemsOfWCID(school.GemWcid)
                    .Select(g => g != item ? Stamp(g) : null)
                    .Where(s => s is { } st && st.Earner == player.Guid.Full && st.Tier <= stamp.Value.Tier)
                    .GroupBy(s => s.Value.Tier)
                    .Select(t => (t.Key, TriuneEarning.GemFloor(cfg, t.Key), TriuneEarning.GemCeiling(cfg, t.Key), t.Count()));
                if (PickupRoom(ceiling, total, carried) > 0)
                    return false;

                // the gem may have left the corpse during the pickup animation (another looter, the corpse rotting) - then this is
                // not ours to destroy: the normal pickup path reports its usual failure
                if (itemRootOwner == null || !itemRootOwner.TryRemoveFromInventory(item.Guid))
                    return false;
                // the client is told before the guid is freed: the pending pickup is released (as every other abort of this
                // pickup does) and the gem leaves the open corpse window - Destroy sends nothing for an item inside a container
                player.Session?.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(player.Session, item.Guid.Full));
                player.Session?.Network.EnqueueSend(new GameMessageDeleteObject(item));
                item.Destroy();
                player.SendMessage($"The {item.Name} crumbles as you touch it - you already carry all the {school.Name} augmentations these lands can give you.", ChatMessageType.Broadcast);
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[SchoolGems] CrumbleOnPickup for {player?.Name}: {ex}");
                return false;
            }
        }

        // ---- use (the gem's Use emote, PromptAddAugment) ----------------------------------------------------------------

        private static readonly string[] NotYoursLines =
        {
            "The gem crumbles in your hand. It remembers who really earned it, and it was not you.",
            "You feel nothing. Somewhere, the one who earned this gem is laughing.",
            "The gem goes dark. It was never yours, and now it is no one's.",
            "The gem sighs, unimpressed, and turns to dust. Try earning one.",
        };

        /// <summary>
        /// The earned gem's use. Returns false for a gem without an earned stamp (the caller then says the gem is no longer
        /// used); true when this handled it, whatever the outcome.
        /// </summary>
        public static bool TryUse(Player player, WorldObject gem)
        {
            var school = ForGem(gem?.WeenieClassId ?? 0);
            var stamp = Stamp(gem);
            if (player == null || school == null || stamp == null)
                return false;

            if (AugMove.AwaitsMove(player))
            {
                // old counters not moved yet: the gem waits (kept) until the move has run
                player.SendMessage("The gem is quiet for now - your augmentations are being brought up to date.", ChatMessageType.Broadcast);
                return true;
            }

            var cfg = TriuneEarning.Current;

            // someone else's gem: it is used up and gives nothing
            if (stamp.Value.Earner != player.Guid.Full)
            {
                if (player.TryConsumeFromInventoryWithNetworking(gem, 1))
                    player.SendMessage(NotYoursLines[ThreadSafeRandom.Next(0, NotYoursLines.Length - 1)], ChatMessageType.Broadcast);
                return true;
            }

            var total = Power(player, school);
            var gemFloor = TriuneEarning.GemFloor(cfg, stamp.Value.Tier);
            if (total < gemFloor)
            {
                player.SendMessage($"The gem will not answer yet. Bring your {school.Name} augmentations to {gemFloor:N0} first.", ChatMessageType.Broadcast);
                return true;
            }

            var ceiling = TriuneEarning.GemCeiling(cfg, stamp.Value.Tier);
            if (total >= ceiling)
            {
                // counts never go down: this gem can never add anything again
                if (player.TryConsumeFromInventoryWithNetworking(gem, 1))
                    player.SendMessage($"The gem crumbles. Your {school.Name} augmentations have already taken all they can from the lands this gem came from.", ChatMessageType.Broadcast);
                return true;
            }

            // consume THIS gem (never another copy) before granting, so a failed consume never awards a free point
            if (!player.TryConsumeFromInventoryWithNetworking(gem, 1))
            {
                player.SendMessage("Failed to use the gem. Please try again.", ChatMessageType.Broadcast);
                return true;
            }

            // +1 straight onto the school's count, at every tier (one stat, owner 2026-11-22)
            var newCount = Total(player, school) + 1;   // the stored count (Power also adds a held charm: never written back)
            player.SetProperty(school.AugCount, newCount);
            player.Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, school.AugCount, newCount));
            player.SendMessage($"Your {school.Name} augmentations grow to {newCount:N0}.", ChatMessageType.Broadcast);
            return true;
        }

        // ---- login: held gems take the new names ---------------------------------------------------------------------

        /// <summary>Renames the attack school gems this player carries (owner 2026-11-22: Gem of War / Void / Melee / Missile
        /// Augmentation), so copies made before the rename read the same as new ones. Every login; cheap when nothing to do.</summary>
        public static void RenameHeldGems(Player player)
        {
            if (player == null)
                return;
            foreach (var school in Schools)
                // only EARNED gems take the new name: an old vendor gem of the same wcid keeps its name (it is no longer used)
                foreach (var gem in player.GetInventoryItemsOfWCID(school.GemWcid))
                    if (Stamp(gem) != null && gem.Name != school.GemName)
                        gem.Name = school.GemName;
        }
    }
}
