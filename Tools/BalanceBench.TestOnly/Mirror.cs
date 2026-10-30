using ACE.Database.Adapter;

namespace BalanceBench;

/// <summary>
/// Copies a real (offline) character onto the bench character: attributes, vitals, skills, the numeric
/// properties that carry augs and ratings, and a fresh copy of every wielded item. Reads the shard DB only;
/// the source character is never changed.
/// </summary>
internal static class Mirror
{
    /// <summary>Properties that must never be copied: identity, position, timers, allegiance, housing, PK, account.</summary>
    private static readonly string[] SkipWords =
    {
        "Allegiance", "House", "Patron", "Monarch", "Fellow", "Squelch", "Timestamp", "Time", "Age", "Creation",
        "Login", "Logoff", "Account", "Pk", "PK", "Vitae", "Death", "Corpse", "Position", "Location", "Guid",
        "Lifestone", "Portal", "Recall", "Gag", "Ban", "Chess", "Squelch", "Title", "Contract", "Kill", "Quest",
        "Bank", "Pyreal", "Coin", "Luminance\u0000", "Experience", "Xp", "XP", "Tick", "Option", "Spell", "Mana",
        "ChannelsActive", "ChannelsAllowed", "Barber", "Heritage", "Gender", "Race", "Appearance", "Palette",
        "Setup", "Clothing", "Icon", "Name", "Shape", "Hair", "Eye", "Nose", "Mouth", "Skin", "Head",
    };

    /// <summary>The ints that carry a character's power: augs, luminance augs, masteries, level, enlightenment.</summary>
    private static bool CopyInt(string n) =>
        n.StartsWith("Augmentation") || n.StartsWith("LumAug") || n.EndsWith("Mastery") || n == "Level" || n == "Enlightenment";

    internal static bool Skippable(string name) => SkipWords.Any(w => name.Contains(w, StringComparison.Ordinal));

    private static ACE.Entity.Models.Biota? Source(string name, out string why)
    {
        var off = PlayerManager.FindByName(name);
        if (off == null) { why = $"no character named {name}"; return null; }
        var online = PlayerManager.GetOnlinePlayer(off.Guid);
        if (online != null) { why = ""; return online.Biota; }
        var db = DatabaseManager.Shard.BaseDatabase.GetBiota(off.Guid.Full);
        if (db == null) { why = "biota not found"; return null; }
        why = "";
        return BiotaConverter.ConvertToEntityBiota(db);
    }

    internal static List<string> Dump(string name, string filter)
    {
        var o = new List<string>();
        var b = Source(name, out var why);
        if (b == null) { o.Add(why); return o; }
        bool Want(string n) => filter == "" || n.Contains(filter, StringComparison.OrdinalIgnoreCase);
        foreach (var kv in b.PropertiesInt ?? new Dictionary<PropertyInt, int>())
            if (Want(kv.Key.ToString())) o.Add($"int {(int)kv.Key} {kv.Key} = {kv.Value}{(Skippable(kv.Key.ToString()) ? "  (skip)" : "")}");
        foreach (var kv in b.PropertiesInt64 ?? new Dictionary<PropertyInt64, long>())
            if (Want(kv.Key.ToString())) o.Add($"int64 {(int)kv.Key} {kv.Key} = {kv.Value}{(Skippable(kv.Key.ToString()) ? "  (skip)" : "")}");
        foreach (var kv in b.PropertiesFloat ?? new Dictionary<PropertyFloat, double>())
            if (Want(kv.Key.ToString())) o.Add($"float {(int)kv.Key} {kv.Key} = {kv.Value}{(Skippable(kv.Key.ToString()) ? "  (skip)" : "")}");
        foreach (var kv in b.PropertiesBool ?? new Dictionary<PropertyBool, bool>())
            if (Want(kv.Key.ToString())) o.Add($"bool {(int)kv.Key} {kv.Key} = {kv.Value}{(Skippable(kv.Key.ToString()) ? "  (skip)" : "")}");
        foreach (var kv in b.PropertiesAttribute ?? new Dictionary<PropertyAttribute, ACE.Entity.Models.PropertiesAttribute>())
            o.Add($"attr {kv.Key} init {kv.Value.InitLevel} ranks {kv.Value.LevelFromCP}");
        foreach (var kv in b.PropertiesAttribute2nd ?? new Dictionary<PropertyAttribute2nd, ACE.Entity.Models.PropertiesAttribute2nd>())
            o.Add($"vital {kv.Key} init {kv.Value.InitLevel} ranks {kv.Value.LevelFromCP}");
        var pos = DatabaseManager.Shard.BaseDatabase.GetPossessedBiotasInParallel(b.Id);
        foreach (var w in pos.WieldedItems)
        {
            var e = BiotaConverter.ConvertToEntityBiota(w);
            var nm = e.PropertiesString != null && e.PropertiesString.TryGetValue(PropertyString.Name, out var n) ? n : "?";
            var loc = e.PropertiesInt != null && e.PropertiesInt.TryGetValue(PropertyInt.CurrentWieldedLocation, out var l) ? (EquipMask)l : EquipMask.None;
            o.Add($"wielded {e.WeenieClassId} {nm} [{loc}]");
        }
        return o;
    }

