using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Database.Models.World;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Managers.QuestBuilder
{
    public class QuestImportResultDto
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        public QuestPackageDto Package { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>Reads quest packages from ace_world for editing in Quest Builder.</summary>
    public static class QuestBuilderImporter
    {
        private static readonly Dictionary<uint, string> CategoryToTrigger = new()
        {
            [7] = "Use",
            [6] = "Give",
            [1] = "Refuse",
            [10] = "PickUp",
            [4] = "Portal",
        };

        private static readonly Dictionary<uint, string> TypeToStep = new()
        {
            [10] = "Tell",
            [18] = "DirectBroadcast",
            [3] = "Give",
            [74] = "TakeItems",
            [21] = "InqQuest",
            [22] = "StampQuest",
            [5] = "Motion",
        };

        public static QuestImportResultDto ImportFromNpcWcid(uint npcWcid, bool includeRelatedActors = true)
        {
            using var ctx = new WorldDbContext();
            var result = new QuestImportResultDto();

            if (!ctx.Weenie.AsNoTracking().Any(w => w.ClassId == npcWcid))
            {
                result.Ok = false;
                result.Message = $"WCID {npcWcid} does not exist in weenie table.";
                return result;
            }

            var npcName = GetWeenieName(ctx, npcWcid) ?? $"NPC {npcWcid}";
            var npcActor = BuildActorFromDb(ctx, npcWcid, npcName, "questGiver");
            if (npcActor.Flows.Count == 0)
            {
                result.Ok = false;
                result.Message = $"WCID {npcWcid} has no recognized emote flows (Use/Give/Refuse/PickUp).";
                return result;
            }

            var actors = new List<QuestActorDto> { npcActor };
            var stampNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectStamps(npcActor, stampNames);

            uint? questItemWcid = null;
            var giveFlow = npcActor.Flows.FirstOrDefault(f => string.Equals(f.Trigger, "Give", StringComparison.OrdinalIgnoreCase));
            if (giveFlow?.GiveWcid != null)
                questItemWcid = giveFlow.GiveWcid;
            else
                questItemWcid = FindTakeItemWcid(giveFlow?.Steps);

            if (includeRelatedActors && questItemWcid.HasValue)
            {
                foreach (var otherWcid in FindPickupObjectIds(ctx, questItemWcid.Value, npcWcid))
                {
                    var objName = GetWeenieName(ctx, otherWcid) ?? $"Object {otherWcid}";
                    var landscape = BuildActorFromDb(ctx, otherWcid, objName, "landscapePickup");
                    if (landscape.Flows.Count == 0) continue;
                    actors.Add(landscape);
                    CollectStamps(landscape, stampNames);
                }
            }

            var items = new List<QuestItemDto>();
            if (questItemWcid.HasValue)
            {
                items.Add(new QuestItemDto
                {
                    Wcid = questItemWcid.Value,
                    Name = GetWeenieName(ctx, questItemWcid.Value) ?? "Quest Item",
                    LongDesc = GetWeenieLongDesc(ctx, questItemWcid.Value),
                });
            }

            var stamps = LoadQuestStamps(ctx, stampNames);
            var packageName = GuessPackageName(stamps.Select(s => s.Name).ToList()) ?? $"imported_{npcWcid}";

            var reward = ExtractRewardFromGive(giveFlow?.Steps);

            result.Ok = true;
            result.Message = $"Imported {actors.Count} actor(s) from WCID {npcWcid}.";
            result.Package = new QuestPackageDto
            {
                Package = packageName,
                Description = $"Imported from database (NPC {npcWcid}).",
                CooldownSeconds = stamps.FirstOrDefault(s => s.MinDelta > 0)?.MinDelta ?? 86400,
                Stamps = stamps,
                Items = items,
                Actors = actors,
                Creatures = new List<QuestCreatureDto>(),
            };

            if (reward.wcid > 0 && !result.Warnings.Any(w => w.Contains("reward")))
                result.Warnings.Add($"Reward item WCID {reward.wcid} detected in Give flow — set reward in Turn-in phase if needed.");

            return result;
        }

        public static QuestImportResultDto ImportFromStampName(string stampName)
        {
            if (string.IsNullOrWhiteSpace(stampName))
                return new QuestImportResultDto { Ok = false, Message = "Stamp name is required." };

            using var ctx = new WorldDbContext();
            var emote = ctx.WeeniePropertiesEmote.AsNoTracking()
                .FirstOrDefault(e =>
                    e.Quest != null && e.Quest.Equals(stampName, StringComparison.OrdinalIgnoreCase));

            if (emote == null)
            {
                var action = ctx.WeeniePropertiesEmoteAction.AsNoTracking()
                    .FirstOrDefault(a =>
                        a.Type == 21 && a.Message != null &&
                        a.Message.Equals(stampName, StringComparison.OrdinalIgnoreCase));
                if (action != null)
                    emote = ctx.WeeniePropertiesEmote.AsNoTracking().FirstOrDefault(e => e.Id == action.EmoteId);
            }

            if (emote == null)
                return new QuestImportResultDto { Ok = false, Message = $"No emote found referencing stamp «{stampName}»." };

            return ImportFromNpcWcid(emote.ObjectId);
        }

        private static QuestActorDto BuildActorFromDb(WorldDbContext ctx, uint wcid, string name, string role)
        {
            var emotes = ctx.WeeniePropertiesEmote.AsNoTracking()
                .Where(e => e.ObjectId == wcid)
                .Include(e => e.WeeniePropertiesEmoteAction)
                .ToList();

            var flows = new List<QuestFlowDto>();
            foreach (var (cat, trigger) in CategoryToTrigger)
            {
                var triggerEmote = emotes.FirstOrDefault(e => e.Category == cat && string.IsNullOrEmpty(e.Quest));
                if (triggerEmote == null) continue;

                var steps = ParseSteps(triggerEmote.WeeniePropertiesEmoteAction.OrderBy(a => a.Order).ToList(), emotes);
                flows.Add(new QuestFlowDto
                {
                    Trigger = trigger,
                    GiveWcid = string.Equals(trigger, "Give", StringComparison.OrdinalIgnoreCase)
                        ? triggerEmote.WeenieClassId
                        : null,
                    Steps = steps,
                });
            }

            return new QuestActorDto
            {
                Wcid = wcid,
                Name = name,
                Role = role,
                Flows = flows,
            };
        }

        private static List<QuestStepDto> ParseSteps(
            List<WeeniePropertiesEmoteAction> linearActions,
            List<WeeniePropertiesEmote> allEmotes)
        {
            var steps = new List<QuestStepDto>();
            foreach (var action in linearActions)
            {
                if (!TypeToStep.TryGetValue(action.Type, out var stepType))
                    continue;

                if (action.Type == 21)
                {
                    var stamp = action.Message ?? "";
                    var onCooldown = allEmotes
                        .Where(e => e.Category == 12 && string.Equals(e.Quest, stamp, StringComparison.OrdinalIgnoreCase))
                        .SelectMany(e => e.WeeniePropertiesEmoteAction.OrderBy(a => a.Order))
                        .ToList();
                    var canComplete = allEmotes
                        .Where(e => e.Category == 13 && string.Equals(e.Quest, stamp, StringComparison.OrdinalIgnoreCase))
                        .SelectMany(e => e.WeeniePropertiesEmoteAction.OrderBy(a => a.Order))
                        .ToList();

                    steps.Add(new QuestStepDto
                    {
                        Type = "InqQuest",
                        Stamp = stamp,
                        Branches = new QuestStepBranchesDto
                        {
                            OnCooldown = ParseSteps(onCooldown, allEmotes),
                            CanComplete = ParseSteps(canComplete, allEmotes),
                        },
                    });
                    continue;
                }

                steps.Add(ActionToStep(action, stepType));
            }
            return steps;
        }

        private static QuestStepDto ActionToStep(WeeniePropertiesEmoteAction action, string stepType)
        {
            var step = new QuestStepDto { Type = stepType, Delay = action.Delay };
            switch (stepType)
            {
                case "Tell":
                case "DirectBroadcast":
                    step.Text = action.Message;
                    break;
                case "StampQuest":
                    step.Stamp = action.Message;
                    break;
                case "Motion":
                    step.Motion = action.Motion.HasValue ? $"0x{action.Motion.Value:X8}" : "0x41000003";
                    break;
                case "Give":
                case "TakeItems":
                    step.Wcid = action.WeenieClassId;
                    step.Stack = action.StackSize ?? 1;
                    break;
            }
            return step;
        }

        private static void CollectStamps(QuestActorDto actor, HashSet<string> stamps)
        {
            void Walk(List<QuestStepDto> steps)
            {
                if (steps == null) return;
                foreach (var s in steps)
                {
                    if ((s.Type == "InqQuest" || s.Type == "StampQuest") && !string.IsNullOrWhiteSpace(s.Stamp))
                        stamps.Add(s.Stamp);
                    if (s.Branches != null)
                    {
                        Walk(s.Branches.OnCooldown);
                        Walk(s.Branches.CanComplete);
                    }
                }
            }
            foreach (var flow in actor.Flows)
                Walk(flow.Steps);
        }

        private static List<QuestStampDto> LoadQuestStamps(WorldDbContext ctx, HashSet<string> stampNames)
        {
            var rows = ctx.Quest.AsNoTracking()
                .Where(q => stampNames.Contains(q.Name))
                .ToList();

            var stamps = new List<QuestStampDto>();
            foreach (var name in stampNames)
            {
                var row = rows.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                stamps.Add(new QuestStampDto
                {
                    Name = name,
                    Message = row?.Message,
                    MinDelta = row != null ? (int)row.MinDelta : -1,
                    MaxSolves = row?.MaxSolves ?? -1,
                });
            }
            return stamps;
        }

        private static List<uint> FindPickupObjectIds(WorldDbContext ctx, uint itemWcid, uint excludeWcid)
        {
            return ctx.WeeniePropertiesEmoteAction.AsNoTracking()
                .Where(a => a.Type == 3 && a.WeenieClassId == itemWcid)
                .Join(
                    ctx.WeeniePropertiesEmote.AsNoTracking(),
                    a => a.EmoteId,
                    e => e.Id,
                    (a, e) => e)
                .Where(e => e.ObjectId != excludeWcid && (e.Category == 7 || e.Category == 10))
                .Select(e => e.ObjectId)
                .Distinct()
                .ToList();
        }

        private static string GetWeenieName(WorldDbContext ctx, uint wcid) =>
            ctx.WeeniePropertiesString.AsNoTracking()
                .Where(s => s.ObjectId == wcid && s.Type == 1)
                .Select(s => s.Value)
                .FirstOrDefault();

        private static string GetWeenieLongDesc(WorldDbContext ctx, uint wcid) =>
            ctx.WeeniePropertiesString.AsNoTracking()
                .Where(s => s.ObjectId == wcid && s.Type == 16)
                .Select(s => s.Value)
                .FirstOrDefault();

        private static uint? FindTakeItemWcid(List<QuestStepDto> steps)
        {
            if (steps == null) return null;
            foreach (var s in steps)
            {
                if (s.Type == "TakeItems" && s.Wcid.HasValue) return s.Wcid;
                if (s.Branches != null)
                {
                    var fromBranch = FindTakeItemWcid(s.Branches.CanComplete) ?? FindTakeItemWcid(s.Branches.OnCooldown);
                    if (fromBranch.HasValue) return fromBranch;
                }
            }
            return null;
        }

        private static (uint wcid, int stack) ExtractRewardFromGive(List<QuestStepDto> steps)
        {
            if (steps == null) return (0, 0);
            foreach (var s in steps)
            {
                if (s.Type == "Give" && s.Wcid.HasValue) return (s.Wcid.Value, s.Stack ?? 1);
                if (s.Branches != null)
                {
                    var r = ExtractRewardFromGive(s.Branches.CanComplete);
                    if (r.wcid > 0) return r;
                    r = ExtractRewardFromGive(s.Branches.OnCooldown);
                    if (r.wcid > 0) return r;
                }
            }
            return (0, 0);
        }

        private static string GuessPackageName(List<string> stampNames)
        {
            if (stampNames.Count == 0) return null;
            var first = stampNames[0];
            var suffixes = new[] { "_started", "_start", "_pickup", "_complete", "_completion", "_daily" };
            foreach (var suffix in suffixes)
            {
                if (first.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return first[..^suffix.Length];
            }
            var idx = first.LastIndexOf('_');
            return idx > 0 ? first[..idx] : first;
        }
    }
}
