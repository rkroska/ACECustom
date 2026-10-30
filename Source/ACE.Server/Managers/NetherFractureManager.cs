using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using log4net;

namespace ACE.Server.Managers
{
    public static class NetherFractureManager
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(NetherFractureManager));
        private static readonly object ConfigGate = new object();
        private static NetherFractureConfigurationStore store;
        private static string path;
        private static string configuredModsDirectory;
        private static DateTime nextRead, nextWarning;
        internal static NetherFractureConfiguration CurrentConfiguration => Config();

        public static void Initialize(string configPath)
        {
            // Throw on missing/invalid startup configuration. Do not guess damage coefficients.
            var initial = new NetherFractureConfigurationStore(File.ReadAllText(configPath));
            if (initial.Current.Enabled && PrototypePresent())
                throw new InvalidDataException("Remove the BalanceBench prototype mod before enabling native Nether Fracture (double application).");
            lock (ConfigGate) { path = configPath; store = initial; nextRead = DateTime.UtcNow.AddSeconds(1); }
            Log.Info($"Native Nether Fracture config validated: enabled={initial.Current.Enabled}, zones={initial.Current.Zones.Count}");
        }

        private static bool PrototypePresent()
        {
            var mods = Path.Combine(AppContext.BaseDirectory, "Mods");
            return new[] { mods, configuredModsDirectory }.Where(p => !string.IsNullOrEmpty(p)).Distinct()
                .Any(p => Directory.Exists(p) && Directory.EnumerateFiles(p, "BalanceBench.dll", SearchOption.AllDirectories).Any());
        }

        public static void ValidateModDirectory(string modsDirectory)
        {
            lock (ConfigGate)
            {
                configuredModsDirectory = modsDirectory;
                if (store?.Current.Enabled == true && PrototypePresent())
                    throw new InvalidDataException("Remove the BalanceBench prototype from the configured ModsDirectory before enabling native Nether Fracture.");
            }
        }

        public static string DeveloperStatus(Player p)
        {
            var cfg = Config(); var key = Key(p);
            if (cfg == null) return "Nether Fracture is not initialized.";
            if (key == null || !cfg.Zones.TryGetValue(key, out var z))
                return $"Nether Fracture enabled={cfg.Enabled}; no profile here. Configured zones: {string.Join(", ", cfg.Zones.Keys)}";
            return FormattableString.Invariant($"Nether Fracture [{key}] enabled={cfg.Enabled}: corrosion={z.Corrosion}, corruption={z.Corruption}, curse={z.Curse}, streak={z.Streak}, fracture={z.Fracture}, radius={z.Radius}m. Bank=.20; window/cooldown=8s; targets=4; group cap=2 strongest.");
        }

        public static void SetZone(Player p, string source, double amount)
        {
            var canonical = source.ToLowerInvariant() switch
            {
                "corrosion" => "Corrosion", "corruption" => "Corruption", "curse" => "Curse", "streak" => "Streak",
                "fracture" => "Fracture", "rupture" => "Fracture", "radius" => "Radius", _ => null
            };
            if (canonical == null || !double.IsFinite(amount)) throw new ArgumentException("Unknown source or non-finite amount.");
            lock (ConfigGate)
            {
                var cfg = Config(); var key = Key(p);
                if (cfg == null || key == null || !cfg.Zones.ContainsKey(key)) throw new ArgumentException("Stand inside a configured named ZC.");
                var node = System.Text.Json.JsonSerializer.SerializeToNode(cfg);
                node["Zones"][key][canonical] = amount;
                var json = node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                var valid = NetherFractureConfiguration.Parse(json);
                if (valid.Enabled && PrototypePresent()) throw new InvalidDataException("Remove the prototype mod first.");
                var temp = path + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, path, true);
                if (!store.TryReload(json, out var error)) throw new InvalidDataException(error);
                Log.Info($"Nether Fracture admin tuning: actor={p.Name}, zone={key}, source={canonical}, amount={amount:R}");
            }
        }

        private static NetherFractureConfiguration Config()
        {
            lock (ConfigGate)
            {
                if (store == null) return null; // Unit tests / uninitialized host: feature inactive.
                if (DateTime.UtcNow >= nextRead)
                {
                    nextRead = DateTime.UtcNow.AddSeconds(1);
                    string error = null;
                    try
                    {
                        var json = File.ReadAllText(path);
                        var candidate = NetherFractureConfiguration.Parse(json);
                        if (candidate.Enabled && PrototypePresent()) error = "BalanceBench prototype mod is still installed.";
                        else store.TryReload(json, out error);
                    }
                    catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException) { error = ex.Message; }
                    catch (System.Text.Json.JsonException ex) { error = ex.Message; }
                    catch (ArgumentException ex) { error = ex.Message; }
                    if (error != null && DateTime.UtcNow >= nextWarning)
                    {
                        nextWarning = DateTime.UtcNow.AddMinutes(1);
                        Log.Error("Nether Fracture reload rejected; retaining last valid configuration: " + error);
                    }
                }
                return store.Current;
            }
        }

        public static bool CharmEligible(Player p)
        {
            // Cast DoTs retain their tuned coefficients if the caster dies. Active releases
            // and new bank credit separately require a living caster.
            if (p == null || !CharmSettingsManager.Pandemic.Enabled || !p.HasPandemicCharm
                || CharmAbilityRegistry.LevelOf(p, CharmAbilityRegistry.PandemicAbilityId) < 1) return false;
            var wand = p.GetEquippedWand();
            return !CharmSettingsManager.Pandemic.On("t11only") || wand != null && ZoneCraftGate.TierOf(wand) >= 11 && !ZoneControlManager.WeaponPowerSuppressed(wand, p);
        }

        private static string Key(Creature c)
        {
            if (c?.Location == null) return null;
            var area = ZoneControlManager.ResolveWinnerForLocation((ushort)c.Location.Landblock, c.Location.Variation ?? 0);
            return area == null ? null : $"{c.Location.Variation ?? 0}:{area.Name}";
        }

        public static bool Monster(Creature c) => c != null && c is not Player && c is not Pet && c is not CombatPet && c.WeenieType == WeenieType.Creature;
        public static bool Tuned(Creature c)
        {
            var cfg = Config(); var key = Key(c);
            return cfg != null && cfg.Enabled && key != null && cfg.Zones.ContainsKey(key);
        }
        public static bool TryTuning(Player p, Creature target, out NetherFractureZone zone)
        {
            zone = null;
            return CharmEligible(p) && TrySourceTuning(p, target, out zone);
        }
        // These are base Void coefficients in the named ZCs, not a penalty for equipping
        // Pandemic. The prototype's approved testers already received them without a charm.
        public static bool TrySourceTuning(Player p, Creature target, out NetherFractureZone zone)
        {
            zone = null;
            var cfg = Config();
            if (cfg == null || !cfg.Enabled || p == null || !Monster(target) || p.Location?.Variation != target.Location?.Variation) return false;
            var key = Key(target); var playerKey = Key(p);
            return key != null && playerKey != null && cfg.Zones.ContainsKey(playerKey) && cfg.Zones.TryGetValue(key, out zone);
        }
        public static bool CanTarget(Player p, Creature c) => TryTuning(p, c, out _) && c.CanBeDamagedBy(p) && p.CanDamage(c);
        private static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        private static bool Three(Player p, Creature target)
        {
            var entries = target.Biota.PropertiesEnchantmentRegistry.Clone(target.BiotaDatabaseLock);
            if (entries == null) return false;
            var families = entries.Where(e => e.CasterObjectId == p.Guid.Full && (e.Duration < 0 || e.Duration + e.StartTime > 0))
                .Where(e => target.ZcDotSnapshotFor(e) is { FractureHandCast: true, Generation: 0 }).Select(e => IndependentVoidDots.Family((uint)e.SpellId)).ToHashSet();
            return families.Contains("corrosion") && families.Contains("corruption") && families.Contains("curse");
        }

        public static void Applied(Player p, Creature target)
        {
            if (!p.IsAlive || !CanTarget(p, target) || !Three(p, target) || !TryTuning(p, target, out var z) || z.Fracture <= 0) return;
            var s = p.NetherFracture;
            if (!s.Prime(target.Guid.Full, Now)) return;
            target.EnqueueBroadcast(new GameMessageScript(target.Guid, PlayScript.EnchantUpPurple));
            p.Session?.Network.EnqueueSend(new GameMessageSystemChat($"Your void enchantments prime {target.Name}. Nether Streak will release Nether Fracture.", ChatMessageType.Spellcasting));
        }

        public static void Credit(Player p, Creature target, uint actual)
        {
            if (actual == 0 || !p.IsAlive || !CanTarget(p, target)) return;
            var s = p.NetherFracture;
            s.Credit(actual, Now);
        }

        public static void BeforeStreak(Player p, Creature target, ref float damage)
        {
            if (!TrySourceTuning(p, target, out var z)) return;
            damage *= (float)z.Streak;
            if (p.IsAlive && CanTarget(p, target) && Three(p, target)) Trigger(p, target, new Position(target.Location), false);
        }
        public static bool IsHandCastVoidStreak(SpellCategory category, bool fromProc, bool weaponSpell, bool handCastFork) =>
            category == SpellCategory.NetherStreak && (handCastFork || !fromProc && !weaponSpell);

        public static void Died(Creature target)
        {
            if (!Monster(target) || target.Location == null) return;
            foreach (var p in PlayerManager.GetAllOnline())
            {
                var s = p.NetherFracture;
                lock (s.Gate) if (!s.Armed.Contains(target.Guid.Full)) continue;
                if (!p.IsAlive || !CanTarget(p, target)) { lock (s.Gate) s.Armed.Remove(target.Guid.Full); continue; }
                var origin = new Position(target.Location);
                // The killing DoT credits its actual capped health loss before this action executes.
                p.EnqueueAction(new ActionEventDelegate(ActionType.ControlFlowDelay, () => Trigger(p, target, origin, true)));
            }
        }

        private static void Trigger(Player p, Creature target, Position origin, bool death)
        {
            if (!p.IsAlive || !CanTarget(p, target) || !TryTuning(p, target, out var z)) return;
            var s = p.NetherFracture;
            if (!s.Release(target.Guid.Full, Now, z.Fracture, out var budget)) return;
            var originHeight = target.PhysicsObj?.GetHeight() ?? 0f;
            BurstVisual(p, origin, (float)z.Radius);
            if (death) p.Session?.Network.EnqueueSend(new GameMessageSystemChat($"Your primed enchantments release Nether Fracture as {target.Name} falls.", ChatMessageType.Spellcasting));
            p.EnqueueAction(new ActionEventDelegate(ActionType.ControlFlowDelay, () => Pulse(p, origin, originHeight, budget, (float)z.Radius)));
        }

        // Trace from the captured blast point, never from the caster or a moving/dead source.
        // Do not use the ring point-blank shortcut: a thin wall can separate adjacent victims.
        internal static bool TraceBurstSight(Position origin, float originHeight,
            Physics.Common.Position target, float targetHeight,
            Func<Physics.Common.Position, Physics.Common.Position, bool> trace)
        {
            if (origin == null || target == null || origin.Variation != target.Variation) return false;
            var start = new Physics.Common.Position(origin);
            var end = new Physics.Common.Position(target);
            start.Frame.Origin.Z += originHeight * WorldObject.ProjHeight;
            end.Frame.Origin.Z += targetHeight * WorldObject.ProjHeight;
            return trace(start, end);
        }

        private static bool Visible(Position origin, float originHeight, Creature target)
        {
            if (origin == null || target.PhysicsObj?.PartArray == null) return false;
            var cell = Physics.Common.LScape.get_landcell(origin.Cell, origin.Variation);
            if (cell == null) return false;
            var sight = Player.CreateRingSightObject();
            try
            {
                sight.SightIgnoresTerrain = false;
                sight.CurCell = cell;
                sight.ProjectileTarget = target.PhysicsObj;
                return TraceBurstSight(origin, originHeight, target.PhysicsObj.Position, target.PhysicsObj.GetHeight(),
                    (start, end) => sight.transition(start, end, false)?.CollisionInfo.CollideObject
                        .Any(c => c.ID == target.PhysicsObj.ID) == true);
            }
            finally { sight.DestroyObject(); }
        }
        private static void Pulse(Player p, Position origin, float originHeight, double budget, float radius)
        {
            if (!p.IsAlive || !CharmEligible(p) || p.CurrentLandblock == null || !double.IsFinite(budget) || budget <= 0) return;
            var blocks = new List<Landblock> { p.CurrentLandblock };
            if (p.CurrentLandblock.Adjacents != null) blocks.AddRange(p.CurrentLandblock.Adjacents.Where(b => b != null));
            var targets = blocks.SelectMany(b => b.GetWorldObjectsForPhysicsHandling()).OfType<Creature>().DistinctBy(c => c.Guid.Full)
                .Where(c => c.IsAlive && c.Location != null && c.Location.Variation == origin.Variation && CanTarget(p, c)
                    && c.Location.Distance2D(origin) <= radius && Visible(origin, originHeight, c))
                .OrderBy(c => c.Location.Distance2D(origin)).Take(NetherFractureMath.TargetCap).ToArray();
            if (targets.Length == 0) return;
            var share = (float)(budget / targets.Length);
            foreach (var c in targets)
                c.EnqueueAction(new ActionEventDelegate(ActionType.ControlFlowDelay, () =>
                {
                    if (!p.IsAlive || !c.IsAlive || c.Location == null || c.Location.Variation != origin.Variation
                        || c.Location.Distance2D(origin) > radius || !CanTarget(p, c) || !Visible(origin, originHeight, c)) return;
                    var dealt = c.TakeDamage(p, DamageType.Nether, share);
                    if (dealt > 0) p.Session?.Network.EnqueueSend(new GameMessageSystemChat($"Your Nether Fracture strikes {c.Name} for {dealt:N0} points of Nether damage.", ChatMessageType.Combat));
                    c.EnqueueBroadcast(new GameMessageScript(c.Guid, PlayScript.HealthDownVoid));
                }));
        }

        private static void BurstVisual(Player p, Position origin, float radius)
        {
            SingleVisual(p, origin, Vector3.Zero, 10f);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Math.PI / 4;
                SingleVisual(p, origin, new Vector3((float)Math.Cos(angle) * (radius / .9f), (float)Math.Sin(angle) * (radius / .9f), 0), 4f);
            }
        }
        private static void SingleVisual(Player p, Position origin, Vector3 velocity, float scale)
        {
            var spell = new Spell(5348);
            if (WorldObjectFactory.CreateNewWorldObject(spell.Wcid) is not SpellProjectile fx) return;
            fx.ObjScale = scale; fx.Setup(spell, SpellProjectile.GetProjectileSpellType(spell.Id), origin.Variation);
            fx.Location = new Position(origin); fx.ProjectileSource = p; fx.Location.Pos += new Vector3(0, 0, 1.2f);
            fx.ReportCollisions = false; fx.IgnoreCollisions = true; fx.Ethereal = true; fx.Missile = false;
            fx.SpawnPos = new Position(fx.Location); fx.PhysicsObj.Velocity = velocity;
            if (!LandblockManager.AddObject(fx)) { fx.Destroy(); return; }
            fx.EnqueueBroadcast(new GameMessageScript(fx.Guid, PlayScript.Launch, 1.0f));
            var chain = new ActionChain(); chain.AddDelaySeconds(.9);
            chain.AddAction(fx, new ActionEventDelegate(ActionType.ControlFlowDelay, () => fx.ProjectileImpact()));
            chain.AddDelaySeconds(2); chain.AddAction(fx, new ActionEventDelegate(ActionType.ControlFlowDelay, () => fx.Destroy())); chain.EnqueueChain();
        }
    }
}