    /// <summary>
    /// Copies from a real character. what = stats | body | weapons | all. body = every worn piece that is not a
    /// weapon, shield or ammo; weapons = the style's best weapons from anywhere in their possessions.
    /// </summary>
    internal static List<string> Apply(Player p, string name, string what, string style)
    {
        var o = new List<string>();
        var b = Source(name, out var why);
        if (b == null) { o.Add(why); return o; }
        if (what is "stats" or "all") CopyStats(p, b, o);
        if (what is "body" or "all") CopyGear(p, b, o);
        if (what is "weapons" or "all") CopyWeapons(p, b, style, o);
        p.SaveBiotaToDatabase();
        return o;
    }

    private const EquipMask HandSlots = EquipMask.MeleeWeapon | EquipMask.MissileWeapon | EquipMask.Held | EquipMask.TwoHanded | EquipMask.Shield | EquipMask.MissileAmmo;

    private static int GetI(ACE.Entity.Models.Biota e, PropertyInt k) => e.PropertiesInt != null && e.PropertiesInt.TryGetValue(k, out var v) ? v : 0;
    private static int GetI(ACE.Entity.Models.Biota e, int k) => GetI(e, (PropertyInt)k);
    private static string Nm(ACE.Entity.Models.Biota e) => e.PropertiesString != null && e.PropertiesString.TryGetValue(PropertyString.Name, out var n) ? n : "?";

    internal static List<string> Prefer = new();

