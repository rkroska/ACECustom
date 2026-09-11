using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers.ZoneScaling;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// Live stat resolution (owner 2026-08-22, Plan_LiveStatResolution_2026-08-22.md): a T11+ piece carries
    /// a GRADE per Zone Control line (0-1000 = where in the band it rolled) plus the loot tier; the numbers
    /// retail code reads (Gear* ratings, Armor Level, the 502xx cantrip props) are a CACHE resolved from the
    /// grades against the LIVE ladder. Weapon scaling already works this way (quality 0-1000); this is the
    /// same model for armor / jewelry lines, the core four, Armor Level and the slot specials.
    ///
    /// Record format, PropertyString.ZcModifiers: "28:490;19:1000;c1:900;c2:850;c3:900;c4:880;25:500;41:650"
    ///   - positive key  = ZoneModifiers catalog key (lines AND specials), value = grade 0-1000
    ///   - c1..c4        = the core four (DamageResist / CritDamageResist / CritResist / NetherResist)
    ///   - -11..-30      = the reserved WEAPON block (2026-08-25): the six continuous weapon cards,
    ///                     which are PropertyFloats and resolve through ZoneModifiers.WeaponBand rather
    ///                     than the armour catalog. See the block comment at WeaponBiteKey.
    ///   - Reinforced (49) is NOT recorded: an earned, frozen armor mod (owner ruling 2026-08-22 §8.3)
    /// PropertyInt.ZcTier = the ladder row; PropertyInt.ZcResolvedVersion = the per-tier ladder version the
    /// cache was last written against (ZoneControlManager.GetLadderVersion).
    ///
    /// Three entry points:
    ///   <see cref="Compute"/>      - PURE: grades -> resolved values. Appraisal uses this (read-only rule §3b).
    ///   <see cref="ApplyIfStale"/> - EQUIP ONLY: re-stamp the cache when the tier's ladder version moved.
    ///   <see cref="Apply"/>        - the unconditional stamp used at DROP time by the producers.
    /// Nothing here walks biotas; items catch up lazily when worn (plan §8.2: lazy only).
    /// </summary>
    public static class ZoneStatResolver
    {
        public const int GradeMax = 1000;

        /// <summary>Core-four pseudo keys inside the record (negative so they never collide with catalog keys).</summary>
        public const int CoreDamageResist = -1;
        public const int CoreCritDamageResist = -2;
        public const int CoreCritResist = -3;
        public const int CoreNetherResist = -4;

        public static readonly int[] CoreKeys = { CoreDamageResist, CoreCritDamageResist, CoreCritResist, CoreNetherResist };

        public static PropertyInt CoreProp(int coreKey) => coreKey switch
        {
            CoreDamageResist => PropertyInt.GearDamageResist,
            CoreCritDamageResist => PropertyInt.GearCritDamageResist,
            CoreCritResist => PropertyInt.GearCritResist,
            _ => PropertyInt.GearNetherResist,
        };

        public static string CoreName(int coreKey) => coreKey switch
        {
            CoreDamageResist => "Damage Resist",
            CoreCritDamageResist => "Crit Damage Resist",
            CoreCritResist => "Crit Resist",
            _ => "Nether Resist",
        };

        public static bool IsCoreKey(int key) => key <= CoreDamageResist && key >= CoreNetherResist;

        // ── weapon-special pseudo keys (2026-08-25, weapon/armour parity) ────────
        //
        // WHY A RESERVED NEGATIVE BLOCK AND NOT SIX MORE ZoneModifiers.Catalog ROWS
        //
        // Catalog keys are load-bearing forever: they live in saved zone pools
        // (ZoneVariantProfile.CustomModifiers), in the `cantrip <scope> band <key>` command surface,
        // and in the plugin's mirrored ModifierCatalog. Minting six of them for weapons would leak
        // weapon keys into the ARMOUR line pool (TryExtraModifier walks the catalog), into the plugin's
        // Cantrips tab, and into the [[ZC]] wire - for six values that never travel the wire at all.
        // That crossing is the thing this design deliberately avoided when the WeaponBand table was
        // added (see the long comment above ZoneModifiers.WeaponBand). So: a reserved key block that
        // lives ONLY in the item's own ZcModifiers record, exactly the way the core four do.
        //
        // WHY NEGATIVE. The record's key token is parsed by Read(): 'cN' is a core key, anything else
        // goes through int.TryParse with NumberStyles.Integer, which accepts a leading '-'. So "-16:640"
        // round-trips through Read/Format with ZERO format work, and a negative key can never collide
        // with a catalog key (all positive) or with a core key (-1..-4, and those are written as 'cN'
        // by Format, so they never appear as a bare negative token in a well-formed record).
        //
        // -5..-10 IS A DELIBERATE GAP so the core block can grow (a fifth core rating) without
        // renumbering weapons. -11..-30 is the WEAPON block; only -11..-16 are defined today.
        // NOTE for anyone touching the record format: a bare '-' in a ZcModifiers string means
        // "this record contains a weapon key" and NOTHING else - grades are 0..1000 and never
        // signed. HasWeaponKey below depends on that; keep it true.
        public const int WeaponBiteKey = -11;
        public const int WeaponArmorRendKey = -12;
        public const int WeaponRendPowerKey = -13;
        public const int WeaponSlayerKey = -14;
        public const int WeaponShieldCleaveKey = -15;
        public const int WeaponCrushKey = -16;
        /// <summary>Cast on Strike damage B, one key per slot (added 2026-08-27).</summary>
        public const int WeaponProcArcDamageKey = -17;
        public const int WeaponProcRingDamageKey = -18;

        /// <summary>The reserved weapon block, -11..-30. Membership of the block, NOT "is defined":
        /// an unknown key inside the block is skipped by Compute exactly like an unknown catalog key,
        /// so an item stamped by a newer build never throws on an older one.</summary>
        public static bool IsWeaponKey(int key) => key <= -11 && key >= -30;

        /// <summary>
        /// One continuous weapon card as the RECORD sees it: the ladder row it resolves against
        /// (<see cref="ZoneModifiers.WeaponBand"/>, which owns the T11-&gt;T25 anchors, the authored
        /// stat names and the hard clamp) plus the PropertyFloat the engine actually reads.
        ///
        /// <see cref="DisplayIsMultiplier"/> is the Crushing Blow trap, isolated to one bool: the
        /// band and the plugin box are in DISPLAY space (7.50 = "crits hit for 7.5x") but the engine
        /// computes <c>1 + CriticalMultiplier</c>, so the stored number is display - 1. See
        /// <see cref="EngineValue"/> - that method is the ONLY place in the server that subtracts it.
        /// </summary>
        public sealed class WeaponSpecial
        {
            public int Key;
            public ZoneModifiers.WeaponBand Band;
            public PropertyFloat Prop;
            public bool DisplayIsMultiplier;
            public string Name => Band?.Name ?? "?";
        }

        // The six rows. NAMED, and the drop-time write sites reference them BY NAME - never by an index
        // into the array below. An index would silently re-point a card at the wrong property the day
        // someone reorders the table, and the only symptom would be Slayer weapons that ignore shields.
        public static readonly WeaponSpecial SpecBite = new WeaponSpecial
        { Key = WeaponBiteKey, Band = ZoneModifiers.WeaponBite, Prop = PropertyFloat.CriticalFrequency };

        public static readonly WeaponSpecial SpecArmorRend = new WeaponSpecial
        { Key = WeaponArmorRendKey, Band = ZoneModifiers.WeaponArmorRend, Prop = (PropertyFloat)ZoneLootMutator.ArmorRendOverridePropId };

        public static readonly WeaponSpecial SpecRendPower = new WeaponSpecial
        { Key = WeaponRendPowerKey, Band = ZoneModifiers.WeaponRendPower, Prop = (PropertyFloat)ZoneLootMutator.RendingModOverridePropId };

        public static readonly WeaponSpecial SpecSlayer = new WeaponSpecial
        { Key = WeaponSlayerKey, Band = ZoneModifiers.WeaponSlayer, Prop = PropertyFloat.SlayerDamageBonus };

        public static readonly WeaponSpecial SpecShieldCleave = new WeaponSpecial
        { Key = WeaponShieldCleaveKey, Band = ZoneModifiers.WeaponShieldCleave, Prop = PropertyFloat.IgnoreShield };

        /// <summary>The ONLY row with DisplayIsMultiplier - see <see cref="EngineValue"/>.</summary>
        public static readonly WeaponSpecial SpecCrush = new WeaponSpecial
        { Key = WeaponCrushKey, Band = ZoneModifiers.WeaponCrush, Prop = PropertyFloat.CriticalMultiplier, DisplayIsMultiplier = true };

        /// <summary>The weapon block, in record-key order - for lookup and for the stable fold order
        /// <see cref="WeaponPinFingerprint"/> depends on.</summary>
        /// <summary>Cast on Strike. Unlike the other six this property is not read by the melee damage
        /// pipeline at all - SpellProjectile.CalculateDamage substitutes it for the rolled spell base.</summary>
        public static readonly WeaponSpecial SpecProcArcDamage = new WeaponSpecial
        { Key = WeaponProcArcDamageKey, Band = ZoneModifiers.WeaponProcArcDamage, Prop = (PropertyFloat)ZoneLootMutator.ProcArcDamagePropId };

        public static readonly WeaponSpecial SpecProcRingDamage = new WeaponSpecial
        { Key = WeaponProcRingDamageKey, Band = ZoneModifiers.WeaponProcRingDamage, Prop = (PropertyFloat)ZoneLootMutator.ProcRingDamagePropId };

        public static readonly WeaponSpecial[] WeaponSpecials =
        {
            SpecBite, SpecArmorRend, SpecRendPower, SpecSlayer, SpecShieldCleave, SpecCrush,
            SpecProcArcDamage, SpecProcRingDamage,
        };

        public static bool TryGetWeapon(int key, out WeaponSpecial ws)
        {
            foreach (var w in WeaponSpecials)
                if (w.Key == key) { ws = w; return true; }
            ws = null;
            return false;
        }

        /// <summary>
        /// 🔴 THE ONLY "- 1.0" IN THE WEAPON PATH. Display space -&gt; the number the engine stores.
        ///
        /// Crushing Blow's card value IS the final crit damage multiplier (7.50 = crits deal 7.5x).
        /// WorldObject_Weapon.GetWeaponCritDamageMod reads PropertyFloat.CriticalMultiplier and the
        /// damage pipeline computes 1 + that, so the stored number must be display - 1. Retail's
        /// absent-property default is 1.0, i.e. the familiar 2x crit - which is why 0 is NOT the
        /// inert value for this property and why a naive "zero it out" is wrong (see WeaponResolveBand).
        ///
        /// WHY IT LIVES HERE AND NOWHERE ELSE. Before this pass the subtraction sat inline at the one
        /// drop-time write site in ZoneLootMutator. Now the number is produced from a recorded grade,
        /// and produced AGAIN on every equip re-stamp (ApplyIfStale -&gt; Compute -&gt; Apply). If the
        /// subtraction were duplicated at the drop site as well, a 7.5x weapon would be stamped 6.5 at
        /// drop, then re-resolved to 5.5 on the first login, 4.5 on the next ladder apply, and so on -
        /// walking silently down forever with nothing in any log. So: DROP and RESOLVE both call this
        /// method, this method is the only subtraction, and neither caller may pre-convert.
        /// If you add a second display-space card, add a row with DisplayIsMultiplier - do not inline.
        /// </summary>
        public static double EngineValue(WeaponSpecial ws, double display)
            => ws != null && ws.DisplayIsMultiplier ? display - 1.0 : display;

        // ── grade math ──────────────────────────────────────────────────────────

        /// <summary>Grade -> value inside an inclusive band. Linear, rounded once.</summary>
        public static int ValueFor(int min, int max, int grade)
        {
            if (min > max) (min, max) = (max, min);
            grade = Math.Clamp(grade, 0, GradeMax);
            return min + (int)Math.Round((max - min) * (grade / (double)GradeMax));
        }

        /// <summary>
        /// Grade -> value inside an inclusive DOUBLE band - the weapon-card sibling of
        /// <see cref="ValueFor"/>. Deliberately NOT "call ValueFor and divide": every weapon card is a
        /// PropertyFloat whose second decimal is the whole design (0.62 armour rend, 1.85x crush,
        /// 0.58 crit chance). Routing those through the int overload rounds 0.62 to 1 and every
        /// Biting Strike weapon on the shard crits on every swing, with nothing in any log to say why.
        /// </summary>
        public static double ValueForD(double min, double max, int grade)
        {
            if (min > max) (min, max) = (max, min);
            grade = Math.Clamp(grade, 0, GradeMax);
            return min + (max - min) * (grade / (double)GradeMax);
        }

        /// <summary>Value -> grade (migration of pre-grade items). Flat band = 1000.</summary>
        public static int GradeFor(int min, int max, int value)
        {
            if (min > max) (min, max) = (max, min);
            if (max == min) return GradeMax;
            return Math.Clamp((int)Math.Round((value - min) * (double)GradeMax / (max - min)), 0, GradeMax);
        }

        /// <summary>
        /// Roll a grade 0-1000 with the tier-weighted third (ZoneModifiers.TierThirds, Option A: T11 uniform,
        /// T25 10/30/60). forceMax = 1000. The producers roll THIS and derive the value with
        /// <see cref="ValueFor"/>, so the grade is the truth and the value its projection.
        /// </summary>
        public static int RollGrade(int tier, bool forceMax = false)
        {
            if (forceMax) return GradeMax;
            var (wLo, wMid, wHi) = ZoneModifiers.TierThirds(tier);
            var pick = ThreadSafeRandom.Next(0, wLo + wMid + wHi - 1);
            if (pick < wLo) return ThreadSafeRandom.Next(0, 333);
            if (pick < wLo + wMid) return ThreadSafeRandom.Next(334, 666);
            return ThreadSafeRandom.Next(667, GradeMax);
        }

        // ── the live ladder ─────────────────────────────────────────────────────

        /// <summary>
        /// The band a catalog key resolves against at a tier: the tier's ANCHORED Default-layer override
        /// (CustomModifierBands from GetAnchoredDefaultProfile - the v11 anchor board under the tier's own
        /// Default, which is what real drops there roll from) when authored, else the catalog band scaled to
        /// the tier (ZoneModifiers.CatalogBandAt). Reading the RAW Default here was the 2x-at-T25 bug: no
        /// Default is authored above v11, so every tier 12-25 took the CatalogBandAt branch at resolve while
        /// drops took the authored band. A ZONE's own band override is a drop-time concern only - the piece does not remember
        /// its zone, and re-resolution only happens after an explicit ladder apply, when the tier's Default
        /// IS the published truth.
        /// </summary>
        public static (int Min, int Max) EffectiveBand(int key, int tier)
        {
            if (!ZoneModifiers.TryGet(key, out var def))
                return (0, 0);
            // Zone Control off: the shrunk fallback band, and nothing authored is consulted (same rule
            // as CoreWindow). owner 2026-08-23.
            if (!ServerConfig.zonecontrol_enabled.Value)
                return ZoneFallback.Band(def);
            var anchored = ZoneControlManager.GetAnchoredDefaultProfile(tier);
            if (anchored?.CustomModifierBands != null
                && anchored.CustomModifierBands.TryGetValue(key, out var live)
                && live != null && live.Max > 0)
                return live.Min <= live.Max ? (live.Min, live.Max) : (live.Max, live.Min);
            return ZoneModifiers.CatalogBandAt(def, tier);
        }

        /// <summary>
        /// The band a WEAPON card resolves against at a tier - the exact twin of <see cref="EffectiveBand"/>,
        /// and it must be read the same way:
        ///
        ///   1. Zone Control OFF -> the card's own T11 rung, TIER-BLIND, and nothing authored is consulted.
        ///      Same rule as EffectiveBand / CoreWindow / BaseArmorLevel: off the switch nothing climbs
        ///      with tier and no authored layer is read (owner 2026-08-23).
        ///      🔴 NOTE this is deliberately NOT the armour SLOT-SPECIAL treatment (Compute zeroes those
        ///      when the switch is off). Zeroing works for a slot special because its props are additive
        ///      ZC-only ratings where 0 IS the inert value. Weapon cards write RETAIL properties where 0
        ///      is not inert but CATASTROPHIC: SlayerDamageBonus 0 makes the weapon deal literally zero
        ///      damage to its slayer type (WorldObject_Weapon.GetWeaponCreatureSlayerModifier returns the
        ///      property verbatim once the creature type matches), and CriticalFrequency 0 means the
        ///      weapon can never crit at all. The bottom rung of the card's own ladder is the honest
        ///      "Zone Control is not tuning this" answer, and it is still inside the card's Lo/Hi clamp,
        ///      so it can never produce a number the authored path could not.
        ///   2. The TIER DEFAULT's authored weapon_&lt;card&gt;_min / _max -> that band, including the
        ///      "one box = EXACT value, not a range" rule the drop path has always had.
        ///   3. Otherwise the ladder: ZoneModifiers.WeaponBandAt(band, tier).
        ///
        /// Step 2 reads the TIER DEFAULT, never a zone, for the same reason EffectiveBand does: the piece
        /// does not remember where it dropped, and a re-resolve only ever happens after an explicit ladder
        /// apply, when the tier Default IS the published truth. A ZONE-level weapon pin therefore shapes
        /// NEW DROPS IN THAT ZONE ONLY and existing weapons drift to the tier Default on their next equip -
        /// which is precisely what a zone-level cantrip band already does to armour, and what the
        /// `cantrip &lt;zone&gt; band` command already prints when you author one.
        ///
        /// <paramref name="stats"/> is the tier Default's Stats dictionary, passed in so a caller
        /// resolving several cards on one weapon pays for ONE locked snapshot read instead of twelve.
        /// Pass null to have it fetched here.
        /// </summary>
        public static (double Min, double Max) WeaponResolveBand(WeaponSpecial ws, int tier,
            Dictionary<string, ZoneScaling.StatCurve> stats = null, bool statsFetched = false)
        {
            if (ws?.Band == null)
                return (0.0, 0.0);
            var b = ws.Band;
            if (!ServerConfig.zonecontrol_enabled.Value)
                return ZoneModifiers.WeaponBandAt(b, 11);
            if (!statsFetched)
                stats = ZoneControlManager.GetAnchoredDefaultProfile(tier)?.Stats;
            var pin = PinBand(stats, b, tier);
            if (pin.HasValue)
                return Clamp(pin.Value.Min, pin.Value.Max, b);
            return ZoneModifiers.WeaponBandAt(b, tier);
        }

        /// <summary>
        /// The band a WEAPON card ROLLS from at drop time: the zone's evaluated profile (which already
        /// has the tier Default merged into it at snapshot-build time) when it authors either box, else
        /// the ladder. The twin of the (ModifierBands ?? CatalogBandAt) pair in
        /// ZoneLootMutator.TryExtraModifier - drop reads the ZONE, resolve reads the tier DEFAULT.
        /// Identical when the zone adds no pin of its own, which is the normal case.
        /// </summary>
        public static (double Min, double Max) WeaponDropBand(ZoneScaling.EvaluatedProfile p, WeaponSpecial ws, int tier)
        {
            if (ws?.Band == null)
                return (0.0, 0.0);
            var b = ws.Band;
            if (p != null && (p.Has(b.MinStat) || p.Has(b.MaxStat)))
            {
                // The historical RollRange semantics, preserved EXACTLY per anchor: one box authored =
                // that exact value (min == max), both authored = the range, reversed bounds swap.
                // ANCHORED since 2026-08-29: the pair above is the T11 anchor; a "_t25" pair beside it
                // is the T25 anchor and tiers between sit on the line. No _t25 pair = FLAT at every
                // tier, which is byte-identical to the old pin behaviour.
                var lo11 = p.Has(b.MinStat) ? p.Get(b.MinStat) : p.Get(b.MaxStat);
                var hi11 = p.Has(b.MaxStat) ? p.Get(b.MaxStat) : lo11;
                var (lo, hi) = AnchorLerp(lo11, hi11,
                    p.Has(b.MinStat + "_t25") ? p.Get(b.MinStat + "_t25") : (double?)null,
                    p.Has(b.MaxStat + "_t25") ? p.Get(b.MaxStat + "_t25") : (double?)null, tier);
                return Clamp(lo, hi, b);
            }
            return ZoneModifiers.WeaponBandAt(b, tier);
        }

        /// <summary>Lerp an authored T11 pair toward its optional T25 anchor pair. One-box rule per
        /// anchor: a lone _min_t25 or _max_t25 is an exact pair at T25. No T25 at all = flat.</summary>
        private static (double Lo, double Hi) AnchorLerp(double lo11, double hi11, double? lo25, double? hi25, int tier)
        {
            if (lo25 == null && hi25 == null)
                return (lo11, hi11);
            var l25 = lo25 ?? hi25.Value;
            var h25 = hi25 ?? l25;
            var t = Math.Clamp((tier - 11) / 14.0, 0.0, 1.0);
            return (lo11 + (l25 - lo11) * t, hi11 + (h25 - hi11) * t);
        }

        /// <summary>The authored pin on a Stats dictionary, or null when neither box is set. Same
        /// one-box rule as <see cref="WeaponDropBand"/>, and since 2026-08-29 the same T11/T25
        /// anchoring; StatCurve.Evaluate(1) is how every other Default-layer read in this file spells
        /// "the authored number".</summary>
        private static (double Min, double Max)? PinBand(Dictionary<string, ZoneScaling.StatCurve> stats, ZoneModifiers.WeaponBand b, int tier)
        {
            if (stats == null || stats.Count == 0)
                return null;
            double? Read(string key)
                => stats.TryGetValue(key, out var c) && c != null ? c.Evaluate(1) : (double?)null;
            var min11 = Read(b.MinStat);
            var max11 = Read(b.MaxStat);
            if (min11 == null && max11 == null)
                return null;
            var lo11 = min11 ?? max11.Value;
            var hi11 = max11 ?? lo11;
            return AnchorLerp(lo11, hi11, Read(b.MinStat + "_t25"), Read(b.MaxStat + "_t25"), tier);
        }

        /// <summary>Order + clamp a weapon band to the card's own Lo/Hi. Those bounds are copied from
        /// the card's historical RollRange(lo, hi) arguments, so no path - authored, ladder or fallback -
        /// can ever produce a value the old drop-time code could not.</summary>
        private static (double Min, double Max) Clamp(double lo, double hi, ZoneModifiers.WeaponBand b)
        {
            if (lo > hi) (lo, hi) = (hi, lo);
            return (Math.Clamp(lo, b.Lo, b.Hi), Math.Clamp(hi, b.Lo, b.Hi));
        }

        /// <summary>
        /// A knob from the tier's DEFAULT layer, or null when the tier does not author it. Nullable so a
        /// caller can tell "authored as 1100" from "not authored" - armor_base_level needs that distinction
        /// (unset falls back to the historical formula, not to a constant).
        /// One locked snapshot read (ZoneControlManager.GetVariationDefault); call it ONCE per operation.
        /// </summary>
        public static double? DefaultLayerValue(int tier, string stat)
        {
            var def = ZoneControlManager.GetAnchoredDefaultProfile(tier);
            if (def?.Stats != null && def.Stats.TryGetValue(stat, out var curve) && curve != null)
                return curve.Evaluate(1);
            return null;
        }

        /// <summary>A core_anchor_* knob from the tier's Default layer, else the C# default.</summary>
        private static double DefaultLayerStat(int tier, string stat, double fallback)
            => DefaultLayerValue(tier, stat) ?? fallback;

        /// <summary>
        /// The core-four window at a tier - THE SAME formula as LootGenerationFactory.ApplyT11GearStats.RollCore
        /// (cap = anchor/18 x (1+(t-11)/14), fixed T11 step = anchor/18/14, floor = cap - 1.5 step, T11 - 0.5 step,
        /// rounded at the end). Anchors: the tier's Default-layer core_anchor_dr / core_anchor_cdr, else the LADDER
        /// constants 1250 / 750. anchorOverride lets a drop-time caller pass the ZONE's evaluated anchors instead.
        /// With zonecontrol_enabled OFF nothing authored is consulted at all - the T10 FALLBACK anchors win
        /// outright, including over anchorOverride (owner 2026-08-23).
        /// </summary>
        public static (int Min, int Max) CoreWindow(int coreKey, int tier, double? anchorOverride = null)
        {
            var isDr = coreKey == CoreDamageResist;
            var zcOn = ServerConfig.zonecontrol_enabled.Value;
            var anchor = !zcOn
                ? (isDr ? ZoneFallback.AnchorDr : ZoneFallback.AnchorCdr)
                : anchorOverride ?? DefaultLayerStat(tier, isDr ? ZoneStat.CoreAnchorDr : ZoneStat.CoreAnchorCdr,
                                                    isDr ? LadderAnchorDr : LadderAnchorCdr);
            // The fallback is FLAT - nothing climbs with tier off the switch, matching the flat armour base
            // and the tier-blind fallback line bands (owner 2026-08-23). Only the ladder scales.
            var scale = zcOn ? 1.0 + (tier - 11) / 14.0 : 1.0;
            var cap = anchor / 18.0 * scale;
            var step = anchor / 18.0 / 14.0;
            var lo = cap - (tier == 11 ? 0.5 : 1.5) * step;
            var min = (int)Math.Round(lo);
            var max = (int)Math.Round(cap);
            if (min > max) min = max;
            return (min, max);
        }

        /// <summary>The LADDER core anchors - the worn-set totals a maxed T25 suit lands on (18 pieces).</summary>
        public const double LadderAnchorDr = 1250.0, LadderAnchorCdr = 750.0;

        /// <summary>The per-tier armor base when nothing is authored: 1100 + 100/tier above 11 on the ladder.</summary>
        public static int LadderArmorLevel(int tier) => 1100 + 100 * (tier - 11);

        /// <summary>
        /// The per-tier armor base (ApplyT11GearStats + Compute). Three-step chain, owner 2026-08-24
        /// (Armor_Base_Values_Plan_2026-08-24.md section 2.1):
        ///   zonecontrol_enabled OFF          -> the flat T10 fallback, and NOTHING authored is consulted
        ///                                       (same rule as EffectiveBand / CoreWindow, owner 2026-08-23)
        ///   Default[tier] armor_base_level   -> that value
        ///   otherwise                        -> 1100 + 100 x (tier - 11), the historical formula
        /// Unset therefore reproduces the pre-2026-08-24 numbers EXACTLY - nothing moves until authored.
        ///
        /// READ STAGE - this one is RESOLVE, not DROP. It is read inside <see cref="Compute"/>, so
        /// authoring armor_base_level RE-PRICES EVERY EXISTING PIECE of that tier on its next equip or
        /// login (or immediately via Apply Ladder). That is the opposite of armor_prot_base /
        /// armor_prot_equalize, which are stamped once at drop time and never revisited.
        ///
        /// PERFORMANCE: Compute calls this exactly ONCE per resolve (after the line loop, not inside it),
        /// so the Default-layer lookup is one locked snapshot read per resolve - no per-line cost, and no
        /// signature change was needed to get there. Callers that already hold the Default's value can use
        /// the overload below to skip the lookup entirely.
        /// </summary>
        public static int BaseArmorLevel(int tier)
        {
            if (!ServerConfig.zonecontrol_enabled.Value)
                return ZoneFallback.ArmorLevel;
            var authored = DefaultLayerValue(tier, ZoneStat.ArmorBaseLevel);
            return authored.HasValue ? (int)Math.Round(authored.Value) : LadderArmorLevel(tier);
        }

        /// <summary>
        /// Same chain as <see cref="BaseArmorLevel(int)"/> but fed an already-resolved authored value, for a
        /// caller that has one in hand (a drop-time evaluated profile, or a loop over many pieces at one
        /// tier). null = not authored -> the ladder formula.
        /// </summary>
        public static int BaseArmorLevel(int tier, double? authored)
        {
            if (!ServerConfig.zonecontrol_enabled.Value)
                return ZoneFallback.ArmorLevel;
            return authored.HasValue ? (int)Math.Round(authored.Value) : LadderArmorLevel(tier);
        }

        // ── armor protection (DROP-time only) ───────────────────────────────────
        // Both of these are stamped when the piece is CREATED and never looked at again, unlike
        // BaseArmorLevel above. Changing either affects NEW DROPS ONLY; existing gear keeps whatever it
        // was stamped with, no matter how many times it is re-equipped or the ladder is applied.

        /// <summary>The C# defaults, so every read site agrees: 1.0 protection, equalize ON.</summary>
        public const double DefaultArmorProtBase = 1.0;
        public const bool DefaultArmorProtEqualize = true;

        /// <summary>
        /// The value an ABSENT ArmorModVs* element is filled with at drop time (armor_prot_base). Same 0-2
        /// scale the engine uses: 1.0 = Average, 1.4 = Superior, 0.6 = Below Average. ArmorModVs* defaults
        /// to 0.0 and MULTIPLIES the piece AL, so an unfilled element is literally zero protection - this
        /// is a floor, not a bonus. Zone layer wins when it defines the key, else the tier Default, else 1.0.
        /// </summary>
        public static double ArmorProtBase(int tier, ZoneScaling.EvaluatedProfile p = null)
        {
            if (p != null && p.Has(ZoneStat.ArmorProtBase))
                return p.Get(ZoneStat.ArmorProtBase, DefaultArmorProtBase);
            return DefaultLayerValue(tier, ZoneStat.ArmorProtBase) ?? DefaultArmorProtBase;
        }

        /// <summary>
        /// Whether a fresh piece's present elements are averaged to their mean (armor_prot_equalize,
        /// default ON). OFF re-admits the spread: a Poor roll stays Poor and an Unparalleled roll stays
        /// Unparalleled instead of both collapsing toward the middle. Zone layer wins when it defines the
        /// key, else the tier Default, else ON. tier &lt;= 0 = "no tier known" -> the C# default.
        /// </summary>
        public static bool ArmorProtEqualize(int tier, ZoneScaling.EvaluatedProfile p = null)
        {
            if (p != null && p.Has(ZoneStat.ArmorProtEqualize))
                return p.Get(ZoneStat.ArmorProtEqualize, 1.0) != 0.0;
            if (tier <= 0)
                return DefaultArmorProtEqualize;
            var authored = DefaultLayerValue(tier, ZoneStat.ArmorProtEqualize);
            return authored.HasValue ? authored.Value != 0.0 : DefaultArmorProtEqualize;
        }

        // ── the record ──────────────────────────────────────────────────────────

        public struct LineRecord
        {
            public int Key;      // catalog key, or a Core* pseudo key
            public int Grade;    // 0-1000
        }

        public static bool HasRecord(WorldObject wo)
            => wo != null && !string.IsNullOrEmpty(wo.GetProperty(PropertyString.ZcModifiers));

        /// <summary>
        /// The item's ladder row. ARMOUR, clothing and jewelry carry ZcTier; WEAPONS DO NOT -
        /// LootGenerationFactory.ApplyT11GearStats returns at its `default:` case for weapons/casters
        /// BEFORE <see cref="StampIdentity"/> ever runs, so a weapon's tier lives in
        /// PropertyInt.WeaponAugScaleTier (stamped by ApplyWeaponAugScaleStamp later in the same
        /// Creature_Death sweep). Reading only ZcTier - which is what this did before 2026-08-25 -
        /// returned 0 for every weapon, which meant Compute silently resolved weapon lines at the T11
        /// rung no matter what tier they dropped at, and ApplyIfStale bailed at its `tier &lt;= 0` guard
        /// so a weapon NEVER re-resolved. Both were live bugs for the armour-style cantrip lines
        /// weapons already carried; the weapon cards inherit the fix.
        ///
        /// Same shape as ZoneCraftGate.TierOf - if one of the two changes, change both.
        /// ORDERING TRAP: at ZoneLootMutator time NEITHER property is set yet (the aug-scale stamp runs
        /// after MutateLootItem), so every drop-time caller must pass the loot tier explicitly rather
        /// than asking the item. VerifyLiveStatCache runs after both stamps, so it may ask.
        /// </summary>
        public static int TierOf(WorldObject wo)
        {
            if (wo == null)
                return 0;
            // the MAX of both stamps, exactly as ZoneCraftGate.TierOf - a weapon carries both
            return Math.Max(wo.GetProperty(PropertyInt.ZcTier) ?? 0, wo.GetProperty(PropertyInt.WeaponAugScaleTier) ?? 0);
        }

        /// <summary>
        /// True when the record contains at least one reserved WEAPON key. Allocation-free and
        /// deliberately a raw character scan, because it sits on the ApplyIfStale early-out - the
        /// no-op path every login takes for every worn piece.
        ///
        /// It is exact, not a heuristic: Format writes core keys as 'cN', catalog keys as positive
        /// integers and grades as 0..1000, so the ONLY '-' a well-formed record can contain is the
        /// sign of a weapon key. Keep that true if you ever extend the format.
        /// </summary>
        public static bool HasWeaponKey(WorldObject wo)
        {
            var raw = wo?.GetProperty(PropertyString.ZcModifiers);
            return !string.IsNullOrEmpty(raw) && raw.IndexOf('-') >= 0;
        }

        /// <summary>Parse the record. Unknown / malformed entries are skipped, never thrown on.</summary>
        public static List<LineRecord> Read(WorldObject wo)
        {
            var list = new List<LineRecord>();
            var raw = wo?.GetProperty(PropertyString.ZcModifiers);
            if (string.IsNullOrEmpty(raw))
                return list;
            foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var i = part.IndexOf(':');
                if (i <= 0) continue;
                var keyTok = part.Substring(0, i).Trim();
                if (!int.TryParse(part.Substring(i + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var grade))
                    continue;
                int key;
                if (keyTok.Length == 2 && (keyTok[0] == 'c' || keyTok[0] == 'C') && keyTok[1] >= '1' && keyTok[1] <= '4')
                    key = -(keyTok[1] - '0');
                else if (!int.TryParse(keyTok, NumberStyles.Integer, CultureInfo.InvariantCulture, out key))
                    continue;
                list.Add(new LineRecord { Key = key, Grade = Math.Clamp(grade, 0, GradeMax) });
            }
            return list;
        }

        public static string Format(IEnumerable<LineRecord> lines)
        {
            var sb = new StringBuilder();
            foreach (var l in lines)
            {
                if (sb.Length > 0) sb.Append(';');
                if (IsCoreKey(l.Key)) sb.Append('c').Append(-l.Key);
                else sb.Append(l.Key.ToString(CultureInfo.InvariantCulture));
                sb.Append(':').Append(Math.Clamp(l.Grade, 0, GradeMax).ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public static void Write(WorldObject wo, List<LineRecord> lines)
        {
            if (wo == null) return;
            if (lines == null || lines.Count == 0)
                wo.RemoveProperty(PropertyString.ZcModifiers);
            else
                wo.SetProperty(PropertyString.ZcModifiers, Format(lines));
        }

        /// <summary>Append one grade to the record (a key already present is REPLACED - one line per key per piece).</summary>
        public static void AddLine(WorldObject wo, int key, int grade)
        {
            if (wo == null) return;
            var lines = Read(wo);
            lines.RemoveAll(l => l.Key == key);
            lines.Add(new LineRecord { Key = key, Grade = Math.Clamp(grade, 0, GradeMax) });
            Write(wo, lines);
        }

        /// <summary>
        /// The stamp that identifies a CURRENT resolve: the tier's ladder version, times two, plus a bit
        /// for the Zone Control switch. The mode HAS to be part of the identity - zonecontrol_enabled
        /// changes what a resolve produces, so without it flipping the switch would move the appraisal
        /// (which computes live) while the item's stamped props never changed (owner 2026-08-23).
        /// </summary>
        public static int ResolveStamp(int tier) => ResolveStamp(tier, false);

        /// <summary>
        /// As above, plus the WEAPON PIN FINGERPRINT when <paramref name="withWeaponPins"/> is set.
        ///
        /// WHY THIS EXISTS (2026-08-25). `ladder apply` is the only thing that bumps
        /// ZoneControlManager.GetLadderVersion, and the only Default-layer edits wired to auto-apply are
        /// core_anchor_dr / core_anchor_cdr and the cantrip band editor (ZoneControlCommands
        /// .AutoApplyForDefault). Authoring or clearing a weapon_&lt;card&gt;_min/max on a tier Default
        /// changes what <see cref="WeaponResolveBand"/> returns, so without something in the identity
        /// moving, every weapon already in the world would keep its old number FOREVER and the edit
        /// would appear to do nothing except on new drops. Folding the authored pins into the stamp
        /// makes the pin edit itself the invalidation - no command surface change, and it cannot be
        /// forgotten the way "remember to run ladder apply" can.
        ///
        /// It is deliberately NOT folded into GetLadderVersion: that Version is displayed by
        /// `/zonecontrol ladder show`, echoed by ladder apply and shipped to the plugin in the |ladder=
        /// tag, where "v3" is a human-meaningful count of applies. A hashed number there would be a lie
        /// in a GM surface. ResolveStamp, by contrast, is compared and never displayed as a quantity.
        ///
        /// ZERO WHEN NOTHING IS AUTHORED, and the shift keeps bit 0 (the Zone Control mode bit) intact:
        /// a shard with no weapon pins produces byte-for-byte the pre-2026-08-25 stamp, so deploying
        /// this does not spuriously re-resolve a single existing item.
        ///
        /// withWeaponPins is passed by the caller rather than derived here because the same item must
        /// get the same answer at stamp time and at compare time; every internal caller derives it from
        /// <see cref="HasWeaponKey"/> on the item itself. An armour piece never pays the lookup.
        /// </summary>
        public static int ResolveStamp(int tier, bool withWeaponPins)
        {
            var stamp = ZoneControlManager.GetLadderVersion(tier).Version * 2
                      + (ServerConfig.zonecontrol_enabled.Value ? 0 : 1);
            if (!withWeaponPins)
                return stamp;
            var fp = WeaponPinFingerprint(tier);
            return fp == 0 ? stamp : unchecked(stamp ^ (fp << 1));
        }

        /// <summary>
        /// A hash of the tier Default's authored weapon_*_min/max values, or 0 when the tier authors
        /// none of them. Rows are folded in <see cref="WeaponSpecials"/> order so the result is stable
        /// across runs; the value's exact bits are meaningless, only "did it change" matters.
        /// With Zone Control OFF this returns 0 because <see cref="WeaponResolveBand"/> consults nothing
        /// authored in that mode - a pin edit genuinely cannot change what a weapon resolves to, so it
        /// must not invalidate anything either.
        /// </summary>
        private static int WeaponPinFingerprint(int tier)
        {
            if (!ServerConfig.zonecontrol_enabled.Value)
                return 0;
            var stats = ZoneControlManager.GetAnchoredDefaultProfile(tier)?.Stats;
            if (stats == null || stats.Count == 0)
                return 0;
            var h = 0;
            foreach (var ws in WeaponSpecials)
            {
                h = Fold(h, stats, ws.Band.MinStat);
                h = Fold(h, stats, ws.Band.MaxStat);
            }
            return h;

            static int Fold(int acc, Dictionary<string, ZoneScaling.StatCurve> s, string stat)
            {
                if (!s.TryGetValue(stat, out var curve) || curve == null)
                    return acc;
                // the VALUE, not the curve object: a re-author to the same number must not invalidate,
                // and MutateVariationDefault replaces the StatCurve instance on every edit.
                return unchecked(acc * 31 + curve.Evaluate(1).GetHashCode() + stat.Length);
            }
        }

        /// <summary>Stamp the tier + the CURRENT resolve stamp (a fresh drop is resolved by definition).</summary>
        public static void StampIdentity(WorldObject wo, int tier)
        {
            if (wo == null) return;
            wo.SetProperty(PropertyInt.ZcTier, tier);
            wo.SetProperty(PropertyInt.ZcResolvedVersion, ResolveStamp(tier, HasWeaponKey(wo)));
        }

        /// <summary>
        /// The WEAPON half of <see cref="StampIdentity"/>: the resolve stamp WITHOUT ZcTier.
        ///
        /// CORRECTED 2026-08-25. This used to say "a weapon must not carry ZcTier", on the grounds
        /// that TierOf and the crafting gate treat "has ZcTier" as "this is an armour-shaped piece".
        /// They do not, and never did: both TierOf implementations take a plain max of ZcTier and
        /// WeaponAugScaleTier and never branch on which is present, and the only code that decides
        /// "is this armour" is <see cref="Compute"/>, which keys on ItemType + ArmorLevel. The rule
        /// was a convention that had documented itself as a constraint.
        /// Weapons now DO carry ZcTier (owner 2026-08-25, stamped in ApplyT11GearStats' default case)
        /// so the crafting gate cannot be switched off by a single missing stamp.
        /// This method still writes only the VERSION, because the version must be stamped after the
        /// weapon's grades are recorded - which is here, not at gear-stat time.
        ///
        /// Call this AFTER the last grade has been recorded - HasWeaponKey reads the finished record.
        /// </summary>
        public static void StampWeaponResolve(WorldObject wo, int tier)
        {
            if (wo == null || tier <= 0) return;
            wo.SetProperty(PropertyInt.ZcResolvedVersion, ResolveStamp(tier, HasWeaponKey(wo)));
        }

        // ── resolution ──────────────────────────────────────────────────────────

        public class ResolvedLine
        {
            public LineRecord Record;
            public ZoneModifiers.Def Def;     // null for a core key
            public int Min, Max, Value;
            public string Name => Def?.Name ?? CoreName(Record.Key);
            /// <summary>The appraisal text for this line, in the stamp format ("Damage Rating +41 [14-69]").</summary>
            public string Text
            {
                get
                {
                    if (Def == null)
                        return $"{Name} +{Value} [{Min}-{Max}]";
                    if (Def.SlotSpecial)
                        return string.IsNullOrEmpty(Def.ValFmt) ? Name : $"{Name} {string.Format(Def.ValFmt, Value)}";
                    return $"{Name} {string.Format(Def.ValFmt ?? "+{0}", Value)} [{Min}-{Max}]";
                }
            }
        }

        public class Resolved
        {
            public int Tier;
            public List<ResolvedLine> Lines = new();
            /// <summary>Every int prop the cache should hold, SET semantics (retail Gear* + 502xx block).</summary>
            public Dictionary<PropertyInt, int> Ints = new();
            /// <summary>
            /// Every FLOAT prop the cache should hold, same SET semantics. This is the weapon half of the
            /// cache: every continuous weapon card is a PropertyFloat (CriticalFrequency,
            /// SlayerDamageBonus, IgnoreShield, CriticalMultiplier and the two override prop ids 9056 /
            /// 9057), so none of them could ever live in <see cref="Ints"/>.
            /// Values here are already in ENGINE space - <see cref="EngineValue"/> has run.
            /// </summary>
            public Dictionary<PropertyFloat, double> Floats = new();
            /// <summary>Resolved Armor Level (base for tier + the key-25 line), null when the piece has no AL.</summary>
            public int? ArmorLevel;
        }

        /// <summary>
        /// PURE: grades -> resolved values against the live ladder. Never touches the item. Returns null when
        /// the piece carries no record. Core keys resolve through <see cref="CoreWindow"/>; catalog keys
        /// through <see cref="EffectiveBand"/>; key 25 adds to the tier's base AL; every Ints prop of a def
        /// gets the value (All Attributes = six props, same number); Reinforced is skipped (frozen).
        /// </summary>
        public static Resolved Compute(WorldObject wo)
        {
            if (!HasRecord(wo))
                return null;
            var tier = TierOf(wo);
            if (tier <= 0) tier = 11;
            var r = new Resolved { Tier = tier };
            var hasArmor = wo.ArmorLevel.HasValue && wo.ItemType == ItemType.Armor;
            var alBonus = 0;

            // The tier Default's Stats, fetched AT MOST ONCE per resolve and only when the record
            // actually contains a weapon key. This used to matter for locking (GetVariationDefault takes
            // the manager lock, and a fully carded weapon would have taken it twelve times - two stat
            // names per card); since 2026-09-01 the anchored table is precomputed and read lock-free, so
            // the caching is now just avoiding twelve dictionary probes.
            Dictionary<string, ZoneScaling.StatCurve> defStats = null;
            var defStatsFetched = false;

            foreach (var rec in Read(wo))
            {
                if (IsWeaponKey(rec.Key))
                {
                    // An unknown key inside the reserved weapon block is skipped, not thrown on - an
                    // item stamped by a newer build must stay loadable on an older one.
                    if (!TryGetWeapon(rec.Key, out var ws))
                        continue;
                    if (!defStatsFetched)
                    {
                        defStats = ZoneControlManager.GetAnchoredDefaultProfile(tier)?.Stats;
                        defStatsFetched = true;
                    }
                    var (wlo, whi) = WeaponResolveBand(ws, tier, defStats, statsFetched: true);
                    var display = Math.Clamp(ValueForD(wlo, whi, rec.Grade), ws.Band.Lo, ws.Band.Hi);
                    // EngineValue is the single display -> engine conversion (Crushing Blow's "- 1.0").
                    // The drop site routes through the same method, so the subtraction happens exactly
                    // once no matter how many times a weapon is re-equipped. See EngineValue.
                    r.Floats[ws.Prop] = EngineValue(ws, display);
                    // NOT added to r.Lines. r.Lines is the APPRAISAL projection (AppraiseInfo walks it),
                    // and weapon cards have never had appraisal lines - adding them here would silently
                    // change the examine panel of every Zone Control weapon on the shard, which is a
                    // GUI change and needs its own plan + go. Floats alone is the mechanical parity.
                    continue;
                }
                if (IsCoreKey(rec.Key))
                {
                    var (cmin, cmax) = CoreWindow(rec.Key, tier);
                    var cval = ValueFor(cmin, cmax, rec.Grade);
                    r.Lines.Add(new ResolvedLine { Record = rec, Def = null, Min = cmin, Max = cmax, Value = cval });
                    r.Ints[CoreProp(rec.Key)] = cval;
                    continue;
                }
                if (!ZoneModifiers.TryGet(rec.Key, out var def) || def.SetsProtection)
                    continue;
                // Zone Control off: slot SPECIALS are disabled outright (owner 2026-08-23). Zero their
                // props so the effect is inert, and leave them out of r.Lines so the appraisal stops
                // advertising them. The RECORD is untouched - switching back on restores the special at
                // its recorded grade on the next equip / login.
                if (def.SlotSpecial && !ServerConfig.zonecontrol_enabled.Value)
                {
                    if (def.Ints != null)
                        foreach (var (propId, _) in def.Ints)
                            r.Ints[(PropertyInt)propId] = 0;
                    continue;
                }
                var (min, max) = EffectiveBand(rec.Key, tier);
                var value = ValueFor(min, max, rec.Grade);
                r.Lines.Add(new ResolvedLine { Record = rec, Def = def, Min = min, Max = max, Value = value });
                if (def.ArmorOnly && def.Ints == null)
                {
                    alBonus += value;                       // key 25 Armor Level
                }
                else if (def.Ints != null)
                    foreach (var (propId, _) in def.Ints)
                        r.Ints[(PropertyInt)propId] = value;
            }

            // 🔴 PRE-APPLIED CRAFTS THAT LAND ON A CARD'S OWN PROPERTY.
            // The Bandit Hilt is stamped LAST at drop time (owner rule: hilts go on after every other
            // tuner so their bonuses ADD on top), and two of the things it adds to are the very
            // properties Biting Strike and Crushing Blow own: +0.25 CriticalFrequency and +0.175
            // CriticalMultiplier. Before the cards were graded that was harmless - nothing ever
            // recomputed the property. Now r.Floats is written with SET semantics on every equip, so
            // without this the hilt's contribution would be ERASED the first time a hilted weapon was
            // re-resolved, and there would be nothing in any log to say where the crit went.
            // Re-adding it here is also correct for a hilt a PLAYER applied with the real recipe after
            // the drop - same marker, same delta, same erase avoided.
            // Only props the RECORD actually produced are touched: a hilted weapon with no Biting
            // Strike card has no -11 key, so CriticalFrequency is not in r.Floats and is left alone.
            if (r.Floats.Count > 0 && ZoneLootMutator.HasBanditHilt(wo))
            {
                if (r.Floats.TryGetValue(PropertyFloat.CriticalFrequency, out var cf))
                    r.Floats[PropertyFloat.CriticalFrequency] = cf + ZoneLootMutator.BanditHiltCritFrequencyBonus;
                if (r.Floats.TryGetValue(PropertyFloat.CriticalMultiplier, out var cm))
                    r.Floats[PropertyFloat.CriticalMultiplier] = cm + ZoneLootMutator.BanditHiltCritMultiplierBonus;
            }

            // EVERY recorded armour piece resolves its AL, line or no line: the tier base is no longer a
            // constant (zonecontrol_enabled swaps the whole ladder for the T10 fallback, owner 2026-08-23),
            // so a piece without an Armor Level line would otherwise keep a stamp from the other set forever.
            // alBonus stays 0 without that line, so a lineless piece lands exactly on the tier base.
            // ONE BaseArmorLevel call per Compute, deliberately outside the line loop: since 2026-08-24 it
            // reads the tier Default (armor_base_level), and that is a locked snapshot read.
            if (hasArmor)
                r.ArmorLevel = BaseArmorLevel(tier) + alBonus;
            return r;
        }

        /// <summary>
        /// Write a Resolved set onto the item (SET semantics - ZC owns these props on a T11+ piece). With
        /// allowNerf false a value that would DROP below what is stamped keeps the stamped number (owner
        /// policy: an accidental apply fixes under-tuned gear, never silently cuts anyone). Stamps the
        /// version. Returns the number of props that actually changed.
        /// </summary>
        public static int Apply(WorldObject wo, Resolved r, bool allowNerf = true)
        {
            if (wo == null || r == null) return 0;
            var changed = 0;
            foreach (var kv in r.Ints)
            {
                var cur = wo.GetProperty(kv.Key);
                var next = kv.Value;
                if (!allowNerf && cur.HasValue && next < cur.Value)
                    next = cur.Value;
                if (cur != next)
                {
                    wo.SetProperty(kv.Key, next);
                    changed++;
                }
            }
            // The weapon half, SAME semantics as the ints above: SET, nerf-guarded, counted only when
            // the stored number actually moves. The comparison is exact rather than epsilon-based on
            // purpose - both sides come from the same deterministic band math, so an unchanged ladder
            // reproduces the identical double and this writes nothing. An epsilon here would let a
            // genuine sub-epsilon retune go unstamped, and the stamp would then claim it had landed.
            foreach (var kv in r.Floats)
            {
                var cur = wo.GetProperty(kv.Key);
                var next = kv.Value;
                if (!allowNerf && cur.HasValue && next < cur.Value)
                    next = cur.Value;
                if (cur != next)
                {
                    wo.SetProperty(kv.Key, next);
                    changed++;
                }
            }
            if (r.ArmorLevel.HasValue)
            {
                var cur = wo.ArmorLevel;
                var next = r.ArmorLevel.Value;
                if (!allowNerf && cur.HasValue && next < cur.Value)
                    next = cur.Value;
                if (cur != next)
                {
                    wo.ArmorLevel = next;
                    changed++;
                }
            }
            // wield gates follow the tier row live too (owner 2026-08-23: T16+ Item Augs + Triune on existing pieces)
            changed += ACE.Server.Factories.LootGenerationFactory.RefreshWieldGate(wo, r.Tier);
            // HasWeaponKey off the ITEM, not off r.Floats: the stamp written here has to be the same
            // number ApplyIfStale computes on the next equip, and that one only has the item to go on.
            wo.SetProperty(PropertyInt.ZcResolvedVersion, ResolveStamp(r.Tier, HasWeaponKey(wo)));
            return changed;
        }

        /// <summary>
        /// EQUIP hook: when the piece's stamp does not match the current one (tier ladder version + the
        /// Zone Control switch), re-resolve and re-stamp, mark the biota dirty. Cheap no-op for every piece
        /// without a record, and for a resolved one. Bounded by what a character can wear - never called
        /// from appraisal (plan §3b). NOT-EQUAL rather than less-than: a mode flip must re-stamp in BOTH
        /// directions, and going to the fallback is a nerf by definition, so the nerf guard is bypassed
        /// while the switch is off (owner 2026-08-23 chose fallback numbers on existing items).
        /// </summary>
        public static bool ApplyIfStale(WorldObject wo)
        {
            if (!HasRecord(wo))
                return false;
            var tier = TierOf(wo);
            if (tier <= 0) return false;
            var ladder = ZoneControlManager.GetLadderVersion(tier);
            var seen = wo.GetProperty(PropertyInt.ZcResolvedVersion) ?? 0;
            if (seen == ResolveStamp(tier, HasWeaponKey(wo)))
                return false;
            var r = Compute(wo);
            if (r == null) return false;
            var allowNerf = ladder.AllowNerf || !ServerConfig.zonecontrol_enabled.Value;
            var changed = Apply(wo, r, allowNerf);
            wo.ChangesDetected = true;
            if (changed > 0)
                wo.SaveBiotaToDatabase();
            return changed > 0;
        }
    }
}
