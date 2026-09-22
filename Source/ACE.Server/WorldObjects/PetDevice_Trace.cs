using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using ACE.Common;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// The pet session trace: one greppable, single-line, 7-bit ASCII record per event, written to
    /// the log4net logger named "PetTrace" at INFO and switched by ServerConfig.pet_trace.
    ///
    ///   [PetTrace] &lt;event&gt; | session=&lt;id&gt; | t=&lt;utc iso8601&gt; | k=v | k=v | ...
    ///
    /// Every record carries a session id (minted once per breed attempt and reused by every record
    /// of that attempt, guardian records included; a fresh id for standalone events) and a UTC
    /// timestamp. Values never contain '|', tabs or line breaks, and any non-ASCII character is
    /// replaced, so a pasted log can be split on " | " and each pair on the first '='.
    ///
    /// Cost rule: every call site checks <see cref="Enabled"/> BEFORE building a record, so the
    /// trace costs one bool read when it is off. Nothing in here changes game behaviour or draws
    /// from the RNG; it only reads state the calling thread already owns.
    ///
    /// This file holds the record format and the pet / breeding helpers; the combat helpers are in
    /// PetDevice_Trace_Combat.cs.
    /// </summary>
    public static partial class PetTrace
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("PetTrace");

        public const string Prefix = "[PetTrace]";
        public const string Separator = " | ";

        /// <summary>One switch for the whole trace (breeding session and combat).</summary>
        public static bool Enabled => ServerConfig.pet_trace.Value;

        /// <summary>Short correlation id: 8 hex characters, minted per breed attempt or per standalone event.</summary>
        public static string NewSessionId() => System.Guid.NewGuid().ToString("N").Substring(0, 8);

        public static string Hex(uint value) => "0x" + value.ToString("X8", CultureInfo.InvariantCulture);

        public static string Timestamp() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------------------------------
        // Record format
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Strips a value down to printable 7-bit ASCII: '|' becomes '/', tabs and line breaks become
        /// spaces, anything else outside 0x20-0x7E becomes '?'. Keys are constants and never pass
        /// through here.
        /// </summary>
        public static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            StringBuilder sb = null;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                char r;
                if (c == '|') r = '/';
                else if (c == '\t' || c == '\r' || c == '\n') r = ' ';
                else if (c < 0x20 || c > 0x7E) r = '?';
                else r = c;

                if (r != c && sb == null)
                {
                    sb = new StringBuilder(value.Length);
                    sb.Append(value, 0, i);
                }
                sb?.Append(r);
            }
            return (sb?.ToString() ?? value).Trim();
        }

        /// <summary>Invariant-culture, round-trip formatting so a reviewer can recompute from the exact value.</summary>
        public static string Format(object value)
        {
            switch (value)
            {
                case null: return "null";
                case string s: return Sanitize(s);
                case bool b: return b ? "true" : "false";
                case float f: return f.ToString("R", CultureInfo.InvariantCulture);
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                case decimal m: return m.ToString(CultureInfo.InvariantCulture);
                case ACE.Entity.ObjectGuid g: return Hex(g.Full);
                case IFormattable fm: return Sanitize(fm.ToString(null, CultureInfo.InvariantCulture));
                default: return Sanitize(value.ToString());
            }
        }

        /// <summary>A record under construction. Build it with Add, finish with Emit (or ToString for tests).</summary>
        public sealed class Record
        {
            private readonly StringBuilder sb;

            internal Record(string evt, string session)
            {
                sb = new StringBuilder(512);
                sb.Append(Prefix).Append(' ').Append(Sanitize(evt));
                sb.Append(Separator).Append("session=").Append(Sanitize(string.IsNullOrEmpty(session) ? "none" : session));
                sb.Append(Separator).Append("t=").Append(Timestamp());
            }

            public Record Add(string key, string value) { sb.Append(Separator).Append(key).Append('=').Append(Sanitize(value ?? "null")); return this; }
            public Record Add(string key, bool value) { sb.Append(Separator).Append(key).Append('=').Append(value ? "true" : "false"); return this; }
            public Record Add(string key, int value) { sb.Append(Separator).Append(key).Append('=').Append(value.ToString(CultureInfo.InvariantCulture)); return this; }
            public Record Add(string key, uint value) { sb.Append(Separator).Append(key).Append('=').Append(value.ToString(CultureInfo.InvariantCulture)); return this; }
            public Record Add(string key, long value) { sb.Append(Separator).Append(key).Append('=').Append(value.ToString(CultureInfo.InvariantCulture)); return this; }
            public Record Add(string key, float value) { sb.Append(Separator).Append(key).Append('=').Append(value.ToString("R", CultureInfo.InvariantCulture)); return this; }
            public Record Add(string key, double value) { sb.Append(Separator).Append(key).Append('=').Append(value.ToString("R", CultureInfo.InvariantCulture)); return this; }
            public Record Add(string key, object value) { sb.Append(Separator).Append(key).Append('=').Append(Format(value)); return this; }
            public Record AddGuid(string key, uint guid) { sb.Append(Separator).Append(key).Append('=').Append(Hex(guid)); return this; }

            public override string ToString() => sb.ToString();

            /// <summary>Writes the record at INFO. The caller has already checked <see cref="Enabled"/>.</summary>
            public void Emit() => log.Info(sb.ToString());
        }

        public static Record Begin(string evt, string session) => new Record(evt, session);

        /// <summary>
        /// Splits a trace line back into its event name and key/value pairs (the inverse of Record).
        /// Returns false for a line that is not a [PetTrace] record.
        /// </summary>
        public static bool TryParse(string line, out string evt, out Dictionary<string, string> pairs)
        {
            evt = null;
            pairs = null;
            if (string.IsNullOrEmpty(line))
                return false;

            var start = line.IndexOf(Prefix, StringComparison.Ordinal);
            if (start < 0)
                return false;

            var parts = line.Substring(start + Prefix.Length).Split(new[] { Separator }, StringSplitOptions.None);
            evt = parts[0].Trim();
            pairs = new Dictionary<string, string>(parts.Length);
            for (var i = 1; i < parts.Length; i++)
            {
                var eq = parts[i].IndexOf('=');
                if (eq <= 0)
                    continue;
                pairs[parts[i].Substring(0, eq)] = parts[i].Substring(eq + 1);
            }
            return evt.Length > 0;
        }

        // ------------------------------------------------------------------------------------------
        // Shared blocks
        // ------------------------------------------------------------------------------------------

        /// <summary>Player / Monster / CombatPet / Pet / MatingGuardian / Object.</summary>
        public static string Kind(WorldObject wo)
        {
            switch (wo)
            {
                case null: return "null";
                case Player: return "Player";
                case MatingGuardian: return "MatingGuardian";
                case CombatPet: return "CombatPet";
                case Pet: return "Pet";
                case Creature: return "Monster";
                default: return "Object";
            }
        }

        public static Record AddCreature(this Record r, string p, Creature c)
        {
            if (c == null)
                return r.Add(p + "guid", "null");
            r.AddGuid(p + "guid", c.Guid.Full).Add(p + "name", c.Name).Add(p + "kind", Kind(c)).Add(p + "wcid", c.WeenieClassId).Add(p + "level", c.Level ?? 0);
            if (c is CombatPet cp && cp.P_PetOwner != null)
                r.Add(p + "owner", cp.P_PetOwner.Name);
            return r;
        }

        public static Record AddPlayer(this Record r, string p, Player pl)
        {
            if (pl == null)
                return r.Add(p + "guid", "null");
            return r.AddGuid(p + "guid", pl.Guid.Full).Add(p + "name", pl.Name).Add(p + "admin", pl.IsAdmin);
        }

        /// <summary>
        /// Everything a reviewer needs about one pet device: identity, tier, sex, breeding state,
        /// every gear rating, every mutation count (as ReadBreedingGenetics resolves them, legacy
        /// fallback included), potency, the consumable flags and the visual keys.
        /// </summary>
        public static Record AddDevice(this Record r, string p, PetDevice d, Player owner)
        {
            if (d == null)
                return r.Add(p + "guid", "null");

            var config = PetDevice.BreedingMath.BreedingConfig.FromServerConfig();
            var g = d.ReadBreedingGenetics(config);
            var tier = global::ACE.Server.Factories.Tables.Wcids.PetDeviceWcids.GetPetLevel(d.WeenieClassId);

            r.AddGuid(p + "guid", d.Guid.Full).Add(p + "wcid", d.WeenieClassId).Add(p + "name", d.Name);
            if (owner != null)
                r.Add(p + "owner", owner.Name).AddGuid(p + "ownerGuid", owner.Guid.Full);
            r.Add(p + "tier", tier.HasValue ? tier.Value.ToString(CultureInfo.InvariantCulture) : "none");
            r.Add(p + "sex", d.SexName);
            var sexOverride = d.GetProperty(PropertyBool.PetIsMaleOverride);
            r.Add(p + "sexOverride", sexOverride.HasValue ? (sexOverride.Value ? "male" : "female") : "derived");
            r.Add(p + "neutered", d.GetProperty(PropertyBool.PetNeutered) == true);
            r.Add(p + "shiny", d.IsShiny);
            r.Add(p + "bred", d.WasBredJuvenile);
            r.Add(p + "juvenile", d.IsJuvenile);
            r.Add(p + "stage", d.MaturityStage).Add(p + "stageName", d.MaturityStageName);
            r.Add(p + "kills", d.MaturityKills).Add(p + "killsRequired", PetDevice.MaturityKillsRequired);
            r.Add(p + "bond", d.PetBondLevel ?? 1).Add(p + "bondAttuned", d.IsPetBondAttuned).Add(p + "bondChar", d.PetBondAttunedCharacterId ?? 0);
            r.Add(p + "chargesStored", d.GetProperty(PropertyInt.PetMaleBreedingCharges)?.ToString(CultureInfo.InvariantCulture) ?? "unset");
            r.Add(p + "chargesAvail", d.GetAvailableMaleCharges(persist: false));
            r.Add(p + "chargesRefresh", d.GetProperty(PropertyFloat.PetMaleChargesRefreshTime) ?? 0.0);
            r.Add(p + "nextBreed", d.GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0);
            r.Add(p + "now", Time.GetUnixTime());
            r.Add(p + "gearDmg", g.GearDamage).Add(p + "gearDr", g.GearDamageResist).Add(p + "gearCrit", g.GearCrit);
            r.Add(p + "gearCritDmg", g.GearCritDamage).Add(p + "gearCritRes", g.GearCritResist).Add(p + "gearCritDmgRes", g.GearCritDamageResist);
            r.Add(p + "mutDmg", g.Dmg).Add(p + "mutDr", g.Dr).Add(p + "mutCrit", g.Crit).Add(p + "mutVit", g.Vit).Add(p + "mutPot", g.Pot);
            r.Add(p + "mutTotalProp", d.GetProperty(PropertyInt.PetMutationCount) ?? 0);
            r.Add(p + "lastMutated", d.GetProperty(PropertyInt.PetLastMutatedStat) ?? 0);
            r.Add(p + "potencyStored", g.PotencyStored).Add(p + "potencyActive", PetPotency.GetActivePotency(d));
            r.Add(p + "incense", d.GetProperty(PropertyFloat.PetIncenseBonus) ?? 0.0);
            r.Add(p + "catalyst", d.GetProperty(PropertyBool.PetChromaticCatalystActive) == true);
            r.Add(p + "weakened", d.GetProperty(PropertyBool.PetGuardianWeakened) == true);
            r.Add(p + "xpMult", d.GetProperty(PropertyFloat.PetMaturityXpMultiplier) ?? 1.0);
            r.AddGuid(p + "setup", d.VisualOverrideSetup ?? 0).AddGuid(p + "paletteBase", d.VisualOverridePaletteBase ?? 0);
            r.Add(p + "paletteTemplate", d.VisualOverridePaletteTemplate ?? 0);
            r.Add(p + "variant", d.VisualOverrideCreatureVariant ?? 0).Add(p + "creatureWcid", d.CaptureSkinCreatureWcid ?? 0);
            return r;
        }

        /// <summary>
        /// The summon-time arithmetic for a genetics block, line by line: gear + count x step, the three
        /// derived crit lines (0.8 / 0.8 / 0.6 of the mutation bonus, half-up), the vitality HP bonus,
        /// and the newborn (stage 1) multiplier with the live maturity config.
        /// </summary>
        public static Record AddSummonMath(this Record r, string p, in PetDevice.BreedingMath.BreedingGenetics g, in PetDevice.BreedingMath.BreedingConfig c)
        {
            var dmgBonus = g.Dmg * c.DamageMutationStep;
            var drBonus = g.Dr * c.DrMutationStep;
            var critBonus = g.Crit * c.CritMutationStep;
            var critDmgFromMut = PetDevice.BreedingMath.MutCritDamage(dmgBonus);
            var critResFromMut = PetDevice.BreedingMath.MutCritResist(drBonus);
            var critDmgResFromMut = PetDevice.BreedingMath.MutCritDamageResist(drBonus);
            var bonusHp = g.Vit * c.VitalityMutationStep;

            r.Add(p + "rule", "rating=gear+count*step; critDmg=gearCritDmg+roundHalfUp(0.8*dmgBonus); critRes=gearCritRes+roundHalfUp(0.8*drBonus); critDmgRes=gearCritDmgRes+roundHalfUp(0.6*drBonus); hp=+vit*vitStep; juvenile=roundHalfUp(value*stageMult)");
            r.Add(p + "dmgStep", c.DamageMutationStep).Add(p + "drStep", c.DrMutationStep).Add(p + "critStep", c.CritMutationStep).Add(p + "vitStep", c.VitalityMutationStep);
            r.Add(p + "dmgBonus", dmgBonus).Add(p + "dmg", g.GearDamage + dmgBonus);
            r.Add(p + "drBonus", drBonus).Add(p + "dr", g.GearDamageResist + drBonus);
            r.Add(p + "critBonus", critBonus).Add(p + "crit", g.GearCrit + critBonus);
            r.Add(p + "critDmgFromMut", critDmgFromMut).Add(p + "critDmg", g.GearCritDamage + critDmgFromMut);
            r.Add(p + "critResFromMut", critResFromMut).Add(p + "critRes", g.GearCritResist + critResFromMut);
            r.Add(p + "critDmgResFromMut", critDmgResFromMut).Add(p + "critDmgRes", g.GearCritDamageResist + critDmgResFromMut);
            r.Add(p + "bonusHp", bonusHp).Add(p + "potencyStored", g.PotencyStored);

            var stages = PetDevice.MaturityStages;
            var juvenileStrength = ServerConfig.pet_maturity_juvenile_strength.Value;
            var stage1 = PetDevice.BreedingMath.MaturityMultiplier(1, stages, juvenileStrength);
            r.Add(p + "maturityEnabled", ServerConfig.pet_maturity_enabled.Value).Add(p + "stages", stages).Add(p + "juvenileStrength", juvenileStrength).Add(p + "stage1Mult", stage1);
            r.Add(p + "stage1.dmg", PetDevice.BreedingMath.RoundHalfUp((g.GearDamage + dmgBonus) * stage1));
            r.Add(p + "stage1.dr", PetDevice.BreedingMath.RoundHalfUp((g.GearDamageResist + drBonus) * stage1));
            r.Add(p + "stage1.crit", PetDevice.BreedingMath.RoundHalfUp((g.GearCrit + critBonus) * stage1));
            r.Add(p + "stage1.critDmg", PetDevice.BreedingMath.RoundHalfUp((g.GearCritDamage + critDmgFromMut) * stage1));
            r.Add(p + "stage1.critRes", PetDevice.BreedingMath.RoundHalfUp((g.GearCritResist + critResFromMut) * stage1));
            r.Add(p + "stage1.critDmgRes", PetDevice.BreedingMath.RoundHalfUp((g.GearCritDamageResist + critDmgResFromMut) * stage1));
            r.Add(p + "stage1.bonusHp", PetDevice.BreedingMath.RoundHalfUp(bonusHp * stage1));
            return r;
        }

        public static Record AddGenetics(this Record r, string p, in PetDevice.BreedingMath.BreedingGenetics g)
        {
            return r.Add(p + "gearDmg", g.GearDamage).Add(p + "gearDr", g.GearDamageResist).Add(p + "gearCrit", g.GearCrit)
                    .Add(p + "gearCritDmg", g.GearCritDamage).Add(p + "gearCritRes", g.GearCritResist).Add(p + "gearCritDmgRes", g.GearCritDamageResist)
                    .Add(p + "mutDmg", g.Dmg).Add(p + "mutDr", g.Dr).Add(p + "mutCrit", g.Crit).Add(p + "mutVit", g.Vit).Add(p + "mutPot", g.Pot)
                    .Add(p + "potencyStored", g.PotencyStored);
        }

        // ------------------------------------------------------------------------------------------
        // device.dump (@pet-dump)
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Full state of one device plus the ID panel text it would show, for a before/after capture
        /// around a breed. Written regardless of the switch: the command is the request.
        /// </summary>
        public static string DeviceDump(PetDevice device, Player owner, string reason)
        {
            var session = NewSessionId();
            var r = Begin("device.dump", session).Add("reason", reason).AddDevice("d.", device, owner);

            var config = PetDevice.BreedingMath.BreedingConfig.FromServerConfig();
            var g = device.ReadBreedingGenetics(config);
            r.AddSummonMath("summon.", g, config);

            var breeding = device.BuildBreedingAppraisalBlock();
            var potency = PetPotency.BuildPotencyAppraisalBlock(device);
            r.Add("id.breeding", breeding == null ? "none" : breeding.Replace("\n", " / "));
            r.Add("id.potency", potency == null ? "none" : potency.Replace("\n", " / "));
            r.Emit();
            return session;
        }

        // ------------------------------------------------------------------------------------------
        // Breeding records
        // ------------------------------------------------------------------------------------------

        /// <summary>One gate refusal. The reason text is the same string the admin chat line shows.</summary>
        public static void BreedGate(string session, string gate, string reason, Player player1, Player partner)
        {
            var r = Begin("breed.gate", session).Add("gate", gate).Add("reason", reason);
            if (player1 != null) r.AddPlayer("p1.", player1);
            if (partner != null) r.AddPlayer("p2.", partner);
            r.Emit();
        }

        /// <summary>The effective breeding config: what BreedingConfig.FromServerConfig read, plus bond and area settings.</summary>
        public static void BreedConfig(string session, in PetDevice.BreedingMath.BreedingConfig c)
        {
            Begin("breed.config", session)
                .Add("baseMutationChance", c.BaseMutationChance).Add("potencyMutationChance", c.PotencyMutationChance)
                .Add("mutationDecayRate", c.MutationDecayRate).Add("mutationMinFloor", c.MutationMinFloor)
                .Add("damageMutationStep", c.DamageMutationStep).Add("drMutationStep", c.DrMutationStep).Add("critMutationStep", c.CritMutationStep)
                .Add("vitalityMutationStep", c.VitalityMutationStep).Add("potencyMutationStep", c.PotencyMutationStep)
                .Add("potencySoftCap", c.PotencySoftCap).Add("potencyHardCap", c.PotencyHardCap).Add("potencyMaxStored", c.PotencyMaxStored)
                .Add("resolvedHardCap", c.ResolvedPotencyHardCap).Add("maxStatMutations", c.MaxStatMutations)
                .Add("forceMutation", c.ForceMutation).Add("guardianEnabled", c.GuardianEnabled)
                .Add("higherParentChance", PetDevice.BreedingMath.HigherParentChance)
                .Add("pet_bond_enabled", ServerConfig.pet_bond_enabled.Value)
                .Add("pet_breeding_enabled", ServerConfig.pet_breeding_enabled.Value)
                .AddGuid("allowedLandblock", (uint)ServerConfig.pet_breeding_allowed_landblock.Value)
                .Add("allowedVariant", ServerConfig.pet_breeding_allowed_variant.Value)
                .Add("danceSyncSeconds", ServerConfig.pet_breeding_dance_sync_seconds.Value)
                .Add("minBond", ServerConfig.pet_breeding_min_bond.Value).Add("minParentLevel", ServerConfig.pet_breeding_min_parent_level.Value)
                .Add("maleMaxCharges", ServerConfig.pet_breeding_male_max_charges.Value).Add("maleResetHours", ServerConfig.pet_breeding_male_charge_reset_hours.Value)
                .Add("cooldownHours", ServerConfig.pet_breeding_cooldown_hours.Value)
                .Add("bypassMaleCharges", ServerConfig.pet_breeding_bypass_male_charges.Value).Add("bypassFemaleCooldown", ServerConfig.pet_breeding_bypass_female_cooldown.Value)
                .Add("dismissAfterBreed", ServerConfig.pet_breeding_dismiss_after_breed.Value).Add("allowShiny", ServerConfig.pet_breeding_allow_shiny.Value)
                .Add("maturityEnabled", ServerConfig.pet_maturity_enabled.Value).Add("imprintOnSummon", ServerConfig.pet_maturity_imprint_on_summon.Value)
                .Add("guardianTimeout", ServerConfig.pet_breeding_guardian_timeout_seconds.Value).Add("guardianTemplate", ServerConfig.pet_breeding_guardian_template_wcid.Value)
                .Add("guardianHealthMult", ServerConfig.pet_breeding_guardian_health_mult.Value).Add("guardianDamageMult", ServerConfig.pet_breeding_guardian_damage_mult.Value)
                .Add("guardianTranslucency", ServerConfig.pet_breeding_guardian_translucency.Value)
                .Emit();
        }

        /// <summary>
        /// One record per inherited line (draws 1-8), recomputed from the inputs and the draws Simulate
        /// consumed: both parents' effective values and their parts, which was higher, the roll, the
        /// 0.55 rule, which parent was taken, and what the baby carries.
        /// </summary>
        public static void BreedInherit(string session, in PetDevice.BreedingMath.BreedingInputs inputs, PetDevice.BreedingMath.BreedingOutcome o)
        {
            var a = inputs.ParentA; var b = inputs.ParentB; var c = inputs.Config;
            var draws = o.RngDraws;
            var baby = o.Baby;

            // The baby's inherited counts are its final counts minus the mutations Simulate added.
            var inheritedDmg = baby.Dmg - (o.StatLine == PetDevice.BreedingMath.MutationLine.Damage ? 1 : 0);
            var inheritedDr = baby.Dr - (o.StatLine == PetDevice.BreedingMath.MutationLine.DamageResist ? 1 : 0);
            var inheritedCrit = baby.Crit - (o.StatLine == PetDevice.BreedingMath.MutationLine.Crit ? 1 : 0);
            var inheritedVit = baby.Vit - (o.StatLine == PetDevice.BreedingMath.MutationLine.Vitality ? 1 : 0);
            var inheritedPot = baby.Pot - (o.PotencyApplied ? 1 : 0);
            var inheritedStored = baby.PotencyStored - (o.PotencyApplied ? o.PotencyStep : 0);

            void Line(int draw, string name, string mode, int gearA, int countA, int gearB, int countB, int step, int babyGear, int babyCount)
            {
                var effA = PetDevice.BreedingMath.Effective(gearA, countA, step);
                var effB = PetDevice.BreedingMath.Effective(gearB, countB, step);
                var roll = draws.Count >= draw ? draws[draw - 1] : double.NaN;
                var picksA = PetDevice.BreedingMath.PicksParent1(effA, effB, roll);
                Begin("breed.inherit", session)
                    .Add("line", name).Add("mode", mode).Add("draw", draw)
                    .Add("a.gear", gearA).Add("a.count", countA).Add("a.eff", effA)
                    .Add("b.gear", gearB).Add("b.count", countB).Add("b.eff", effB)
                    .Add("step", step).Add("higher", effA >= effB ? "A" : "B").Add("higherChance", PetDevice.BreedingMath.HigherParentChance)
                    .Add("roll", roll).Add("rule", "roll<higherChance takes the higher parent (tie=A) else the lower")
                    .Add("picked", picksA ? "A" : "B").Add("baby.gear", babyGear).Add("baby.count", babyCount)
                    .Emit();
            }

            Line(1, "damage", "gear+count", a.GearDamage, a.Dmg, b.GearDamage, b.Dmg, c.DamageMutationStep, baby.GearDamage, inheritedDmg);
            Line(2, "damageResist", "gear+count", a.GearDamageResist, a.Dr, b.GearDamageResist, b.Dr, c.DrMutationStep, baby.GearDamageResist, inheritedDr);
            Line(3, "crit", "gear+count", a.GearCrit, a.Crit, b.GearCrit, b.Crit, c.CritMutationStep, baby.GearCrit, inheritedCrit);
            Line(4, "critDamage", "gearOnly", a.GearCritDamage, 0, b.GearCritDamage, 0, 0, baby.GearCritDamage, 0);
            Line(5, "critResist", "gearOnly", a.GearCritResist, 0, b.GearCritResist, 0, 0, baby.GearCritResist, 0);
            Line(6, "critDamageResist", "gearOnly", a.GearCritDamageResist, 0, b.GearCritDamageResist, 0, 0, baby.GearCritDamageResist, 0);
            Line(7, "vitality", "countOnly", 0, a.Vit, 0, b.Vit, c.VitalityMutationStep, 0, inheritedVit);
            // Potency compares the stored value (missing = 0); stored and count travel together. "gear" here is the stored value.
            Line(8, "potency", "stored+count", a.PotencyStored, a.Pot, b.PotencyStored, b.Pot, 0, inheritedStored, inheritedPot);
        }

        /// <summary>The stat roll (draw 9), the line pick (draw 10) and the potency roll (draw 11) with their arithmetic.</summary>
        public static void BreedRoll(string session, in PetDevice.BreedingMath.BreedingInputs inputs, PetDevice.BreedingMath.BreedingOutcome o)
        {
            var c = inputs.Config;
            var draws = o.RngDraws;

            // Reconstruct the inherited baby (before mutation) to list the eligible lines the pick saw.
            var inherited = o.Baby;
            switch (o.StatLine)
            {
                case PetDevice.BreedingMath.MutationLine.Damage: inherited.Dmg -= 1; break;
                case PetDevice.BreedingMath.MutationLine.DamageResist: inherited.Dr -= 1; break;
                case PetDevice.BreedingMath.MutationLine.Crit: inherited.Crit -= 1; break;
                case PetDevice.BreedingMath.MutationLine.Vitality: inherited.Vit -= 1; break;
            }
            if (o.PotencyApplied)
            {
                inherited.Pot -= 1;
                inherited.PotencyStored -= o.PotencyStep;
            }

            var eligible = PetDevice.BreedingMath.EligibleStatLines(inherited, c.MaxStatMutations);
            var eligibleNames = new StringBuilder();
            foreach (var line in eligible)
                eligibleNames.Append(eligibleNames.Length > 0 ? "," : "").Append(line);

            var decayed = (c.BaseMutationChance + o.IncenseBonus) / (1.0 + c.MutationDecayRate * o.InheritedStatMutations);
            var pickConsumed = o.StatMutated && eligible.Count > 0;
            var pickRoll = pickConsumed && draws.Count >= 10 ? draws[9] : double.NaN;

            var stat = Begin("breed.roll", session).Add("kind", "stat")
                .Add("rule", "chance=clamp(max(floor, (base+incense)/(1+decay*inherited)),0,1); mutated=forced||roll<chance")
                .Add("base", c.BaseMutationChance).Add("decay", c.MutationDecayRate).Add("inherited", o.InheritedStatMutations)
                .Add("decayed", decayed).Add("floor", c.MutationMinFloor)
                .Add("incenseA", inputs.Options.IncenseA).Add("incenseB", inputs.Options.IncenseB).Add("incense", o.IncenseBonus)
                .Add("chance", o.StatChance).Add("draw", 9).Add("roll", o.StatRoll).Add("forced", c.ForceMutation).Add("mutated", o.StatMutated)
                .Add("maxStatMutations", c.MaxStatMutations).Add("eligible", eligibleNames.ToString()).Add("eligibleCount", eligible.Count);
            if (pickConsumed)
                stat.Add("pickDraw", 10).Add("pickRoll", pickRoll).Add("pickRule", "index=min(n-1,floor(roll*n))")
                    .Add("pickIndex", PetDevice.BreedingMath.PickIndex(pickRoll, eligible.Count));
            else
                stat.Add("pickDraw", "none");
            stat.Add("line", o.StatLine).Add("lineName", PetDevice.BreedingMath.LineName(o.StatLine)).Add("step", o.StatStep).Add("allLinesCapped", o.StatAllLinesCapped)
                .Emit();

            var potencyDraw = 9 + (pickConsumed ? 1 : 0) + 1;
            Begin("breed.roll", session).Add("kind", "potency")
                .Add("rule", "mutated=roll<clamp(chance,0,1); step=stepConfig, /4 (min 1) at or above softCap, then min(step, hardCap-stored); applied when step>0")
                .Add("chance", o.PotencyChance).Add("draw", potencyDraw).Add("roll", o.PotencyRoll).Add("mutated", o.PotencyMutated)
                .Add("storedBefore", inherited.PotencyStored).Add("stepConfig", c.PotencyMutationStep)
                .Add("softCap", c.PotencySoftCap).Add("softCapped", o.PotencySoftCapped)
                .Add("hardCapBreeding", c.PotencyHardCap).Add("maxStored", c.PotencyMaxStored).Add("hardCap", c.ResolvedPotencyHardCap)
                .Add("step", o.PotencyStep).Add("applied", o.PotencyApplied).Add("storedAfter", o.Baby.PotencyStored)
                .Add("guardianSpawns", o.GuardianSpawned)
                .Emit();
        }

        /// <summary>The same REPLAY blob the parity harness runs (BreedingReplay.ToJson). Phase: decision or blessing.</summary>
        public static void BreedReplay(string session, string phase, string json)
        {
            Begin("breed.replay", session).Add("phase", phase).Add("json", json).Emit();
        }

        // ------------------------------------------------------------------------------------------
        // Consumables, neutering, tailoring, maturity (standalone events: fresh session id each)
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// A consumable used on a device: the property it writes, its previous and new value, and
        /// whether the item was consumed or refused (and why). Refusals carry consumed=false.
        /// </summary>
        public static void ConsumableUse(Player player, WorldObject item, WorldObject target, string property, object before, object after, bool consumed, string reason)
        {
            var r = Begin("consumable.use", NewSessionId()).AddPlayer("p.", player)
                .Add("item", item?.Name).Add("itemWcid", item?.WeenieClassId ?? 0).AddGuid("itemGuid", item?.Guid.Full ?? 0)
                .Add("target", target?.Name).AddGuid("targetGuid", target?.Guid.Full ?? 0).Add("targetWcid", target?.WeenieClassId ?? 0)
                .Add("property", property).Add("before", before).Add("after", after)
                .Add("consumed", consumed).Add("reason", reason ?? "ok");
            if (target is PetDevice d)
                r.Add("targetJuvenile", d.IsJuvenile).Add("targetNeutered", d.GetProperty(PropertyBool.PetNeutered) == true);
            r.Emit();
        }

        /// <summary>Neutering kit: PetNeutered before and after, whether the kit was consumed (or why it was refused).</summary>
        public static void Neuter(Player player, WorldObject kit, WorldObject target, bool wasNeutered, bool applied, string reason)
        {
            Begin("neuter.apply", NewSessionId()).AddPlayer("p.", player)
                .Add("kit", kit?.Name).Add("kitWcid", kit?.WeenieClassId ?? 0).AddGuid("kitGuid", kit?.Guid.Full ?? 0)
                .Add("target", target?.Name).AddGuid("targetGuid", target?.Guid.Full ?? 0)
                .Add("property", "PetNeutered").Add("before", wasNeutered).Add("after", wasNeutered || applied)
                .Add("applied", applied).Add("consumed", applied).Add("reason", reason ?? "ok").Emit();
        }

        /// <summary>The visual property set tailoring moves, as read from an object (kit or device).</summary>
        public static Record AddVisuals(this Record r, string p, WorldObject o)
        {
            if (o == null)
                return r;
            return r.AddGuid(p + "setup", o.GetProperty(PropertyDataId.VisualOverrideSetup) ?? 0)
                    .AddGuid(p + "motionTable", o.GetProperty(PropertyDataId.VisualOverrideMotionTable) ?? 0)
                    .AddGuid(p + "combatTable", o.GetProperty(PropertyDataId.VisualOverrideCombatTable) ?? 0)
                    .AddGuid(p + "soundTable", o.GetProperty(PropertyDataId.VisualOverrideSoundTable) ?? 0)
                    .AddGuid(p + "paletteBase", o.GetProperty(PropertyDataId.VisualOverridePaletteBase) ?? 0)
                    .AddGuid(p + "clothingBase", o.GetProperty(PropertyDataId.VisualOverrideClothingBase) ?? 0)
                    .Add(p + "paletteTemplate", o.GetProperty(PropertyInt.VisualOverridePaletteTemplate) ?? 0)
                    .Add(p + "shade", o.GetProperty(PropertyFloat.VisualOverrideShade) ?? 0.0)
                    .Add(p + "scale", o.GetProperty(PropertyFloat.VisualOverrideScale) ?? 0.0)
                    .Add(p + "creatureName", o.GetProperty(PropertyString.CapturedCreatureName) ?? "")
                    .Add(p + "variant", o.GetProperty(PropertyInt.CapturedCreatureVariant) ?? 0)
                    .Add(p + "creatureType", o.GetProperty(PropertyInt.CapturedCreatureType) ?? 0)
                    .Add(p + "creatureWcid", o.GetProperty(PropertyInt.CapturedCreatureWCID) ?? 0)
                    .Add(p + "sourceDamageType", o.GetProperty(PropertyInt.CapturedSourceDamageType) ?? 0)
                    .AddGuid(p + "icon", o.GetProperty(PropertyDataId.VisualOverrideIcon) ?? 0)
                    .Add(p + "animParts", (o.GetProperty(PropertyString.CapturedObjDescAnimParts) ?? "").Length)
                    .Add(p + "palettes", (o.GetProperty(PropertyString.CapturedObjDescPalettes) ?? "").Length)
                    .Add(p + "textures", (o.GetProperty(PropertyString.CapturedObjDescTextures) ?? "").Length);
        }

        /// <summary>A credited (or refused) maturity kill with its share and tier arithmetic.</summary>
        public static void MaturityKill(PetDevice device, Player owner, CombatPet pet, Creature victim, float victimDamage, float totalHealth, double minShare, int? tier,
            bool credited, string reason, float multiplier, int killsToAdd, int killsBefore, int killsAfter, int stageBefore, int stageAfter, bool adult)
        {
            var r = Begin("maturity.kill", NewSessionId()).AddCreature("victim.", victim).Add("victimLevel", victim?.Level ?? 0);
            r.AddGuid("device", device?.Guid.Full ?? 0).Add("deviceName", device?.Name).Add("owner", owner?.Name).Add("pet", pet?.Name)
             .Add("shareRule", "share=petDamage/historyTotal, credited when share>=minShare and victimLevel>=tier")
             .Add("petDamage", victimDamage).Add("historyTotal", totalHealth).Add("share", totalHealth > 0 ? victimDamage / totalHealth : 0.0f).Add("minShare", minShare)
             .Add("tier", tier.HasValue ? tier.Value.ToString(CultureInfo.InvariantCulture) : "none")
             .Add("credited", credited).Add("reason", reason ?? "ok");
            if (credited)
                r.Add("killRule", "killsToAdd=max(1,round(xpMult)); stage=min(stages,kills/ceil(required/stages)+1); adult when kills>=required")
                 .Add("xpMult", multiplier).Add("killsToAdd", killsToAdd).Add("killsBefore", killsBefore).Add("killsAfter", killsAfter)
                 .Add("killsRequired", PetDevice.MaturityKillsRequired).Add("stages", PetDevice.MaturityStages).Add("killsPerStage", PetDevice.MaturityKillsPerStage)
                 .Add("stageBefore", stageBefore).Add("stageAfter", stageAfter).Add("adult", adult);
            r.Emit();
        }
    }
}
