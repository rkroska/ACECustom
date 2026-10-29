using System;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>The four T11+ cloak procs. None = the cloak keeps its retail behavior (Cloak.cs).
    /// The numbers are sent to loot rules (ZoneLootProps.CloakProcId) - NEVER renumber.</summary>
    public enum ZcCloakProc
    {
        None = 0,
        CalmingBreeze = 1,      // was: the 8 ring spells
        SmokeScreen = 2,        // was: the 3 Shrouds of Darkness
        ArtfulDodger = 3,       // was: the -200 damage weave (CloakWeaveProc 2)
        AsheronsGrace = 4,      // was: Cloaked in Skill
    }

    /// <summary>
    /// T11+ CLOAK PROCS (owner 2026-10-28). A cloak stamped ZcTier 11+ no longer casts its retail proc spell or takes 200 off a
    /// hit. Instead every hit the wearer takes rolls a flat chance (zc_cloak_proc_chance, 2%) on a per-character cooldown
    /// (zc_cloak_proc_cooldown, 7 s), and the proc the cloak ALREADY CARRIES picks which of four defensive effects fires:
    ///
    ///   the 8 rings        -> Calming Breeze : heal zc_cloak_breeze_heal (500), static - no buff, rating or aug touches it
    ///   the 3 Shrouds      -> Smoke Screen   : the hit is negated, True Damage included
    ///   the -200 weave     -> Artful Dodger  : the next zc_cloak_dodger_hits (3) hits within zc_cloak_dodger_window (7 s)
    ///                                          take stamina instead of health; what stamina can't cover goes to health
    ///   Cloaked in Skill   -> Asheron's Grace: zc_cloak_grace_seconds (2 s) of protection from all damage, with the
    ///                                          lifestone shield. Built on the Cheat Death window (Player.ZcDamageImmune),
    ///                                          not on real lifestone protection, which the wearer's own swing dispels.
    ///
    /// Nothing on the cloak is rewritten: the mapping is read at proc time, so every T11+ cloak already in a pack, a bank or
    /// a house changes at once, and zc_cloak_procs_enabled FALSE (or Zone Control off) puts them all back on retail.
    /// Retail cloaks below T11 never change. The armor zone lock makes the proc dormant, like the other worn specials.
    ///
    /// Call order at every place a player takes a hit (TakeDamageInternal, SpellProjectile, the smart ring, Harm, Drain):
    /// PreHit before the health damage lands (the roll; Smoke Screen negates here and waiting Artful Dodger charges are
    /// spent here), PostHit after it, only while the player is still alive (Calming Breeze heals, Asheron's Grace and
    /// Artful Dodger start - so both begin after the hit that fired them). Hotspot ticks, DoTs and zone ticks are not hits.
    /// </summary>
    public static class ZcCloakProcs
    {
        public const string CalmingBreezeName = "Calming Breeze";
        public const string SmokeScreenName = "Smoke Screen";
        public const string ArtfulDodgerName = "Artful Dodger";
        public const string AsheronsGraceName = "Asheron's Grace";

        // visual effects - one place, so they are easy to swap after an in-game look
        private const PlayScript BreezeEffect = PlayScript.HealthUpRed;              // the heal-self / healing kit effect
        private const PlayScript SmokeEffect = PlayScript.ShieldUpGrey;              // grey veil around the wearer
        private const PlayScript DodgerArmEffect = PlayScript.SwapHealth_Red_To_Yellow;   // health -> stamina
        private const PlayScript DodgerHitEffect = PlayScript.HealthDownYellow;      // stamina takes the hit
        public const PlayScript GraceEffect = PlayScript.ShieldUpBlue;               // the lifestone protection shield
        private const PlayScript GraceFadeEffect = PlayScript.ShieldDownBlue;

        public static bool Enabled => ServerConfig.zc_cloak_procs_enabled.Value && ServerConfig.zonecontrol_enabled.Value;

        public static double Chance => Math.Clamp(ServerConfig.zc_cloak_proc_chance.Value, 0.0, 1.0);
        public static double Cooldown => Math.Max(0.0, ServerConfig.zc_cloak_proc_cooldown.Value);
        public static int BreezeHeal => (int)Math.Clamp(ServerConfig.zc_cloak_breeze_heal.Value, 0, int.MaxValue);
        public static int DodgerHits => (int)Math.Clamp(ServerConfig.zc_cloak_dodger_hits.Value, 0, 1000);
        public static double DodgerWindow => Math.Max(0.0, ServerConfig.zc_cloak_dodger_window.Value);
        public static double GraceSeconds => Math.Max(0.0, ServerConfig.zc_cloak_grace_seconds.Value);

        /// <summary>Which T11+ proc this item carries, or None when it is not a T11+ cloak, the system is off, or its retail
        /// proc is not one of the 13 retail cloak procs (it then keeps that proc).</summary>
        public static ZcCloakProc Classify(WorldObject cloak)
        {
            if (cloak == null || !Cloak.IsCloak(cloak) || !Enabled || !ZoneControlManager.IsZcGear(cloak))
                return ZcCloakProc.None;

            var procSpell = cloak.ProcSpell;
            if (procSpell.HasValue)
            {
                switch ((SpellId)procSpell.Value)
                {
                    case SpellId.AcidRing:
                    case SpellId.BladeRing:
                    case SpellId.FlameRing:
                    case SpellId.ForceRing:
                    case SpellId.FrostRing:
                    case SpellId.LightningRing:
                    case SpellId.ShockwaveRing:
                    case SpellId.NetherRing:
                        return ZcCloakProc.CalmingBreeze;

                    case SpellId.CloakMagicDLower:
                    case SpellId.CloakMeleeDLower:
                    case SpellId.CloakMissileDLower:
                        return ZcCloakProc.SmokeScreen;

                    case SpellId.CloakAllSkill:
                        return ZcCloakProc.AsheronsGrace;

                    default:
                        return ZcCloakProc.None;
                }
            }

            return cloak.CloakWeaveProc == 2 ? ZcCloakProc.ArtfulDodger : ZcCloakProc.None;
        }

        /// <summary>True when this cloak's retail proc is replaced (Cloak.HasProcSpell / HasDamageProc answer false for it).</summary>
        public static bool Owns(WorldObject cloak) => Classify(cloak) != ZcCloakProc.None;

        /// <summary>The proc's in-game name, or null for None.</summary>
        public static string NameOf(ZcCloakProc proc) => proc switch
        {
            ZcCloakProc.CalmingBreeze => CalmingBreezeName,
            ZcCloakProc.SmokeScreen => SmokeScreenName,
            ZcCloakProc.ArtfulDodger => ArtfulDodgerName,
            ZcCloakProc.AsheronsGrace => AsheronsGraceName,
            _ => null,
        };

        /// <summary>What the proc does, from the live settings (ASCII only - the client renders this).</summary>
        public static string DescriptionOf(ZcCloakProc proc) => proc switch
        {
            ZcCloakProc.CalmingBreeze => $"heals you for {BreezeHeal:N0} health",
            ZcCloakProc.SmokeScreen => "negates all damage from that hit",
            ZcCloakProc.ArtfulDodger => $"your next {DodgerHits} {(DodgerHits == 1 ? "hit" : "hits")} within {DodgerWindow:0.#}s strike stamina instead of health",
            ZcCloakProc.AsheronsGrace => $"protects you from all damage for {GraceSeconds:0.#}s",
            _ => null,
        };

        /// <summary>The cloak's line for the item panel's modifier block, or null for anything this system does not own.
        /// Built live, never baked into the item, so a settings change reads on every cloak at once.</summary>
        public static string AppraisalLine(WorldObject cloak, bool off = false)
        {
            var proc = Classify(cloak);
            if (proc == ZcCloakProc.None)
                return null;

            if (off)
                return $"- {NameOf(proc)} (off here)";

            return $"- {NameOf(proc)}: {Chance * 100:0.#}% when hit, {DescriptionOf(proc)} ({Cooldown:0.#}s cooldown)";
        }

        /// <summary>
        /// Before a hit's health damage lands. <paramref name="normal"/> and <paramref name="trueDamage"/> are the hit's two parts
        /// (pass 0 for True Damage on a path that has none) and are changed in place: Smoke Screen zeroes both, a waiting Artful
        /// Dodger charge moves them to stamina. Also closes the Grace / Cheat Death window on the paths that have no guard of their
        /// own (Harm, Drain, a player's smart ring). Returns the proc that fired on THIS hit, for PostHit.
        /// </summary>
        public static ZcCloakProc PreHit(Player defender, WorldObject source, ref float normal, ref float trueDamage)
        {
            if (defender == null || defender.IsDead || defender.Invincible)
                return ZcCloakProc.None;

            if (float.IsNaN(normal) || normal < 0) normal = 0;
            if (float.IsNaN(trueDamage) || trueDamage < 0) trueDamage = 0;

            var total = normal + trueDamage;
            if (total <= 0)
                return ZcCloakProc.None;

            // Asheron's Grace / Cheat Death: the hit is absorbed whole. The melee and projectile paths return before they get
            // here; this is the guard for Harm, Drain and the smart ring, which have none of their own.
            if (defender.ZcDamageImmune)
            {
                defender.ZcAnnounceAbsorb(source, $"{Math.Round(total):N0} damage");
                normal = 0;
                trueDamage = 0;
                return ZcCloakProc.None;
            }

            var now = Time.GetUnixTime();

            // the system switched off, or the armor zone lock (dormant, like Cheat Death / Battle Mending -
            // Creature.GetZoneModifierMax): no roll, and any Artful Dodger charges still waiting go with it
            if (!Enabled || ZoneControlManager.WornPowerSuppressed(defender))
            {
                defender.ZcDodgerHitsLeft = 0;
                return ZcCloakProc.None;
            }

            // a waiting Artful Dodger charge takes this hit - before any new roll (the cooldown blocks one anyway)
            if (defender.ZcDodgerHitsLeft > 0)
            {
                if (now < defender.ZcDodgerUntil)
                {
                    TakeOnStamina(defender, source, ref normal, ref trueDamage);
                    return ZcCloakProc.None;
                }
                defender.ZcDodgerHitsLeft = 0;
            }

            var proc = Classify(defender.EquippedCloak);
            if (proc == ZcCloakProc.None)
                return ZcCloakProc.None;

            // /cloakproc force (developer testing): this hit procs, whatever the chance and cooldown say
            if (!defender.ZcCloakProcForceNext)
            {
                if (now < defender.ZcCloakProcReadyAt)
                    return ZcCloakProc.None;

                if (ThreadSafeRandom.Next(0.0f, 1.0f) >= Chance)
                    return ZcCloakProc.None;
            }
            defender.ZcCloakProcForceNext = false;
            defender.ZcCloakProcReadyAt = now + Cooldown;

            switch (proc)
            {
                case ZcCloakProc.SmokeScreen:
                    AnnounceWeave(defender, proc);
                    defender.EnqueueBroadcast(new GameMessageScript(defender.Guid, SmokeEffect));
                    defender.SendMessage($"Smoke Screen negates {Math.Round(total):N0} points of damage{FromWhom(source)}!", ChatMessageType.Magic);
                    if (source is Player attacker && attacker != defender)
                        attacker.SendMessage($"The cloak of {defender.Name} weaves Smoke Screen and negates your attack!", ChatMessageType.Magic);
                    normal = 0;
                    trueDamage = 0;
                    break;

                // Calming Breeze, Asheron's Grace and Artful Dodger act after the hit lands (PostHit) - the hit that fired
                // Artful Dodger lands as normal, and only the NEXT hits go to stamina
            }

            return proc;
        }

        /// <summary>After the hit's health damage has landed. Only acts while the wearer is alive.</summary>
        public static void PostHit(Player defender, ZcCloakProc proc)
        {
            if (defender == null || defender.IsDead || defender.Health.Current <= 0)
                return;

            switch (proc)
            {
                case ZcCloakProc.CalmingBreeze:
                {
                    // static: a straight health delta, never the heal path, so no Healing Boost, spell, rating or aug changes it
                    var healed = defender.UpdateVitalDelta(defender.Health, BreezeHeal);
                    if (healed > 0)
                        defender.DamageHistory.OnHeal((uint)healed);
                    AnnounceWeave(defender, proc);
                    defender.EnqueueBroadcast(new GameMessageScript(defender.Guid, BreezeEffect));
                    defender.SendMessage($"Calming Breeze restores {Math.Max(0, healed):N0} points of your health.", ChatMessageType.Magic);
                    break;
                }

                case ZcCloakProc.AsheronsGrace:
                {
                    var seconds = GraceSeconds;
                    if (seconds <= 0)
                        break;
                    var until = Time.GetUnixTime() + seconds;
                    defender.ZcCloakGraceUntil = until;
                    AnnounceWeave(defender, proc);
                    defender.EnqueueBroadcast(new GameMessageScript(defender.Guid, GraceEffect));
                    defender.SendMessage($"Asheron's Grace! You are protected from all damage for {seconds:0.#} seconds.", ChatMessageType.Magic);

                    var graceExpiry = new ActionChain();
                    graceExpiry.AddDelaySeconds(seconds);
                    graceExpiry.AddAction(defender, ActionType.Player_ZcCloakProcExpired, () =>
                    {
                        if (defender.IsDead || defender.ZcCloakGraceUntil != until)
                            return;
                        defender.EnqueueBroadcast(new GameMessageScript(defender.Guid, GraceFadeEffect));
                        defender.SendMessage("Asheron's Grace fades.", ChatMessageType.Magic);
                    });
                    graceExpiry.EnqueueChain();
                    break;
                }

                case ZcCloakProc.ArtfulDodger:
                {
                    var hits = DodgerHits;
                    var window = DodgerWindow;
                    if (hits <= 0 || window <= 0)
                        break;
                    var until = Time.GetUnixTime() + window;
                    defender.ZcDodgerHitsLeft = hits;
                    defender.ZcDodgerUntil = until;
                    AnnounceWeave(defender, proc);
                    defender.EnqueueBroadcast(new GameMessageScript(defender.Guid, DodgerArmEffect));
                    defender.SendMessage($"Artful Dodger! Your next {hits} {(hits == 1 ? "hit" : "hits")} within {window:0.#} seconds strike your stamina instead of your health.", ChatMessageType.Magic);

                    var dodgerExpiry = new ActionChain();
                    dodgerExpiry.AddDelaySeconds(window);
                    dodgerExpiry.AddAction(defender, ActionType.Player_ZcCloakProcExpired, () =>
                    {
                        // only this arming's notice, and only if a charge was left unused
                        if (defender.IsDead || defender.ZcDodgerUntil != until || defender.ZcDodgerHitsLeft <= 0)
                            return;
                        defender.ZcDodgerHitsLeft = 0;
                        defender.SendMessage("Artful Dodger fades.", ChatMessageType.Magic);
                    });
                    dodgerExpiry.EnqueueChain();
                    break;
                }
            }
        }

        /// <summary>One Artful Dodger charge: the whole hit (True Damage included) comes off stamina; whatever stamina cannot
        /// cover stays on the hit, split between its two parts as before.</summary>
        private static void TakeOnStamina(Player defender, WorldObject source, ref float normal, ref float trueDamage)
        {
            var total = normal + trueDamage;
            var want = (int)Math.Min(int.MaxValue, Math.Round((double)total));
            var taken = want > 0 ? -defender.UpdateVitalDelta(defender.Stamina, -want) : 0;
            if (taken < 0) taken = 0;

            var left = Math.Max(0f, total - taken);
            var scale = total > 0 ? left / total : 0f;
            normal *= scale;
            trueDamage *= scale;

            defender.ZcDodgerHitsLeft--;
            var hitsLeft = defender.ZcDodgerHitsLeft;

            var remaining = hitsLeft > 0 ? $" ({hitsLeft} left)" : "";
            if (taken <= 0)
            {
                defender.SendMessage($"Artful Dodger can't help - you have no stamina left!{remaining}", ChatMessageType.Magic);
                return;
            }

            defender.EnqueueBroadcast(new GameMessageScript(defender.Guid, DodgerHitEffect));
            var through = left >= 0.5f ? $" {Math.Round(left):N0} gets through to your health." : "";
            defender.SendMessage($"Artful Dodger turns {taken:N0} points of damage{FromWhom(source)} into stamina loss.{through}{remaining}", ChatMessageType.Magic);
        }

        /// <summary>The same nearby broadcast the retail weave used, with the new name.</summary>
        private static void AnnounceWeave(Player defender, ZcCloakProc proc)
        {
            defender.EnqueueBroadcast(new GameMessageSystemChat($"The cloak of {defender.Name} weaves {NameOf(proc)}!", ChatMessageType.Spellcasting),
                WorldObject.LocalBroadcastRange, ChatMessageType.Spellcasting);
        }

        /// <summary>" from &lt;name&gt;" for a chat line, or nothing when the hit has no source.</summary>
        private static string FromWhom(WorldObject source) => source != null ? " from " + source.Name : "";
    }
}
