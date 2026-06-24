using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ACE.Database;
using ACE.Database.Models.World;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Managers.QuestBuilder
{
    public static class QuestBuilderCompiler
    {
        private static readonly Dictionary<string, int> TriggerCategory = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Use"] = 7,
            ["Give"] = 6,
            ["Refuse"] = 1,
            ["PickUp"] = 10,
            ["Portal"] = 4,
        };

        private static readonly Dictionary<string, int> ActionType = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tell"] = 10,
            ["DirectBroadcast"] = 18,
            ["Give"] = 3,
            ["TakeItems"] = 74,
            ["InqQuest"] = 21,
            ["StampQuest"] = 22,
            ["Motion"] = 5,
        };

        private static readonly Dictionary<string, int> BranchCategory = new(StringComparer.OrdinalIgnoreCase)
        {
            ["onCooldown"] = 12,
            ["canComplete"] = 13,
        };

        private static readonly HashSet<string> FlagStampsWithoutQuestRow = new(StringComparer.OrdinalIgnoreCase)
        {
            "dynamicQuestFlag",
        };

        public static QuestValidationResultDto Validate(QuestPackageDto package)
        {
            var result = new QuestValidationResultDto { Ok = true };
            if (package == null)
            {
                AddIssue(result, "error", "null_package", "Package is required.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(package.Package))
                AddIssue(result, "error", "package_name", "Package name is required.");

            var stampNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (package.Stamps != null)
            {
                foreach (var s in package.Stamps)
                {
                    if (string.IsNullOrWhiteSpace(s?.Name))
                    {
                        AddIssue(result, "error", "stamp_name", "Every stamp needs a name.");
                        continue;
                    }
                    if (!stampNames.Add(s.Name))
                        AddIssue(result, "warning", "stamp_dup", $"Duplicate stamp definition: {s.Name}");
                    if (s.MinDelta < 0 && s.MinDelta != -1)
                        AddIssue(result, "warning", "stamp_delta", $"Stamp {s.Name}: min_Delta will use package default.");
                }
            }

            using var ctx = new WorldDbContext();
            var questNames = ctx.Quest.AsNoTracking().Select(q => q.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            void RequireQuestRow(string baseStamp)
            {
                if (string.IsNullOrWhiteSpace(baseStamp) || baseStamp.Contains("@#kt", StringComparison.Ordinal))
                    return;
                if (baseStamp.StartsWith("Dynamic_", StringComparison.OrdinalIgnoreCase))
                    return;
                if (FlagStampsWithoutQuestRow.Contains(baseStamp))
                    return;
                if (package.Stamps?.Any(s => string.Equals(s.Name, baseStamp, StringComparison.OrdinalIgnoreCase)) == true)
                    return;
                if (!questNames.Contains(baseStamp))
                    AddIssue(result, "warning", "missing_quest_row",
                        $"No quest row for '{baseStamp}' (add to Stamps or quest table).");
            }

            if (package.Actors != null)
            {
                foreach (var actor in package.Actors)
                {
                    if (actor.Wcid == 0)
                        AddIssue(result, "error", "actor_wcid", $"Actor '{actor.Name}' needs a WCID.");

                    if (actor.Flows == null || actor.Flows.Count == 0)
                        AddIssue(result, "warning", "no_flows", $"Actor {actor.Wcid} has no flows.");

                    foreach (var flow in actor.Flows ?? Enumerable.Empty<QuestFlowDto>())
                    {
                        if (!TriggerCategory.ContainsKey(flow.Trigger ?? ""))
                            AddIssue(result, "error", "trigger", $"Unknown trigger '{flow.Trigger}' on actor {actor.Wcid}.");

                        if (string.Equals(flow.Trigger, "Give", StringComparison.OrdinalIgnoreCase) &&
                            (!flow.GiveWcid.HasValue || flow.GiveWcid == 0))
                            AddIssue(result, "error", "give_wcid", $"Give flow on {actor.Wcid} needs giveWcid.");

                        WalkSteps(flow.Steps, RequireQuestRow, result, actor.Wcid, flow.Trigger);
                    }
                }
            }

            if (package.Creatures != null)
            {
                foreach (var c in package.Creatures)
                {
                    if (c.Wcid == 0)
                        AddIssue(result, "error", "creature_wcid", "Creature needs a WCID.");
                    if (c.DropItemWcid == 0)
                        AddIssue(result, "error", "drop_wcid", $"Creature {c.Wcid} needs dropItemWcid for create list.");
                }
            }

            foreach (var actor in package.Actors ?? Enumerable.Empty<QuestActorDto>())
            {
                if (IsLandscapePickupActor(actor) && !StepsContainGive(actor))
                    AddIssue(result, "error", "landscape_give",
                        $"Landscape object {actor.Wcid} ({actor.Name}) needs a Give emote step for the quest item.");
            }

            result.Ok = result.Issues.All(i => i.Severity != "error");
            return result;
        }

        private static bool IsLandscapePickupActor(QuestActorDto actor)
        {
            if (string.Equals(actor.Role, "landscapePickup", StringComparison.OrdinalIgnoreCase))
                return true;
            if (actor.Flows == null || actor.Flows.Count == 0)
                return false;
            var hasPickup = actor.Flows.Any(f =>
                string.Equals(f.Trigger, "PickUp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.Trigger, "Use", StringComparison.OrdinalIgnoreCase));
            var hasGiveFlow = actor.Flows.Any(f => string.Equals(f.Trigger, "Give", StringComparison.OrdinalIgnoreCase));
            return hasPickup && !hasGiveFlow;
        }

        private static bool StepsContainGive(QuestActorDto actor)
        {
            foreach (var flow in actor.Flows ?? Enumerable.Empty<QuestFlowDto>())
            {
                if (StepsContainGiveRecursive(flow.Steps))
                    return true;
            }
            return false;
        }

        private static bool StepsContainGiveRecursive(List<QuestStepDto> steps)
        {
            if (steps == null) return false;
            foreach (var step in steps)
            {
                if (string.Equals(step.Type, "Give", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (step.Branches != null)
                {
                    if (StepsContainGiveRecursive(step.Branches.OnCooldown))
                        return true;
                    if (StepsContainGiveRecursive(step.Branches.CanComplete))
                        return true;
                }
            }
            return false;
        }

        private static void WalkSteps(List<QuestStepDto> steps, Action<string> requireQuest, QuestValidationResultDto result, uint actorWcid, string trigger)
        {
            if (steps == null) return;
            foreach (var step in steps)
            {
                if (string.Equals(step.Type, "InqQuest", StringComparison.OrdinalIgnoreCase))
                {
                    requireQuest(GetBaseStamp(step.Stamp));
                    if (step.Branches == null ||
                        (step.Branches.OnCooldown?.Count ?? 0) == 0 && (step.Branches.CanComplete?.Count ?? 0) == 0)
                        AddIssue(result, "error", "inq_branches", $"InqQuest on actor {actorWcid} ({trigger}) needs onCooldown and canComplete branches.");
                }
                if (string.Equals(step.Type, "StampQuest", StringComparison.OrdinalIgnoreCase))
                    requireQuest(GetBaseStamp(step.Stamp));
                if (string.Equals(step.Type, "Motion", StringComparison.OrdinalIgnoreCase))
                {
                    if (step.Motion != null && !Regex.IsMatch(step.Motion, "^0x[0-9A-Fa-f]+$"))
                        AddIssue(result, "error", "invalid_motion", $"Invalid motion literal '{step.Motion}' on actor {actorWcid} ({trigger}). Must be a hex value like 0x41000003.");
                }

                if (step.Branches != null)
                {
                    WalkSteps(step.Branches.OnCooldown, requireQuest, result, actorWcid, trigger);
                    WalkSteps(step.Branches.CanComplete, requireQuest, result, actorWcid, trigger);
                }
            }
        }

        private static void AddIssue(QuestValidationResultDto result, string severity, string code, string message)
        {
            result.Issues.Add(new QuestValidationIssueDto { Severity = severity, Code = code, Message = message });
            if (severity == "error") result.Ok = false;
        }

        public static QuestExportResultDto Export(QuestPackageDto package, bool updateOnly = false)
        {
            var validation = Validate(package);
            if (!validation.Ok)
                throw new InvalidOperationException("Fix validation errors before export.");

            var files = new List<QuestExportFileDto>();
            var sbReadme = new StringBuilder();
            var pkg = SanitizeFileName(package.Package ?? "quest_package");

            sbReadme.AppendLine($"# Quest package: {package.Package}");
            sbReadme.AppendLine();
            if (updateOnly)
            {
                sbReadme.AppendLine("## Update mode");
                sbReadme.AppendLine("Re-import for an **existing** quest. Scripts replace `quest` rows and emotes only.");
                sbReadme.AppendLine("Weenie shells (NPC, object, item) are **not** included — your placed NPCs and items stay as-is.");
                sbReadme.AppendLine();
            }
            sbReadme.AppendLine("## Run order (MySQL Workbench)");
            var order = 1;

            if (package.Stamps?.Count > 0)
            {
                var sql = CompileQuestRows(package);
                files.Add(new QuestExportFileDto { FileName = $"{order:D2}_quests_{pkg}.sql", Content = sql });
                sbReadme.AppendLine($"{order}. `{order:D2}_quests_{pkg}.sql`");
                order++;
            }

            if (!updateOnly)
            {
                foreach (var item in package.Items ?? Enumerable.Empty<QuestItemDto>())
                {
                    var sql = CompileItemShell(item);
                    files.Add(new QuestExportFileDto { FileName = $"{order:D2}_item_{item.Wcid}.sql", Content = sql });
                    sbReadme.AppendLine($"{order}. `{order:D2}_item_{item.Wcid}.sql` — {item.Name}");
                    order++;
                }
            }
            else if (package.Items?.Count > 0)
            {
                sbReadme.AppendLine("(Item weenie shells skipped — update mode.)");
            }

            foreach (var creature in package.Creatures ?? Enumerable.Empty<QuestCreatureDto>())
            {
                var sql = CompileCreatureDrop(creature);
                files.Add(new QuestExportFileDto
                {
                    FileName = creature.PatchExisting
                        ? $"{order:D2}_patch_creature_{creature.Wcid}_drop.sql"
                        : $"{order:D2}_creature_{creature.Wcid}_drop.sql",
                    Content = sql
                });
                sbReadme.AppendLine($"{order}. creature drop WCID {creature.Wcid}");
                order++;
            }

            var actorList = package.Actors ?? Enumerable.Empty<QuestActorDto>().ToList();
            var hasLandscape = false;
            foreach (var actor in actorList)
            {
                var isLandscape = IsLandscapePickupActor(actor);
                if (isLandscape) hasLandscape = true;
                var prefix = isLandscape ? "world_object" : "npc";
                var template = actor.CloneFromWcid ?? (isLandscape ? 78780023u : 78780020u);

                if (!updateOnly)
                {
                    var shellSql = CompileActorShellFromClone(actor, template);
                    files.Add(new QuestExportFileDto { FileName = $"{order:D2}_shell_{prefix}_{actor.Wcid}.sql", Content = shellSql });
                    sbReadme.AppendLine($"{order}. `{order:D2}_shell_{prefix}_{actor.Wcid}.sql` — {actor.Name} (weenie shell from WCID {template})");
                    order++;
                }

                var emoteSql = CompileActorEmotesOnly(actor, package);
                files.Add(new QuestExportFileDto { FileName = $"{order:D2}_{prefix}_{actor.Wcid}_emotes.sql", Content = emoteSql });
                sbReadme.AppendLine($"{order}. `{order:D2}_{prefix}_{actor.Wcid}_emotes.sql` — emotes for {actor.Wcid}");
                order++;
            }

            sbReadme.AppendLine();
            AppendAfterImportAndPlacement(sbReadme, package, actorList, hasLandscape, updateOnly);
            sbReadme.AppendLine();
            sbReadme.AppendLine("## Corpse drops");
            sbReadme.AppendLine("Creatures use `weenie_properties_create_list` (Treasure, shade 0) — loot appears on corpse.");
            if (package.Items?.Any(i => i.CloneFromWcid.HasValue) == true)
            {
                sbReadme.AppendLine();
                sbReadme.AppendLine("## Item templates");
                sbReadme.AppendLine("Quest items with a clone template: duplicate the template weenie in Workbench and adjust WCID/name, or extend export in a future builder version.");
            }

            return new QuestExportResultDto
            {
                PackageName = package.Package,
                Files = files,
                Readme = sbReadme.ToString()
            };
        }

        private static void AppendAfterImportAndPlacement(
            StringBuilder sb,
            QuestPackageDto package,
            List<QuestActorDto> actors,
            bool hasLandscape,
            bool updateOnly)
        {
            sb.AppendLine("## After import");
            sb.AppendLine("- Restart the game server (or reload quest cache) so new `quest` rows are picked up.");
            sb.AppendLine("- SQL defines **weenies and emotes only** — nothing is placed in the world until you spawn it in-game (or via landblock tools).");
            if (updateOnly)
                sb.AppendLine("- **Update mode:** weenie shells were skipped; use the commands below only for WCIDs you have not placed yet.");
            sb.AppendLine();
            sb.AppendLine("## In-game placement (admin commands)");
            sb.AppendLine("Stand where the object should appear, face the desired direction, then run commands in-game (`@` prefix).");
            sb.AppendLine();
            sb.AppendLine("### Persistent spawn (`createinst`)");
            sb.AppendLine("Use **`@createinst <wcid>`** (alias **`@cin`**) to write a **landblock_instance** row at your current position. This is the normal way to place quest NPCs, pickup objects, and fixed mob spawns.");
            sb.AppendLine("**`@create <wcid>`** spawns a temporary copy for testing; it is **not** saved to the world database.");
            sb.AppendLine("**`/save`** is for character recall — it does **not** persist quest spawns.");
            sb.AppendLine();

            var npcs = actors.Where(a => !IsLandscapePickupActor(a)).ToList();
            var landscape = actors.Where(IsLandscapePickupActor).ToList();
            var creatures = package.Creatures ?? new List<QuestCreatureDto>();

            if (npcs.Count > 0)
            {
                sb.AppendLine("### Quest NPC(s)");
                foreach (var npc in npcs)
                {
                    sb.AppendLine($"- **{npc.Name}** — WCID `{npc.Wcid}`");
                    sb.AppendLine($"  - Place: `@createinst {npc.Wcid}`");
                    sb.AppendLine($"  - Quick test (not saved): `@create {npc.Wcid}`");
                }
                sb.AppendLine();
            }

            if (hasLandscape && landscape.Count > 0)
            {
                sb.AppendLine("### Landscape pickup object(s)");
                sb.AppendLine("Players **Use** or **Pick Up** these objects to receive the quest item (not corpse loot).");
                foreach (var obj in landscape)
                {
                    sb.AppendLine($"- **{obj.Name}** — WCID `{obj.Wcid}`");
                    sb.AppendLine($"  - Static placement: `@createinst {obj.Wcid}`");
                }
                sb.AppendLine("- **Repeat attempts** are usually blocked by quest stamps on the emote flow, not by removing the object.");
                sb.AppendLine("- To **physically respawn** the object (e.g. daily reset for everyone), use a **generator** (see below) with this WCID as the child profile.");
                sb.AppendLine();
            }

            if (creatures.Count > 0)
            {
                sb.AppendLine("### Creature(s) (corpse loot)");
                sb.AppendLine("Drop tables are in the creature SQL (`weenie_properties_create_list`). The mob must exist in the world to be killed.");
                foreach (var c in creatures)
                {
                    sb.AppendLine($"- **{c.Name}** — WCID `{c.Wcid}` (drops item `{c.DropItemWcid}`)");
                    sb.AppendLine($"  - Single fixed spawn: `@createinst {c.Wcid}`");
                    sb.AppendLine($"  - Respawning mob: place a **generator** whose child profile uses WCID `{c.Wcid}` (recommended for kill quests).");
                }
                sb.AppendLine();
            }

            sb.AppendLine("### Generators (respawn mobs / objects)");
            sb.AppendLine("Retail quests usually use a **generator weenie** that spawns children on a timer. The export does not create generators yet — add one in MySQL Workbench or clone an existing generator:");
            sb.AppendLine("1. Clone a generator weenie (search `weenie` for `type` Generator or copy a nearby landblock generator).");
            sb.AppendLine("2. Add a row to **`weenie_properties_generator`** on that generator: set **`weenie_Class_Id`** to the child WCID (mob or landscape object above), tune probability / delay / init create as needed.");
            sb.AppendLine("3. In-game: stand at the spawn point → **`@createinst <generatorWcid>`**.");
            sb.AppendLine("4. Target the generator → **`@regen`** to force the first spawn (Envoy+).");
            sb.AppendLine("5. **`@generatordump`** (Developer) on the generator lists child profiles and timers.");
            sb.AppendLine();
            if (creatures.Count > 0 || landscape.Count > 0)
            {
                sb.AppendLine("**Suggested generator children for this package:**");
                foreach (var c in creatures)
                    sb.AppendLine($"- Mob: `{c.Wcid}` ({c.Name})");
                foreach (var obj in landscape)
                    sb.AppendLine($"- Landscape object: `{obj.Wcid}` ({obj.Name})");
                sb.AppendLine();
            }
            sb.AppendLine("### Other useful commands");
            sb.AppendLine("| Command | Purpose |");
            sb.AppendLine("| --- | --- |");
            sb.AppendLine("| `@createinst <wcid>` / `@cin` | Save spawn at current location to `landblock_instance` |");
            sb.AppendLine("| `@create <wcid>` | Temporary spawn for testing |");
            sb.AppendLine("| `@regen` | Regenerate selected generator (target/appraisal) |");
            sb.AppendLine("| `@generatordump` | Dump generator child list and timing |");
            sb.AppendLine("| `@removeinst` | Remove selected landblock instance (if misplaced) |");
        }

        public static string CompileQuestRows(QuestPackageDto package)
        {
            var parts = new StringBuilder();
            parts.AppendLine($"-- Quest definitions: {package.Package}");
            parts.AppendLine();

            foreach (var stamp in package.Stamps ?? Enumerable.Empty<QuestStampDto>())
            {
                var minDelta = stamp.MinDelta != -1 ? stamp.MinDelta : package.CooldownSeconds;
                var maxSolves = stamp.MaxSolves;

                parts.AppendLine($"DELETE FROM `quest` WHERE `name` = {SqlStr(stamp.Name)};");
                parts.AppendLine();
                parts.AppendLine("INSERT INTO `quest` (`name`, `min_Delta`, `max_Solves`, `message`)");
                parts.AppendLine($"VALUES ({SqlStr(stamp.Name)}, {minDelta}, {maxSolves}, {SqlStr(stamp.Message ?? "")});");
                parts.AppendLine();
            }

            return parts.ToString();
        }

        private static readonly string[] WeenieCloneDeleteTables =
        {
            "weenie_properties_emote",
            "weenie_properties_book_page_data",
            "weenie_properties_book",
            "weenie_properties_anim_part",
            "weenie_properties_attribute",
            "weenie_properties_attribute_2nd",
            "weenie_properties_body_part",
            "weenie_properties_bool",
            "weenie_properties_create_list",
            "weenie_properties_d_i_d",
            "weenie_properties_event_filter",
            "weenie_properties_float",
            "weenie_properties_generator",
            "weenie_properties_i_i_d",
            "weenie_properties_int",
            "weenie_properties_int64",
            "weenie_properties_palette",
            "weenie_properties_position",
            "weenie_properties_skill",
            "weenie_properties_spell_book",
            "weenie_properties_string",
        };

        private static readonly (string Table, string Columns)[] WeenieCloneCopyTables =
        {
            ("weenie_properties_anim_part", "`object_Id`, `index`, `animation_Id`"),
            ("weenie_properties_attribute", "`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`"),
            ("weenie_properties_attribute_2nd", "`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`"),
            ("weenie_properties_body_part", "`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`"),
            ("weenie_properties_bool", "`object_Id`, `type`, `value`"),
            ("weenie_properties_create_list", "`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`"),
            ("weenie_properties_d_i_d", "`object_Id`, `type`, `value`"),
            ("weenie_properties_event_filter", "`object_Id`, `event`"),
            ("weenie_properties_float", "`object_Id`, `type`, `value`"),
            ("weenie_properties_generator", "`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`"),
            ("weenie_properties_i_i_d", "`object_Id`, `type`, `value`"),
            ("weenie_properties_int", "`object_Id`, `type`, `value`"),
            ("weenie_properties_int64", "`object_Id`, `type`, `value`"),
            ("weenie_properties_palette", "`object_Id`, `sub_Palette_Id`, `offset`, `length`"),
            ("weenie_properties_position", "`object_Id`, `position_Type`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`"),
            ("weenie_properties_skill", "`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`"),
            ("weenie_properties_spell_book", "`object_Id`, `spell`, `probability`"),
        };

        public static string CompileActorShellFromClone(QuestActorDto actor, uint templateWcid)
        {
            var target = actor.Wcid;
            var name = actor.Name ?? "Quest Actor";
            var className = $"ace{target}-{SanitizeClassName(name)}";
            var sb = new StringBuilder();
            sb.AppendLine($"-- Weenie shell: {name} ({target})");
            sb.AppendLine($"-- Cloned from template WCID {templateWcid} (must exist in this database).");
            sb.AppendLine();

            sb.AppendLine($"DELETE ea FROM `weenie_properties_emote_action` ea");
            sb.AppendLine($"INNER JOIN `weenie_properties_emote` e ON ea.`emote_Id` = e.`id`");
            sb.AppendLine($"WHERE e.`object_Id` = {target};");
            foreach (var table in WeenieCloneDeleteTables)
                sb.AppendLine($"DELETE FROM `{table}` WHERE `object_Id` = {target};");
            sb.AppendLine($"DELETE FROM `weenie` WHERE `class_Id` = {target};");
            sb.AppendLine();
            sb.AppendLine($"INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)");
            sb.AppendLine($"SELECT {target}, {SqlStr(className)}, `type`, UTC_TIMESTAMP() FROM `weenie` WHERE `class_Id` = {templateWcid};");
            sb.AppendLine();

            foreach (var (table, columns) in WeenieCloneCopyTables)
            {
                var selectCols = columns.Replace("`object_Id`", target.ToString(), StringComparison.Ordinal);
                sb.AppendLine($"INSERT INTO `{table}` ({columns})");
                sb.AppendLine($"SELECT {selectCols} FROM `{table}` WHERE `object_Id` = {templateWcid};");
                sb.AppendLine();
            }

            sb.AppendLine("INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)");
            sb.AppendLine($"SELECT {target}, `type`, CASE WHEN `type` = 1 THEN {SqlStr(name)} ELSE `value` END");
            sb.AppendLine($"FROM `weenie_properties_string` WHERE `object_Id` = {templateWcid};");
            sb.AppendLine();
            sb.AppendLine($"-- Verify: SELECT class_Id FROM weenie WHERE class_Id = {target};");
            return sb.ToString();
        }

        public static string CompileActorEmotesOnly(QuestActorDto actor, QuestPackageDto package)
        {
            var parts = new StringBuilder();
            parts.AppendLine($"-- Emotes for WCID {actor.Wcid} ({actor.Name})");
            parts.AppendLine($"-- Generated by Quest Builder — package {package.Package}");
            parts.AppendLine($"-- Run the matching *_shell_*_{actor.Wcid}.sql file before this script.");
            parts.AppendLine($"DELETE FROM `weenie_properties_emote` WHERE `object_Id` = {actor.Wcid};");
            parts.AppendLine();

            foreach (var flow in actor.Flows ?? Enumerable.Empty<QuestFlowDto>())
            {
                if (!TriggerCategory.TryGetValue(flow.Trigger ?? "", out var cat))
                    continue;

                var sets = CompileFlow(actor.Wcid, cat, flow);
                parts.Append(CompileEmoteSets(sets));
            }

            return parts.ToString();
        }

        private static string CompileCreatureDrop(QuestCreatureDto creature)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"-- Corpse loot: WCID {creature.Wcid} drops {creature.DropItemWcid} x{creature.DropStack}");
            sb.AppendLine($"-- create_list: destination_Type 8 (Treasure), shade 0 = always on corpse");
            sb.AppendLine();

            if (!creature.PatchExisting)
            {
                sb.AppendLine($"-- Patch-only mode: set PatchExisting=true if weenie {creature.Wcid} already exists.");
                sb.AppendLine($"-- Full creature clone from template {creature.TemplateWcid} is not auto-generated in v1.");
                sb.AppendLine();
            }

            sb.AppendLine($"DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = {creature.Wcid} AND `weenie_Class_Id` = {creature.DropItemWcid};");
            sb.AppendLine();
            sb.AppendLine("INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)");
            sb.AppendLine($"VALUES ({creature.Wcid}, 8, {creature.DropItemWcid}, {creature.DropStack}, 0, 0, 0);");
            return sb.ToString();
        }

        private static string CompileItemShell(QuestItemDto item)
        {
            var name = item.Name ?? "Quest Item";
            var desc = item.LongDesc ?? "";
            var className = $"ace{item.Wcid}-questitem";

            var cloneNote = item.CloneFromWcid.HasValue
                ? $"\n-- Clone stats/appearance from template WCID {item.CloneFromWcid} in Workbench if needed.\n"
                : "";

            return $@"-- Item: {name} ({item.Wcid}){cloneNote}
DELETE FROM `weenie` WHERE `class_Id` = {item.Wcid};

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES ({item.Wcid}, '{EscapeSql(className)}', 38, UTC_TIMESTAMP());

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES ({item.Wcid},   1, 128)
     , ({item.Wcid},   5, 5)
     , ({item.Wcid},  11, 1)
     , ({item.Wcid},  12, 1)
     , ({item.Wcid},  16, 1)
     , ({item.Wcid},  19, 100)
     , ({item.Wcid},  33, 1)
     , ({item.Wcid}, 114, 1);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES ({item.Wcid}, 1, {SqlStr(name)})
     , ({item.Wcid}, 16, {SqlStr(desc)});

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES ({item.Wcid}, 1, 0x02000181)
     , ({item.Wcid}, 8, 0x06001B27);
";
        }

        private class EmoteSet
        {
            public uint ObjectId;
            public int Category;
            public uint? WeenieClassId;
            public string Quest;
            public List<EmoteAction> Actions = new();
        }

        private class EmoteAction
        {
            public int Order;
            public int TypeId;
            public string Message;
            public double Delay;
            public string Motion;
            public uint? WeenieClassId;
            public int? StackSize;
        }

        private static List<EmoteSet> CompileFlow(uint wcid, int triggerCat, QuestFlowDto flow)
        {
            var allSets = new List<EmoteSet>();
            var triggerActions = new List<EmoteAction>();
            var branchSets = new List<EmoteSet>();
            var order = 0;

            foreach (var step in flow.Steps ?? Enumerable.Empty<QuestStepDto>())
            {
                ExpandStep(step, ref order, triggerActions, branchSets, wcid);
            }

            allSets.Add(new EmoteSet
            {
                ObjectId = wcid,
                Category = triggerCat,
                WeenieClassId = string.Equals(flow.Trigger, "Give", StringComparison.OrdinalIgnoreCase) ? flow.GiveWcid : null,
                Actions = triggerActions
            });

            foreach (var bs in branchSets)
                allSets.Add(bs);

            return allSets;
        }

        private static void ExpandStep(QuestStepDto step, ref int order, List<EmoteAction> linear, List<EmoteSet> branches, uint wcid)
        {
            if (!ActionType.TryGetValue(step.Type ?? "", out var typeId) && step.Type != "InqQuest")
                return;

            if (string.Equals(step.Type, "InqQuest", StringComparison.OrdinalIgnoreCase))
            {
                var stamp = step.Stamp ?? "";
                var branchKey = stamp;
                linear.Add(new EmoteAction { Order = order++, TypeId = 21, Message = stamp });

                foreach (var branch in new (string key, List<QuestStepDto> list)[]
                {
                    ("onCooldown", step.Branches?.OnCooldown),
                    ("canComplete", step.Branches?.CanComplete),
                })
                {
                    if (!BranchCategory.TryGetValue(branch.key, out var cat)) continue;
                    var branchActions = new List<EmoteAction>();
                    var o = 0;
                    foreach (var child in branch.list ?? Enumerable.Empty<QuestStepDto>())
                    {
                        var subLinear = branchActions;
                        var subBranches = new List<EmoteSet>();
                        ExpandStep(child, ref o, subLinear, subBranches, wcid);
                        foreach (var sb in subBranches)
                            branches.Add(sb);
                    }
                    branches.Add(new EmoteSet
                    {
                        ObjectId = wcid,
                        Category = cat,
                        Quest = branchKey,
                        Actions = branchActions
                    });
                }
                return;
            }

            linear.Add(StepToAction(step, order++));
        }

        private static EmoteAction StepToAction(QuestStepDto step, int order)
        {
            ActionType.TryGetValue(step.Type ?? "", out var typeId);
            var a = new EmoteAction { Order = order, TypeId = typeId, Delay = step.Delay };
            switch (step.Type)
            {
                case "Tell":
                case "DirectBroadcast":
                    a.Message = step.Text;
                    break;
                case "StampQuest":
                    a.Message = step.Stamp;
                    break;
                case "Motion":
                    a.Motion = ValidateMotionLiteral(step.Motion ?? "0x41000003");
                    break;
                case "Give":
                case "TakeItems":
                    a.WeenieClassId = step.Wcid;
                    a.StackSize = step.Stack ?? 1;
                    break;
            }
            return a;
        }

        private static string CompileEmoteSets(List<EmoteSet> sets)
        {
            var sb = new StringBuilder();
            foreach (var es in sets)
            {
                if (es.Actions.Count == 0)
                    throw new InvalidOperationException(
                        $"Emote category {es.Category} for WCID {es.ObjectId} has no actions.");

                var wcidCol = es.WeenieClassId?.ToString() ?? "NULL";
                var questCol = es.Quest != null ? SqlStr(es.Quest) : "NULL";
                sb.AppendLine($"-- Emote category {es.Category}");
                sb.AppendLine("INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)");
                sb.AppendLine($"VALUES ({es.ObjectId}, {es.Category}, 1, {wcidCol}, NULL, NULL, {questCol}, NULL, NULL, NULL);");
                sb.AppendLine();
                sb.AppendLine("SET @parent_id = LAST_INSERT_ID();");
                sb.AppendLine();
                sb.AppendLine("INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)");
                sb.AppendLine("VALUES");
                sb.AppendLine(string.Join(",\n", es.Actions.Select(a => ActionToSql(a))));
                sb.AppendLine(";");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string ActionToSql(EmoteAction a)
        {
            var comment = ActionType.FirstOrDefault(kv => kv.Value == a.TypeId).Key ?? a.TypeId.ToString();
            var msg = SqlStr(a.Message);
            var motion = SqlHexLiteralOrNull(a.Motion);
            var wcid = a.WeenieClassId?.ToString() ?? "NULL";
            var stack = a.StackSize?.ToString() ?? "NULL";
            return $"(@parent_id, {a.Order}, {a.TypeId} /* {comment} */, {a.Delay}, 1, {motion}, {msg}, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, {wcid}, {stack}, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)";
        }

        public static uint FindNextWcid(uint rangeStart = 78780090, uint rangeEnd = 78780199)
        {
            using var ctx = new WorldDbContext();
            var used = ctx.Weenie.AsNoTracking()
                .Where(w => w.ClassId >= rangeStart && w.ClassId <= rangeEnd)
                .Select(w => w.ClassId)
                .ToHashSet();

            for (var id = rangeStart; id <= rangeEnd; id++)
            {
                if (!used.Contains(id))
                    return id;
            }
            return 0;
        }

        public static string GetBaseStamp(string stamp) =>
            string.IsNullOrEmpty(stamp) ? stamp : stamp.Contains('@') ? stamp[..stamp.IndexOf('@')] : stamp;

        private static string SqlStr(string s) =>
            s == null ? "NULL" : $"'{EscapeSql(s)}'";

        private static string ValidateMotionLiteral(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "0x41000003";

            if (!Regex.IsMatch(value, "^0x[0-9A-Fa-f]+$"))
                throw new InvalidOperationException($"Invalid motion literal: {value}");

            return value;
        }

        private static string SqlHexLiteralOrNull(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "NULL";

            if (!Regex.IsMatch(value, "^0x[0-9A-Fa-f]+$"))
                throw new InvalidOperationException($"Invalid motion literal: {value}");

            return value;
        }

        private static string EscapeSql(string s) => s?.Replace("'", "''") ?? "";

        private static string SanitizeClassName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "questactor";
            var chars = name.ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray();
            var s = new string(chars).Trim('-');
            while (s.Contains("--", StringComparison.Ordinal))
                s = s.Replace("--", "-", StringComparison.Ordinal);
            return string.IsNullOrEmpty(s) ? "questactor" : s.Length > 40 ? s[..40] : s;
        }

        private static string SanitizeFileName(string name) =>
            string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));
    }
}