    private static void CopyWeapons(Player p, ACE.Entity.Models.Biota b, string style, List<string> o)
    {
        if (p.CombatMode != CombatMode.NonCombat) p.SetCombatMode(CombatMode.NonCombat);
        foreach (var item in p.EquippedObjects.Values.Where(i => i.CurrentWieldedLocation.HasValue && (i.CurrentWieldedLocation.Value & HandSlots) != 0).ToList())
            if (p.TryDequipObjectWithNetworking(item.Guid, out var wo, Player.DequipObjectAction.ConsumeItem) && wo != null) wo.Destroy();

        var pos = DatabaseManager.Shard.BaseDatabase.GetPossessedBiotasInParallel(b.Id);
        var all = pos.WieldedItems.Concat(pos.Inventory).Select(x => BiotaConverter.ConvertToEntityBiota(x)).ToList();
        // named weapons first (in the order given), then tier, quality - so a style keeps the same weapon from run to run
        int Rank(ACE.Entity.Models.Biota x) { var i = Prefer.FindIndex(n => string.Equals(n, Nm(x), StringComparison.OrdinalIgnoreCase)); return i < 0 ? int.MaxValue : i; }
        IEnumerable<ACE.Entity.Models.Biota> Best(IEnumerable<ACE.Entity.Models.Biota> xs) => xs.OrderBy(Rank).ThenByDescending(x => GetI(x, 9061)).ThenByDescending(x => GetI(x, 9060)).ThenBy(x => x.Id);
        bool Melee(ACE.Entity.Models.Biota x, params int[] skills) => x.WeenieType == WeenieType.MeleeWeapon && skills.Contains(GetI(x, PropertyInt.WeaponSkill));
        bool Valid(ACE.Entity.Models.Biota x, EquipMask m) => ((EquipMask)GetI(x, PropertyInt.ValidLocations) & m) != 0;

        var picks = new List<(ACE.Entity.Models.Biota src, EquipMask loc)>();
        switch (style)
        {
            case "2h":
                foreach (var x in Best(all.Where(x => Melee(x, 41))).Take(1)) picks.Add((x, EquipMask.TwoHanded));
                break;
            case "dw":
                var one = Best(all.Where(x => Melee(x, 44, 45, 46))).ToList();
                if (one.Count > 0) picks.Add((one[0], EquipMask.MeleeWeapon));
                var second = one.Skip(1).FirstOrDefault(x => Nm(x) != Nm(one[0])) ?? one.Skip(1).FirstOrDefault();
                if (second != null) picks.Add((second, EquipMask.Shield));
                break;
            case "shield":
                foreach (var x in Best(all.Where(x => Melee(x, 44, 45, 46))).Take(1)) picks.Add((x, EquipMask.MeleeWeapon));
                foreach (var x in Best(all.Where(x => x.WeenieType != WeenieType.MeleeWeapon && Valid(x, EquipMask.Shield))).Take(1)) picks.Add((x, EquipMask.Shield));
                break;
            case "bow":
                {
                    // the ammo first (largest stack), then the best launcher that fires it - a crossbow with arrows never shoots
                    // a launcher named in Prefer picks the ammo it fires (2026-10-24: a player carrying arrows, quarrels and darts)
                    var named = Best(all.Where(x => x.WeenieType == WeenieType.MissileLauncher && Rank(x) != int.MaxValue)).FirstOrDefault();
                    var ammo = all.Where(x => Valid(x, EquipMask.MissileAmmo) && (named == null || (GetI(x, PropertyInt.AmmoType) & GetI(named, PropertyInt.AmmoType)) != 0))
                                  .OrderByDescending(x => GetI(x, PropertyInt.StackSize)).FirstOrDefault();
                    var ammoType = ammo != null ? GetI(ammo, PropertyInt.AmmoType) : 0;
                    var launchers = all.Where(x => x.WeenieType == WeenieType.MissileLauncher && (ammoType == 0 || (GetI(x, PropertyInt.AmmoType) & ammoType) != 0));
                    foreach (var x in Best(launchers).Take(1)) picks.Add((x, EquipMask.MissileWeapon));
                    if (ammo != null) picks.Add((ammo, EquipMask.MissileAmmo));
                }
                break;
            case "war":
                foreach (var x in Best(all.Where(x => x.WeenieType == WeenieType.Caster && GetI(x, PropertyInt.DamageType) != 1024 && GetI(x, 9061) > 0)).Take(1)) picks.Add((x, EquipMask.Held));
                break;
            case "void":
                foreach (var x in Best(all.Where(x => x.WeenieType == WeenieType.Caster && GetI(x, PropertyInt.DamageType) == 1024)).Take(1)) picks.Add((x, EquipMask.Held));
                break;
        }
        if (picks.Count == 0) { o.Add($"weapons: {Nm(b)} has nothing for {style}"); return; }
        foreach (var (src, loc) in picks)
        {
            var wo = Clone(src);
            if (wo == null || !p.TryCreateInInventoryWithNetworking(wo)) { o.Add($"weapons: could not create {Nm(src)}"); wo?.Destroy(); continue; }
            p.HandleActionGetAndWieldItem(wo.Guid.Full, loc);
            o.Add($"weapons: {wo.Name} T{GetI(src, 9061)} q{GetI(src, 9060)} -> {loc} {(wo.CurrentWieldedLocation != null ? "ok" : "FAILED")}");
        }
    }

    /// <summary>A copy of one saved item, by its database id, into the pack (2026-10-24: a tester's bow they no longer carry).</summary>
    internal static string CloneItem(Player p, uint id)
    {
        var db = DatabaseManager.Shard.BaseDatabase.GetBiota(id);
        if (db == null) return $"cloneitem: no item {id} in the database";
        var wo = Clone(BiotaConverter.ConvertToEntityBiota(db));
        if (wo == null || !p.TryCreateInInventoryWithNetworking(wo)) { wo?.Destroy(); return $"cloneitem: could not create item {id}"; }
        return $"cloneitem: {wo.Name} (copy of {id}) in the pack";
    }

    private static WorldObject? Clone(ACE.Entity.Models.Biota e)
    {
        e.Id = GuidManager.NewDynamicGuid().Full;
        e.PropertiesIID?.Clear();
        e.PropertiesInt?.Remove(PropertyInt.CurrentWieldedLocation);
        e.PropertiesPosition?.Clear();
        return WorldObjectFactory.CreateWorldObject(e);
    }

