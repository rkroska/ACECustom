using System.Collections.Generic;
using System.Linq;
using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Command;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    public static class ILTCommands
    {
        /// <summary>
        /// Priority order used by the <c>levelskills</c> sub-command.
        /// Specialized skills are always prioritised over Trained skills regardless of
        /// position in this table. Skills absent from this table fill alphabetically
        /// after all listed skills.
        /// </summary>
        private static readonly Dictionary<Skill, int> LevelSkillsPriority = new()
        {
            { Skill.Leadership,           1  },
            { Skill.Loyalty,              2  },
            { Skill.CreatureEnchantment,  3  },
            { Skill.LifeMagic,            4  },
            { Skill.ItemEnchantment,      5  },
            { Skill.WarMagic,             6  },
            { Skill.VoidMagic,            7  },
            { Skill.ManaConversion,       8  },
            { Skill.MeleeDefense,         9  },
            { Skill.MissileWeapons,       10 },
            { Skill.FinesseWeapons,       11 },
            { Skill.HeavyWeapons,         12 },
            { Skill.LightWeapons,         13 },
            { Skill.ArcaneLore,           14 },
            { Skill.MagicDefense,         15 },
            { Skill.MissileDefense,       16 },
        };

        [CommandHandler("ilt", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "ILT custom server commands and player preferences.",
            "Usage: /ilt [help|features|dmgformat|showoverkill|levelskills|trainskills|xp level|train]")]
        public static void HandleILT(Session session, params string[] parameters)
        {
            var player = session.Player;
            if (player == null) return;

            var sub = parameters.Length > 0 ? parameters[0].ToLower() : "help";

            // Aliases: /ilt xp level  and  /ilt xp  both map to levelskills
            if (sub == "xp" && (parameters.Length < 2 || parameters[1].ToLower() == "level"))
                sub = "levelskills";

            // Alias: /ilt train -> /ilt trainskills
            if (sub == "train")
                sub = "trainskills";

            if (sub == "features")
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("=== ILT Custom Features ===", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("  Coming Soon", ChatMessageType.System));
            }
            else if (sub == "dmgformat")
            {
                // Cycle or set damage number format
                int mode;
                if (parameters.Length >= 2)
                {
                    mode = parameters[1].ToLower() switch
                    {
                        "commas"  => 1,
                        "short"   => 2,
                        "default" => 0,
                        _ => -1   // unknown
                    };

                    if (mode == -1)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat(
                            $"Unknown format '{parameters[1]}'. Valid options: default, commas, short.", ChatMessageType.System));
                        return;
                    }
                }
                else
                    mode = (player.DamageNumberFormat + 1) % 3;  // cycle 0 → 1 → 2 → 0

                player.DamageNumberFormat = mode;
                var label = mode switch { 1 => "commas (1,247)", 2 => "short (K/M/B/T/Q)", _ => "default (vanilla)" };
                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Damage format set to {label}.", ChatMessageType.System));
            }
            else if (sub == "showoverkill")
            {
                // Toggle or set ShowOverkill (controls both kill and death overkill suffixes)
                bool newValue;
                if (parameters.Length >= 2)
                {
                    if (parameters[1].ToLower() == "on")
                        newValue = true;
                    else if (parameters[1].ToLower() == "off")
                        newValue = false;
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat(
                            $"Unknown value '{parameters[1]}'. Valid options: on, off.", ChatMessageType.System));
                        return;
                    }
                }
                else
                    newValue = !player.ShowOverkill; // toggle

                player.ShowOverkill = newValue;
                var state = newValue ? "ON" : "OFF";
                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Show Overkill: {state}. The [Overkill] suffix will {(newValue ? "now" : "no longer")} appear on kill and death messages.",
                    ChatMessageType.System));
            }
            else if (sub == "levelskills")
            {
                if (player.AvailableExperience <= 0)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat(
                        "You have no available XP to spend.", ChatMessageType.System));
                    return;
                }

                var sortedSkills = player.Skills.Values
                    .Where(s => s.AdvancementClass >= SkillAdvancementClass.Trained && !s.IsMaxRank)
                    .OrderByDescending(s => s.AdvancementClass)  // Specialized (4) before Trained (3)
                    .ThenBy(s => LevelSkillsPriority.TryGetValue(s.Skill, out var p) ? p : int.MaxValue)
                    .ThenBy(s => s.Skill.ToString())              // alphabetical fallback
                    .ToList();

                if (sortedSkills.Count == 0)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat(
                        "All of your skills are already maxed or not trained.", ChatMessageType.System));
                    return;
                }

                int skillsUpdated = 0;
                foreach (var skill in sortedSkills)
                {
                    if (player.AvailableExperience <= 0) break;

                    uint startingXp = skill.ExperienceSpent;
                    player.SpendAllAvailableSkillXp(skill, sendNetworkUpdate: false);

                    if (skill.ExperienceSpent > startingXp)
                    {
                        skillsUpdated++;
                        session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(player, skill));
                    }
                }

                if (skillsUpdated > 0)
                {
                    // Send updated available XP total so the client UI stays in sync
                    session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(
                        player, PropertyInt64.AvailableExperience, player.AvailableExperience ?? 0));
                    session.Network.EnqueueSend(new GameMessageSystemChat(
                        $"Successfully spent XP into {skillsUpdated} skill{(skillsUpdated == 1 ? "" : "s")}.",
                        ChatMessageType.Advancement));
                    // Persist immediately — prevents XP loss if the server crashes before the auto-save fires.
                    player.SaveBiotaToDatabase(enqueueSave: true);
                }
                else
                    session.Network.EnqueueSend(new GameMessageSystemChat(
                        "No XP was spent — not enough available for the next rank in any skill.",
                        ChatMessageType.System));
            }
            else if (sub == "trainskills")
            {
                if ((player.AvailableSkillCredits ?? 0) <= 0)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat(
                        "You have no skill credits to spend.", ChatMessageType.System));
                    return;
                }

                // Untrained skills sorted by the same LevelSkillsPriority table, then alphabetically
                var sortedSkills = player.Skills.Values
                    .Where(s => s.AdvancementClass == SkillAdvancementClass.Untrained)
                    .OrderBy(s => LevelSkillsPriority.TryGetValue(s.Skill, out var p) ? p : int.MaxValue)
                    .ThenBy(s => s.Skill.ToString())
                    .ToList();

                if (sortedSkills.Count == 0)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat(
                        "All of your skills are already trained or better.", ChatMessageType.System));
                    return;
                }

                int skillsUpdated = 0;
                foreach (var skill in sortedSkills)
                {
                    // Look up the credit cost for this skill from the dat tables
                    if (!DatManager.PortalDat.SkillTable.SkillBaseHash.TryGetValue((uint)skill.Skill, out var skillBase))
                        continue; // dat data missing — skip silently

                    var cost = skillBase.TrainedCost;
                    if (cost <= 0)
                        continue; // free/always-trained skill, already handled

                    // Skip unaffordable skills — cheaper lower-priority skills may still be trainable
                    if (cost > (player.AvailableSkillCredits ?? 0))
                        continue;

                    if (player.TrainSkill(skill.Skill, cost))
                    {
                        skillsUpdated++;
                        session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(player, skill));
                    }
                }

                if (skillsUpdated > 0)
                {
                    // One credit update after all training is complete
                    session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(
                        player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0));
                    session.Network.EnqueueSend(new GameMessageSystemChat(
                        $"Trained {skillsUpdated} skill{(skillsUpdated == 1 ? "" : "s")}. " +
                        $"You now have {player.AvailableSkillCredits ?? 0} credits remaining.",
                        ChatMessageType.Advancement));
                    // Persist immediately — prevents loss if server crashes before auto-save
                    player.SaveBiotaToDatabase(enqueueSave: true);
                }
                else
                    session.Network.EnqueueSend(new GameMessageSystemChat(
                        "No skills were trained — not enough credits for the next skill in the priority list.",
                        ChatMessageType.System));
            }
            else
            {
                var dmgLabel = player.DamageNumberFormat switch { 1 => "commas", 2 => "short", _ => "default" };
                var okLabel  = player.ShowOverkill ? "ON" : "OFF";
                session.Network.EnqueueSend(new GameMessageSystemChat("=== ILT Custom Commands ===", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("  /ilt features", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("      View a list of custom ILT server features.", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"  /ilt dmgformat [default|commas|short]", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"      Set your damage number display style. (currently: {dmgLabel})", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"  /ilt showoverkill [on|off]", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"      Toggle the [Overkill] suffix on kill and death messages. (currently: {okLabel})", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("  /ilt levelskills  |  /ilt xp level  |  /ilt xp", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("      Spend all available XP into trained and specialized skills, in priority order.", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("  /ilt trainskills  |  /ilt train", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("      Train all affordable untrained skills using skill credits, in priority order.", ChatMessageType.System));
            }
        }
    }
}
