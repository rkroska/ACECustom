using System;
using System.Linq;

using ACE.Common;
using ACE.Database;
using ACE.DatLoader;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories;
using ACE.Server.Entity;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    public partial class PetDevice : WorldObject
    {
        /// <summary>
        /// Pure breeding-area rule shared by breeding, @breed-debug and CombatPet.IsInMotelOrEncounter.
        /// A configured value of 0 means "anywhere"; a 16-bit value (0x013A) matches the whole landblock;
        /// a value above 0xFFFF is a full raw cell (0x013A02AE) and matches that exact cell only.
        /// A configured variant of -1 ignores the variation; otherwise it must match exactly.
        /// </summary>
        public static bool MatchesBreedingArea(uint currentCell, int currentVariant, uint allowedLandblock, int allowedVariant)
        {
            bool locValid;
            if (allowedLandblock == 0)
                locValid = true;
            else if (allowedLandblock > 0xFFFF)
                locValid = currentCell == allowedLandblock;
            else
                locValid = (currentCell >> 16) == allowedLandblock;

            var varValid = allowedVariant == -1 || currentVariant == allowedVariant;

            return locValid && varValid;
        }

        /// <summary>
        /// True when the position is inside the configured breeding area (pet_breeding_allowed_landblock /
        /// pet_breeding_allowed_variant). See MatchesBreedingArea for the matching rules.
        /// </summary>
        public static bool IsInBreedingArea(Position location, int? currentVariant)
        {
            if (location == null)
                return false;

            var allowedLandblock = (uint)ServerConfig.pet_breeding_allowed_landblock.Value;
            var allowedVariant = (int)ServerConfig.pet_breeding_allowed_variant.Value;

            return MatchesBreedingArea(location.Cell, currentVariant ?? -1, allowedLandblock, allowedVariant);
        }

        /// <summary>
        /// True when the player stands inside the configured breeding area.
        /// </summary>
        private static bool IsInBreedingArea(Player player)
        {
            if (player?.Location == null)
                return false;

            return IsInBreedingArea(player.Location, player.CurrentLandblock?.VariationId);
        }

        /// <summary>
        /// Pure inheritance / mutation / cap arithmetic for breeding, kept free of world state so the
        /// website simulator (ClientApp/src/utils/breedingModel.ts) and the unit tests can mirror it
        /// exactly. The live breed path calls <see cref="Simulate"/> for every decision it makes and
        /// <see cref="RollAwakenedBlessing"/> when a mating guardian dies; nothing in here reads a
        /// device, a player, ServerConfig or the global RNG.
        ///
        /// Random draw order (the contract with breedingModel.ts and the [REPLAY] blobs):
        ///   1-8  inheritance, one uniform draw per line: damage, damageResist, crit, critDamage,
        ///        critResist, critDamageResist, vitality, potency. roll &lt; 0.55 takes the parent with
        ///        the HIGHER effective value (ties favour parent A), otherwise the lower one.
        ///   9    stat mutation roll (always drawn; ignored when ForceMutation is on).
        ///   10   stat line pick, ONLY when a stat mutation happens and a line is eligible:
        ///        index = min(n - 1, floor(roll * n)) over [dmg, dr, crit, vit] minus capped lines.
        ///   11   potency mutation roll.
        ///   12   Awakened Blessing pick, ONLY when the guardian spawned (GuardianEnabled and the breed
        ///        mutated) and was killed: eligible stat lines, then potency when its capped step &gt; 0.
        /// breedingModel.ts and this class must change together.
        /// </summary>
        public static class BreedingMath
        {
            /// <summary>Chance that a line is inherited from the parent with the higher effective value.</summary>
            public const double HigherParentChance = 0.55;

            /// <summary>Mutation line ids. The numeric values are what PetLastMutatedStat stores.</summary>
            public enum MutationLine
            {
                None = 0,
                Damage = 1,
                DamageResist = 2,
                Crit = 3,
                Vitality = 4,
                Potency = 5,
            }

            /// <summary>Client-safe display name of a mutation line.</summary>
            public static string LineName(MutationLine line) => line switch
            {
                MutationLine.Damage => "Damage Rating",
                MutationLine.DamageResist => "Damage Resist Rating",
                MutationLine.Crit => "Crit Rating",
                MutationLine.Vitality => "Vitality",
                MutationLine.Potency => "Potency",
                _ => "None",
            };

            /// <summary>The heritable state of one pet, exactly as the server stores it on its device.</summary>
            public struct BreedingGenetics
            {
                /// <summary>Clean base ratings rolled by loot; mutations are NOT folded into these.</summary>
                public int GearDamage, GearDamageResist, GearCrit, GearCritDamage, GearCritResist, GearCritDamageResist;
                /// <summary>Mutation counts per line.</summary>
                public int Dmg, Dr, Crit, Vit, Pot;
                /// <summary>Complete effective stored potency (mutations already included).</summary>
                public int PotencyStored;

                /// <summary>Sum of the four stat-line counts: the number that drives mutation decay.</summary>
                public int StatMutations => Dmg + Dr + Crit + Vit;
                public int TotalMutations => StatMutations + Pot;

                public int GetCount(MutationLine line) => line switch
                {
                    MutationLine.Damage => Dmg,
                    MutationLine.DamageResist => Dr,
                    MutationLine.Crit => Crit,
                    MutationLine.Vitality => Vit,
                    MutationLine.Potency => Pot,
                    _ => 0,
                };

                public bool SameAs(in BreedingGenetics other)
                    => GearDamage == other.GearDamage && GearDamageResist == other.GearDamageResist && GearCrit == other.GearCrit
                    && GearCritDamage == other.GearCritDamage && GearCritResist == other.GearCritResist && GearCritDamageResist == other.GearCritDamageResist
                    && Dmg == other.Dmg && Dr == other.Dr && Crit == other.Crit && Vit == other.Vit && Pot == other.Pot
                    && PotencyStored == other.PotencyStored;

                /// <summary>One-line ASCII dump in the same shape the website logs (describeGenetics).</summary>
                public override string ToString()
                    => $"gear[dmg {GearDamage}, dr {GearDamageResist}, crit {GearCrit}, critDmg {GearCritDamage}, critRes {GearCritResist}, critDmgRes {GearCritDamageResist}] " +
                       $"counts[dmg {Dmg}, dr {Dr}, crit {Crit}, vit {Vit}, pot {Pot}] potencyStored {PotencyStored}";
            }

            /// <summary>
            /// The breeding config, field for field what /api/visualizer/breeding-config exposes to the
            /// website. Built from ServerConfig by <see cref="FromServerConfig"/> on the live path and from
            /// a [REPLAY] blob by the harness.
            /// </summary>
            public struct BreedingConfig
            {
                public double BaseMutationChance, PotencyMutationChance, MutationDecayRate, MutationMinFloor;
                public int DamageMutationStep, DrMutationStep, CritMutationStep, VitalityMutationStep, PotencyMutationStep;
                /// <summary>0 = no soft cap.</summary>
                public int PotencySoftCap;
                /// <summary>0 = no cap. The effective hard cap is the smallest positive of the two.</summary>
                public long PotencyHardCap, PotencyMaxStored;
                /// <summary>0 = uncapped per-line mutation count.</summary>
                public int MaxStatMutations;
                public bool ForceMutation, GuardianEnabled;

                public int ResolvedPotencyHardCap => ResolvePotencyHardCap(PotencyHardCap, PotencyMaxStored);

                public int StepFor(MutationLine line) => line switch
                {
                    MutationLine.Damage => DamageMutationStep,
                    MutationLine.DamageResist => DrMutationStep,
                    MutationLine.Crit => CritMutationStep,
                    MutationLine.Vitality => VitalityMutationStep,
                    MutationLine.Potency => PotencyMutationStep,
                    _ => 0,
                };

                public static BreedingConfig FromServerConfig() => new()
                {
                    BaseMutationChance = ServerConfig.pet_breeding_base_mutation_chance.Value,
                    PotencyMutationChance = ServerConfig.pet_breeding_potency_mutation_chance.Value,
                    MutationDecayRate = ServerConfig.pet_breeding_mutation_decay_rate.Value,
                    MutationMinFloor = ServerConfig.pet_breeding_mutation_min_floor.Value,
                    DamageMutationStep = (int)ServerConfig.pet_breeding_damage_mutation_step.Value,
                    DrMutationStep = (int)ServerConfig.pet_breeding_dr_mutation_step.Value,
                    CritMutationStep = (int)ServerConfig.pet_breeding_crit_mutation_step.Value,
                    VitalityMutationStep = (int)ServerConfig.pet_breeding_vitality_mutation_step.Value,
                    PotencyMutationStep = (int)ServerConfig.pet_breeding_potency_mutation_step.Value,
                    PotencySoftCap = (int)ServerConfig.pet_breeding_potency_soft_cap.Value,
                    PotencyHardCap = ServerConfig.pet_breeding_potency_hard_cap.Value,
                    PotencyMaxStored = ServerConfig.pet_potency_max_stored.Value,
                    MaxStatMutations = (int)ServerConfig.pet_breeding_max_stat_mutations.Value,
                    ForceMutation = ServerConfig.pet_breeding_force_mutation.Value,
                    GuardianEnabled = ServerConfig.pet_breeding_guardian_enabled.Value,
                };
            }

            /// <summary>Per-breed consumables and the simulator's guardian assumption.</summary>
            public struct BreedingOptions
            {
                /// <summary>Courtship Incense bonus stored on each parent's device (0, 0.025, 0.05, 0.10).</summary>
                public double IncenseA, IncenseB;
                /// <summary>Chromatic Catalyst active on a device: a rolled palette comes from the vibrant pool.</summary>
                public bool CatalystA, CatalystB;
                /// <summary>
                /// True when the parents kill the guardian, which adds the Awakened Blessing draw. The live
                /// breed passes false (the guardian's fate is unknown when the breed is decided) and rolls the
                /// blessing later through <see cref="RollAwakenedBlessing"/>, the same helper Simulate uses.
                /// </summary>
                public bool GuardianKilled;
            }

            public struct BreedingInputs
            {
                public BreedingGenetics ParentA, ParentB;
                public BreedingConfig Config;
                public BreedingOptions Options;
            }

            /// <summary>Result of one Awakened Blessing roll.</summary>
            public struct BlessingResult
            {
                /// <summary>Line that received the bonus, or None when every line was capped.</summary>
                public MutationLine Line;
                public int Step;
                public bool AllLinesCapped;
                /// <summary>The draw consumed (only meaningful when <see cref="Drew"/>).</summary>
                public double Roll;
                public bool Drew;
            }

            /// <summary>Everything <see cref="Simulate"/> decided, mirroring breedingModel.ts's BreedResult.</summary>
            public sealed class BreedingOutcome
            {
                public BreedingGenetics Baby;
                /// <summary>Baby stat-mutation count right after inheritance (what the decay curve used).</summary>
                public int InheritedStatMutations;
                public double IncenseBonus;

                public double StatChance, StatRoll;
                public bool StatMutated;
                /// <summary>Line that received the +1, or None (no mutation / every line capped).</summary>
                public MutationLine StatLine;
                public int StatStep;
                public bool StatAllLinesCapped;

                public double PotencyChance, PotencyRoll;
                public bool PotencyMutated, PotencyApplied, PotencySoftCapped;
                /// <summary>Capped step actually applied (0 when capped out).</summary>
                public int PotencyStep;

                /// <summary>Guardian gating applied: GuardianEnabled and the breed mutated.</summary>
                public bool GuardianSpawned, GuardianKilled;
                public MutationLine BlessingLine;
                public int BlessingStep;
                public bool BlessingAllLinesCapped;

                /// <summary>Every random draw consumed, in order. Replaying them reproduces this outcome exactly.</summary>
                public System.Collections.Generic.List<double> RngDraws = new();

                /// <summary>True when at least one mutation line changed (stat, potency or blessing).</summary>
                public bool HasMutation => StatLine != MutationLine.None || PotencyApplied || BlessingLine != MutationLine.None;

                /// <summary>The server rolls a new colour palette only for a mutated breed.</summary>
                public bool PaletteRolled => HasMutation;

                /// <summary>What PetLastMutatedStat records: the blessing, else the stat line, else potency.</summary>
                public MutationLine LastMutatedStat
                    => BlessingLine != MutationLine.None ? BlessingLine
                     : StatLine != MutationLine.None ? StatLine
                     : PotencyApplied ? MutationLine.Potency
                     : MutationLine.None;
            }

            /// <summary>
            /// The whole breed as one pure function of its inputs and a draw source. Deterministic and
            /// side-effect free: the same inputs and the same draws always give the same baby.
            /// <paramref name="nextDouble"/> must return a uniform double in [0, 1); production passes
            /// ThreadSafeRandom.Next(0.0f, 1.0f), tests pass a scripted list.
            /// </summary>
            public static BreedingOutcome Simulate(in BreedingInputs inputs, Func<double> nextDouble)
            {
                if (nextDouble == null)
                    throw new ArgumentNullException(nameof(nextDouble));

                var o = new BreedingOutcome();
                var config = inputs.Config;
                var options = inputs.Options;
                var parentA = inputs.ParentA;
                var parentB = inputs.ParentB;

                double Draw()
                {
                    var v = nextDouble();
                    o.RngDraws.Add(v);
                    return v;
                }

                // 1-8. Inheritance: independent 55/45 roll per line, higher effective value favoured.
                // Damage / damage resist / crit are package deals (gear AND count travel together); the
                // three derived crit lines are gear-only; vitality is count-only; potency compares the
                // stored value (missing = 0) and carries stored AND count.
                var baby = new BreedingGenetics();

                var dmgRes = InheritLine(parentA.GearDamage, parentA.Dmg, parentB.GearDamage, parentB.Dmg, config.DamageMutationStep, Draw());
                baby.GearDamage = dmgRes.Gear; baby.Dmg = dmgRes.Count;

                var drRes = InheritLine(parentA.GearDamageResist, parentA.Dr, parentB.GearDamageResist, parentB.Dr, config.DrMutationStep, Draw());
                baby.GearDamageResist = drRes.Gear; baby.Dr = drRes.Count;

                var critRes = InheritLine(parentA.GearCrit, parentA.Crit, parentB.GearCrit, parentB.Crit, config.CritMutationStep, Draw());
                baby.GearCrit = critRes.Gear; baby.Crit = critRes.Count;

                baby.GearCritDamage = InheritGearOnly(parentA.GearCritDamage, parentB.GearCritDamage, Draw());
                baby.GearCritResist = InheritGearOnly(parentA.GearCritResist, parentB.GearCritResist, Draw());
                baby.GearCritDamageResist = InheritGearOnly(parentA.GearCritDamageResist, parentB.GearCritDamageResist, Draw());

                var vitRes = InheritLine(0, parentA.Vit, 0, parentB.Vit, config.VitalityMutationStep, Draw());
                baby.Vit = vitRes.Count;

                var potRes = InheritPotency(parentA.PotencyStored, parentA.Pot, parentB.PotencyStored, parentB.Pot, Draw());
                baby.PotencyStored = potRes.Stored; baby.Pot = potRes.Count;

                o.InheritedStatMutations = baby.StatMutations;
                o.IncenseBonus = CombinedIncenseBonus(options.IncenseA, options.IncenseB);

                // 9. Stat mutation roll. Decay is driven by the BABY's inherited counts. The draw is always
                // consumed, even when force_mutation makes the result moot, so replays line up.
                o.StatChance = StatMutationChance(o.InheritedStatMutations, config, o.IncenseBonus);
                o.StatRoll = Draw();
                o.StatMutated = config.ForceMutation || o.StatRoll < o.StatChance;

                // 10. Stat line pick, only when a mutation happened and a line is still under its cap.
                if (o.StatMutated)
                {
                    var eligible = EligibleStatLines(baby, config.MaxStatMutations);
                    if (eligible.Count > 0)
                    {
                        var line = eligible[PickIndex(Draw(), eligible.Count)];
                        o.StatLine = line;
                        o.StatStep = config.StepFor(line);
                        AddMutation(ref baby, line, 0);
                    }
                    else
                        o.StatAllLinesCapped = true;
                }

                // 11. Potency roll: independent, incense does not apply.
                o.PotencyChance = Math.Clamp(config.PotencyMutationChance, 0.0, 1.0);
                o.PotencyRoll = Draw();
                o.PotencyMutated = o.PotencyRoll < o.PotencyChance;
                o.PotencySoftCapped = config.PotencySoftCap > 0 && baby.PotencyStored >= config.PotencySoftCap;
                if (o.PotencyMutated)
                {
                    o.PotencyStep = PotencyMutationStep(config.PotencyMutationStep, baby.PotencyStored, config.PotencySoftCap, config.ResolvedPotencyHardCap);
                    if (o.PotencyStep > 0)
                    {
                        AddMutation(ref baby, MutationLine.Potency, o.PotencyStep);
                        o.PotencyApplied = true;
                    }
                }

                // 12. Guardian and Awakened Blessing.
                o.GuardianSpawned = config.GuardianEnabled && (o.StatLine != MutationLine.None || o.PotencyApplied);
                if (o.GuardianSpawned && options.GuardianKilled)
                {
                    o.GuardianKilled = true;
                    var blessing = RollAwakenedBlessing(ref baby, config, Draw);
                    o.BlessingLine = blessing.Line;
                    o.BlessingStep = blessing.Step;
                    o.BlessingAllLinesCapped = blessing.AllLinesCapped;
                }

                o.Baby = baby;
                return o;
            }

            /// <summary>
            /// The Awakened Blessing (draw 12): one bonus mutation over the eligible stat lines plus
            /// potency when its capped step is positive. Consumes exactly one draw when any line is
            /// eligible and none otherwise. Called by <see cref="Simulate"/> and by the live guardian
            /// death handler, so both sides run the identical code.
            /// </summary>
            public static BlessingResult RollAwakenedBlessing(ref BreedingGenetics baby, in BreedingConfig config, Func<double> nextDouble)
            {
                var eligible = EligibleStatLines(baby, config.MaxStatMutations);
                var potStep = PotencyMutationStep(config.PotencyMutationStep, baby.PotencyStored, config.PotencySoftCap, config.ResolvedPotencyHardCap);
                if (potStep > 0)
                    eligible.Add(MutationLine.Potency);

                if (eligible.Count == 0)
                    return new BlessingResult { Line = MutationLine.None, AllLinesCapped = true };

                var roll = nextDouble();
                var line = eligible[PickIndex(roll, eligible.Count)];
                var step = line == MutationLine.Potency ? potStep : config.StepFor(line);
                AddMutation(ref baby, line, step);
                return new BlessingResult { Line = line, Step = step, Roll = roll, Drew = true };
            }

            /// <summary>Adds one mutation to a line. <paramref name="potencyStep"/> is only used for the potency line.</summary>
            public static void AddMutation(ref BreedingGenetics baby, MutationLine line, int potencyStep)
            {
                switch (line)
                {
                    case MutationLine.Damage: baby.Dmg += 1; break;
                    case MutationLine.DamageResist: baby.Dr += 1; break;
                    case MutationLine.Crit: baby.Crit += 1; break;
                    case MutationLine.Vitality: baby.Vit += 1; break;
                    case MutationLine.Potency: baby.PotencyStored += potencyStep; baby.Pot += 1; break;
                }
            }

            /// <summary>Stat lines still under the per-line cap, in pick order [dmg, dr, crit, vit]. 0 = uncapped.</summary>
            public static System.Collections.Generic.List<MutationLine> EligibleStatLines(in BreedingGenetics baby, int maxStatMutations)
            {
                var eligible = new System.Collections.Generic.List<MutationLine>(4);
                if (maxStatMutations <= 0 || baby.Dmg < maxStatMutations) eligible.Add(MutationLine.Damage);
                if (maxStatMutations <= 0 || baby.Dr < maxStatMutations) eligible.Add(MutationLine.DamageResist);
                if (maxStatMutations <= 0 || baby.Crit < maxStatMutations) eligible.Add(MutationLine.Crit);
                if (maxStatMutations <= 0 || baby.Vit < maxStatMutations) eligible.Add(MutationLine.Vitality);
                return eligible;
            }

            /// <summary>Uniform pick of one of <paramref name="count"/> entries from a [0, 1) draw: min(n - 1, floor(roll * n)).</summary>
            public static int PickIndex(double roll, int count)
                => Math.Max(0, Math.Min(count - 1, (int)Math.Floor(roll * count)));

            /// <summary>Both parents' incense bonuses summed and clamped to +50%.</summary>
            public static double CombinedIncenseBonus(double incenseA, double incenseB)
                => Math.Clamp(incenseA + incenseB, 0.0, 0.50);

            /// <summary>
            /// clamp(max(floor, (base + incense) / (1 + decay * babyStatMutations)), 0, 1). Incense decays with the
            /// line like the base does, so a heavily mutated line slows down for paying players too; added after
            /// the decay it held the chance near base + incense forever.
            /// </summary>
            public static double StatMutationChance(int babyStatMutations, in BreedingConfig config, double incenseBonus)
            {
                var decayed = (config.BaseMutationChance + incenseBonus) / (1.0 + config.MutationDecayRate * babyStatMutations);
                return Math.Clamp(Math.Max(config.MutationMinFloor, decayed), 0.0, 1.0);
            }

            /// <summary>Effective value of a line: gear base plus mutation count times step.</summary>
            public static int Effective(int gear, int count, int step) => gear + count * step;

            /// <summary>
            /// The 55/45 rule. Returns true when parent 1 is chosen. Ties count parent 1 as the higher
            /// parent. <paramref name="roll"/> is uniform in [0, 1).
            /// </summary>
            public static bool PicksParent1(int effective1, int effective2, double roll)
            {
                var isHigh1 = effective1 >= effective2;
                return roll < HigherParentChance ? isHigh1 : !isHigh1;
            }

            /// <summary>
            /// Package-deal inheritance of one line: the chosen parent's gear value AND mutation count
            /// travel together. Compared on gear + count * step.
            /// </summary>
            public static (int Gear, int Count) InheritLine(int gear1, int count1, int gear2, int count2, int step, double roll)
            {
                var pick1 = PicksParent1(Effective(gear1, count1, step), Effective(gear2, count2, step), roll);
                return pick1 ? (gear1, count1) : (gear2, count2);
            }

            /// <summary>
            /// Gear-only line (crit damage, crit resist, crit damage resist): 55/45 on the gear value.
            /// </summary>
            public static int InheritGearOnly(int gear1, int gear2, double roll)
                => PicksParent1(gear1, gear2, roll) ? gear1 : gear2;

            /// <summary>
            /// Potency: the stored value is already the complete effective value (missing = 0), so the
            /// comparison is on the stored value alone; the chosen parent's stored value and potency
            /// mutation count travel together.
            /// </summary>
            public static (int Stored, int Count) InheritPotency(int stored1, int count1, int stored2, int count2, double roll)
                => PicksParent1(stored1, stored2, roll) ? (stored1, count1) : (stored2, count2);

            /// <summary>
            /// Effective potency hard cap: the smallest POSITIVE of pet_breeding_potency_hard_cap and
            /// pet_potency_max_stored. 0 = uncapped.
            /// </summary>
            public static int ResolvePotencyHardCap(long breedingHardCap, long maxStored)
            {
                var caps = new[] { breedingHardCap, maxStored }.Where(c => c > 0).ToList();
                if (caps.Count == 0)
                    return 0;
                var min = caps.Min();
                return min > int.MaxValue ? int.MaxValue : (int)min;
            }

            /// <summary>
            /// Potency gained by one potency mutation from <paramref name="currentStored"/>: the configured
            /// step, quartered (min 1) at or above the soft cap, and clamped so the hard cap is never
            /// overshot. 0 means the line is capped and no mutation can be applied.
            /// </summary>
            public static int PotencyMutationStep(int stepConfig, int currentStored, int softCap, int hardCap)
            {
                var step = stepConfig;
                if (softCap > 0 && currentStored >= softCap)
                    step = Math.Max(1, stepConfig / 4);

                if (hardCap > 0)
                    step = Math.Min(step, Math.Max(0, hardCap - currentStored));

                return Math.Max(0, step);
            }

            /// <summary>
            /// Half-up rounding, the same rule as JavaScript's Math.round for non-negative values.
            /// C#'s default Math.Round is banker's rounding (2.5 -&gt; 2, 4.5 -&gt; 4) and disagrees with
            /// the website on every exact .5; never use bare Math.Round on a value that can land on .5.
            /// </summary>
            public static int RoundHalfUp(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

            /// <summary>Crit damage bonus derived from damage mutations (same rule as the summon path).</summary>
            public static int MutCritDamage(int mutDamage) => RoundHalfUp(mutDamage * 0.8);

            /// <summary>Crit resist bonus derived from damage resist mutations (same rule as the summon path).</summary>
            public static int MutCritResist(int mutDamageResist) => RoundHalfUp(mutDamageResist * 0.8);

            /// <summary>Crit damage resist bonus derived from damage resist mutations (same rule as the summon path).</summary>
            public static int MutCritDamageResist(int mutDamageResist) => RoundHalfUp(mutDamageResist * 0.6);

            /// <summary>Ratings a pet shows when summoned, evaluated from its stored genetics.</summary>
            public struct SummonedRatings
            {
                public int DamageRating, DamageResistRating, CritRating, CritDamageRating, CritResistRating, CritDamageResistRating;
                /// <summary>Bonus HP from vitality mutations (added on top of the species base).</summary>
                public int BonusHp;
                public int PotencyStored;

                public override string ToString()
                    => $"DR {DamageRating} / DRR {DamageResistRating} / Crit {CritRating} / CD {CritDamageRating} / CR {CritResistRating} / CDR {CritDamageResistRating} / HP +{BonusHp}";
            }

            /// <summary>Default juvenile growth multipliers by stage 1..5 (pet_maturity_stages 5, pet_maturity_juvenile_strength 0.5).</summary>
            public static readonly double[] DefaultMaturityMultipliers = { 0.5, 0.6, 0.7, 0.8, 0.9 };

            /// <summary>
            /// Strength multiplier of a juvenile at <paramref name="stage"/> (1..stages); anything else is
            /// adult (1.0). Same lerp as PetDevice.MaturityStrengthMult. With the default 5 stages and
            /// 0.5 strength this is exactly 0.5 / 0.6 / 0.7 / 0.8 / 0.9 (bit-identical to the literals).
            /// </summary>
            public static double MaturityMultiplier(int stage, int stages = 5, double juvenileStrength = 0.5)
            {
                if (stage < 1 || stages < 1 || stage > stages)
                    return 1.0;
                var from = Math.Clamp(juvenileStrength, 0.05, 1.0);
                var t = Math.Clamp((stage - 1) / (double)stages, 0.0, 1.0);
                return from + (1.0 - from) * t;
            }

            /// <summary>
            /// The summon-time arithmetic (CombatPet.Init + ApplyMaturity): gear + count x step per line,
            /// the derived crit lines, vitality HP, and the juvenile multiplier with half-up rounding.
            /// <paramref name="stage"/> 1..5 is a juvenile; 0 (or above 5) is an adult.
            /// </summary>
            public static SummonedRatings SummonedStats(in BreedingGenetics pet, in BreedingConfig config, int stage = 0)
            {
                var m = MaturityMultiplier(stage);
                var dmgBonus = pet.Dmg * config.DamageMutationStep;
                var drBonus = pet.Dr * config.DrMutationStep;
                int Scale(int v) => RoundHalfUp(v * m);
                return new SummonedRatings
                {
                    DamageRating = Scale(pet.GearDamage + dmgBonus),
                    DamageResistRating = Scale(pet.GearDamageResist + drBonus),
                    CritRating = Scale(pet.GearCrit + pet.Crit * config.CritMutationStep),
                    CritDamageRating = Scale(pet.GearCritDamage + MutCritDamage(dmgBonus)),
                    CritResistRating = Scale(pet.GearCritResist + MutCritResist(drBonus)),
                    CritDamageResistRating = Scale(pet.GearCritDamageResist + MutCritDamageResist(drBonus)),
                    BonusHp = Scale(pet.Vit * config.VitalityMutationStep),
                    PotencyStored = pet.PotencyStored,
                };
            }
        }

        /// <summary>
        /// This device's heritable state as <see cref="BreedingMath"/> sees it. Legacy devices without
        /// mutation counts fall back to the stored rating divided by the step.
        /// </summary>
        public BreedingMath.BreedingGenetics ReadBreedingGenetics(in BreedingMath.BreedingConfig config)
        {
            int MutCount(PropertyInt propCount, PropertyInt propLegacyRating, int step)
                => GetProperty(propCount) ?? ((GetProperty(propLegacyRating) ?? 0) / Math.Max(1, step));

            return new BreedingMath.BreedingGenetics
            {
                GearDamage = GearDamage ?? 0,
                GearDamageResist = GearDamageResist ?? 0,
                GearCrit = GearCrit ?? 0,
                GearCritDamage = GearCritDamage ?? 0,
                GearCritResist = GearCritResist ?? 0,
                GearCritDamageResist = GearCritDamageResist ?? 0,
                Dmg = MutCount(PropertyInt.PetMutDamageCount, PropertyInt.PetMutDamageRating, config.DamageMutationStep),
                Dr = MutCount(PropertyInt.PetMutDamageResistCount, PropertyInt.PetMutDamageResistRating, config.DrMutationStep),
                Crit = MutCount(PropertyInt.PetMutCritCount, PropertyInt.PetMutCritRating, config.CritMutationStep),
                Vit = MutCount(PropertyInt.PetMutVitalityCount, PropertyInt.PetMutVitality, config.VitalityMutationStep),
                Pot = MutCount(PropertyInt.PetMutPotencyCount, PropertyInt.PetMutPotency, config.PotencyMutationStep),
                PotencyStored = PetPotencyStored ?? 0,
            };
        }

        /// <summary>
        /// Everything decided about a breed BEFORE the baby object exists: participants, the species
        /// donor, the rolled palette, and every inherited/mutated stat. CompleteBirth consumes it.
        /// Splitting the decision from the birth lets the birth be deferred (e.g. until a mating
        /// guardian dies) without recomputing or re-rolling anything.
        /// </summary>
        private sealed class PendingBreed
        {
            public Player Player1, Partner, Winner;
            public PetDevice Donor, Device1, Device2;
            public CombatPet Pet1, Pet2;
            public uint BabyWcid;
            public uint? BabyPaletteBase;
            public System.Collections.Generic.List<string> MutationSummary;
            /// <summary>
            /// The baby as BreedingMath.Simulate decided it: inherited Gear* base ratings, mutation counts
            /// (evaluated against the live step config at summon time) and stored potency. The Awakened
            /// Blessing adds to it in OnGuardianSlain.
            /// </summary>
            public BreedingMath.BreedingGenetics Baby;
            /// <summary>The config the breed was rolled with; the blessing uses the same caps and steps.</summary>
            public BreedingMath.BreedingConfig Config;
            /// <summary>The full simulator inputs and every draw consumed so far, for the [REPLAY] log line.</summary>
            public BreedingMath.BreedingInputs Inputs;
            public System.Collections.Generic.List<double> RngDraws;

            // Effective (summon-time) ratings: gear + count * step, with the crit lines derived the same
            // way CombatPet.Init derives them. Used to stat the mating guardian.
            public BreedingMath.SummonedRatings EffectiveRatings => BreedingMath.SummonedStats(Baby, Config);

            // Phase 2 (mating guardian) bookkeeping.
            public uint GuardianGuid;
            public bool GuardianWeakened;
            public int LastMutatedStat;

            /// <summary>[PetTrace] session id minted at the dance; null when the trace was off.</summary>
            public string TraceSession;
        }

        /// <summary>
        /// Breeds waiting on a mating guardian, keyed by guardian GUID. In-memory only: a server
        /// restart drops them (the parents already paid; the guardian cannot survive a restart).
        /// </summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, PendingBreed> pendingGuardianBreeds = new();

        /// <summary>
        /// The live Player object for a player captured earlier, or the captured object itself if that
        /// character is no longer online. Player.Session is never nulled on logout, so a captured
        /// reference alone cannot tell the two apart.
        /// </summary>
        private static Player ResolveLivePlayer(Player captured)
        {
            if (captured == null)
                return null;
            return PlayerManager.GetOnlinePlayer(captured.Guid.Full) ?? captured;
        }

        private static PendingBreed FindPendingGuardianBreed(Player player)
        {
            if (player == null) return null;
            foreach (var p in pendingGuardianBreeds.Values)
            {
                if (p.Player1?.Guid.Full == player.Guid.Full || p.Partner?.Guid.Full == player.Guid.Full)
                    return p;
            }
            return null;
        }

        /// <param name="forced">Set by the admin @breed command: skips the mutual-dance requirement and
        /// tells both owners the ritual was forced rather than performed.</param>
        public static void CheckMultiplayerBreeding(Player player1, string triggerSource = "Manual", bool forced = false)
        {
            if (player1 == null)
                return;

            // [PetTrace] one session id per breed attempt; every record of this attempt (the guardian's
            // included, seconds later) carries it. Minted only when the trace is on.
            var trace = PetTrace.Enabled;
            var session = trace ? PetTrace.NewSessionId() : null;

            if (ServerConfig.pet_breeding_verbose_logging.Value)
                log.Info($"[PetBreeding] Breeding trigger received from {player1.Name} (Source: {triggerSource}, LB: 0x{player1.Location.Landblock:X4}, Cell: 0x{player1.Location.LandblockId.Raw:X8})");

            if (trace)
                PetTrace.Begin("breed.trigger", session).AddPlayer("p1.", player1).Add("source", triggerSource).Add("forced", forced)
                    .AddGuid("cell", player1.Location?.Cell ?? 0).Add("variant", player1.CurrentLandblock?.VariationId ?? -1).Emit();

            if (!ServerConfig.pet_breeding_enabled.Value)
            {
                if (ServerConfig.pet_breeding_verbose_logging.Value)
                    log.Warn($"[PetBreeding] Breeding aborted: ServerConfig.pet_breeding_enabled is false.");
                if (trace) PetTrace.BreedGate(session, "enabled", "pet_breeding_enabled is false", player1, null);
                if (player1.IsAdmin) player1.SendMessage("[Breeding Debug] Breeding failed: ServerConfig.pet_breeding_enabled is FALSE.");
                return;
            }

            if (player1.IsTrading)
            {
                if (trace) PetTrace.BreedGate(session, "trading", "player is in trade", player1, null);
                if (player1.IsAdmin) player1.SendMessage("[Breeding Debug] Breeding failed: Player is in trade.");
                return;
            }

            // 1. Check if player1 has an active summoned combat pet
            if (player1.CurrentActivePet is not CombatPet pet1)
            {
                if (trace) PetTrace.BreedGate(session, "noPet", "no active combat pet summoned", player1, null);
                if (player1.IsAdmin) player1.SendMessage("[Breeding Debug] Breeding failed: You do not have an active Combat Pet summoned.");
                return;
            }

            // 2. Check if player1 is in the allowed breeding area (unless Admin bypass)
            // Admin bypass covers LOCATION checks only (area, landblock, pet landcell) as a testing
            // convenience. It must NOT skip the economy - sex pairing, breeding charges, recovery
            // cooldown - or admins can never test those rules, and they silently never apply to admin
            // characters. Use @pet-reset-cooldown to reset charges/cooldown between test breeds.
            var isAdminBypass = player1.IsAdmin;
            if (player1.Location == null)
                return;

            var currentLandblock = player1.Location.Landblock;

            if (!IsInBreedingArea(player1))
            {
                if (ServerConfig.pet_breeding_verbose_logging.Value)
                    log.Info($"[PetBreeding] {player1.Name} location check failed: current LB=0x{currentLandblock:X4} (Cell=0x{player1.Location.Cell:X8}, Var={player1.CurrentLandblock?.VariationId ?? -1}).");
                if (trace)
                    PetTrace.BreedGate(session, "location",
                        $"cell 0x{player1.Location.Cell:X8} variant {player1.CurrentLandblock?.VariationId ?? -1} is outside allowed 0x{ServerConfig.pet_breeding_allowed_landblock.Value:X} variant {ServerConfig.pet_breeding_allowed_variant.Value}; adminBypass={isAdminBypass}",
                        player1, null);
                if (isAdminBypass)
                {
                    player1.SendMessage($"[Breeding Debug] Location check would fail for non-admins (current LB 0x{currentLandblock:X4}), but bypassed for Admin.");
                }
                else
                {
                    player1.SendTransientError("You must be in the designated breeding area to perform the breeding ritual.");
                    return;
                }
            }

            // Both partners must have danced within the sync window. player1 just danced (this call is
            // driven by that emote); the partner is checked below.
            var danceWindow = TimeSpan.FromSeconds(ServerConfig.pet_breeding_dance_sync_seconds.Value);
            var nowUtc = DateTime.UtcNow;

            var device1 = pet1.TryGetSummoningDevice() ?? player1.FindObject(pet1.SummoningDeviceGuid.Full, Player.SearchLocations.Everywhere) as PetDevice;
            if (device1 == null)
            {
                if (trace) PetTrace.BreedGate(session, "device1", $"summoning device 0x{pet1.SummoningDeviceGuid.Full:X8} of pet {pet1.Name} not found", player1, null);
                player1.SendTransientError("Failed to locate parent summoning device.");
                return;
            }

            // 3. Scan for a partner: same landblock, also inside the breeding area, who danced within
            //    the sync window, and whose pet shares a landcell with ours.
            var onlinePlayers = PlayerManager.GetAllOnline();
            var roomCandidates = new System.Collections.Generic.List<(Player Player, CombatPet Pet)>();

            foreach (var otherPlayer in onlinePlayers)
            {
                if (otherPlayer.Guid == player1.Guid)
                    continue;

                if (otherPlayer.IsTrading || otherPlayer.Location == null)
                {
                    if (player1.IsAdmin && otherPlayer.IsTrading) player1.SendMessage($"[Breeding Debug] Skipping {otherPlayer.Name}: Currently in trade.");
                    continue;
                }

                var sameLandblock = otherPlayer.Location.Landblock == player1.Location.Landblock;
                var dist = player1.GetDistance(otherPlayer);

                if (!sameLandblock && !isAdminBypass)
                {
                    if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Skipping {otherPlayer.Name}: Different landblock (0x{otherPlayer.Location.Landblock:X4}).");
                    continue;
                }

                if (!IsInBreedingArea(otherPlayer) && !isAdminBypass)
                {
                    if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Skipping {otherPlayer.Name}: Not inside the designated breeding area.");
                    continue;
                }

                var sinceTheirDance = nowUtc - otherPlayer.LastDanceTime;
                if (sinceTheirDance > danceWindow && !forced)
                {
                    if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Skipping {otherPlayer.Name}: Has not danced within {danceWindow.TotalSeconds:0.#}s (last dance {sinceTheirDance.TotalSeconds:0.#}s ago).");
                    continue;
                }

                if (otherPlayer.CurrentActivePet is not CombatPet otherPet)
                {
                    if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Found nearby player {otherPlayer.Name} ({dist:F1}m away), but they have NO active combat pet summoned.");
                    continue;
                }

                if (pet1.Location == null || otherPet.Location == null)
                {
                    if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Skipping {otherPlayer.Name}: A pet has no world location yet (still spawning or teleporting).");
                    continue;
                }

                if (pet1.Location.Cell != otherPet.Location.Cell && !isAdminBypass)
                {
                    if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Skipping {otherPlayer.Name}: Their pet is in landcell 0x{otherPet.Location.Cell:X8}, yours is in 0x{pet1.Location.Cell:X8}. The pets must share a landcell.");
                    continue;
                }

                roomCandidates.Add((otherPlayer, otherPet));
            }

            if (roomCandidates.Count == 0)
            {
                if (trace)
                    PetTrace.BreedGate(session, "partnerScan",
                        $"no room candidate among {onlinePlayers.Count} online players (same landblock, in area, danced within {danceWindow.TotalSeconds:0.#}s, pet in landcell 0x{pet1.Location?.Cell ?? 0:X8}); forced={forced}",
                        player1, null);
                if (player1.IsAdmin)
                    player1.SendMessage($"[Breeding Debug] No eligible partner found: needs to be in the breeding area, have danced within {danceWindow.TotalSeconds:0.#}s, and have a summoned pet sharing your pet's landcell. (Checked {onlinePlayers.Count} online players)");
                else if (!forced)
                    player1.SendMessage($"[Breeding] Your pet performs the courtship dance, waiting for a partner... (Your partner must have their pet summoned in the same room and *dance* within {danceWindow.TotalSeconds:0.#} seconds).");
                return;
            }

            // In crowded rooms with multiple pairs dancing, prioritize candidates whose pets are mutually compatible
            // (opposite sex, not neutered, adult, charges/cooldowns ready) so bystanders do not block valid pairs.
            bool IsCandidateCompatible(Player p, CombatPet cp)
            {
                if (p.IsBusy) return false;
                var dev = cp.TryGetSummoningDevice() ?? p.FindObject(cp.SummoningDeviceGuid.Full, Player.SearchLocations.Everywhere) as PetDevice;
                if (dev == null) return false;
                if (device1.IsMale == dev.IsMale) return false;
                if (device1.GetProperty(PropertyBool.PetNeutered) == true || dev.GetProperty(PropertyBool.PetNeutered) == true) return false;
                if (device1.IsJuvenile || dev.IsJuvenile) return false;
                if (!ServerConfig.pet_breeding_allow_shiny.Value && (device1.IsShiny || dev.IsShiny)) return false;

                if (!ServerConfig.pet_breeding_bypass_male_charges.Value)
                {
                    var maleDev = device1.IsMale ? device1 : dev;
                    var charges = maleDev.GetProperty(PropertyInt.PetMaleBreedingCharges) ?? (int)ServerConfig.pet_breeding_male_max_charges.Value;
                    if (charges <= 0) return false;
                }

                if (!ServerConfig.pet_breeding_bypass_female_cooldown.Value)
                {
                    var femaleDev = device1.IsMale ? dev : device1;
                    var nextBreed = femaleDev.GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                    if (nextBreed > (double)DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;
                }

                return true;
            }

            var compatibleCandidates = roomCandidates.Where(c => IsCandidateCompatible(c.Player, c.Pet)).ToList();
            var chosen = compatibleCandidates.Count > 0
                ? compatibleCandidates.OrderBy(c => player1.GetDistance(c.Player)).First()
                : roomCandidates.OrderBy(c => player1.GetDistance(c.Player)).First();

            Player partner = chosen.Player;
            CombatPet pet2 = chosen.Pet;
            if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Matched partner {partner.Name} ({player1.GetDistance(partner):F1}m away) with summoned pet {pet2.Name} in landcell 0x{pet2.Location.Cell:X8}!");

            if (player1.IsBusy || partner.IsBusy)
            {
                if (trace) PetTrace.BreedGate(session, "busy", $"player1 busy={player1.IsBusy}, partner busy={partner.IsBusy}", player1, partner);
                if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Breeding aborted: Player or partner is busy.");
                return;
            }

            var pendingFor = FindPendingGuardianBreed(player1) ?? FindPendingGuardianBreed(partner);
            if (pendingFor != null)
            {
                var pendingIsMine = pendingFor.Player1?.Guid.Full == player1.Guid.Full || pendingFor.Partner?.Guid.Full == player1.Guid.Full;
                if (trace) PetTrace.BreedGate(session, "pendingGuardian", $"a mating guardian 0x{pendingFor.GuardianGuid:X8} (session {pendingFor.TraceSession ?? "none"}) is still standing for {(pendingIsMine ? player1.Name : partner.Name)}", player1, partner);
                player1.SendMessage(pendingIsMine
                    ? "[Breeding] You already have a mating guardian to defeat. Finish that ritual first."
                    : $"[Breeding] {partner.Name} already has a mating guardian to defeat. They must finish that ritual first.");
                partner.SendMessage("[Breeding] A mating guardian is still standing from a previous ritual. Finish that one first.");
                return;
            }

            player1.IsBusy = true;
            partner.IsBusy = true;

            // Every refusal below is a transient error, which the client flashes centre-screen and
            // never writes to chat, so a failed breed looks like nothing happened. Echo the reason
            // into an admin's chat window so a test tells you which gate stopped it, and write the
            // same reason to the trace under a stable gate name.
            void DebugGate(string gate, string reason)
            {
                if (trace) PetTrace.BreedGate(session, gate, reason, player1, partner);
                if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Gate failed: {reason}");
                if (partner.IsAdmin && partner != player1) partner.SendMessage($"[Breeding Debug] Gate failed: {reason}");
            }

            try
            {
                var device2 = pet2.TryGetSummoningDevice() ?? partner.FindObject(pet2.SummoningDeviceGuid.Full, Player.SearchLocations.Everywhere) as PetDevice;

                if (device2 == null)
                {
                    DebugGate("device2", $"partner {partner.Name}'s summoning device could not be found (pet {pet2.Name}, device guid 0x{pet2.SummoningDeviceGuid.Full:X8}).");
                    player1.SendTransientError("Failed to locate parent summoning devices.");
                    partner.SendTransientError("Failed to locate parent summoning devices.");
                    return;
                }

                // Both devices are known: record the whole attempt (both players, both devices, every
                // rating, count, flag and stamp) and the effective config before any gate can refuse.
                if (trace)
                {
                    PetTrace.Begin("breed.attempt", session).AddPlayer("p1.", player1).AddPlayer("p2.", partner)
                        .Add("source", triggerSource).Add("forced", forced).Add("adminBypass", isAdminBypass)
                        .Add("pet1", pet1.Name).AddGuid("pet1Guid", pet1.Guid.Full).Add("pet1Level", pet1.Level ?? 0).Add("pet1MaxHp", pet1.Health?.MaxValue ?? 0)
                        .Add("pet2", pet2.Name).AddGuid("pet2Guid", pet2.Guid.Full).Add("pet2Level", pet2.Level ?? 0).Add("pet2MaxHp", pet2.Health?.MaxValue ?? 0)
                        .AddGuid("cell1", pet1.Location?.Cell ?? 0).AddGuid("cell2", pet2.Location?.Cell ?? 0)
                        .Add("candidates", roomCandidates.Count).Add("compatible", compatibleCandidates.Count)
                        .AddDevice("a.", device1, player1).AddDevice("b.", device2, partner)
                        .Emit();
                    PetTrace.BreedConfig(session, BreedingMath.BreedingConfig.FromServerConfig());
                }

                if (device1.GetProperty(PropertyBool.PetNeutered) == true)
                {
                    DebugGate("neutered", $"{device1.Name} (yours) is neutered.");
                    player1.SendTransientError("Your pet is spayed/neutered and cannot breed.");
                    return;
                }

                if (device2.GetProperty(PropertyBool.PetNeutered) == true)
                {
                    DebugGate("neutered", $"{device2.Name} ({partner.Name}'s) is neutered.");
                    player1.SendTransientError($"{partner.Name}'s pet is spayed/neutered and cannot breed.");
                    partner.SendTransientError("Your pet is spayed/neutered and cannot breed.");
                    return;
                }

                var inInv1 = player1.FindObject(device1.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) != null;
                var inInv2 = partner.FindObject(device2.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) != null;
                if (!inInv1 || !inInv2)
                {
                    DebugGate("inventory", $"a device left its owner's inventory (yours in inventory: {inInv1}, {partner.Name}'s: {inInv2}).");
                    player1.SendTransientError("Summoning devices must remain in inventory to breed.");
                    partner.SendTransientError("Summoning devices must remain in inventory to breed.");
                    return;
                }

                var lvl1 = global::ACE.Server.Factories.Tables.Wcids.PetDeviceWcids.GetPetLevel(device1.WeenieClassId);
                var lvl2 = global::ACE.Server.Factories.Tables.Wcids.PetDeviceWcids.GetPetLevel(device2.WeenieClassId);
                if (!lvl1.HasValue || !lvl2.HasValue)
                {
                    DebugGate("tierLookup", $"tier lookup failed (wcid {device1.WeenieClassId} -> {(lvl1.HasValue ? lvl1.Value.ToString() : "none")}, " +
                              $"wcid {device2.WeenieClassId} -> {(lvl2.HasValue ? lvl2.Value.ToString() : "none")}). Only devices listed in PetDeviceWcids have a tier.");
                    player1.SendTransientError("Failed to determine parent pet tiers.");
                    partner.SendTransientError("Failed to determine parent pet tiers.");
                    return;
                }

                var minParentLevel = (int)ServerConfig.pet_breeding_min_parent_level.Value;
                if (lvl1.Value < minParentLevel || lvl2.Value < minParentLevel)
                {
                    var msg = $"Parent pets must be at least tier {minParentLevel} to breed.";
                    DebugGate("minParentLevel", $"tier below pet_breeding_min_parent_level {minParentLevel} (yours {lvl1.Value}, {partner.Name}'s {lvl2.Value}).");
                    player1.SendTransientError(msg);
                    partner.SendTransientError(msg);
                    return;
                }

                var minBond = (int)ServerConfig.pet_breeding_min_bond.Value;
                var bond1 = device1.PetBondLevel ?? 1;
                var bond2 = device2.PetBondLevel ?? 1;
                if (bond1 < minBond || bond2 < minBond)
                {
                    var msg = $"Parent pets must have a bond level of at least {minBond} to breed.";
                    DebugGate("minBond", $"bond below pet_breeding_min_bond {minBond} (yours {bond1}, {partner.Name}'s {bond2}). " +
                              $"pet_bond_enabled is {ServerConfig.pet_bond_enabled.Value}; bond only grows while it is TRUE.");
                    player1.SendTransientError(msg);
                    partner.SendTransientError(msg);
                    return;
                }

                // Shiny is a capture-only trait: it is never bred for and never inherited.
                if (!ServerConfig.pet_breeding_allow_shiny.Value)
                {
                    foreach (var (dev, owner) in new[] { (device1, player1), (device2, partner) })
                    {
                        if (!dev.IsShiny)
                            continue;
                        var other = owner == player1 ? partner : player1;
                        DebugGate("shiny", $"{dev.Name} ({owner.Name}'s) is shiny and pet_breeding_allow_shiny is false.");
                        owner.SendTransientError($"{dev.Name} is shiny and cannot breed. Shiny is a capture-only trait.");
                        other.SendTransientError($"Breeding cancelled: {owner.Name}'s pet is shiny and cannot breed.");
                        return;
                    }
                }

                // Juveniles cannot breed until they have been raised to adulthood.
                foreach (var (dev, owner) in new[] { (device1, player1), (device2, partner) })
                {
                    if (!dev.IsJuvenile)
                        continue;
                    var other = owner == player1 ? partner : player1;
                    DebugGate("juvenile", $"{dev.Name} ({owner.Name}'s) is juvenile: {dev.MaturityStageName}, {dev.MaturityKills}/{MaturityKillsRequired} kills.");
                    owner.SendTransientError($"{dev.Name} is still a {dev.MaturityStageName.ToLowerInvariant()} and cannot breed until it is an adult ({dev.MaturityKills}/{MaturityKillsRequired} kills).");
                    other.SendTransientError($"Breeding cancelled: {owner.Name}'s pet is not an adult yet.");
                    return;
                }

                // Breeding requires exactly one male and one female.
                var isMale1 = device1.IsMale;
                var isMale2 = device2.IsMale;

                if (isMale1 == isMale2)
                {
                    var sex = isMale1 ? "males" : "females";
                    var msgSex = $"Breeding cancelled: two {sex} cannot breed. You need one male and one female.";
                    DebugGate("sex", $"both devices are {sex} ({device1.Name} and {device2.Name}). Use @setsex on an appraised device.");
                    player1.SendTransientError(msgSex);
                    partner.SendTransientError(msgSex);
                    return;
                }

                // The male sires (spends a breeding charge); the female takes the recovery cooldown.
                PetDevice maleDevice = isMale1 ? device1 : device2;
                PetDevice femaleDevice = isMale1 ? device2 : device1;
                var donorDevices = new System.Collections.Generic.List<PetDevice> { femaleDevice };

                var nowUnix = Time.GetUnixTime();

                // Check male breeding charges
                var maleMaxCharges = (int)ServerConfig.pet_breeding_male_max_charges.Value;
                var maleCharges = maleDevice?.GetAvailableMaleCharges() ?? maleMaxCharges;
                if (maleDevice != null && maleCharges <= 0 && !ServerConfig.pet_breeding_bypass_male_charges.Value)
                {
                    var restHours = ServerConfig.pet_breeding_male_charge_reset_hours.Value;
                    var maleOwner = maleDevice == device1 ? player1 : partner;
                    DebugGate("maleCharges", $"{maleDevice.Name} ({maleOwner.Name}'s) has 0 of {maleMaxCharges} breeding charges left; refill {restHours:0.#}h after the last one. @pet-reset-cooldown clears it.");
                    maleOwner.SendTransientError($"{maleDevice.Name} has exhausted its {maleMaxCharges} daily breeding charges. Rest for {restHours:0.#}h.");
                    return;
                }

                // Check female recovery cooldown
                foreach (var donorDevice in donorDevices)
                {
                    var nextDonor = donorDevice.GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                    if (nowUnix < nextDonor && !ServerConfig.pet_breeding_bypass_female_cooldown.Value)
                    {
                        var remaining = TimeSpan.FromSeconds(nextDonor - nowUnix);
                        DebugGate("femaleCooldown", $"{donorDevice.Name} is on the female recovery cooldown for another {remaining.Hours}h {remaining.Minutes}m (nextBreed {nextDonor:0} > now {nowUnix:0}). @pet-reset-cooldown clears it.");
                        player1.SendTransientError($"{donorDevice.Name} is still recovering from her last litter. Ready in {remaining.Hours}h {remaining.Minutes}m.");
                        partner.SendTransientError("Breeding cancelled: the female is still recovering from her last litter.");
                        return;
                    }
                }

                // The baby always goes to the female's owner. A coin flip meant the player who ate the
                // recovery cooldown could walk away with nothing, which reads as being robbed.
                var winner = femaleDevice == device1 ? player1 : partner;
                var loser = winner == player1 ? partner : player1;

                // The winner must be able to receive the baby in their MAIN pack right now, before any
                // charge, cooldown or consumable is spent. Anything that slips past this (pack filled
                // during a guardian fight, owner logged out) is handled by the deferred delivery in
                // CompleteBirth, which never puts the baby on the ground.
                var babyBurden = Math.Max(device1.EncumbranceVal ?? 0, device2.EncumbranceVal ?? 0);
                if (winner.GetFreeInventorySlots(false) <= 0)
                {
                    DebugGate("packSlot", $"{winner.Name} (the female's owner) has no free main-pack slot for the baby.");
                    winner.SendTransientError($"Breeding cancelled: your main pack has no free slot for the baby. Free a slot and dance again.");
                    loser.SendTransientError($"Breeding cancelled: {winner.Name}'s main pack has no free slot for the baby.");
                    return;
                }
                if (!winner.HasEnoughBurdenToAddToInventory(babyBurden))
                {
                    DebugGate("burden", $"{winner.Name} (the female's owner) cannot carry another {babyBurden} burden.");
                    winner.SendTransientError($"Breeding cancelled: you are too encumbered to carry the baby. Lighten your load and dance again.");
                    loser.SendTransientError($"Breeding cancelled: {winner.Name} is too encumbered to carry the baby.");
                    return;
                }

                // Every gate has passed - the breed will now go through.
                if (forced)
                {
                    var forcedMsg = "[Breeding] Breed was forced by an admin - dance ritual not active.";
                    player1.SendMessage(forcedMsg);
                    partner.SendMessage(forcedMsg);
                }

                // Litters bred: counted for both owners now that the breed is certain, on the characters
                // rather than the essences, so it survives the pets being traded away or destroyed. An
                // admin-forced breed is a test tool and is not counted.
                if (!forced)
                {
                    CountLitterBred(player1);
                    CountLitterBred(partner);
                }

                // Breeding resolves in this same tick, so the particles play once on the birth path below
                // rather than twice in the same instant.
                var ritualMsg = $"The mating ritual has begun between {pet1.Name} and {pet2.Name}...";
                player1.SendMessage(ritualMsg);
                partner.SendMessage(ritualMsg);

                // Everything about the offspring's stats is decided by BreedingMath.Simulate, the same
                // pure function the parity tests and @breed-replay run. This method only gathers its
                // inputs from the devices and config, and applies the side effects afterwards.
                var config = BreedingMath.BreedingConfig.FromServerConfig();
                var genetics1 = device1.ReadBreedingGenetics(config);
                var genetics2 = device2.ReadBreedingGenetics(config);

                // Courtship Incense bonus on each device (Simulate sums and clamps them to +50%). It
                // affects this roll, so it is consumed on every successful breed.
                var incense1 = device1.GetProperty(PropertyFloat.PetIncenseBonus) ?? 0.0;
                var incense2 = device2.GetProperty(PropertyFloat.PetIncenseBonus) ?? 0.0;
                device1.RemoveProperty(PropertyFloat.PetIncenseBonus);
                device2.RemoveProperty(PropertyFloat.PetIncenseBonus);

                // Chromatic Catalyst: read now, consumed only if a mutation palette is actually rolled.
                var catalyst1 = device1.GetProperty(PropertyBool.PetChromaticCatalystActive) ?? false;
                var catalyst2 = device2.GetProperty(PropertyBool.PetChromaticCatalystActive) ?? false;
                var chromaticCatalystActive = catalyst1 || catalyst2;

                // Offering of Subjugation: read now, consumed only when a mating guardian actually spawns
                // (TrySpawnMatingGuardian).
                var guardianWeakened = (device1.GetProperty(PropertyBool.PetGuardianWeakened) ?? false) ||
                                       (device2.GetProperty(PropertyBool.PetGuardianWeakened) ?? false);

                var inputs = new BreedingMath.BreedingInputs
                {
                    ParentA = genetics1,
                    ParentB = genetics2,
                    Config = config,
                    Options = new BreedingMath.BreedingOptions
                    {
                        IncenseA = incense1, IncenseB = incense2,
                        CatalystA = catalyst1, CatalystB = catalyst2,
                        // The guardian's fate is not known yet. OnGuardianSlain rolls the Awakened
                        // Blessing (draw 12) later through BreedingMath.RollAwakenedBlessing, the very
                        // helper Simulate uses when GuardianKilled is true.
                        GuardianKilled = false,
                    },
                };

                // Draws 1-11: inheritance per line, stat mutation roll, stat line pick, potency roll.
                var outcome = BreedingMath.Simulate(inputs, static () => ThreadSafeRandom.Next(0.0f, 1.0f));
                var baby = outcome.Baby;

                var mutationSummary = new System.Collections.Generic.List<string>();
                if (outcome.PotencyApplied)
                    mutationSummary.Add($"+{outcome.PotencyStep} Potency");
                if (outcome.StatLine != BreedingMath.MutationLine.None)
                    mutationSummary.Add($"+{outcome.StatStep} {BreedingMath.LineName(outcome.StatLine)}");

                // [PetTrace] the decision, line by line: inheritance per line (draws 1-8), the stat and
                // potency rolls (draws 9-11) with their arithmetic, and the REPLAY blob the parity
                // harness runs. These lines used to sit under pet_breeding_verbose_logging; they live
                // here now so the same fact is never logged twice.
                if (trace)
                {
                    PetTrace.BreedInherit(session, inputs, outcome);
                    PetTrace.BreedRoll(session, inputs, outcome);
                    PetTrace.BreedReplay(session, "decision", BreedingReplay.ToJson(inputs, outcome.RngDraws, baby));
                }

                uint? babyPaletteBase = null;

                // Update charges & cooldowns - for everyone, admins included. This used to be skipped for
                // admins, which meant charges never decremented and the recovery cooldown was never
                // written on admin characters, so neither rule ever appeared to work in testing.
                var chargesAfter = -1;
                if (!ServerConfig.pet_breeding_bypass_male_charges.Value && maleDevice != null)
                {
                    var chargesLeft = Math.Max(0, maleCharges - 1);
                    maleDevice.SetProperty(PropertyInt.PetMaleBreedingCharges, chargesLeft);
                    chargesAfter = chargesLeft;

                    var studOwner = maleDevice == device1 ? player1 : partner;
                    studOwner.SendMessage($"[Breeding] {maleDevice.Name} spent a breeding charge: {chargesLeft}/{maleMaxCharges} left today.");
                    // breed.commit carries this fact when the trace is on.
                    if (!trace)
                        log.Info($"[PetBreeding] Stud {maleDevice.Name} (0x{maleDevice.Guid.Full:X8}, {studOwner.Name}) charges {maleCharges} -> {chargesLeft}.");
                }

                var nextBreedWritten = 0.0;
                if (!ServerConfig.pet_breeding_bypass_female_cooldown.Value)
                {
                    var donorCooldown = ServerConfig.pet_breeding_cooldown_hours.Value * 3600.0;
                    nextBreedWritten = nowUnix + donorCooldown;
                    foreach (var donorDevice in donorDevices)
                        donorDevice.SetProperty(PropertyFloat.PetNextBreedingTime, nextBreedWritten);
                }

                // Roll 50/50 for species donor parent
                var donorRoll = ThreadSafeRandom.Next(0, 1);
                var donor = donorRoll == 0 ? device1 : device2;
                var babyWcid = donor.WeenieClassId;

                var paletteIndex = -1; var paletteCount = 0;
                if (mutationSummary.Count > 0)
                {
                    // Fully random draw from the same master DAT pool the 3D showroom offers, so a
                    // previewed colour is always one breeding can actually roll. Deliberately unfiltered
                    // and not species-gated: the whole point is that the result is a lottery.
                    try
                    {
                        var pool = chromaticCatalystActive
                            ? ACE.Server.Services.PetMutationService.GetVibrantPalettePool()
                            : ACE.Server.Services.PetMutationService.GetMasterPalettePool();
                        if (pool != null && pool.Count > 0)
                        {
                            paletteCount = pool.Count;
                            paletteIndex = ThreadSafeRandom.Next(0, pool.Count - 1);
                            babyPaletteBase = pool[paletteIndex].PaletteId;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"[PetBreeding] Failed to roll a mutation palette: {ex.Message}");
                    }
                }

                // The Chromatic Catalyst only does anything when a palette is actually rolled.
                if (babyPaletteBase.HasValue)
                {
                    device1.RemoveProperty(PropertyBool.PetChromaticCatalystActive);
                    device2.RemoveProperty(PropertyBool.PetChromaticCatalystActive);
                }

                foreach (var parentDevice in new[] { device1, device2 })
                {
                    parentDevice.ChangesDetected = true;
                    parentDevice.SaveBiotaToDatabase();
                }

                if (trace)
                {
                    PetTrace.Begin("breed.commit", session)
                        .Add("male", maleDevice?.Name ?? "none").AddGuid("maleGuid", maleDevice?.Guid.Full ?? 0)
                        .Add("female", femaleDevice.Name).AddGuid("femaleGuid", femaleDevice.Guid.Full)
                        .Add("winner", winner.Name).AddGuid("winnerGuid", winner.Guid.Full)
                        .Add("maleChargesBefore", maleCharges).Add("maleChargesAfter", chargesAfter).Add("bypassMaleCharges", ServerConfig.pet_breeding_bypass_male_charges.Value)
                        .Add("now", nowUnix).Add("femaleNextBreed", nextBreedWritten).Add("bypassFemaleCooldown", ServerConfig.pet_breeding_bypass_female_cooldown.Value)
                        .Add("incenseRemovedA", incense1).Add("incenseRemovedB", incense2)
                        .Add("donorRoll", donorRoll).Add("donor", donor == device1 ? "A" : "B").Add("donorName", donor.Name).Add("babyWcid", babyWcid)
                        .Add("mutated", mutationSummary.Count > 0).Add("mutations", string.Join(" and ", mutationSummary))
                        .Add("catalystA", catalyst1).Add("catalystB", catalyst2).Add("palettePool", chromaticCatalystActive ? "vibrant" : "master")
                        .Add("paletteCount", paletteCount).Add("paletteIndex", paletteIndex).AddGuid("palette", babyPaletteBase ?? 0)
                        .Add("catalystConsumed", babyPaletteBase.HasValue && chromaticCatalystActive)
                        .Add("guardianWeakened", guardianWeakened).Add("guardianSpawns", outcome.GuardianSpawned)
                        .Add("lastMutatedStat", (int)outcome.LastMutatedStat)
                        .AddGenetics("baby.", baby)
                        .Emit();
                }

                var pending = new PendingBreed
                {
                    Player1 = player1, Partner = partner, Winner = winner,
                    Donor = donor, Device1 = device1, Device2 = device2, Pet1 = pet1, Pet2 = pet2,
                    BabyWcid = babyWcid, BabyPaletteBase = babyPaletteBase, MutationSummary = mutationSummary,
                    Baby = baby, Config = config, Inputs = inputs, RngDraws = outcome.RngDraws,
                    GuardianWeakened = guardianWeakened, LastMutatedStat = (int)outcome.LastMutatedStat,
                    TraceSession = session,
                };

                // Mutation breeds can be gated behind a mating guardian: a monster wearing the
                // offspring's exact look that the two parent pets must kill together. If the guardian
                // cannot be spawned for any reason, the birth completes immediately instead - the
                // parents have already paid, so the breed must never be lost.
                // outcome.GuardianSpawned == GuardianEnabled && the breed mutated (== mutationSummary.Count > 0).
                if (outcome.GuardianSpawned)
                {
                    if (TrySpawnMatingGuardian(pending))
                        return;
                }

                CompleteBirth(pending);
            }
            finally
            {
                player1.IsBusy = false;
                partner.IsBusy = false;
            }
        }

        /// <summary>
        /// Spawns the mating guardian for a decided mutation breed and defers the birth to its death
        /// (or to the timeout). Returns false if anything prevents the spawn so the caller can fall
        /// back to an immediate birth.
        /// </summary>
        private static bool TrySpawnMatingGuardian(PendingBreed p)
        {
            MatingGuardian guardian = null;
            try
            {
                var pet1 = p.Pet1; var pet2 = p.Pet2;
                if (pet1 == null || pet2 == null || pet1.Location == null || pet1.IsDestroyed || pet2.IsDestroyed)
                {
                    log.Warn("[PetBreeding] Guardian skipped: a parent pet has no location or is gone. Completing birth immediately.");
                    if (PetTrace.Enabled) PetTrace.Begin("guardian.skipped", p.TraceSession).Add("reason", "parent pet has no location or is gone").Emit();
                    return false;
                }

                var templateWcid = (uint)ServerConfig.pet_breeding_guardian_template_wcid.Value;
                var weenie = DatabaseManager.World.GetCachedWeenie(templateWcid);
                if (weenie == null)
                {
                    log.Warn($"[PetBreeding] Guardian skipped: template weenie {templateWcid} not found (pet_breeding_guardian_template_wcid). Completing birth immediately.");
                    if (PetTrace.Enabled) PetTrace.Begin("guardian.skipped", p.TraceSession).Add("reason", "template weenie not found").Add("template", templateWcid).Emit();
                    return false;
                }

                guardian = new MatingGuardian(weenie, GuidManager.NewDynamicGuid());
                guardian.TraceSession = p.TraceSession;

                // Look: exactly what the baby will look like. Same dressing path as a summon, then the
                // same base/template rule the baby uses (native base, mutation in the template), and
                // the captured palette rows cleared so CalculateObjDesc reaches the template branch.
                p.Donor.ApplyVisualOverridesTo(guardian);
                if (p.BabyPaletteBase.HasValue && p.BabyPaletteBase.Value != 0)
                {
                    var nativeBase = Creature.GetSetupDefaultPaletteId(p.Donor.VisualOverrideSetup ?? 0);
                    if (nativeBase != 0)
                        guardian.PaletteBaseId = nativeBase;
                    guardian.PaletteTemplate = (int)p.BabyPaletteBase.Value;
                    guardian.Biota.PropertiesPalette?.Clear();
                }
                guardian.Name = $"Spirit of {guardian.Name}";
                var translucency = (float)Math.Clamp(ServerConfig.pet_breeding_guardian_translucency.Value, 0.0, 0.95);
                if (translucency > 0.001f)
                    guardian.Translucency = translucency;

                // Stats: the offspring's EFFECTIVE ratings (gear + mutations, as a summon would evaluate
                // them) and health from both parents. Outgoing damage is overridden in DamageEvent
                // (8% of the defending pet's max health, clamped, x pet_breeding_guardian_damage_mult).
                var hpMult = Math.Max(0.01, ServerConfig.pet_breeding_guardian_health_mult.Value);

                var effective = p.EffectiveRatings;
                guardian.Level = Math.Max(pet1.Level ?? 1, pet2.Level ?? 1);
                guardian.SetProperty(PropertyInt.DamageRating, effective.DamageRating);
                guardian.SetProperty(PropertyInt.DamageResistRating, effective.DamageResistRating);
                guardian.SetProperty(PropertyInt.CritRating, effective.CritRating);
                guardian.SetProperty(PropertyInt.CritDamageRating, effective.CritDamageRating);
                guardian.SetProperty(PropertyInt.CritResistRating, effective.CritResistRating);
                guardian.SetProperty(PropertyInt.CritDamageResistRating, effective.CritDamageResistRating);

                var combinedHealth = (double)pet1.Health.MaxValue + pet2.Health.MaxValue;
                guardian.Health.StartingValue = (uint)Math.Max(1, Math.Round(combinedHealth * hpMult));
                guardian.Health.Current = guardian.Health.MaxValue;

                // Level alone does not make a template dangerous: it keeps the combat skills of whatever
                // weenie it was cloned from. Template 7 attacks at skill 58, so against pets defending at
                // 700+ every swing was evaded and the ritual could not scratch them. Match the better
                // parent's melee defence so the guardian lands roughly half its attacks, and rise with the
                // pets instead of needing a config knob.
                var petDefense = Math.Max(
                    pet1.GetCreatureSkill(Skill.MeleeDefense).Current,
                    pet2.GetCreatureSkill(Skill.MeleeDefense).Current);
                var guardianAttack = (ushort)Math.Clamp(petDefense, 0u, ushort.MaxValue);
                foreach (var atkSkill in new[] { Skill.UnarmedCombat, Skill.LightWeapons, Skill.HeavyWeapons, Skill.FinesseWeapons })
                    guardian.GetCreatureSkill(atkSkill).InitLevel = guardianAttack;

                guardian.NeutraliseTemplate();

                var spawnPos = FindGuardianSpawnPosition(pet1, pet2, guardian);
                guardian.Location = spawnPos;
                guardian.Home = new Position(spawnPos);

                guardian.Bind(pet1, pet2, p.Player1, p.Partner, OnGuardianSlain, OnGuardianLost, p.GuardianWeakened);

                if (!guardian.EnterWorld())
                {
                    log.Warn("[PetBreeding] Guardian skipped: EnterWorld failed. Completing birth immediately.");
                    if (PetTrace.Enabled) PetTrace.Begin("guardian.skipped", p.TraceSession).Add("reason", "EnterWorld failed").Emit();
                    guardian.Unbind();
                    return false;
                }

                // Physics placement may have nudged it; home must be where it actually stands or the
                // monster loop keeps walking it "home" every idle cycle.
                guardian.Home = new Position(guardian.Location);

                guardian.WakeUp(false);
                guardian.PlayParticleEffect(PlayScript.EnchantUpBlue, guardian.Guid);

                var timeout = Math.Max(5.0, ServerConfig.pet_breeding_guardian_timeout_seconds.Value);
                var stirMsg = $"The union stirs something... {guardian.Name} rises before {pet1.Name} and {pet2.Name}! " +
                              $"Only the two parents can harm it. They have {timeout:0}s to bring it down together.";
                p.Player1.SendMessage(stirMsg);
                p.Partner.SendMessage(stirMsg);

                if (PetTrace.Enabled)
                {
                    var dmgMult = ServerConfig.pet_breeding_guardian_damage_mult.Value;
                    PetTrace.Begin("guardian.spawn", p.TraceSession)
                        .AddCreature("g.", guardian).Add("template", templateWcid)
                        .Add("pet1", pet1.Name).Add("pet1Level", pet1.Level ?? 1).Add("pet1MaxHp", pet1.Health.MaxValue)
                        .Add("pet2", pet2.Name).Add("pet2Level", pet2.Level ?? 1).Add("pet2MaxHp", pet2.Health.MaxValue)
                        .Add("levelRule", "level=max(pet1Level,pet2Level)").Add("level", guardian.Level ?? 0)
                        .Add("hpRule", "maxHp=max(1,round((pet1MaxHp+pet2MaxHp)*healthMult))").Add("healthMult", hpMult).Add("maxHp", guardian.Health.MaxValue)
                        .Add("ratingsRule", "the baby's summon ratings: gear+count*step and the derived crit lines")
                        .Add("dmgRating", effective.DamageRating).Add("drRating", effective.DamageResistRating).Add("critRating", effective.CritRating)
                        .Add("critDmgRating", effective.CritDamageRating).Add("critResRating", effective.CritResistRating).Add("critDmgResRating", effective.CritDamageResistRating)
                        .Add("outgoingRule", "per hit base=clamp(defendingPetMaxHp*0.08,20,500)*damageMult*(weakened?0.5:1)")
                        .Add("damageMult", dmgMult).Add("weakened", p.GuardianWeakened)
                        .Add("incomingRule", "per hit: nRaw=maxHp/raw; mod=clamp((nRaw/30)^0.81,0.01,1); x2.5 weakened; cap maxHp*(weakened?0.25:0.10)")
                        .AddGuid("palette", p.BabyPaletteBase ?? 0).Add("translucency", translucency)
                        .AddGuid("cell", guardian.Location?.Cell ?? 0).Add("timeout", timeout)
                        .Emit();
                }
                else
                {
                    log.Info($"[PetBreeding] Mating guardian {guardian.Name} (0x{guardian.Guid.Full:X8}) spawned for {p.Player1.Name} + {p.Partner.Name}: " +
                             $"template={templateWcid}, level={guardian.Level}, hp={guardian.Health.MaxValue}, dmgRating={effective.DamageRating}, " +
                             $"drRating={effective.DamageResistRating}, weakened={p.GuardianWeakened}, palette=0x{(p.BabyPaletteBase ?? 0):X8}, timeout={timeout:0}s");
                }

                // On the world queue, not the guardian: an action queued on a creature is silently
                // dropped once that creature has no landblock, which is exactly the case we must handle.
                var timeoutChain = new ACE.Server.Entity.Actions.ActionChain();
                timeoutChain.AddDelaySeconds(timeout);
                timeoutChain.AddAction(WorldManager.ActionQueue, ACE.Server.Entity.Actions.ActionType.PetDevice_GuardianTimeout, () => OnGuardianTimeout(guardian));
                timeoutChain.EnqueueChain();

                // Register LAST. Anything above that throws must leave no registry entry behind, or
                // the catch below falls back to an immediate birth AND the guardian's death would
                // deliver a second baby.
                p.GuardianGuid = guardian.Guid.Full;
                pendingGuardianBreeds[guardian.Guid.Full] = p;

                // The Offering of Subjugation has now had its effect: consume it from both parents.
                if (p.GuardianWeakened)
                {
                    foreach (var dev in new[] { p.Device1, p.Device2 })
                    {
                        if (dev == null || dev.GetProperty(PropertyBool.PetGuardianWeakened) != true)
                            continue;
                        dev.RemoveProperty(PropertyBool.PetGuardianWeakened);
                        dev.ChangesDetected = true;
                        dev.SaveBiotaToDatabase();
                        if (PetTrace.Enabled)
                            PetTrace.Begin("consumable.consumed", p.TraceSession).Add("item", "Offering of Subjugation").Add("property", "PetGuardianWeakened")
                                .AddGuid("device", dev.Guid.Full).Add("deviceName", dev.Name).Add("consumedBy", "guardian spawn").Emit();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[PetBreeding] Guardian spawn threw; completing birth immediately. {ex}");
                if (PetTrace.Enabled) PetTrace.Begin("guardian.skipped", p.TraceSession).Add("reason", "spawn threw").Add("error", ex.Message).Emit();
                if (guardian != null)
                {
                    // Make sure the fallback birth is the only birth: drop the registry entry (if we got
                    // that far) and remove the guardian without running its lost/slain callbacks.
                    pendingGuardianBreeds.TryRemove(guardian.Guid.Full, out _);
                    p.GuardianGuid = 0;
                    try
                    {
                        if (guardian.CurrentLandblock != null)
                            guardian.Fade();
                        else
                            guardian.Unbind();
                    }
                    catch (Exception cleanupEx)
                    {
                        log.Error($"[PetBreeding] Guardian cleanup after failed spawn threw: {cleanupEx}");
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// The creature name a baby inherits with its look. Prefers the weenie name behind
        /// <see cref="PropertyInt.CapturedCreatureWCID"/>, which survives a rename, over the stored
        /// <see cref="PetDevice.VisualOverrideName"/>, which a rename overwrites with the owner's chosen name.
        /// Falls back to the stored name for legacy essences that predate the captured WCID.
        /// </summary>
        private static string ResolveInheritedCreatureName(PetDevice donor)
        {
            var creatureWcid = donor.GetProperty(PropertyInt.CapturedCreatureWCID);
            if (creatureWcid.HasValue && creatureWcid.Value > 0)
            {
                var weenieName = DatabaseManager.World.GetCachedWeenie((uint)creatureWcid.Value)?.GetProperty(PropertyString.Name);
                if (!string.IsNullOrWhiteSpace(weenieName))
                    return weenieName;
            }

            return donor.VisualOverrideName;
        }

        /// <summary>
        /// One more committed breed for this owner (PropertyInt.PetLittersBred, the "Litters bred"
        /// leaderboard). Kept on the character, not the essence, so it survives the pets being traded
        /// away or destroyed. Never throws into the breeding path.
        /// </summary>
        private static void CountLitterBred(Player owner)
        {
            if (owner == null)
                return;

            try
            {
                var count = owner.GetProperty(PropertyInt.PetLittersBred) ?? 0;
                owner.SetProperty(PropertyInt.PetLittersBred, count + 1);
                owner.ChangesDetected = true;
            }
            catch (Exception ex)
            {
                log.Error($"[PetBreeding] Could not count the litter for {owner.Name}: {ex}");
            }
        }

        /// <summary>
        /// Physics radius of a creature from its live physics object, falling back to its setup's
        /// bounding sphere. Creatures do not push each other apart at placement, so spawn spacing
        /// has to be computed by hand.
        /// </summary>
        private static float GetCreatureRadius(Creature c)
        {
            try
            {
                if (c.PhysicsObj != null)
                {
                    var r = c.PhysicsObj.GetPhysicsRadius();
                    if (r > 0.01f) return r;
                }
            }
            catch { }

            try
            {
                var setup = DatManager.PortalDat.ReadFromDat<ACE.DatLoader.FileTypes.SetupModel>(c.SetupTableId);
                if (setup?.Spheres != null && setup.Spheres.Count > 0)
                    return setup.Spheres[0].Radius * (c.ObjScale ?? 1.0f);
            }
            catch { }

            return 1.0f;
        }

        /// <summary>
        /// Picks a spawn spot for the guardian that does not overlap either parent pet: in front of
        /// pet 1, then behind it, then in front of / behind pet 2, at radius + radius + a gap. The
        /// first candidate clear of both pets wins; if the room is that cramped, the last candidate
        /// is used and physics placement slides it off any wall on entry.
        /// </summary>
        private static Position FindGuardianSpawnPosition(CombatPet pet1, CombatPet pet2, Creature guardian)
        {
            var guardianRadius = GetCreatureRadius(guardian);
            var r1 = GetCreatureRadius(pet1);
            var r2 = GetCreatureRadius(pet2);
            const float gap = 1.5f;

            var candidates = new System.Collections.Generic.List<Position>
            {
                pet1.Location.InFrontOf(r1 + guardianRadius + gap, false),
                pet1.Location.InFrontOf(r1 + guardianRadius + gap, true),
            };
            if (pet2.Location != null)
            {
                candidates.Add(pet2.Location.InFrontOf(r2 + guardianRadius + gap, false));
                candidates.Add(pet2.Location.InFrontOf(r2 + guardianRadius + gap, true));
            }

            Position chosen = null;
            foreach (var c in candidates)
            {
                c.LandblockId = new LandblockId(c.GetCell());
                var clear1 = c.Distance2D(pet1.Location) >= r1 + guardianRadius + 0.5f;
                var clear2 = pet2.Location == null || c.Distance2D(pet2.Location) >= r2 + guardianRadius + 0.5f;
                if (clear1 && clear2)
                {
                    chosen = c;
                    break;
                }
            }

            return chosen ?? candidates[candidates.Count - 1];
        }

        /// <summary>
        /// The parent pets killed the guardian: the birth completes with a bonus Awakened Blessing
        /// mutation on top of whatever the breed had already rolled.
        /// </summary>
        private static void OnGuardianSlain(MatingGuardian guardian)
        {
            if (!pendingGuardianBreeds.TryRemove(guardian.Guid.Full, out var p))
                return;

            var yieldMsg = $"{guardian.Name} yields to its parents and dissolves into light...";
            p.Player1.SendMessage(yieldMsg);
            p.Partner.SendMessage(yieldMsg);

            // Awakened Blessing (draw 12): the same caps and steps as the original breed roll, through
            // the same BreedingMath helper Simulate uses. Base combat ratings remain clean because their
            // mutation counts are evaluated dynamically when summoned; potency is the one stat whose
            // effective value is stored directly on the device.
            // [PetTrace] the eligible lines the blessing chose from, evaluated on the baby BEFORE the
            // roll (the same lists RollAwakenedBlessing builds), so the pick index can be checked.
            var traceEligible = "";
            var tracePotStep = 0;
            if (PetTrace.Enabled)
            {
                var eligible = BreedingMath.EligibleStatLines(p.Baby, p.Config.MaxStatMutations);
                tracePotStep = BreedingMath.PotencyMutationStep(p.Config.PotencyMutationStep, p.Baby.PotencyStored, p.Config.PotencySoftCap, p.Config.ResolvedPotencyHardCap);
                if (tracePotStep > 0)
                    eligible.Add(BreedingMath.MutationLine.Potency);
                traceEligible = string.Join(",", eligible);
            }

            var blessing = BreedingMath.RollAwakenedBlessing(ref p.Baby, p.Config, static () => ThreadSafeRandom.Next(0.0f, 1.0f));
            if (blessing.Drew)
                p.RngDraws?.Add(blessing.Roll);

            if (PetTrace.Enabled)
            {
                var r = PetTrace.Begin("guardian.slain", p.TraceSession).AddCreature("g.", guardian);
                guardian.AddFightSummary(r, "fight.");
                r.Add("blessingRule", "eligible=stat lines under maxStatMutations then potency when its capped step>0; index=min(n-1,floor(roll*n)); draw 12")
                 .Add("eligible", traceEligible).Add("eligibleCount", traceEligible.Length == 0 ? 0 : traceEligible.Split(',').Length)
                 .Add("potencyStep", tracePotStep).Add("drew", blessing.Drew).Add("roll", blessing.Drew ? blessing.Roll : double.NaN)
                 .Add("line", blessing.Line).Add("lineName", BreedingMath.LineName(blessing.Line)).Add("step", blessing.Step).Add("allLinesCapped", blessing.AllLinesCapped)
                 .AddGenetics("baby.", p.Baby)
                 .Emit();
            }

            if (blessing.Line == BreedingMath.MutationLine.None)
            {
                var cappedMsg = "[Breeding] The Awakened Blessing flares, but every mutation line has reached its limit.";
                p.Player1.SendMessage(cappedMsg);
                p.Partner.SendMessage(cappedMsg);
                CompleteBirth(p);
                return;
            }

            var statName = BreedingMath.LineName(blessing.Line);
            var mutationStep = blessing.Step;
            p.LastMutatedStat = (int)blessing.Line;
            p.MutationSummary.Add($"Awakened Blessing: +{mutationStep} {statName}");

            // The blessing REPLAY blob (draw 12 appended, guardianKilled:true) moved from the verbose
            // switch into the trace with the rest of the decision lines.
            if (PetTrace.Enabled && p.RngDraws != null)
            {
                var killedInputs = p.Inputs;
                killedInputs.Options.GuardianKilled = true;
                PetTrace.BreedReplay(p.TraceSession, "blessing", BreedingReplay.ToJson(killedInputs, p.RngDraws, p.Baby));
            }

            p.Player1.PlayParticleEffect(PlayScript.LevelUp, p.Player1.Guid);
            p.Partner.PlayParticleEffect(PlayScript.LevelUp, p.Partner.Guid);

            var blessingMsg = $"[Breeding] The Awakened Blessing stirs within the newborn: bonus {statName} mutation!";
            p.Player1.SendMessage(blessingMsg);
            p.Partner.SendMessage(blessingMsg);

            CompleteBirth(p);
        }

        /// <summary>
        /// The guardian was lost (a parent pet died, or guardian was removed without dying). The
        /// parents already paid for this breed, so the birth completes anyway: under no
        /// circumstances is the baby lost or the breed wasted.
        /// </summary>
        private static void OnGuardianLost(MatingGuardian guardian)
        {
            if (!pendingGuardianBreeds.TryRemove(guardian.Guid.Full, out var p))
                return;

            var lostMsg = "[Breeding] The spectral guardian dissolves back into the ether...";
            p.Player1.SendMessage(lostMsg);
            p.Partner.SendMessage(lostMsg);
            if (PetTrace.Enabled)
            {
                var r = PetTrace.Begin("guardian.lost", p.TraceSession).AddCreature("g.", guardian).Add("reason", guardian.LostReason ?? "unknown");
                guardian.AddFightSummary(r, "fight.");
                r.Add("blessing", false).Emit();
            }
            else
                log.Info($"[PetBreeding] Mating guardian {guardian.Name} (0x{guardian.Guid.Full:X8}) lost; completing birth for {p.Player1.Name} and {p.Partner.Name} regardless.");

            CompleteBirth(p);
        }

        /// <summary>
        /// The guardian outlived its window without being slain: it fades peacefully and the birth
        /// still completes, since the parents already paid for the breed.
        /// </summary>
        private static void OnGuardianTimeout(MatingGuardian guardian)
        {
            if (guardian.IsResolved)
                return;

            if (!pendingGuardianBreeds.TryRemove(guardian.Guid.Full, out var p))
            {
                guardian.Fade();
                return;
            }

            var fightSeconds = ACE.Server.Entity.Timers.RunningTime - guardian.SpawnTime;
            if (PetTrace.Enabled)
            {
                var r = PetTrace.Begin("guardian.timeout", p.TraceSession).AddCreature("g.", guardian)
                    .Add("timeout", Math.Max(5.0, ServerConfig.pet_breeding_guardian_timeout_seconds.Value));
                guardian.AddFightSummary(r, "fight.");
                r.Add("blessing", false).Emit();
            }
            else
                log.Info($"[PetBreeding] Mating guardian {guardian.Name} (0x{guardian.Guid.Full:X8}) timed out after {fightSeconds:F0}s with {guardian.Health.Current}/{guardian.Health.MaxValue} health left; completing birth regardless.");

            guardian.Fade();

            var fadeMsg = "[Breeding] The spectral guardian dissolves back into the ether...";
            p.Player1.SendMessage(fadeMsg);
            p.Partner.SendMessage(fadeMsg);

            CompleteBirth(p);
        }

        /// <summary>
        /// Creates the baby device from a decided breed, writes its inheritance and mutation, places it
        /// with the winner, announces it, and dismisses the parents. Pure function of the record:
        /// safe to call immediately or later.
        /// </summary>
        private static void CompleteBirth(PendingBreed p)
        {
            // A birth can be deferred (guardian fight), and Player.Session is never nulled on logout, so
            // the Player objects captured at breed time may be stale. Resolve the live objects now.
            var player1 = ResolveLivePlayer(p.Player1); var partner = ResolveLivePlayer(p.Partner);
            var winner = p.Winner;
            var donor = p.Donor; var pet1 = p.Pet1; var pet2 = p.Pet2;
            var babyWcid = p.BabyWcid; var babyPaletteBase = p.BabyPaletteBase; var mutationSummary = p.MutationSummary;
            var genetics = p.Baby;
            var babyPotency = genetics.PotencyStored;
            var babyDmgMuts = genetics.Dmg; var babyDrMuts = genetics.Dr; var babyCritMuts = genetics.Crit;
            var babyVitMuts = genetics.Vit; var babyPotMuts = genetics.Pot;

            var baby = WorldObjectFactory.CreateNewWorldObject(babyWcid) as PetDevice;
            if (baby == null)
            {
                player1.SendTransientError("Failed to spawn the baby pet device.");
                partner.SendTransientError("Failed to spawn the baby pet device.");
                return;
            }

            // baby.Name is still the template weenie's name here (e.g. "Electrified Moar Essence (250)").
            // It is kept as the starting point so the " Essence (tier)" suffix is always correct, and the
            // creature word is composed in below once the inherited look is known.
            var templateName = baby.Name;

            // Copy visual overrides
            baby.VisualOverrideSetup = donor.VisualOverrideSetup;
            baby.VisualOverrideMotionTable = donor.VisualOverrideMotionTable;
            baby.VisualOverrideCombatTable = donor.VisualOverrideCombatTable;
            baby.VisualOverrideSoundTable = donor.VisualOverrideSoundTable;
            baby.VisualOverridePaletteBase = donor.VisualOverridePaletteBase;
            baby.VisualOverrideClothingBase = donor.VisualOverrideClothingBase;
            baby.VisualOverrideScale = donor.VisualOverrideScale;
            // The look's REAL creature, not whatever the parent happens to be called. A rename overwrites
            // CapturedCreatureName with the owner's chosen name but leaves CapturedCreatureWCID intact, so a
            // baby of "Bob's Banana" that looks like a white rabbit is born a White Rabbit, matching its icon.
            baby.VisualOverrideName = ResolveInheritedCreatureName(donor);
            // The shiny variant is never inherited, even if a shiny somehow reached this point.
            baby.VisualOverrideCreatureVariant = donor.IsShiny && !ServerConfig.pet_breeding_allow_shiny.Value
                ? null
                : donor.VisualOverrideCreatureVariant;
            baby.VisualOverrideCreatureType = donor.VisualOverrideCreatureType;
            baby.VisualOverrideShade = donor.VisualOverrideShade;
            baby.VisualOverridePaletteTemplate = donor.VisualOverridePaletteTemplate;
            baby.VisualOverrideCapturedItems = donor.VisualOverrideCapturedItems;

            // The capture identity travels with the look, exactly as the tailoring kit already carries it
            // (PetTailoring.CopyVisuals). Without CapturedSourceDamageType the baby falls back to its template's
            // damage word and silently changes element - a Slash parent producing an Electric baby.
            // PetCustomName is deliberately NOT copied: the baby is a new pet and takes its creature's name.
            foreach (var capProp in new[] { PropertyInt.CapturedSourceDamageType, PropertyInt.CapturedCreatureWCID })
            {
                var capVal = donor.GetProperty(capProp);
                if (capVal.HasValue)
                    baby.SetProperty(capProp, capVal.Value);
            }

            // Compose the name from the look the baby actually inherited, then front it with the element it
            // will really attack with. Both are known now, so the baby is born correctly named rather than
            // waiting for its first summon to repair it.
            var composed = BuildDisplayNameAfterCaptureApply(templateName, null, baby.VisualOverrideName);
            var babyDamageType = TryResolveCapturedSourceDamageTypeForCombatPet(baby);
            if (babyDamageType.HasValue)
                composed = ApplyEssenceDamageLabel(composed, babyDamageType.Value);

            baby.Name = composed;

            // The Use line comes from the template weenie and still names the template creature
            // ("...summon or dismiss your Lightning Skeleton Samurai."). Capture and tailoring both re-sync it;
            // breeding did not, which is why bred pets described a creature they did not look like.
            MonsterCapture.SyncPetDeviceUseStringAfterSkinRename(baby, templateName, baby.Name);

            // The ObjDesc recipe (part meshes, subpalette ranges, texture swaps) is what makes the
            // baby look like its parent. Without it a bred pet falls back to the bare weenie.
            foreach (var objDescProp in new[] { PropertyString.CapturedObjDescAnimParts, PropertyString.CapturedObjDescPalettes, PropertyString.CapturedObjDescTextures })
            {
                var val = donor.GetProperty(objDescProp);
                if (!string.IsNullOrEmpty(val))
                    baby.SetProperty(objDescProp, val);
            }

            if (donor.GetProperty(PropertyDataId.Icon) is uint icon && icon != 0)
                baby.SetProperty(PropertyDataId.Icon, icon);
            if (donor.GetProperty(PropertyDataId.IconOverlay) is uint iconOverlay && iconOverlay != 0)
                baby.SetProperty(PropertyDataId.IconOverlay, iconOverlay);

            // Write baby ratings & properties
            baby.PetBondAttuned = false;
            baby.PetBondAttunedCharacterId = 0;
            baby.PetBondLevel = 1;
            baby.PetPotencyStored = babyPotency;

            // Inherited gear base ratings: these are what CombatPet reads at summon and what the ID
            // panel shows as "Base". Only written when present, exactly like loot generation.
            // The creature-side DamageRating/CritRating/Vitality properties are NOT written to the
            // device: summon never reads them, and a stray Vitality value would be mistaken for
            // phantom vitality mutations by the legacy fallback.
            if (genetics.GearDamage > 0) baby.GearDamage = genetics.GearDamage;
            if (genetics.GearDamageResist > 0) baby.GearDamageResist = genetics.GearDamageResist;
            if (genetics.GearCrit > 0) baby.GearCrit = genetics.GearCrit;
            if (genetics.GearCritDamage > 0) baby.GearCritDamage = genetics.GearCritDamage;
            if (genetics.GearCritResist > 0) baby.GearCritResist = genetics.GearCritResist;
            if (genetics.GearCritDamageResist > 0) baby.GearCritDamageResist = genetics.GearCritDamageResist;

            // Persistent genetic mutation counts. Always written (including 0) on a bred baby so the
            // legacy PetMut*Rating / Vitality fallbacks can never trigger on it. The counts are
            // evaluated against the live *_mutation_step config at summon time.
            baby.SetProperty(PropertyInt.PetMutDamageCount, babyDmgMuts);
            baby.SetProperty(PropertyInt.PetMutDamageResistCount, babyDrMuts);
            baby.SetProperty(PropertyInt.PetMutCritCount, babyCritMuts);
            baby.SetProperty(PropertyInt.PetMutVitalityCount, babyVitMuts);
            baby.SetProperty(PropertyInt.PetMutPotencyCount, babyPotMuts);

            var totalMutations = babyDmgMuts + babyDrMuts + babyCritMuts + babyVitMuts + babyPotMuts;
            if (totalMutations > 0)
                baby.SetProperty(PropertyInt.PetMutationCount, totalMutations);

            if (p.LastMutatedStat > 0)
                baby.SetProperty(PropertyInt.PetLastMutatedStat, p.LastMutatedStat);

            // Creature recolour goes through PaletteTemplate, not PaletteBase. Creature.CalculateObjDesc
            // reads the creature's PaletteTemplate (line ~252) and expands a full 0x04 palette DID into
            // two subpalette ranges (0..255 and 255..1) covering all 2048 slots.
            //
            // CRITICAL: that branch is unreachable when the pet's biota carries any palette/animpart/
            // texture rows, because CalculateObjDesc returns early (line ~141). ApplyCapturedObjDesc
            // populates those rows from CapturedObjDescPalettes, so the captured string must be cleared
            // or the mutation colour is silently discarded.
            // The mutation goes in the TEMPLATE (overlay). The BASE must stay a palette the model
            // legitimately renders with - its native DefaultPaletteId, or the one it was captured
            // with. Every in-game case where the mutation was written into the base rendered
            // nothing; every case with a real base rendered. This is exactly what @create does.
            // Prefer the native base so a donor carrying a stale mutation base can't poison the
            // lineage; fall back to the inherited captured base for models with no native one.
            if (babyPaletteBase.HasValue && babyPaletteBase.Value != 0)
            {
                var nativeBase = Creature.GetSetupDefaultPaletteId(baby.VisualOverrideSetup ?? 0);
                if (nativeBase != 0)
                    baby.VisualOverridePaletteBase = nativeBase;

                baby.VisualOverridePaletteTemplate = (int)babyPaletteBase.Value;
                baby.RemoveProperty(PropertyString.CapturedObjDescPalettes);

                // The birth record carries the palette keys when the trace is on.
                if (!PetTrace.Enabled)
                    log.Info($"[PetBreeding] Mutation palette 0x{babyPaletteBase.Value:X8} applied to {baby.Name}: " +
                             $"VisualOverridePaletteTemplate={(int)babyPaletteBase.Value}, CapturedObjDescPalettes cleared " +
                             $"(otherwise CalculateObjDesc early-returns and the colour never reaches the client).");
            }

            var mutationSuffix = mutationSummary.Count > 0
                ? $" [GENETIC MUTATION] Gained {string.Join(" and ", mutationSummary)}" +
                  (babyPaletteBase.HasValue ? " & Rare DAT Palette unlocked!" : "!")
                : "";

            var successMsg = $"Congratulations! A baby pet has been born: {baby.Name}! Placed in {winner.Name}'s inventory.";
            successMsg += mutationSuffix;

            // Bred babies start life as juveniles: small, weak, and unable to breed until raised.
            // Set before delivery so the client's first look at the item already carries the flag.
            baby.MarkBornJuvenile();

            // Player.Session is never nulled on logout, so the captured Player object cannot tell us
            // whether the winner is still online. Ask the PlayerManager for the live object instead.
            var liveWinner = PlayerManager.GetOnlinePlayer(winner.Guid.Full);
            var winnerOnline = liveWinner != null && !liveWinner.IsLoggingOut && !liveWinner.IsDestroyed;

            var delivered = winnerOnline && liveWinner.TryCreateInInventoryWithNetworking(baby);

            if (PetTrace.Enabled)
            {
                var stored = baby.ReadBreedingGenetics(p.Config);
                var r = PetTrace.Begin("birth", p.TraceSession)
                    .AddGuid("baby", baby.Guid.Full).Add("babyName", baby.Name).Add("babyWcid", babyWcid)
                    .Add("donor", donor == p.Device1 ? "A" : "B").AddGuid("donorGuid", donor.Guid.Full)
                    .Add("winner", winner.Name).AddGuid("winnerGuid", winner.Guid.Full)
                    .Add("mutations", string.Join(" and ", mutationSummary)).Add("lastMutatedStat", p.LastMutatedStat)
                    .Add("bond", baby.PetBondLevel ?? 0).Add("bondAttuned", baby.IsPetBondAttuned)
                    .Add("juvenile", baby.IsJuvenile).Add("kills", baby.MaturityKills).Add("stage", baby.MaturityStage)
                    .Add("mutTotalProp", baby.GetProperty(PropertyInt.PetMutationCount) ?? 0)
                    .AddGenetics("decided.", genetics).AddGenetics("stored.", stored)
                    .Add("storedMatchesDecided", stored.SameAs(genetics))
                    .AddSummonMath("summon.", stored, p.Config)
                    .AddGuid("palette", babyPaletteBase ?? 0).AddGuid("paletteBase", baby.VisualOverridePaletteBase ?? 0)
                    .Add("paletteTemplate", baby.VisualOverridePaletteTemplate ?? 0)
                    .Add("capturedPalettesCleared", babyPaletteBase.HasValue && babyPaletteBase.Value != 0)
                    .AddGuid("setup", baby.VisualOverrideSetup ?? 0).Add("variant", baby.VisualOverrideCreatureVariant ?? 0)
                    .Add("winnerOnline", winnerOnline)
                    .Add("delivery", delivered ? "inventory" : "deferred")
                    .Add("deliveryReason", delivered ? "TryCreateInInventoryWithNetworking succeeded" : winnerOnline ? "packs full, written to persisted inventory for next login" : "winner offline, written to persisted inventory for next login")
                    .Add("dismissParents", ServerConfig.pet_breeding_dismiss_after_breed.Value);
                r.Emit();
            }

            if (delivered)
            {
                player1.SendMessage(successMsg);
                partner.SendMessage(successMsg);
                player1.PlayParticleEffect(PlayScript.VisionUpWhite, player1.Guid);
                partner.PlayParticleEffect(PlayScript.VisionUpWhite, partner.Guid);
                pet1.PlayParticleEffect(PlayScript.WeddingBliss, pet1.Guid);
                pet2.PlayParticleEffect(PlayScript.WeddingBliss, pet2.Guid);
            }
            else
            {
                // Residual case (pre-breed gate already required a free main-pack slot): the winner
                // logged out or filled every pack during the guardian fight. The baby is never dropped
                // on the ground, where it would rot in 5 minutes or be taken by anyone. It is written
                // straight into the winner's persisted inventory (ContainerId = winner), exactly the
                // way the login inventory load (GetInventoryInParallel by Container IID) expects, so it
                // is in their pack the next time they log in. No attunement: newborns stay tradeable
                // until first summoned, like any other baby.
                var deferredMsg = winnerOnline
                    ? $"A baby pet has been born: {baby.Name}! {winner.Name}'s packs are full, so the baby has been tucked away and will be in {winner.Name}'s pack at their next login."
                    : $"A baby pet has been born: {baby.Name}! {winner.Name} is not online, so the baby has been tucked away and will be in their pack at their next login.";
                deferredMsg += mutationSuffix;

                player1.SendMessage(deferredMsg);
                partner.SendMessage(deferredMsg);

                baby.Location = null;
                baby.Placement = ACE.Entity.Enum.Placement.Resting;
                baby.OwnerId = winner.Guid.Full;
                baby.ContainerId = winner.Guid.Full;
                baby.PlacementPosition = 0;

                if (!PetTrace.Enabled)
                    log.Info($"[PetBreeding] Baby {baby.Name} (0x{baby.Guid.Full:X8}) for {winner.Name} (0x{winner.Guid.Full:X8}) delivered to persisted inventory " +
                             $"(winnerOnline={winnerOnline}); it will load into their pack at next login.");
            }

            baby.SaveBiotaToDatabase();

            // Breeding consumes the summon: dismiss both parents after a short delay so the birth
            // particles play out first. Re-breeding then requires a re-summon and its use cooldown,
            // which is the natural pacing between rituals. Destroy() clears the owner's
            // CurrentActivePet itself, and is guarded so a pet already gone is a no-op.
            if (ServerConfig.pet_breeding_dismiss_after_breed.Value)
            {
                // Queued on the world queue so a parent that died in the guardian fight cannot swallow
                // the action, and dismissed directly: this is a system dismissal, not an owner stow, so
                // the post-combat recall block does not apply.
                var dismissChain = new ACE.Server.Entity.Actions.ActionChain();
                dismissChain.AddDelaySeconds(2.0);
                dismissChain.AddAction(WorldManager.ActionQueue, ACE.Server.Entity.Actions.ActionType.PetDevice_DismissAfterBreed, () =>
                {
                    if (!pet1.IsDestroyed)
                        pet1.Destroy();
                    if (!pet2.IsDestroyed)
                        pet2.Destroy();
                });
                dismissChain.EnqueueChain();
            }
        }

        public string BuildBreedingAppraisalBlock()
        {
            if (!IsCombatPetDevice())
                return null;

            if (!ServerConfig.pet_breeding_enabled.Value)
                return null;

            var sb = new System.Text.StringBuilder();

            // One line: sex plus what it means for breeding right now. A male is always the stud and
            // a female always the dam, so a separate "Role" line only repeated the sex.
            var isMale = IsMale;
            if (GetProperty(PropertyBool.PetNeutered) == true)
            {
                sb.AppendLine($"Sex: {SexName} (neutered - cannot breed)");
            }
            else if (IsShiny && !ServerConfig.pet_breeding_allow_shiny.Value)
            {
                sb.AppendLine($"Sex: {SexName} (shiny - cannot breed)");
            }
            else if (IsJuvenile)
            {
                sb.AppendLine($"Sex: {SexName} ({MaturityStageName.ToLowerInvariant()} - cannot breed yet)");
            }
            else if (isMale)
            {
                var maleMax = (int)ServerConfig.pet_breeding_male_max_charges.Value;
                var maleAvailable = GetAvailableMaleCharges();
                var sexLine = $"Sex: Male ({maleAvailable}/{maleMax} breeding charges today)";

                var maleResetHours = ServerConfig.pet_breeding_male_charge_reset_hours.Value;
                if (maleAvailable < maleMax && maleResetHours > 0)
                {
                    var refillAt = (GetProperty(PropertyFloat.PetMaleChargesRefreshTime) ?? 0.0) + maleResetHours * 3600.0;
                    var untilRefill = refillAt - Time.GetUnixTime();
                    if (untilRefill > 0)
                    {
                        var ts = TimeSpan.FromSeconds(untilRefill);
                        sexLine += $" - refills in {(int)ts.TotalHours}h {ts.Minutes}m";
                    }
                }

                sb.AppendLine(sexLine);
            }
            else
            {
                var now = Time.GetUnixTime();
                var nextBreeding = GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                if (now < nextBreeding)
                {
                    var remaining = TimeSpan.FromSeconds(nextBreeding - now);
                    sb.AppendLine($"Sex: Female (recovering - ready to breed in {remaining.Hours}h {remaining.Minutes}m)");
                }
                else
                    sb.AppendLine("Sex: Female (ready to breed)");
            }

            var growthLine = BuildMaturityAppraisalLine();
            if (growthLine != null)
                sb.AppendLine(growthLine);

            var imprintLine = BuildImprintAppraisalLine();
            if (imprintLine != null)
                sb.AppendLine(imprintLine);

            // Say it up front rather than letting a mutation or a serum quietly do nothing. Only claim
            // the colour is fixed when the captured textures actually cover the body: a few
            // replacements still leave the rest of the parts tinting.
            var colourVis = ACE.Server.Services.PetMutationService.GetColourChangeVisibility(this);
            if (colourVis.Coverage == ACE.Server.Services.PetMutationService.ColourCoverage.Hidden)
                sb.AppendLine($"Colour: fixed by this pet's captured textures on {colourVis.TexturedParts} of {colourVis.TotalParts} parts (mutations and serums cannot change it)");
            else if (colourVis.Coverage == ACE.Server.Services.PetMutationService.ColourCoverage.FixedColour)
                sb.AppendLine("Colour: fixed - most of this creature is drawn with full-colour textures, so mutations and serums cannot visibly change it");
            else if (colourVis.Coverage == ACE.Server.Services.PetMutationService.ColourCoverage.Unknown)
                sb.AppendLine($"Colour: {colourVis.TextureCount} captured textures may cover part of a new colour");

            // Translucency: the tincture-set level, else the summon template's (Maiden and K'nath are 50%).
            var translucency = GetProperty(PropertyFloat.PetTranslucency);
            if (!translucency.HasValue && PetClass.HasValue)
            {
                var summonWeenie = DatabaseManager.World.GetCachedWeenie((uint)PetClass.Value);
                translucency = summonWeenie != null ? ACE.Entity.Models.WeenieExtensions.GetProperty(summonWeenie, PropertyFloat.Translucency) : null;
            }
            if (translucency.HasValue && translucency.Value >= 0.001)
                sb.AppendLine($"Translucency: {translucency.Value * 100:0}%");

            var dmgStep = (int)ServerConfig.pet_breeding_damage_mutation_step.Value;
            var drStep = (int)ServerConfig.pet_breeding_dr_mutation_step.Value;
            var critStep = (int)ServerConfig.pet_breeding_crit_mutation_step.Value;
            var vitStep = (int)ServerConfig.pet_breeding_vitality_mutation_step.Value;
            var potStep = (int)ServerConfig.pet_breeding_potency_mutation_step.Value;

            var dmgMuts = GetProperty(PropertyInt.PetMutDamageCount) ?? ((GetProperty(PropertyInt.PetMutDamageRating) ?? 0) / Math.Max(1, dmgStep));
            var drMuts = GetProperty(PropertyInt.PetMutDamageResistCount) ?? ((GetProperty(PropertyInt.PetMutDamageResistRating) ?? 0) / Math.Max(1, drStep));
            var critMuts = GetProperty(PropertyInt.PetMutCritCount) ?? ((GetProperty(PropertyInt.PetMutCritRating) ?? 0) / Math.Max(1, critStep));
            var vitMuts = GetProperty(PropertyInt.PetMutVitalityCount) ?? ((GetProperty(PropertyInt.PetMutVitality) ?? 0) / Math.Max(1, vitStep));
            var potMuts = GetProperty(PropertyInt.PetMutPotencyCount) ?? ((GetProperty(PropertyInt.PetMutPotency) ?? 0) / Math.Max(1, potStep));

            var statMuts = dmgMuts + drMuts + critMuts + vitMuts;
            var totalMutations = statMuts + potMuts;

            var maxStatMuts = (int)ServerConfig.pet_breeding_max_stat_mutations.Value;
            sb.AppendLine($"Total Mutations: {totalMutations}");

            var dynDmgBonus = dmgMuts * dmgStep;
            var dynDrBonus = drMuts * drStep;
            var dynCritBonus = critMuts * critStep;
            var dynVitBonus = vitMuts * vitStep;
            var dynPotBonus = potMuts * potStep;

            if (totalMutations > 0)
            {
                sb.AppendLine("--- Genetic Mutations ---");
                var capSuffix = maxStatMuts > 0 ? $"/{maxStatMuts}" : "";
                if (dmgMuts > 0) sb.AppendLine($"* Damage:        +{dynDmgBonus} [{dmgMuts}{capSuffix} Muts]");
                if (drMuts > 0) sb.AppendLine($"* Damage Resist: +{dynDrBonus} [{drMuts}{capSuffix} Muts]");
                if (critMuts > 0) sb.AppendLine($"* Crit Rating:   +{dynCritBonus} [{critMuts}{capSuffix} Muts]");
                if (vitMuts > 0) sb.AppendLine($"* Vitality:      +{dynVitBonus} HP [{vitMuts}{capSuffix} Muts]");
                if (potMuts > 0) sb.AppendLine($"* Potency:       +{dynPotBonus} [{potMuts} Muts]");
            }

            var baseDmg = GearDamage ?? 0;
            var baseDr = GearDamageResist ?? 0;
            var baseCrit = GearCrit ?? 0;

            var totalDmg = baseDmg + dynDmgBonus;
            var totalDr = baseDr + dynDrBonus;
            var totalCrit = baseCrit + dynCritBonus;

            if (totalDmg > 0 || totalDr > 0 || totalCrit > 0)
            {
                sb.AppendLine("--- Combat Ratings ---");
                sb.AppendLine($"* Damage: {totalDmg} (Base: {baseDmg}, Mut: +{dynDmgBonus})");
                sb.AppendLine($"* Damage Resist: {totalDr} (Base: {baseDr}, Mut: +{dynDrBonus})");
                sb.AppendLine($"* Crit Rating: {totalCrit} (Base: {baseCrit}, Mut: +{dynCritBonus})");
            }

            // StringBuilder.AppendLine emits "\r\n" on Windows and the AC client draws the bare CR as a
            // music-note glyph. Every string bound for the client must use "\n" only.
            return sb.ToString().Replace("\r\n", "\n").TrimEnd();
        }

        /// <summary>
        /// Stud breeding charges available right now. Charges are refilled lazily: the first read
        /// after pet_breeding_male_charge_reset_hours has elapsed since the last refill tops the device
        /// back up to pet_breeding_male_max_charges. A reset period of 0 disables regeneration.
        /// </summary>
        public int GetAvailableMaleCharges(bool persist = true)
        {
            var maxCharges = Math.Max(0, (int)ServerConfig.pet_breeding_male_max_charges.Value);
            var charges = Math.Min(GetProperty(PropertyInt.PetMaleBreedingCharges) ?? maxCharges, maxCharges);

            var resetHours = ServerConfig.pet_breeding_male_charge_reset_hours.Value;
            if (resetHours <= 0)
                return charges;

            var now = Time.GetUnixTime();
            var lastRefill = GetProperty(PropertyFloat.PetMaleChargesRefreshTime) ?? 0.0;

            // Never stamped (pre-existing device): start the rest window now rather than granting a
            // free instant refill.
            if (lastRefill <= 0.0)
            {
                if (persist)
                {
                    SetProperty(PropertyFloat.PetMaleChargesRefreshTime, now);
                    ChangesDetected = true;
                }
                return charges;
            }

            if (now < lastRefill + resetHours * 3600.0)
                return charges;

            if (persist)
            {
                SetProperty(PropertyInt.PetMaleBreedingCharges, maxCharges);
                SetProperty(PropertyFloat.PetMaleChargesRefreshTime, now);
                ChangesDetected = true;
                SaveBiotaToDatabase();
            }

            return maxCharges;
        }

        public static void RunBreedingDiagnostics(Player player)
        {
            if (player == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== PET BREEDING DIAGNOSTICS ===");
            sb.AppendLine($"Breeding System Enabled: {ServerConfig.pet_breeding_enabled.Value}");

            var allowedLb = (uint)ServerConfig.pet_breeding_allowed_landblock.Value;
            var allowedVar = (int)ServerConfig.pet_breeding_allowed_variant.Value;
            var curLb = player.Location.Landblock;
            var curCell = player.Location.LandblockId.Raw;
            var curVar = player.CurrentLandblock?.VariationId ?? -1;
            bool locMatch = MatchesBreedingArea(curCell, curVar, allowedLb, -1);
            bool varMatch = (allowedVar == -1) || (curVar == allowedVar);
            var allowedKind = allowedLb == 0 ? "anywhere" : (allowedLb > 0xFFFF ? "exact cell" : "whole landblock");

            sb.AppendLine($"Your Location: Landblock=0x{curLb:X4}, Cell=0x{curCell:X8}, Variant={curVar}");
            sb.AppendLine($"Allowed Target: {allowedKind} 0x{allowedLb:X}, Variant={allowedVar} => Location Match: {(locMatch && varMatch ? "VALID (Room OK)" : "INVALID (Wrong Location)")}");

            if (player.CurrentActivePet is CombatPet myPet)
            {
                var myDevice = myPet.TryGetSummoningDevice() ?? player.FindObject(myPet.SummoningDeviceGuid.Full, Player.SearchLocations.Everywhere) as PetDevice;
                var isMale = myDevice?.IsMale ?? false;
                var maleMaxCharges = (int)ServerConfig.pet_breeding_male_max_charges.Value;
                var maleCharges = myDevice?.GetAvailableMaleCharges() ?? maleMaxCharges;
                var nextBreed = myDevice?.GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                var onCooldown = Time.GetUnixTime() < nextBreed;
                sb.AppendLine($"Your Active Pet: {myPet.Name} (WCID {myPet.WeenieClassId}) | Device: {(myDevice != null ? myDevice.Name : "Not Found")}");
                sb.AppendLine($"  Sex: {(myDevice != null ? myDevice.SexName : "?")} {(isMale ? $"({maleCharges}/{maleMaxCharges} charges)" : $"({(onCooldown ? "recovering" : "ready to breed")})")}");
            }
            else
            {
                sb.AppendLine("Your Active Pet: NONE SUMMONED! (Please summon your combat pet)");
            }

            var online = PlayerManager.GetAllOnline();
            int candidateCount = 0;

            if (player.IsAdmin)
                sb.AppendLine($"--- Other Online Players ({online.Count} total) ---");

            foreach (var p in online)
            {
                if (p.Guid == player.Guid) continue;
                var dist = player.GetDistance(p);
                var sameLb = p.Location.Landblock == curLb;
                var otherPet = p.CurrentActivePet as CombatPet;
                var hasPet = otherPet != null;

                if (player.IsAdmin)
                    sb.AppendLine($"* {p.Name}: Dist={dist:F1}m, LB=0x{p.Location.Landblock:X4}, Pet={(hasPet ? otherPet.Name : "None")}, Trade={p.IsTrading}");

                if (hasPet && (dist <= 30.0f || sameLb)) candidateCount++;
            }

            if (player.IsAdmin)
                sb.AppendLine($"Valid Partner Candidates within 30m: {candidateCount}");
            else
            {
                sb.AppendLine($"Eligible breeding partners nearby: {candidateCount}");
                if (candidateCount == 0)
                    sb.AppendLine("  (Ensure your partner has their pet summoned in this room and is ready to breed)");
            }

            sb.AppendLine("================================");

            player.SendMessage(sb.ToString().Replace("\r\n", "\n"));
        }
    }
}
