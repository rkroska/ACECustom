using ACE.Server.Managers.ZoneScaling;

namespace BalanceBench;

/// <summary>/bench: the balance bench. Also reachable from inbox files as "bench ...".</summary>
internal static class Commands
{
    private const string Name = "bench";
    private const string Usage =
        "ping | where | go <tier> | clear [radius] | zone [regular|leader|boss] | pack <n> <rank> [ring|arc|line] [dist] [wcid]\n" +
        "mirror <name> [stats|body|weapons|all] [style] | srcprops <name> [filter] | stats | equip <style> | strip | gear | which <cmd> | fight on|off [power] [spellId] | run start <label> | run stop | refill | healat <0-1> | surges on|off|show | deedsrefresh | status\n" +
        "trial <label> <n> <rank> <ring|arc|line> <dist> [timeout=150 power=1 spell=0 spell2=0 dot=0 wcid=0 camp=leader:regular[:n] clear=40 seed=0 healat=0.35]";

    // bench surges: the always-on Aetheria surges' caster id (no object has it; Deeds uses 0x00DEED00)
    private const uint BenchSurgeCasterId = 0x00BE5C00;
    private static readonly uint[] SurgeIds = { 5204, 5206, 5208 };

    // a flat beach spot in Tou Tou (landblock F157); the variation picks the tier
    private const uint SpotCell = 0xF1570026;
    private const float SpotX = 100.54f, SpotY = 121.13f, SpotZ = 0.1f;

    internal static void Register(AccessLevel access)
    {
        CommandManager.TryAddCommand(Handle, Name, access, CommandHandlerFlag.RequiresWorld, "T11+ balance bench.", Usage);
    }

    internal static void Unregister() => CommandManager.TryRemoveCommand(Name);

    private static void Handle(Session session, params string[] p)
    {
        var player = session?.Player;
        if (player == null) return;
        if (!PatchClass.IsBench(player)) { Say(player, "The bench only runs for its listed characters."); return; }
        using var scope = Bench.Use(player);
        try { Dispatch(player, p); }
        catch (Exception ex) { Say(player, $"bench error: {ex.Message}"); }
    }

    internal static void Say(Player player, string text) =>
        player.Session.Network.EnqueueSend(new GameMessageSystemChat(text.Replace("\r\n", "\n"), ChatMessageType.Broadcast));

    private static ZcRank Rank(string s) => s.ToLowerInvariant() switch
    {
        "boss" => ZcRank.Boss,
        "leader" => ZcRank.Leader,
        _ => ZcRank.Regular,
    };

