using System;
using System.Globalization;
using System.Linq;
using System.Text;

using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Managers.Rifts;
using ACE.Server.Managers.ZoneScaling;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// /rift — Greater Rifts. Player-facing: status / leave / return / abandon. Everything else
    /// (open, close, pool, guardian, set, setloot, show, reload) is gated to Developer+ inside the handler
    /// so the one command serves both audiences.
    /// </summary>
    public static class RiftCommands
    {
        [CommandHandler("rift", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Greater Rifts.",
            "(blank = status) | leave | return | abandon"
            + " || DEV: open <tier> [lbHex] | close [player] | list | pool add here <name> | pool guardianpos | pool remove <hex> | pool list | "
            + "guardian add <minTier> <maxTier> <wcid> | guardian remove <wcid> | guardian list | "
            + "set <key> <value> | setloot <stat> <base> <perTier> | setloot <stat> clear | show | reload")]
        public static void HandleRift(Session session, params string[] parameters)
        {
            var player = session?.Player;
            if (player == null)
                return;

            var sub = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "status";

            switch (sub)
            {
                // ── player-facing ──
                case "status":
                    Reply(session, RiftManager.BuildStatus(player));
                    return;

                case "leave":
                    Reply(session, RiftManager.Leave(player) ?? "You step out of the rift.");
                    return;

                case "return":
                    Reply(session, RiftManager.ReEnter(player) ?? "You return to your rift.");
                    return;

                case "abandon":
                    Reply(session, RiftManager.Abandon(player) ?? "Rift abandoned.");
                    return;
            }

            if (session.AccessLevel < AccessLevel.Developer)
            {
                Reply(session, "Usage: /rift (status) | leave | return | abandon");
                return;
            }

            switch (sub)
            {
                case "open":
                    HandleOpen(session, parameters);
                    return;
                case "close":
                    HandleClose(session, parameters);
                    return;
                case "list":
                    HandleList(session);
                    return;
                case "pool":
                    HandlePool(session, parameters);
                    return;
                case "guardian":
                    HandleGuardian(session, parameters);
                    return;
                case "set":
                    HandleSet(session, parameters);
                    return;
                case "setloot":
                    HandleSetLoot(session, parameters);
                    return;
                case "show":
                    HandleShow(session);
                    return;
                case "reload":
                    RiftManager.ReloadConfig();
                    Reply(session, "Rift config reloaded from the shard store.");
                    return;
                default:
                    Reply(session, "Unknown /rift subcommand. See /rift help in the command usage.");
                    return;
            }
        }

        private static void HandleOpen(Session session, string[] p)
        {
            if (p.Length < 2 || !int.TryParse(p[1], out var tier))
            {
                Reply(session, "Usage: /rift open <tier> [lbHex]");
                return;
            }

            ushort? lb = null;
            if (p.Length >= 3)
            {
                if (!TryParseLandblock(p[2], out var parsed))
                {
                    Reply(session, $"Could not parse landblock '{p[2]}' (expected hex, e.g. 01AB).");
                    return;
                }
                lb = parsed;
            }

            var err = RiftManager.OpenRun(session.Player, tier, lb);
            if (err != null)
                Reply(session, err);
        }

        private static void HandleClose(Session session, string[] p)
        {
            RiftRun run;
            if (p.Length >= 2)
            {
                var target = PlayerManager.GetOnlinePlayer(string.Join(" ", p.Skip(1)));
                if (target == null)
                {
                    Reply(session, "Player not found online.");
                    return;
                }
                run = RiftManager.GetRunByOwner(target.Guid.Full);
            }
            else
            {
                run = RiftManager.GetRunByOwner(session.Player.Guid.Full);
            }

            if (run == null)
            {
                Reply(session, "No open rift found for that player.");
                return;
            }

            RiftManager.ForceClose(run);
            Reply(session, $"Closed rift run {run.RunId} (tier {run.Tier}, {run.OwnerName}).");
        }

        private static void HandleList(Session session)
        {
            var runs = RiftManager.GetActiveRuns();
            if (runs.Count == 0)
            {
                Reply(session, "No rifts are open.");
                return;
            }

            var sb = new StringBuilder($"Open rifts ({runs.Count}):\n");
            foreach (var run in runs.OrderBy(r => r.RunId))
                sb.AppendLine($"  #{run.RunId} {run.OwnerName} - tier {run.Tier} {run.DungeonName} ({run.DungeonLb:X4} v{run.Variation}) {run.State} {run.ProgressPercent}%");
            Reply(session, sb.ToString().TrimEnd());
        }

        private static void HandlePool(Session session, string[] p)
        {
            var cfg = RiftManager.Config;
            var action = p.Length > 1 ? p[1].ToLowerInvariant() : "list";

            switch (action)
            {
                case "add":
                {
                    // /rift pool add here <name...>
                    if (p.Length < 3 || !p[2].Equals("here", StringComparison.OrdinalIgnoreCase))
                    {
                        Reply(session, "Usage: /rift pool add here <name> (stand at the desired entry point inside the dungeon)");
                        return;
                    }

                    var loc = session.Player.Location;
                    var lbObj = session.Player.CurrentLandblock;
                    if (loc == null || lbObj == null)
                        return;

                    if (!loc.Indoors || lbObj.PhysicsLandblock?.IsDungeon != true)
                    {
                        Reply(session, "Rift pool dungeons must be added from INSIDE a dungeon landblock.");
                        return;
                    }

                    var lb = loc.LandblockId.Landblock;
                    var name = p.Length > 3 ? string.Join(" ", p.Skip(3)) : lb.ToString("X4");

                    // population source = the variation you're standing in (0 normalizes to the base rows);
                    // instance rows are per-variation exact-match, so this decides which copy's monsters
                    // populate rift runs of this dungeon
                    var sourceVariation = loc.Variation is int v && v != 0 ? loc.Variation : null;
                    var sourceLabel = sourceVariation?.ToString() ?? "base";

                    var existing = cfg.DungeonPool.FirstOrDefault(d => d.Landblock == lb);
                    if (existing != null)
                    {
                        existing.Name = name;
                        existing.Entry = RiftPos.FromPosition(loc);
                        existing.SourceVariation = sourceVariation;
                        RiftManager.SaveConfig();
                        Reply(session, $"Updated pool dungeon {name} ({lb:X4}): entry point set to your position, population source v:{sourceLabel}.");
                        return;
                    }

                    cfg.DungeonPool.Add(new RiftDungeonEntry
                    {
                        Landblock = lb,
                        Name = name,
                        Entry = RiftPos.FromPosition(loc),
                        SourceVariation = sourceVariation,
                    });
                    RiftManager.SaveConfig();
                    Reply(session, $"Added pool dungeon {name} ({lb:X4}), entry at your position, population source v:{sourceLabel}. Now stand at the guardian spot and run: /rift pool guardianpos");
                    return;
                }

                case "guardianpos":
                {
                    var loc = session.Player.Location;
                    if (loc == null)
                        return;

                    var lb = loc.LandblockId.Landblock;
                    var entry = cfg.DungeonPool.FirstOrDefault(d => d.Landblock == lb);
                    if (entry == null)
                    {
                        Reply(session, $"Landblock {lb:X4} is not in the pool. Add it first: /rift pool add here <name>");
                        return;
                    }

                    entry.Guardian = RiftPos.FromPosition(loc);
                    RiftManager.SaveConfig();
                    Reply(session, $"Guardian spawn for {entry.Name} ({lb:X4}) set to your position.");
                    return;
                }

                case "remove":
                {
                    if (p.Length < 3 || !TryParseLandblock(p[2], out var lb))
                    {
                        Reply(session, "Usage: /rift pool remove <lbHex>");
                        return;
                    }

                    var removed = cfg.DungeonPool.RemoveAll(d => d.Landblock == lb);
                    if (removed > 0)
                        RiftManager.SaveConfig();
                    Reply(session, removed > 0 ? $"Removed {lb:X4} from the pool." : $"{lb:X4} was not in the pool.");
                    return;
                }

                case "list":
                default:
                {
                    if (cfg.DungeonPool.Count == 0)
                    {
                        Reply(session, "The rift dungeon pool is empty. Stand inside a dungeon and use: /rift pool add here <name>");
                        return;
                    }

                    var sb = new StringBuilder($"Rift dungeon pool ({cfg.DungeonPool.Count}):\n");
                    foreach (var d in cfg.DungeonPool)
                        sb.AppendLine($"  {d.Name} ({d.Landblock:X4}) source v:{d.SourceVariation?.ToString() ?? "base"} entry:{(d.Entry != null ? "set" : "MISSING")} guardian:{(d.Guardian != null ? "set" : "entry fallback")}");
                    Reply(session, sb.ToString().TrimEnd());
                    return;
                }
            }
        }

        private static void HandleGuardian(Session session, string[] p)
        {
            var cfg = RiftManager.Config;
            var action = p.Length > 1 ? p[1].ToLowerInvariant() : "list";

            switch (action)
            {
                case "add":
                {
                    if (p.Length < 5 || !int.TryParse(p[2], out var min) || !int.TryParse(p[3], out var max) || !uint.TryParse(p[4], out var wcid))
                    {
                        Reply(session, "Usage: /rift guardian add <minTier> <maxTier> <wcid>");
                        return;
                    }

                    var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
                    if (weenie == null)
                    {
                        Reply(session, $"No weenie {wcid} exists.");
                        return;
                    }

                    var pool = cfg.GuardianPools.FirstOrDefault(g => g.MinTier == min && g.MaxTier == max);
                    if (pool == null)
                    {
                        pool = new RiftGuardianPool { MinTier = min, MaxTier = max };
                        cfg.GuardianPools.Add(pool);
                    }

                    if (!pool.Wcids.Contains(wcid))
                        pool.Wcids.Add(wcid);

                    RiftManager.SaveConfig();
                    Reply(session, $"Added guardian {weenie.GetName()} ({wcid}) for tiers {min}-{max}.");
                    return;
                }

                case "remove":
                {
                    if (p.Length < 3 || !uint.TryParse(p[2], out var wcid))
                    {
                        Reply(session, "Usage: /rift guardian remove <wcid>");
                        return;
                    }

                    var removed = 0;
                    foreach (var pool in cfg.GuardianPools)
                        removed += pool.Wcids.RemoveAll(w => w == wcid);
                    if (removed > 0)
                    {
                        cfg.GuardianPools.RemoveAll(g => g.Wcids.Count == 0);   // prune only when something changed, then persist
                        RiftManager.SaveConfig();
                    }
                    Reply(session, removed > 0 ? $"Removed wcid {wcid} from {removed} guardian pool(s)." : $"Wcid {wcid} was not in any guardian pool.");
                    return;
                }

                case "list":
                default:
                {
                    if (cfg.GuardianPools.Count == 0)
                    {
                        Reply(session, "No guardian pools configured. Use: /rift guardian add <minTier> <maxTier> <wcid>");
                        return;
                    }

                    var sb = new StringBuilder($"Guardian pools ({cfg.GuardianPools.Count}):\n");
                    foreach (var g in cfg.GuardianPools.OrderBy(g => g.MinTier))
                        sb.AppendLine($"  tiers {g.MinTier}-{g.MaxTier}: {string.Join(", ", g.Wcids)}");
                    Reply(session, sb.ToString().TrimEnd());
                    return;
                }
            }
        }

        private static void HandleSet(Session session, string[] p)
        {
            if (p.Length < 3)
            {
                Reply(session, "Usage: /rift set <key> <value>. Keys: timer grace maxruns progressbase progresspertier progressperkill hpgrowth dmgratingpertier guardianhpmult guardiandmgbonus currencywcid currencybase currencypertier");
                return;
            }

            var cfg = RiftManager.Config;
            var key = p[1].ToLowerInvariant();
            var raw = p[2];

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            {
                Reply(session, $"Could not parse value '{raw}'.");
                return;
            }

            // validate before anything is persisted: no negatives, integer keys must be whole and in range, and the
            // wcid must survive its uint cast (a negative wraps to a huge value that survives a restart)
            if (double.IsNaN(val) || double.IsInfinity(val) || val < 0)
            {
                Reply(session, $"'{key}' must be a number of 0 or more.");
                return;
            }
            var integerKey = key is "timer" or "grace" or "maxruns" or "currencywcid";
            if (integerKey && val != Math.Floor(val))
            {
                Reply(session, $"'{key}' must be a whole number.");
                return;
            }
            if ((key is "timer" or "grace" or "maxruns") && val > int.MaxValue || key == "currencywcid" && val > uint.MaxValue)
            {
                Reply(session, $"'{key}' is out of range.");
                return;
            }
            switch (key)
            {
                case "timer": cfg.TimerSeconds = (int)val; break;
                case "grace": cfg.GraceSeconds = (int)val; break;
                case "maxruns": cfg.MaxActiveRuns = (int)val; break;
                case "progressbase": cfg.ProgressBase = val; break;
                case "progresspertier": cfg.ProgressPerTier = val; break;
                case "progressperkill": cfg.ProgressPerKill = val; break;
                case "hpgrowth": cfg.HpGrowth = val; break;
                case "dmgratingpertier": cfg.DamageRatingPerTier = val; break;
                case "guardianhpmult": cfg.GuardianHpMult = val; break;
                case "guardiandmgbonus": cfg.GuardianDamageRatingBonus = val; break;
                case "currencywcid": cfg.CurrencyWcid = (uint)val; break;
                case "currencybase": cfg.CurrencyBase = val; break;
                case "currencypertier": cfg.CurrencyPerTier = val; break;
                default:
                    Reply(session, $"Unknown key '{key}'.");
                    return;
            }

            RiftManager.SaveConfig();
            Reply(session, $"Set {key} = {val}. (Applies to runs opened from now on.)");
        }

        private static void HandleSetLoot(Session session, string[] p)
        {
            if (p.Length < 3)
            {
                Reply(session, "Usage: /rift setloot <stat> <base> <perTier>  |  /rift setloot <stat> clear");
                return;
            }

            var cfg = RiftManager.Config;
            var stat = p[1].ToLowerInvariant();

            if (!ZoneStat.All.Contains(stat, StringComparer.OrdinalIgnoreCase))
            {
                Reply(session, $"'{stat}' is not a known zone stat.");
                return;
            }

            if (p[2].Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                var removed = cfg.LootStatBase.Remove(stat) | cfg.LootStatPerTier.Remove(stat);
                if (removed)
                    RiftManager.SaveConfig();
                Reply(session, removed ? $"Cleared rift loot stat {stat}." : $"{stat} was not set.");
                return;
            }

            if (p.Length < 4 ||
                !double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var baseVal) ||
                !double.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var perTier))
            {
                Reply(session, "Usage: /rift setloot <stat> <base> <perTier>");
                return;
            }

            cfg.LootStatBase[stat] = baseVal;
            cfg.LootStatPerTier[stat] = perTier;
            RiftManager.SaveConfig();
            Reply(session, $"Rift loot stat {stat} = {baseVal} + {perTier} * tier. (Applies to runs opened from now on.)");
        }

        private static void HandleShow(Session session)
        {
            var cfg = RiftManager.Config;
            var sb = new StringBuilder("Rift config:\n");
            sb.AppendLine($"  timer {cfg.TimerSeconds}s | grace {cfg.GraceSeconds}s | maxruns {cfg.MaxActiveRuns}");
            sb.AppendLine($"  progress: {cfg.ProgressBase} + {cfg.ProgressPerTier}/tier, {cfg.ProgressPerKill}/kill");
            sb.AppendLine($"  scaling: hp x{cfg.HpGrowth}^tier | +{cfg.DamageRatingPerTier} dmg rating/tier | guardian hp x{cfg.GuardianHpMult}, +{cfg.GuardianDamageRatingBonus} rating");
            sb.AppendLine($"  currency: wcid {cfg.CurrencyWcid}, {cfg.CurrencyBase} + {cfg.CurrencyPerTier}/tier");
            sb.AppendLine($"  loot stats ({cfg.LootStatBase.Count}):");
            foreach (var kv in cfg.LootStatBase)
            {
                cfg.LootStatPerTier.TryGetValue(kv.Key, out var per);
                sb.AppendLine($"    {kv.Key} = {kv.Value} + {per}/tier");
            }
            sb.AppendLine($"  dungeons: {cfg.DungeonPool.Count} | guardian pools: {cfg.GuardianPools.Count} | open runs: {RiftManager.GetActiveRuns().Count}");
            Reply(session, sb.ToString().TrimEnd());
        }

        private static bool TryParseLandblock(string s, out ushort lb)
        {
            s = s?.Trim() ?? "";
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);
            return ushort.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out lb);
        }

        private static void Reply(Session session, string msg)
        {
            session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        }
    }
}
