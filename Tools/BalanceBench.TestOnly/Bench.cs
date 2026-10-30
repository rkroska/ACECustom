using ACE.Server.Entity.Actions;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;

namespace BalanceBench;

/// <summary>A monster the bench spawned, with its timings.</summary>
internal class Tracked
{
    public uint Guid;
    public uint Wcid;
    public string Name = "";
    public string Rank = "";
    public string Archetype = "";
    public uint MaxHp;
    public DateTime Spawned;
    public DateTime? FirstHit;
    public DateTime? Died;
    public int Hits;                      // damage events from the bench (any kind: swing, arrow, spell, DoT tick, splash)
    public Position? DeathPos;            // where the corpse lies - the loot walk (2026-10-25)
}

/// <summary>Running totals for one trial.</summary>
internal class Stats
{
    public DateTime Start = DateTime.UtcNow;
    public DateTime? FirstHit;
    public DateTime? LastKill;
    public Dictionary<string, double> DealtByKind = new();
    public Dictionary<string, int> HitsByKind = new();
    public readonly Dictionary<uint, int> LabDotTicks = new();
    public readonly Dictionary<int, int> LabDotGenerations = new();
    public readonly Dictionary<uint, LabMonster> LabMonsters = new();
    public int Crits;
    public double DealtToOthers;          // damage on monsters the bench did not spawn
    public Dictionary<string, double> TakenByKind = new();
    public Dictionary<string, int> TakenHits = new();
    public double TakenFromTracked;
    public int Heals, StamRefills, ManaRefills, Deaths;
    public double SustainSeconds;         // real-sustain mode: seconds spent casting heals / revitalize / mana boost, not attacking
    public int Swings, Casts;
    public double HealedHp;
    // raw hit sizes before the health cap (DamageEvent.Damage), bench attacks on tracked monsters only
    public int RawHits, KillShots;          // KillShots: a single hit at or above the monster's MAX health
    public double RawSum, RawMax;
    public int OneHitKills;                 // monsters that died to the first damage event they took (any kind)
}

/// <summary>One bench character's whole state (2026-10-23: several characters bench at once, one tier each - nothing
/// a trial reads or writes may be shared between them).</summary>
internal sealed class Ctx
{
    public string Name = "";
    public readonly Dictionary<uint, Tracked> Spawns = new();
    public Stats? Run;
    public string RunLabel = "";
    public bool Fighting;
    public float Power = 1.0f;
    public uint SpellId;
    public uint SpellId2;            // filler cast when every target already carries the DoT (0 = wait)
    public double DotRefresh;        // > 0: SpellId is a DoT; recast on a target only after this many seconds
    public readonly Dictionary<uint, DateTime> LastCast = new();
    public int DotMax;               // > 0: cast the DoT on at most this many different monsters (the rest is the charm's spread)
    public bool LabCatalyst;
    public bool LabCombo;
    public readonly HashSet<uint> LabComboReleased = new();
    public uint DotSpell3;
    public readonly Dictionary<uint, DateTime> LastCast3 = new();
    public uint DotSpell2;           // a SECOND DoT kept on every target too (2026-10-25: Grumpy's void - Corrosion + Corruption carry the kill)
    public readonly Dictionary<uint, DateTime> LastCast2 = new();
    public int Seed;
    public double HealAt = 0.35;
    public DateTime? TrialDeadline;
    public Dictionary<string, object?>? TrialInfo;
    public bool Swap;
    public int Swaps;
    public readonly Queue<(uint Guid, EquipMask Slot)> WieldQueue = new();
    public (uint Guid, EquipMask Slot, int Tries)? WieldCheck;   // the last wield, confirmed on the next free tick
    public List<(ItemType Type, Skill Skill, EquipMask Slot)> SwapKinds = new();
    public DateTime SwapUntil;
    public DateTime LastHit, LastIssue;
    public float ShieldMult = 1f;    // stand-in for the Shield Arc "damage" setting until the server is rebuilt
    public readonly Dictionary<string, int> UseDone = new();
    // real sustain (owner 2026-10-25, "more accurate testing"): no free refills - the character stops attacking and casts a real
    // Heal / Revitalize / Mana Boost Self (its own life augs and healing ratings apply) for CastSeconds, and keeps its vitals
    // and vitae from one camp to the next
    public bool RealSustain;
    public double CastSeconds = 2.5;
    public DateTime BusyUntil;
    public string SwapNote = "";        // the last reason a swap was skipped (2026-10-25 twin-claw debug)
}

/// <summary>
/// The bench itself: spawns zone packs, drives the bench character's attacks from the server,
/// logs every point of damage dealt and taken, and writes a summary when a trial ends.
/// Everything runs on the world thread.
/// </summary>
internal static class Bench
{
    // ---- per-character state (see Ctx). Every entry point - the tick, an inbox command, a hook - selects the character
    // it acts for with Use(); the members below read and write THAT character's state only.
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Ctx> All = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Ctx Idle = new() { Name = "" };     // no character selected: an empty state nothing is booked to
    [ThreadStatic] private static Ctx? cur;
    internal static Ctx C => cur ?? Idle;
    internal static bool Selected => cur != null;

    internal static Ctx For(Player p) => All.GetOrAdd(p.Name.TrimStart('+'), n => new Ctx { Name = n });

