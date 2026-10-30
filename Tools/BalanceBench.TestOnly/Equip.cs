namespace BalanceBench;

/// <summary>
/// Wears what /testchar preset left in the pack: every armour, clothing and jewellery piece, then the style's
/// weapons. Uses the client's own wield request, so wield requirements and coverage rules all apply.
/// </summary>
internal static class Equip
{
    private static bool Occupied(Player p, EquipMask slot) =>
        p.EquippedObjects.Values.Any(e => e.CurrentWieldedLocation.HasValue && (e.CurrentWieldedLocation.Value & slot) != 0);

    private static readonly EquipMask[][] EitherSlots =
    {
        new[] { EquipMask.FingerWearLeft, EquipMask.FingerWearRight },
        new[] { EquipMask.WristWearLeft, EquipMask.WristWearRight },
    };

    private static readonly EquipMask WeaponSlots = EquipMask.MeleeWeapon | EquipMask.MissileWeapon | EquipMask.MissileAmmo | EquipMask.Held | EquipMask.Shield | EquipMask.TwoHanded;

    internal static List<string> Run(Player p, string style)
    {
        var log = new List<string>();
        if (p.CombatMode != CombatMode.NonCombat) p.SetCombatMode(CombatMode.NonCombat);

        var loose = p.GetAllPossessions().Where(i => i.CurrentWieldedLocation == null && i.ValidLocations.HasValue
                                                     && i.ValidLocations.Value != EquipMask.None && !i.IsAbilityCharm).ToList();

        // 1. armour, clothing, jewellery, cloak, aetheria
        foreach (var item in loose.Where(i => (i.ValidLocations!.Value & WeaponSlots) == 0))
        {
            var valid = item.ValidLocations!.Value;
            var slot = valid;
            foreach (var pair in EitherSlots)
                if ((valid & (pair[0] | pair[1])) == (pair[0] | pair[1]))
                    slot = !Occupied(p, pair[0]) ? pair[0] : pair[1];
            if (Occupied(p, slot)) continue;
            Wield(p, item, slot, log);
        }

        // every suit piece must actually be worn (2026-10-23: the T12 shirt was left in the pack - a wield that does not
        // take is retried, and any piece still off is reported)
        for (var pass = 0; pass < 3; pass++)
        {
            var off = p.GetAllPossessions().Where(i => i.CurrentWieldedLocation == null && i.ValidLocations.HasValue && i.ValidLocations.Value != EquipMask.None
                                                       && (i.ValidLocations.Value & WeaponSlots) == 0 && !i.IsAbilityCharm).ToList();
            if (off.Count == 0) break;
            foreach (var item in off)
            {
                var valid = item.ValidLocations!.Value;
                var slot = valid;
                foreach (var pair in EitherSlots)
                    if ((valid & (pair[0] | pair[1])) == (pair[0] | pair[1]))
                        slot = !Occupied(p, pair[0]) ? pair[0] : pair[1];
                Wield(p, item, slot, log);
            }
        }
        var stillOff = p.GetAllPossessions().Where(i => i.CurrentWieldedLocation == null && i.ValidLocations.HasValue && i.ValidLocations.Value != EquipMask.None
                                                        && (i.ValidLocations.Value & WeaponSlots) == 0 && !i.IsAbilityCharm).Select(i => i.Name).ToList();
        if (stillOff.Count > 0) log.Add("NOT WORN: " + string.Join(", ", stillOff));

        // 2. weapons
        var melee = loose.Where(i => i.ItemType == ItemType.MeleeWeapon).ToList();
        var launcher = loose.FirstOrDefault(i => i.ItemType == ItemType.MissileWeapon && (i.ValidLocations!.Value & EquipMask.MissileWeapon) != 0);
        var ammo = loose.FirstOrDefault(i => (i.ValidLocations!.Value & EquipMask.MissileAmmo) != 0);
        var casters = loose.Where(i => i.ItemType == ItemType.Caster).ToList();
        var shield = loose.FirstOrDefault(i => i.ItemType == ItemType.Armor && (i.ValidLocations!.Value & EquipMask.Shield) != 0);

        switch (style)
        {
            case "dw":
                if (melee.Count > 0) Wield(p, melee[0], EquipMask.MeleeWeapon, log);
                if (melee.Count > 1) Wield(p, melee[1], EquipMask.Shield, log);
                break;
            case "shield":
                if (melee.Count > 0) Wield(p, melee[0], EquipMask.MeleeWeapon, log);
                if (shield != null) Wield(p, shield, EquipMask.Shield, log);
                else log.Add("no shield in the pack");
                break;
            case "bow":
                // the ammo must match the launcher (2026-10-24: wielded arrows made a crossbow fail to wield, and the first loose
                // ammo was an arrow): take wrong ammo off first, then wield the launcher and its own ammo
                var inHand = p.EquippedObjects.Values.FirstOrDefault(i => i.ItemType == ItemType.MissileWeapon && (i.ValidLocations!.Value & EquipMask.MissileWeapon) != 0);
                if (launcher == null && inHand != null)
                {
                    if (!p.EquippedObjects.Values.Any(i => ((i.ValidLocations ?? EquipMask.None) & EquipMask.MissileAmmo) != 0 && i.AmmoType == inHand.AmmoType))
                        ammo = loose.FirstOrDefault(i => (i.ValidLocations!.Value & EquipMask.MissileAmmo) != 0 && i.AmmoType == inHand.AmmoType);
                    else
                        ammo = null;
                }
                else if (launcher != null)
                {
                    foreach (var worn in p.EquippedObjects.Values.Where(i => ((i.ValidLocations ?? EquipMask.None) & EquipMask.MissileAmmo) != 0
                                                                             && i.AmmoType != launcher.AmmoType).ToList())
                        p.HandleActionPutItemInContainer(worn.Guid.Full, p.Guid.Full, 0);
                    ammo = loose.FirstOrDefault(i => (i.ValidLocations!.Value & EquipMask.MissileAmmo) != 0 && i.AmmoType == launcher.AmmoType);
                    Wield(p, launcher, EquipMask.MissileWeapon, log);
                    if (ammo == null && !p.EquippedObjects.Values.Any(i => ((i.ValidLocations ?? EquipMask.None) & EquipMask.MissileAmmo) != 0))
                        log.Add($"no {launcher.AmmoType} ammo in the pack");
                }
                if (ammo != null) Wield(p, ammo, EquipMask.MissileAmmo, log);
                break;
            case "war":
            case "void":
                var c = casters.FirstOrDefault(x => (x.Name ?? "").Contains(style == "war" ? "Fire" : "Nether", StringComparison.OrdinalIgnoreCase)) ?? casters.FirstOrDefault();
                if (c != null) Wield(p, c, EquipMask.Held, log);
                break;
            default: // 2h
                var two = melee.FirstOrDefault(i => (i.ValidLocations!.Value & EquipMask.TwoHanded) != 0) ?? melee.FirstOrDefault();
                if (two != null) Wield(p, two, (two.ValidLocations!.Value & EquipMask.TwoHanded) != 0 ? EquipMask.TwoHanded : EquipMask.MeleeWeapon, log);
                break;
        }
        return log;
    }

    private static void Wield(Player p, WorldObject item, EquipMask slot, List<string> log)
    {
        p.HandleActionGetAndWieldItem(item.Guid.Full, slot);
        log.Add($"{item.Name} -> {slot}: {(item.CurrentWieldedLocation != null ? "ok" : "FAILED")}");
    }

    /// <summary>Moves every wielded item back into the pack.</summary>
    internal static int Strip(Player p)
    {
        int n = 0;
        foreach (var item in p.EquippedObjects.Values.ToList())
        {
            p.HandleActionPutItemInContainer(item.Guid.Full, p.Guid.Full, 0);
            n++;
        }
        return n;
    }
}
