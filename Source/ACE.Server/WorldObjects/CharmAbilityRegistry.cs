using System;
using System.Collections.Generic;

namespace ACE.Server.WorldObjects
{
    public static class CharmAbilityRegistry
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly struct AbilityEntry
        {
            public readonly Func<Player, bool> Get;
            public readonly Action<Player, bool> Set;
            public readonly string DisplayName;

            public AbilityEntry(Func<Player, bool> get, Action<Player, bool> set, string displayName)
            {
                Get = get;
                Set = set;
                DisplayName = displayName;
            }
        }

        // ── Ability ID constants — use these instead of raw ints ──────────────
        public const int ManaBarrierAbilityId     = 1;
        public const int InfiniteCastingAbilityId = 16;
        public const int AsheronsFavorAbilityId   = 17;
        public const int ArtisansCharmAbilityId   = 18;
        public const int ShrapnelCharmAbilityId   = 19;
        public const int AgonyCharmAbilityId      = 20;
        public const int ExplosiveArrowCharmAbilityId = 21;
        public const int SplitCastAbilityId       = 22;
        public const int OmnistrikeAbilityId       = 23;
        /// <summary>Opt-in: pay pyreals to restore one summoning essence charge when empty (PetDevice summon path).</summary>
        public const int PetDevicePyrealAutoRefillAbilityId = 24;
        /// <summary>Bypass pet device SummoningMastery checks (Primalist / Necromancer / Naturalist essences).</summary>
        public const int UniversalSummoningMasteryAbilityId = 25;
        public const int AutoRebuffAbilityId      = 26;
        public const int ForkAbilityId             = 27;
        // 28 (Far Shot) and 29 (Mule's Tinkering) are taken by weenies / another branch - the levelled charms start at 30.
        /// <summary>Levelled charm (1-100): a T11+ launcher fires extra arrows.</summary>
        public const int SplitArrowAbilityId       = 30;
        /// <summary>Levelled charm (1-100): hand-cast war arcs / bolts / streaks jump from monster to monster.</summary>
        public const int ChainAbilityId            = 31;
        /// <summary>Levelled charm (1-100): a ticking void DoT spreads to nearby monsters.</summary>
        public const int PandemicAbilityId         = 32;
        /// <summary>Levelled charm (1-100): melee cleave reaches further, all around, more targets.</summary>
        public const int ImprovedCleaveAbilityId   = 33;
        /// <summary>Levelled charm (1-100, 2026-10-21): a dual wielder's hits ignore a share of the armor left.</summary>
        public const int ArmorBreakerAbilityId     = 34;
        /// <summary>Levelled charm (1-100, 2026-10-21): the shield guards a wider arc against monster attacks.</summary>
        public const int ShieldArcAbilityId        = 35;

