using ACE.Entity.Enum;
using ACE.Server.Factories;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// Tier 11+ melee weapons with Slash Rending on a Slashing/Piercing base deal Slashing only (owner 2026-10-28).
    ///
    /// WHY. Player.GetDamageType picks ONE element per swing for a Slash/Pierce weapon: Pierce on a thrust,
    /// Slash otherwise. A swing under the power bar's thrust threshold (full speed) is a thrust, and the shield
    /// and dual-wield stances pick thrusts too, while a rend only applies on its own element
    /// (WorldObject_Weapon.GetRendingMod) - so every thrust lost the whole Slash Rending / Rend Power card.
    /// The Dolabra and the slash/pierce swords are the common cases.
    ///
    /// SCOPE. Only weapons that pass <see cref="Applies"/>: a MeleeWeapon (not missile, thrown or caster),
    /// tier 11+ by ZoneCraftGate.TierOf (ZcTier or the legacy WeaponAugScaleTier), damage type exactly
    /// Slash|Pierce, Slash Rending in any imbue slot. A Slash/Pierce weapon with Pierce Rending, no rend, or
    /// tier 10 and below is left alone.
    ///
    /// WHERE IT RUNS.
    ///   - MeleeWeapon(Biota) ctor: every weapon loaded from the shard is rewritten and saved on its owner's
    ///     next save. This is the retroactive half: no SQL, so a copy already in memory can never save the old
    ///     type back over it.
    ///   - Creature_Death T11+ sweep: new drops are written Slashing before the element tint is applied.
    ///   - Player.GetDamageType: <see cref="Effective"/> makes a weapon that only became eligible mid-session
    ///     (an essence or forge result, an admin command) hit as Slash at once; its stored type is fixed on the
    ///     next load.
    /// </summary>
    public static class ZoneSlashRendDamageType
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const DamageType SlashPierce = DamageType.Slash | DamageType.Pierce;

        /// <summary>True for a tier 11+ melee weapon whose damage type is exactly Slashing/Piercing and that carries
        /// Slash Rending. Cheapest checks first: this runs on every weapon load.</summary>
        public static bool Applies(WorldObject wo)
        {
            return wo is MeleeWeapon
                && wo.W_DamageType == SlashPierce
                && (wo.GetImbuedEffects() & ImbuedEffectType.SlashRending) != 0
                && ZoneCraftGate.TierOf(wo) >= LootGenerationFactory.ZoneLootSetMinTier;
        }

        /// <summary>The damage type <paramref name="wo"/> should deal, given the type read from it: Slash for a weapon
        /// this rule covers, otherwise <paramref name="damageType"/> unchanged. Reads only; never writes the item.</summary>
        public static DamageType Effective(WorldObject wo, DamageType damageType)
        {
            return damageType == SlashPierce && Applies(wo) ? DamageType.Slash : damageType;
        }

        /// <summary>Rewrites a covered weapon to Slashing only and drops the Piercing bit from its element tint
        /// (LootGenerationFactory.ApplyZoneElementTint gave it Slashing + Piercing). Returns true when the item
        /// changed; a second call is a no-op. Call only before the item is sent to a client (load, drop) - the
        /// tint is part of the object description and is not re-sent here. <paramref name="existingItem"/> = an item
        /// loaded from the shard (logged at Info, once per weapon, so the retroactive rewrite can be audited); a new
        /// drop logs at Debug.</summary>
        public static bool Normalize(WorldObject wo, bool existingItem)
        {
            if (!Applies(wo))
                return false;

            wo.W_DamageType = DamageType.Slash;

            var fx = wo.UiEffects;
            if (fx.HasValue && (fx.Value & UiEffects.Piercing) != 0)
            {
                var slashOnly = fx.Value & ~UiEffects.Piercing;
                wo.UiEffects = slashOnly == 0 ? (UiEffects?)null : slashOnly;
            }

            var msg = $"[ZoneSlashRend] {(existingItem ? "load" : "drop")}: {wo.Name} (0x{wo.Guid}, wcid {wo.WeenieClassId}, tier {ZoneCraftGate.TierOf(wo)}) Slashing/Piercing -> Slashing";
            if (existingItem)
                log.Info(msg);
            else
                log.Debug(msg);
            return true;
        }
    }
}
