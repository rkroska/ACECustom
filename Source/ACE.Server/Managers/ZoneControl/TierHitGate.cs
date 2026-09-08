using System;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
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
        public static bool CanHit(Creature attacker, Creature target, out string reason)
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

            // Effective counts, so growth charms count toward the gate exactly as they do everywhere
            // else (Creature_Properties: Effective* = raw luminance + Triune Weave).
            var creature = (long)player.EffectiveCreatureAugCount;
            var item = (long)player.EffectiveItemAugCount;
            var triune = player.GetProperty(PropertyInt64.TriuneWeaveCount) ?? 0;

            // EVERY unmet requirement, one per line under a plain lead line (owner 2026-09-01).
            //
            // It used to return on the FIRST failure, so a player short on two counters was told about
            // Creature only, farmed exactly that, came back, and was told about Item for the first
            // time - one grind turned into two, reading as the goalposts moving. The message is the
            // only feedback the gate ever gives, so it has to be complete.
            //
            // Requirements the player already MEETS are left out, and so is any counter this tier does
            // not ask for at all (MinWieldTriune is 0 below T16 - printing "0 of 0" is noise). The list
            // is exactly what is still to do.
            var needCreature = row.MinWieldCreature > 0 && creature < row.MinWieldCreature;
            var needItem = row.MinWieldAugs > 0 && item < row.MinWieldAugs;
            var needTriune = row.MinWieldTriune > 0 && triune < row.MinWieldTriune;

            if (!needCreature && !needItem && !needTriune)
                return true;

            // Built ONLY on a block - the pass path above allocates nothing. Lines are newline-joined
            // and split by CanHitOrTell into one chat message each, rather than relying on the client
            // to render a newline inside a single system message.
            var sb = new System.Text.StringBuilder("You are not powerful enough to fight this creature.");
            if (needCreature) sb.Append('\n').Append($"Creature augmentations: {creature:N0} of {row.MinWieldCreature:N0}");
            if (needItem) sb.Append('\n').Append($"Item augmentations: {item:N0} of {row.MinWieldAugs:N0}");
            if (needTriune) sb.Append('\n').Append($"Triune Weave: {triune:N0} of {row.MinWieldTriune:N0}");
            reason = sb.ToString();
            return false;
        }

        /// <summary>
        /// CanHit plus the player-facing message. Every blocked swing says why - deliberately not
        /// rate-limited (owner 2026-08-31: "This message can be displayed every swing / hit").
        /// </summary>
        public static bool CanHitOrTell(Creature attacker, Creature target)
        {
            if (CanHit(attacker, target, out var reason))
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
    }
}
