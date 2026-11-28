using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using ACE.Common;
using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Managers.WeaponScaling;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

using log4net;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Salvage Bags (owner 2026-10-02 as "Gear Essences"; renamed 2026-10-04 - the code keeps the GearEssences name):
    /// consumables a player uses on a T11+ Zone Control item to change its properties, in the spirit of Path of Exile
    /// currency but kept deliberately simple. Player names (owner 2026-10-04):
    ///
    ///   Core:   Bag of Forgetfulness (remove one), Bag of Memory (add one), Bag of Second Thoughts (reroll the LOWEST one,
    ///           owner 2026-11-26), Bag of Tempering (Weapon Grade one sub-grade up or down, owner 2026-11-26)
    ///   Extras: Bag of Fortune (reroll one, keep the better), Bag of Rebirth (reroll all), Bag of Locking (lock one at random),
    ///           Bag of Vengeance (set a weapon's Slayer to the monster type that dropped the bag),
    ///           Bag of Madness (owner 2026-11-26, a Vaal orb: 30% nothing, 30% one property SET past its max, 30% every property
    ///           SHIFTED +-15% of its range from where it is, 10% one property removed - only the Madness list, never a locked
    ///           one; the item is Tainted every time),
    ///           Bag of Transmutation (a weapon's damage type becomes a random NEW one of the eight - Nether, Fire, Cold,
    ///           Lightning, Acid, Slash, Pierce, Bludgeon; its rending, Cast on Strike spells, tint and a leading
    ///           type word in its name follow),
    ///           Bag of Emptiness (remove all), Bag of Exchange (remove one and add one)
    ///   Crafting steps (owner 2026-11-26, Path of Exile's transmute / augment / alteration / chaos):
    ///           Bag of Genesis (add one to an item with no properties), Bag of Augmentation (add one to an item with
    ///           exactly one), Bag of Alteration (an item with one or two properties ends up with ONE new random one),
    ///           Bag of Reforging (replace every property, same count). The counts are the ones the appraisal's
    ///           "Properties: N of cap" line shows; Alteration and Reforging keep a locked line and a Slayer.
    ///   The code's EssenceKind names are the original ones: Loss, Gain, Change, Edge, Fortune, Renewal, Keeping, Hunt,
    ///   Chaos, Transmutation, Emptiness, Trade (the 2026-11-26 four use their player names).
    ///
    /// "Property" means an armour modifier LINE (a ZcModifiers catalog key) on armour, shields, jewelry,
    /// clothing and cloaks, and a graded weapon CARD (record keys -11..-16 and -19) on weapons. The player never
    /// sees the two systems - one essence works on any piece.
    ///
    /// Owner rules:
    ///   - The Always Rolled resists (50-53), the legacy core four, Reinforced (49) and the slot specials
    ///     are PROTECTED: never removed, rerolled or added. A line locked by a Bag of Locking is protected too.
    ///   - Loss never takes an item's last property - a locked line counts as one (Emptiness does clear everything it can,
    ///     on purpose, and a Bag of Madness may remove an item's only Madness property).
    ///   - The item must be in the player's pack, not worn or wielded (no equip-cache refresh needed).
    ///   - Gain picks with each line's own loot chance, so chase lines stay rare (a pick is weighted among what is left, so a
    ///     chase line's share grows as the ordinary lines run out; none is ever forced).
    ///   - Every refusal happens BEFORE the essence is consumed. Every refusal depends only on the item, never on a roll, so
    ///     retrying can't fish for an outcome.
    ///   - A Tainted item (Madness) takes no more bags of any kind.
    ///
    /// Bags drop only inside Zone Control zones: see Creature_Death (bag_odds).
    /// </summary>
    public static class GearEssences
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // 78780310-78780329: Gear Essences block (docs/WCID_ALLOCATION_7878.md). 310-325 are all bags since 2026-11-26
        // (320-322 and 325 held the single-type bags folded into Transmutation on 2026-10-04, never live); 326-329 are free.
        public const uint LossWcid = 78780310;
        public const uint GainWcid = 78780311;
        public const uint ChangeWcid = 78780312;
        public const uint EdgeWcid = 78780313;
        public const uint FortuneWcid = 78780314;
        public const uint RenewalWcid = 78780315;
        public const uint KeepingWcid = 78780316;
        public const uint HuntWcid = 78780317;
        public const uint ChaosWcid = 78780318;
        public const uint TransmutationWcid = 78780319;
        public const uint GenesisWcid = 78780320;
        public const uint AugmentationWcid = 78780321;
        public const uint AlterationWcid = 78780322;
        public const uint EmptinessWcid = 78780323;
        public const uint TradeWcid = 78780324;
        public const uint ReforgingWcid = 78780325;

        public static bool IsGearEssence(uint wcid) => wcid >= LossWcid && wcid <= ReforgingWcid;

        internal enum EssenceKind { Loss, Gain, Change, Edge, Fortune, Renewal, Keeping, Hunt, Chaos, Transmutation, Emptiness, Trade, Genesis, Augmentation, Alteration, Reforging }

        internal static EssenceKind KindOf(uint wcid) => wcid switch
        {
            LossWcid => EssenceKind.Loss,
            GainWcid => EssenceKind.Gain,
            ChangeWcid => EssenceKind.Change,
            EdgeWcid => EssenceKind.Edge,
            FortuneWcid => EssenceKind.Fortune,
            RenewalWcid => EssenceKind.Renewal,
            KeepingWcid => EssenceKind.Keeping,
            HuntWcid => EssenceKind.Hunt,
            ChaosWcid => EssenceKind.Chaos,
            EmptinessWcid => EssenceKind.Emptiness,
            TradeWcid => EssenceKind.Trade,
            TransmutationWcid => EssenceKind.Transmutation,
            GenesisWcid => EssenceKind.Genesis,
            AugmentationWcid => EssenceKind.Augmentation,
            AlterationWcid => EssenceKind.Alteration,
            ReforgingWcid => EssenceKind.Reforging,
            _ => throw new ArgumentOutOfRangeException(nameof(wcid), wcid, "not a Salvage Bag"),
        };

        /// <summary>The eight damage types Transmutation picks from (owner 2026-10-04: melee, missile and magic all use all eight).</summary>
        internal static readonly DamageType[] TransmutationTypes =
        {
            DamageType.Nether, DamageType.Fire, DamageType.Cold, DamageType.Electric, DamageType.Acid,
            DamageType.Slash, DamageType.Pierce, DamageType.Bludgeon,
        };

        /// <summary>The types a Transmutation may roll: every one the weapon does not already deal (a Slash/Pierce sword
        /// picks from the other six), so every use is a real change. A weapon with no type of its own picks from all eight.</summary>
        internal static List<DamageType> TransmutationChoices(DamageType current)
            => TransmutationTypes.Where(t => (current & t) == 0).ToList();

        /// <summary>The graded weapon cards an essence may touch. Cast on Strike damage (-17/-18) is left out:
        /// it is tied to the proc spell slots and means nothing to a player on its own.</summary>
        private static readonly ZoneStatResolver.WeaponSpecial[] EssenceWeaponCards =
        {
            ZoneStatResolver.SpecBite, ZoneStatResolver.SpecArmorRend, ZoneStatResolver.SpecRendPower,
            ZoneStatResolver.SpecSlayer, ZoneStatResolver.SpecShieldCleave, ZoneStatResolver.SpecCrush,
            ZoneStatResolver.SpecSpellArmorRend,
        };

        /// <summary>Every damage-type rend imbue (the physical three and the five elements).</summary>
        private const ImbuedEffectType DamageRends =
            ImbuedEffectType.SlashRending | ImbuedEffectType.PierceRending | ImbuedEffectType.BludgeonRending |
            ImbuedEffectType.AcidRending | ImbuedEffectType.ColdRending | ImbuedEffectType.ElectricRending |
            ImbuedEffectType.FireRending | ImbuedEffectType.NetherRending;

        /// <summary>True when any bit of <paramref name="mask"/> sits in ImbuedEffect2-5, where a bag's removal (it clears
        /// ImbuedEffect) can't reach it - also when the same bit is in ImbuedEffect too (CodeRabbit #629 2026-11-27: comparing
        /// the union with ImbuedEffect missed a bit held in both).</summary>
        internal static bool ImbueOutsideFirstSlot(WorldObject wo, ImbuedEffectType mask)
            => ((ImbuedEffectType)((wo.GetProperty(PropertyInt.ImbuedEffect2) ?? 0) | (wo.GetProperty(PropertyInt.ImbuedEffect3) ?? 0)
                | (wo.GetProperty(PropertyInt.ImbuedEffect4) ?? 0) | (wo.GetProperty(PropertyInt.ImbuedEffect5) ?? 0)) & mask) != 0;

        // -- item state shared with the appraisal ------------------------------------------------

        /// <summary>The record key locked by a Bag of Locking, or null.</summary>
        public static int? LockedKey(WorldObject wo) => wo?.GetProperty(PropertyInt.GearEssenceLockedKey);

        /// <summary>True once a Bag of Madness has been used on the item: no bag works on it again.</summary>
        public static bool IsTainted(WorldObject wo) => wo?.GetProperty(PropertyBool.GearEssenceTainted) == true;

        /// <summary>The appraisal bullet for a Tainted item.</summary>
        public const string TaintedAppraisalLine = "- Tainted: bags no longer work on this item";

        /// <summary>The marker on an appraisal line that never uses a property slot (owner 2026-10-04: the Always Rolled resists;
        /// the slot specials and the legacy resists for the same reason) - so a player counting lines matches "Properties: X of Y".</summary>
        public const string BuiltInMarker = " (Built-in)";

        /// <summary>True when a modifier line uses one of the item's property slots: what a drop's armor_modifier_cap counts
        /// and what a bag's limit counts (Reinforced included).</summary>
        public static bool UsesASlot(ZoneModifiers.Def def)
            => def != null && !(def.SlotSpecial || def.Class == ZoneModifiers.ModifierClass.Always || def.Class == ZoneModifiers.ModifierClass.None);

        /// <summary>"- Properties: 3 of 5" for a tier 11+ Zone item: the lines or cards that use a slot, and the tier's limit
        /// (armor_modifier_cap / weapon_modifier_cap) - the SAME count and limit the bags enforce. "- Properties: 3" when the
        /// tier sets no limit; null below tier 11.</summary>
        public static string PropertiesAppraisalLine(WorldObject wo)
        {
            var p = PropertiesCount(wo);
            if (p == null)
                return null;
            var (count, cap) = p.Value;
            return cap == int.MaxValue ? $"- Properties: {count}" : $"- Properties: {count} of {cap}";
        }

        /// <summary>The two numbers behind <see cref="PropertiesAppraisalLine"/>: the lines or cards that use a slot, and the tier's
        /// limit (int.MaxValue when the tier sets none). Null below tier 11. The loot properties (ZoneLootProps) send the same pair.</summary>
        public static (int Count, int Cap)? PropertiesCount(WorldObject wo)
        {
            if (wo == null)
                return null;
            var tier = ZoneStatResolver.TierOf(wo);
            if (tier < LootGenerationFactory.ZoneLootSetMinTier)
                return null;
            var weapon = IsWeapon(wo);
            var count = weapon ? CardCount(wo) : LineCount(wo);
            return (count, CachedCap(tier, weapon));
        }

        /// <summary>How long an appraisal reuses a tier's limit: every appraisal of a T11+ piece shows it (loot ID'ing included),
        /// and building the tier profile each time is the expensive part. The bags themselves always read it fresh.</summary>
        private static readonly TimeSpan CapCacheFor = TimeSpan.FromSeconds(10);

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<(int Tier, bool Weapon), (int Cap, DateTime At)> CapCache = new();

        private static int CachedCap(int tier, bool weapon)
        {
            var now = DateTime.UtcNow;
            if (CapCache.TryGetValue((tier, weapon), out var hit) && now - hit.At < CapCacheFor)
                return hit.Cap;
            var cap = CapAt(TierDefaultProfile(tier), weapon ? ZoneStat.WeaponModifierCap : ZoneStat.ArmorModifierCap, tier);
            CapCache[(tier, weapon)] = (cap, now);
            return cap;
        }

        /// <summary>The appraisal bullet naming a locked weapon card, or null when no weapon card is locked.</summary>
        public static string LockedCardAppraisalLine(WorldObject wo)
        {
            var key = LockedKey(wo);
            if (key == null || !ZoneStatResolver.TryGetWeapon(key.Value, out var card)
                || !ZoneStatResolver.Read(wo).Any(l => l.Key == key.Value))
                return null;
            return $"- Locked: {(card == ZoneStatResolver.SpecRendPower ? RendName(wo) : card.Name)}";
        }

        // -- blacksmithing --------------------------------------------------------------------------
        // The forge (ForgeMath / ForgeWeaponWriter) decides for itself which tier 11+ properties a forged item ends up with:
        // it is not a bag and follows none of a bag's rules. It does go through the SAME add / remove / regrade steps as the
        // bags below, so the record, the stats, the baked text and the "Properties: X of Y" count can never disagree.

        /// <summary>A property the forge leaves with the item it is on: a weapon's rend (the rend belongs to the weapon's
        /// element), Biting Strike / Crushing Blow under a Bandit Hilt (the hilt adds onto them), and the property a Bag
        /// of Locking locked (owner, 2026-10-07: the forge respects locks).</summary>
        private static bool ForgePinned(WorldObject wo, Unit u)
            => u.Card == ZoneStatResolver.SpecRendPower
               || u.Key == LockedKey(wo)
               || (ZoneLootMutator.HasBanditHilt(wo) && (u.Card == ZoneStatResolver.SpecBite || u.Card == ZoneStatResolver.SpecCrush));

        /// <summary>
        /// What the forge needs to know about a tier 11+ item's properties: the record keys it may keep, drop or hand to
        /// another item; how many slots are used by properties it never moves; and the tier's limit (int.MaxValue = none).
        /// The two counts add up to exactly what the appraisal shows as X in "Properties: X of Y". Empty below tier 11.
        /// </summary>
        internal static (List<int> PoolKeys, int FixedSlots, int Cap) ForgeProperties(WorldObject wo)
        {
            var tier = ZoneStatResolver.TierOf(wo);
            if (wo == null || tier < LootGenerationFactory.ZoneLootSetMinTier)
                return (new List<int>(), 0, int.MaxValue);
            var weapon = IsWeapon(wo);
            var pool = AllUnits(wo).Where(u => !ForgePinned(wo, u)).Select(u => u.Key).ToList();
            var count = weapon ? CardCount(wo) : LineCount(wo);
            var cap = CapAt(TierDefaultProfile(tier), weapon ? ZoneStat.WeaponModifierCap : ZoneStat.ArmorModifierCap, tier);
            return (pool, Math.Max(0, count - pool.Count), cap);
        }

        /// <summary>ASCII name of a property by its record key, for the forge's chat line.</summary>
        internal static string ForgePropertyName(int key)
        {
            if (ZoneStatResolver.TryGetWeapon(key, out var card))
                return card == ZoneStatResolver.SpecRendPower ? "Rending" : card.Name;
            return ZoneModifiers.TryGet(key, out var def) ? def.Name : $"Modifier {key}";
        }

        /// <summary>
        /// Makes <paramref name="target"/>'s movable properties exactly <paramref name="want"/> (record key -> grade), and
        /// sets the grades in <paramref name="regrade"/> on record lines it already carries. <paramref name="target"/> is a
        /// forged item that starts as a copy of the main item; <paramref name="donor"/> is the second item, the source of
        /// anything a gained property needs beyond its grade (a Slayer's creature type).
        /// Every grade the forge writes is clamped at 1000 (SetGradeInPlace / AddLine / StampGraded): a Bag of Madness over-max
        /// grade on either item does NOT survive forging - the forge never produces a grade past a band's max.
        /// </summary>
        internal static void ForgeApplyProperties(WorldObject target, IReadOnlyDictionary<int, int> want, IReadOnlyDictionary<int, int> regrade, WorldObject donor)
        {
            var tier = ZoneStatResolver.TierOf(target);
            if (tier < LootGenerationFactory.ZoneLootSetMinTier)
                return;
            var weapon = IsWeapon(target);
            var ops = new List<Op>();
            var have = AllUnits(target).Where(u => !ForgePinned(target, u)).ToList();

            foreach (var u in have.Where(u => !want.ContainsKey(u.Key)))
            {
                var op = RemoveOp(u);
                ApplyOp(target, op);
                ops.Add(op);
            }

            foreach (var (key, grade) in want)
            {
                var i = have.FindIndex(u => u.Key == key);
                if (i >= 0)
                {
                    var op = new Op { Action = OpAction.Regrade, Key = key, Def = have[i].Def, Card = have[i].Card, Grade = grade };
                    ApplyOp(target, op);
                    ops.Add(op);
                    continue;
                }

                var add = new Op { Action = OpAction.Add, Key = key, Grade = grade };
                if (weapon)
                {
                    add.Card = EssenceWeaponCards.FirstOrDefault(c => c.Key == key);
                    if (add.Card == null || add.Card == ZoneStatResolver.SpecRendPower)
                        continue;   // not a card the forge moves
                    // ForgeMath never draws this (ForgeWeapon.NoGainKeys); held here too so the pairing cannot be made
                    if (add.Card == ZoneStatResolver.SpecArmorRend && !ForgeCanGainArmorRend(target))
                        continue;
                    // Spell Armor Rend is a caster card: never onto a melee or missile weapon (NoGainKeys too)
                    if (add.Card == ZoneStatResolver.SpecSpellArmorRend && !(target is Caster))
                        continue;
                }
                else if (!ZoneModifiers.TryGet(key, out add.Def) || IsProtected(add.Def))
                    continue;
                ApplyOp(target, add);
                ops.Add(add);

                if (add.Card == ZoneStatResolver.SpecSlayer && donor != null)
                {
                    // a Slayer is a strength AND a prey: the prey comes from the weapon the card came from
                    if (donor.GetProperty(PropertyInt.SlayerCreatureType) is int prey) target.SetProperty(PropertyInt.SlayerCreatureType, prey);
                    else target.RemoveProperty(PropertyInt.SlayerCreatureType);
                    if (donor.GetProperty(PropertyBool.SlayerAllCreatures) == true) target.SetProperty(PropertyBool.SlayerAllCreatures, true);
                    else target.RemoveProperty(PropertyBool.SlayerAllCreatures);
                }
            }

            // grades of lines the item keeps whatever the forge decides (the built-in resists, a weapon's rend)
            foreach (var (key, grade) in regrade)
            {
                if (!ZoneStatResolver.Read(target).Any(l => l.Key == key))
                    continue;
                SetGradeInPlace(target, key, grade);
                if (ZoneStatResolver.TryGetWeapon(key, out var card))
                    ops.Add(new Op { Action = OpAction.Regrade, Key = key, Card = card, Grade = grade });
                else if (ZoneModifiers.TryGet(key, out var def))
                    ops.Add(new Op { Action = OpAction.Regrade, Key = key, Def = def, Grade = grade });
            }

            Resolve(target, ops);

            // a lock on a property that is gone means nothing
            if (LockedKey(target) is int locked && !ZoneStatResolver.Read(target).Any(l => l.Key == locked))
                target.RemoveProperty(PropertyInt.GearEssenceLockedKey);
        }

        // -- planning types ------------------------------------------------------------------------

        private enum OpAction { Remove, Add, Regrade }

        /// <summary>One change to one property. All randomness is decided while planning.</summary>
        private class Op
        {
            public OpAction Action;
            public int Key;                                 // record key (catalog key or weapon card key)
            public ZoneModifiers.Def Def;                   // armour line, null for a weapon card
            public ZoneStatResolver.WeaponSpecial Card;     // weapon card, null for an armour line
            public int Grade;                               // new grade (Add / Regrade)
            public ImbuedEffectType Rend;                   // Add of Rend Power: the rend to add
            public string Text;                             // filled while applying: what the player is told
            public string Before;                           // Regrade: the property before, for the chat line
            public bool NoLower;                            // Fortune: never below the value the item holds (under no-nerf)
            public bool OverMax;                            // Madness Regrade: the grade may pass 1000 (ZoneStatResolver.GradeCap)
            public EvaluatedProfile Profile;                // Add of key 54 (jewelry Cast on Strike): the zone stats for the proc rate
        }

        /// <summary>One planned use, chosen (including every random roll) BEFORE the essence is consumed.</summary>
        private class Plan
        {
            public EssenceKind Kind;
            public List<Op> Ops = new();
            public int Quality;                             // Edge: new Weapon Grade quality (unchanged on a "no change" roll)
            public TemperStep Step;                         // Edge: the step rolled
            public int LockKey;                             // Keeping: the key to lock
            public CreatureType Species;                    // Hunt: the Slayer type
            public DamageType Element;                      // Transmutation: the new damage type (rolled while planning)
            public MadnessOutcome Madness;                  // Chaos: the outcome rolled
            public bool FortuneKept;                        // Fortune: the old roll was better and stays
        }

        /// <summary>A property an essence can see on an item: an armour line or a weapon card.</summary>
        private struct Unit
        {
            public int Key;
            public ZoneModifiers.Def Def;
            public ZoneStatResolver.WeaponSpecial Card;
        }

        internal static bool IsWeapon(WorldObject wo) => wo is MeleeWeapon || wo is MissileLauncher || wo is Missile || wo is Caster;

        // -- drops -------------------------------------------------------------------------------

        /// <summary>
        /// The per-kill bag roll (zone drops only; the caller checks the zone gate). 1 in bag_odds,
        /// per rank like special_odds; then the bag_weight_* stats pick which. Unset odds = never.
        /// The four core weights default to 1; every extra defaults to 0 (off until the owner sets it).
        /// A Bag of Vengeance remembers the killed monster's type. Returns the new bag, or null.
        /// </summary>
        public static WorldObject TryRollDrop(EvaluatedProfile zoneLoot, string killerName, Creature killed)
        {
            var denom = ZoneStatResolver.OddsToInt(zoneLoot.Get(ZoneStat.BagOdds, 0.0));   // 0 = unset, below 1 or not finite
            if (denom < 1 || ThreadSafeRandom.Next(1, denom) != 1)
                return null;

            var species = killed?.CreatureType;
            var huntable = species != null && species != CreatureType.Invalid;

            var table = new List<(uint Wcid, double Weight)>
            {
                (LossWcid, zoneLoot.Get(ZoneStat.BagWeightForgetfulness, 1.0)),
                (GainWcid, zoneLoot.Get(ZoneStat.BagWeightMemory, 1.0)),
                (ChangeWcid, zoneLoot.Get(ZoneStat.BagWeightSecondThoughts, 1.0)),
                (EdgeWcid, zoneLoot.Get(ZoneStat.BagWeightTempering, 1.0)),
                (FortuneWcid, zoneLoot.Get(ZoneStat.BagWeightFortune, 0.0)),
                (RenewalWcid, zoneLoot.Get(ZoneStat.BagWeightRebirth, 0.0)),
                (KeepingWcid, zoneLoot.Get(ZoneStat.BagWeightLocking, 0.0)),
                (HuntWcid, huntable ? zoneLoot.Get(ZoneStat.BagWeightVengeance, 0.0) : 0.0),
                (ChaosWcid, zoneLoot.Get(ZoneStat.BagWeightMadness, 0.0)),
                (TransmutationWcid, zoneLoot.Get(ZoneStat.BagWeightTransmutation, 0.0)),
                (EmptinessWcid, zoneLoot.Get(ZoneStat.BagWeightEmptiness, 0.0)),
                (TradeWcid, zoneLoot.Get(ZoneStat.BagWeightExchange, 0.0)),
                (GenesisWcid, zoneLoot.Get(ZoneStat.BagWeightGenesis, 0.0)),
                (AugmentationWcid, zoneLoot.Get(ZoneStat.BagWeightAugmentation, 0.0)),
                (AlterationWcid, zoneLoot.Get(ZoneStat.BagWeightAlteration, 0.0)),
                (ReforgingWcid, zoneLoot.Get(ZoneStat.BagWeightReforging, 0.0)),
            };
            var weights = table.Select(t => double.IsFinite(t.Weight) ? Math.Max(0.0, t.Weight) : 0.0).ToList();
            if (weights.Sum() <= 0.0)
                return null;

            var wcid = table[WeightedIndex(weights)].Wcid;
            var essence = WorldObjectFactory.CreateNewWorldObject(wcid);
            if (essence == null)
            {
                log.Warn($"[ZONELOOT] SALVAGE BAG: rolled wcid {wcid} but it has no weenie - apply the Salvage Bags SQL to ace_world");
                return null;
            }

            if (wcid == HuntWcid)
            {
                essence.SetProperty(PropertyInt.GearEssenceHuntCreatureType, (int)species.Value);
                essence.Name = $"Bag of Vengeance ({SpeciesName(species.Value)})";
            }

            log.Info($"[ZONELOOT] SALVAGE BAG: {killerName ?? "(unknown)"} killed {killed?.Name} ({killed?.WeenieClassId}) -> {essence.Name}, odds 1 in {denom}");
            return essence;
        }

        /// <summary>The " [35-69]" band a resolved line ends with - left out of chat.</summary>
        private static readonly Regex TrailingBand = new Regex(@"\s*\[[^\]]*\]\s*$", RegexOptions.Compiled);

        private static readonly Regex CamelSplit = new Regex("(?<=[a-z])(?=[A-Z])", RegexOptions.Compiled);

        /// <summary>A species as players read it: "GearKnight" -> "Gear Knight" (also the item bounties' fallback target name).</summary>
        internal static string SpeciesName(CreatureType species) => CamelSplit.Replace(species.ToString(), " ");

        // -- entry point -------------------------------------------------------------------------

        /// <summary>CraftTool.HandleActionUseOnTarget for an essence. Checks, asks for confirmation, and
        /// applies on "yes". Always ends the client's use.</summary>
        public static void UseOnTarget(Player player, WorldObject essence, WorldObject target)
        {
            if (player.IsBusy)
            {
                player.SendUseDoneEvent(WeenieError.YoureTooBusy);
                return;
            }

            try
            {
                // a dry run: the plan is built only to find a refusal BEFORE the popup; Apply plans again (fresh rolls) on "yes"
                var refusal = Verify(player, essence, target);
                if (refusal == null)
                    BuildPlan(essence, target, out refusal);
                if (refusal != null)
                {
                    player.SendTransientError(refusal);
                    return;
                }

                var essenceGuid = essence.Guid.Full;
                var targetGuid = target.Guid.Full;
                var promptedState = PromptState(essence, target);   // what the player said yes to (review 2026-11-26)
                void OnResponse(bool response, bool _)
                {
                    if (response)
                        Apply(player, essenceGuid, targetGuid, promptedState);
                }

                if (!player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, OnResponse), ConfirmText(essence, target)))
                    player.SendWeenieError(WeenieError.ConfirmationInProgress);
            }
            catch (Exception ex)
            {
                // nothing is consumed or changed before the confirmation, so a throw here costs the player nothing
                log.Error($"[SALVAGE BAG] {player.Name} could not start {essence?.Name} on {target?.Name}: {ex}");
                player.SendTransientError("Something went wrong with the bag. Please tell an admin.");
            }
            finally
            {
                player.SendUseDoneEvent();   // always end the client's use, or it stays busy
            }
        }

        private static string ConfirmText(WorldObject essence, WorldObject target)
        {
            var what = KindOf(essence.WeenieClassId) switch
            {
                EssenceKind.Loss => "One random property will be removed.",
                EssenceKind.Gain => "One random property will be added.",
                EssenceKind.Change => SecondThoughtsConfirmText(target),
                EssenceKind.Edge => TemperingConfirmText(target),
                EssenceKind.Fortune => "One random property will be rerolled. If the new roll is no better, the old one stays.",
                EssenceKind.Renewal => "Every property will be rerolled (a locked one stays). Each can go up or down.",
                EssenceKind.Keeping => LockedKey(target) != null
                    ? "One random property will be locked. Other bags will not change it. The lock it already has moves to the new one."
                    : "One random property will be locked. Other bags will not change it.",
                EssenceKind.Hunt => $"Its Slayer will become {HuntSpeciesText(essence)}.",
                EssenceKind.Chaos => MadnessConfirmText(target),
                EssenceKind.Transmutation => "Its damage type will change to a random new one (Nether, Fire, Frost, Lightning, Acid, Slashing, Piercing or Bludgeoning). Any rending, Cast on Strike, and a damage-type word at the start of its name change to match.",
                EssenceKind.Emptiness => "Every property that bags can change will be removed.",
                EssenceKind.Trade => "One random property will be removed and another added.",
                EssenceKind.Genesis => "One random property will be added. This bag only works on an item with no properties.",
                EssenceKind.Augmentation => "One random property will be added. This bag only works on an item with exactly one property.",
                EssenceKind.Alteration => ReplaceConfirmText(EssenceKind.Alteration, target),
                EssenceKind.Reforging => ReplaceConfirmText(EssenceKind.Reforging, target),
                _ => throw new ArgumentOutOfRangeException(nameof(essence), essence.WeenieClassId, "no confirm text"),
            };
            return $"Use the {essence.Name} on your {target.Name}?\n\n{what}";
        }

        private static string HuntSpeciesText(WorldObject essence)
        {
            var species = essence.GetProperty(PropertyInt.GearEssenceHuntCreatureType);
            return species.HasValue ? SpeciesName((CreatureType)species.Value) : "unknown";
        }

        private static string ElementName(DamageType dt) => dt switch
        {
            DamageType.Fire => "Fire",
            DamageType.Cold => "Frost",
            DamageType.Acid => "Acid",
            DamageType.Electric => "Lightning",
            DamageType.Nether => "Nether",
            DamageType.Slash => "Slashing",
            DamageType.Pierce => "Piercing",
            DamageType.Bludgeon => "Bludgeoning",
            _ => dt.ToString(),
        };

        private static ImbuedEffectType RendFor(DamageType dt) => dt switch
        {
            DamageType.Fire => ImbuedEffectType.FireRending,
            DamageType.Cold => ImbuedEffectType.ColdRending,
            DamageType.Acid => ImbuedEffectType.AcidRending,
            DamageType.Electric => ImbuedEffectType.ElectricRending,
            DamageType.Nether => ImbuedEffectType.NetherRending,
            DamageType.Slash => ImbuedEffectType.SlashRending,
            DamageType.Pierce => ImbuedEffectType.PierceRending,
            DamageType.Bludgeon => ImbuedEffectType.BludgeonRending,
            _ => throw new ArgumentOutOfRangeException(nameof(dt), dt, "not a Transmutation damage type"),
        };

        /// <summary>Location and item-kind checks shared by the first use and the confirmation. Null = OK.</summary>
        private static string Verify(Player player, WorldObject essence, WorldObject target)
        {
            if (essence == null || target == null)
                return "The bag or the item is no longer there.";

            if (essence == target)
                return $"You can't use the {essence.Name} on itself.";

            if (player.FindObject(essence.Guid.Full, Player.SearchLocations.MyInventory) == null)
                return $"The {essence.Name} must be in your pack.";

            if (player.FindObject(target.Guid.Full, Player.SearchLocations.MyInventory) == null)
            {
                if (player.FindObject(target.Guid.Full, Player.SearchLocations.MyEquippedItems) != null)
                    return $"Take the {target.Name} off before using the bag on it.";
                return $"The {target.Name} must be in your pack.";
            }

            // any open trade blocks essences: a pack in the trade window carries its contents, and changing an item does not
            // reset the other side's acceptance
            if (player.IsTrading)
                return "You can't use a bag while you are trading.";

            if (IsTainted(target))
                return $"Your {target.Name} is Tainted. Bags no longer work on it.";

            // T11+ RARES (owner 2026-10-08): a rare takes no bag of any kind - every roll on it is already at the
            // maximum, and a bag could only break that. Here, before the kind is even read, so all twelve are covered
            // (Edge returns early just below), and before the popup, so nothing is consumed.
            if (ZoneRare.IsRare(target))
                return ZoneRare.BagRefusalText(target.Name, ZoneRare.TierOfRare(target));

            var kind = KindOf(essence.WeenieClassId);

            if (kind == EssenceKind.Edge)
            {
                if (!IsWeapon(target) || !target.GetProperty(PropertyInt.WeaponAugScaleQuality).HasValue)
                    return $"The {essence.Name} only works on weapons that show a Weapon Grade.";
                return null;
            }

            // Eligible = tier 11+ Zone gear, with or without a record (owner 2026-10-04 D7: a T11 weapon that dropped with no
            // cards takes essences too - Gain can give it its first). Tier 11+ stamps exist only on Zone drops.
            if (ZoneStatResolver.TierOf(target) < LootGenerationFactory.ZoneLootSetMinTier)
                return $"The {essence.Name} only works on tier 11 and higher Zone gear.";

            if ((kind == EssenceKind.Hunt || kind == EssenceKind.Transmutation) && !IsWeapon(target))
                return $"The {essence.Name} only works on weapons.";

            return null;
        }

        // -- what an essence can see ---------------------------------------------------------------

        /// <summary>Lines no bag touches: every line that uses no slot, and Reinforced (earned and frozen).</summary>
        private static bool IsProtected(ZoneModifiers.Def def) => !UsesASlot(def) || def.SetsProtection;

        /// <summary>Every property an essence may change, in record order, locked one included.</summary>
        private static List<Unit> AllUnits(WorldObject wo)
        {
            var list = new List<Unit>();
            var weapon = IsWeapon(wo);
            foreach (var rec in ZoneStatResolver.Read(wo))
            {
                if (weapon)
                {
                    var card = EssenceWeaponCards.FirstOrDefault(c => c.Key == rec.Key);
                    if (card != null)
                        list.Add(new Unit { Key = rec.Key, Card = card });
                    continue;   // armour-style lines left on old weapons are not touched
                }
                if (rec.Key <= 0 || !ZoneModifiers.TryGet(rec.Key, out var def) || IsProtected(def))
                    continue;   // legacy core four (negative), retired keys and protected lines
                list.Add(new Unit { Key = rec.Key, Def = def });
            }
            return list;
        }

        /// <summary>The properties an essence may change right now: AllUnits minus the locked one.</summary>
        private static List<Unit> FreeUnits(WorldObject wo)
        {
            var locked = LockedKey(wo);
            return AllUnits(wo).Where(u => u.Key != locked).ToList();
        }

        /// <summary>FreeUnits that may also be REMOVED: a Bandit Hilt adds onto Biting Strike and Crushing Blow,
        /// so removing those cards would take the hilt's share with them.</summary>
        private static List<Unit> RemovableUnits(WorldObject wo)
        {
            var units = FreeUnits(wo);
            if (ZoneLootMutator.HasBanditHilt(wo))
                units.RemoveAll(u => u.Card == ZoneStatResolver.SpecBite || u.Card == ZoneStatResolver.SpecCrush);
            return units;
        }

        /// <summary>Alteration and Reforging: the bags that replace properties rather than add or remove them.</summary>
        internal static bool IsReplaceKind(EssenceKind kind) => kind == EssenceKind.Alteration || kind == EssenceKind.Reforging;

        /// <summary>What Alteration and Reforging replace: RemovableUnits minus the Slayer. Only a Bag of Vengeance gives a
        /// Slayer, so a replacing bag must never take one (owner 2026-11-26).</summary>
        private static List<Unit> ReplaceUnits(WorldObject wo)
        {
            var units = RemovableUnits(wo);
            units.RemoveAll(u => u.Card == ZoneStatResolver.SpecSlayer);
            return units;
        }

        /// <summary>
        /// The property-count rules of the crafting-step bags (owner 2026-11-26), on the count the appraisal's
        /// "Properties: N of cap" line shows (<see cref="PropertiesCount"/>): Genesis needs 0, Augmentation exactly 1,
        /// Alteration 1 or 2. Null = the bag may be used; every other kind has no count rule here.
        /// </summary>
        internal static string CountRefusal(EssenceKind kind, int count, string itemName)
        {
            string Has() => count == 1 ? "1 property" : $"{count} properties";
            return kind switch
            {
                EssenceKind.Genesis when count != 0 => $"A Bag of Genesis only works on an item with no properties. Your {itemName} has {Has()}.",
                EssenceKind.Augmentation when count != 1 => $"A Bag of Augmentation only works on an item with exactly one property. Your {itemName} has {Has()}.",
                EssenceKind.Alteration when count < 1 || count > 2 => $"A Bag of Alteration only works on an item with one or two properties. Your {itemName} has {Has()}.",
                _ => null,
            };
        }

        /// <summary>The count the appraisal shows and the bags enforce: cards on a weapon, slot-using lines on anything else.</summary>
        private static int ShownCount(WorldObject wo) => IsWeapon(wo) ? CardCount(wo) : LineCount(wo);

        private static int GradeOf(WorldObject wo, int key)
            => ZoneStatResolver.Read(wo).Where(l => l.Key == key).Select(l => (int?)l.Grade).FirstOrDefault() ?? 0;

        private static Op RemoveOp(Unit u) => new Op { Action = OpAction.Remove, Key = u.Key, Def = u.Def, Card = u.Card };

        private static Op RegradeOp(Unit u, int grade) => new Op { Action = OpAction.Regrade, Key = u.Key, Def = u.Def, Card = u.Card, Grade = grade };

        // -- planning (all randomness happens here, before anything is consumed) ------------------

        private static Plan BuildPlan(WorldObject essence, WorldObject target, out string refusal)
        {
            refusal = null;
            var kind = KindOf(essence.WeenieClassId);
            var tier = ZoneStatResolver.TierOf(target);
            var plan = new Plan { Kind = kind };

            switch (kind)
            {
                case EssenceKind.Edge:
                {
                    // one sub-grade step (owner 2026-11-26): 60 up / 30 none / 10 down by default, never past the tier's top, never S
                    var quality = target.GetProperty(PropertyInt.WeaponAugScaleQuality) ?? 0;
                    var (up, down, top) = TemperingSettings(TierDefaultProfile(tier), tier);
                    var temperRefusal = TemperingRefusal(quality, top, target.Name);
                    if (temperRefusal != null)
                        return Refuse(out refusal, temperRefusal);
                    plan.Step = RollTemperStep(ThreadSafeRandom.Next(0.0f, 1.0f), up, down);
                    plan.Quality = TemperedQuality(quality, plan.Step, top);
                    return plan;
                }

                case EssenceKind.Loss:
                {
                    var all = AllUnits(target);
                    var removable = RemovableUnits(target);
                    if (all.Count == 0)
                        return Refuse(out refusal, NothingToChange(target));
                    if (all.Count < 2)
                        return Refuse(out refusal, $"Your {target.Name} has only one property a bag can change, so the bag won't take it.");
                    if (removable.Count == 0)
                        return Refuse(out refusal, $"None of the properties on your {target.Name} can be removed.");
                    plan.Ops.Add(RemoveOp(Pick(removable)));
                    return plan;
                }

                case EssenceKind.Change:
                {
                    // the LOWEST-graded changeable property (owner 2026-11-26), at the tier's normal odds: up or down
                    var free = FreeUnits(target);
                    if (free.Count == 0)
                        return Refuse(out refusal, NothingToChange(target));
                    plan.Ops.Add(RegradeOp(LowestUnit(target, free), ZoneStatResolver.RollGrade(tier)));
                    return plan;
                }

                case EssenceKind.Fortune:
                {
                    var free = FreeUnits(target);
                    if (free.Count == 0)
                        return Refuse(out refusal, NothingToChange(target));
                    var u = Pick(free);
                    var grade = ZoneStatResolver.RollGrade(tier);
                    var op = RegradeOp(u, grade);
                    if (kind == EssenceKind.Fortune)
                    {
                        // a roll no better than the current one changes nothing at all
                        plan.FortuneKept = grade <= GradeOf(target, u.Key);
                        op.NoLower = true;
                    }
                    plan.Ops.Add(op);
                    return plan;
                }

                case EssenceKind.Renewal:
                {
                    var free = FreeUnits(target);
                    if (free.Count == 0)
                        return Refuse(out refusal, NothingToChange(target));
                    foreach (var u in free)
                        plan.Ops.Add(RegradeOp(u, ZoneStatResolver.RollGrade(tier)));
                    return plan;
                }

                case EssenceKind.Keeping:
                {
                    var current = LockedKey(target);
                    var choices = AllUnits(target).Where(u => u.Key != current).ToList();
                    if (choices.Count == 0)
                        return Refuse(out refusal, current != null
                            ? $"Your {target.Name} has no other property to lock."
                            : NothingToChange(target));
                    plan.LockKey = Pick(choices).Key;
                    return plan;
                }

                case EssenceKind.Hunt:
                    return PlanHunt(essence, target, tier, plan, out refusal);

                case EssenceKind.Chaos:
                {
                    // the Bag of Madness redesign (owner 2026-11-26): only the Madness list, never a locked property
                    var eligible = MadnessUnits(target);
                    if (eligible.Count == 0)
                        return Refuse(out refusal, MadnessNothingToChange(target));
                    var removable = MadnessRemovableUnits(target);
                    var record = ZoneStatResolver.Read(target);
                    var roll = RollMadness(
                        eligible.Select(u => (u.Key, record.Where(l => l.Key == u.Key).Select(l => l.Grade).FirstOrDefault())).ToList(),
                        removable.Select(u => u.Key).ToList(),
                        MadnessSettingsFrom(TierDefaultProfile(tier), tier),
                        ThreadSafeRandom.Next, () => ThreadSafeRandom.Next(0.0f, 1.0f));
                    plan.Madness = roll.Outcome;
                    foreach (var u in eligible)
                        if (roll.Grades.TryGetValue(u.Key, out var grade))
                        {
                            var op = RegradeOp(u, grade);
                            op.OverMax = true;
                            plan.Ops.Add(op);
                        }
                    if (roll.RemovedKey is int gone)
                        plan.Ops.Add(RemoveOp(removable.First(u => u.Key == gone)));
                    return plan;
                }

                case EssenceKind.Transmutation:
                {
                    if (ImbueOutsideFirstSlot(target, DamageRends))
                        return Refuse(out refusal, $"The rending on your {target.Name} can't be changed by a bag.");
                    if (LockedKey(target) == ZoneStatResolver.WeaponRendPowerKey && (target.GetImbuedEffects() & DamageRends) != 0)
                        return Refuse(out refusal, $"The Rending on your {target.Name} is locked, so its damage type can't be changed.");
                    var choices = TransmutationChoices(target.W_DamageType);
                    if (choices.Count == 0)
                        return Refuse(out refusal, $"Your {target.Name} already deals every damage type.");
                    plan.Element = choices[ThreadSafeRandom.Next(0, choices.Count - 1)];
                    return plan;
                }

                case EssenceKind.Emptiness:
                {
                    var removable = RemovableUnits(target);
                    if (removable.Count == 0)
                        return Refuse(out refusal, NothingToChange(target));
                    foreach (var u in removable)
                        plan.Ops.Add(RemoveOp(u));
                    return plan;
                }

                case EssenceKind.Trade:
                {
                    var removable = RemovableUnits(target);
                    if (removable.Count == 0)
                        return Refuse(out refusal, NothingToChange(target));
                    var gone = Pick(removable);
                    var add = PlanAdd(target, tier, gone.Key, out refusal);
                    if (add == null)
                        return null;
                    plan.Ops.Add(RemoveOp(gone));
                    plan.Ops.Add(add);
                    return plan;
                }

                case EssenceKind.Gain:
                {
                    var add = PlanAdd(target, tier, 0, out refusal);
                    if (add == null)
                        return null;
                    plan.Ops.Add(add);
                    return plan;
                }

                case EssenceKind.Genesis:
                case EssenceKind.Augmentation:
                {
                    var countRefusal = CountRefusal(kind, ShownCount(target), target.Name);
                    if (countRefusal != null)
                        return Refuse(out refusal, countRefusal);
                    var add = PlanAdd(target, tier, 0, out refusal);
                    if (add == null)
                        return null;
                    plan.Ops.Add(add);
                    return plan;
                }

                case EssenceKind.Alteration:
                case EssenceKind.Reforging:
                {
                    var countRefusal = CountRefusal(kind, ShownCount(target), target.Name);
                    if (countRefusal != null)
                        return Refuse(out refusal, countRefusal);
                    var replace = ReplaceUnits(target);
                    if (replace.Count == 0)
                        return Refuse(out refusal, NothingToChange(target));
                    // the rending bits must all sit where a removal clears them (ImbuedEffect), as Transmutation checks - for a
                    // damage Rending and for Armor Rending alike (lab retest 2026-11-26: an Armor Rending bit in ImbuedEffect2
                    // survived a Reforging with no card behind it)
                    if (replace.Any(u => u.Card == ZoneStatResolver.SpecRendPower)
                        && ImbueOutsideFirstSlot(target, DamageRends))
                        return Refuse(out refusal, $"The rending on your {target.Name} can't be changed by a bag.");
                    if (replace.Any(u => u.Card == ZoneStatResolver.SpecArmorRend)
                        && ImbueOutsideFirstSlot(target, ImbuedEffectType.ArmorRending))
                        return Refuse(out refusal, $"The Armor Rending on your {target.Name} can't be changed by a bag.");
                    var addCount = ReplacementCountFor(target, kind, tier, replace.Count);
                    if (addCount < 1)
                        return Refuse(out refusal, $"Your {target.Name} already has as many properties a bag can't change as its tier allows.");
                    var adds = PlanReplacements(target, tier, replace, addCount, out refusal);
                    if (adds == null)
                        return null;
                    plan.Ops.AddRange(replace.Select(RemoveOp));
                    plan.Ops.AddRange(adds);
                    return plan;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(essence), essence.WeenieClassId, "no planner for this bag");
            }
        }

        // -- Bag of Madness (owner 2026-11-26 redesign) ---------------------------------------------
        // A Vaal orb: the only way past a perfect drop, and a one-use last step. Every outcome touches only the Madness list
        // (ZoneStatResolver.CanGoOverMax) and never a locked property; the item is Tainted every time, "nothing" included.
        // There is no full wipe. "% of range" is in grade points: 10% of the range = 100 grade points, the ceiling is 1250.

        /// <summary>The four Madness outcomes.</summary>
        internal enum MadnessOutcome { Nothing, OverMax, Shift, Remove }

        /// <summary>The Madness knobs: the four outcome weights and the over-max / shift ranges as fractions of a property's range.</summary>
        internal readonly record struct MadnessSettings(double Nothing, double OverMax, double Shift, double Remove,
            double OverMaxMin, double OverMaxMax, double ShiftRange);

        /// <summary>The owner's numbers (2026-11-26): 30 / 30 / 30 / 10, over max by 10-25% of the range, shift +-15%.</summary>
        internal static readonly MadnessSettings MadnessDefaults = new(0.30, 0.30, 0.30, 0.10, 0.10, 0.25, 0.15);

        /// <summary>The most a Madness over-max may reach above max, as a fraction of the range: the 1250 grade ceiling.</summary>
        internal const double MadnessOverMaxCap = (ZoneStatResolver.GradeOverMaxCeiling - ZoneStatResolver.GradeMax) / (double)ZoneStatResolver.GradeMax;

        /// <summary>
        /// The Madness knobs from the item's tier Default (<paramref name="p"/>, null = none). Unset or not a real number = the
        /// default. The four chances are WEIGHTS (each outcome's chance is its value over the four's total; the defaults add to 1);
        /// a negative weight is 0, and four zero weights mean the defaults. The over-max range is held to 0..0.25 (the 1250
        /// ceiling) and put in order; the shift is held to 0..1.
        /// </summary>
        internal static MadnessSettings MadnessSettingsFrom(EvaluatedProfile p, int tier)
        {
            double Read(string stat, double fallback)
            {
                var v = p?.GetT(stat, fallback, tier) ?? fallback;
                return double.IsFinite(v) ? v : fallback;
            }
            var d = MadnessDefaults;
            var nothing = Math.Max(0.0, Read(ZoneStat.BagMadnessNothingChance, d.Nothing));
            var overMax = Math.Max(0.0, Read(ZoneStat.BagMadnessOverMaxChance, d.OverMax));
            var shift = Math.Max(0.0, Read(ZoneStat.BagMadnessShiftChance, d.Shift));
            var remove = Math.Max(0.0, Read(ZoneStat.BagMadnessRemoveChance, d.Remove));
            if (nothing + overMax + shift + remove <= 0.0)
                (nothing, overMax, shift, remove) = (d.Nothing, d.OverMax, d.Shift, d.Remove);
            var lo = Math.Clamp(Read(ZoneStat.BagMadnessOverMaxMin, d.OverMaxMin), 0.0, MadnessOverMaxCap);
            var hi = Math.Clamp(Read(ZoneStat.BagMadnessOverMaxMax, d.OverMaxMax), 0.0, MadnessOverMaxCap);
            if (lo > hi) (lo, hi) = (hi, lo);
            var range = Math.Clamp(Read(ZoneStat.BagMadnessShift, d.ShiftRange), 0.0, 1.0);
            return new MadnessSettings(nothing, overMax, shift, remove, lo, hi, range);
        }

        /// <summary>The outcome a roll in [0, 1) lands on, the four weights laid end to end in the order nothing, over max, shift,
        /// remove.</summary>
        internal static MadnessOutcome PickMadnessOutcome(double roll, MadnessSettings s)
        {
            s = MadnessScaled(s);
            var total = s.Nothing + s.OverMax + s.Shift + s.Remove;
            var x = roll * total;
            if (x < s.Nothing) return MadnessOutcome.Nothing;
            if (x < s.Nothing + s.OverMax) return MadnessOutcome.OverMax;
            if (x < s.Nothing + s.OverMax + s.Shift) return MadnessOutcome.Shift;
            return s.Remove > 0.0 ? MadnessOutcome.Remove
                 : s.Shift > 0.0 ? MadnessOutcome.Shift : s.OverMax > 0.0 ? MadnessOutcome.OverMax : MadnessOutcome.Nothing;   // float tail
        }

        /// <summary>The four outcome weights divided by the largest, so a huge finite weight can't make their sum infinite
        /// (CodeRabbit 2026-11-26). Same odds; the prompt and the roll both use it.</summary>
        internal static MadnessSettings MadnessScaled(MadnessSettings s)
        {
            var largest = Math.Max(Math.Max(s.Nothing, s.OverMax), Math.Max(s.Shift, s.Remove));
            return largest <= 0.0 ? s : s with { Nothing = s.Nothing / largest, OverMax = s.OverMax / largest, Shift = s.Shift / largest, Remove = s.Remove / largest };
        }

        /// <summary>A fraction of the range as grade points (10% = 100).</summary>
        internal static int GradePoints(double fraction) => (int)Math.Round(fraction * ZoneStatResolver.GradeMax, MidpointRounding.AwayFromZero);

        /// <summary>The over-max outcome's grade range: max + OverMaxMin .. max + OverMaxMax, in grade points (default 1100..1250).</summary>
        internal static (int Lo, int Hi) MadnessOverMaxGrades(MadnessSettings s)
            => (ZoneStatResolver.GradeMax + GradePoints(s.OverMaxMin), Math.Min(ZoneStatResolver.GradeOverMaxCeiling, ZoneStatResolver.GradeMax + GradePoints(s.OverMaxMax)));

        /// <summary>A shift outcome's new grade: <paramref name="current"/> moved by <paramref name="delta"/> grade points, held to
        /// 0..1250 - it may pass the max.</summary>
        /// <summary>The confirm prompt's words for the over-max outcome, from the grade points the roll really uses
        /// (MadnessOverMaxGrades), not the raw settings: an end of 0, or one so small it rounds to 0 points (0.0001), lands on
        /// the maximum itself, so the prompt says "set to its maximum" there (CodeRabbit #629).</summary>
        internal static string MadnessOverMaxText(MadnessSettings s)
        {
            var (lo, hi) = MadnessOverMaxGrades(s);
            string Pct(int grade) => FormattableString.Invariant($"{(grade - ZoneStatResolver.GradeMax) * 100.0 / ZoneStatResolver.GradeMax:0.#}%");
            if (hi <= ZoneStatResolver.GradeMax) return "set to its maximum";
            if (lo <= ZoneStatResolver.GradeMax) return $"set to its maximum or past it, by up to {Pct(hi)} of its range";
            return $"set past its maximum, by {Pct(lo)} to {Pct(hi)} of its range";
        }

        internal static int MadnessShifted(int current, int delta) => Math.Clamp(current + delta, 0, ZoneStatResolver.GradeOverMaxCeiling);

        /// <summary>One Madness result, decided before anything is used: the outcome, the new grade per record key (over max: one
        /// key; shift: every eligible key), and the key removed (null when the removal found nothing it may take).</summary>
        internal sealed class MadnessRoll
        {
            public MadnessOutcome Outcome;
            public Dictionary<int, int> Grades = new();
            public int? RemovedKey;
        }

        /// <summary>
        /// Roll a Bag of Madness on <paramref name="eligible"/> (record key, current grade: the Madness list minus a locked
        /// property). <paramref name="removable"/> is the part of it outcome 4 may take (a Bandit Hilt keeps Biting Strike and
        /// Crushing Blow). <paramref name="nextInt"/> is max-INCLUSIVE like ThreadSafeRandom.Next(int, int); <paramref name="nextDouble"/>
        /// returns [0, 1). Pure, so the bounds can be tested.
        ///   Nothing  - no grade changes.
        ///   OverMax  - one random eligible property is SET to max + OverMaxMin..OverMaxMax of its range, whatever it was.
        ///   Shift    - every eligible property moves from its CURRENT grade by -ShiftRange..+ShiftRange of its range (each its own
        ///              roll), held to 0..1250, so it may pass the max or fall.
        ///   Remove   - one random removable property is removed.
        /// </summary>
        internal static MadnessRoll RollMadness(IReadOnlyList<(int Key, int Grade)> eligible, IReadOnlyList<int> removable,
            MadnessSettings s, Func<int, int, int> nextInt, Func<double> nextDouble)
        {
            var roll = new MadnessRoll { Outcome = PickMadnessOutcome(nextDouble(), s) };
            switch (roll.Outcome)
            {
                case MadnessOutcome.OverMax when eligible.Count > 0:
                {
                    var (lo, hi) = MadnessOverMaxGrades(s);
                    var pick = eligible[nextInt(0, eligible.Count - 1)];
                    roll.Grades[pick.Key] = nextInt(lo, hi);
                    break;
                }
                case MadnessOutcome.Shift:
                {
                    var d = GradePoints(s.ShiftRange);
                    // an unchanged grade gets no op: a regrade re-resolves the card against the tier Default and skips the
                    // no-nerf guard, which would overwrite a zone-stamped value for nothing (CodeRabbit 2026-11-26). If nothing
                    // moved, the plan is empty and the bag takes the Taint-only path.
                    foreach (var (key, grade) in eligible)
                    {
                        var shifted = MadnessShifted(grade, nextInt(-d, d));
                        if (shifted != grade)
                            roll.Grades[key] = shifted;
                    }
                    break;
                }
                case MadnessOutcome.Remove when removable.Count > 0:
                    roll.RemovedKey = removable[nextInt(0, removable.Count - 1)];
                    break;
            }
            return roll;
        }

        /// <summary>The properties a Bag of Madness may change: FreeUnits (so never a locked one) on the Madness list.</summary>
        private static List<Unit> MadnessUnits(WorldObject wo)
            => FreeUnits(wo).Where(u => ZoneStatResolver.CanGoOverMax(u.Key)).ToList();

        /// <summary>The Madness properties outcome 4 may remove: RemovableUnits (a Bandit Hilt keeps Biting Strike and Crushing
        /// Blow) on the Madness list, and a Rending only when its rend bits sit where a removal clears them (as Transmutation and
        /// the replacing bags check).</summary>
        private static List<Unit> MadnessRemovableUnits(WorldObject wo)
        {
            var rendClearable = !ImbueOutsideFirstSlot(wo, DamageRends);
            return RemovableUnits(wo)
                .Where(u => ZoneStatResolver.CanGoOverMax(u.Key) && (u.Card != ZoneStatResolver.SpecRendPower || rendClearable))
                .ToList();
        }

        /// <summary>The record keys a Bag of Madness may change on <paramref name="wo"/> now, and the ones it may remove.</summary>
        internal static (List<int> Eligible, List<int> Removable) MadnessKeys(WorldObject wo)
            => (MadnessUnits(wo).Select(u => u.Key).ToList(), MadnessRemovableUnits(wo).Select(u => u.Key).ToList());

        private const string MadnessListText =
            "Max Health, Damage Rating, Crit Damage Rating, Healing Boost, All Attributes and Armor Level, and on weapons Biting Strike, "
            + "Crushing Blow, Rending and Slayer";

        private static string MadnessNothingToChange(WorldObject target)
            => $"Your {target.Name} has no property a Bag of Madness can change. It only changes {MadnessListText}, and never a locked property.";

        /// <summary>The confirm text of Madness: the four outcomes with the live chances, and what on this item it can touch.</summary>
        private static string MadnessConfirmText(WorldObject target)
        {
            var tier = ZoneStatResolver.TierOf(target);
            var s = MadnessScaled(MadnessSettingsFrom(TierDefaultProfile(tier), tier));
            var total = s.Nothing + s.OverMax + s.Shift + s.Remove;
            string Pct(double w) => FormattableString.Invariant($"{w / total * 100:0.#}%");
            string RangePct(double f) => FormattableString.Invariant($"{f * 100:0.#}%");
            var units = MadnessUnits(target);
            var names = string.Join(", ", units.Select(u => UnitName(target, u)));
            // the removal can only take what MadnessRemovableUnits allows (lab retest 2026-11-26: under a Bandit Hilt, Biting
            // Strike and Crushing Blow stay, so a hilted weapon's removal can come up empty - say so before the bag is used)
            var removable = MadnessRemovableUnits(target);
            var removeLine = removable.Count == 0
                ? $"{Pct(s.Remove)}: a property would be removed, but none of these can be, so nothing changes.\n"
                : removable.Count < units.Count
                    ? $"{Pct(s.Remove)}: one of these is removed at random: {string.Join(", ", removable.Select(u => UnitName(target, u)))}.\n"
                    : $"{Pct(s.Remove)}: one random property below is removed.\n";
            var overMaxText = MadnessOverMaxText(s);
            return $"One of these happens, and then the item is Tainted: no bag will work on it again.\n"
                + $"{Pct(s.Nothing)}: nothing changes.\n"
                + $"{Pct(s.OverMax)}: one random property below is {overMaxText}.\n"
                + $"{Pct(s.Shift)}: every property below moves up to {RangePct(s.ShiftRange)} of its range up or down from where it is, and may pass its maximum.\n"
                + removeLine
                + $"It can touch: {names}. A locked property is left alone.";
        }

        // -- Bag of Tempering (owner 2026-11-26) ---------------------------------------------------

        /// <summary>One Tempering roll: up one sub-grade, no change, or down one.</summary>
        internal enum TemperStep { None, Up, Down }

        /// <summary>The defaults when the tier Default sets nothing (owner 2026-11-26): 60% up, 10% down, 30% no change.</summary>
        internal const double TemperingUpDefault = 0.60, TemperingDownDefault = 0.10;

        /// <summary>The top rung by default: rung 14 = A+ (rungs count up from F- = 0; S would be 15 and is never reached).</summary>
        internal const int TemperingMaxRungDefault = 14;

        /// <summary>The highest rung <c>bag_tempering_max_subgrade</c> may name: A+. S stays a drop-only chase.</summary>
        internal const int TemperingMaxRung = 14;

        /// <summary>A rung (0 = F- .. 14 = A+) as an index into <see cref="WeaponScalingManager.SubGradeBands"/> (0 = S .. 15 = F-).</summary>
        internal static int SubGradeIndexOfRung(int rung)
            => WeaponScalingManager.SubGradeBands.Length - 1 - Math.Clamp(rung, 0, TemperingMaxRung);

        /// <summary>Where <paramref name="quality"/> sits in <see cref="WeaponScalingManager.SubGradeBands"/> (0 = S .. 15 = F-).</summary>
        internal static int SubGradeIndex(int quality)
            => Array.IndexOf(WeaponScalingManager.SubGradeBands, WeaponScalingManager.GetSubGradeBand(quality));

        /// <summary>
        /// The Tempering knobs from the item's tier Default (<paramref name="p"/>, null = none): the up and down chances and the top
        /// sub-grade as a <see cref="WeaponScalingManager.SubGradeBands"/> index. Unset or not a real number = the defaults. Up is
        /// held to 0..1 and down to 0..(1 - up), so the two never add past 1; the top is rounded and held to F-..A+.
        /// </summary>
        internal static (double Up, double Down, int TopIndex) TemperingSettings(EvaluatedProfile p, int tier)
        {
            double Read(string stat, double fallback)
            {
                var v = p?.GetT(stat, fallback, tier) ?? fallback;
                return double.IsFinite(v) ? v : fallback;
            }
            var up = Math.Clamp(Read(ZoneStat.BagTemperingUpChance, TemperingUpDefault), 0.0, 1.0);
            var down = Math.Clamp(Read(ZoneStat.BagTemperingDownChance, TemperingDownDefault), 0.0, 1.0 - up);
            var rung = (int)Math.Round(Math.Clamp(Read(ZoneStat.BagTemperingMaxSubgrade, TemperingMaxRungDefault), 0.0, TemperingMaxRung),
                MidpointRounding.AwayFromZero);
            return (up, down, SubGradeIndexOfRung(rung));
        }

        /// <summary>The step a roll in [0, 1) lands on: below <paramref name="up"/> = up, then <paramref name="down"/> more = down,
        /// the rest = no change.</summary>
        internal static TemperStep RollTemperStep(double roll, double up, double down)
            => roll < up ? TemperStep.Up : roll < up + down ? TemperStep.Down : TemperStep.None;

        /// <summary>
        /// The Weapon Grade quality after a Tempering <paramref name="step"/>. Up and down move ONE sub-grade
        /// (<see cref="WeaponScalingManager.SubGradeBands"/>: F- -> F -> F+ -> D- ... -> A+) and LAND ON THE NEW SUB-GRADE'S
        /// MIDPOINT (QMid) - the point the weapon ladder prices each sub-grade at (ResolveScriptK is exact at the midpoints), so
        /// a tempered C+ deals what the ladder says a C+ deals, not the bottom of its band. Never above <paramref name="topIndex"/>,
        /// never below F-, and an S weapon is never touched. "No change", and a step the bounds stop, keep the quality exactly
        /// as it was.
        /// </summary>
        internal static int TemperedQuality(int quality, TemperStep step, int topIndex)
        {
            var bands = WeaponScalingManager.SubGradeBands;
            var last = bands.Length - 1;
            var i = SubGradeIndex(quality);
            if (i <= 0)
                return quality;                       // S: never touched (refused before the bag is used)
            topIndex = Math.Clamp(topIndex, 1, last); // never S
            var next = step switch { TemperStep.Up => i - 1, TemperStep.Down => i + 1, _ => i };
            if (next < topIndex)
                next = i;                             // never above the top (an item already above it does not move up)
            if (next > last)
                next = last;                          // never below F-
            return next == i ? quality : bands[next].QMid;
        }

        /// <summary>Why Tempering refuses this Weapon Grade before anything is used, or null: an S weapon is never changed, and a
        /// weapon at or above the top can only stay or go down, so the bag would be a pure loss.</summary>
        internal static string TemperingRefusal(int quality, int topIndex, string itemName)
        {
            var bands = WeaponScalingManager.SubGradeBands;
            var i = SubGradeIndex(quality);
            if (i <= 0)
                return $"Your {itemName} has an S Weapon Grade. A Bag of Tempering never changes an S weapon.";
            var top = Math.Clamp(topIndex, 1, bands.Length - 1);
            if (i <= top)
                return $"Your {itemName} is already at {bands[i].Grade}. A Bag of Tempering raises a Weapon Grade only up to {bands[top].Grade}.";
            return null;
        }

        /// <summary>The confirm text of Tempering: the step's outcomes for this weapon, with the live chances.</summary>
        private static string TemperingConfirmText(WorldObject target)
        {
            var quality = target.GetProperty(PropertyInt.WeaponAugScaleQuality) ?? 0;
            var tier = ZoneStatResolver.TierOf(target);
            var (up, down, top) = TemperingSettings(TierDefaultProfile(tier), tier);
            var bands = WeaponScalingManager.SubGradeBands;
            var i = Math.Clamp(SubGradeIndex(quality), 1, bands.Length - 1);
            static string Pct(double x) => FormattableString.Invariant($"{x * 100:0.#}%");
            var now = bands[i].Grade;
            var upTo = bands[i - 1].Grade;
            if (i >= bands.Length - 1)   // F-: there is no lower step, so a "down" roll changes nothing
                return $"Its Weapon Grade ({now}) moves one step: {Pct(up)} up to {upTo}, otherwise no change (it is already the lowest). A Bag of Tempering never goes above {bands[top].Grade}.";
            return $"Its Weapon Grade ({now}) moves one step: {Pct(up)} up to {upTo}, {Pct(1.0 - up - down)} no change, {Pct(down)} down to {bands[i + 1].Grade}. A Bag of Tempering never goes above {bands[top].Grade}.";
        }

        private static Plan Refuse(out string refusal, string text)
        {
            refusal = text;
            return null;
        }

        private static string NothingToChange(WorldObject target) => $"Your {target.Name} has no properties the bag can change.";

        private static string AtCap(WorldObject target, int cap) => $"Your {target.Name} already has as many properties as its tier allows ({cap}).";

        private static Unit Pick(List<Unit> units) => units[ThreadSafeRandom.Next(0, units.Count - 1)];

        /// <summary>
        /// Second Thoughts' pick (owner 2026-11-26): the index of the LOWEST grade in <paramref name="grades"/>. TIE RULE: the
        /// first of the equally low ones, in the order given - the item's record order, which is the order the armour appraisal
        /// lists its lines (and the order they were stamped). Deterministic, so the confirm prompt can name the property the
        /// bag will reroll and the bag rerolls exactly that one. -1 when empty. Weapons compare their cards' grades the same way.
        /// </summary>
        internal static int LowestIndex(IReadOnlyList<int> grades)
        {
            var best = -1;
            for (var i = 0; i < grades.Count; i++)
                if (best < 0 || grades[i] < grades[best])   // strictly lower: an equal grade later on never wins the tie
                    best = i;
            return best;
        }

        /// <summary>The unit Second Thoughts rerolls: the lowest-graded of <paramref name="free"/> (record order), see <see cref="LowestIndex"/>.</summary>
        private static Unit LowestUnit(WorldObject target, List<Unit> free)
        {
            var record = ZoneStatResolver.Read(target);
            var grades = free.Select(u => record.Where(l => l.Key == u.Key).Select(l => l.Grade).FirstOrDefault()).ToList();
            return free[LowestIndex(grades)];
        }

        /// <summary>The record key Second Thoughts would reroll on <paramref name="target"/> now, or null when nothing can change.</summary>
        internal static int? SecondThoughtsKey(WorldObject target)
        {
            var free = FreeUnits(target);
            return free.Count == 0 ? null : LowestUnit(target, free).Key;
        }

        /// <summary>The confirm text of Second Thoughts: names the property it will reroll, with its value now.</summary>
        private static string SecondThoughtsConfirmText(WorldObject target)
        {
            var free = FreeUnits(target);
            if (free.Count == 0)
                return "Its lowest property will be rerolled at the tier's normal odds. It can go up or down.";
            var u = LowestUnit(target, free);
            return $"Its lowest property, {Describe(target, u.Key, u.Def, u.Card, GradeOf(target, u.Key))}, will be rerolled at the tier's normal odds. It can go up or down. A locked property is never picked.";
        }

        /// <summary>The tier Default flattened to an EvaluatedProfile - the same table the drop path and
        /// /wsforge read chances, caps and bands from. Like WeaponScalingCommands.TierDefaultProfile, plus the
        /// armour slot rules (CustomModifierSlots) that weapons never need - kept separate so /wsforge is unchanged.</summary>
        private static EvaluatedProfile TierDefaultProfile(int tier)
        {
            var def = ZoneControlManager.GetAnchoredDefaultProfile(tier);
            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (def?.Stats != null)
                foreach (var kv in def.Stats)
                    if (kv.Value != null)
                        values[kv.Key] = kv.Value.Evaluate(1);
            return new EvaluatedProfile($"T{tier} Default", 1, ZoneVariant.Minion, values,
                modifierSlots: def?.CustomModifierSlots, weaponCardToggles: def?.CustomWeaponCards);
        }

        /// <summary>The tier's modifier cap, or int.MaxValue when none is authored (uncapped, as at drop).</summary>
        private static int CapAt(EvaluatedProfile p, string capStat, int tier)
            => p.Has(capStat) ? Math.Max(0, (int)Math.Round(p.GetT(capStat, 0.0, tier), MidpointRounding.AwayFromZero)) : int.MaxValue;

        /// <summary>A line or card's loot chance at this tier, 0 when unset (never rolls at drop, never offered here).</summary>
        private static double ChanceAt(EvaluatedProfile p, string chanceStat, int tier)
            => Math.Clamp(p.GetT(chanceStat, 0.0, tier), 0.0, 1.0);

        /// <summary>A random index picked by weight. Never a zero-weight entry; -1 only when no weight is above 0
        /// (every caller checks the total first).</summary>
        internal static int WeightedIndex(List<double> weights)
        {
            var total = weights.Sum();
            var roll = ThreadSafeRandom.Next(0.0f, 1.0f) * total;
            for (int i = 0; i < weights.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0.0)
                    return i;
            }
            return weights.FindLastIndex(w => w > 0.0);   // floating-point tail: never a zero-weight entry
        }

        /// <summary>Plan adding one property (Gain, Trade, Chaos' bonus). <paramref name="removingKey"/> is a key
        /// the same use removes first: it frees a cap slot and is never added back. 0 = none.</summary>
        private static Op PlanAdd(WorldObject target, int tier, int removingKey, out string refusal)
            => PlanAdd(target, TierDefaultProfile(tier), tier, AddState.Of(target, skipCap: removingKey != 0), out refusal);

        private static Op PlanAdd(WorldObject target, EvaluatedProfile p, int tier, AddState state, out string refusal)
            => IsWeapon(target) ? PlanAddCard(target, p, tier, state, out refusal) : PlanAddLine(target, p, tier, state, out refusal);

        /// <summary>
        /// What the item will carry when the next planned add lands: its record keys, imbue bits and proc slot, after the
        /// removals and adds planned before it in the same use. A single add (Gain, Exchange, Chaos) plans from the item as
        /// it is - Exchange's removed key stays in Present, so it is never added back. Alteration and Reforging plan several
        /// adds after removing what they replace (<see cref="PlanReplacements"/>), so a replaced property may come back and
        /// a replaced rend frees the rend slot. SkipCap: the use never grows the count (Exchange, the replacing bags).
        /// </summary>
        private sealed class AddState
        {
            public HashSet<int> Present;
            public ImbuedEffectType Imbues;
            public bool ProcSlotTaken;
            public bool SkipCap;

            public static AddState Of(WorldObject target, bool skipCap) => new AddState
            {
                Present = ZoneStatResolver.Read(target).Select(r => r.Key).ToHashSet(),
                Imbues = target.GetImbuedEffects(),
                ProcSlotTaken = target.ProcSpell != null,
                ProcIsLines = target.ProcSpell != null && ZoneLootMutator.IsJewelProcSpell(target.ProcSpell.Value),
                SkipCap = skipCap,
            };

            /// <summary>The proc on the item is the Cast on Strike line's own (removing the line clears it); a retail or
            /// crafted proc is not, and stays taken (review 2026-11-26).</summary>
            public bool ProcIsLines;

            /// <summary>A planned removal: the key, and the imbue bits or proc slot its card or line holds.</summary>
            public void Remove(Unit u)
            {
                Present.Remove(u.Key);
                if (u.Card == ZoneStatResolver.SpecRendPower)
                    Imbues &= ~DamageRends;
                else if (u.Card == ZoneStatResolver.SpecArmorRend)
                    Imbues &= ~ImbuedEffectType.ArmorRending;
                else if (u.Card == null && u.Key == ZoneModifiers.JewelProcKey && ProcIsLines)
                    ProcSlotTaken = false;
            }

            /// <summary>A planned add, so the next pick sees it (never the same key twice, never a second rend).</summary>
            public void Add(Op op)
            {
                Present.Add(op.Key);
                if (op.Card == ZoneStatResolver.SpecRendPower)
                    Imbues |= op.Rend;
                else if (op.Card == ZoneStatResolver.SpecArmorRend)
                    Imbues |= ImbuedEffectType.ArmorRending;
                else if (op.Card == null && op.Key == ZoneModifiers.JewelProcKey)
                    ProcSlotTaken = true;
            }
        }

        /// <summary>True when every line a bag could still add is a Chase line (none of the ordinary ones is left).</summary>
        internal static bool OnlyChaseLeft(IEnumerable<ZoneModifiers.Def> candidates)
            => candidates.All(d => d.Class == ZoneModifiers.ModifierClass.Chase);

        /// <summary>True when the ordinary (non-Chase) lines among <paramref name="candidates"/> can fill <paramref name="slots"/>
        /// new lines on their own, so a multi-line plan can never be left with only Chase lines to pick from.</summary>
        internal static bool CanFillWithoutChase(IEnumerable<ZoneModifiers.Def> candidates, int slots)
            => candidates.Count(d => d.Class != ZoneModifiers.ModifierClass.Chase) >= slots;

        /// <summary>
        /// How many new properties a replacing bag puts on after taking <paramref name="removed"/> off, when the item keeps
        /// <paramref name="kept"/> it can't change (locked line, Slayer, Reinforced, Split Arrows ...) and its tier allows
        /// <paramref name="cap"/> (owner 2026-11-26): Alteration always puts on ONE (an item with two changeable properties
        /// loses the second, as Path of Exile's alteration on a two-mod item can); Reforging puts back as many as it took.
        /// Never past the cap: an item above it (a legacy drop, or one made before a cap cut) comes down to the cap
        /// (review 2026-11-26). 0 = the bag can't add anything.
        /// </summary>
        internal static int ReplacementCount(EssenceKind kind, int removed, int kept, int cap)
        {
            var want = kind == EssenceKind.Alteration ? Math.Min(1, removed) : removed;
            var room = cap == int.MaxValue ? int.MaxValue : Math.Max(0, cap - kept);
            return Math.Min(want, room);
        }

        /// <summary><see cref="ReplacementCount"/> for this item: its shown count, what stays, and its tier's live cap.</summary>
        private static int ReplacementCountFor(WorldObject target, EssenceKind kind, int tier, int removed)
        {
            var cap = CapAt(TierDefaultProfile(tier), IsWeapon(target) ? ZoneStat.WeaponModifierCap : ZoneStat.ArmorModifierCap, tier);
            return ReplacementCount(kind, removed, ShownCount(target) - removed, cap);
        }

        /// <summary>The confirm text of Alteration and Reforging: what is replaced and what the item ends with, worked out
        /// from this item (review 2026-11-26: "an item with two ends up with one" was wrong when Reinforced, a locked line,
        /// a Slayer, Split Arrows, Cleave or Cast on Strike also count), plus warnings for a Rending and for tinkers.</summary>
        private static string ReplaceConfirmText(EssenceKind kind, WorldObject target)
        {
            var replace = ReplaceUnits(target);
            var shown = ShownCount(target);
            var adds = ReplacementCountFor(target, kind, ZoneStatResolver.TierOf(target), replace.Count);
            var after = shown - replace.Count + adds;
            string Props(int n) => n == 1 ? "1 property" : $"{n} properties";

            var text = kind == EssenceKind.Alteration
                ? $"{Props(replace.Count)} will be removed and 1 new random property added, so it will have {Props(after)}. This bag only works on an item with one or two properties."
                : replace.Count == 1
                    ? $"1 property will be replaced with a new random one, so it will have {Props(after)}."
                    : $"{Props(replace.Count)} will be replaced with new random ones, so it will have {Props(after)}.";
            if (kind == EssenceKind.Reforging && adds < replace.Count)
                text += " It has more properties than its tier now allows, so it comes down to the limit.";
            text += " A locked property, a Slayer and built-in ones such as Reinforced or Split Arrows stay.";
            if (replace.Any(u => u.Card == ZoneStatResolver.SpecRendPower))
                text += " Its Rending is replaced too: it can roll back on, with a new value, but that isn't certain.";
            var rare = replace.Where(u => u.Def?.Class == ZoneModifiers.ModifierClass.Chase).Select(u => u.Def.Name).ToList();
            if (rare.Count > 0)
                text += $" {string.Join(" and ", rare)} {(rare.Count == 1 ? "is" : "are")} among the rarest properties and will very likely not come back.";
            var tinkered = ZoneStatResolver.ReadTinkerBonus(target).Keys.ToHashSet();
            if (replace.Any(u => u.Def?.Ints != null && u.Def.Ints.Any(i => tinkered.Contains(i.Item1))))
                text += " Tinkering on the replaced properties is lost.";
            return text;
        }

        /// <summary>
        /// Alteration and Reforging: <paramref name="count"/> new properties after <paramref name="replace"/> come off, picked one
        /// at a time through the same planners as Memory (same rules, same loot-chance weights), each seeing the removals and
        /// the adds before it. A replaced property may roll back on. Null, with nothing consumed, when the item can't take
        /// that many.
        /// </summary>
        private static List<Op> PlanReplacements(WorldObject target, int tier, List<Unit> replace, int count, out string refusal)
        {
            var p = TierDefaultProfile(tier);
            var state = AddState.Of(target, skipCap: true);
            foreach (var u in replace)
                state.Remove(u);

            // Decide BEFORE any random pick whether the ordinary lines alone can fill every slot (CodeRabbit 2026-11-26). A refusal
            // that depended on the picks would be free to retry: a run where a Chase line happened to come up early would succeed,
            // so repeated tries could push a Chase line onto every bag actually used. Each ordinary pick uses up one ordinary
            // line and a Chase pick uses none, so "ordinary lines >= slots" guarantees the planner never meets OnlyChaseLeft.
            if (!IsWeapon(target) && !CanFillWithoutChase(LineCandidates(target, p, tier, state).Defs, count))
            {
                refusal = $"Your {target.Name} can't take {count} new {(count == 1 ? "property" : "properties")} without forcing one of the rarest ones, so the bag won't change it.";
                return null;
            }

            var adds = new List<Op>();
            for (var i = 0; i < count; i++)
            {
                var op = PlanAdd(target, p, tier, state, out refusal);
                if (op == null)
                {
                    refusal = count == 1
                        ? $"A bag can't find a new property for your {target.Name}, so it won't change it."
                        : $"A bag can't find {count} new properties for your {target.Name}, so it won't change it.";
                    return null;
                }
                state.Add(op);
                adds.Add(op);
            }
            refusal = null;
            return adds;
        }

        private static Op PlanAddLine(WorldObject target, EvaluatedProfile p, int tier, AddState state, out string refusal)
        {
            refusal = null;

            var cap = CapAt(p, ZoneStat.ArmorModifierCap, tier);
            if (!state.SkipCap && LineCount(target) >= cap)   // Exchange and the replacing bags remove first: the count never grows
            {
                refusal = AtCap(target, cap);
                return null;
            }

            var (candidates, weights) = LineCandidates(target, p, tier, state);

            if (candidates.Count == 0)
            {
                refusal = $"There is nothing more a bag can add to your {target.Name}.";
                return null;
            }

            // A Chase line's tiny chance only means "rare" next to the ordinary lines. When only Chase lines are left, the weighted
            // pick would hand one out every time (review 2026-11-26: a capped piece, Reforged, always gained Crit Chance; repeated
            // Reforging fished Cast on Strike). So a bag refuses then, before anything is used - a deterministic refusal, so
            // retrying can't fish for it either.
            if (OnlyChaseLeft(candidates))
            {
                refusal = $"Your {target.Name} already has every property a bag can add except the rarest ones ({string.Join(", ", candidates.Select(d => d.Name))}), and a bag never forces one of those.";
                return null;
            }

            var pick = candidates[WeightedIndex(weights)];
            return new Op { Action = OpAction.Add, Key = pick.Key, Def = pick, Grade = ZoneStatResolver.RollGrade(tier), Profile = p };
        }

        /// <summary>The lines a bag may add to this piece now, with their loot chances as weights: not protected, not already
        /// there (or planned), allowed on the slot, the one proc slot free for Cast on Strike, and a chance above 0.</summary>
        private static (List<ZoneModifiers.Def> Defs, List<double> Weights) LineCandidates(WorldObject target, EvaluatedProfile p, int tier, AddState state)
        {
            var piece = ZoneModifiers.PieceMask(target);
            var defs = new List<ZoneModifiers.Def>();
            var weights = new List<double>();
            foreach (var def in ZoneModifiers.Catalog.Values)
            {
                if (IsProtected(def) || state.Present.Contains(def.Key))
                    continue;
                if (!ZoneModifiers.SlotAllowed(ZoneModifiers.EffectiveSlotMask(def, p.ModifierSlots), piece))
                    continue;
                if (def.Key == ZoneModifiers.JewelProcKey && state.ProcSlotTaken)
                    continue;   // the one proc slot is taken (retail / crafted proc, or a proc line this use keeps or adds)
                var chance = ChanceAt(p, ZoneModifiers.LineChanceStat(def.Key), tier);
                if (chance <= 0)
                    continue;
                defs.Add(def);
                weights.Add(chance);
            }
            return (defs, weights);
        }

        /// <summary>Lines an armour / jewelry / clothing piece carries, as a drop's armor_modifier_cap counts them: every line
        /// that uses a slot (AllUnits - the locked one included), plus Reinforced (earned and frozen, never in the record).</summary>
        private static int LineCount(WorldObject target)
        {
            // on a RARE, Reinforced is a built-in (always there, outside the cap) and uses no slot - the count stays "cap of cap"
            var reinforced = (target.GetProperty((PropertyInt)ZoneModifiers.ReinforcedRank) ?? 0) > 0
                && ZoneRare.ReinforcedUsesASlot(ZoneRare.TierOfRare(target));
            return AllUnits(target).Count + (reinforced ? 1 : 0);
        }

        /// <summary>Card kinds a weapon carries, as a drop's weapon_modifier_cap counts them.</summary>
        private static int CardCount(WorldObject target)
        {
            var present = ZoneStatResolver.Read(target).Select(r => r.Key).ToHashSet();
            var count = EssenceWeaponCards.Count(c => present.Contains(c.Key));
            // Cast on Strike counts only when it is a card (in the record): a player's own Ring Glyph proc is not one
            if (present.Contains(ZoneStatResolver.WeaponProcArcDamageKey)) count++;
            if (present.Contains(ZoneStatResolver.WeaponProcRingDamageKey)) count++;
            // Cleave counts when a card changed the base weapon's own (two-handers carry Cleaving natively). A drop's card always
            // lands above it since 2026-10-04 (ZoneLootMutator); "!=" also counts an older drop that rolled below it
            var cleave = target.GetProperty(PropertyInt.Cleaving) ?? 0;
            var baseCleave = DatabaseManager.World.GetCachedWeenie(target.WeenieClassId)?.GetProperty(PropertyInt.Cleaving) ?? 0;
            if (cleave > 1 && cleave != baseCleave) count++;
            if (target.GetProperty(PropertyBool.SplitArrows) == true) count++;
            return count;
        }

        /// <summary>
        /// True when the weapon carries an imbue the player put on it themselves: Critical Strike, Crippling Blow and the
        /// like, or an Armor Rending / rend imbue bit with no card behind it in the record (<paramref name="present"/> is
        /// the record's keys). The one test both a bag and the forge use before adding a card that sets an imbue bit.
        /// </summary>
        private static bool HasOwnImbue(ImbuedEffectType imbues, ICollection<int> present)
        {
            var ownOther = (imbues & ~(DamageRends | ImbuedEffectType.ArmorRending)) != 0;
            var ownArmorRend = (imbues & ImbuedEffectType.ArmorRending) != 0 && !present.Contains(ZoneStatResolver.SpecArmorRend.Key);
            var ownRend = (imbues & DamageRends) != 0 && !present.Contains(ZoneStatResolver.SpecRendPower.Key);
            return ownOther || ownArmorRend || ownRend;
        }

        /// <summary>
        /// False when forging must not hand <paramref name="main"/> the Armor Rending property from the other weapon: it
        /// carries the player's own imbue. The record does not say which imbue bit a card added, so an Armor Rending
        /// card next to the player's own Armor Rending would take the imbue with it when the card is later lost - the
        /// reason a bag never makes this pairing either (PlanAddCard). A weapon that already has the card is not asked.
        /// </summary>
        internal static bool ForgeCanGainArmorRend(WorldObject main)
        {
            if (main == null || !IsWeapon(main))
                return true;
            var present = ZoneStatResolver.Read(main).Select(r => r.Key).ToHashSet();
            return !HasOwnImbue(main.GetImbuedEffects(), present);
        }

        private static Op PlanAddCard(WorldObject target, EvaluatedProfile p, int tier, AddState state, out string refusal)
        {
            refusal = null;
            var physical = target is MeleeWeapon || target is MissileLauncher;
            var present = state.Present;

            var cap = CapAt(p, ZoneStat.WeaponModifierCap, tier);
            if (!state.SkipCap && CardCount(target) >= cap)   // Exchange and the replacing bags remove first: the count never grows
            {
                refusal = AtCap(target, cap);
                return null;
            }

            // An essence never adds a rend next to one the weapon already carries: the record does not say which
            // bit a card added, so a later Loss would strip the player's own imbue too. Same for Armor Rending.
            // Nor next to ANY imbue the player put on the weapon themselves - Critical Strike, Crippling Blow, and also an Armor
            // Rending or rend imbue (an imbue bit with no card behind it in the record). The drop path never makes that pairing:
            // an imbue recipe needs an un-imbued weapon, so a weapon with a Rending or Armor Rending card can't take an imbue, and
            // a bag must not add one of those cards next to an imbue either (review 2026-10-04: an own Armor Rending + a Rending
            // card = 4 effects on a 3-slot weapon).
            var imbues = state.Imbues;
            var ownImbue = HasOwnImbue(imbues, present);
            var rends = ownImbue || (imbues & DamageRends) != 0 ? new List<ImbuedEffectType>() : ZoneLootMutator.GetMatchingRends(target.W_DamageType);
            var armorRendFree = !ownImbue && (imbues & ImbuedEffectType.ArmorRending) == 0;

            // Slayer is left out: its type comes from a monster (a Bag of Vengeance does that).
            var options = new List<(ZoneStatResolver.WeaponSpecial Card, string ChanceStat, bool Eligible)>
            {
                (ZoneStatResolver.SpecRendPower, ZoneStat.WeaponImbueChance, rends.Count > 0),
                (ZoneStatResolver.SpecBite, ZoneStat.WeaponBiteChance, true),
                (ZoneStatResolver.SpecCrush, ZoneStat.WeaponCrushChance, true),
                (ZoneStatResolver.SpecArmorRend, ZoneStat.WeaponArmorRendChance, physical && armorRendFree),
                (ZoneStatResolver.SpecShieldCleave, ZoneStat.WeaponShieldCleaveChance, physical),
                (ZoneStatResolver.SpecSpellArmorRend, ZoneStat.WeaponSpellArmorRendChance, target is Caster),
            };

            var candidates = new List<ZoneStatResolver.WeaponSpecial>();
            var weights = new List<double>();
            foreach (var (card, chanceStat, eligible) in options)
            {
                if (!eligible || present.Contains(card.Key) || !p.WeaponCardEnabled(chanceStat))
                    continue;
                var chance = ChanceAt(p, chanceStat, tier);
                if (chance <= 0)
                    continue;
                candidates.Add(card);
                weights.Add(chance);
            }

            if (candidates.Count == 0)
            {
                refusal = $"There is nothing more a bag can add to your {target.Name}.";
                return null;
            }

            var pick = candidates[WeightedIndex(weights)];
            var op = new Op { Action = OpAction.Add, Key = pick.Key, Card = pick, Grade = ZoneStatResolver.RollGrade(tier) };
            if (pick == ZoneStatResolver.SpecRendPower)
                op.Rend = rends[ThreadSafeRandom.Next(0, rends.Count - 1)];
            return op;
        }

        private static Plan PlanHunt(WorldObject essence, WorldObject target, int tier, Plan plan, out string refusal)
        {
            refusal = null;
            var stored = essence.GetProperty(PropertyInt.GearEssenceHuntCreatureType);
            if (stored == null || (CreatureType)stored.Value == CreatureType.Invalid)
                return Refuse(out refusal, $"This {essence.Name} does not remember a monster type.");
            plan.Species = (CreatureType)stored.Value;

            if (target.GetProperty(PropertyBool.SlayerAllCreatures) == true)
                return Refuse(out refusal, $"Your {target.Name} already slays all creatures.");

            // a Slayer card, or a slayer the base weapon already had: change the type, keep the strength
            var hasSlayer = ZoneStatResolver.Read(target).Any(l => l.Key == ZoneStatResolver.WeaponSlayerKey) || target.SlayerCreatureType != null;
            if (hasSlayer)
            {
                if (LockedKey(target) == ZoneStatResolver.WeaponSlayerKey)
                    return Refuse(out refusal, $"The Slayer on your {target.Name} is locked.");
                if (target.SlayerCreatureType == plan.Species)
                    return Refuse(out refusal, $"Your {target.Name} is already {SlayerPhrase(plan.Species)}.");
                return plan;   // keep the slayer's strength, change only its type
            }

            // a NEW Slayer card follows the tier's weapon-card switch and chance, as Memory's cards do
            var p = TierDefaultProfile(tier);
            if (!p.WeaponCardEnabled(ZoneStat.WeaponSlayerChance) || ChanceAt(p, ZoneStat.WeaponSlayerChance, tier) <= 0)
                return Refuse(out refusal, $"Slayers can't be added to tier {tier} weapons right now.");
            var cap = CapAt(p, ZoneStat.WeaponModifierCap, tier);
            if (CardCount(target) >= cap)
                return Refuse(out refusal, AtCap(target, cap));
            plan.Ops.Add(new Op { Action = OpAction.Add, Key = ZoneStatResolver.WeaponSlayerKey, Card = ZoneStatResolver.SpecSlayer, Grade = ZoneStatResolver.RollGrade(tier) });
            return plan;
        }

        // -- applying ----------------------------------------------------------------------------

        /// <summary>Confirmation "yes": find both items again, re-check, plan, consume, then change the item.</summary>
        private static void Apply(Player player, uint essenceGuid, uint targetGuid, string promptedState)
        {
            if (player.IsBusy)
            {
                player.SendTransientError("You are too busy to use the bag right now.");
                return;
            }

            var essence = player.FindObject(essenceGuid, Player.SearchLocations.MyInventory);
            var target = player.FindObject(targetGuid, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems);

            string refusal;
            Plan plan;
            try
            {
                refusal = Verify(player, essence, target);
                // the item changed under the open prompt (another bag, a tinker, a trade back): what the player agreed to is
                // no longer what would happen, so nothing is used and they ask again
                if (refusal == null && promptedState != null && PromptState(essence, target) != promptedState)
                    refusal = $"Your {target.Name} changed since you chose the bag. Use the bag again.";
                plan = refusal == null ? BuildPlan(essence, target, out refusal) : null;
            }
            catch (Exception ex)
            {
                // nothing is consumed yet
                log.Error($"[SALVAGE BAG] {player.Name} could not apply {essence?.Name} on {target?.Name}: {ex}");
                player.SendTransientError("Something went wrong with the bag. Please tell an admin.");
                return;
            }
            if (refusal != null)
            {
                player.SendTransientError(refusal);
                return;
            }

            var essenceName = essence.Name;
            var essenceWcid = essence.WeenieClassId;
            var huntSpecies = essence.GetProperty(PropertyInt.GearEssenceHuntCreatureType);

            // transaction-first: the essence is gone before the item changes
            if (!player.TryConsumeFromInventoryWithNetworking(essence, 1))
            {
                player.SendTransientError($"Could not use the {essenceName}.");
                return;
            }
            // a stack that is left is saved now, like the item below - a crash before the next player save must not
            // bring the used essence back next to the changed item
            if (!essence.IsDestroyed)
                essence.SaveBiotaToDatabase();

            var before = Fingerprint(target);

            string result;
            try
            {
                result = Execute(target, plan);
                if (plan.Kind != EssenceKind.Edge)   // the appraisal's Tainted line reads Worked on an emptied piece
                    target.SetProperty(PropertyBool.GearEssenceWorked, true);
                target.ChangesDetected = true;
                target.SaveBiotaToDatabase();
            }
            catch (Exception ex)
            {
                // The essence is already gone. Refund it ONLY when the item is provably unchanged - a refund after a change
                // that landed would hand out the effect AND a new essence. Either way the item's state is saved and resent,
                // so client, memory and DB agree, and the log carries enough to review it.
                var untouched = Fingerprint(target) == before;
                var refunded = untouched && Refund(player, essenceWcid, huntSpecies, essenceName);
                log.Error($"[SALVAGE BAG] {player.Name} used {essenceName} ({essenceWcid}) on {target.Name} (0x{target.Guid}) and it failed after the bag was consumed - {(untouched ? (refunded ? "item unchanged, essence refunded" : "item unchanged, REFUND FAILED") : "item changed part-way, NOT refunded")}. Record now: {target.GetProperty(PropertyString.ZcModifiers)}. {ex}");
                try
                {
                    if (!untouched && plan.Kind != EssenceKind.Edge)
                        target.SetProperty(PropertyBool.GearEssenceWorked, true);   // the appraisal keeps its block on a part-emptied piece
                    target.ChangesDetected = true;
                    target.SaveBiotaToDatabase();
                    player.EnqueueBroadcast(new GameMessageUpdateObject(target));
                    player.MoveItemToFirstContainerSlot(target);
                }
                catch (Exception saveEx) { log.Error($"[SALVAGE BAG] saving {target.Name} (0x{target.Guid}) after the failure also failed: {saveEx}"); }
                player.SendTransientError(refunded
                    ? "Something went wrong with the bag. You got it back - please tell an admin."
                    : "Something went wrong with the bag. Please tell an admin.");
                return;
            }

            player.EnqueueBroadcast(new GameMessageUpdateObject(target));
            player.MoveItemToFirstContainerSlot(target);

            player.SendMessage($"Your {essenceName} {result}", ChatMessageType.Craft);
            log.Info($"[SALVAGE BAG] {player.Name} used {essenceName} on {target.Name} (0x{target.Guid}): {result}");
        }

        /// <summary>What a confirm prompt promised: the item's state; for Madness and Tempering the odds the prompt showed; and for Alteration and Reforging how many properties
        /// go and come (the tier cap can change while the prompt is open - CodeRabbit 2026-11-26). Apply refuses when it
        /// differs, so a bag is never used on anything but what the player agreed to.</summary>
        private static string PromptState(WorldObject essence, WorldObject target)
        {
            var state = Fingerprint(target);
            var kind = KindOf(essence.WeenieClassId);
            var tier = ZoneStatResolver.TierOf(target);
            // Madness and Tempering show their odds in the prompt; an admin change to them under an open prompt must not apply
            // odds the player never saw (CodeRabbit 2026-11-26). The rolls themselves stay fresh at "Yes".
            if (kind == EssenceKind.Chaos)
            {
                var m = MadnessSettingsFrom(TierDefaultProfile(tier), tier);
                return FormattableString.Invariant($"{state}|madness {m.Nothing:R} {m.OverMax:R} {m.Shift:R} {m.Remove:R} {m.OverMaxMin:R} {m.OverMaxMax:R} {m.ShiftRange:R}");
            }
            if (kind == EssenceKind.Edge)
            {
                var (up, down, top) = TemperingSettings(TierDefaultProfile(tier), tier);
                return FormattableString.Invariant($"{state}|tempering {up:R} {down:R} {top}");
            }
            if (!IsReplaceKind(kind))
                return state;
            var removed = ReplaceUnits(target).Count;
            return $"{state}|{removed}>{ReplacementCountFor(target, kind, ZoneStatResolver.TierOf(target), removed)}";
        }

        /// <summary>What an essence can change on an item, as one string: equal before and after = nothing changed. Tinker
        /// bonuses and the proc spell are in it too (lab retest 2026-11-26): a tinker can land while a bag's prompt is open,
        /// and the prompt's tinker warning must match what the bag then removes.</summary>
        private static string Fingerprint(WorldObject wo)
            => string.Join("|", wo.GetProperty(PropertyString.ZcModifiers), (int)wo.ImbuedEffect, (int)wo.W_DamageType,
                wo.GetProperty(PropertyInt.WeaponAugScaleQuality), (int?)wo.SlayerCreatureType, LockedKey(wo), IsTainted(wo), wo.Name,
                wo.GetProperty(PropertyString.ZcTinkerBonus), wo.ProcSpell);

        /// <summary>A replacement essence after a failed use (the Hunt keeps its monster type and name). True = it is in the pack.</summary>
        private static bool Refund(Player player, uint wcid, int? huntSpecies, string name)
        {
            try
            {
                var refund = WorldObjectFactory.CreateNewWorldObject(wcid);
                if (refund == null)
                    return false;
                if (huntSpecies.HasValue)
                {
                    refund.SetProperty(PropertyInt.GearEssenceHuntCreatureType, huntSpecies.Value);
                    refund.Name = name;
                }
                if (player.TryCreateInInventoryWithNetworking(refund))
                    return true;
                refund.Destroy();
                log.Error($"[SALVAGE BAG] refund of {name} ({wcid}) to {player.Name} did not fit in the pack");
            }
            catch (Exception ex) { log.Error($"[SALVAGE BAG] refund of {name} ({wcid}) to {player.Name} failed: {ex}"); }
            return false;
        }

        /// <summary>Carry out a plan and return the chat text (it follows "Your Bag of X ").</summary>
        private static string Execute(WorldObject target, Plan plan)
        {
            var name = target.Name;
            switch (plan.Kind)
            {
                case EssenceKind.Edge:
                {
                    var oldQuality = target.GetProperty(PropertyInt.WeaponAugScaleQuality) ?? 0;
                    var from = WeaponScalingManager.GetQualitySubGrade(oldQuality);
                    if (plan.Quality == oldQuality)
                        return $"left the Weapon Grade of your {name} at {from}.";
                    target.SetProperty(PropertyInt.WeaponAugScaleQuality, plan.Quality);
                    return $"{(plan.Quality > oldQuality ? "raised" : "lowered")} the Weapon Grade of your {name} from {from} to {WeaponScalingManager.GetQualitySubGrade(plan.Quality)}.";
                }

                case EssenceKind.Keeping:
                {
                    target.SetProperty(PropertyInt.GearEssenceLockedKey, plan.LockKey);
                    var unit = AllUnits(target).First(u => u.Key == plan.LockKey);
                    return $"locked {UnitName(target, unit)} on your {name}. Other bags will not change it.";
                }

                case EssenceKind.Transmutation:
                    ApplyElement(target, plan.Element);
                    return target.Name != name
                        ? $"turned your {name} to {ElementName(plan.Element)} damage. It is now your {target.Name}."
                        : $"turned your {name} to {ElementName(plan.Element)} damage.";

                case EssenceKind.Hunt when plan.Ops.Count == 0:
                    target.SlayerCreatureType = plan.Species;
                    return $"made your {name} {SlayerPhrase(plan.Species)}.";
            }

            if (plan.Kind == EssenceKind.Chaos && plan.Ops.Count == 0)
            {
                // nothing changes, or a removal that found nothing it may take - the item is Tainted all the same
                target.SetProperty(PropertyBool.GearEssenceTainted, true);
                return plan.Madness == MadnessOutcome.Remove
                    ? $"clawed at your {name}, but nothing it can reach could be torn off. It is now Tainted."
                    : $"seethed around your {name}, but nothing changed. It is now Tainted.";
            }

            if (plan.Kind == EssenceKind.Fortune && plan.FortuneKept)
            {
                var kept = plan.Ops[0];
                return $"rolled no better on {Describe(target, kept.Key, kept.Def, kept.Card, GradeOf(target, kept.Key))}, so your {name} keeps it as it was.";
            }

            if (plan.Kind == EssenceKind.Hunt)
                target.SlayerCreatureType = plan.Species;

            foreach (var op in plan.Ops)
                ApplyOp(target, op);
            Resolve(target, plan.Ops);

            if (plan.Kind == EssenceKind.Chaos)
                target.SetProperty(PropertyBool.GearEssenceTainted, true);

            // the after-values, now every op has resolved
            foreach (var op in plan.Ops.Where(o => o.Action != OpAction.Remove))
                op.Text = Describe(target, op.Key, op.Def, op.Card, op.Grade);

            var removed = string.Join(", ", plan.Ops.Where(o => o.Action == OpAction.Remove).Select(o => o.Text));
            var added = string.Join(", ", plan.Ops.Where(o => o.Action == OpAction.Add).Select(o => o.Text));
            var regraded = string.Join(", ", plan.Ops.Where(o => o.Action == OpAction.Regrade).Select(o => o.Text));

            return plan.Kind switch
            {
                EssenceKind.Loss => $"took {removed} off your {name}.",
                EssenceKind.Gain => $"added {added} to your {name}.",
                EssenceKind.Change => $"rerolled the lowest property on your {name}: {plan.Ops[0].Before} is now {regraded}.",
                EssenceKind.Fortune => $"changed {plan.Ops[0].Before} on your {name} to {regraded}.",
                EssenceKind.Renewal => AllUnits(target).Any(u => u.Key == LockedKey(target))
                    ? $"rerolled every unlocked property on your {name}: {regraded}."
                    : $"rerolled every property on your {name}: {regraded}.",
                EssenceKind.Hunt => $"made your {name} {SlayerPhrase(plan.Species)} ({DescribeCard(target, ZoneStatResolver.SpecSlayer, plan.Ops[0].Grade)}).",
                EssenceKind.Chaos => plan.Madness switch
                {
                    // CodeRabbit #629: a grade of exactly 1000 (over-max low end set to 0) reached the maximum, it didn't pass it;
                    // a card the hard clamp holds says so without claiming it was already there
                    MadnessOutcome.OverMax => plan.Ops[0].Grade <= ZoneStatResolver.GradeMax
                        ? $"set {plan.Ops[0].Before} on your {name} to its maximum, {regraded}. It is now Tainted."
                        : CardHeldAtLimit(target, plan.Ops[0])
                            ? $"drove {plan.Ops[0].Before} on your {name} past its maximum, but its hard limit holds it at {regraded}. It is now Tainted."
                            : $"drove {plan.Ops[0].Before} on your {name} past its maximum, to {regraded}. It is now Tainted.",
                    MadnessOutcome.Shift => $"shook every property it can reach on your {name}: "
                        + string.Join(", ", plan.Ops.Select(o => $"{o.Before} -> {o.Text}")) + ". It is now Tainted.",
                    _ => $"tore {removed} off your {name}. It is now Tainted.",
                },
                EssenceKind.Emptiness => $"took {removed} off your {name}.",
                EssenceKind.Genesis or EssenceKind.Augmentation => $"added {added} to your {name}.",
                EssenceKind.Alteration or EssenceKind.Reforging => $"replaced {removed} on your {name} with {added}.",
                _ => $"took {removed} off your {name} and added {added}.",
            };
        }

        private static void ApplyOp(WorldObject target, Op op)
        {
            var tier = ZoneStatResolver.TierOf(target);

            switch (op.Action)
            {
                case OpAction.Remove:
                {
                    op.Text = Describe(target, op.Key, op.Def, op.Card, GradeOf(target, op.Key));

                    var lines = ZoneStatResolver.Read(target);
                    lines.RemoveAll(l => l.Key == op.Key);
                    ZoneStatResolver.Write(target, lines);

                    if (op.Card != null)
                    {
                        var baseValue = DatabaseManager.World.GetCachedWeenie(target.WeenieClassId)?.GetProperty(op.Card.Prop);
                        if (baseValue.HasValue)
                            target.SetProperty(op.Card.Prop, baseValue.Value);   // the base weapon's own value, not none
                        else
                            target.RemoveProperty(op.Card.Prop);
                        if (op.Card == ZoneStatResolver.SpecRendPower)
                        {
                            target.ImbuedEffect &= ~DamageRends;
                            RefreshUnderlay(target);
                        }
                        else if (op.Card == ZoneStatResolver.SpecArmorRend)
                        {
                            target.ImbuedEffect &= ~ImbuedEffectType.ArmorRending;
                            RefreshUnderlay(target);
                        }
                        else if (op.Card == ZoneStatResolver.SpecSlayer)
                        {
                            target.RemoveProperty(PropertyInt.SlayerCreatureType);
                            target.RemoveProperty(PropertyBool.SlayerAllCreatures);
                        }
                    }
                    else
                    {
                        // Compute only writes props for keys still in the record, so the removed line's props go by
                        // hand. Armor Level (no Ints) needs nothing: Compute rebuilds AL as tier base + remaining lines.
                        if (op.Def.Ints != null)
                            foreach (var (propId, _) in op.Def.Ints)
                                target.RemoveProperty((PropertyInt)propId);
                        ReplaceCantripLine(target, op.Def, null);
                        if (op.Key == ZoneModifiers.JewelProcKey)
                            ZoneLootMutator.ClearJewelProc(target);
                    }
                    return;
                }

                case OpAction.Add:
                    if (op.Card != null)
                    {
                        if (op.Card == ZoneStatResolver.SpecRendPower)
                        {
                            target.ImbuedEffect |= op.Rend;
                            ZoneLootMutator.ApplyRendUnderlay(target, op.Rend);
                        }
                        else if (op.Card == ZoneStatResolver.SpecArmorRend)
                            target.ImbuedEffect |= ImbuedEffectType.ArmorRending;

                        // same as a drop: recorded grade; Resolve writes the value through EngineValue
                        ZoneStatResolver.AddLine(target, op.Key, op.Grade);
                    }
                    else
                    {
                        // the drop's own stamp: record, props and the "Zone Cantrip:" text line
                        ZoneModifiers.StampGraded(target, op.Def, op.Grade, ZoneStatResolver.EffectiveBand(op.Key, tier));
                        if (op.Key == ZoneModifiers.JewelProcKey)
                            ZoneLootMutator.StampJewelProc(target, op.Profile, tier);
                    }
                    return;

                case OpAction.Regrade:
                    op.Before = Describe(target, op.Key, op.Def, op.Card, GradeOf(target, op.Key));
                    SetGradeInPlace(target, op.Key, op.Grade, op.OverMax);
                    return;
            }
        }

        /// <summary>
        /// Re-resolve the item after the essence changed its record. The OTHER lines follow the ladder's own
        /// no-nerf policy exactly as an equip would (ApplyIfStale), so an essence never pulls an unrelated line
        /// down after a plain `ladder apply`. The lines the essence changed are then SET to their new values,
        /// so a reroll can go down and removing Armor Level lowers the AL. Armour line text is kept in step.
        /// </summary>
        private static void Resolve(WorldObject target, List<Op> ops)
        {
            // a tinker on a removed line's prop goes with the line (2026-10-05); Steel on the piece stays
            var removedProps = ops.Where(o => o.Action == OpAction.Remove && o.Def?.Ints != null)
                                  .SelectMany(o => o.Def.Ints.Select(i => i.Item1)).ToList();
            if (removedProps.Count > 0)
                ZoneStatResolver.DropTinkerBonus(target, removedProps);

            var r = ZoneStatResolver.Compute(target);
            var tier = ZoneStatResolver.TierOf(target);
            if (r == null)
            {
                // the record is now empty: a removed Armor Level line leaves the piece at its tier base - plus any Steel tinkered on it
                if (ops.Any(o => o.Action == OpAction.Remove && o.Def != null && IsArmorLevelLine(o.Def)) && target.ArmorLevel.HasValue)
                    target.ArmorLevel = ZoneStatResolver.BaseArmorLevel(tier)
                        + (ZoneStatResolver.ReadTinkerBonus(target).TryGetValue((int)PropertyInt.ArmorLevel, out var steel) ? steel : 0);
                return;
            }

            var allowNerf = ZoneControlManager.GetLadderVersion(tier).AllowNerf || !ServerConfig.zonecontrol_enabled.Value;

            // Fortune never lowers the line it rerolls. Its plan only goes ahead with a HIGHER grade, but the item may still hold
            // a value from before a ladder change: under the no-nerf policy (allowNerf false) that older, higher value is the
            // item's to keep, so it is the floor; once the tier's ladder allows nerfs (every `ladder apply` does, or Zone Control
            // is off) the new value lands and there is no floor (review 2026-10-04 - both directions). The values are read BEFORE
            // Apply rewrites them.
            var floatsBefore = new Dictionary<PropertyFloat, double>();
            var intsBefore = new Dictionary<PropertyInt, int>();
            int? armorBefore = null;
            if (!allowNerf)
            {
                foreach (var op in ops.Where(o => o.NoLower))
                {
                    if (op.Card != null)
                    {
                        if (target.GetProperty(op.Card.Prop) is double fv) floatsBefore[op.Card.Prop] = fv;
                    }
                    else if (IsArmorLevelLine(op.Def))
                        armorBefore = target.ArmorLevel;
                    else if (op.Def?.Ints != null)
                    {
                        foreach (var (propId, _) in op.Def.Ints)
                            if (target.GetProperty((PropertyInt)propId) is int iv) intsBefore[(PropertyInt)propId] = iv;
                    }
                }
            }

            ZoneStatResolver.Apply(target, r, allowNerf);

            foreach (var op in ops)
            {
                if (op.Card != null)
                {
                    if (op.Action != OpAction.Remove && r.Floats.TryGetValue(op.Card.Prop, out var f))
                        target.SetProperty(op.Card.Prop, op.NoLower && floatsBefore.TryGetValue(op.Card.Prop, out var fb) ? Math.Max(f, fb) : f);
                    continue;
                }

                if (IsArmorLevelLine(op.Def))
                {
                    if (r.ArmorLevel.HasValue)
                        target.ArmorLevel = op.NoLower && armorBefore.HasValue ? Math.Max(r.ArmorLevel.Value, armorBefore.Value) : r.ArmorLevel.Value;   // key 25
                }
                else if (op.Action != OpAction.Remove && op.Def.Ints != null)
                {
                    foreach (var (propId, _) in op.Def.Ints)
                        if (r.Ints.TryGetValue((PropertyInt)propId, out var v))
                            target.SetProperty((PropertyInt)propId, op.NoLower && intsBefore.TryGetValue((PropertyInt)propId, out var ib) ? Math.Max(v, ib) : v);
                }

                // keep the baked text in step with the record (appraisal reads the record; this is tidiness)
                if (op.Action == OpAction.Regrade)
                {
                    var line = r.Lines.FirstOrDefault(l => l.Record.Key == op.Key);
                    if (line != null)
                        ReplaceCantripLine(target, op.Def, line.Text);
                }
            }
        }

        /// <summary>The Armor Level line (key 25): armour-only, no Int props - its value is the piece's AL.</summary>
        private static bool IsArmorLevelLine(ZoneModifiers.Def def) => def.ArmorOnly && def.Ints == null;

        /// <summary>Set a key's grade where it already sits in the record, keeping the record (and so the
        /// appraisal's Modifiers list) in stamp order. AddLine would move it to the end. Clamped at GradeMax, so no bag
        /// and no forge regrade goes past a band's max; only a Bag of Madness passes <paramref name="overMax"/>, and even then
        /// only a key that may hold one (ZoneStatResolver.GradeCap) goes above 1000.</summary>
        private static void SetGradeInPlace(WorldObject target, int key, int grade, bool overMax = false)
        {
            var lines = ZoneStatResolver.Read(target);
            var i = lines.FindIndex(l => l.Key == key);
            if (i < 0)
                return;
            var rec = lines[i];
            rec.Grade = Math.Clamp(grade, 0, overMax ? ZoneStatResolver.GradeCap(key) : ZoneStatResolver.GradeMax);
            lines[i] = rec;
            ZoneStatResolver.Write(target, lines);
        }

        /// <summary>Change a weapon's damage type: a rend (to the new type's), the Cast on Strike spells (to the new
        /// type's pair), the name tint and a leading damage-type word all follow, so they keep matching.</summary>
        private static void ApplyElement(WorldObject target, DamageType element)
        {
            SetElementAndRend(target, element);

            if (ZoneLootMutator.TryGetProcSpells(element, out var arc, out var ring))
            {
                var keys = ZoneStatResolver.Read(target).Select(l => l.Key).ToHashSet();
                if (keys.Contains(ZoneStatResolver.WeaponProcArcDamageKey) && target.ProcSpell != null)
                    target.ProcSpell = arc;
                if (keys.Contains(ZoneStatResolver.WeaponProcRingDamageKey) && target.GetProperty((PropertyDataId)ZoneLootMutator.ProcSpell2PropId) != null)
                    target.SetProperty((PropertyDataId)ZoneLootMutator.ProcSpell2PropId, ring);
            }

            LootGenerationFactory.ApplyZoneElementTint(target);
            RenameForElement(target, element);

            // the wield gates from the tier row (slot 4 is the Life-aug gate since 2026-10-05; the old T16+ charm gate is retired) -
            // re-stamped here because this path returns before the resolve that would
            LootGenerationFactory.RefreshWieldGate(target, ZoneStatResolver.TierOf(target));
        }

        /// <summary>Element words a name can carry after its start, beyond <see cref="TypeWords"/>.</summary>
        private static readonly string[] LaterElementWords = { "Flame", "Flames", "Ice", "Storm", "Storms" };

        /// <summary>The damage type, and a rend if the weapon has one, to the new type's.</summary>
        private static void SetElementAndRend(WorldObject target, DamageType element)
        {
            target.W_DamageType = element;

            if ((target.ImbuedEffect & DamageRends) != 0)
            {
                var rend = RendFor(element);
                target.ImbuedEffect = (target.ImbuedEffect & ~DamageRends) | rend;
                ZoneLootMutator.ApplyRendUnderlay(target, rend);
            }
        }

        /// <summary>A new drop's damage type, set before its sweep: the sweep then rolls the rend and procs for this type and
        /// does the tint and wield gates itself, which is why this is not <see cref="ApplyElement"/>.</summary>
        internal static void RetypeDrop(WorldObject target, DamageType element)
        {
            SetElementAndRend(target, element);

            var renamed = DropRename(target.Name, element, StyleOf(target));
            if (renamed != null)
                target.Name = renamed;
        }

        /// <summary><see cref="ElementRename"/>, except a physical melee drop going elemental gains its word ("Ono" ->
        /// "Flaming Ono"), so the name matches the tint. A name that states an element later on ("... of Black Fire") is left
        /// alone: a word in front would give it two. Null when the name stays as it is.</summary>
        internal static string DropRename(string name, DamageType element, NameStyle style)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            var word = TypeWord(style, element);
            if (word != null && !TypeWords.Any(old => name.StartsWith(old + " ", StringComparison.Ordinal)))
                return name.Split(' ').Any(w => TypeWords.Contains(w) || LaterElementWords.Contains(w)) ? null : word + " " + name;
            return ElementRename(name, element, style);
        }

        /// <summary>How a weapon kind names its damage type (from the retail weenie names, counted 2026-10-04): melee says
        /// "Flaming / Frost / Acid / Lightning / Corrupted Ono" and nothing for a physical type; bows say "Fire / Frost / Acid /
        /// Electric / Corrupted / Slashing / Piercing / Blunt Bow"; casters the same as bows but "Nether" for nether.</summary>
        internal enum NameStyle { Melee, Launcher, Caster }

        /// <summary>The name prefix T11+ drops carried until 2026-10-04 (LootGenerationFactory.ApplyT11NamePrefix, removed).</summary>
        private const string LegacyTierPrefix = "T11 - ";

        internal static NameStyle StyleOf(WorldObject wo) => wo is MissileLauncher ? NameStyle.Launcher : wo is Caster ? NameStyle.Caster : NameStyle.Melee;

        /// <summary>Every damage-type word a weapon name can start with, any kind. "Blade" is left out on purpose
        /// ("Blade of ..." names).</summary>
        private static readonly string[] TypeWords =
        {
            "Flaming", "Fire", "Frost", "Acid", "Lightning", "Electric", "Corrupted", "Nether", "Void",
            "Slashing", "Piercing", "Blunt", "Bludgeoning",
        };

        /// <summary>The word <paramref name="style"/> puts in front of a <paramref name="dt"/> weapon, or null for none
        /// (a physical melee weapon has no word).</summary>
        private static string TypeWord(NameStyle style, DamageType dt)
        {
            var melee = style == NameStyle.Melee;
            return dt switch
            {
                DamageType.Fire => melee ? "Flaming" : "Fire",
                DamageType.Cold => "Frost",
                DamageType.Acid => "Acid",
                DamageType.Electric => melee ? "Lightning" : "Electric",
                DamageType.Nether => style == NameStyle.Caster ? "Nether" : "Corrupted",
                DamageType.Slash => melee ? null : "Slashing",
                DamageType.Pierce => melee ? null : "Piercing",
                DamageType.Bludgeon => melee ? null : "Blunt",
                _ => null,
            };
        }

        /// <summary>A name that starts with a damage-type word follows the new type in the weapon kind's own words
        /// ("Frost Ono" -> "Flaming Ono", "Piercing Bow" -> "Blunt Bow"), so the name never says the wrong type (owner review
        /// item 2026-10-04 night); a physical melee type drops the word ("Frost Ono" -> "Ono", the retail physical name).
        /// A name with no type word is left alone, and so is a name the player chose with @item-name ("Frost Fang" stays).</summary>
        internal static void RenameForElement(WorldObject target, DamageType element)
        {
            if (ItemRename.HasCustomName(target))
                return;

            var renamed = ElementRename(target.Name, element, StyleOf(target));
            if (renamed != null)
                target.Name = renamed;
        }

        /// <summary>The name with its leading type word swapped for <paramref name="element"/>'s in <paramref name="style"/>
        /// (removed when the style has none), or null when the name does not start with one (or already has the right one).</summary>
        internal static string ElementRename(string name, DamageType element, NameStyle style = NameStyle.Melee)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            // drops made before 2026-10-04 carry a "T11 - " prefix ("T11 - Frost Ono"): rename the part after it, keep the prefix
            if (name.StartsWith(LegacyTierPrefix, StringComparison.Ordinal))
            {
                var rest = ElementRename(name.Substring(LegacyTierPrefix.Length), element, style);
                return rest == null ? null : LegacyTierPrefix + rest;
            }
            var word = TypeWord(style, element);
            foreach (var old in TypeWords)
            {
                if (!name.StartsWith(old + " ", StringComparison.Ordinal) || name.Length <= old.Length + 1)
                    continue;
                if (word == null)
                    return name.Substring(old.Length + 1);   // physical melee: "Frost Ono" -> "Ono"
                return old == word ? null : word + name.Substring(old.Length);
            }
            return null;
        }

        // -- text helpers ------------------------------------------------------------------------

        private static string UnitName(WorldObject target, Unit u)
            => u.Card == null ? u.Def.Name : u.Card == ZoneStatResolver.SpecRendPower ? RendName(target) : u.Card.Name;

        /// <summary>"an Olthoi Slayer" / "a Drudge Slayer".</summary>
        internal static string SlayerPhrase(CreatureType species)
        {
            var n = SpeciesName(species);
            return (n.Length > 0 && "AEIOU".IndexOf(char.ToUpperInvariant(n[0])) >= 0 ? "an " : "a ") + n + " Slayer";
        }

        /// <summary>A property as the player should read it in chat: "Damage Rating +41" or "Biting Strike (+39% Crit Chance)".</summary>
        private static string Describe(WorldObject target, int key, ZoneModifiers.Def def, ZoneStatResolver.WeaponSpecial card, int grade)
        {
            if (card != null)
            {
                var label = card == ZoneStatResolver.SpecRendPower ? RendName(target) : card.Name;
                return $"{label} ({DescribeCard(target, card, grade)})";
            }
            var line = ZoneStatResolver.Compute(target)?.Lines.FirstOrDefault(l => l.Record.Key == key)?.Text;
            return line == null ? def.Name : TrailingBand.Replace(line, "");
        }

        private static readonly ImbuedEffectType[] AllImbueTypes = Enum.GetValues<ImbuedEffectType>();

        /// <summary>"Fire Rending" for the weapon's own elemental rend, else the card name.</summary>
        private static string RendName(WorldObject target)
        {
            var rends = target.ImbuedEffect & DamageRends;
            foreach (var r in AllImbueTypes)
                if (r != 0 && (rends & r) == r && (DamageRends & r) == r)
                    return r.DisplayName();   // the appraisal's name ("Lightning Rending", "Void Rending")
            return ZoneStatResolver.SpecRendPower.Name;
        }

        /// <summary>A weapon card's display value at a grade, worded like the Property Details line.</summary>
        private static string DescribeCard(WorldObject target, ZoneStatResolver.WeaponSpecial card, int grade)
        {
            var (lo, hi) = ZoneStatResolver.WeaponResolveBand(card, ZoneStatResolver.TierOf(target));
            var d = ZoneStatResolver.CardValue(card, lo, hi, grade);   // hard clamp, and the over-max ceiling for a Madness grade
            if (ZoneLootMutator.HasBanditHilt(target))
            {
                // the appraisal shows the hilt's share on top of the card (Compute re-adds it)
                if (card == ZoneStatResolver.SpecBite) d += ZoneLootMutator.BanditHiltCritFrequencyBonus;
                else if (card == ZoneStatResolver.SpecCrush) d += ZoneLootMutator.BanditHiltCritMultiplierBonus;
            }

            // "(over max)" as the appraisal marks it (lab retest 2026-11-26) - only when the value really passed what grade 1000
            // gives: a card at its hard clamp (T25 Rend Power 5.0) keeps its value (CodeRabbit 2026-11-26)
            var overMax = grade > ZoneStatResolver.GradeMax && ZoneStatResolver.CardValue(card, lo, hi, grade) > ZoneStatResolver.CardValue(card, lo, hi, ZoneStatResolver.GradeMax) + 1e-9;
            return DescribeCardValue(card, d) + (overMax ? ZoneStatResolver.OverMaxMarker : "");
        }

        /// <summary>True when <paramref name="op"/> pushes a weapon card's grade past max but its hard clamp keeps the value where
        /// grade 1000 puts it (T25 Rend Power at 5.0) - the over-max result message says so (CodeRabbit 2026-11-26).</summary>
        private static bool CardHeldAtLimit(WorldObject target, Op op)
        {
            if (op.Card == null || op.Grade <= ZoneStatResolver.GradeMax)
                return false;
            var (lo, hi) = ZoneStatResolver.WeaponResolveBand(op.Card, ZoneStatResolver.TierOf(target));
            return ZoneStatResolver.CardValue(op.Card, lo, hi, op.Grade) <= ZoneStatResolver.CardValue(op.Card, lo, hi, ZoneStatResolver.GradeMax) + 1e-9;
        }

        private static string DescribeCardValue(ZoneStatResolver.WeaponSpecial card, double d)
        {
            if (card == ZoneStatResolver.SpecBite) return FormattableString.Invariant($"+{d * 100:0}% Crit Chance");
            if (card == ZoneStatResolver.SpecCrush) return FormattableString.Invariant($"{d:0.##}x Crit Dmg");
            if (card == ZoneStatResolver.SpecSlayer) return FormattableString.Invariant($"{d:0.##}x Damage");
            if (card == ZoneStatResolver.SpecRendPower) return FormattableString.Invariant($"+{d * 100:0}% Dmg");
            if (card == ZoneStatResolver.SpecArmorRend) return FormattableString.Invariant($"{d * 100:0}% Armor Ignored");
            if (card == ZoneStatResolver.SpecShieldCleave) return FormattableString.Invariant($"{d * 100:0}% Shield Ignored");
            if (card == ZoneStatResolver.SpecSpellArmorRend) return FormattableString.Invariant($"{d * 100:0}% Spell Armor Ignored");
            return FormattableString.Invariant($"{d:0.##}");
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Name, string Prefix), Regex> CantripLinePatterns = new();

        /// <summary>Replace (or with null, remove) the item's "Zone Cantrip: &lt;Name&gt; ..." LongDesc line for a
        /// catalog line. The name must be followed by a space and the start of its own value ("+41", "2% HP"),
        /// so "Max Health" never matches "Max Health Pct +2%".</summary>
        private static void ReplaceCantripLine(WorldObject target, ZoneModifiers.Def def, string newText)
        {
            var desc = target.LongDesc;
            if (string.IsNullOrEmpty(desc))
                return;

            var fmt = def.ValFmt ?? "+{0}";
            var valuePrefix = fmt.Contains("{0}") ? fmt.Substring(0, fmt.IndexOf("{0}")) : fmt;
            var pattern = CantripLinePatterns.GetOrAdd((def.Name, valuePrefix),
                k => new Regex("^Zone Cantrip: " + Regex.Escape(k.Name) + " " + Regex.Escape(k.Prefix) + @"-?\d", RegexOptions.Compiled));
            var lines = desc.Split('\n').ToList();
            var index = lines.FindIndex(l => pattern.IsMatch(l.TrimEnd('\r')));
            if (index < 0)
                return;

            if (newText != null)
                lines[index] = "Zone Cantrip: " + newText;
            else
            {
                lines.RemoveAt(index);
                // a blank line on both sides now: drop one, so the blocks around it keep their spacing
                if (index > 0 && index < lines.Count && lines[index].Trim().Length == 0 && lines[index - 1].Trim().Length == 0)
                    lines.RemoveAt(index);
            }

            target.LongDesc = string.Join("\n", lines).Trim('\n');
        }

        /// <summary>After an imbue bit is removed: show the underlay of an imbue the weapon still has, or none.</summary>
        private static void RefreshUnderlay(WorldObject target)
        {
            // leave an underlay alone unless it is an imbue's (a base weenie can carry its own)
            var current = target.IconUnderlayId;
            if (current == null || !(RecipeManager.IconUnderlay.ContainsValue(current.Value) || current.Value == ZoneLootMutator.NetherRendUnderlay))
                return;

            var imbues = target.GetImbuedEffects();
            foreach (var kv in RecipeManager.IconUnderlay)
            {
                if ((imbues & kv.Key) != 0)
                {
                    target.IconUnderlayId = kv.Value;
                    return;
                }
            }

            if ((imbues & ImbuedEffectType.NetherRending) != 0)
                ZoneLootMutator.ApplyRendUnderlay(target, ImbuedEffectType.NetherRending);
            else
                target.RemoveProperty(PropertyDataId.IconUnderlay);
        }
    }
}