    internal readonly struct Scope : IDisposable
    {
        private readonly Ctx? prev;
        internal Scope(Ctx? next) { prev = cur; cur = next; }
        public void Dispose() => cur = prev;
    }
    internal static Scope Use(Ctx? c) => new(c);
    internal static Scope Use(Player? p) => new(p != null && PatchClass.IsBench(p) ? For(p) : null);

    /// <summary>The character whose spawn this creature is, if any.</summary>
    internal static Ctx? Owner(uint guid) => All.Values.FirstOrDefault(c => c.Spawns.ContainsKey(guid));

    internal static Dictionary<uint, Tracked> Spawns => C.Spawns;
    internal static Stats? Run { get => C.Run; set => C.Run = value; }
    internal static string RunLabel { get => C.RunLabel; set => C.RunLabel = value; }
    internal static bool Fighting { get => C.Fighting; set => C.Fighting = value; }
    internal static float Power { get => C.Power; set => C.Power = value; }
    internal static uint SpellId { get => C.SpellId; set => C.SpellId = value; }
    internal static uint SpellId2 { get => C.SpellId2; set => C.SpellId2 = value; }
    internal static double DotRefresh { get => C.DotRefresh; set => C.DotRefresh = value; }
    internal static Dictionary<uint, DateTime> LastCast => C.LastCast;
    internal static int DotMax { get => C.DotMax; set => C.DotMax = value; }
    internal static uint DotSpell2 { get => C.DotSpell2; set => C.DotSpell2 = value; }
    internal static int Seed { get => C.Seed; set => C.Seed = value; }
    internal static double HealAt { get => C.HealAt; set => C.HealAt = value; }
    internal static bool RealSustain { get => C.RealSustain; set => C.RealSustain = value; }
    internal static double CastSeconds { get => C.CastSeconds; set => C.CastSeconds = value; }
    private static DateTime? trialDeadline { get => C.TrialDeadline; set => C.TrialDeadline = value; }
    private static Dictionary<string, object?>? trialInfo { get => C.TrialInfo; set => C.TrialInfo = value; }
    internal static bool Swap { get => C.Swap; set => C.Swap = value; }
    internal static int Swaps { get => C.Swaps; set => C.Swaps = value; }
    private static Queue<(uint Guid, EquipMask Slot)> wieldQueue => C.WieldQueue;
    private static (uint Guid, EquipMask Slot, int Tries)? wieldCheck { get => C.WieldCheck; set => C.WieldCheck = value; }
    private static List<(ItemType Type, Skill Skill, EquipMask Slot)> swapKinds { get => C.SwapKinds; set => C.SwapKinds = value; }
    private static DateTime swapUntil { get => C.SwapUntil; set => C.SwapUntil = value; }
    internal static DateTime LastHit { get => C.LastHit; set => C.LastHit = value; }
    internal static DateTime LastIssue { get => C.LastIssue; set => C.LastIssue = value; }
    [ThreadStatic] private static int OutOfZone;

    // fight driver

    // trial

    private static System.Threading.Timer? timer;
    private static int queued;

    internal static void Start()
    {
        timer = new System.Threading.Timer(_ =>
        {
            if (Interlocked.Exchange(ref queued, 1) == 1) return;
            WorldManager.EnqueueAction(new ActionEventDelegate(ActionType.AdminCommands_HandleActionQueryHouse, () =>
            {
                try { Tick(); }
                catch (Exception ex) { Mod.Log($"tick failed: {ex}", ModManager.LogLevel.Warn); }
                finally { Interlocked.Exchange(ref queued, 0); }
            }));
        }, null, 1000, 250);
    }

    internal static void Stop()
    {
        timer?.Dispose();
        timer = null;
        foreach (var c in All.Values) c.Fighting = false;
    }

    // ---------------------------------------------------------------- spawning

    /// <summary>Wcids of the governing zone at the player's spot, filtered to one rank.</summary>
    internal static List<uint> ZoneWcids(Player p, ZcRank rank)
    {
        var area = ZoneControlManager.ResolveWinnerForLocation((ushort)(p.Location.Landblock), p.Location.Variation ?? 0);
        if (area?.Profile?.WcidOverrides == null) return new();
        var list = new List<uint>();
        foreach (var wcid in area.Profile.WcidOverrides.Keys)
        {
            var w = DatabaseManager.World.GetCachedWeenie(wcid);
            if (w == null || w.WeenieType != WeenieType.Creature) continue;
            if (ZoneControlManager.ResolveRankForWcid(area, wcid).Rank == rank) list.Add(wcid);
        }
        return list;
    }

    internal static string ZoneName(Player p) =>
        ZoneControlManager.ResolveWinnerForLocation((ushort)p.Location.Landblock, p.Location.Variation ?? 0)?.Name ?? "(none)";