        private static readonly Dictionary<int, AbilityEntry> Registry = new()
        {
            // Format: { abilityId, (getter, setter, "Display Name") }
            { ManaBarrierAbilityId,     new AbilityEntry(p => p.HasManaBarrier,     (p, v) => p.HasManaBarrier     = v, "Mana Barrier")     },
            { InfiniteCastingAbilityId, new AbilityEntry(p => p.HasInfiniteCasting, (p, v) => p.HasInfiniteCasting = v, "Infinite Casting") },
            { AsheronsFavorAbilityId,   new AbilityEntry(p => p.HasAsheronsFavor,   (p, v) => p.HasAsheronsFavor   = v, "Asheron's Favor")  },
            { ArtisansCharmAbilityId,   new AbilityEntry(p => p.HasArtisanCharm,    (p, v) => p.HasArtisanCharm    = v, "Artisan's Charm")  },
            { ShrapnelCharmAbilityId,   new AbilityEntry(p => p.HasShrapnelCharm,   (p, v) => p.HasShrapnelCharm   = v, "Shrapnel Charm")   },
            { AgonyCharmAbilityId,       new AbilityEntry(p => p.HasAgonyCharm,      (p, v) => p.HasAgonyCharm      = v, "Agony Charm")      },
            { ExplosiveArrowCharmAbilityId, new AbilityEntry(p => p.HasExplosiveArrowCharm, (p, v) => p.HasExplosiveArrowCharm = v, "Explosive Arrow Charm") },
            { SplitCastAbilityId,       new AbilityEntry(p => p.HasSplitCast,       (p, v) => p.HasSplitCast       = v, "Split Cast")       },
            { OmnistrikeAbilityId,       new AbilityEntry(p => p.HasOmnistrike, (p, v) => p.HasOmnistrike = v, "Omni Strike")       },
            { PetDevicePyrealAutoRefillAbilityId, new AbilityEntry(p => p.PetDevicePyrealAutoRefillEnrolled, (p, v) => p.PetDevicePyrealAutoRefillEnrolled = v, "Summon Essence Refill") },
            { UniversalSummoningMasteryAbilityId, new AbilityEntry(p => p.HasUniversalSummoningMastery, (p, v) => p.HasUniversalSummoningMastery = v, "Universal Summoning Mastery") },
            { AutoRebuffAbilityId,      new AbilityEntry(p => p.HasAutoRebuffCharm,     (p, v) => p.HasAutoRebuffCharm = v,     "Auto-Rebuff")          },
            { ForkAbilityId,            new AbilityEntry(p => p.HasForkCharm,           (p, v) => p.HasForkCharm           = v, "Fork")                 },
            { SplitArrowAbilityId,      new AbilityEntry(p => p.HasSplitArrowCharm,     (p, v) => p.HasSplitArrowCharm     = v, "Split Arrow")          },
            { ChainAbilityId,           new AbilityEntry(p => p.HasChainCharm,          (p, v) => p.HasChainCharm          = v, "Chain")                },
            { PandemicAbilityId,        new AbilityEntry(p => p.HasPandemicCharm,       (p, v) => p.HasPandemicCharm       = v, "Pandemic")             },
            { ImprovedCleaveAbilityId,  new AbilityEntry(p => p.HasImprovedCleaveCharm, (p, v) => p.HasImprovedCleaveCharm = v, "Improved Cleave")      },
            { ArmorBreakerAbilityId,    new AbilityEntry(p => p.HasArmorBreakerCharm,   (p, v) => p.HasArmorBreakerCharm   = v, "Armor Breaker")        },
            { ShieldArcAbilityId,       new AbilityEntry(p => p.HasShieldArcCharm,      (p, v) => p.HasShieldArcCharm      = v, "Shield Arc")           },
        };

        /// <summary>The charm level (1-100) the player's active charm of this ability has, 0 when it is not active.</summary>
        public static int LevelOf(Player player, int abilityId)
        {
            if (player == null || !IsActive(player, abilityId)) return 0;
            var level = player.ActiveCharmLevels.TryGetValue(abilityId, out var own) && own > 0 ? own : 1;
            if (CharmSettingsManager.LeveledBlockFor(abilityId) == null) return level;
            // a levelled charm works only in T11+ and at no more than the tier's cap where the player stands (owner 2026-10-25)
            var tier = ACE.Server.Managers.ZoneControl.ZoneControlManager.GetEffectiveVariation(player);
            return EffectiveLeveledLevel(level, tier, tier >= MinCharmTier ? ZoneCharmLevelCap(player, tier) : null);
        }

        /// <summary>Levelled charms work in T11+ only (owner 2026-10-25): below it they do nothing at all.</summary>
        public const int MinCharmTier = ACE.Server.Managers.VariationManager.EndgameMinVariation;

        /// <summary>The level a levelled charm works at: 0 below T11, else its own level held to the cap.</summary>
        public static int EffectiveLeveledLevel(int level, int tier, int? cap) =>
            tier < MinCharmTier ? 0 : CapLevel(level, cap);

