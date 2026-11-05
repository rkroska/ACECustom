using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Reflection boss (owner 2026-11-04, for live events): a monster weenie with ReflectionLives set comes back after it
    /// dies, wearing the look of a player who damaged it and named "&lt;Player&gt;'s Reflection", until its lives run out.
    /// Each life is linearly tougher and pays linearly more. Who is copied is set by ReflectionPick (top damager, random
    /// damager or random player in sight), always skipping anyone this chain already copied and anyone no longer online;
    /// then a random player nearby who hasn't been copied; then a random player nearby. It never comes back in its own
    /// look. With no player in sight the chain ends.
    /// </summary>
    partial class Creature
    {
        private const string DefaultReflectionNameFormat = "%s's Reflection";

        /// <summary>
        /// Called from the death sequence once the corpse is made: if this monster has Reflection lives left, spawns the next
        /// life at the same spot, dressed as the chosen player. Does nothing for ordinary monsters, players and pets.
        /// Failures are logged and never break the death sequence.
        /// </summary>
        private void TrySpawnReflection()
        {
            if (this is Player || this is Pet)
                return;

            var lives = GetProperty(PropertyInt.ReflectionLives) ?? 0;
            var nextLife = (GetProperty(PropertyInt.ReflectionLife) ?? 0) + 1;
            if (lives <= 0 || nextLife > lives || Location == null)
                return;

            try
            {
                var copied = ParseReflectionCopied(GetProperty(PropertyString.ReflectionCopied));
                var player = ChooseReflectionPlayer(copied);
                if (player == null)
                {
                    log.Info($"[REFLECTION] {Name} (0x{Guid.Full:X8}) has no player online nearby to copy; life {nextLife} of {lives} not spawned.");
                    return;
                }

                var wcid = (uint)(GetProperty(PropertyInt.ReflectionWcid) ?? 0);
                if (wcid == 0)
                    wcid = WeenieClassId;

                if (WorldObjectFactory.CreateNewWorldObject(wcid) is not Creature reflection)
                {
                    log.Error($"[REFLECTION] {Name} (0x{Guid.Full:X8}): weenie {wcid} is not a creature; life {nextLife} not spawned.");
                    return;
                }

                copied.Add(player.Guid.Full);
                CarryReflectionSettings(reflection, nextLife, copied);
                reflection.CopyLookFrom(player);

                var format = GetProperty(PropertyString.ReflectionNameFormat);
                if (string.IsNullOrWhiteSpace(format))
                    format = DefaultReflectionNameFormat;
                reflection.Name = format.Replace("%s", player.Name.TrimStart('+'));

                reflection.ScaleForReflectionLife(nextLife);

                if (!PlaceReflection(reflection))
                {
                    log.Error($"[REFLECTION] {reflection.Name}: could not enter the world at {Location}; life {nextLife} not spawned.");
                    reflection.Destroy();
                    return;
                }

                // Arrival lines (emote category 9) normally fire only for generator spawns; every life gets them.
                reflection.EmoteManager?.OnGeneration();

                log.Info($"[REFLECTION] {Name} (0x{Guid.Full:X8}) returned as {reflection.Name} (0x{reflection.Guid.Full:X8}), life {nextLife} of {lives}, health {reflection.Health.MaxValue:N0}.");
            }
            catch (Exception ex)
            {
                log.Error($"[REFLECTION] {Name} (0x{Guid.Full:X8}): spawning life {nextLife} failed.", ex);
            }
        }

        /// <summary>
        /// Picks the player to copy by ReflectionPick: 0 the top damager, 1 a random damager, 2 a random player in sight
        /// (a pet's damage counts for its owner; anyone already copied or offline is skipped). Then a random visible
        /// player not yet copied; then any random visible player. Null when no player is online and in sight.
        /// </summary>
        private Player ChooseReflectionPlayer(HashSet<uint> copied)
        {
            var pick = GetProperty(PropertyInt.ReflectionPick) ?? 0;
            if (pick != 2)
            {
                var damagers = DamageHistory.GetDamagers()
                    .OrderByDescending(d => d.TotalDamage)
                    .Select(d => d.TryGetPetOwnerOrAttacker() as Player)
                    .Where(p => p != null && !copied.Contains(p.Guid.Full) && IsReflectionCandidate(p))
                    .Distinct()
                    .ToList();

                if (damagers.Count > 0)
                    return pick == 1 ? damagers[ThreadSafeRandom.Next(0, damagers.Count - 1)] : damagers[0];
            }

            var nearby = PhysicsObj?.ObjMaint?.GetKnownPlayersValuesAsPlayer()?.Where(IsReflectionCandidate).ToList() ?? new List<Player>();
            if (nearby.Count == 0)
                return null;

            var fresh = nearby.Where(p => !copied.Contains(p.Guid.Full)).ToList();
            var pool = fresh.Count > 0 ? fresh : nearby;
            return pool[ThreadSafeRandom.Next(0, pool.Count - 1)];
        }

        /// <summary>True when the player is still logged in and in the world, so their look can be read.</summary>
        private static bool IsReflectionCandidate(Player player)
        {
            return player != null && player.CurrentLandblock != null && PlayerManager.GetOnlinePlayer(player.Guid) != null;
        }

        /// <summary>
        /// Puts every Reflection setting of the boss that died first onto the next life, so the chain behaves the same even
        /// when ReflectionWcid points at a weenie with other settings.
        /// </summary>
        private void CarryReflectionSettings(Creature reflection, int life, HashSet<uint> copied)
        {
            reflection.SetProperty(PropertyInt.ReflectionLives, GetProperty(PropertyInt.ReflectionLives) ?? 0);
            reflection.SetProperty(PropertyInt.ReflectionLife, life);
            reflection.SetProperty(PropertyString.ReflectionCopied, string.Join(",", copied.Select(g => g.ToString("X8"))));

            CarryOrClear(reflection, PropertyInt.ReflectionWcid, GetProperty(PropertyInt.ReflectionWcid));
            CarryOrClear(reflection, PropertyInt.ReflectionDamageRatingStep, GetProperty(PropertyInt.ReflectionDamageRatingStep));
            CarryOrClear(reflection, PropertyInt.ReflectionPick, GetProperty(PropertyInt.ReflectionPick));

            var healthStep = GetProperty(PropertyFloat.ReflectionHealthStep);
            if (healthStep.HasValue) reflection.SetProperty(PropertyFloat.ReflectionHealthStep, healthStep.Value);
            else reflection.RemoveProperty(PropertyFloat.ReflectionHealthStep);

            var rewardStep = GetProperty(PropertyFloat.ReflectionRewardStep);
            if (rewardStep.HasValue) reflection.SetProperty(PropertyFloat.ReflectionRewardStep, rewardStep.Value);
            else reflection.RemoveProperty(PropertyFloat.ReflectionRewardStep);

            var format = GetProperty(PropertyString.ReflectionNameFormat);
            if (!string.IsNullOrWhiteSpace(format)) reflection.SetProperty(PropertyString.ReflectionNameFormat, format);
            else reflection.RemoveProperty(PropertyString.ReflectionNameFormat);
        }

        /// <summary>Sets an int property to the value, or removes it when the value is null.</summary>
        private static void CarryOrClear(WorldObject target, PropertyInt property, int? value)
        {
            if (value.HasValue) target.SetProperty(property, value.Value);
            else target.RemoveProperty(property);
        }

        /// <summary>
        /// Gives this creature the player's body and look as the player sees it in game (worn gear, Dressing Room, show helm
        /// and show cloak), by the same CalculateObjDesc a corpse is dressed with. The weenie's own model is replaced; its
        /// scale and translucency are kept.
        /// </summary>
        private void CopyLookFrom(Player player)
        {
            var objDesc = player.CalculateObjDesc();

            SetupTableId = player.SetupTableId;
            MotionTableId = player.MotionTableId;
            SoundTableId = player.SoundTableId;
            if (player.PaletteBaseId.HasValue) PaletteBaseId = player.PaletteBaseId;
            if (player.CombatTableDID.HasValue) CombatTableDID = player.CombatTableDID;
            Heritage = player.Heritage;
            Gender = player.Gender;

            // The weenie's own clothing and colors would be drawn on top of the copied look.
            ClothingBase = null;
            PaletteTemplate = null;
            Shade = null;

            Biota.PropertiesAnimPart = objDesc.AnimPartChanges.Clone(BiotaDatabaseLock);
            Biota.PropertiesPalette = objDesc.SubPalettes.Clone(BiotaDatabaseLock);
            Biota.PropertiesTextureMap = objDesc.TextureChanges.Clone(BiotaDatabaseLock);
        }

        /// <summary>
        /// Linear scaling for a Reflection life: health x(1 + HealthStep x life), XP and luminance x(1 + RewardStep x life),
        /// damage rating + DamageRatingStep x life, all from this weenie's own values. Starts at full health.
        /// </summary>
        private void ScaleForReflectionLife(int life)
        {
            var healthMult = 1.0 + (GetProperty(PropertyFloat.ReflectionHealthStep) ?? 0.0) * life;
            if (healthMult > 0 && healthMult != 1.0)
                Health.StartingValue = (uint)Math.Clamp(Math.Round(Health.StartingValue * healthMult), 1, uint.MaxValue);
            Health.Current = Health.MaxValue;

            var rewardMult = 1.0 + (GetProperty(PropertyFloat.ReflectionRewardStep) ?? 0.0) * life;
            if (rewardMult > 0 && rewardMult != 1.0)
            {
                var xp = WeenieKillXp;
                if (xp > 0)
                {
                    SetProperty(PropertyInt64.XpOverride64, (long)Math.Min(Math.Round(xp * rewardMult), MaxWeenieKillXp));
                    RemoveProperty(PropertyInt.XpOverride);
                }

                var lum = LuminanceAward ?? 0;
                if (lum > 0)
                    LuminanceAward = (int)Math.Min(Math.Round(lum * rewardMult), int.MaxValue);
            }

            var ratingStep = GetProperty(PropertyInt.ReflectionDamageRatingStep) ?? 0;
            if (ratingStep != 0)
                SetProperty(PropertyInt.DamageRating, (GetProperty(PropertyInt.DamageRating) ?? 0) + ratingStep * life);
        }

        /// <summary>
        /// Enters the world where this creature died: 0.25 above the spot first (a scaled model can be refused at the exact
        /// floor height, as RiftManager found), then the exact spot.
        /// </summary>
        private bool PlaceReflection(Creature reflection)
        {
            var raised = new Position(Location);
            raised.PositionZ += 0.25f;

            foreach (var pos in new[] { raised, new Position(Location) })
            {
                reflection.Location = pos;
                if (reflection.EnterWorld())
                    return true;
            }
            return false;
        }

        /// <summary>Reads ReflectionCopied ("0A1B2C3D,...") into a set of GUIDs; bad entries are ignored.</summary>
        private static HashSet<uint> ParseReflectionCopied(string value)
        {
            var set = new HashSet<uint>();
            if (string.IsNullOrWhiteSpace(value))
                return set;

            foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (uint.TryParse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var guid))
                    set.Add(guid);
            }
            return set;
        }
    }
}
