using System;
using System.Collections.Generic;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Pet maturity: a bred essence is born a juvenile and grows into an adult by killing creatures
    /// at or above its own tier while summoned. Growth is tracked as a kill counter on the device,
    /// split into stages; each stage steps the summoned pet's size and strength toward adult values.
    /// Juveniles cannot breed. Maturity is its own counter, independent of bond and potency.
    /// </summary>
    public partial class PetDevice
    {
        /// <summary>True while this essence is still growing. Only bred babies are ever juvenile.</summary>
        public bool IsJuvenile => ServerConfig.pet_maturity_enabled.Value && (GetProperty(PropertyBool.PetIsJuvenile) ?? false);

        /// <summary>True if this essence was born from a breed (juvenile now, or raised to adulthood).</summary>
        public bool WasBredJuvenile => GetProperty(PropertyInt.PetMaturityKills).HasValue;

        public int MaturityKills => Math.Max(0, GetProperty(PropertyInt.PetMaturityKills) ?? 0);

        public static int MaturityKillsRequired => Math.Max(1, (int)ServerConfig.pet_maturity_kills_required.Value);

        public static int MaturityStages => Math.Max(1, (int)ServerConfig.pet_maturity_stages.Value);

        public static int MaturityKillsPerStage => Math.Max(1, (int)Math.Ceiling(MaturityKillsRequired / (double)MaturityStages));

        /// <summary>Current growth stage, 1..stages, for a juvenile. Adults report stages + 1.</summary>
        public int MaturityStage
        {
            get
            {
                if (!IsJuvenile)
                    return MaturityStages + 1;
                return Math.Min(MaturityStages, MaturityKills / MaturityKillsPerStage + 1);
            }
        }

        /// <summary>0 at stage 1, rising per stage, 1.0 for an adult.</summary>
        public double MaturityFraction
        {
            get
            {
                if (!IsJuvenile)
                    return 1.0;
                return Math.Clamp((MaturityStage - 1) / (double)MaturityStages, 0.0, 1.0);
            }
        }

        private static string SanitizeAscii(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            var sb = new System.Text.StringBuilder(str.Length);
            foreach (var c in str)
            {
                if (c >= 32 && c <= 126)
                    sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        /// <summary>Configured growth stage name for a 1-based stage; "Stage N" when the list is short.</summary>
        public static string GetMaturityStageName(int stage)
        {
            var raw = ServerConfig.pet_maturity_stage_names.Value ?? "";
            var names = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (stage >= 1 && stage <= names.Length)
            {
                var clean = SanitizeAscii(names[stage - 1]);
                if (clean.Length > 0)
                    return clean;
            }
            return $"Stage {stage}";
        }

        /// <summary>The current stage's name for a juvenile, or "Adult".</summary>
        public string MaturityStageName => IsJuvenile ? GetMaturityStageName(MaturityStage) : "Adult";

        /// <summary>All stage names, for stripping a previous stage's tag out of a pet's name.</summary>
        public static IEnumerable<string> AllMaturityStageNames()
        {
            var raw = ServerConfig.pet_maturity_stage_names.Value ?? "";
            foreach (var n in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var clean = SanitizeAscii(n);
                if (clean.Length > 0) yield return clean;
            }
            for (var i = 1; i <= MaturityStages; i++)
                yield return $"Stage {i}";
        }

        /// <summary>Multiplier on the essence's adult scale for the summoned pet.</summary>
        public double MaturityScaleMult => Lerp(ServerConfig.pet_maturity_juvenile_scale.Value, 1.0, MaturityFraction);

        /// <summary>Multiplier on the inherited combat ratings and vitality bonus for the summoned pet.</summary>
        public double MaturityStrengthMult => Lerp(ServerConfig.pet_maturity_juvenile_strength.Value, 1.0, MaturityFraction);

        private static double Lerp(double from, double to, double t)
        {
            from = Math.Clamp(from, 0.05, 1.0);
            return from + (to - from) * Math.Clamp(t, 0.0, 1.0);
        }

        /// <summary>Called on a freshly bred baby. No-op when maturity is disabled.</summary>
        public void MarkBornJuvenile()
        {
            // The kill counter marks "born from a breed" and drives imprinting, so it is always written;
            // only the juvenile state itself is gated by the maturity switch.
            SetProperty(PropertyInt.PetMaturityKills, 0);

            if (ServerConfig.pet_maturity_enabled.Value)
                SetProperty(PropertyBool.PetIsJuvenile, true);
        }

        /// <summary>
        /// Imprinting: the first character to summon a bred essence becomes its only owner. This is
        /// what stops a breeder from raising an elite adult and handing it to someone else - a baby can
        /// change hands only while it is still an unsummoned newborn.
        /// </summary>
        public void TryImprintOnSummon(Player player)
        {
            if (player == null || !ServerConfig.pet_maturity_imprint_on_summon.Value || !WasBredJuvenile || IsPetBondAttuned)
                return;

            PetBondAttuned = true;
            PetBondAttunedCharacterId = (long)player.Character.Id;
            Attuned = AttunedStatus.Attuned;
            Bonded = BondedStatus.Bonded;
            ChangesDetected = true;
            SaveBiotaToDatabase();

            player.UpdateProperty(this, PropertyInt.Attuned, (int)AttunedStatus.Attuned);
            player.UpdateProperty(this, PropertyInt.Bonded, (int)BondedStatus.Bonded);

            player.SendMessage($"{GetBondMessageDisplayName()} has imprinted on you. It is now bound to this character and cannot be traded or dropped.");
            if (PetTrace.Enabled)
                PetTrace.Begin("maturity.imprint", PetTrace.NewSessionId()).AddPlayer("p.", player).AddGuid("device", Guid.Full).Add("deviceName", Name)
                    .Add("bondAttuned", true).Add("bondChar", PetBondAttunedCharacterId ?? 0).Emit();
            else
                log.Info($"[PetMaturity] {Name} (0x{Guid.Full:X8}) imprinted on {player.Name}.");
        }

        /// <summary>ID-panel line for a bred essence's ownership state, or null for an essence that was never bred.</summary>
        public string BuildImprintAppraisalLine()
        {
            if (!ServerConfig.pet_maturity_imprint_on_summon.Value || !WasBredJuvenile)
                return null;

            if (!IsPetBondAttuned)
                return "Bond: Unbound (imprints on first summon)";

            var ownerName = "another character";
            if (PetBondAttunedCharacterId.HasValue)
                ownerName = PlayerManager.FindByGuid(new ACE.Entity.ObjectGuid((uint)PetBondAttunedCharacterId.Value))?.Name ?? ownerName;
            return $"Bond: {ownerName}";
        }

        /// <summary>One ID-panel line describing growth, or null for an essence that was never bred.</summary>
        public string BuildMaturityAppraisalLine()
        {
            if (!ServerConfig.pet_maturity_enabled.Value || !WasBredJuvenile)
                return null;

            if (!IsJuvenile)
                return "Growth: Adult";

            var kills = MaturityKills;
            var required = MaturityKillsRequired;
            var stage = MaturityStage;
            var stages = MaturityStages;
            var toNext = Math.Max(0, Math.Min(required, stage * MaturityKillsPerStage) - kills);
            var toAdult = Math.Max(0, required - kills);

            // One line. The rules (tier gate, damage share) are taught by the NPCs, not the item.
            var nextLabel = stage < stages
                ? $"{toNext} kills to {GetMaturityStageName(stage + 1)}, {toAdult} to Adult"
                : $"{toAdult} kills to Adult";
            return $"Growth: {GetMaturityStageName(stage)} {stage}/{stages} - {nextLabel}";
        }

        /// <summary>
        /// The one rule for whether a kill counts toward a juvenile's growth, shared by
        /// <see cref="CreditMaturityKills"/> and the bond award in Creature_Death so the two cannot drift:
        /// the pet did at least <paramref name="minShare"/> of the damage, to a creature at or above the
        /// essence's tier. Returns null when the kill qualifies, otherwise the reason it does not.
        /// <paramref name="tier"/> is the essence tier, for callers that trace it. Callers decide what a
        /// non-juvenile means; this does not look at maturity.
        /// </summary>
        public static string MaturityCreditRefusal(PetDevice device, double damageShare, int victimLevel, double minShare, out int? tier)
        {
            tier = ACE.Server.Factories.Tables.Wcids.PetDeviceWcids.GetPetLevel(device.WeenieClassId);

            if (damageShare < minShare)
                return "share below minShare";

            if (tier.HasValue && victimLevel < tier.Value)
                return "victim level below tier";

            return null;
        }

        /// <summary>
        /// Called when any creature dies. Every juvenile combat pet that did enough of the damage to
        /// a creature at or above its essence tier earns one maturity kill.
        /// </summary>
        public static void CreditMaturityKills(Creature victim)
        {
            if (!ServerConfig.pet_maturity_enabled.Value || victim == null || victim is Player || victim is CombatPet || victim is MatingGuardian)
                return;

            try
            {
                var totalHealth = victim.DamageHistory.TotalHealth;
                if (totalHealth <= 0)
                    return;

                var minShare = Math.Clamp(ServerConfig.pet_maturity_min_damage_share.Value, 0.0, 1.0);
                var victimLevel = victim.Level ?? 0;

                HashSet<uint> credited = null;

                foreach (var kvp in victim.DamageHistory.TotalDamage)
                {
                    var info = kvp.Value;
                    if (info.TotalDamage <= 0)
                        continue;

                    if (info.TryGetAttacker() is not CombatPet combatPet)
                        continue;

                    var owner = info.TryGetPetOwner() ?? combatPet.P_PetOwner;
                    if (owner == null)
                        continue;

                    var share = info.TotalDamage / totalHealth;

                    var device = combatPet.TryGetSummoningDevice();
                    if (device == null && combatPet.SummoningDeviceGuid != ACE.Entity.ObjectGuid.Invalid)
                        device = owner.FindObject(combatPet.SummoningDeviceGuid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) as PetDevice;
                    if (device == null || !device.IsJuvenile)
                        continue;

                    var refusal = MaturityCreditRefusal(device, share, victimLevel, minShare, out var tier);
                    if (refusal != null)
                    {
                        // [PetTrace] a juvenile that missed out is worth a line; adults never reach here.
                        if (PetTrace.Enabled)
                            PetTrace.MaturityKill(device, owner, combatPet, victim, info.TotalDamage, totalHealth, minShare, tier, false, refusal, 0, 0, 0, 0, 0, 0, false);
                        continue;
                    }

                    credited ??= new HashSet<uint>();
                    if (!credited.Add(device.Guid.Full))
                        continue;

                    try
                    {
                        device.AddMaturityKill(owner, combatPet, victim, info.TotalDamage, totalHealth, minShare, tier);
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"[PetMaturity] Credit failed for {device.Name} (0x{device.Guid.Full:X8}): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warn($"[PetMaturity] CreditMaturityKills threw for {victim.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Growth should be seen, not just read: everyone nearby gets an emote line from the pet, and
        /// an optional extra effect on top of the level-up flash ApplyMaturity already plays.
        /// </summary>
        private static void NarrateGrowth(CombatPet pet, string emoteText, PlayScript? extraEffect)
        {
            if (pet == null || pet.IsDestroyed || pet.CurrentLandblock == null)
                return;

            pet.EnqueueBroadcast(new GameMessageEmoteText(pet.Guid.Full, pet.Name, emoteText), WorldObject.LocalBroadcastRange);
            if (extraEffect.HasValue)
                pet.PlayParticleEffect(extraEffect.Value, pet.Guid);
        }

        /// <summary>Records one kill, advancing a stage or reaching adulthood when the counter crosses a boundary.</summary>
        private void AddMaturityKill(Player owner, CombatPet pet, Creature victim = null, float victimDamage = 0, float totalHealth = 0, double minShare = 0, int? tier = null)
        {
            var multiplier = (float)(GetProperty(PropertyFloat.PetMaturityXpMultiplier) ?? 1.0);
            var killsToAdd = (int)Math.Max(1, Math.Round(multiplier));
            var before = MaturityStage;
            var killsBefore = MaturityKills;
            var kills = killsBefore + killsToAdd;
            SetProperty(PropertyInt.PetMaturityKills, kills);

            var displayName = GetBondMessageDisplayName();

            if (kills >= MaturityKillsRequired)
            {
                RemoveProperty(PropertyBool.PetIsJuvenile);
                RemoveProperty(PropertyFloat.PetMaturityXpMultiplier);
                ChangesDetected = true;
                SaveBiotaToDatabase();

                if (owner?.Session != null)
                    owner.SendMessage($"{displayName} has reached adulthood! It stands at its full size, fights at full strength, and can now breed.");
                if (PetTrace.Enabled)
                    PetTrace.MaturityKill(this, owner, pet, victim, victimDamage, totalHealth, minShare, tier, true, null, multiplier, killsToAdd, killsBefore, kills, before, MaturityStage, true);
                else
                    log.Info($"[PetMaturity] {Name} (0x{Guid.Full:X8}, {owner?.Name}) reached adulthood after {kills} kills.");

                pet?.ApplyMaturity(this, grew: true);
                NarrateGrowth(pet, "lets out a roar and rises to its full size!", PlayScript.WeddingBliss);
                return;
            }

            ChangesDetected = true;
            SaveBiotaToDatabase();

            var after = MaturityStage;
            if (PetTrace.Enabled)
                PetTrace.MaturityKill(this, owner, pet, victim, victimDamage, totalHealth, minShare, tier, true, null, multiplier, killsToAdd, killsBefore, kills, before, after, false);
            if (after > before)
            {
                var stageName = GetMaturityStageName(after);
                if (owner?.Session != null)
                    owner.SendMessage($"{displayName} has grown into a {stageName}! Its body swells with new strength. ({after}/{MaturityStages})");
                if (!PetTrace.Enabled)
                    log.Info($"[PetMaturity] {Name} (0x{Guid.Full:X8}, {owner?.Name}) grew to stage {after}/{MaturityStages} at {kills} kills.");
                pet?.ApplyMaturity(this, grew: true);
                NarrateGrowth(pet, $"shudders and swells as it grows into a {stageName}!", null);
            }
        }
    }
}