    /// <summary>
    /// Spawns count monsters of the rank. Layout: ring (circle round the player at dist), arc (120 degree arc in
    /// front, dist to dist+8), line (in front at dist). wcid 0 = random from the zone list.
    /// </summary>
    internal static List<Tracked> SpawnPack(Player p, int count, ZcRank rank, string layout, float dist, uint wcid = 0, List<uint>? picks = null)
    {
        // picks: an explicit monster per slot (a real camp: its leader, then its regulars), overriding count and rank
        if (picks != null && picks.Count > 0) count = picks.Count;
        var pool = picks != null && picks.Count > 0 ? picks : wcid != 0 ? new List<uint> { wcid } : ZoneWcids(p, rank);
        var made = new List<Tracked>();
        if (pool.Count == 0) return made;

        var loc = p.Location;
        var heading = Math.Atan2(2 * loc.RotationW * loc.RotationZ, 1 - 2 * loc.RotationZ * loc.RotationZ);
        var rng = Seed != 0 ? new Random(Seed) : new Random();

        for (int i = 0; i < count; i++)
        {
            double ang, r;
            switch (layout)
            {
                case "arc":
                    ang = heading + (count == 1 ? 0 : (-Math.PI / 3 + (2 * Math.PI / 3) * i / (count - 1)));
                    r = dist + (i % 3) * 4;
                    break;
                case "line":
                    ang = heading + (i - (count - 1) / 2.0) * 0.12;
                    r = dist;
                    break;
                case "far":
                    // a tight pack (4 m ring) centred dist metres in front: melee has to walk in, ranged can open at once
                    ang = 2 * Math.PI * i / Math.Max(1, count);
                    r = 4;
                    break;
                default: // ring
                    ang = heading + 2 * Math.PI * i / Math.Max(1, count);
                    r = dist;
                    break;
            }
            var dx = (float)(-Math.Sin(ang) * r);
            var dy = (float)(Math.Cos(ang) * r);
            if (layout == "far")
            {
                dx += (float)(-Math.Sin(heading) * dist);
                dy += (float)(Math.Cos(heading) * dist);
            }
            var pos = new Position(loc.LandblockId.Raw, loc.PositionX + dx, loc.PositionY + dy, loc.PositionZ + 0.5f, 0f, 0f, loc.RotationZ, loc.RotationW, false, loc.Variation);
            pos.SetLandblock();
            pos.LandblockId = new LandblockId(pos.GetCell());

            // the spawn point must lie in the character's own zone: one that lands outside it gets no zone scaling and is a
            // plain retail monster (2026-10-23: camps 54 m ahead of a character facing out of Tou Tou spawned at 110k HP)
            var here = ZoneControlManager.ResolveWinnerForLocation((ushort)pos.Landblock, pos.Variation ?? 0)?.Name;
            if (here == null || here != ZoneName(p)) { OutOfZone++; continue; }

            var pick = picks != null && picks.Count > 0 ? picks[i] : pool[rng.Next(pool.Count)];
            if (WorldObjectFactory.CreateNewWorldObject(pick) is not Creature c) continue;
            c.Location = pos;
            ZoneSpawnScaler.ApplyToSpawn(c);
            if (!c.EnterWorld()) { c.Destroy(); continue; }

            var t = new Tracked
            {
                Guid = c.Guid.Full, Wcid = pick, Name = c.Name, Rank = (picks != null ? (ZcRank?)RankOf(c) : rank)?.ToString() ?? "",
                Archetype = c.ZcArchetype.ToString(),
                MaxHp = c.Health.MaxValue, Spawned = DateTime.UtcNow,
            };
            Spawns[t.Guid] = t;
            made.Add(t);
        }
        return made;
    }

    private static ZcRank RankOf(Creature c)
    {
        var area = ZoneControlManager.ResolveWinnerForLocation((ushort)c.Location.Landblock, c.Location.Variation ?? 0);
        return area != null ? ZoneControlManager.ResolveRankForWcid(area, c.WeenieClassId).Rank : ZcRank.Regular;
    }

    internal static Creature? Live(uint guid, Player p)
    {
        var wo = p.CurrentLandblock?.GetObject(new ObjectGuid(guid));
        return wo as Creature;
    }

    /// <summary>Destroys tracked test spawns only. Never deletes unrelated world creatures.</summary>
    internal static int Clear(Player p, float radius)
    {
        int n = 0;
        foreach (var t in Spawns.Values.ToList())
        {
            var c = Live(t.Guid, p);
            if (c != null) { c.Destroy(); n++; }
        }
        Spawns.Clear();
        return n;
    }

    // ---------------------------------------------------------------- driver

    internal static void Refill(Player p)
    {
        // a death's vitae shrinks all three vitals for the next fights - a bench fight always starts without it (2026-10-23:
        // the T13-T15 characters fought at half health after a run of deaths)
        if (p.Vitae < 1.0f)
            p.EnchantmentManager.RemoveVitae();
        p.UpdateVitalDelta(p.Health, (int)(p.Health.MaxValue - p.Health.Current));
        p.UpdateVitalDelta(p.Stamina, (int)(p.Stamina.MaxValue - p.Stamina.Current));
        p.UpdateVitalDelta(p.Mana, (int)(p.Mana.MaxValue - p.Mana.Current));
    }

    private static void Tick()
    {
        foreach (var p in PlayerManager.GetAllOnline().Where(PatchClass.IsBench).ToList())
        {
            using var scope = Use(For(p));
            try { TickOne(p); }
            catch (Exception ex) { Mod.Log($"tick {p.Name} failed: {ex}", ModManager.LogLevel.Warn); }
        }
    }

    private static void TickOne(Player p)
    {

        if (Run != null && p.IsAlive && RealSustain)
            SustainTick(p);
        if (trialDeadline != null)
        {
            var alive = Spawns.Values.Count(t => t.Died == null);
            if (alive == 0 || DateTime.UtcNow > trialDeadline || !p.IsAlive)
            {
                FinishTrial(p, alive == 0 ? "cleared" : !p.IsAlive ? "died" : "timeout");
                return;
            }
        }

        if (!Fighting || !p.IsAlive || p.Teleporting) return;
        if (RealSustain && DateTime.UtcNow < C.BusyUntil) return;   // casting a heal / revitalize / mana boost: not attacking
        Drive(p);
    }