        /// <summary>The levelled-charm cap where this player stands, null when there is none. The governing zone (its own
        /// row over the tier Default) when one covers the spot; on a T11+ spot no enabled zone covers (just outside a zone,
        /// a disabled zone) the tier's own Default row, so stepping off a zone never lifts the cap (Fable review 2026-10-25).</summary>
        public static int? ZoneCharmLevelCap(Player player, int tier)
        {
            if (player == null) return null;
            var zp = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveZoneDefaultForPlayer(player);
            if (zp != null)
                return zp.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.CharmLevelCap)
                    ? CapFromSetting(zp.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.CharmLevelCap)) : null;
            // a single dictionary read of the saved tier row (no profile rebuild on the combat path)
            var tierRow = ACE.Server.Managers.ZoneControl.ZoneControlManager.GetAnchoredDefaultProfile(tier);
            return tierRow?.Stats != null && tierRow.Stats.TryGetValue(ACE.Server.Managers.ZoneScaling.ZoneStat.CharmLevelCap, out var curve) && curve != null
                ? CapFromSetting(curve.Base) : null;
        }

        /// <summary>A charm_level_cap setting as a cap: below 1 = 1 (0 never means "no cap"), 100 or more = none (the
        /// highest level), not a number = none; fractions round down.</summary>
        public static int? CapFromSetting(double value)
        {
            if (!double.IsFinite(value) || value >= CharmSettingsManager.LeveledCharmBlock.MaxLevel) return null;
            return value < 1 ? 1 : (int)Math.Floor(value);
        }

        /// <summary>A charm level held to a cap (null = none); never below 1, never above the charm's own level.</summary>
        public static int CapLevel(int level, int? cap) =>
            cap.HasValue ? Math.Max(1, Math.Min(level, cap.Value)) : level;

        /// <summary>Returns all registered ability IDs. Prefer this over hardcoded numeric ranges.</summary>
        public static IEnumerable<int> RegisteredIds => Registry.Keys;

        private static readonly Dictionary<uint, int> WCIDToAbilityId = new()
        {
            // Mana Barrier
            { 777700001, ManaBarrierAbilityId }, { 777700054, ManaBarrierAbilityId }, { 777710004, ManaBarrierAbilityId }, { 777720004, ManaBarrierAbilityId },
            // Infinite Casting Stone
            { 777700019, InfiniteCastingAbilityId }, { 777700055, InfiniteCastingAbilityId },
            // Asheron's Favor (Tiers 1–3)
            { 777700020, AsheronsFavorAbilityId }, { 777710002, AsheronsFavorAbilityId }, { 777720002, AsheronsFavorAbilityId },
            // Artisan's Charm (Tiers 1–3)
            { 777700021, ArtisansCharmAbilityId }, { 777710003, ArtisansCharmAbilityId }, { 777720003, ArtisansCharmAbilityId },
            // Shrapnel Charm (Tier 1 only)
            { 777700022, ShrapnelCharmAbilityId },
            // Agony Charm (Tier 1 only)
            { 777700023, AgonyCharmAbilityId },
            // Explosive Arrow Charm (Tiers 1–3)
            { 777700025, ExplosiveArrowCharmAbilityId }, { 777710005, ExplosiveArrowCharmAbilityId }, { 777720005, ExplosiveArrowCharmAbilityId },
            // Split Cast Charm (Tier 1 only)
            { 777700024, SplitCastAbilityId },
            // Omni Strike Charm
            { 777700026, OmnistrikeAbilityId },
            // Summon Essence Refill Charm (pyreal auto-refill enrollment)
            { 78780030, PetDevicePyrealAutoRefillAbilityId },
            { 78780031, UniversalSummoningMasteryAbilityId },
            // Auto-Rebuff Charm
            { 777700300, AutoRebuffAbilityId },
            // Fork Charm (Tiers 1–3)
            { 777700027, ForkAbilityId }, { 777710007, ForkAbilityId }, { 777720007, ForkAbilityId },
            // Levelled charms (one item each, level 1-100)
            { 777700035, SplitArrowAbilityId },
            { 777700036, ChainAbilityId },
            { 777700037, PandemicAbilityId },
            { 777700038, ImprovedCleaveAbilityId },
            { 777700039, ArmorBreakerAbilityId },
            { 777700040, ShieldArcAbilityId },
        };

        /// <summary>
        /// Returns the ability ID for a given WCID, or null if it's not a charm.
        /// </summary>
        public static int? GetAbilityIdForWCID(uint wcid)
        {
            return WCIDToAbilityId.TryGetValue(wcid, out var abilityId) ? abilityId : null;
        }

        /// <summary>
        /// Apply or remove an ability on the player for the given ability ID.
        /// </summary>
        public static void Apply(Player player, int abilityId, bool enable, int level = 1)
        {
            if (!Registry.TryGetValue(abilityId, out var entry))
            {
                log.Warn($"[CharmAbilityRegistry] Apply called for unregistered abilityId={abilityId} — Has* property not updated.");
                return;
            }

            entry.Set(player, enable);

            if (enable)
                player.ActiveCharmLevels[abilityId] = level;
            else
                player.ActiveCharmLevels.Remove(abilityId);
        }


        /// <summary>
        /// Returns the display name for an ability ID, or null if not found.
        /// </summary>
        public static string GetDisplayName(int abilityId)
        {
            return Registry.TryGetValue(abilityId, out var entry) ? entry.DisplayName : null;
        }

        /// <summary>
        /// Returns true if the player currently has this ability active.
        /// </summary>
        public static bool IsActive(Player player, int abilityId)
        {
            return Registry.TryGetValue(abilityId, out var entry) && entry.Get(player);
        }

        /// <summary>
        /// Returns a list of display names for all currently active charms on the player.
        /// </summary>
        public static List<string> GetActiveDisplayNames(Player player)
        {
            var active = new List<string>();
            foreach (var entry in Registry.Values)
            {
                if (entry.Get(player))
                    active.Add(entry.DisplayName);
            }
            return active;
        }
    }
}
