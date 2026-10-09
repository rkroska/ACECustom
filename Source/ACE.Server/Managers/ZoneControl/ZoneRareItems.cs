using System;
using System.Collections.Generic;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

using log4net;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// T11+ RARES, the item half (owner 2026-10-08). ZoneRare.cs holds the odds; this file holds what a rare IS: which
    /// drops can become one, which cards and lines it carries, how it is named, flagged and announced.
    ///
    /// A rare is NOT a new item class and has no weenie of its own. It is one ordinary item of the kill's own loot, made
    /// in "rare mode" by the same sweep every T11+ drop goes through (Creature.ApplyZoneDropSweep): the mutator is told
    /// the tier (ZoneLootMutator.MutateLootItem, rare:) and StampRare below finishes the piece. The decisions the mutator
    /// needs - which weapon cards, which armour lines - are the pure functions here, so they are tested without a world.
    ///
    /// ONE ENTRY POINT PER STEP, each taking the ZoneRareTier, so the two tiers share one path instead of each growing
    /// its own: TryRollDrop (the roll), the mutator's rare: argument (the rolls on the item), StampRare (identity) and
    /// Announce.
    ///
    /// PRISTINE is the piece the kill rolled, made at the kill's tier from the kill's own zone profile.
    /// ASCENDANT (owner 2026-10-09) is a piece of a HIGHER tier - kill tier + zc_rare_ascendant_tier_bonus, never past
    /// 25 - and it has to be indistinguishable from a perfect drop of that tier. So nothing about it is "raised":
    ///   - the piece the kill rolled is REPLACED by one of the same kind generated at the item tier (TryRollDrop), so
    ///     the weenie and every base roll come from the same tables a kill of that tier uses;
    ///   - the sweep then runs AT the item tier with the item tier's Default profile (ZoneControlManager
    ///     .EvaluateTierDefault) - bands, card cap, line cap, always-rolled lines, item spells. That is the very layer
    ///     ZoneStatResolver re-resolves an item against on equip and login, so what is stamped at drop is what it
    ///     resolves to later and nothing moves on first equip;
    ///   - the one thing kept from the kill is the tier it dropped in, stored as PropertyInt.ZcRareGateTier: its wield
    ///     requirements are that tier's (GateTier / GateTierOf), and so is the aug floor under a launcher's or caster's
    ///     tier steps (WeaponScalingCombat.WieldFloorAugs).
    /// It is static: made once, never changed by who holds it.
    /// </summary>
    public static partial class ZoneRare
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // -- identity --------------------------------------------------------------------------------

        /// <summary>The word a rare of this tier carries in front of its name and in its appraisal line. Null for None.</summary>
        internal static string TierName(ZoneRareTier tier) => tier switch
        {
            ZoneRareTier.Pristine => "Pristine",
            ZoneRareTier.Ascendant => "Ascendant",
            _ => null,
        };

        /// <summary>
        /// "Sword" -> "Pristine Sword". Idempotent: a name that already leads with the tier word comes back unchanged, so a
        /// second pass (a re-stamp, a dev command run twice) can never produce "Pristine Pristine Sword".
        /// </summary>
        internal static string PrefixName(string name, ZoneRareTier tier)
        {
            var word = TierName(tier);
            if (word == null || string.IsNullOrWhiteSpace(name))
                return name;

            var trimmed = name.Trim();
            if (trimmed.Equals(word, StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(word + " ", StringComparison.OrdinalIgnoreCase))
                return name;

            return word + " " + trimmed;
        }

        /// <summary>The pinned appraisal bullet. Plain ASCII - the client draws anything else as garbage.
        /// <paramref name="foundTier"/> is the tier an Ascendant item dropped in (its stored gate tier); it is shown whenever
        /// it is a real tier, so the line also explains wield requirements lower than the item's tier would ask.</summary>
        internal static string AppraisalText(ZoneRareTier tier, int itemTier, int foundTier = 0)
        {
            var word = TierName(tier);
            if (word == null)
                return null;
            if (itemTier < MinTier)
                return $"- {word}: every roll is at the maximum for its tier";
            var found = tier == ZoneRareTier.Ascendant && foundTier >= MinTier && foundTier <= MaxTier ? $" (found in Tier {foundTier})" : "";
            return $"- {word}: every roll is at the maximum for Tier {itemTier}{found}";
        }

        /// <summary>The tier whose wield requirements this item carries: ZoneRare.GateTier on its stored drop tier. Every
        /// wield-gate stamp asks this (LootGenerationFactory.StampTierGates / RefreshWieldGate). <paramref name="itemTier"/>
        /// is what the caller would otherwise have used - returned unchanged for an item with no stored gate tier.</summary>
        public static int GateTierOf(WorldObject wo, int itemTier)
            => GateTier(wo?.GetProperty(PropertyInt.ZcRareGateTier), itemTier);

        /// <summary>The Salvage Bag refusal for a rare (GearEssences.Verify). Names the item's own tier word, so an Ascendant
        /// piece is not told it is Pristine. Plain ASCII.</summary>
        internal static string BagRefusalText(string itemName, ZoneRareTier tier)
            => $"Your {itemName} is {TierName(tier) ?? "a rare item"}. Bags do not work on it.";

        /// <summary>The imbue refusal for a rare (ZoneCraftGate.IsBlocked). Plain ASCII.</summary>
        internal static string ImbueRefusalText(ZoneRareTier tier)
            => $"This item is {TierName(tier) ?? "a rare item"} and cannot be imbued. Ordinary tinkering still works.";

        /// <summary>Which rare this item is. None for an ordinary item, and for a stored value this build does not know.</summary>
        public static ZoneRareTier TierOfRare(WorldObject wo)
        {
            var raw = wo?.GetProperty(PropertyInt.ZcRare) ?? 0;
            return raw == (int)ZoneRareTier.Pristine || raw == (int)ZoneRareTier.Ascendant ? (ZoneRareTier)raw : ZoneRareTier.None;
        }

        public static bool IsRare(WorldObject wo) => TierOfRare(wo) != ZoneRareTier.None;

        /// <summary>The appraisal bullet for this item, built live from its flag and tier. Null for an ordinary item.</summary>
        public static string AppraisalLine(WorldObject wo)
        {
            var tier = TierOfRare(wo);
            return tier == ZoneRareTier.None ? null : AppraisalText(tier, ZoneCraftGate.TierOf(wo), wo.GetProperty(PropertyInt.ZcRareGateTier) ?? 0);
        }

        // -- which drops can be a rare ------------------------------------------------------------------

        /// <summary>
        /// Can a drop of this kind be upgraded to a rare? Zone GEAR only: a weapon (melee, launcher, caster) or a wearable
        /// piece of armor / clothing / jewelry (shields are armor, cloaks are clothing). Never coins, never anything that
        /// stacks (salvage, ammunition, components), never an item that cannot be worn or wielded.
        /// </summary>
        internal static bool IsEligibleKind(bool isWeapon, ItemType itemType, WeenieType weenieType, bool wearable, int maxStackSize)
        {
            if (weenieType == WeenieType.Coin || maxStackSize > 1)
                return false;
            if (isWeapon)
                return true;
            if (!wearable)
                return false;
            return itemType == ItemType.Armor || itemType == ItemType.Clothing || itemType == ItemType.Jewelry;
        }

        /// <summary>IsEligibleKind for a generated drop. Asked BEFORE the sweep, so it reads only what the item is born with.</summary>
        public static bool IsEligible(WorldObject wo)
        {
            if (wo == null)
                return false;
            return IsEligibleKind(
                wo is MeleeWeapon || wo is MissileLauncher || wo is Caster,
                wo.ItemType, wo.WeenieType,
                (wo.ValidLocations ?? EquipMask.None) != EquipMask.None,
                wo.MaxStackSize ?? 0);
        }

        // -- weapon cards --------------------------------------------------------------------------------

        // The ten weapon cards, in the order ZoneLootMutator.TrySpecialRolls keeps its won / eligible / chance arrays.
        // That method's won and chance arrays are still written out positionally, so a card added there needs its index
        // added here too (ZoneRareItemTests pins the count).
        internal const int CardRend = 0;
        internal const int CardSlayer = 1;
        internal const int CardBite = 2;
        internal const int CardCrush = 3;
        internal const int CardArmorRend = 4;
        internal const int CardShieldCleave = 5;
        internal const int CardCleave = 6;
        internal const int CardSplit = 7;
        internal const int CardProcArc = 8;
        internal const int CardProcRing = 9;
        internal const int CardCount = 10;

        /// <summary>
        /// Which cards a weapon can carry at all - the one copy of the rules, used by the ordinary roll and by rare mode.
        /// Rend needs an element the weapon deals (eligRend), Slayer a creature type to attune to (eligSlayer), the two
        /// Cast on Strike slots a resolvable element and a free proc slot (hasProcSlots). Armor Rend and Shield Cleave are
        /// physical effects - melee and missile only, never a caster. Biting Strike and Crushing Blow are live on every
        /// weapon (magic crits exist). Cleave is melee, Split Arrows is missile.
        /// </summary>
        internal static bool[] WeaponCardEligibility(bool isMelee, bool isMissile, bool isCaster, bool eligRend, bool eligSlayer, bool hasProcSlots)
        {
            var isWeapon = isMelee || isMissile || isCaster;
            var physical = isMelee || isMissile;
            var e = new bool[CardCount];
            e[CardRend] = isWeapon && eligRend;
            e[CardSlayer] = isWeapon && eligSlayer;
            e[CardBite] = isWeapon;
            e[CardCrush] = isWeapon;
            e[CardArmorRend] = physical;
            e[CardShieldCleave] = physical;
            e[CardCleave] = isMelee;
            e[CardSplit] = isMissile;
            e[CardProcArc] = isWeapon && hasProcSlots;
            e[CardProcRing] = isWeapon && hasProcSlots;
            return e;
        }

        /// <summary>
        /// The card set of a rare weapon (owner 2026-10-08).
        ///
        /// CORE, always: Rending (with its Rend Power), Slayer and Armor Rend - each wherever the weapon is eligible for
        /// it. The core is forced: it does not ask the card's chance or its on/off toggle, and it is never trimmed, so a
        /// tier whose cap is below the core count still gets the whole core (the same rule the ordinary roll applies to
        /// pinned cards).
        ///
        /// EXTRAS, at random: from the other cards the weapon is eligible for AND that can drop at this tier
        /// (<paramref name="enabled"/> = toggled on, chance authored and above zero - Split Arrows is off in the live
        /// config and must stay off), until the card count reaches <paramref name="cap"/> or the pool runs out.
        /// Biting Strike and Crushing Blow are never an extra (owner: near-worthless against current monster crit
        /// resists, and each would spend a slot a real card could have had).
        ///
        /// <paramref name="pickIndex"/>(n) returns an index in [0, n). Each pick removes one card from the pool, so the
        /// loop ends after at most ten picks whatever the picker returns.
        /// </summary>
        internal static bool[] PickWeaponCards(bool[] eligible, bool[] enabled, int cap, Func<int, int> pickIndex)
        {
            var chosen = new bool[CardCount];
            if (eligible == null || eligible.Length < CardCount)
                return chosen;

            var count = 0;
            foreach (var core in new[] { CardRend, CardSlayer, CardArmorRend })
            {
                if (!eligible[core])
                    continue;
                chosen[core] = true;
                count++;
            }

            var pool = new List<int>();
            for (var i = 0; i < CardCount; i++)
            {
                if (i == CardBite || i == CardCrush)
                    continue;
                if (i == CardRend || i == CardSlayer || i == CardArmorRend)
                    continue;   // the core is decided above; a core card the weapon cannot carry is not an extra either
                if (eligible[i] && enabled != null && i < enabled.Length && enabled[i])
                    pool.Add(i);
            }

            while (count < cap && pool.Count > 0)
            {
                var k = pickIndex != null ? pickIndex(pool.Count) : 0;
                if (k < 0 || k >= pool.Count)
                    k = 0;   // a bad picker must not throw inside loot generation
                chosen[pool[k]] = true;
                pool.RemoveAt(k);
                count++;
            }

            return chosen;
        }

        // -- armour / jewelry lines ------------------------------------------------------------------------

        /// <summary>
        /// The extra lines of a rare armor / shield / clothing / jewelry / cloak piece: EXACTLY <paramref name="cap"/>
        /// of them (fewer only when fewer lines can drop on the piece), drawn at random WITHOUT replacement, each line's
        /// own drop chance as its weight (owner 2026-10-08: random on purpose, so some Pristine pieces are better than
        /// others - no line is guaranteed). <paramref name="weights"/> has one entry per line valid for the piece; a
        /// weight of zero (chance unset, zero, or the line switched off) is never drawn, so rare mode cannot switch a
        /// line on. <paramref name="draw"/> returns a number in [0, 1).
        ///
        /// Returns the chosen indexes into <paramref name="weights"/>, in pick order.
        /// </summary>
        internal static List<int> PickExtraLines(IReadOnlyList<double> weights, int cap, Func<double> draw)
        {
            var chosen = new List<int>();
            if (weights == null || cap <= 0)
                return chosen;

            var pool = new List<int>();
            var poolWeights = new List<double>();
            for (var i = 0; i < weights.Count; i++)
            {
                var w = weights[i];
                if (double.IsFinite(w) && w > 0.0)
                {
                    pool.Add(i);
                    poolWeights.Add(w);
                }
            }

            while (chosen.Count < cap && pool.Count > 0)
            {
                var total = 0.0;
                for (var i = 0; i < poolWeights.Count; i++)
                    total += poolWeights[i];

                var roll = (draw != null ? draw() : 0.0) * total;
                var k = pool.Count - 1;   // floating-point tail guard, and the answer for a draw of 1.0 or more
                for (var i = 0; i < poolWeights.Count; i++)
                {
                    roll -= poolWeights[i];
                    if (roll < 0.0)
                    {
                        k = i;
                        break;
                    }
                }

                chosen.Add(pool[k]);
                pool.RemoveAt(k);
                poolWeights.RemoveAt(k);
            }

            return chosen;
        }

        // -- the roll ------------------------------------------------------------------------------------

        /// <summary>The numbers one kill's rare roll runs on - also what `/zcrare tier` prints.</summary>
        public sealed class Odds
        {
            public int PlayerTier;                  // TierHitGate.PlayerTier
            public int OwnTier;                     // the highest enabled zone tier at or below PlayerTier
            public int KillTier;                    // the tier the loot is generated at
            public double Scale;                    // BelowTierScale
            public long PristineOneIn, AscendantOneIn;
            public double PristineChance, AscendantChance;
        }

        /// <summary>The live odds for this player on a kill whose loot is generated at <paramref name="killTier"/>. Reads the five
        /// settings as they are right now, and the enabled zone tiers from the published Zone Control snapshot (no lock).</summary>
        public static Odds OddsFor(Player player, int killTier)
        {
            var o = new Odds
            {
                PlayerTier = TierHitGate.PlayerTier(player),
                KillTier = killTier,
                PristineOneIn = ServerConfig.zc_rare_pristine_odds.Value,
                AscendantOneIn = ServerConfig.zc_rare_ascendant_odds.Value,
            };
            o.OwnTier = OwnTier(o.PlayerTier, ZoneControlManager.EnabledZoneTiers);
            o.Scale = BelowTierScale(o.OwnTier, killTier,
                ServerConfig.zc_rare_odds_one_tier_below.Value, ServerConfig.zc_rare_odds_two_tiers_below.Value);
            o.PristineChance = ChancePerKill(o.PristineOneIn, o.Scale);
            o.AscendantChance = ChancePerKill(o.AscendantOneIn, o.Scale);
            return o;
        }

        /// <summary>One kill's rare: the tier, the item of the kill's own loot that becomes it, who found it, and the odds used.</summary>
        public sealed class Drop
        {
            public ZoneRareTier Tier;
            public WorldObject Piece;
            public Player Finder;
            public Odds Odds;
            /// <summary>The tier the piece is swept, stamped and later resolved at: the kill tier for Pristine, the kill tier
            /// plus the bonus for Ascendant.</summary>
            public int ItemTier;
            /// <summary>Ascendant only: the tier it dropped in, whose wield requirements it carries. 0 for Pristine.</summary>
            public int GateTier;
            /// <summary>The profile the sweep rolls the piece from. Null = the kill's own zone profile (Pristine). Ascendant:
            /// the item tier's Default, named after the kill's zone so the provenance line still says where it dropped.</summary>
            public ZoneScaling.EvaluatedProfile Profile;
            /// <summary>Ascendant only: the piece the kill rolled, which <see cref="Piece"/> replaced in the loot list. The
            /// loot loop uses it to carry a slot special over to the replacement. Null when nothing was replaced.</summary>
            public WorldObject Replaced;
            /// <summary>Set by the loot loop: did the piece reach the corpse (or the dropped list)?</summary>
            public bool Placed;
        }

        /// <summary>
        /// The profile an Ascendant piece of this item tier is rolled from: the tier's Default (the v11 anchor board under
        /// the tier's own Default) - NOT the kill's zone profile, which belongs to the lower kill tier and would roll the
        /// kill tier's bands and caps. It is also the layer ZoneStatResolver resolves a stored grade against, so drop and
        /// re-resolve agree by construction. A fresh object every call; <paramref name="scopeKey"/> (the kill's zone name)
        /// only labels the provenance line.
        /// </summary>
        public static ZoneScaling.EvaluatedProfile AscendantProfile(int itemTier, string scopeKey = null)
        {
            var p = ZoneControlManager.EvaluateTierDefault(itemTier);
            if (!string.IsNullOrEmpty(scopeKey))
                p.ScopeKey = scopeKey;
            return p;
        }

        /// <summary>
        /// The once-per-kill rare roll. Returns null on a miss - the overwhelmingly common case, and the cheap one: with
        /// both tiers off it reads two settings and returns. On a hit it picks ONE item, uniformly, among the eligible
        /// items this kill actually generated; a kill that generated none has no rare (nothing is spawned for it).
        ///
        /// The finder is the kill's top damager resolved to a player - a combat pet's kill is its owner's, the way
        /// Bounty counts it. No online player behind the kill = no roll. The below-tier share is THAT player's: their
        /// own tier (the highest tier they qualify for that has an enabled zone) against the tier the loot drops at.
        ///
        /// Runs on the landblock thread of the dying monster and touches nothing shared: it only reads live settings,
        /// the published zone snapshot and the finder's aug counters. The caller wraps it in try / catch - a rare roll
        /// must never cost a corpse.
        ///
        /// ASCENDANT replaces the picked piece inside <paramref name="items"/> with one of the same kind generated at
        /// the item tier (see the file header for why). <paramref name="killTreasure"/> is the kill's own treasure
        /// profile, re-tiered for that; <paramref name="zoneLoot"/> only lends its zone name to the provenance line.
        /// The list is touched only once the replacement exists, so a miss or a failure leaves the kill's loot as it was.
        /// </summary>
        public static Drop TryRollDrop(DamageHistoryInfo killer, Creature killed, int killTier, List<WorldObject> items,
            ACE.Database.Models.World.TreasureDeath killTreasure = null, ZoneScaling.EvaluatedProfile zoneLoot = null)
        {
            if (ServerConfig.zc_rare_pristine_odds.Value <= 0 && ServerConfig.zc_rare_ascendant_odds.Value <= 0)
                return null;
            if (killTier < MinTier || items == null || items.Count == 0)
                return null;

            if (!(killer?.TryGetPetOwnerOrAttacker() is Player finder) || finder.Session == null)
                return null;

            var odds = OddsFor(finder, killTier);
            if (!(odds.PristineChance > 0) && !(odds.AscendantChance > 0))
                return null;

            // two independent draws; Pick tests Ascendant first so one kill never yields both
            var tier = Pick(ThreadSafeRandom.Next(0.0f, 1.0f), ThreadSafeRandom.Next(0.0f, 1.0f), odds.AscendantChance, odds.PristineChance);
            if (tier == ZoneRareTier.None)
                return null;

            var eligible = new List<WorldObject>();
            foreach (var item in items)
                if (IsEligible(item))
                    eligible.Add(item);

            if (eligible.Count == 0)
            {
                log.Info($"[ZONELOOT] RARE {TierName(tier)} rolled for {finder.Name} on {killed?.Name} ({killed?.WeenieClassId}) at tier {killTier}, but the kill generated no zone gear - no rare.");
                return null;
            }

            var drop = new Drop
            {
                Tier = tier,
                Piece = eligible[ThreadSafeRandom.Next(0, eligible.Count - 1)],
                Finder = finder,
                Odds = odds,
                ItemTier = killTier,
            };

            if (tier == ZoneRareTier.Ascendant)
            {
                drop.ItemTier = AscendantTier(killTier, ServerConfig.zc_rare_ascendant_tier_bonus.Value);
                drop.GateTier = killTier;
                drop.Profile = AscendantProfile(drop.ItemTier, zoneLoot?.ScopeKey);

                // the kill rolled this piece at the KILL tier; an Ascendant piece is a drop of the ITEM tier, so one of the
                // same kind is generated there and takes its place. Should that fail (a tier with no loot tables yet), the
                // rare is not thrown away: the piece the kill rolled is kept and swept at the item tier - every roll, band
                // and cap is still the item tier's, only the base weenie is the kill tier's - and the log says so.
                WorldObject replacement = null;
                try { replacement = CreateLike(drop.Piece, drop.ItemTier, killTreasure, drop.Profile); }
                catch (Exception ex) { log.Error($"[ZONELOOT] RARE Ascendant: generating a tier {drop.ItemTier} piece like {drop.Piece.Name} ({drop.Piece.WeenieClassId}) threw: {ex}"); }
                var at = replacement != null ? items.IndexOf(drop.Piece) : -1;
                if (at >= 0)
                {
                    // the swap first, in full - list, then the drop - and only then the old piece. Destroy is guarded: were
                    // it to throw out of here, the caller would drop the whole roll while the list already held the
                    // item-tier piece, and the loop would sweep that as an ordinary kill-tier drop.
                    var old = drop.Piece;
                    items[at] = replacement;
                    drop.Piece = replacement;
                    drop.Replaced = old;
                    try { old.Destroy(); }
                    catch (Exception ex) { log.Warn($"[ZONELOOT] RARE Ascendant: could not destroy the replaced piece {old.Name} (0x{old.Guid}): {ex.Message}"); }
                }
                else
                {
                    replacement?.Destroy();
                    log.Warn($"[ZONELOOT] RARE Ascendant: could not generate a tier {drop.ItemTier} piece like {drop.Piece.Name} ({drop.Piece.WeenieClassId}) for {finder.Name} - the tier {killTier} piece is upgraded in place instead.");
                }
            }

            return drop;
        }

        /// <summary>
        /// One zone-set piece of the same kind as <paramref name="like"/> (same weapon family / armor slot / jewelry slot),
        /// generated at <paramref name="itemTier"/> by the generator every kill uses - CreateZoneLootSet with a one-item
        /// count, the way the slot special spawns a piece. Unswept: the caller runs the sweep. Null when the kind cannot be
        /// expressed as a slot or the generator produced nothing. Shared by the drop and by nothing else on purpose - the
        /// dev mint asks for a kind by name, not for "one like this".
        /// </summary>
        private static WorldObject CreateLike(WorldObject like, int itemTier, ACE.Database.Models.World.TreasureDeath killTreasure, ZoneScaling.EvaluatedProfile profile)
        {
            var one = ACE.Server.Factories.LootGenerationFactory.ZoneSetCountsLike(like);
            if (one == null)
                return null;

            var treasure = Creature.ZoneTreasureAt(killTreasure, itemTier);
            List<WorldObject> made;
            using (ZoneStatResolver.ScopeDropFloor(ZoneStatResolver.GradeFloorOf(profile)))
                made = ACE.Server.Factories.LootGenerationFactory.CreateZoneLootSet(treasure, one);

            WorldObject piece = null;
            foreach (var item in made)
            {
                if (piece == null && IsEligible(item))
                    piece = item;
                else
                    item.Destroy();
            }
            return piece;
        }

        /// <summary>
        /// Marks a piece with the tier it dropped in BEFORE the sweep stamps its wield requirements, so they come out as
        /// that tier's from the first stamp (and every later re-stamp). Ascendant only; a tier off the ladder is not stored.
        /// </summary>
        public static void StampGateTier(WorldObject wo, ZoneRareTier tier, int gateTier)
        {
            if (wo == null || tier != ZoneRareTier.Ascendant || gateTier < MinTier || gateTier > MaxTier)
                return;
            wo.SetProperty(PropertyInt.ZcRareGateTier, gateTier);
        }

        // -- making the item -----------------------------------------------------------------------------

        /// <summary>
        /// The icon background of each rare tier (owner's pick, 2026-10-09): the same sunburst-around-a-dark-centre shape in two
        /// colours, so the two read as one family and the gold one as the step above. Both are existing client icons.
        /// </summary>
        public const uint PristineIconUnderlay = 0x06006E9A;    // green sunburst
        public const uint AscendantIconUnderlay = 0x06006E9D;   // gold sunburst

        /// <summary>The icon background for a rare tier; null for none.</summary>
        internal static uint? IconUnderlayFor(ZoneRareTier tier) => tier switch
        {
            ZoneRareTier.Pristine => PristineIconUnderlay,
            ZoneRareTier.Ascendant => AscendantIconUnderlay,
            _ => null,
        };

        /// <summary>
        /// Finishes a piece the sweep made in rare mode: the rolls the mutator does not own, then the identity.
        /// Called by Creature.ApplyZoneDropSweep after the weapon quality stamp and before the description cleanup.
        ///
        ///   - a weapon's Weapon Grade is S (quality 1000);
        ///   - a weapon's Damage / Crit Damage rating - rolled inside item creation, long before anyone knows the kill
        ///     has a rare - is raised to the top of its band for the tier (<paramref name="lootTier"/> is the tier the
        ///     piece is swept at: the item tier, which for an Ascendant piece is above the kill's);
        ///   - the flag (PropertyInt.ZcRare), the name prefix, and Bonded so it is never dropped on death. NOT Attuned:
        ///     a rare stays tradeable. The zone's own attuned / bonded rules were applied by the mutator as for any drop.
        ///
        /// The appraisal line is built live from the flag (AppraiseInfo), so nothing is written into LongDesc here and
        /// the LongDesc whitelist needs no entry. No icon underlay either: a weapon's underlay is its rend's.
        ///
        /// Never throws: a failure is logged and the piece stays whatever it had become. Returns false when the piece
        /// did not end up flagged.
        /// </summary>
        public static bool StampRare(WorldObject wo, ZoneRareTier tier, int lootTier)
        {
            if (wo == null || TierName(tier) == null)
                return false;

            try
            {
                if (wo is MeleeWeapon || wo is MissileLauncher || wo is Caster)
                {
                    if (wo.GetProperty(PropertyInt.WeaponAugScaleQuality).HasValue)
                        wo.SetProperty(PropertyInt.WeaponAugScaleQuality, WeaponScaling.WeaponScalingManager.QualityMax);
                    MaxWeaponRating(wo, lootTier, PropertyInt.GearDamage, 28);
                    MaxWeaponRating(wo, lootTier, PropertyInt.GearCritDamage, 29);
                }

                wo.SetProperty(PropertyInt.ZcRare, (int)tier);

                var name = PrefixName(wo.Name, tier);
                wo.Name = name;
                wo.SetProperty(PropertyString.Name, name);

                wo.Bonded = BondedStatus.Bonded;

                // Its tier's icon background, so a rare reads as one at a glance in a pack, on a corpse and in a trade window
                // (owner 2026-10-09). On a weapon it replaces the rend's element underlay - this runs after the cards are
                // stamped, so it wins - and the element still shows through the icon tint and in Property Details. Nothing
                // re-stamps the underlay afterwards: bags, imbues and the forge all refuse a rare.
                if (IconUnderlayFor(tier) is uint underlay)
                    wo.IconUnderlayId = underlay;
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[ZONELOOT] RARE: could not finish {wo.Name} (0x{wo.Guid}, {wo.WeenieClassId}) as {TierName(tier)}: {ex}");
                return IsRare(wo);
            }
        }

        /// <summary>
        /// The weapon's own rating line (LootGenerationFactory.TryMutateGearRatingForWeapons: one of Damage Rating /
        /// Crit Damage Rating, a plain frozen Gear* value with no grade record) at grade 1000 - the same band, and the
        /// same T25 catalog ceiling, that roll uses. Only the line the drop actually rolled, and only ever raised.
        /// </summary>
        private static void MaxWeaponRating(WorldObject wo, int lootTier, PropertyInt prop, int catalogKey)
        {
            var current = wo.GetProperty(prop) ?? 0;
            if (current <= 0)
                return;

            var (min, max) = ZoneStatResolver.EffectiveBand(catalogKey, lootTier);
            var top = ZoneStatResolver.ValueFor(min, max, ZoneStatResolver.GradeMax);
            if (ZoneModifiers.TryGet(catalogKey, out var def))
                top = Math.Min(top, ZoneModifiers.CatalogBandAt(def, MaxTier).Max);

            if (top > current)
                wo.SetProperty(prop, top);
        }

        // -- announce + audit ------------------------------------------------------------------------------

        /// <summary>The server-wide line. Plain ASCII; the item name is the one the client shows (material first).</summary>
        internal static string BroadcastText(string finderName, string itemName) => $"{finderName} has found the {itemName}!";

        /// <summary>
        /// A rare exists: tell the server, the audit channel and the log. Call it only once the piece is safely on the
        /// corpse / in the dropped list / in a pack. <paramref name="devMint"/> = made by `/zcrare`: audited and logged
        /// (marked), never broadcast.
        /// </summary>
        public static void Announce(Drop drop, Creature killed, bool devMint = false)
        {
            if (drop?.Piece == null)
                return;

            var piece = drop.Piece;
            var finder = drop.Finder;
            var o = drop.Odds ?? new Odds();
            var word = TierName(drop.Tier) ?? drop.Tier.ToString();
            var itemName = piece.NameWithMaterial;
            var finderName = finder?.Name ?? "(unknown)";
            var itemTier = ZoneCraftGate.TierOf(piece);
            // an Ascendant item carries two tiers: the one it is (item tier) and the one it dropped in (its wield gate)
            var dropTier = piece.GetProperty(PropertyInt.ZcRareGateTier);
            var tierText = dropTier.HasValue ? $"tier {itemTier}, found in tier {dropTier.Value}" : $"tier {itemTier}";
            var from = killed != null ? $"{killed.Name} ({killed.WeenieClassId})" : "(no kill)";
            var oneIn = drop.Tier == ZoneRareTier.Ascendant ? o.AscendantOneIn : o.PristineOneIn;
            var chance = drop.Tier == ZoneRareTier.Ascendant ? o.AscendantChance : o.PristineChance;
            var scale = o.Scale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var chanceText = chance.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture);

            log.Info($"[ZONELOOT] RARE {word}{(devMint ? " (DEV MINT)" : "")}: finder {finderName} (0x{finder?.Guid}, account {finder?.Account?.AccountName ?? "?"}), "
                + $"player tier {o.PlayerTier}, own tier {o.OwnTier}, kill tier {o.KillTier}, item tier {itemTier}, drop tier {dropTier ?? itemTier} -> {itemName} (0x{piece.Guid}, wcid {piece.WeenieClassId}) "
                + $"from {from}; odds 1 in {oneIn}, scale {scale}, chance per kill {chanceText}");

            try
            {
                PlayerManager.BroadcastToAuditChannel(finder, devMint
                    ? $"[RARE] DEV MINT: {finderName} created the {itemName} ({word}, {tierText}, 0x{piece.Guid}, wcid {piece.WeenieClassId}) with /zcrare."
                    : $"[RARE] {finderName} found the {itemName} ({word}, {tierText}, 0x{piece.Guid}, wcid {piece.WeenieClassId}) on {from}. Odds 1 in {oneIn}, scale {scale}.");

                if (!devMint)
                    PlayerManager.BroadcastToAll(new GameMessageSystemChat(BroadcastText(finderName, itemName), ChatMessageType.WorldBroadcast));
            }
            catch (Exception ex)
            {
                // the item is real and logged above; a chat failure must not undo that or reach the loot path
                log.Error($"[ZONELOOT] RARE: announce failed for {itemName} (0x{piece.Guid}): {ex}");
            }
        }

        /// <summary>
        /// Closes a kill's rare after the loot loop. A rare must never be silently lost, and a full corpse (120 items)
        /// refuses an item without a word - so a piece the corpse did not take is placed on the ground where the monster
        /// died (never into the finder's pack: see the note inside). Then the announcement, once the piece is somewhere it
        /// can be picked up. The caller wraps this in try / catch.
        /// </summary>
        public static void FinishDrop(Drop drop, Creature killed)
        {
            if (drop?.Piece == null)
                return;

            var piece = drop.Piece;
            if (!IsRare(piece))
            {
                // StampRare failed and said so; the piece dropped as whatever it had become
                log.Error($"[ZONELOOT] RARE: {piece.Name} (0x{piece.Guid}, {piece.WeenieClassId}) won a {TierName(drop.Tier)} roll for {drop.Finder?.Name} but is not flagged - not announced.");
                return;
            }

            if (!drop.Placed)
            {
                // an overflowing corpse is expected now and then and is handled: Warn, then Info once the piece is down.
                // Error is kept for the one case nothing can be done about (RARE LOST below).
                log.Warn($"[ZONELOOT] RARE: {piece.Name} (0x{piece.Guid}, {piece.WeenieClassId}) did not fit on the corpse of {killed?.Name} ({killed?.WeenieClassId}) - dropping it on the ground for {drop.Finder?.Name} instead.");

                // Onto the ground, never into the finder's pack (review 2026-10-09). This runs on the MONSTER's landblock
                // thread, and the finder comes from the damage history - they may have portalled or logged out since the
                // killing blow, so their pack belongs to another thread by now. Placing the piece in the world is what the
                // no-corpse path already does from here.
                var finder = drop.Finder;
                if (killed?.Location != null)
                {
                    piece.Location = new Position(killed.Location);
                    LandblockManager.AddObject(piece);
                    finder?.Session?.Network.EnqueueSend(new GameMessageSystemChat($"The {piece.NameWithMaterial} did not fit on the corpse and fell to the ground.", ChatMessageType.Broadcast));
                    drop.Placed = true;
                    log.Info($"[ZONELOOT] RARE: {piece.Name} (0x{piece.Guid}) was dropped on the ground at {killed.Location}.");
                }
                else
                {
                    log.Error($"[ZONELOOT] RARE LOST: {piece.Name} (0x{piece.Guid}, {piece.WeenieClassId}) for {finder?.Name} could not be placed anywhere.");
                    return;
                }
            }

            Announce(drop, killed);
        }
    }
}
