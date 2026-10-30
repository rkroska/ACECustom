namespace BalanceBench;

/// <summary>Tags each damage call with what kind of hit it is, so DamageHistory.Add can book it.
/// Several bench characters run at once (2026-10-23): every hook first selects the character it belongs to (the
/// attacker, the victim, a pet's owner, or whoever spawned the monster) and books only into that character's state.</summary>
[HarmonyPatch]
internal static class Hooks
{
    [ThreadStatic] internal static string? Kind;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.DamageTarget), new[] { typeof(Creature), typeof(WorldObject) })]
    public static void PlayerHitPre(Player __instance, Creature target, WorldObject damageSource, out string? __state)
    {
        __state = Kind;
        if (!PatchClass.IsBench(__instance)) return;
        using var scope = Bench.Use(__instance);
        bool missile = __instance.GetEquippedMissileLauncher() != null;
        if (missile)
            Kind = target == __instance.MissileTarget ? "missile" : "split";
        else
            Kind = target == __instance.MeleeTarget ? "melee" : "cleave";
        if (Kind == "melee" || Kind == "missile") Bench.LastHit = DateTime.UtcNow;
        if (Bench.Run != null && (Kind == "melee" || Kind == "missile")) Bench.Run.Swings++;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.DamageTarget), new[] { typeof(Creature), typeof(WorldObject) })]
    public static void PlayerHitPost(Player __instance, DamageEvent __result, string? __state)
    {
        if (PatchClass.IsBench(__instance))
        {
            using var scope = Bench.Use(__instance);
            if (Bench.Run != null && __result != null && __result.HasDamage && __result.IsCritical)
                Bench.Run.Crits++;
        }
        Kind = __state;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DamageEvent), nameof(DamageEvent.CalculateDamage))]
    public static void CalcPost(Creature attacker, Creature defender, WorldObject damageSource, DamageEvent __result)
    {
        if (__result == null || attacker is not Player bp || !PatchClass.IsBench(bp) || defender == null) return;
        using var scope = Bench.Use(bp);
        if (Bench.Run is { } run && Bench.Spawns.ContainsKey(defender.Guid.Full) && __result.HasDamage)
        {
            var raw = (double)__result.Damage;
            run.RawHits++;
            run.RawSum += raw;
            if (run.RawHits <= 8 || __result.IsCritical)
            {
                // the server's own factor breakdown for the first hits of every fight (drift hunt, 2026-10-23), and every crit
                // (crit test, 2026-10-23: how big a crit lands against a normal hit)
                var r = __result;
                var w = damageSource;
                Outbox.Write("hitlog", new Dictionary<string, object?>
                {
                    ["n"] = run.RawHits, ["target"] = defender.Name, ["wcid"] = defender.WeenieClassId,
                    ["weapon"] = w?.Name, ["wTier"] = w?.GetProperty((PropertyInt)9061), ["wGuid"] = w?.Guid.Full,
                    ["wSlayer"] = w?.GetProperty(PropertyFloat.SlayerDamageBonus), ["wCrit"] = w?.GetProperty(PropertyFloat.CriticalMultiplier),
                    ["wBite"] = w?.GetProperty(PropertyFloat.CriticalFrequency), ["wRend"] = w?.GetProperty((PropertyFloat)9056),
                    ["wAR"] = w?.GetProperty((PropertyFloat)9055), ["dmgType"] = r.DamageType.ToString(),
                    ["base"] = r.BaseDamage, ["attr"] = r.AttributeMod, ["power"] = r.PowerMod, ["slayer"] = r.SlayerMod,
                    ["dr"] = r.DamageRatingMod, ["sneak"] = r.SneakAttackMod, ["reckless"] = r.RecklessnessMod, ["crit"] = r.IsCritical,
                    ["critMod"] = r.CriticalDamageMod, ["critDR"] = r.CriticalDamageRatingMod, ["beforeMit"] = r.DamageBeforeMitigation,
                    ["armor"] = r.ArmorMod, ["resist"] = r.ResistanceMod, ["weaponResist"] = r.WeaponResistanceMod,
                    ["drr"] = r.DamageResistanceRatingMod, ["beforeZone"] = r.DamageBeforeZoneTaken, ["damage"] = r.Damage,
                    ["wsFlat"] = r.WeaponScalingFlatBonus, ["drrBase"] = r.DamageResistanceRatingBaseMod, ["zcCritRatio"] = r.ZcCritRatio,
                    ["raw"] = raw, ["maxHp"] = defender.Health.MaxValue, ["hpBefore"] = defender.Health.Current,
                });
            }
            if (raw > run.RawMax) run.RawMax = raw;
            if (raw >= defender.Health.MaxValue) run.KillShots++;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.SendUseDoneEvent))]
    public static void UseDonePre(Player __instance, WeenieError errorType)
    {
        if (!PatchClass.IsBench(__instance)) return;
        var map = Bench.For(__instance).UseDone;
        var k = errorType.ToString();
        lock (map) map[k] = map.GetValueOrDefault(k) + 1;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SpellProjectile), nameof(SpellProjectile.DamageTarget))]
    public static void SpellHitPre(SpellProjectile __instance, out string? __state)
    {
        __state = Kind;
        Kind = "spell:" + (__instance.Spell?.Name ?? "?");
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(SpellProjectile), nameof(SpellProjectile.DamageTarget))]
    public static void SpellHitPost(string? __state) => Kind = __state;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ACE.Server.WorldObjects.Managers.EnchantmentManager), nameof(ACE.Server.WorldObjects.Managers.EnchantmentManager.ApplyDamageTick))]
    public static void DotPre(bool landing, out string? __state)
    {
        __state = Kind;
        Kind = landing ? "dotland" : "dot";
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ACE.Server.WorldObjects.Managers.EnchantmentManager), nameof(ACE.Server.WorldObjects.Managers.EnchantmentManager.ApplyDamageTick))]
    public static void DotPost(string? __state) => Kind = __state;

    /// <summary>Which character a point of damage belongs to: a bench character taking it, a bench character (or its pet)
    /// dealing it, else whoever spawned the creature that took it.</summary>
    private static Ctx? OwnerOf(Creature victim, WorldObject attacker)
    {
        if (victim is Player vp && PatchClass.IsBench(vp)) return Bench.For(vp);
        var src = attacker as Player ?? (attacker as ACE.Server.WorldObjects.Pet)?.P_PetOwner;
        if (src != null && PatchClass.IsBench(src)) return Bench.For(src);
        return Bench.Owner(victim.Guid.Full);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DamageHistory), nameof(DamageHistory.Add))]
    public static void OnAdd(DamageHistory __instance, WorldObject attacker, DamageType damageType, uint amount)
    {
        if (attacker == null || __instance.Creature == null) return;
        var owner = OwnerOf(__instance.Creature, attacker);
        if (owner?.Run == null) return;
        using var scope = Bench.Use(owner);
        Bench.OnDamage(__instance.Creature, attacker, damageType, amount, Kind ?? "other");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Creature), "Die", new[] { typeof(DamageHistoryInfo), typeof(DamageHistoryInfo) })]
    public static void OnDie(Creature __instance)
    {
        if (__instance is Player p)
        {
            if (PatchClass.IsBench(p) && Bench.For(p).Run is { } r) r.Deaths++;
            return;
        }
        var owner = Bench.Owner(__instance.Guid.Full);
        if (owner == null) return;
        using var scope = Bench.Use(owner);
        Bench.OnDeath(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "Die", new[] { typeof(DamageHistoryInfo), typeof(DamageHistoryInfo) })]
    public static void OnPlayerDie(Player __instance)
    {
        if (PatchClass.IsBench(__instance) && Bench.For(__instance).Run is { } r) r.Deaths++;
    }
}