    private static string Arg(string[] p, int i, string def) => p.Length > i ? p[i] : def;
    private static float F(string[] p, int i, float def) => p.Length > i && float.TryParse(p[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;
    private static int I(string[] p, int i, int def) => p.Length > i && int.TryParse(p[i], out var v) ? v : def;
    private static uint U(string[] p, int i, uint def) => p.Length > i && uint.TryParse(p[i], out var v) ? v : def;

    internal static void Dispatch(Player player, string[] p)
    {
        var sub = p.Length > 0 ? p[0].ToLowerInvariant() : "";
        TestOnlyPolicy.CheckBench(player, p);
        switch (sub)
        {
            case "lab":
            case "native":
                Say(player, NativeBenchAdapter.Probe(player));
                return;
            case "testonly":
                TestOnlyAudit.AssertPure();
                Say(player, "Test-only audit passed; no gameplay modifiers or prototype code compiled.");
                return;
            case "fracturelab":
                NativeGroupCamps.Command(player,p);
                return;
            case "void":
                if(p.Length > 1 && p[1] != "show") throw new InvalidOperationException("Test-only harness cannot change live damage settings. Use native developer controls separately.");
                Say(player, ACE.Server.Managers.NetherFractureManager.DeveloperStatus(player));
                return;
            case "visual":
            case "labcapacity":
                throw new InvalidOperationException("Prototype command excluded from the test-only harness.");
            case "ping":
                Say(player, $"pong {player.Name} lvl {player.Level}");
                return;

            case "where":
                Say(player, $"{player.Location} variation {player.Location.Variation} zone {Bench.ZoneName(player)} teleporting {player.Teleporting}");
                return;

            case "go":
            {
                var tier = I(p, 1, 0);
                if (tier < 1) { Say(player, "Usage: /bench go <tier>"); return; }
                Bench.Fighting = false;
                Bench.Clear(player, 0);
                var pos = new Position(SpotCell, SpotX, SpotY, SpotZ, 0f, 0f, 0f, 1f, false, tier);
                player.Teleport(pos);
                Say(player, $"Teleporting to Tou Tou variation {tier}.");
                return;
            }

            case "clear":
                Say(player, $"Cleared {Bench.Clear(player, F(p, 1, 40))} creatures.");
                return;

            case "zone":
            {
                var rank = Rank(Arg(p, 1, "regular"));
                var list = Bench.ZoneWcids(player, rank);
                Say(player, $"{Bench.ZoneName(player)} {rank}: " + string.Join(", ", list.Select(w => $"{w} {DatabaseManager.World.GetCachedWeenie(w)?.GetName()}")));
                return;
            }

            case "pack":
            {
                var made = Bench.SpawnPack(player, I(p, 1, 5), Rank(Arg(p, 2, "regular")), Arg(p, 3, "ring"), F(p, 4, 4), U(p, 5, 0));
                Say(player, $"Spawned {made.Count}: " + string.Join(", ", made.Select(t => $"{t.Name} hp {t.MaxHp} {t.Archetype}")));
                return;
            }

            case "fight":
            {
                var on = Arg(p, 1, "on").ToLowerInvariant() != "off";
                Bench.Power = F(p, 2, Bench.Power);
                Bench.SpellId = U(p, 3, Bench.SpellId);
                Bench.Fighting = on;
                Say(player, $"Fight {(on ? "on" : "off")}, power {Bench.Power}, spell {Bench.SpellId}.");
                return;
            }

            case "run":
                if (Arg(p, 1, "") == "start") { Bench.BeginRun(Arg(p, 2, "run")); Say(player, "Run started."); }
                else { Outbox.Write("run", Bench.Summary("stopped")); Bench.Run = null; Say(player, "Run stopped, summary written."); }
                return;

            case "which":
            {
                if (CommandManager.commandHandlers.TryGetValue(Arg(p, 1, ""), out var info))
                    Say(player, $"{info.Handler.Method.DeclaringType?.FullName}.{info.Handler.Method.Name} in {info.Handler.Method.DeclaringType?.Assembly.Location} access {info.Attribute?.Access} minparams {info.Attribute?.ParameterCount}");
                else Say(player, "not registered");
                return;
            }

            case "equip":
                Say(player, "equip: " + string.Join("; ", Equip.Run(player, Arg(p, 1, "2h").ToLowerInvariant())));
                return;

            case "strip":
                Say(player, $"strip: {Equip.Strip(player)} items unwielded");
                return;

            case "cloneitem":
                Say(player, uint.TryParse(Arg(p, 1, ""), out var cloneId) ? Mirror.CloneItem(player, cloneId) : "cloneitem <database id>");
                return;

            case "gear":
                Say(player, "wielded: " + string.Join("; ", player.EquippedObjects.Values.Select(i => $"{i.Name} [{i.CurrentWieldedLocation}]")));
                Say(player, "pack: " + string.Join("; ", player.GetAllPossessions().Where(i => i.CurrentWieldedLocation == null && i.ValidLocations is not null and not EquipMask.None).Select(i => i.Name)));
                return;

            case "mirror":
            {
                // names with spaces: use _ (Nerd_Parade)
                if (p.Length < 2) { Say(player, "Usage: /bench mirror <name> [stats|body|weapons|all] [style]"); return; }
                Mirror.Prefer = Arg(p, 4, "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(n => n.Replace('_', ' ')).ToList();
                Say(player, "mirror: " + string.Join("; ", Mirror.Apply(player, p[1].Replace('_', ' '), Arg(p, 2, "all").ToLowerInvariant(), Arg(p, 3, "2h").ToLowerInvariant())));
                return;
            }

            case "stats":
                Say(player, $"DR {player.GetDamageRating()} CR {player.GetCritRating()} CDR {player.GetCritDamageRating()} DResR {player.GetDamageResistRating()} " +
                            $"hp {player.Health.MaxValue} st {player.Stamina.MaxValue} mana {player.Mana.MaxValue} str {player.Strength.Current} coo {player.Coordination.Current} qui {player.Quickness.Current} " +
                            $"level {player.Level} enchantments {player.EnchantmentManager.GetEnchantments(MagicSchool.CreatureEnchantment).Count + player.EnchantmentManager.GetEnchantments(MagicSchool.LifeMagic).Count}");
                return;

            case "refill":
                Bench.Refill(player);
                Say(player, "Refilled.");
                return;

            case "healat":
                Bench.HealAt = Math.Clamp(F(p, 1, 0.35f), 0, 1);
                Say(player, $"Heal at {Bench.HealAt}.");
                return;

            case "dropweapons":
            {
                // destroys every weapon and caster not wielded (the preset's plain test weapons) so forged ones take their place
                // wielded ones too (2026-10-23): a weapon left in hand by the previous script carried a maxed kit into the next test
                foreach (var held in player.EquippedObjects.Values.Where(i => (i.ItemType == ItemType.MeleeWeapon || i.ItemType == ItemType.MissileWeapon || i.ItemType == ItemType.Caster)
                             && ((i.ValidLocations ?? EquipMask.None) & EquipMask.MissileAmmo) == 0).ToList())
                {
                    player.TryDequipObjectWithNetworking(held.Guid, out _, Player.DequipObjectAction.ConsumeItem);
                    held.Destroy();
                }
                var gone = player.GetAllPossessions().Where(i => i.CurrentWieldedLocation == null
                    && (i.ItemType == ItemType.MeleeWeapon || i.ItemType == ItemType.MissileWeapon || i.ItemType == ItemType.Caster)
                    && ((i.ValidLocations ?? EquipMask.None) & EquipMask.MissileAmmo) == 0).ToList();   // keep the arrows
                foreach (var i in gone)
                {
                    player.TryRemoveFromInventoryWithNetworking(i.Guid, out _, Player.RemoveFromInventoryAction.ConsumeItem);
                    i.Destroy();
                }
                Say(player, $"dropweapons: destroyed {gone.Count} ({string.Join(", ", gone.Select(i => i.Name))})");
                return;
            }

            case "rating":
                throw new InvalidOperationException("Synthetic rating overrides are excluded; use the validated native kit.");

            case "suitlines":
            {
                // bench suitlines <key:grade:count,...> [always=<grade>]: rewrites the worn suit's Zone Control line records
                // (2026-10-23: the kits copy real players - Schneebly's T13 suit, Nerd Parade's T15 suit). Every line key not
                // listed is dropped (slot specials included); the four Always Rolled resists (keys 50-53) stay, at always=
                // when given. Each listed key goes on <count> pieces that may carry it. The server then resolves the props from
                // the record (ApplyIfStale), so a later ladder re-resolve keeps them. Re-equip afterwards (bench strip + equip)
                // so the worn caches rebuild.
                if (p.Length < 2) { Say(player, "Usage: /bench suitlines <key:grade:count,...> [always=<grade>]"); return; }
                var always = p.Skip(2).Select(x => x.Split('=', 2)).Where(x => x.Length == 2 && x[0] == "always").Select(x => int.TryParse(x[1], out var g) ? g : -1).FirstOrDefault(-1);
                var spec = new List<(int Key, int Grade, int Count)>();
                foreach (var part in p[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = part.Split(':');
                    if (f.Length == 3 && int.TryParse(f[0], out var k) && int.TryParse(f[1], out var g) && int.TryParse(f[2], out var c))
                        spec.Add((k, Math.Clamp(g, 0, 1000), c));
                }
                var suit = player.EquippedObjects.Values.Where(i => i.ItemType != ItemType.MeleeWeapon && i.ItemType != ItemType.MissileWeapon
                        && i.ItemType != ItemType.Caster && ACE.Server.Managers.ZoneControl.ZoneStatResolver.HasRecord(i))
                    .OrderBy(i => i.Guid.Full).ToList();
                if (suit.Count == 0) { Say(player, "suitlines: no worn Zone Control pieces"); return; }
                bool Allowed(ACE.Server.Managers.ZoneControl.ZoneModifiers.Def d, WorldObject wo) =>
                    !(d.ArmorOnly && wo.ItemType != ItemType.Armor) && !(d.JewelryOnly && wo.ItemType != ItemType.Jewelry);
                var plan = suit.ToDictionary(w => w, _ => new List<(int Key, int Grade)>());
                foreach (var (key, grade, count) in spec)
                {
                    if (!ACE.Server.Managers.ZoneControl.ZoneModifiers.TryGet(key, out var def)) continue;
                    var ok = suit.Where(w => Allowed(def, w)).ToList();
                    if (ok.Count == 0) continue;
                    var start = (key * 7) % ok.Count;          // spread each key over a different run of pieces
                    for (var n = 0; n < Math.Min(count, ok.Count); n++)
                        plan[ok[(start + n) % ok.Count]].Add((key, grade));
                }
                var lineDefs = ACE.Server.Managers.ZoneControl.ZoneModifiers.AllDefs.Where(d => d.Key < 50 || d.Key > 53).ToList();
                var lines = 0;
                string? probe = null;
                foreach (var wo in suit)
                {
                    var before = $"{wo.Name}: rec[{wo.GetProperty(PropertyString.ZcModifiers)}] DR {wo.GearDamage} CDR {wo.GearCritDamage} ver {wo.GetProperty(PropertyInt.ZcResolvedVersion)}";
                    var rec = ACE.Server.Managers.ZoneControl.ZoneStatResolver.Read(wo)
                        .Where(r => r.Key < 0 || (r.Key >= 50 && r.Key <= 53))
                        .Select(r => always >= 0 && r.Key >= 50 ? new ACE.Server.Managers.ZoneControl.ZoneStatResolver.LineRecord { Key = r.Key, Grade = always } : r)
                        .ToList();
                    foreach (var (key, grade) in plan[wo])
                        rec.Add(new ACE.Server.Managers.ZoneControl.ZoneStatResolver.LineRecord { Key = key, Grade = grade });
                    lines += plan[wo].Count;
                    // drop every line prop the old record set, then let the server resolve the new record onto the piece
                    foreach (var d in lineDefs)
                        if (d.Ints != null)
                            foreach (var (propId, _) in d.Ints)
                                wo.RemoveProperty((PropertyInt)propId);
                    ACE.Server.Managers.ZoneControl.ZoneStatResolver.Write(wo, rec);
                    wo.SetProperty(PropertyInt.ZcResolvedVersion, 0);
                    var applied = ACE.Server.Managers.ZoneControl.ZoneStatResolver.ApplyIfStale(wo);
                    probe ??= before + $" -> rec[{wo.GetProperty(PropertyString.ZcModifiers)}] DR {wo.GearDamage} CDR {wo.GearCritDamage} ver {wo.GetProperty(PropertyInt.ZcResolvedVersion)} applied {applied}";
                }
                if (probe != null) Say(player, "suitlines probe: " + probe);
                RebuildWornCaches(player);
                Say(player, $"suitlines: {lines} lines over {suit.Count} pieces" + (always >= 0 ? $", always-rolled at grade {always}" : "") + " - worn totals rebuilt");
                return;
            }

            case "weapons":
            {
                // bench weapons (2026-10-25): every weapon carried or worn, with its damage type and skill - the swap's pool
                var all = player.GetAllPossessions().Where(o => o.ItemType == ItemType.MeleeWeapon || o.ItemType == ItemType.MissileWeapon || o.ItemType == ItemType.Caster)
                    .Select(o => $"{o.Name}:{o.W_DamageType}/{o.WeaponSkill}{(o.CurrentWieldedLocation != null ? "[" + o.CurrentWieldedLocation + "]" : "")}").OrderBy(x => x).ToList();
                Say(player, $"weapons ({all.Count}): " + string.Join(", ", all));
                return;
            }

            case "deedsrefresh":
            {
                // bench deedsrefresh (2026-10-25): re-read the account's QB from the database (the bench runner writes it) and
                // re-sync QB Deeds, without a relog
                player.Account.QuestCountCache = null;
                player.RefreshDeeds();
                Say(player, $"deedsrefresh: QB {player.ReadDeedsQb()} Deeds tier {player.DeedsTier} enabled {player.DeedsEnabled}");
                return;
            }

            case "surges":
                if(p.Length>1 && p[1]!="show") throw new InvalidOperationException("Synthetic surges are excluded. Use the character's native Deeds/surges.");
                Say(player,"Native surge IDs: "+string.Join(", ",(player.Biota.PropertiesEnchantmentRegistry.Clone(player.BiotaDatabaseLock)??new()).Where(e=>SurgeIds.Contains((uint)e.SpellId)).Select(e=>e.SpellId)));
                return;

            case "enchants":
            {
                // bench enchants: what lifts each attribute - base, the enchantment multiplier and additive, the gear lines -
                // and every active enchantment (2026-10-23: new bench characters read 7,000 strength below Balancer)
                var parts = new List<string>();
                foreach (var at in new[] { PropertyAttribute.Strength, PropertyAttribute.Endurance, PropertyAttribute.Coordination, PropertyAttribute.Quickness, PropertyAttribute.Focus, PropertyAttribute.Self })
                {
                    var a2 = player.Attributes[at];
                    parts.Add($"{at}: base {a2.Base} x{player.EnchantmentManager.GetAttributeMod_Multiplier(at):0.###} +{player.EnchantmentManager.GetAttributeMod_Additive(at)} " +
                              $"+gear {player.GetZoneModifierBonus(ACE.Server.Managers.ZoneControl.ZoneModifiers.AttrBonusBase + (int)at)} = {a2.Current}");
                }
                Say(player, "attributes: " + string.Join(" | ", parts));
                var list = player.Biota.PropertiesEnchantmentRegistry?.Select(e => $"{e.SpellId}:{(SpellId)e.SpellId}:{e.StatModType}:{e.StatModKey}:{e.StatModValue:0.##}").ToList() ?? new List<string>();
                Say(player, $"enchantments ({list.Count}): " + string.Join(", ", list.OrderBy(x => x)));
                return;
            }

            case "charm":
            {
                // bench charm <2h|dw|shield|bow|war|void|none> [level]: switches THIS character's levelled charms - the style's
                // one on at the level, every other carried charm off. Only the carried items change: the server-wide charm
                // enable (/charm <name> on|off) is never touched, so other bench characters keep theirs (2026-10-23).
                var ids = new Dictionary<string, int>
                {
                    ["2h"] = CharmAbilityRegistry.ImprovedCleaveAbilityId, ["dw"] = CharmAbilityRegistry.ArmorBreakerAbilityId,
                    ["shield"] = CharmAbilityRegistry.ShieldArcAbilityId, ["bow"] = CharmAbilityRegistry.SplitArrowAbilityId,
                    ["war"] = CharmAbilityRegistry.ChainAbilityId, ["void"] = CharmAbilityRegistry.PandemicAbilityId,
                };
                var style = Arg(p, 1, "none").ToLowerInvariant();
                var want = ids.TryGetValue(style, out var wid) ? wid : 0;
                var level = Math.Clamp(I(p, 2, 1), 1, 100);
                var changes = new List<string>();
                foreach (var item in player.GetAllPossessionsDeep().Where(i => i.IsAbilityCharm && i.CharmGrantsAbility is int a && ids.ContainsValue(a)).ToList())
                {
                    var ab = item.CharmGrantsAbility!.Value;
                    if (ab == want)
                    {
                        player.SetCharmLevel(ab, level);
                        item.IsCharmActivated = true;
                        CharmAbilityRegistry.Apply(player, ab, true, level);
                        player.SyncLevelledCharmItem(item, ab);
                        changes.Add($"{item.Name} ON level {level}");
                    }
                    else if (item.IsCharmActivated)
                    {
                        item.IsCharmActivated = false;
                        changes.Add($"{item.Name} off");
                    }
                }
                // every other style's charm effect off on the character itself too, item or not - a flag left by a copy
                // (bench mirror) or an earlier kit must never carry into this run
                foreach (var ab in ids.Values.Where(a => a != want))
                    CharmAbilityRegistry.Apply(player, ab, false, CharmAbilityRegistry.LevelOf(player, ab));
                Say(player, $"charm: {(changes.Count > 0 ? string.Join(", ", changes) : "nothing to change")}" +
                            (want != 0 && !changes.Any(c => c.Contains(" ON ")) ? " - WARNING: no carried charm for that style" : ""));
                return;
            }

            case "dropgear":
            {
                // destroys the suit - every armour, clothing, jewellery and cloak piece, worn or carried - so a different
                // tier's premade can take its place (the new-player kit wears the previous tier's suit). Weapons, ammo,
                // shields, aetheria and charms stay.
                const EquipMask keep = EquipMask.MeleeWeapon | EquipMask.MissileWeapon | EquipMask.MissileAmmo | EquipMask.Held
                                       | EquipMask.Shield | EquipMask.TwoHanded | EquipMask.SigilOne | EquipMask.SigilTwo | EquipMask.SigilThree;
                bool IsSuit(WorldObject i) => i.ValidLocations is { } v && v != EquipMask.None && (v & keep) == 0 && !i.IsAbilityCharm;
                var worn = player.EquippedObjects.Values.Where(IsSuit).ToList();
                foreach (var w in worn)
                {
                    player.TryDequipObjectWithNetworking(w.Guid, out _, Player.DequipObjectAction.ConsumeItem);
                    w.Destroy();
                }
                var loose = player.GetAllPossessions().Where(i => i.CurrentWieldedLocation == null && IsSuit(i)).ToList();
                foreach (var i in loose)
                {
                    player.TryRemoveFromInventoryWithNetworking(i.Guid, out _, Player.RemoveFromInventoryAction.ConsumeItem);
                    i.Destroy();
                }
                Say(player, $"dropgear: destroyed {worn.Count} worn and {loose.Count} carried suit pieces");
                return;
            }

            case "usedone":
                var ud = Bench.C.UseDone;
                lock (ud) { Say(player, "usedone: " + string.Join(", ", ud.Select(kv => kv.Key + "=" + kv.Value))); if (F(p, 1, 0) == 1) ud.Clear(); }
                return;

            case "shieldmult":
                if(F(p,1,1f)!=1f) throw new InvalidOperationException("Damage modifiers are excluded. Native damage only.");
                Say(player,"Native damage unchanged; test-only harness has no shield multiplier.");
                return;

            case "status":
                Say(player, $"fighting {Bench.Fighting} run {(Bench.Run != null ? Bench.RunLabel : "-")} spawns {Bench.Spawns.Count} alive {Bench.Spawns.Values.Count(t => t.Died == null)} " +
                            $"hp {player.Health.Current}/{player.Health.MaxValue} zone {Bench.ZoneName(player)}");
                return;

            case "trial":
            {
                // bench trial <label> <n> <rank> <layout> <dist> [timeout= power= spell= spell2= dot= wcid= clear= seed=]
                if (p.Length < 6) { Say(player, "Usage: /bench " + Usage); return; }
                var kv = p.Skip(6).Select(x => x.Split('=', 2)).Where(x => x.Length == 2).ToDictionary(x => x[0].ToLowerInvariant(), x => x[1]);
                float KF(string k, float d) => kv.TryGetValue(k, out var v) && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : d;
                // ids are whole numbers past float precision (730000573 as a float is 730000576): parse them exactly
                uint KU(string k) => kv.TryGetValue(k, out var v) && uint.TryParse(v, out var u) ? u : 0;
                Bench.Power = KF("power", 1f);
                Bench.SpellId = KU("spell");
                Bench.SpellId2 = KU("spell2");
                Bench.DotRefresh = KF("dot", 0);
                Bench.DotMax = (int)KF("dotmax", 0);
                Bench.C.LabCatalyst = KF("catalyst", 0) > 0;
                Bench.C.LabCombo = KF("combo", 0) > 0;
                Bench.DotSpell2 = KU("dot2");
                Bench.C.DotSpell3 = KU("dot3");
                Bench.Swap = KF("swap", 0) > 0;
                Bench.Seed = (int)KF("seed", 0);
                Bench.HealAt = KF("healat", 0.35f);
                // sustain=1 (2026-10-25): real heal / revitalize / mana boost casts costing castsec seconds each, vitals carried
                // between camps - instead of the instant free refills
                if(KF("sustain", 1) <= 0) throw new InvalidOperationException("Timed tests require real sustain; no automatic refills.");
                Bench.RealSustain = true;
                Bench.CastSeconds = Math.Clamp(KF("castsec", 2.5f), 0.5f, 10f);
                // camp=<leader>:<regular>[:<count>] - a real Tou Tou camp: its leader plus <count> (default 3) of its regulars
                List<uint>? picks = null;
                if (kv.TryGetValue("camp", out var camp))
                {
                    var parts = camp.Split(':');
                    if (parts.Length >= 2 && uint.TryParse(parts[0], out var lw) && uint.TryParse(parts[1], out var rw))
                    {
                        var n = parts.Length >= 3 && int.TryParse(parts[2], out var nn) ? nn : 3;
                        picks = new List<uint> { lw };
                        for (var i = 0; i < n; i++) picks.Add(rw);
                    }
                }
                Bench.StartTrial(player, p[1], I(p, 2, 5), Rank(p[3]), p[4], F(p, 5, 4), (int)KF("timeout", 150), KU("wcid"), KF("clear", 40), picks);
                Say(player, $"Trial {p[1]} started.");
                return;
            }

            default:
                Say(player, "Usage: /bench " + Usage);
                return;
        }
    }

    /// <summary>The worn-gear sums (ratings, Zone Control line props and their per-prop max) are caches kept on
    /// equip / dequip. After the bench rewrites worn pieces in place, rebuild them from scratch from what is worn now -
    /// the same Add calls the server makes on equip (Creature_Equipment: AddItemToEquippedItemsRatingCache,
    /// UpdateZoneModifierCache).</summary>
    internal static void RebuildWornCaches(Player player)
    {
        foreach (var f in new[] { "equippedItemsRatingCache", "equippedItemsZcRatingCache", "zoneModifierCache", "zoneModifierMaxCache" })
            AccessTools.Field(typeof(Creature), f).SetValue(player, null);
        foreach (var f in new[] { "equippedZcGearCount", "equippedZcWornCount" })
            AccessTools.Field(typeof(Creature), f).SetValue(player, 0);
        var addRating = AccessTools.Method(typeof(Creature), "AddItemToEquippedItemsRatingCache");
        var updZone = AccessTools.Method(typeof(Creature), "UpdateZoneModifierCache");
        foreach (var worn in player.EquippedObjects.Values.ToList())
        {
            addRating.Invoke(player, new object[] { worn });
            updZone.Invoke(player, new object[] { worn, 1 });
        }
    }
}
