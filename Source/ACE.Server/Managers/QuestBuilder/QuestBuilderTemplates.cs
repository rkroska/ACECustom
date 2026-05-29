using System.Collections.Generic;

namespace ACE.Server.Managers.QuestBuilder
{
    public static class QuestBuilderTemplates
    {
        public static List<QuestTemplateInfoDto> List() => new()
        {
            new() { Id = "blank", Label = "Blank package", Description = "Empty stamps and one NPC actor." },
            new() { Id = "kill_turnin", Label = "Kill + turn-in", Description = "Brief NPC, give quest item, reward coins (Boom Boom style)." },
            new() { Id = "pickup_turnin", Label = "Pickup object + turn-in", Description = "Daily gated landscape pickup and separate NPC turn-in stamp (echo/lens style)." },
            new() { Id = "daily_pickup", Label = "Daily pickup object", Description = "Use object with InqQuest daily gate." },
            new() { Id = "daily_exchange", Label = "Daily NPC exchange", Description = "Give item for daily reward." },
        };

        public static QuestPackageDto Create(string templateId, uint nextWcid)
        {
            return templateId switch
            {
                "kill_turnin" => KillTurnIn(nextWcid),
                "pickup_turnin" => PickupTurnIn(nextWcid),
                "daily_pickup" => DailyPickup(nextWcid),
                "daily_exchange" => DailyExchange(nextWcid),
                _ => Blank(nextWcid),
            };
        }

        private static QuestPackageDto Blank(uint wcid) => new()
        {
            Package = "new_quest",
            CooldownSeconds = 86400,
            Stamps = new(),
            Items = new(),
            Actors = new()
            {
                new QuestActorDto
                {
                    Wcid = wcid,
                    Name = "Quest NPC",
                    CloneFromWcid = 78780020,
                    Flows = new()
                    {
                        new QuestFlowDto
                        {
                            Trigger = "Use",
                            Steps = new()
                            {
                                new QuestStepDto { Type = "Motion" },
                                new QuestStepDto { Type = "Tell", Text = "Greetings, adventurer.", Delay = 1 },
                            }
                        }
                    }
                }
            },
            Creatures = new(),
        };

        private static QuestPackageDto KillTurnIn(uint baseWcid)
        {
            var npc = baseWcid;
            var item = baseWcid + 1;
            var mob = baseWcid + 2;
            const string stamp = "custom_quest_complete";

            return new QuestPackageDto
            {
                Package = "kill_turnin_quest",
                Description = "Kill mob for item, return to NPC for reward.",
                CooldownSeconds = 0,
                Stamps = new()
                {
                    new QuestStampDto
                    {
                        Name = stamp,
                        MinDelta = 0,
                        MaxSolves = 1,
                        Message = "You completed this quest.",
                    }
                },
                Items = new()
                {
                    new QuestItemDto { Wcid = item, Name = "Quest Relic", LongDesc = "Turn this in to the quest giver." },
                },
                Creatures = new()
                {
                    new QuestCreatureDto
                    {
                        Wcid = mob,
                        Name = "Quest Target",
                        TemplateWcid = 78780092,
                        PatchExisting = false,
                        DropItemWcid = item,
                        DropStack = 1,
                    }
                },
                Actors = new()
                {
                    new QuestActorDto
                    {
                        Wcid = npc,
                        Name = "Quest Giver",
                        CloneFromWcid = 78780020,
                        Flows = new()
                        {
                            new QuestFlowDto
                            {
                                Trigger = "Use",
                                Steps = new()
                                {
                                    new QuestStepDto { Type = "Motion" },
                                    new QuestStepDto { Type = "Tell", Text = "A beast to the west holds a relic I need. Slay it and bring me the item.", Delay = 1 },
                                }
                            },
                            new QuestFlowDto
                            {
                                Trigger = "Give",
                                GiveWcid = item,
                                Steps = new()
                                {
                                    new QuestStepDto { Type = "Motion" },
                                    new QuestStepDto
                                    {
                                        Type = "InqQuest",
                                        Stamp = stamp,
                                        Branches = new QuestStepBranchesDto
                                        {
                                            OnCooldown = new()
                                            {
                                                new QuestStepDto { Type = "Tell", Text = "You have already been rewarded for this task." },
                                            },
                                            CanComplete = new()
                                            {
                                                new QuestStepDto { Type = "TakeItems", Wcid = item, Stack = 1 },
                                                new QuestStepDto { Type = "Tell", Text = "Excellent work!", Delay = 1 },
                                                new QuestStepDto { Type = "Give", Wcid = 300004, Stack = 10 },
                                                new QuestStepDto { Type = "StampQuest", Stamp = stamp },
                                            }
                                        }
                                    }
                                }
                            },
                            new QuestFlowDto
                            {
                                Trigger = "Refuse",
                                Steps = new()
                                {
                                    new QuestStepDto { Type = "Motion" },
                                    new QuestStepDto { Type = "Tell", Text = "That is not what I asked for." },
                                }
                            },
                        }
                    }
                },
            };
        }

