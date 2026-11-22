using System;
using System.Collections.Generic;
using System.Linq;

using log4net;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// Attack school gems past the cap (owner 2026-11-01, locked; one stat since 2026-11-22). One +1 gem per school - Gem of
    /// War / Void / Melee / Missile Augmentation - dropped at T11-T25 and reused every tier. A gem adds +1 straight onto that
    /// school's LumAug count (no separate counter):
    ///   - a kill at the player's own tier, in the corpse of the top damager (the corpse owner), never straight into the pack -
    ///     a player who skips looting gets no gems;
    ///   - an independent roll per school at the tier's floor (T15+: the 4,000 luminance cap) and not yet at this tier's
    ///     ceiling (base + the summed Points of T15..tier: 4,500 at T15 ... 9,500 at T25); the chance follows the same weekly
    ///     kill bands as the hunting rewards, on the tier's own gem weeks (4 at the floor, 2.5 at the ceiling);
    ///   - the gem is stamped with its earner (AllowedActivator + "Earned by ...") and its tier (PropertyInt.EarnedGemTier);
    ///   - used by anyone else: consumed, nothing added, a line about who really earned it;
    ///   - used by the earner: +1 while the school is under the gem's OWN tier's ceiling (T15 gems keep working in T16 up to
    ///     4,500); a gem that can never be used again crumbles; under the floor = refused and kept;
    ///   - picked up by the earner when the school + the carried earned gems of that school at that tier or lower already fill
    ///     that tier's ceiling: it crumbles at once.
    /// Off unless /zonecontrol triune gems on. Pace settings live in TriuneEarning's store.
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

        /// <summary>Do gems drop at <paramref name="tier"/>? T11-T14 need the drip on as well as the gems.</summary>
        public static bool GemsDropAt(TriuneEarning.Config cfg, int tier) =>
            cfg.GemsEnabled && tier >= TriuneEarning.DripMinTier && (!TriuneEarning.IsDripTier(tier) || cfg.DripEnabled);

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

        /// <summary>True when gems are on and at least one school can still drop a gem for this player at this tier.</summary>
        public static bool CanEarnAny(Player player, TriuneEarning.Config cfg, int tier)
        {
            if (!GemsDropAt(cfg, tier))
                return false;
            var ceiling = TriuneEarning.GemCeiling(cfg, tier);
            var floor = TriuneEarning.GemFloor(cfg, tier);
            return Schools.Any(s => Qualifies(Power(player, s), floor, ceiling));   // the same count the drop and the use test
        }

        /// <summary>
        /// Room a newly picked-up gem would have: its tier's ceiling minus the highest total the earned gems already carried at
        /// that tier or lower can still reach. Used lowest tier first (the best order), each tier's gems add +1 only up to that
        /// tier's own ceiling - so a spent lower-tier gem, or the part of a pile past its ceiling, takes no room.
        /// </summary>
        /// <param name="carriedByTier">Earned gems carried at the picked-up gem's tier or lower: (tier, its ceiling, how many).</param>
        public static long PickupRoom(long pickedCeiling, long total, IEnumerable<(int Tier, long Ceiling, int Count)> carriedByTier)
        {
            var reach = total;
            foreach (var (_, tierCeiling, count) in carriedByTier.OrderBy(c => c.Tier))
                if (reach < tierCeiling)
                    reach = Math.Min(tierCeiling, reach + count);
            return pickedCeiling - reach;
        }

        // ---- the drop (victim thread, while the corpse is filled) ------------------------------------------------------

        /// <summary>Called from Creature.GenerateTreasure after the corpse's loot: rolls the corpse owner's school gems.</summary>
        public static void AddToCorpse(Creature victim, Corpse corpse)
        {
            try
            {
                if (victim == null || corpse == null || victim is Player || corpse.KillerId == null)
                    return;

                var cfg = TriuneEarning.Current;
                if (!cfg.GemsEnabled)
                    return;

                var killTier = TierRewardScale.KillTier(victim);
                if (!GemsDropAt(cfg, killTier))
                    return;
                var row = TriuneEarning.GetTier(killTier);
                if (row == null || row.Points <= 0)
                    return;
                if (!BountyManager.PaysBounty(victim))
                    return;

                // the corpse owner: the top damager, or a pet's owner - the one whose corpse this is.
                // These reads of the player happen on the VICTIM's thread (TierRewards reads only on the player's): the corpse is
                // filled here, before it enters the world, so the roll cannot be deferred to the player's thread. Property reads go
                // through the biota lock - the worst case is a value a moment old (e.g. the week count one kill off at a band edge).
                var player = PlayerManager.GetOnlinePlayer(new ObjectGuid(corpse.KillerId.Value));
                if (player == null || player.IsLoggingOut || player.IsMule || player.IsOlthoiPlayer)
                    return;
                if (AugMove.AwaitsMove(player))
                    return;   // old counters not moved yet: no gem on them (the move adds them later)
                if (TierRewardScale.OwnTier(player) != killTier)
                    return;

                // the week's band, as it stood BEFORE this kill: the count TriuneEarning took at the death, ahead of its credit
                // (the count itself is moved on the player's own thread); a kill it did not see reads the count as it is now
                var killsBefore = victim.TriuneWeekKillsAtDeath ?? TriuneEarning.WeekKills(player, cfg);
                var chance = TriuneEarning.GemChance(row, killsBefore);
                if (!(chance > 0))
                    return;

                var ceiling = TriuneEarning.GemCeiling(cfg, killTier);
                var floor = TriuneEarning.GemFloor(cfg, killTier);
                foreach (var school in Schools)
                {
                    if (!Qualifies(Power(player, school), floor, ceiling))
                        continue;
                    if (ThreadSafeRandom.Next(0.0f, 1.0f) >= chance)
                        continue;

                    var gem = WorldObjectFactory.CreateNewWorldObject(school.GemWcid);
                    if (gem == null)
                    {
                        log.Warn($"[SchoolGems] gem {school.GemWcid} could not be created");
                        continue;
                    }
                    gem.AllowedActivator = player.Guid.Full;
                    gem.SetProperty(PropertyInt.EarnedGemTier, killTier);
                    gem.SetProperty(PropertyString.LongDesc, $"Earned by {player.Name}.");
                    if (!corpse.TryAddToInventory(gem))
                    {
                        log.Warn($"[SchoolGems] {corpse.Name} is full - {player.Name}'s {gem.Name} was not added");
                        gem.Destroy();
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"[SchoolGems] AddToCorpse: {ex}");
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
                    .Select(t => (t.Key, TriuneEarning.GemCeiling(cfg, t.Key), t.Count()));
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