    private const uint HealSelfId = 4311, RevitalizeSelfId = 4321, ManaBoostSelfId = 4315;   // the Incantation self spells

    /// <summary>Real sustain: below the heal line (or 20 pct stamina / mana) the character stops attacking and casts the real
    /// self spell - the server's own boost math, life augs and healing ratings included - and stays busy for CastSeconds.
    /// Health first, then stamina, then mana: one cast at a time, as a player would.</summary>
    private static void SustainTick(Player p)
    {
        var now = DateTime.UtcNow;
        if (now < C.BusyUntil || Run == null) return;
        uint spellId = p.Health.Current < p.Health.MaxValue * HealAt ? HealSelfId
            : p.Stamina.Current < p.Stamina.MaxValue * 0.2 ? RevitalizeSelfId
            : p.Mana.Current < p.Mana.MaxValue * 0.2 ? ManaBoostSelfId : 0;
        if (spellId == 0) return;
        var spell = new Spell(spellId);
        if (spell.NotFound) return;
        p.HandleActionCancelAttack();
        var hp = p.Health.Current;
        p.TryCastSpell(spell, p, null, null, false, false, false);
        C.BusyUntil = now.AddSeconds(CastSeconds);
        Run.SustainSeconds += CastSeconds;
        if (spellId == HealSelfId) { Run.Heals++; Run.HealedHp += Math.Max(0, (double)p.Health.Current - hp); }
        else if (spellId == RevitalizeSelfId) Run.StamRefills++;
        else Run.ManaRefills++;
    }

    /// <summary>Metres walked to open every corpse once: nearest corpse first, from where the character stands now.</summary>
    private static double LootMeters(Player p)
    {
        var left = Spawns.Values.Where(t => t.DeathPos != null).Select(t => t.DeathPos!).ToList();
        var at = p.Location;
        double m = 0;
        while (left.Count > 0)
        {
            var next = left.OrderBy(x => x.DistanceTo(at)).First();
            m += Math.Max(0, next.DistanceTo(at) - 2.0);    // corpses open from about 2 m away
            at = next;
            left.Remove(next);
        }
        return Math.Round(m, 1);
    }

    // ---------------------------------------------------------------- element swap (owner 2026-10-22: "if i were really
    // playing i would be swapping to diff elements based on what i'm fighting")
    // what the hands held when swapping started (kind + slot), so an empty hand is refilled with the right kind
    private static readonly (ResistanceType R, DamageType D)[] SwapElements =
    {
        (ResistanceType.Slash, DamageType.Slash), (ResistanceType.Pierce, DamageType.Pierce), (ResistanceType.Bludgeon, DamageType.Bludgeon),
        (ResistanceType.Fire, DamageType.Fire), (ResistanceType.Cold, DamageType.Cold), (ResistanceType.Acid, DamageType.Acid),
        (ResistanceType.Electric, DamageType.Electric),
    };
    // the incantation streak of each element, for a war wand of that element
    private static readonly Dictionary<DamageType, uint> StreakOf = new()
    {
        [DamageType.Electric] = 4452, [DamageType.Fire] = 4440, [DamageType.Acid] = 4432, [DamageType.Cold] = 4448,
        [DamageType.Slash] = 4458, [DamageType.Pierce] = 4444, [DamageType.Bludgeon] = 4456,
    };

    /// <summary>The element this monster takes the most from (its weakness), read the way a hit reads it.</summary>
    internal static DamageType WeaknessOf(Creature c)
        => SwapElements.OrderByDescending(e => c.GetResistanceMod(e.R)).First().D;

    private static readonly EquipMask[] WeaponSlots = { EquipMask.TwoHanded, EquipMask.MeleeWeapon, EquipMask.Shield, EquipMask.MissileWeapon, EquipMask.Held };