    private static void CopyStats(Player p, ACE.Entity.Models.Biota b, List<string> o)
    {
        int n = 0;
        foreach (var kv in b.PropertiesInt ?? new Dictionary<PropertyInt, int>())
        {
            if (!CopyInt(kv.Key.ToString())) continue;
            p.SetProperty(kv.Key, kv.Value); n++;
            p.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(p, kv.Key, kv.Value));
        }
        foreach (var kv in b.PropertiesInt64 ?? new Dictionary<PropertyInt64, long>())
        {
            if (!kv.Key.ToString().StartsWith("LumAug")) continue;
            p.SetProperty(kv.Key, kv.Value); n++;
        }
        foreach (var kv in b.PropertiesBool ?? new Dictionary<PropertyBool, bool>())
        {
            var nm = kv.Key.ToString();
            if (!(nm.StartsWith("Has") && (nm.EndsWith("Charm") || nm.Contains("Mastery")))) continue;
            p.SetProperty(kv.Key, kv.Value); n++;
        }
        o.Add($"stats: {n} numeric properties copied");

        foreach (var kv in b.PropertiesAttribute ?? new Dictionary<PropertyAttribute, ACE.Entity.Models.PropertiesAttribute>())
        {
            if (!p.Attributes.TryGetValue(kv.Key, out var a)) continue;
            a.StartingValue = kv.Value.InitLevel;
            p.SetAttributeRank(a, kv.Value.LevelFromCP);
            p.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(p, a));
        }
        foreach (var kv in b.PropertiesAttribute2nd ?? new Dictionary<PropertyAttribute2nd, ACE.Entity.Models.PropertiesAttribute2nd>())
        {
            if (!p.Vitals.TryGetValue(kv.Key, out var v)) continue;
            v.StartingValue = kv.Value.InitLevel;
            v.Ranks = kv.Value.LevelFromCP;
            v.ExperienceSpent = kv.Value.CPSpent;
            v.Current = v.MaxValue;
            p.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(p, v));
        }
        o.Add($"stats: level {p.Level}, str {p.Strength.Current} coo {p.Coordination.Current} qui {p.Quickness.Current} " +
              $"hp {p.Health.MaxValue} st {p.Stamina.MaxValue} mana {p.Mana.MaxValue}");
    }

    private static void CopyGear(Player p, ACE.Entity.Models.Biota b, List<string> o)
    {
        if (p.CombatMode != CombatMode.NonCombat) p.SetCombatMode(CombatMode.NonCombat);

        // the bench character's own wielded items go; mirrored gear is recreated each time
        int gone = 0;
        foreach (var item in p.EquippedObjects.Values.ToList())
            if (p.TryDequipObjectWithNetworking(item.Guid, out var wo, Player.DequipObjectAction.ConsumeItem) && wo != null)
            { wo.Destroy(); gone++; }
        o.Add($"gear: removed {gone} wielded items");

        var pos = DatabaseManager.Shard.BaseDatabase.GetPossessedBiotasInParallel(b.Id);
        var made = new List<(WorldObject wo, EquipMask loc)>();
        foreach (var w in pos.WieldedItems)
        {
            var e = BiotaConverter.ConvertToEntityBiota(w);
            var loc = e.PropertiesInt != null && e.PropertiesInt.TryGetValue(PropertyInt.CurrentWieldedLocation, out var l) ? (EquipMask)l : EquipMask.None;
            if ((loc & HandSlots) != 0) continue;
            var wo = Clone(e);
            if (wo == null) { o.Add($"gear: could not create {e.WeenieClassId}"); continue; }
            if (!p.TryCreateInInventoryWithNetworking(wo)) { o.Add($"gear: no room for {wo.Name}"); wo.Destroy(); continue; }
            made.Add((wo, loc));
        }

        // body first, then the weapon, then the off hand / ammo
        int Order(EquipMask m) => (m & (EquipMask.MeleeWeapon | EquipMask.MissileWeapon | EquipMask.Held | EquipMask.TwoHanded)) != 0 ? 1
                                : (m & (EquipMask.Shield | EquipMask.MissileAmmo)) != 0 ? 2 : 0;
        int ok = 0;
        foreach (var (wo, loc) in made.OrderBy(m => Order(m.loc)))
        {
            p.HandleActionGetAndWieldItem(wo.Guid.Full, loc);
            if (wo.CurrentWieldedLocation != null) ok++;
            else o.Add($"gear: could not wield {wo.Name} [{loc}]");
        }
        o.Add($"gear: {ok} of {made.Count} items wielded");
    }
}
