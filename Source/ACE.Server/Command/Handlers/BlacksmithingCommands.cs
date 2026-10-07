using System;
using System.Linq;
using System.Text;

using log4net;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Services;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Blacksmithing prototype (2026-09-28): developer tools for trying weapon dyes in game before the
    /// forge itself exists. A dye is PropertyInt.ForgeDyePalette, painted at render time by
    /// WorldObject.ApplyForgeDye (which also swaps the three full-colour textures for their indexed twins);
    /// ServerConfig.forge_dye_mode picks 'full' or 'ranges'.
    /// </summary>
    public static class BlacksmithingCommands
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [CommandHandler("dye_weapon", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld,
            "Dyes the last appraised weapon with a full 0x04 palette (blacksmithing prototype).",
            "[paletteId|random|vibrant|clear]\n" +
            "Example: @dye_weapon 0x0400001D\n" +
            "random = any palette from the pet mutation pool, vibrant = the Chromatic Catalyst pool (the default), clear = restore the retail look.\n" +
            "The paint mode is ServerConfig forge_dye_mode: @modifystring forge_dye_mode full|ranges")]
        public static void HandleDyeWeapon(Session session, params string[] parameters)
        {
            var player = session.Player;
            var target = CommandHandlerHelper.GetLastAppraisedObject(session);
            if (target == null || !(target is MeleeWeapon || target is MissileLauncher || target is Caster))
            {
                ChatPacket.SendServerMessage(session, "Appraise a weapon first (melee, missile launcher or caster).", ChatMessageType.System);
                return;
            }

            var arg = parameters.Length > 0 ? parameters[0] : "vibrant";
            var sb = new StringBuilder();
            sb.Append($"=== @dye_weapon: {target.Name} (WCID {target.WeenieClassId}) ===\n");

            if (arg.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                target.ForgeDyePalette = null;
                sb.Append("Dye removed; retail look restored.\n");
            }
            else
            {
                uint palette;
                if (arg.Equals("random", StringComparison.OrdinalIgnoreCase) || arg.Equals("vibrant", StringComparison.OrdinalIgnoreCase))
                {
                    var pool = arg.Equals("vibrant", StringComparison.OrdinalIgnoreCase)
                        ? PetMutationService.GetVibrantPalettePool()
                        : PetMutationService.GetMasterPalettePool();
                    if (pool.Count == 0)
                    {
                        ChatPacket.SendServerMessage(session, "The palette pool is empty; pass a palette id instead.", ChatMessageType.System);
                        return;
                    }
                    palette = pool[ACE.Common.ThreadSafeRandom.Next(0, pool.Count - 1)].PaletteId;
                    sb.Append($"Rolled from the {arg.ToLowerInvariant()} pool ({pool.Count} palettes).\n");
                }
                else if (!TryParseUInt(arg, out palette) || (palette & 0xFF000000) != 0x04000000)
                {
                    ChatPacket.SendServerMessage(session, "Palette must be a 0x04xxxxxx palette id, or random / vibrant / clear.", ChatMessageType.System);
                    return;
                }

                target.ForgeDyePalette = (int)palette;
                sb.Append($"Palette 0x{palette:X8}\n");
            }

            DescribeTarget(sb, target);
            SaveAndRedraw(player, target);
            sb.Append("If the colour does not change, unwield and re-wield the weapon.");

            var msg = sb.ToString();
            ChatPacket.SendServerMessage(session, msg, ChatMessageType.System);
            log.Info($"[DyeWeapon] {player.Name}:\n{msg}");
        }

        [CommandHandler("dye_armor", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld,
            "Dyes armour or clothing with a 0x04 palette, painted only on the ranges the piece recolours (blacksmithing prototype).",
            "[worn] [paletteId|random|vibrant|clear]\n" +
            "Without 'worn' it acts on the last appraised piece. With 'worn' it gives every worn piece the same palette.\n" +
            "Example: @dye_armor worn vibrant")]
        public static void HandleDyeArmor(Session session, params string[] parameters)
        {
            var player = session.Player;
            var args = parameters.ToList();
            var worn = args.RemoveAll(a => a.Equals("worn", StringComparison.OrdinalIgnoreCase)) > 0;
            var arg = args.Count > 0 ? args[0] : "vibrant";

            bool IsGarment(WorldObject wo) => wo != null && !(wo is MeleeWeapon || wo is MissileLauncher || wo is Caster)
                && ((wo.ValidLocations ?? EquipMask.None) & (EquipMask.Clothing | EquipMask.Armor | EquipMask.Cloak)) != 0;

            var targets = new System.Collections.Generic.List<WorldObject>();
            if (worn)
                targets.AddRange(player.EquippedObjects.Values.Where(IsGarment));
            else if (IsGarment(CommandHandlerHelper.GetLastAppraisedObject(session)))
                targets.Add(CommandHandlerHelper.GetLastAppraisedObject(session));
            if (targets.Count == 0)
            {
                ChatPacket.SendServerMessage(session, worn ? "You are wearing no armour or clothing." : "Appraise a piece of armour or clothing first, or use: @dye_armor worn", ChatMessageType.System);
                return;
            }

            var sb = new StringBuilder();
            sb.Append($"=== @dye_armor: {targets.Count} piece{(targets.Count == 1 ? "" : "s")} ===\n");

            uint? palette = null;
            if (!arg.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                if (arg.Equals("random", StringComparison.OrdinalIgnoreCase) || arg.Equals("vibrant", StringComparison.OrdinalIgnoreCase))
                {
                    var pool = arg.Equals("vibrant", StringComparison.OrdinalIgnoreCase)
                        ? PetMutationService.GetVibrantPalettePool()
                        : PetMutationService.GetMasterPalettePool();
                    if (pool.Count == 0)
                    {
                        ChatPacket.SendServerMessage(session, "The palette pool is empty; pass a palette id instead.", ChatMessageType.System);
                        return;
                    }
                    palette = pool[ACE.Common.ThreadSafeRandom.Next(0, pool.Count - 1)].PaletteId;
                }
                else if (TryParseUInt(arg, out var parsed) && (parsed & 0xFF000000) == 0x04000000)
                    palette = parsed;
                else
                {
                    ChatPacket.SendServerMessage(session, "Palette must be a 0x04xxxxxx palette id, or random / vibrant / clear.", ChatMessageType.System);
                    return;
                }
                sb.Append($"Palette 0x{palette.Value:X8} (repeat it with: @dye_armor {(worn ? "worn " : "")}0x{palette.Value:X8})\n");
            }
            else
                sb.Append("Dye removed; retail look restored.\n");

            foreach (var target in targets)
            {
                // What the dye can reach: the ranges of the colour option the piece is drawn with.
                var colours = 0;
                var ranges = 0;
                if (target.ClothingBase.HasValue && DatManager.PortalDat.TryReadClothingTable(target.ClothingBase.Value, out ClothingTable table) && table.ClothingSubPalEffects.Count > 0)
                {
                    var key = (uint)(target.PaletteTemplate ?? 0);
                    var option = table.ClothingSubPalEffects.TryGetValue(key, out var o) ? o : table.ClothingSubPalEffects[table.ClothingSubPalEffects.Keys.ElementAt(0)];
                    foreach (var sp in option.CloSubPalettes)
                        foreach (var r in sp.Ranges)
                        {
                            ranges++;
                            colours += (int)r.NumColors;
                        }
                }

                target.ForgeDyePalette = palette.HasValue ? (int)palette.Value : null;
                target.ChangesDetected = true;
                target.SaveBiotaToDatabase();
                player.EnqueueBroadcast(new GameMessageUpdateObject(target));
                if (target.CurrentWieldedLocation == null && player.FindObject(target.Guid.Full, Player.SearchLocations.MyInventory) != null)
                    player.MoveItemToFirstContainerSlot(target);

                sb.Append(ranges == 0
                    ? $"[WARNING] {target.Name}: no recolourable ranges, the dye cannot show.\n"
                    : $"{target.Name}: {ranges} range{(ranges == 1 ? "" : "s")}, {colours} of 2048 colours{(target.CurrentWieldedLocation != null ? ", worn" : "")}\n");
            }

            // one redraw of the wearer covers every worn piece
            if (targets.Any(t => t.CurrentWieldedLocation != null))
                player.EnqueueBroadcast(new GameMessageObjDescEvent(player));

            var msg = sb.ToString().TrimEnd();
            ChatPacket.SendServerMessage(session, msg, ChatMessageType.System);
            log.Info($"[DyeArmor] {player.Name}:\n{msg}");
        }

        /// <summary>Per player: the weapon picked as the MAIN one by the first @forge_preview.</summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, uint> previewMain = new();

        [CommandHandler("forge_preview", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld,
            "Shows what forging two copies of a weapon would give, without changing either (blacksmithing prototype).",
            "[clear]\n" +
            "1. Appraise the MAIN weapon (it keeps tinkers, imbue, dye and hone levels) and run @forge_preview.\n" +
            "2. Appraise the second copy and run @forge_preview again.")]
        public static void HandleForgePreview(Session session, params string[] parameters)
        {
            var player = session.Player;
            if (parameters.Length > 0 && parameters[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                previewMain.TryRemove(player.Guid.Full, out _);
                ChatPacket.SendServerMessage(session, "Forge preview cleared.", ChatMessageType.System);
                return;
            }

            var target = CommandHandlerHelper.GetLastAppraisedObject(session);
            if (target == null)
            {
                ChatPacket.SendServerMessage(session, "Appraise a weapon first.", ChatMessageType.System);
                return;
            }

            WorldObject main = null;
            if (previewMain.TryGetValue(player.Guid.Full, out var mainGuid) && mainGuid != target.Guid.Full)
                main = player.FindObject(mainGuid, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems);

            if (main == null)
            {
                var reason = ForgeWeaponReader.RefusalReason(target);
                if (reason != null)
                {
                    ChatPacket.SendServerMessage(session, reason, ChatMessageType.System);
                    return;
                }
                previewMain[player.Guid.Full] = target.Guid.Full;
                ChatPacket.SendServerMessage(session, $"Main weapon: {target.Name}. Now appraise the second copy and run @forge_preview again.", ChatMessageType.System);
                return;
            }

            var refusal = ForgeWeaponReader.RefusalReason(target);
            refusal ??= ForgeWeaponReader.PairRefusalReason(main, target);
            if (refusal != null)
            {
                ChatPacket.SendServerMessage(session, refusal, ChatMessageType.System);
                return;
            }

            var config = ForgeMath.ForgeConfig.FromServerConfig();
            var m = ForgeWeaponReader.Read(main);
            var f = ForgeWeaponReader.Read(target);
            var p = config.HigherParentChance;

            var sb = new StringBuilder();
            sb.Append("=== Forge preview ===\n");
            sb.Append($"Main:   {main.Name} ({Describe(m)})\n");
            sb.Append($"Feeder: {target.Name} ({Describe(f)})\n");
            sb.Append($"Forge group: {m.Group}\n");
            if (m.TinkerCount > 0 || f.TinkerCount > 0)
                sb.Append("Stats below are untinkered; the main weapon's tinkers are applied again after the forge.\n");

            var blend = config.RollMode != ForgeMath.RollMode.Pick;
            sb.Append(blend
                ? $"Stat: main / feeder -> rolled between the two ({(config.RollMode == ForgeMath.RollMode.BestOfTwo ? "best of two rolls, leaning to the better value" : "anywhere in between")})\n"
                : "Stat: main / feeder -> chance the result keeps the main value\n");
            foreach (var line in ForgeMath.OrderFor(m.IsArmor))
            {
                var mh = m.Lines.TryGetValue(line, out var mv);
                var fh = f.Lines.TryGetValue(line, out var fv);
                if (!mh && !fh)
                    continue;
                var mainBetter = mh != fh ? mh : (ForgeMath.LowerIsBetter(line) ? mv <= fv : mv >= fv);
                var odds = blend && mh && fh ? (mv == fv ? "unchanged" : "between") : ForgeMath.ChanceFromMain(mainBetter, p).ToString("P0");
                sb.Append($"  {ForgeMath.LineName(line)}: {(mh ? Num(mv) : "none")} / {(fh ? Num(fv) : "none")} -> {odds}\n");
            }
            if (m.Quality.HasValue || f.Quality.HasValue)
            {
                int mq = m.Quality ?? 0, fq = f.Quality ?? 0;
                sb.Append($"  Quality: {Grade(mq)} / {Grade(fq)} -> {(blend ? (mq == fq ? "unchanged" : "between") : ForgeMath.ChanceFromMain(mq >= fq, p).ToString("P0"))}\n");
            }

            var families = m.Spells.Keys.Union(f.Spells.Keys).OrderBy(k => k).ToList();
            if (families.Count > 0)
            {
                sb.Append("Spells, per family:\n");
                foreach (var fam in families)
                {
                    m.Spells.TryGetValue(fam, out var ms);
                    f.Spells.TryGetValue(fam, out var fs);
                    var name = ForgeWeaponReader.FamilyName(fam);
                    if (ms != null && fs != null)
                        sb.Append($"  {name}: {SpellName(ms)} / {SpellName(fs)} -> {ForgeMath.ChanceFromMain(ms.Level >= fs.Level, p):P0} keeps main\n");
                    else
                        sb.Append($"  {name}: only on the {(ms != null ? "main" : "feeder")} weapon ({SpellName(ms ?? fs)}) -> {p:P0} kept, {1 - p:P0} lost\n");
                }
            }

            // Element: a plain 50/50; a changed element turns the main weapon into its model's version in that element.
            var feederElementWcid = ForgeGroups.FindElementVariant(main.WeenieClassId, (ACE.Entity.Enum.DamageType)f.Element);
            if (m.Element != f.Element && m.Rend != 0)
                sb.Append($"Element: {(ACE.Entity.Enum.DamageType)m.Element} / {(ACE.Entity.Enum.DamageType)f.Element} -> stays {(ACE.Entity.Enum.DamageType)m.Element}: the main weapon carries {ForgeWeaponReader.RendName(m.Rend)}.\n");
            else if (m.Element != f.Element)
            {
                var flipName = feederElementWcid.HasValue ? DatabaseName(feederElementWcid.Value) : null;
                sb.Append($"Element: {(ACE.Entity.Enum.DamageType)m.Element} / {(ACE.Entity.Enum.DamageType)f.Element} -> 50% each. " +
                          (flipName != null ? $"On {(ACE.Entity.Enum.DamageType)f.Element} the result becomes a {flipName}.\n"
                                            : $"{main.Name} has no {(ACE.Entity.Enum.DamageType)f.Element} version, so it keeps its own element.\n"));
            }

            foreach (var key in m.Packages.Keys.Union(f.Packages.Keys).OrderBy(k => k))
            {
                m.Packages.TryGetValue(key, out var mp);
                f.Packages.TryGetValue(key, out var fp);
                sb.Append($"  {mp?.Value ?? "none"} / {fp?.Value ?? "none"}\n");
            }

            // Everything that is never rolled.
            var tier = Math.Max(m.Tier, f.Tier);
            var wield = new System.Collections.Generic.SortedDictionary<int, int>(m.WieldDifficulty);
            foreach (var kv in f.WieldDifficulty)
                wield[kv.Key] = wield.TryGetValue(kv.Key, out var cur) ? Math.Max(cur, kv.Value) : kv.Value;
            sb.Append($"Result (no roll): tier {(tier > 0 ? tier.ToString() : "below 11")}, wield requirements {(wield.Count == 0 ? "none" : string.Join(", ", wield.Values))}\n");
            sb.Append($"Kept from the main weapon: {m.TinkerCount} tinkers, imbue {(m.ImbuedEffect != 0 ? m.ImbuedEffect.ToString() : "none")}, dye {(m.DyePalette.HasValue ? "yes" : "none")}\n");
            if (f.Tier > m.Tier)
                sb.Append("[WARNING] The result takes the feeder's higher tier and its requirements.\n");
            if ((m.Quality ?? 0) > 0 && !f.Quality.HasValue)
                sb.Append($"[WARNING] The feeder has no quality: {1 - p:P0} chance the result's quality drops to 0.\n");

            // One sample roll with the real math.
            var sample = ForgeMath.Forge(m, f, config, () => ACE.Common.ThreadSafeRandom.Next(0.0f, 1.0f));
            var r = sample.Result;
            var resultWcid = r.Element == m.Element ? main.WeenieClassId : (feederElementWcid ?? main.WeenieClassId);
            sb.Append($"Sample roll: {DatabaseName(resultWcid)}: ");
            sb.Append(string.Join(", ", ForgeMath.OrderFor(r.IsArmor).Where(r.Lines.ContainsKey).Select(l => $"{ForgeMath.LineName(l)} {Num(r.Lines[l])}")));
            if (r.Quality.HasValue)
                sb.Append($", Quality {Grade(r.Quality.Value)}");
            sb.Append($"; {r.Spells.Count} spell famil{(r.Spells.Count == 1 ? "y" : "ies")}");
            sb.Append(sample.Spark ? $"; SPARK: +1 hone on {ForgeMath.LineName(sample.SparkLine)}\n" : "\n");

            var msg = sb.ToString();
            ChatPacket.SendServerMessage(session, msg, ChatMessageType.System);
            log.Info($"[ForgePreview] {player.Name}:\n{msg}");
        }

        [CommandHandler("forge_now", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld,
            "Forges two weapons for real, free and without a smith (blacksmithing developer tool). The second weapon is destroyed.",
            "[clear]\n" +
            "1. Appraise the MAIN weapon (it keeps tinkers, imbue, dye and hone levels) and run @forge_now.\n" +
            "2. Appraise the second weapon and run @forge_now again, then confirm.\n" +
            "Both must be in your pack (not wielded) and not in a trade window. Players do the same by handing weapons to a forge smith.")]
        public static void HandleForgeNow(Session session, params string[] parameters)
        {
            var player = session.Player;
            if (parameters.Length > 0 && parameters[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                previewMain.TryRemove(player.Guid.Full, out _);
                ForgeService.ClearSelection(player);
                ChatPacket.SendServerMessage(session, "Forge selection cleared.", ChatMessageType.System);
                return;
            }

            var target = CommandHandlerHelper.GetLastAppraisedObject(session);
            if (target == null)
            {
                ChatPacket.SendServerMessage(session, "Appraise a weapon first.", ChatMessageType.System);
                return;
            }

            WorldObject main = null;
            if (previewMain.TryGetValue(player.Guid.Full, out var mainGuid) && mainGuid != target.Guid.Full)
                main = player.FindObject(mainGuid, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems);
            if (main == null)
            {
                var reason = ForgeWeaponReader.RefusalReason(target);
                if (reason != null)
                {
                    ChatPacket.SendServerMessage(session, reason, ChatMessageType.System);
                    return;
                }
                previewMain[player.Guid.Full] = target.Guid.Full;
                ChatPacket.SendServerMessage(session, $"Main weapon: {target.Name}. Now appraise the second weapon and run @forge_now again.", ChatMessageType.System);
                return;
            }

            // Same checks, popup and forge a smith uses; no smith and no fee.
            ForgeService.Offer(player, null, main, target, 0);
            previewMain.TryRemove(player.Guid.Full, out _);
        }

        private static string DatabaseName(uint wcid)
            => ACE.Database.DatabaseManager.World.GetCachedWeenie(wcid)?.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name) ?? $"WCID {wcid}";

        private static string Describe(ForgeMath.ForgeWeapon w)
            => $"tier {(w.Tier > 0 ? w.Tier.ToString() : "<11")}" + (w.Quality.HasValue ? $", quality {Grade(w.Quality.Value)}" : "") + (w.TinkerCount > 0 ? $", {w.TinkerCount} tinkers" : "");

        private static string Grade(int quality) => $"{ACE.Server.Managers.WeaponScaling.WeaponScalingManager.GetQualitySubGrade(quality)} ({quality})";

        private static string Num(double v) => v == Math.Floor(v) ? ((long)v).ToString() : v.ToString("0.###");

        private static string SpellName(ForgeMath.SpellEntry s)
        {
            var spell = new Entity.Spell(s.SpellId, false);
            return spell.NotFound ? $"spell {s.SpellId}" : spell.Name;
        }

        /// <summary>What the dye will paint on this weapon under the current mode, so a test result can be read back.</summary>
        private static void DescribeTarget(StringBuilder sb, WorldObject target)
        {
            var mode = ServerConfig.forge_dye_mode.Value;
            sb.Append($"Mode: {mode}\n");
            sb.Append($"Setup 0x{target.SetupTableId:X8}, PaletteBase 0x{target.PaletteBaseId ?? 0:X8}, PaletteTemplate {target.PaletteTemplate?.ToString() ?? "none"}, Shade {target.Shade?.ToString("F2") ?? "none"}\n");

            // The same full-colour -> indexed twin swaps ApplyForgeDye makes, and how much of the model a dye then reaches.
            var twins = WorldObject.GetDyeTwinSwaps(target.SetupTableId);
            foreach (var (part, oldTex, newTex) in twins)
                sb.Append($"Texture fix: part {part} 0x{oldTex:X8} -> 0x{newTex:X8} (full-colour texture traded for its dyeable twin)\n");
            var packed = string.Join(",", twins.Select(t => $"{t.Part}:{t.Old}:{t.New}"));
            var (fixedPolys, drawnPolys) = PetMutationService.MeasureFixedColourPolygons(target.SetupTableId, null, packed);
            if (drawnPolys > 0)
            {
                var blocked = PetMutationService.IsFixedColourModel(fixedPolys, drawnPolys);
                sb.Append($"{(blocked ? "[WARNING] " : "")}{100 * fixedPolys / drawnPolys}% of this model ignores dye{(blocked ? ": the colour will barely show." : ".")}\n");
            }

            if (!target.ClothingBase.HasValue || !DatManager.PortalDat.TryReadClothingTable(target.ClothingBase.Value, out ClothingTable table))
            {
                sb.Append("No ClothingBase: the dye always paints the full palette on this weapon.\n");
                return;
            }

            sb.Append($"ClothingBase 0x{target.ClothingBase.Value:X8}: {table.ClothingSubPalEffects.Count} retail colour options, setup {(table.ClothingBaseEffects.ContainsKey(target.SetupTableId) ? "listed" : "NOT listed")}\n");
            if (table.ClothingSubPalEffects.Count == 0)
            {
                sb.Append("No colour options: 'ranges' falls back to the full palette.\n");
                return;
            }

            var palOption = (uint)(target.PaletteTemplate ?? 0);
            var option = table.ClothingSubPalEffects.TryGetValue(palOption, out var o) ? o : table.ClothingSubPalEffects[table.ClothingSubPalEffects.Keys.ElementAt(0)];
            var ranges = option.CloSubPalettes.SelectMany(sp => sp.Ranges).Select(r => $"{r.Offset}+{r.NumColors}");
            sb.Append($"Recolourable ranges (colour offset+count): {string.Join(", ", ranges)}\n");
        }

        /// <summary>Same save + redraw the recipe system and @pet-make-alpha use for an item in hand or in a pack.</summary>
        private static void SaveAndRedraw(Player player, WorldObject target)
        {
            target.ChangesDetected = true;
            target.SaveBiotaToDatabase();

            player.EnqueueBroadcast(new GameMessageUpdateObject(target));
            if (target.CurrentWieldedLocation != null)
                player.EnqueueBroadcast(new GameMessageObjDescEvent(player));
            else if (player.FindObject(target.Guid.Full, Player.SearchLocations.MyInventory) != null)
                player.MoveItemToFirstContainerSlot(target);
        }

        private static bool TryParseUInt(string s, out uint value)
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(s.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out value);
            return uint.TryParse(s, out value);
        }
    }
}