    /// <summary>With Swap on: when the weapons in hand are not the target's weakness and the pack holds the same kind of
    /// weapon in that element, put the old ones away and queue the new ones (the queue is worked off at the top of Drive,
    /// one wield per free moment, so the swap costs real time). True while a swap is under way.</summary>
    private static bool TrySwap(Player p, Creature target)
    {
        if (!Swap || target == null) return false;
        var want = WeaknessOf(target);
        var held = WeaponSlots.Select(slot => (slot, wo: p.EquippedObjects.Values.FirstOrDefault(o => o.CurrentWieldedLocation == slot)))
            .Where(x => x.wo != null && (x.wo.ItemType == ItemType.MeleeWeapon || x.wo.ItemType == ItemType.MissileWeapon || x.wo.ItemType == ItemType.Caster)
                        && ((x.wo.ValidLocations ?? EquipMask.None) & EquipMask.MissileAmmo) == 0)
            .ToList();
        if (held.Count > 0)
            swapKinds = held.Select(x => (x.wo!.ItemType, x.wo.WeaponSkill, x.slot)).ToList();
        var loose = p.GetAllPossessions().Where(o => o.CurrentWieldedLocation == null && o.W_DamageType == want).ToList();
        // a hand left empty (a wield that never took): refill it with the target's element of the kind it held
        var empty = swapKinds.Where(k => !held.Any(h => h.slot == k.Slot)).ToList();
        if (empty.Count > 0)
        {
            foreach (var k in empty)
            {
                var fill = loose.FirstOrDefault(o => o.ItemType == k.Type && o.WeaponSkill == k.Skill)
                           ?? p.GetAllPossessions().FirstOrDefault(o => o.CurrentWieldedLocation == null && o.ItemType == k.Type && o.WeaponSkill == k.Skill);
                if (fill != null) wieldQueue.Enqueue((fill.Guid.Full, k.Slot));
            }
            swapUntil = DateTime.UtcNow.AddSeconds(0.3);
            return wieldQueue.Count > 0;
        }
        if (held.Count == 0) { C.SwapNote = $"nothing held (want {want})"; return false; }
        if (held.All(x => x.wo!.W_DamageType == want)) { C.SwapNote = $"already {want}"; return false; }
        var plan = new List<(WorldObject Old, WorldObject New, EquipMask Slot)>();
        foreach (var (slot, wo) in held)
        {
            if (wo!.W_DamageType == want) continue;
            var next = loose.FirstOrDefault(o => o.ItemType == wo.ItemType && o.WeaponSkill == wo.WeaponSkill && !plan.Any(x => x.New == o));
            if (next == null)
            {
                // no full set in this element - keep fighting with what is in hand
                C.SwapNote = $"no loose {wo.ItemType}/{wo.WeaponSkill} in {want} for {slot} (holding {string.Join(" + ", held.Select(h => $"{h.wo!.Name}:{h.wo.W_DamageType}"))}; " +
                             $"loose {want}: {string.Join(", ", loose.Select(o => $"{o.Name}:{o.ItemType}/{o.WeaponSkill}"))})";
                return false;
            }
            plan.Add((wo, next, slot));
        }
        // stop the swing in progress first: an attack still queued when the weapon leaves the hand lands as a punch
        // (2026-10-23: the first 1-2 hits of a camp were bare-handed bludgeon)
        if (p.Attacking || p.MeleeTarget != null || p.MissileTarget != null)
            p.HandleActionCancelAttack();
        foreach (var (old, nw, slot) in plan)
        {
            p.HandleActionPutItemInContainer(old.Guid.Full, p.Guid.Full, 0);
            wieldQueue.Enqueue((nw.Guid.Full, slot));
        }
        swapUntil = DateTime.UtcNow.AddSeconds(0.3);
        Swaps++;
        return true;
    }

    private static bool LabDotDue(Tracked target, Dictionary<uint, DateTime> casts, uint spellId, Player p)
    {
        if (!casts.TryGetValue(target.Guid, out var when)) return true;
        if ((DateTime.UtcNow - when).TotalSeconds < DotRefresh) return false;
        var creature = Live(target.Guid, p);
        if (creature == null) return false;
        var spell = new Spell(spellId);
        return !creature.EnchantmentManager.GetEnchantments(spell.Category)
            .Any(e => e.CasterObjectId == p.Guid.Full && (uint)e.SpellId == spellId);
    }

