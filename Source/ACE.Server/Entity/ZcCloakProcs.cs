using System;

using log4net;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;
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
    ///                                          take stamina instead of health, zc_cloak_dodger_stamina_mult (10) stamina
    ///                                          per point of damage; what stamina can't cover goes to health
    ///   Cloaked in Skill   -> Asheron's Grace: zc_cloak_grace_seconds (2 s) of protection from all damage, with the
    ///                                          lifestone shield. Built on the Cheat Death window (Player.ZcDamageImmune),
    ///                                          not on real lifestone protection, which the wearer's own swing dispels.
    ///
    /// Every number above is a server setting that a zone or a tier Default can re-tune with the Zone Control stats cloak_*
    /// (owner 2026-10-29), read where the wearer stands - see <see cref="DialsFor"/>.
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
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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

        /// <summary>Where a set of dials came from.</summary>
        public enum DialSource
        {
            /// <summary>No Zone Control value: the server settings (zc_cloak_*).</summary>
            Server,
            /// <summary>The governing zone's Default row (its own stats over the tier Default stack).</summary>
            Zone,
            /// <summary>The tier's Default row (with the v11 anchor under T12+), on a T11+ spot no enabled zone covers.</summary>
            TierDefault,
        }

        /// <summary>
        /// The proc numbers in force at one spot, already clamped. The server settings (zc_cloak_*) are the defaults everywhere;
        /// a zone or a tier Default re-tunes any of them with the Zone Control stats cloak_* (owner 2026-10-29: "Zone Control
        /// dials"). <see cref="Origin"/> / <see cref="Tier"/> say where the authored values came from; <see cref="Source"/> is
        /// that as text for /cloakproc status, built only when asked for, never on the hit path (ASCII).
        /// </summary>
        public readonly struct Dials
        {
            public double Chance { get; }
            public double Cooldown { get; }
            public int BreezeHeal { get; }
            public int DodgerHits { get; }
            public double DodgerWindow { get; }
            public double DodgerStaminaMult { get; }
            public double GraceSeconds { get; }
            public DialSource Origin { get; }
            public int Tier { get; }

            /// <summary>Where these values came from, as text (ASCII).</summary>
            public string Source => Origin switch
            {
                DialSource.Zone => "Zone Control - the zone you are in",
                DialSource.TierDefault => $"Zone Control - T{Tier} Default",
                _ => "server settings",
            };

            private Dials(double chance, double cooldown, int breezeHeal, int dodgerHits, double dodgerWindow, double dodgerStaminaMult,
                double graceSeconds, DialSource origin, int tier)
            {
                Chance = chance;
                Cooldown = cooldown;
                BreezeHeal = breezeHeal;
                DodgerHits = dodgerHits;
                DodgerWindow = dodgerWindow;
                DodgerStaminaMult = dodgerStaminaMult;
                GraceSeconds = graceSeconds;
                Origin = origin;
                Tier = tier;
            }

            /// <summary>
            /// The one place the numbers are held to their ranges - a server setting and a zone stat alike, since neither is
            /// checked when it is set. Chance 0..1; cooldown, windows and Grace 0..3600 s (new caps: a server setting was
            /// unbounded above before); heal 0..int.MaxValue; hits 0..1000; stamina per point 0.01..1000. Whole-number dials round
            /// half away from zero. A value that is not a number takes the setting's declared default (PropertyManager) - a zone
            /// value that is not a number never gets here (<see cref="DialsFor"/> keeps the server value for it).
            /// </summary>
            public static Dials Clamped(double chance, double cooldown, double breezeHeal, double dodgerHits, double dodgerWindow,
                double dodgerStaminaMult, double graceSeconds, DialSource origin = DialSource.Server, int tier = 0)
                => new Dials(
                    Clean(chance, 0.0, 1.0, ServerConfig.zc_cloak_proc_chance.Default),
                    Clean(cooldown, 0.0, 3600.0, ServerConfig.zc_cloak_proc_cooldown.Default),
                    (int)Math.Round(Clean(breezeHeal, 0.0, int.MaxValue, ServerConfig.zc_cloak_breeze_heal.Default), MidpointRounding.AwayFromZero),
                    (int)Math.Round(Clean(dodgerHits, 0.0, 1000.0, ServerConfig.zc_cloak_dodger_hits.Default), MidpointRounding.AwayFromZero),
                    Clean(dodgerWindow, 0.0, 3600.0, ServerConfig.zc_cloak_dodger_window.Default),
                    Clean(dodgerStaminaMult, 0.01, 1000.0, ServerConfig.zc_cloak_dodger_stamina_mult.Default),
                    Clean(graceSeconds, 0.0, 3600.0, ServerConfig.zc_cloak_grace_seconds.Default),
                    origin, tier);

            private static double Clean(double value, double min, double max, double fallback)
                => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
        }

        /// <summary>The server settings alone (zc_cloak_*), clamped - what every spot uses until Zone Control authors a dial.</summary>
        public static Dials ServerDials() => Dials.Clamped(
            ServerConfig.zc_cloak_proc_chance.Value,
            ServerConfig.zc_cloak_proc_cooldown.Value,
            ServerConfig.zc_cloak_breeze_heal.Value,
            ServerConfig.zc_cloak_dodger_hits.Value,
            ServerConfig.zc_cloak_dodger_window.Value,
            ServerConfig.zc_cloak_dodger_stamina_mult.Value,
            ServerConfig.zc_cloak_grace_seconds.Value);

        /// <summary>
        /// The dials where this player stands (owner 2026-10-29). Same rule as the charm level cap
        /// (CharmAbilityRegistry.LevelOf / ZoneCharmLevelCap): T11+ spots only - retail is never touched, whatever a zone below
        /// v11 authors - then the governing zone's Default row (its own stats over the tier Default), else on a spot no enabled
        /// zone covers the tier's own Default row (one dictionary read, no profile rebuild), else the server settings. Per dial,
        /// so a zone that sets only the heal keeps the server chance; an authored value that is not a number keeps the server
        /// value. Read where the WEARER stands, not through the attacker, so melee, spells, PvP and hits with no attacker all
        /// resolve alike. Players have no rank, so rank rows never apply. Zone Control off = the server settings. No allocation
        /// on the hit path.
        /// </summary>
        public static Dials DialsFor(Player player)
        {
            var server = ServerDials();
            if (player == null || !ServerConfig.zonecontrol_enabled.Value)
                return server;

            EvaluatedProfile zone;
            ZoneVariantProfile tierRow = null;
            int tier;
            try
            {
                tier = ZoneControlManager.GetEffectiveVariation(player);
                if (tier < VariationManager.EndgameMinVariation)
                    return server;

                zone = ZoneControlManager.ResolveZoneDefaultForPlayer(player);
                if (zone == null)
                    tierRow = ZoneControlManager.GetAnchoredDefaultProfile(tier);
            }
            catch (Exception ex)
            {
                // never let zone resolution break a damage path - the server settings stand, and the log says so (once a minute)
                WarnResolveFailed(player, ex);
                return server;
            }

            if (zone == null && tierRow == null)
                return server;

            var authored = false;
            var chance = Pick(zone, tierRow, ZoneStat.CloakProcChance, server.Chance, ref authored);
            var cooldown = Pick(zone, tierRow, ZoneStat.CloakProcCooldown, server.Cooldown, ref authored);
            var heal = Pick(zone, tierRow, ZoneStat.CloakBreezeHeal, server.BreezeHeal, ref authored);
            var hits = Pick(zone, tierRow, ZoneStat.CloakDodgerHits, server.DodgerHits, ref authored);
            var window = Pick(zone, tierRow, ZoneStat.CloakDodgerWindow, server.DodgerWindow, ref authored);
            var mult = Pick(zone, tierRow, ZoneStat.CloakDodgerStaminaMult, server.DodgerStaminaMult, ref authored);
            var grace = Pick(zone, tierRow, ZoneStat.CloakGraceSeconds, server.GraceSeconds, ref authored);
            if (!authored)
                return server;

            return Dials.Clamped(chance, cooldown, heal, hits, window, mult, grace,
                zone != null ? DialSource.Zone : DialSource.TierDefault, tier);
        }

        /// <summary>One dial: the zone's value when it has one, else the tier row's, else the server's. A value that is not a
        /// number counts as unset.</summary>
        private static double Pick(EvaluatedProfile zone, ZoneVariantProfile tierRow, string stat, double server, ref bool authored)
        {
            double value;
            if (zone != null)
            {
                if (!zone.Has(stat))
                    return server;
                value = zone.Get(stat, server);
            }
            else if (tierRow != null && tierRow.TryGet(stat, out var curve) && curve != null)
                value = curve.Base;
            else
                return server;

            if (!double.IsFinite(value))
                return server;
            authored = true;
            return value;
        }

        private static double nextResolveWarnAt;

        /// <summary>Logs a failed Zone Control lookup at most once a minute (it is on the hit path).</summary>
        private static void WarnResolveFailed(Player player, Exception ex)
        {
            var now = Time.GetUnixTime();
            if (now < nextResolveWarnAt)
                return;
            nextResolveWarnAt = now + 60.0;
            log.Warn($"[ZcCloakProcs] Zone Control lookup failed for {player?.Name}; using the server settings. {ex.GetType().Name}: {ex.Message}");
        }

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

        /// <summary>What the proc does, from the given dials (ASCII only - the client renders this).</summary>
        public static string DescriptionOf(ZcCloakProc proc, Dials dials) => proc switch
        {
            ZcCloakProc.CalmingBreeze => $"heals you for {dials.BreezeHeal:N0} health",
            ZcCloakProc.SmokeScreen => "negates all damage from that hit",
            ZcCloakProc.ArtfulDodger => $"your next {dials.DodgerHits} {(dials.DodgerHits == 1 ? "hit" : "hits")} within {dials.DodgerWindow:0.#}s strike stamina at {dials.DodgerStaminaMult:0.##}x instead of health",
            ZcCloakProc.AsheronsGrace => $"protects you from all damage for {dials.GraceSeconds:0.#}s",
            _ => null,
        };

        /// <summary>The cloak's line for the item panel's modifier block, or null for anything this system does not own.
        /// Built live, never baked into the item, so a settings change reads on every cloak at once. The numbers are the dials
        /// where <paramref name="holder"/> stands - the panel passes the wearer of a worn cloak, else the examiner, the same
        /// holder the armor zone lock is judged for - and the server settings when there is none. A spot whose chance is 0
        /// reads "(off here)", like the armor zone lock.</summary>
        public static string AppraisalLine(WorldObject cloak, bool off = false, Player holder = null)
        {
            var proc = Classify(cloak);
            if (proc == ZcCloakProc.None)
                return null;

            var dials = holder != null ? DialsFor(holder) : ServerDials();
            if (off || dials.Chance <= 0)
                return $"- {NameOf(proc)} (off here)";

            return $"- {NameOf(proc)}: {dials.Chance * 100:0.#}% when hit, {DescriptionOf(proc, dials)} ({dials.Cooldown:0.#}s cooldown)";
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
                    // stamina per point from the dials where the hit lands
                    TakeOnStamina(defender, source, ref normal, ref trueDamage, DialsFor(defender).DodgerStaminaMult);
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
            }

            // the chance and cooldown where the hit lands - resolved only now, for a T11+ cloak off cooldown
            var dials = DialsFor(defender);
            if (!defender.ZcCloakProcForceNext && ThreadSafeRandom.Next(0.0f, 1.0f) >= dials.Chance)
                return ZcCloakProc.None;
            defender.ZcCloakProcForceNext = false;
            defender.ZcCloakProcReadyAt = now + dials.Cooldown;

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
            if (proc == ZcCloakProc.None || defender == null || defender.IsDead || defender.Health.Current <= 0)
                return;

            // the dials where the wearer is now (a proc is rare, so this read is too)
            var dials = DialsFor(defender);

            switch (proc)
            {
                case ZcCloakProc.CalmingBreeze:
                {
                    // static: a straight health delta, never the heal path, so no Healing Boost, spell, rating or aug changes it
                    var healed = defender.UpdateVitalDelta(defender.Health, dials.BreezeHeal);
                    if (healed > 0)
                        defender.DamageHistory.OnHeal((uint)healed);
                    AnnounceWeave(defender, proc);
                    defender.EnqueueBroadcast(new GameMessageScript(defender.Guid, BreezeEffect));
                    defender.SendMessage($"Calming Breeze restores {Math.Max(0, healed):N0} points of your health.", ChatMessageType.Magic);
                    break;
                }

                case ZcCloakProc.AsheronsGrace:
                {
                    var seconds = dials.GraceSeconds;
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
                    var hits = dials.DodgerHits;
                    var window = dials.DodgerWindow;
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

        /// <summary>One Artful Dodger charge: the whole hit (True Damage included) comes off stamina at <paramref name="mult"/>
        /// stamina per point (the dials where the hit lands); the damage stamina cannot pay for stays on the hit, split between its
        /// two parts as before.</summary>
        private static void TakeOnStamina(Player defender, WorldObject source, ref float normal, ref float trueDamage, double mult)
        {
            // stamina pays zc_cloak_dodger_stamina_mult per point of damage (owner 2026-10-29: 10x - a 500 hit costs 5,000
            // stamina), so a long fight runs the pool dry; the damage stamina could not pay for goes to health at 1 for 1
            var total = normal + trueDamage;
            // rounded UP (review 2026-10-29): a small positive cost never becomes 0 and lets the hit through with stamina left
            var want = (int)Math.Min(int.MaxValue, Math.Ceiling((double)total * mult));
            var taken = want > 0 ? -defender.UpdateVitalDelta(defender.Stamina, -want) : 0;
            if (taken < 0) taken = 0;

            var covered = (float)Math.Min(total, taken / mult);
            var left = Math.Max(0f, total - covered);
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
            defender.SendMessage($"Artful Dodger turns {Math.Round(covered):N0} points of damage{FromWhom(source)} into {taken:N0} stamina loss.{through}{remaining}", ChatMessageType.Magic);
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
