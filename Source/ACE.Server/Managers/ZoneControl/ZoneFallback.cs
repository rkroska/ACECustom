using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// The FALLBACK set: what a zone-mutated item resolves to when <c>zonecontrol_enabled</c> is OFF
    /// (owner 2026-08-23). These are NOT "defaults" in the Zone Control sense - a Default is the
    /// per-tier authored layer (Default 11 and up) that carries the T11-T25 LADDER. This is the other
    /// thing entirely: the numbers gear reads when Zone Control is not authoring it at all.
    ///
    /// Every value here is T10 MAX-ROLLED gear, measured from the shard 2026-08-21 (Drexel / Nerd
    /// Parade, the best-geared T10 characters - memory ref-t10-best-geared-baseline):
    ///   armour  732 = their best single armour piece (their 12-piece sets totalled 5,969 / 6,482)
    ///   DR       92 = worn GearDamageResist total
    ///   CDR      73 = worn GearCritDamageResist total (also used for Crit Resist / Nether Resist)
    ///   line    211 = worn GearDamage total, the highest of the per-line T10 totals we cap under
    ///                 (Crit Damage was 201, Healing Boost 355 - owner picked the Damage figure)
    ///
    /// Switching the bool off re-resolves live items on their next equip / login (Live Stat Resolution
    /// follows the ladder both directions), so this is reversible and touches nothing in the store.
    /// </summary>
    public static class ZoneFallback
    {
        /// <summary>Flat armour level per piece - no tier ladder off the switch.</summary>
        public const int ArmorLevel = 732;

        /// <summary>Core-four worn-set anchors (the CoreWindow divisor is still 18 pieces).</summary>
        public const double AnchorDr = 92.0, AnchorCdr = 73.0;

        /// <summary>Worn-gear hard caps, matching the anchors so a full set lands on the T10 total.</summary>
        public const int CapDr = 92, CapCdr = 73, CapLine = 211;

        /// <summary>
        /// The worn total the LADDER's T11 line band produces (18 pieces x the 69 max of a 14-69 band),
        /// i.e. the 1250-class anchor. Dividing CapLine by it gives the shrink factor below.
        /// </summary>
        private const double LadderLineWorn = 1250.0;

        /// <summary>
        /// Catalog band shrunk for the fallback: a maxed 18-piece set should land on CapLine by ADDITION,
        /// not by slamming into the clamp (owner 2026-08-23 - otherwise every piece advertises +69 while
        /// the set delivers 211). Scale = 211/1250, so the 14-69 class becomes 2-12 and the Armor Level
        /// line's 50-250 becomes 8-42. PINNED lines (Crit Chance, Max Health Pct, Life on Hit, Reinforced -
        /// the 1-3 bands) do not scale on the ladder either, so they pass through unchanged.
        /// </summary>
        public static (int Min, int Max) Band(ZoneModifiers.Def def)
        {
            if (def == null)
                return (0, 0);
            var (min, max) = def.Min <= def.Max ? (def.Min, def.Max) : (def.Max, def.Min);
            if (!def.TierScaled)
                return (min, max);
            var scale = CapLine / LadderLineWorn;
            return (System.Math.Max(1, (int)System.Math.Round(min * scale)),
                    System.Math.Max(1, (int)System.Math.Round(max * scale)));
        }

        /// <summary>The fallback worn cap for one of the three gear_cap_* stats.</summary>
        public static int GearCap(string capStat) =>
            capStat == ZoneStat.GearCapDr ? CapDr
          : capStat == ZoneStat.GearCapCdr ? CapCdr
          : CapLine;
    }
}