    private static void Drive(Player p)
    {
        if (wieldCheck != null || wieldQueue.Count > 0)
        {
            if (p.IsBusy || DateTime.UtcNow < swapUntil) return;
            if (wieldCheck is { } wc)
            {
                var inHand = p.EquippedObjects.Values.Any(o => o.Guid.Full == wc.Guid);
                if (!inHand && wc.Tries < 5)
                {
                    // the wield did not take (still moving out of the old slot, busy, ...): try again shortly
                    p.HandleActionGetAndWieldItem(wc.Guid, wc.Slot);
                    wieldCheck = (wc.Guid, wc.Slot, wc.Tries + 1);
                    swapUntil = DateTime.UtcNow.AddSeconds(0.5);
                    return;
                }
                wieldCheck = null;
            }
            if (wieldQueue.Count > 0)
            {
                var (g, slot) = wieldQueue.Dequeue();
                p.HandleActionGetAndWieldItem(g, slot);
                wieldCheck = (g, slot, 1);
                swapUntil = DateTime.UtcNow.AddSeconds(0.5);
            }
            return;
        }

        var target = Spawns.Values.Where(t => t.Died == null)
            .Select(t => Live(t.Guid, p)).Where(c => c != null && c.IsAlive)
            .OrderBy(c => c!.Location.DistanceTo(p.Location)).FirstOrDefault();
        if (target == null) return;

        var launcher = p.GetEquippedMissileLauncher();
        var wand = p.GetEquippedWand();

        if (launcher != null)
        {
            if (p.CombatMode != CombatMode.Missile) { p.SetCombatMode(CombatMode.Missile); return; }
            if (p.MissileTarget != null && p.MissileTarget.IsAlive && !Stalled()) return;
            if (p.IsBusy || (p.Attacking && !Stalled())) return;
            if (TrySwap(p, p.MissileTarget != null && p.MissileTarget.IsAlive ? p.MissileTarget : target)) return;
            AutoRepeat(p);
            LastIssue = DateTime.UtcNow;
            p.HandleActionTargetedMissileAttack((p.MissileTarget != null && p.MissileTarget.IsAlive ? p.MissileTarget : target).Guid.Full, 2, Power);
        }
        else if (wand != null && SpellId != 0)
        {
            // a real caster carries components; the bench character has none
            if (p.SpellComponentsRequired) p.SpellComponentsRequired = false;
            if (p.CombatMode != CombatMode.Magic) { p.SetCombatMode(CombatMode.Magic); return; }
            if (p.MagicState.IsCasting || p.IsBusy) return;
            var spell = SpellId;
            var at = target;
            // Active combos prime ONE target before releasing into its neighbours.
            // The same focused rotation is available without a payoff as a control.
            if (DotRefresh > 0 && C.LabCombo && C.DotSpell3 != 0)
            {
                var focus = Spawns.Values.Where(t => t.Died == null)
                    .Select(t => Live(t.Guid, p)).Where(c => c != null && c.IsAlive)
                    .OrderBy(c => c!.Location.DistanceTo(p.Location))
                    .FirstOrDefault(c => !LastCast.ContainsKey(c!.Guid.Full) || !C.LastCast2.ContainsKey(c.Guid.Full)
                        || !C.LastCast3.ContainsKey(c.Guid.Full) || (SpellId2 != 0 && !C.LabComboReleased.Contains(c.Guid.Full)));
                if (focus != null)
                {
                    at = focus; var nowCombo = DateTime.UtcNow;
                    if (!LastCast.ContainsKey(at.Guid.Full)) { spell = SpellId; LastCast[at.Guid.Full] = nowCombo; }
                    else if (!C.LastCast2.ContainsKey(at.Guid.Full)) { spell = DotSpell2; C.LastCast2[at.Guid.Full] = nowCombo; }
                    else if (!C.LastCast3.ContainsKey(at.Guid.Full)) { spell = C.DotSpell3; C.LastCast3[at.Guid.Full] = nowCombo; }
                    else { spell = SpellId2; C.LabComboReleased.Add(at.Guid.Full); }
                    if (Run != null) Run.Casts++;
                    p.HandleActionCastTargetedSpell(at.Guid.Full, spell);
                    return;
                }
            }
            if (DotRefresh > 0)
            {
                var now = DateTime.UtcNow;
                var fresh = Spawns.Values.Where(t => t.Died == null && LabDotDue(t, LastCast, SpellId, p)
                        && (DotMax <= 0 || LastCast.ContainsKey(t.Guid) || LastCast.Count(kv => Spawns.TryGetValue(kv.Key, out var tt) && tt.Died == null) < DotMax))
                    .Select(t => Live(t.Guid, p)).Where(c => c != null && c.IsAlive)
                    .OrderBy(c => c!.Location.DistanceTo(p.Location)).FirstOrDefault();
                if (fresh != null) { at = fresh; LastCast[fresh.Guid.Full] = now; }
                else if (DotSpell2 != 0 && Spawns.Values.Where(t => t.Died == null && LabDotDue(t, C.LastCast2, DotSpell2, p) && (DotMax <= 0 || C.LastCast2.ContainsKey(t.Guid) || C.LastCast2.Count(kv => Spawns.TryGetValue(kv.Key, out var tt) && tt.Died == null) < DotMax))
                             .Select(t => Live(t.Guid, p)).Where(c => c != null && c.IsAlive)
                             .OrderBy(c => c!.Location.DistanceTo(p.Location)).FirstOrDefault() is { } fresh2)
                {
                    // the second DoT, once the first is on everything
                    at = fresh2; spell = DotSpell2; C.LastCast2[fresh2.Guid.Full] = now;
                }
                else if (C.DotSpell3 != 0 && Spawns.Values.Where(t => t.Died == null && LabDotDue(t, C.LastCast3, C.DotSpell3, p) && (DotMax <= 0 || C.LastCast3.ContainsKey(t.Guid) || C.LastCast3.Count(kv => Spawns.TryGetValue(kv.Key, out var tt) && tt.Died == null) < DotMax))
                             .Select(t => Live(t.Guid, p)).Where(c => c != null && c.IsAlive)
                             .OrderBy(c => c!.Location.DistanceTo(p.Location)).FirstOrDefault() is { } fresh3)
                {
                    at = fresh3; spell = C.DotSpell3; C.LastCast3[fresh3.Guid.Full] = now;
                }
                else if (SpellId2 != 0) spell = C.LabCatalyst && C.DotSpell3 != 0 && NativeBenchAdapter.NeedsRearm(p, at) ? C.DotSpell3 : SpellId2;
                else return;
            }
            if (Swap && DotRefresh <= 0)
            {
                if (TrySwap(p, at)) return;
                var w = p.GetEquippedWand();
                if (w != null && StreakOf.TryGetValue(w.W_DamageType, out var streak)) spell = streak;
            }
            if (Run != null) Run.Casts++;
            p.HandleActionCastTargetedSpell(at.Guid.Full, spell);
        }
        else
        {
            if (p.CombatMode != CombatMode.Melee) { p.SetCombatMode(CombatMode.Melee); return; }
            // the server's auto-repeat chain can break while the target lives (a monster steps out of reach, a heal):
            // re-issue after StallSeconds with no hit instead of standing idle
            if (p.MeleeTarget != null && p.MeleeTarget.IsAlive && !Stalled()) return;
            if (p.IsBusy || (p.Attacking && !Stalled())) return;
            if (TrySwap(p, p.MeleeTarget != null && p.MeleeTarget.IsAlive ? p.MeleeTarget : target)) return;
            // never swing with an empty weapon hand (a wield still landing): that hit would be a punch
            if (swapKinds.Count > 0 && swapKinds.Any(k => !p.EquippedObjects.Values.Any(o => o.CurrentWieldedLocation == k.Slot))) return;
            AutoRepeat(p);
            LastIssue = DateTime.UtcNow;
            p.HandleActionTargetedMeleeAttack((p.MeleeTarget != null && p.MeleeTarget.IsAlive ? p.MeleeTarget : target).Guid.Full, 2, Power);
        }
    }

