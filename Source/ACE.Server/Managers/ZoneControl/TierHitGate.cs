using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Managers.WeaponScaling;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// The T11+ HIT GATE (owner 2026-08-31).
    ///
    /// A player may only land a hit on a tier-11-or-higher monster once their augmentation counters
    /// reach that tier's requirement. Below it, every swing and every spell MISSES outright - all or
    /// nothing, no partial damage, no probability.
    ///
    /// 🔴 WHY THIS IS NOT A SKILL THRESHOLD. The obvious design - author monster defenses just under
    /// a best-in-slot player's attack skill - CANNOT produce this behaviour. Hit chance is a sigmoid,
    /// P = 1/(1+exp(-0.03 x (A-D))) (SkillCheck.cs), so "defense = ceiling minus one" is a 50/50 coin
    /// flip, and the span from never-hits to always-hits is only ~920 points (~9,000-11,500 with
    /// defense scaling on). There is no value that makes a threshold binary. An aug counter is
    /// already a step function, so the gate reads that instead.
    ///
    /// It is also immune to a pile of things a skill threshold would have to chase: uncapped
    /// enlightenment, possibly-stacking aura categories, velvet tinkering, attribute ranks, and the
    /// War Magic x1.11 that Void and Missile do not get.
    ///
    /// DIRECTION: player -> monster ONLY. Monsters always land on players regardless (owner: "players
    /// should never resist or evade t11+ monsters, even in t25 bis gear"); that is already achieved by
    /// authoring monster attack_skill / magic_skill at 100,000, which sits ~92,000 above player
    /// defenses where the sigmoid saturates at a gap of 460.
    ///
    /// KEYED ON THE MONSTER'S VARIATION, never on the weapon's tier - otherwise an under-augmented
    /// player dodges the whole gate by swinging a lower-tier weapon, which is the loophole it exists
    /// to close.
    /// </summary>
    public static class TierHitGate
    {
        /// <summary>Below this variation the gate never applies - retail content is untouched.</summary>
        public const int MinGatedVariation = 11;

        /// <summary>
        /// Can this attacker land on this target? TRUE for everything the gate does not cover, so a
        /// caller can use it as a plain pre-condition.
        ///
        /// <paramref name="reason"/> is non-null ONLY on a block, and is shown to the player on EVERY
        /// blocked swing (owner 2026-08-31 - a silent miss reads as bad luck, which defeats the point).
        /// </summary>
        public static bool CanHit(Creature attacker, Creature target, out string reason, AttackSchool school = AttackSchool.None)
        {
            reason = null;

            if (!ServerConfig.zc_tier_hit_gate_enabled.Value)
                return true;

            // Only PLAYERS are gated. Monster-on-player and monster-on-monster are never touched.
            if (attacker is not Player player || target == null)
                return true;

            // The MONSTER's layer decides the requirement, not the weapon.
            //
            // GOVERNED MONSTERS ONLY (owner 2026-09-01, before shipping the gate ON). This used to key
            // on target.Location.Variation alone, which meant the gate fired on ANY creature standing at
            // v11+ - including one carrying ExemptFromZoneScaling, and including landblocks no enabled
            // zone covers. An exempt vendor or quest NPC was therefore UNATTACKABLE by an
            // under-augmented player, with no way to opt out, the moment the gate was switched on.
            //
            // ResolveForCreature is the single "may Zone Control touch this creature" gate and answers
            // all four questions at once: not a Player, not a Pet, not ExemptFromZoneScaling, an ENABLED
            // zone covers the landblock, and that zone's variation matches the creature's. If Zone
            // Control does not govern the monster, the gate has no business gating it. Lock-free hot
            // path (a hashset probe then a dictionary lookup), the same call the combat sites already
            // make - see ZoneControlManager.ResolveForCreature.
            if (ZoneControlManager.ResolveForCreature(target) == null)
                return true;

            // GetEffectiveVariation, not raw Location.Variation - every other Zone Control consumer
            // resolves through it, and it is what honours the ForceEndgameSystems test hook. Reading
            // the raw value here made the gate the one consumer that disagreed with the rest.
            var variation = ZoneControlManager.GetEffectiveVariation(target);
            if (variation < MinGatedVariation)
                return true;

            var row = WeaponScalingManager.GetTier(variation);
            if (row == null)
                return true;

            // ONE stat per school (owner 2026-11-22): the LumAug counts hold everything - bought, earned, and the retired
            // Triune Weave / school charms moved there at login.
            var creature = player.EffectiveCreatureAugCount;
            var item = player.EffectiveItemAugCount;
            var life = player.EffectiveLifeAugCount;
            var schoolCount = SchoolCount(player, school);

            // EVERY unmet requirement, one per line under a plain lead line (owner 2026-09-01).
            //
            // It used to return on the FIRST failure, so a player short on two counters was told about
            // Creature only, farmed exactly that, came back, and was told about Item for the first
            // time - one grind turned into two, reading as the goalposts moving. The message is the
            // only feedback the gate ever gives, so it has to be complete.
            //
            // Requirements the player already MEETS are left out, and so is any counter this tier does not ask for at all
            // (a spell of no attack school - a Life or Creature debuff - asks no school). The list is exactly what is still to do.
            var needCreature = Short(creature, row.MinWieldCreature);
            var needItem = Short(item, row.MinWieldAugs);
            var needLife = Short(life, row.MinWieldLife);
            var needSchool = school != AttackSchool.None && Short(schoolCount, row.MinWieldSkillCharm);

            if (!needCreature && !needItem && !needLife && !needSchool)
                return true;

            // Built ONLY on a block - the pass path above allocates nothing. Lines are newline-joined
            // and split by CanHitOrTell into one chat message each, rather than relying on the client
            // to render a newline inside a single system message.
            var sb = new System.Text.StringBuilder("You are not powerful enough to fight this creature.");
            if (needCreature) sb.Append('\n').Append($"Creature augmentations: {creature:N0} of {row.MinWieldCreature:N0}");
            if (needItem) sb.Append('\n').Append($"Item augmentations: {item:N0} of {row.MinWieldAugs:N0}");
            if (needLife) sb.Append('\n').Append($"Life augmentations: {life:N0} of {row.MinWieldLife:N0}");
            if (needSchool) sb.Append('\n').Append($"{school} augmentations: {schoolCount:N0} of {row.MinWieldSkillCharm:N0}");
            reason = sb.ToString();
            return false;
        }

        /// <summary>The school of an attack. None = no school is asked. The gate is uniform at T11-T25 (owner 2026-11-22):
        /// Creature, Item, Life and the attack's school at every tier, the school on the same ladder as Item and Life.</summary>
        public enum AttackSchool { None, Melee, Missile, War, Void }

        /// <summary>A weapon swing or shot: melee -> Melee, an arrow / bolt / dart / thrown weapon -> Missile.</summary>
        public static AttackSchool SchoolOf(CombatType combatType) => combatType switch
        {
            CombatType.Melee => AttackSchool.Melee,
            CombatType.Missile => AttackSchool.Missile,
            _ => AttackSchool.None,
        };

        /// <summary>
        /// A spell: a proc follows the attack that fired it (the proccing item: a melee weapon -> Melee, a bow / crossbow /
        /// atlatl / thrown weapon -> Missile); any other spell follows its own school (War -> War, Void, DoT ticks included,
        /// -> Void). Life and Creature spells ask no school.
        /// </summary>
        public static AttackSchool SchoolOf(Spell spell, WorldObject item)
        {
            if (item is MeleeWeapon)
                return AttackSchool.Melee;
            if (item != null && item.IsMissileWeapon)
                return AttackSchool.Missile;
            return spell?.School switch
            {
                MagicSchool.WarMagic => AttackSchool.War,
                MagicSchool.VoidMagic => AttackSchool.Void,
                _ => AttackSchool.None,
            };
        }

        /// <summary>A player's augmentation count for an attack school (0 for None).</summary>
        public static long SchoolCount(Player player, AttackSchool school) => school switch
        {
            AttackSchool.Melee => player.EffectiveMeleeAugCount,
            AttackSchool.Missile => player.EffectiveMissileAugCount,
            AttackSchool.War => player.EffectiveWarAugCount,
            AttackSchool.Void => player.EffectiveVoidAugCount,
            _ => 0,
        };

        /// <summary>The player's best attack school count - "which tier is this player" reads the school they fight with best.</summary>
        public static long BestSchoolCount(Player player) => Math.Max(Math.Max(player.EffectiveMeleeAugCount, player.EffectiveMissileAugCount),
            Math.Max(player.EffectiveWarAugCount, player.EffectiveVoidAugCount));

        /// <summary>One counter against one requirement. A requirement of 0 means this tier does not ask for that counter.</summary>
        private static bool Short(long have, int need) => need > 0 && have < need;

        /// <summary>
        /// Do these counts meet this tier row's gate? The same comparisons CanHit makes, so "which tier is this player" and
        /// "can this player hit that monster" can never drift apart: Creature, Item, Life and the attack school at every tier -
        /// here the player's BEST school, so a player is the tier their best attack can fight.
        /// </summary>
        internal static bool MeetsTier(long creature, long item, long life, long bestSchool, WeaponScalingTier row)
        {
            return row != null
                && !Short(creature, row.MinWieldCreature)
                && !Short(item, row.MinWieldAugs)
                && !Short(life, row.MinWieldLife)
                && !Short(bestSchool, row.MinWieldSkillCharm);
        }

        /// <summary>
        /// The highest tier these counters qualify for, 0 when they do not reach T11. Climbs the rows from T11 upward and
        /// stops at the first one that is not met, or at a gap in the table - a tier is only earned through the ones below it.
        /// Rows under T11 (the stored table carries a T10 row) are ignored, and so are rows past the top of the ladder
        /// (the tier-add command accepts any number): no player is ever a tier that has no content.
        /// </summary>
        internal static int HighestTier(long creature, long item, long life, long bestSchool, IEnumerable<WeaponScalingTier> rows)
        {
            if (rows == null)
                return 0;

            var best = 0;
            var expected = MinGatedVariation;
            foreach (var row in rows.Where(r => r != null && r.Tier >= MinGatedVariation && r.Tier <= ZoneRare.MaxTier).OrderBy(r => r.Tier))
            {
                if (row.Tier != expected || !MeetsTier(creature, item, life, bestSchool, row))
                    break;

                best = row.Tier;
                expected++;
            }
            return best;
        }

        /// <summary>
        /// The highest tier this player can fight: the top variation whose monsters their best attack can land on. 0 = not
        /// yet T11. Creature, Item, Life and the best attack school at every tier (owner 2026-11-22: uniform T11-T25).
        /// </summary>
        public static int PlayerTier(Player player)
        {
            if (player == null)
                return 0;

            return HighestTier(
                player.EffectiveCreatureAugCount,
                player.EffectiveItemAugCount,
                player.EffectiveLifeAugCount,
                BestSchoolCount(player),
                WeaponScalingManager.Current.Tiers);
        }

        // ---- reward tier: capped at the highest tier gem the player owns (owner 2026-11-17) ----------------------------

        /// <summary>The Tou Tou Endgame Portal Gems: v11 is 777700029, v12-v25 are 777705120-777705133. Bonded and attuned with
        /// unlimited uses, so owning one means the player passed that tier's gem requirements and can enter the tier.</summary>
        public const uint GemT11Wcid = 777700029;
        public const uint GemT12Wcid = 777705120;

        /// <summary>The tier a weenie class id is the portal gem of, or 0 when it is not a tier gem.</summary>
        public static int GemTier(uint wcid)
        {
            if (wcid == GemT11Wcid)
                return MinGatedVariation;
            if (wcid >= GemT12Wcid && wcid <= GemT12Wcid + (uint)(ZoneRare.MaxTier - 12))
                return 12 + (int)(wcid - GemT12Wcid);
            return 0;
        }

        /// <summary>The highest tier gem among these weenie class ids, 0 when there is none. Stops looking once a gem at
        /// <paramref name="stopAt"/> or higher is found (a gem above the hit tier never raises the reward tier).</summary>
        internal static int HighestGemTier(IEnumerable<uint> wcids, int stopAt = int.MaxValue)
        {
            var best = 0;
            if (wcids == null)
                return best;
            foreach (var wcid in wcids)
            {
                best = Math.Max(best, GemTier(wcid));
                if (best >= stopAt)
                    break;
            }
            return best;
        }

        /// <summary>
        /// The tier a player is rewarded as (owner 2026-11-17: "these should align", then "use whichever the highest gem they
        /// have is"): the lower of the tier they can hit (PlayerTier) and the highest tier gem they own. Before this a player
        /// who could hit a tier but whose gem refused them (short on Life augs or Enlightenment) was counted at that tier, and
        /// every kill in the tier they could actually reach was cut by the tier dip. Feeds TierRewardScale.OwnTier (coins, kill
        /// tasks, bounties, own-tier Triune and school gems) and the rare odds.
        ///
        /// T11 is the floor (owner 2026-11-17): T11 is reached without its gem, so anyone who can hit T11 counts as at least
        /// T11 and the gem cap only bites from T12.
        ///
        /// The gem tier is the one last found in the player's pack (Player.OwnedGemTier, read at login and on every heartbeat)
        /// - inventory is only read on the player's thread, and this is also called from a dying monster's thread (the rare
        /// roll). If it has never been read the hit tier alone is used.
        /// </summary>
        public static int RewardTier(Player player) => player == null ? 0 : RewardTier(PlayerTier(player), player.OwnedGemTier);

        /// <summary>The rule itself: the hit tier, capped at the gem tier floored at T11. A gem tier below 0 = unknown, no cap.</summary>
        internal static int RewardTier(int hitTier, int gemTier)
        {
            if (gemTier < 0)
                return hitTier;
            return Math.Min(hitTier, Math.Max(gemTier, MinGatedVariation));
        }

        /// <summary>
        /// CanHit plus the player-facing message. Every blocked swing says why - deliberately not
        /// rate-limited (owner 2026-08-31: "This message can be displayed every swing / hit").
        /// </summary>
        public static bool CanHitOrTell(Creature attacker, Creature target, AttackSchool school = AttackSchool.None)
        {
            if (CanHit(attacker, target, out var reason, school))
                return true;

            // One chat message PER LINE (owner 2026-09-01: lead line, then a line per unmet
            // requirement). Sent separately rather than as one message containing newlines - the
            // client is not relied on to break the line.
            if (attacker is Player player && reason != null)
            {
                var net = player.Session?.Network;
                if (net != null)
                    foreach (var line in reason.Split('\n'))
                        net.EnqueueSend(new GameMessageSystemChat(line, ChatMessageType.Broadcast));
            }

            return false;
        }

        /// <summary>
        /// LOW-TIER PROC RESIST (owner 2026-10-06, correcting 10-05: "We want the PROCs from those items to get resisted, not all
        /// damage and spells"). True = this harmful PROC on a governed v11+ monster comes from gear below T11, so the caller
        /// treats it as resisted. The callers are the PROC paths only - hand-casts (any wand, bare hands) and gems are never
        /// gated.
        ///
        /// <paramref name="item"/> is the proccing item. NULL means the item is unknown and the cast is left alone.
        /// Aetheria surges are exempt: there is no T11 aetheria, so the rule would remove them outright.
        /// </summary>
        public static bool IsLowTierCast(WorldObject caster, WorldObject target, Spell spell, WorldObject item)
        {
            if (!ServerConfig.zc_low_tier_item_spell_resist.Value)
                return false;
            if (caster is not Player || target is not Creature targetCreature || target is Player)
                return false;
            if (spell == null || !spell.IsHarmful || spell.IsSelfTargeted)
                return false;
            // ResolveCombatProfile: null while zonecontrol_enabled is OFF - the master switch makes this inert like every combat gate
            if (ZoneControlManager.ResolveCombatProfile(targetCreature) == null
                || ZoneControlManager.GetEffectiveVariation(targetCreature) < MinGatedVariation)
                return false;
            // the allow list (owner 2026-10-06: "add / remove procs allowed via plugin, similar to craft components") - read after
            // the profile resolve, which has initialised the zone store
            if (ZoneControlManager.IsProcAllowed(spell.Id))
                return false;

            if (item == null || item is Creature)
                return false;
            if (Aetheria.IsAetheria(item.WeenieClassId))
                return false;
            return ZoneCraftGate.TierOf(item) < MinGatedVariation;
        }

        /// <summary>IsLowTierCast plus the normal resist feedback ("X resists your spell" + the resist sound).
        /// Call it BEFORE the skill resist roll and before any Overpower check - Overpower must not land it.</summary>
        public static bool BlockLowTierCast(WorldObject caster, WorldObject target, Spell spell, WorldObject item)
        {
            if (!IsLowTierCast(caster, target, spell, item))
                return false;

            if (caster is Player player && player.Session != null)
            {
                player.SendChatMessage(target, $"{target.Name} resists your spell", ChatMessageType.Magic);
                player.Session.Network.EnqueueSend(new GameMessageSound(player.Guid, Sound.ResistSpell, 1.0f));
            }
            return true;
        }
    }
}