        private static QuestPackageDto PickupTurnIn(uint baseWcid)
        {
            var npc = baseWcid;
            var item = baseWcid + 1;
            var pickupObj = baseWcid + 2;
            const string pickupStamp = "custom_quest_pickup";
            const string completeStamp = "custom_quest_complete";

            return new QuestPackageDto
            {
                Package = "pickup_turnin_quest",
                Description = "Take item from world object (daily pickup stamp), return to NPC for reward (separate completion stamp).",
                CooldownSeconds = 86400,
                Stamps = new()
                {
                    new QuestStampDto
                    {
                        Name = pickupStamp,
                        MinDelta = 86400,
                        MaxSolves = -1,
                        Message = "You obtained the quest item from the source.",
                    },
                    new QuestStampDto
                    {
                        Name = completeStamp,
                        MinDelta = 86400,
                        MaxSolves = 1,
                        Message = "You completed this quest.",
                    },
                },
                Items = new()
                {
                    new QuestItemDto { Wcid = item, Name = "Quest Token", LongDesc = "Bring this to the quest giver." },
                },
                Creatures = new(),
                Actors = new()
                {
                    new QuestActorDto
                    {
                        Wcid = npc,
                        Name = "Quest Giver",
                        CloneFromWcid = 78780020,
                        Role = "questGiver",
                        Flows = new()
                        {
                            new QuestFlowDto
                            {
                                Trigger = "Use",
                                Steps = new()
                                {
                                    new QuestStepDto { Type = "Motion" },
                                    new QuestStepDto
                                    {
                                        Type = "Tell",
                                        Text = "Fetch the token from the resonator, then bring it to me for your reward.",
                                        Delay = 1,
                                    },
                                },
                            },
                            new QuestFlowDto
                            {
                                Trigger = "Give",
                                GiveWcid = item,
                                Steps = new()
                                {
                                    new QuestStepDto { Type = "Motion" },
                                    new QuestStepDto
                                    {
                                        Type = "InqQuest",
                                        Stamp = completeStamp,
                                        Branches = new QuestStepBranchesDto
                                        {
                                            OnCooldown = new()
                                            {
                                                new QuestStepDto { Type = "Tell", Text = "You have already been rewarded. Come back later." },
                                            },
                                            CanComplete = new()
                                            {
                                                new QuestStepDto { Type = "TakeItems", Wcid = item, Stack = 1 },
                                                new QuestStepDto { Type = "Tell", Text = "Well done!", Delay = 1 },
                                                new QuestStepDto { Type = "Give", Wcid = 300004, Stack = 10 },
                                                new QuestStepDto { Type = "StampQuest", Stamp = completeStamp },
                                            },
                                        },
                                    },
                                },
                            },
                            new QuestFlowDto
                            {
                                Trigger = "Refuse",
                                Steps = new()
                                {
                                    new QuestStepDto { Type = "Motion" },
                                    new QuestStepDto { Type = "Tell", Text = "That is not what I need." },
                                },
                            },
                        },
                    },
                    new QuestActorDto
                    {
                        Wcid = pickupObj,
                        Name = "Quest Pickup Object",
                        CloneFromWcid = 78780023,
                        Role = "landscapePickup",
                        Flows = new()
                        {
                            new QuestFlowDto
                            {
                                Trigger = "Use",
                                Steps = new()
                                {
                                    new QuestStepDto
                                    {
                                        Type = "InqQuest",
                                        Stamp = pickupStamp,
                                        Branches = new QuestStepBranchesDto
                                        {
                                            OnCooldown = new()
                                            {
                                                new QuestStepDto { Type = "DirectBroadcast", Text = "You must wait before taking another." },
                                            },
                                            CanComplete = new()
                                            {
                                                new QuestStepDto { Type = "DirectBroadcast", Text = "You take the quest token." },
                                                new QuestStepDto { Type = "Give", Wcid = item, Stack = 1 },
                                                new QuestStepDto { Type = "StampQuest", Stamp = pickupStamp },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        private static QuestPackageDto DailyPickup(uint wcid) => new()
        {
            Package = "daily_pickup",
            CooldownSeconds = 86400,
            Stamps = new()
            {
                new QuestStampDto { Name = "custom_daily_pickup", Message = "Daily pickup complete." }
            },
            Items = new()
            {
                new QuestItemDto { Wcid = wcid + 1, Name = "Daily Token", LongDesc = "A token from the daily source." }
            },
            Actors = new()
            {
                new QuestActorDto
                {
                    Wcid = wcid,
                    Name = "Daily Object",
                    CloneFromWcid = 78780023,
                    Flows = new()
                    {
                        new QuestFlowDto
                        {
                            Trigger = "Use",
                            Steps = new()
                            {
                                new QuestStepDto
                                {
                                    Type = "InqQuest",
                                    Stamp = "custom_daily_pickup",
                                    Branches = new QuestStepBranchesDto
                                    {
                                        OnCooldown = new()
                                        {
                                            new QuestStepDto { Type = "DirectBroadcast", Text = "You must wait before claiming again." },
                                        },
                                        CanComplete = new()
                                        {
                                            new QuestStepDto { Type = "DirectBroadcast", Text = "You receive the daily token." },
                                            new QuestStepDto { Type = "Give", Wcid = (uint)(wcid + 1), Stack = 1 },
                                            new QuestStepDto { Type = "StampQuest", Stamp = "custom_daily_pickup" },
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            Creatures = new(),
        };

        private static QuestPackageDto DailyExchange(uint wcid) => new()
        {
            Package = "daily_exchange",
            CooldownSeconds = 86400,
            Stamps = new()
            {
                new QuestStampDto { Name = "custom_daily_exchange", Message = "Daily exchange complete." }
            },
            Items = new()
            {
                new QuestItemDto { Wcid = wcid + 1, Name = "Daily Offering", LongDesc = "Offer to the daily NPC." }
            },
            Actors = new()
            {
                new QuestActorDto
                {
                    Wcid = wcid,
                    Name = "Daily NPC",
                    CloneFromWcid = 78780020,
                    Flows = new()
                    {
                        new QuestFlowDto
                        {
                            Trigger = "Give",
                            GiveWcid = (uint)(wcid + 1),
                            Steps = new()
                            {
                                new QuestStepDto { Type = "Motion" },
                                new QuestStepDto
                                {
                                    Type = "InqQuest",
                                    Stamp = "custom_daily_exchange",
                                    Branches = new QuestStepBranchesDto
                                    {
                                        OnCooldown = new()
                                        {
                                            new QuestStepDto { Type = "Tell", Text = "Come back tomorrow." },
                                        },
                                        CanComplete = new()
                                        {
                                            new QuestStepDto { Type = "TakeItems", Wcid = (uint)(wcid + 1), Stack = 1 },
                                            new QuestStepDto { Type = "Give", Wcid = 300004, Stack = 1 },
                                            new QuestStepDto { Type = "StampQuest", Stamp = "custom_daily_exchange" },
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            Creatures = new(),
        };
    }
}