    internal const double StallSeconds = 2.5;

    private static bool Stalled()
    {
        var now = DateTime.UtcNow;
        return (now - LastHit).TotalSeconds > StallSeconds && (now - LastIssue).TotalSeconds > StallSeconds;
    }

    private static void AutoRepeat(Player p)
    {
        if (!p.GetCharacterOption(CharacterOption.AutoRepeatAttacks))
            p.SetCharacterOption(CharacterOption.AutoRepeatAttacks, true);
    }

    // ---------------------------------------------------------------- trials

    internal static void BeginRun(string label)
    {
        Run = new Stats();
        RunLabel = label;
    }

    internal static void StartTrial(Player p, string label, int count, ZcRank rank, string layout, float dist, int timeoutSec, uint wcid, float clearRadius, List<uint>? picks = null)
    {
        Fighting = false;
        trialDeadline = null;
        Clear(p, clearRadius);
        if (!RealSustain) Refill(p);          // real sustain: the vitals and vitae carry over from the last camp
        C.BusyUntil = default;
        OutOfZone = 0;
        var pack = SpawnPack(p, count, rank, layout, dist, wcid, picks);
        if (OutOfZone > 0)
        {
            foreach (var t in pack)
                Live(t.Guid, p)?.Destroy();
            Spawns.Clear();
            Outbox.Write("trial", new { label, ok = false, outcome = "error", error = $"{OutOfZone} spawn point(s) outside {ZoneName(p)} - move the character back to its spot", zone = ZoneName(p) });
            return;
        }
        if (pack.Count == 0)
        {
            Outbox.Write("trial", new { label, ok = false, error = "nothing spawned (no wcids of that rank in this zone?)", zone = ZoneName(p) });
            return;
        }
        BeginRun(label);
        C.SwapNote = "";
        LastCast.Clear();
        C.LastCast2.Clear();
        C.LastCast3.Clear();
        C.LabComboReleased.Clear();
        wieldQueue.Clear();
        wieldCheck = null;
        swapKinds = new();
        Swaps = 0;
        trialInfo = new Dictionary<string, object?>
        {
            ["label"] = label, ["zone"] = ZoneName(p), ["variation"] = p.Location.Variation, ["count"] = pack.Count,
            ["rank"] = rank.ToString(), ["layout"] = layout, ["dist"] = dist, ["power"] = Power, ["spell"] = SpellId, ["spell2"] = SpellId2, ["combo"] = C.LabCombo, ["catalyst"] = C.LabCatalyst, ["dot3"] = C.DotSpell3, ["dotRefresh"] = DotRefresh, ["seed"] = Seed,
            ["weapon"] = (p.GetEquippedMissileLauncher() ?? p.GetEquippedWand() ?? p.GetEquippedMeleeWeapon())?.Name,
            ["offhand"] = p.GetEquippedShield()?.Name ?? p.GetDualWieldWeapon()?.Name,
            ["monsters"] = pack.Select(t => new { t.Name, t.Wcid, t.MaxHp, t.Archetype, t.Rank }).ToList(),
        };
        trialDeadline = DateTime.UtcNow.AddSeconds(timeoutSec);
        Fighting = true;
    }

    internal static object Summary(string outcome, Player? p = null)
    {
        var s = Run ?? new Stats();
        var end = DateTime.UtcNow;
        var dur = (end - s.Start).TotalSeconds;
        var fightSecs = s.FirstHit != null ? ((s.LastKill ?? end) - s.FirstHit.Value).TotalSeconds : 0;
        var ttk = Spawns.Values.Where(t => t.Died != null && t.FirstHit != null)
            .Select(t => Math.Round((t.Died!.Value - t.FirstHit!.Value).TotalSeconds, 2)).ToList();
        var dealt = s.DealtByKind.Values.Sum();
        var taken = s.TakenByKind.Values.Sum();
        return new Dictionary<string, object?>
        {
            ["outcome"] = outcome,
            ["testOnlyHarness"] = true, ["gameplayOverrides"] = false, ["nativeProfile"] = p == null ? null : ACE.Server.Managers.NetherFractureManager.DeveloperStatus(p),
            ["info"] = trialInfo,
            ["seconds"] = Math.Round(dur, 2),
            ["fightSeconds"] = Math.Round(fightSecs, 2),
            // from the moment the bench starts fighting (pack already spawned) to the last kill: counts walking in
            ["swaps"] = Swaps,
            ["rawHits"] = s.RawHits,
            ["rawAvg"] = s.RawHits > 0 ? Math.Round(s.RawSum / s.RawHits) : 0,
            ["rawMax"] = Math.Round(s.RawMax),
            ["killShots"] = s.KillShots,
            ["oneHitKills"] = s.OneHitKills,
            ["hitsPerKill"] = Spawns.Values.Count(t => t.Died != null) is var nk && nk > 0 ? Math.Round(Spawns.Values.Where(t => t.Died != null).Sum(t => t.Hits) / (double)nk, 2) : 0,
            ["hitsEach"] = Spawns.Values.Where(t => t.Died != null).Select(t => t.Hits).ToList(),
            ["engageSeconds"] = Math.Round(((s.LastKill ?? end) - s.Start).TotalSeconds, 2),
            ["kills"] = Spawns.Values.Count(t => t.Died != null),
            ["spawned"] = Spawns.Count,
            ["ttk"] = ttk,
            ["dealt"] = Math.Round(dealt),
            ["dps"] = fightSecs > 0 ? Math.Round(dealt / fightSecs) : 0,
            ["labMonsters"] = s.LabMonsters.Values.ToArray(),
            ["labDotTicks"] = s.LabDotTicks, ["labDotGenerations"] = s.LabDotGenerations,
            ["dealtByKind"] = s.DealtByKind.ToDictionary(k => k.Key, k => Math.Round(k.Value)),
            ["hitsByKind"] = s.HitsByKind,
            ["crits"] = s.Crits,
            ["swings"] = s.Swings,
            ["casts"] = s.Casts,
            ["dealtToOthers"] = Math.Round(s.DealtToOthers),
            ["taken"] = Math.Round(taken),
            ["takenPerSec"] = fightSecs > 0 ? Math.Round(taken / fightSecs, 1) : 0,
            ["takenByKind"] = s.TakenByKind.ToDictionary(k => k.Key, k => Math.Round(k.Value)),
            ["takenHits"] = s.TakenHits,
            ["takenFromTracked"] = Math.Round(s.TakenFromTracked),
            ["heals"] = s.Heals, ["healedHp"] = Math.Round(s.HealedHp),
            ["stamRefills"] = s.StamRefills, ["manaRefills"] = s.ManaRefills, ["deaths"] = s.Deaths,
            ["swapNote"] = C.SwapNote,
            ["realSustain"] = RealSustain, ["sustainSeconds"] = Math.Round(s.SustainSeconds, 1),
            ["lootMeters"] = p != null ? LootMeters(p) : 0, ["corpses"] = Spawns.Values.Count(t => t.DeathPos != null),
            ["endHealth"] = p != null && p.Health.MaxValue > 0 ? Math.Round((double)p.Health.Current / p.Health.MaxValue, 3) : 0,
            ["endStamina"] = p != null && p.Stamina.MaxValue > 0 ? Math.Round((double)p.Stamina.Current / p.Stamina.MaxValue, 3) : 0,
            ["endMana"] = p != null && p.Mana.MaxValue > 0 ? Math.Round((double)p.Mana.Current / p.Mana.MaxValue, 3) : 0,
            ["vitae"] = p != null ? Math.Round(p.Vitae, 3) : 1,
            // metres per second at the character's run rate (MotionInterp.RunAnimSpeed 4.0 x run rate): the loot walk and travel
            ["runSpeed"] = p != null ? Math.Round(p.GetRunRate() * 4.0, 2) : 0,
        };
    }

    private static void FinishTrial(Player p, string outcome)
    {
        Fighting = false;
        trialDeadline = null;
        var summary = Summary(outcome, p);
        Outbox.Write("trial", summary);
        Mod.Log($"trial {RunLabel} {outcome}");
        Clear(p, 0);
        Run = null;
        trialInfo = null;
        if (p.IsAlive && !RealSustain) Refill(p);
        C.BusyUntil = default;
    }

    // ---------------------------------------------------------------- hooks (called from Hooks.cs)

    internal static void OnDamage(Creature victim, WorldObject attacker, DamageType type, uint amount, string kind)
    {
        var s = Run;
        if (s == null || amount == 0) return;
        var now = DateTime.UtcNow;

        if (victim is Player vp && PatchClass.IsBench(vp))
        {
            var key = $"{kind}:{type}";
            s.TakenByKind[key] = s.TakenByKind.GetValueOrDefault(key) + amount;
            s.TakenHits[key] = s.TakenHits.GetValueOrDefault(key) + 1;
            if (Spawns.ContainsKey(attacker.Guid.Full)) s.TakenFromTracked += amount;
            return;
        }

        var src = attacker is Player ap ? ap : (attacker as Creature)?.PetOwner();
        if (src == null || !PatchClass.IsBench(src)) return;

        if (Spawns.TryGetValue(victim.Guid.Full, out var t))
        {
            t.FirstHit ??= now;
            s.FirstHit ??= now;
            t.Hits++;
            s.DealtByKind[kind] = s.DealtByKind.GetValueOrDefault(kind) + amount;
            s.HitsByKind[kind] = s.HitsByKind.GetValueOrDefault(kind) + 1;
        }
        else
            s.DealtToOthers += amount;
    }

    internal static void OnDeath(Creature c)
    {
        if (!Spawns.TryGetValue(c.Guid.Full, out var t) || t.Died != null) return;
        t.Died = DateTime.UtcNow;
        t.DeathPos = c.Location != null ? new Position(c.Location) : null;
        if (Run != null) { Run.LastKill = t.Died; if (t.Hits <= 1) Run.OneHitKills++; }
    }

    private static Player? PetOwner(this Creature c) =>
        c is ACE.Server.WorldObjects.Pet pet ? pet.P_PetOwner : null;
}
