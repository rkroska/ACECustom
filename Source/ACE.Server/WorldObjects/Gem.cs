using System;
using System.Linq;


using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics;

namespace ACE.Server.WorldObjects
{
    public class Gem : Stackable
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Gem(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Gem(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
        }

        /// <summary>
        /// This is raised by Player.HandleActionUseItem.<para />
        /// The item should be in the players possession.
        /// 
        /// The OnUse method for this class is to use a contract to add a tracked quest to our quest panel.
        /// This gives the player access to information about the quest such as starting and ending NPC locations,
        /// and shows our progress for kill tasks as well as any timing information such as when we can repeat the
        /// quest or how much longer we have to complete it in the case of at timed quest.   Og II
        /// </summary>
        public override void ActOnUse(WorldObject activator)
        {
            // Monster Capture System - POC: Handle capture crystals
            if (activator is Player player && MonsterCapture.IsCaptureCrystal(this))
            {
                MonsterCapture.UseCaptureCrystal(player, this);
                return;
            }

            ActOnUse(activator, false);
        }

        public void ActOnUse(WorldObject activator, bool confirmed)
        {
            if (!(activator is Player player))
                return;

            if (player.IsBusy || player.Teleporting || player.suicideInProgress)
            {
                player.SendWeenieError(WeenieError.YoureTooBusy);
                return;
            }

            if (player.IsJumping)
            {
                player.SendWeenieError(WeenieError.YouCantDoThatWhileInTheAir);
                return;
            }

            if (!string.IsNullOrWhiteSpace(UseSendsSignal))
            {
                player.CurrentLandblock?.EmitSignal(player, UseSendsSignal);
                return;
            }

            // handle rare gems
            if (RareId != null && player.GetCharacterOption(CharacterOption.ConfirmUseOfRareGems) && !confirmed)
            {
                var msg = $"Are you sure you want to use {Name}?";
                void onResponse(bool response, bool _) { if (response) { ActOnUse(activator, true); } }
                var confirm = new Confirmation_Custom(player.Guid, onResponse);
                if (!player.ConfirmationManager.EnqueueSend(confirm, msg))
                    player.SendWeenieError(WeenieError.ConfirmationInProgress);
                return;
            }

            if (RareUsesTimer)
            {
                var currentTime = Time.GetUnixTime();

                var timeElapsed = currentTime - player.LastRareUsedTimestamp;

                if (timeElapsed < RareTimer)
                {
                    // TODO: get retail message
                    var remainTime = (int)Math.Ceiling(RareTimer - timeElapsed);
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You may use another timed rare in {remainTime}s", ChatMessageType.Broadcast));
                    return;
                }
            }

            if (UseUserAnimation != MotionCommand.Invalid)
            {
                // some gems have UseUserAnimation and UseSound, similar to food
                // eg. 7559 - Condensed Dispel Potion

                // the animation is also weird, and differs from food, in that it is the full animation
                // instead of stopping at the 'eat/drink' point... so we pass 0.5 here?

                var animMod = (UseUserAnimation == MotionCommand.MimeDrink || UseUserAnimation == MotionCommand.MimeEat) ? 0.5f : 1.0f;

                player.ApplyConsumable(UseUserAnimation, () => UseGem(player), animMod);
            }
            else
                UseGem(player);
        }

        public void UseGem(Player player)
        {
            if (player.IsDead) return;

            // verify item is still valid
            if (player.FindObject(Guid.Full, Player.SearchLocations.MyInventory) == null)
            {
                //player.SendWeenieError(WeenieError.ObjectGone);   // results in 'Unable to move object!' transient error
                player.SendTransientError($"Cannot find the {Name}");   // custom message
                return;
            }

            // ── Ability Charm Toggle ──────────────────────────────────────────────────────────
            if (IsAbilityCharm && CharmGrantsAbility.HasValue)
            {
                HandleAbilityCharmToggle(player);
                return; // Do NOT consume the item
            }
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            // trying to use a dispel potion while pk timer is active
            // send error message and cancel - do not consume item
            if (SpellDID != null)
            {
                var spell = new Spell(SpellDID.Value);

                if (spell.MetaSpellType == SpellType.Dispel && !VerifyDispelPKStatus(this, player))
                    return;
            }

            if (RareUsesTimer)
            {
                var currentTime = Time.GetUnixTime();

                player.LastRareUsedTimestamp = currentTime;

                // local broadcast usage
                player.EnqueueBroadcast(new GameMessageSystemChat($"{player.Name} used the rare item {Name}", ChatMessageType.Broadcast));
            }

            if (SpellDID.HasValue)
            {
                var spell = new Spell((uint)SpellDID);

                // should be 'You cast', instead of 'Item cast'
                // omitting the item caster here, so player is also used for enchantment registry caster,
                // which could prevent some scenarios with spamming enchantments from multiple gem sources to protect against dispels

                // TODO: figure this out better
                if (spell.MetaSpellType == SpellType.PortalSummon)
                    TryCastSpell(spell, player, this, tryResist: false);
                else if (spell.IsImpenBaneType || spell.IsItemRedirectableType)
                    player.TryCastItemEnchantment_WithRedirects(spell, player, this);
                else
                    player.TryCastSpell(spell, player, this, tryResist: false);
            }

            if (UseCreateContractId > 0)
            {
                if (!player.ContractManager.Add(UseCreateContractId.Value))
                    return;

                // this wasn't in retail, but the lack of feedback when using a contract gem just seems jarring so...
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{Name} accepted. Click on the quill icon in the lower right corner to open your contract tab to view your active contracts.", ChatMessageType.Broadcast));
            }

            if (UseCreateItem > 0)
            {
                if (!HandleUseCreateItem(player))
                    return;
            }

            if (UseSound > 0)
                player.Session.Network.EnqueueSend(new GameMessageSound(player.Guid, UseSound));

            if ((GetProperty(PropertyBool.UnlimitedUse) ?? false) == false)
                player.TryConsumeFromInventoryWithNetworking(this, 1);
        }

        public bool HandleUseCreateItem(Player player)
        {
            var amount = UseCreateQuantity ?? 1;

            var itemsToReceive = new ItemsToReceive(player);

            itemsToReceive.Add(UseCreateItem.Value, amount);

            if (itemsToReceive.PlayerExceedsLimits)
            {
                if (itemsToReceive.PlayerExceedsAvailableBurden)
                    player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, "You are too encumbered to use that!"));
                else if (itemsToReceive.PlayerOutOfInventorySlots)
                    player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, "You do not have enough pack space to use that!"));
                else if (itemsToReceive.PlayerOutOfContainerSlots)
                    player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, "You do not have enough container slots to use that!"));

                return false;
            }

            if (itemsToReceive.RequiredSlots > 0)
            {
                var remaining = amount;

                while (remaining > 0)
                {
                    var item = WorldObjectFactory.CreateNewWorldObject(UseCreateItem.Value);

                    if (item is Stackable)
                    {
                        var stackSize = Math.Min(remaining, item.MaxStackSize ?? 1);

                        item.SetStackSize(stackSize);
                        remaining -= stackSize;
                    }
                    else
                        remaining--;

                    player.TryCreateInInventoryWithNetworking(item);
                }
            }
            else
            {
                player.SendTransientError($"Unable to use {Name} at this time!");
                return false;
            }
            return true;
        }

        public int? RareId
        {
            get => GetProperty(PropertyInt.RareId);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.RareId); else SetProperty(PropertyInt.RareId, value.Value); }
        }

        public bool RareUsesTimer
        {
            get => GetProperty(PropertyBool.RareUsesTimer) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.RareUsesTimer); else SetProperty(PropertyBool.RareUsesTimer, value); }
        }

        public override void HandleActionUseOnTarget(Player player, WorldObject target)
        {
            // Monster Capture System - Handle captured appearance on pet device
            if (MonsterCapture.IsCapturedAppearance(this) && target is PetDevice petDevice)
            {
                MonsterCapture.ApplyAppearanceToCrate(player, petDevice, this);
                return;
            }

            // Tailoring kits
            if (Tailoring.IsTailoringKit(WeenieClassId))
            {
                Tailoring.UseObjectOnTarget(player, this, target);
                return;
            }

            // Fallback on recipe manager
            base.HandleActionUseOnTarget(player, target);
        }

        /// <summary>
        /// For Rares that use cooldown timers (RareUsesTimer),
        /// any other rares with RareUsesTimer may not be used for 3 minutes
        /// Note that if the player logs out, this cooldown timer continues to tick/expire (unlike enchantments)
        /// </summary>
        public static int RareTimer = 180;

        public string UseSendsSignal
        {
            get => GetProperty(PropertyString.UseSendsSignal);
            set { if (value == null) RemoveProperty(PropertyString.UseSendsSignal); else SetProperty(PropertyString.UseSendsSignal, value); }
        }

        public override void OnActivate(WorldObject activator)
        {
            if (ItemUseable == Usable.Contained && activator is Player player)
            {               
                var containedItem = player.FindObject(Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems);
                if (containedItem != null) // item is contained by player
                {
                    if (player.IsBusy || player.Teleporting || player.suicideInProgress)
                    {
                        player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.YoureTooBusy));
                        player.EnchantmentManager.StartCooldown(this);
                        return;
                    }

                    if (player.IsDead)
                    {
                        player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.Dead));
                        player.EnchantmentManager.StartCooldown(this);
                        return;
                    }
                }
                else
                    return;
            }

            base.OnActivate(activator);
        }
        private void HandleAbilityCharmToggle(Player player)
        {
            var abilityId = CharmGrantsAbility!.Value;
            var abilityName = CharmAbilityRegistry.GetDisplayName(abilityId) ?? Name;

            if (!IsCharmActivated)
            {
                // Guard: only one charm per ability may be active at a time.
                // CR-35: use GetAllPossessionsDeep so charms inside nested bags are detected —
                // the same deep scan used by ValidateAbilityCharms() on login.
                var duplicate = player.GetAllPossessionsDeep()
                    .FirstOrDefault(i => i.Guid != Guid
                        && i.IsAbilityCharm
                        && i.CharmGrantsAbility == abilityId
                        && i.IsCharmActivated);

                if (duplicate != null)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                        $"You already have a {abilityName} charm active. Deactivate it first.",
                        ChatMessageType.Broadcast));
                    return;
                }
            }

            if (abilityId == CharmAbilityRegistry.AutoRebuffAbilityId)
            {
                var currentTime = Time.GetUnixTime();

                // Deactivation (turning OFF) is always free and instantly allowed
                if (IsCharmActivated)
                {
                    IsCharmActivated = false;
                    CharmAbilityRegistry.Apply(player, abilityId, false);
                    player.Session?.Network?.EnqueueSend(new GameMessageSystemChat("Auto-Rebuff Charm deactivated.", ChatMessageType.Broadcast));
                    player.Session?.Network?.EnqueueSend(new GameMessageSound(player.Guid, Sound.ShieldDown));
                    SaveBiotaToDatabase();
                    player.SaveBiotaToDatabase(enqueueSave: true);
                    return;
                }

                // Activation (turning ON)
                if (!CharmSettingsManager.AutoRebuff.Enabled)
                {
                    player.Session?.Network?.EnqueueSend(new GameMessageSystemChat("Auto-Rebuff Charm is currently disabled globally by the developer.", ChatMessageType.Broadcast));
                    return;
                }

                IsCharmActivated = true;
                CharmAbilityRegistry.Apply(player, abilityId, true, CharmLevel ?? 1);
                player.IsDispelMessageTriggered = false; // Arm the dispel alert message trigger

                var dispelLockoutActive = currentTime - player.LastDispelTimestamp < 180.0;
                if (dispelLockoutActive)
                {
                    var remainingSeconds = (int)Math.Ceiling(180.0 - (currentTime - player.LastDispelTimestamp));
                    player.Session?.Network?.EnqueueSend(new GameMessageSystemChat($"Auto-Rebuff Charm activated. Because you were recently dispelled, buffs will automatically apply in {remainingSeconds}s after your lockout expires.", ChatMessageType.Broadcast));
                }
                else
                {
                    player.ApplyUltimateBlessings();
                }
                
                SaveBiotaToDatabase();
                player.SaveBiotaToDatabase(enqueueSave: true);
                return;
            }

            if (!IsCharmActivated)
            {

                if (abilityId == CharmAbilityRegistry.UniversalSummoningMasteryAbilityId
                    && !ServerConfig.pet_charm_universal_summoning_mastery_enabled.Value)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                        "Universal Summoning Mastery is not enabled on this server.",
                        ChatMessageType.Broadcast));
                    return;
                }

                // Activate
                IsCharmActivated = true;
                CharmAbilityRegistry.Apply(player, abilityId, true, CharmLevel ?? 1);

                // ILT: Infinite Casting — tell the client comps are no longer required
                if (abilityId == CharmAbilityRegistry.InfiniteCastingAbilityId)
                {
                    player.SpellComponentsRequired = false;
                    player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyBool(player, PropertyBool.SpellComponentsRequired, false));
                }

                // ILT: Asheron's Favor — apply Health + Natural Armor enchantments
                if (abilityId == CharmAbilityRegistry.AsheronsFavorAbilityId)
                    player.ApplyAsheronsFavorEnchantments();

                var activateMsg = BuildActivationMessage(abilityId, CharmLevel ?? 1, true, player);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(activateMsg, ChatMessageType.Broadcast));
                player.Session.Network.EnqueueSend(new GameMessageSound(player.Guid, Sound.HealthUp, 1.0f));
            }
            else
            {
                // Deactivate
                IsCharmActivated = false;
                CharmAbilityRegistry.Apply(player, abilityId, false);

                // ILT: Infinite Casting — restore client comp requirement
                if (abilityId == CharmAbilityRegistry.InfiniteCastingAbilityId)
                {
                    player.SpellComponentsRequired = true;
                    player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyBool(player, PropertyBool.SpellComponentsRequired, true));
                }

                // ILT: Asheron's Favor — remove Health + Natural Armor enchantments
                if (abilityId == CharmAbilityRegistry.AsheronsFavorAbilityId)
                    player.RemoveAsheronsFavorEnchantments();

                var deactivateMsg = BuildActivationMessage(abilityId, CharmLevel ?? 1, false, player);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(deactivateMsg, ChatMessageType.Broadcast));
                player.Session.Network.EnqueueSend(new GameMessageSound(player.Guid, Sound.ShieldDown, 1.0f));
            }

            SaveBiotaToDatabase();
            player.SaveBiotaToDatabase(enqueueSave: true);
        }

        private static string BuildActivationMessage(int abilityId, int level, bool activating, Player player = null)
        {
            if (abilityId == CharmAbilityRegistry.ManaBarrierAbilityId)
            {
                if (activating)
                {
                    return level switch
                    {
                        1 => "Mana Barrier Level I activated. Your Mana will absorb incoming damage at a 1:1 ratio.",
                        2 => "Mana Barrier Level II activated. Your Mana will absorb incoming damage at a 1.5:1 ratio.",
                        3 => "Mana Barrier Level III activated. Your Mana will absorb incoming damage at a 2:1 ratio.",
                        _ => $"Mana Barrier Level {level} activated."
                    };
                }
                return $"Mana Barrier Level {level} deactivated. Your Mana is no longer absorbing damage.";
            }

            if (abilityId == CharmAbilityRegistry.InfiniteCastingAbilityId)
            {
                return activating
                    ? "Infinite Casting Stone activated. Spells will be cast without consuming components."
                    : "Infinite Casting Stone deactivated. Spell components will be consumed normally.";
            }

            if (abilityId == CharmAbilityRegistry.AsheronsFavorAbilityId)
            {
                if (activating)
                {
                    return level switch
                    {
                        1 => "Asheron's Favor activated. 10% HP + 50 Armor",
                        2 => "Greater Asheron's Favor activated. 15% HP + 100 Armor",
                        3 => "Asheron's Blessing activated. 20% HP + 250 Armor",
                        _ => "Asheron's Favor activated."
                    };
                }
                return $"Asheron's Favor (Level {level}) deactivated. The protective blessings have faded.";
            }

            if (abilityId == CharmAbilityRegistry.ShrapnelCharmAbilityId)
            {
                var knowsShrapnel = player?.SpellIsKnown(6152u) == true; // Rocky Shrapnel
                if (activating)
                {
                    if (!knowsShrapnel)
                        return "Shrapnel Charm activated. Learn Rocky Shrapnel to enable Tectonic Rifts redirection.";
                    var agonyActive = player?.HasAgonyCharm == true;
                    return agonyActive
                        ? "Shrapnel Charm activated. Rocky Shrapnel takes priority — Tectonic Rifts will cast as Rocky Shrapnel while both charms are active."
                        : "Shrapnel Charm activated. Tectonic Rifts will be cast as Rocky Shrapnel.";
                }
                else
                {
                    var agonyFallback = player?.HasAgonyCharm == true && player?.SpellIsKnown(2673u) == true;
                    return agonyFallback
                        ? "Shrapnel Charm deactivated. Tectonic Rifts will now cast as Ring of Unspeakable Agony."
                        : "Shrapnel Charm deactivated. Tectonic Rifts will cast normally.";
                }
            }

            if (abilityId == CharmAbilityRegistry.AgonyCharmAbilityId)
            {
                var knowsAgony = player?.SpellIsKnown(2673u) == true; // Ring of Unspeakable Agony
                if (activating)
                {
                    if (!knowsAgony)
                        return "Agony Charm activated. Learn Ring of Unspeakable Agony to enable Tectonic Rifts redirection.";
                    var shrapnelActive = player?.HasShrapnelCharm == true;
                    return shrapnelActive
                        ? "Agony Charm activated. Note: Rocky Shrapnel takes priority while the Shrapnel Charm is also active."
                        : "Agony Charm activated. Tectonic Rifts will be cast as Ring of Unspeakable Agony.";
                }
                else
                {
                    var shrapnelActive = player?.HasShrapnelCharm == true;
                    return shrapnelActive
                        ? "Agony Charm deactivated. Tectonic Rifts will continue casting as Rocky Shrapnel."
                        : "Agony Charm deactivated. Tectonic Rifts will cast normally.";
                }
            }

            if (abilityId == CharmAbilityRegistry.ArtisansCharmAbilityId)
            {
                if (activating)
                    return level switch
                    {
                        1 => "Artisan's Charm activated. Imbue success chance increased by 4%.",
                        2 => "Greater Artisan's Charm activated. Imbue success chance increased by 8%.",
                        3 => "Master Artisan's Charm activated. Imbue success chance increased by 12%.",
                        _ => $"Artisan's Charm (Level {level}) activated."
                    };
                return level switch
                {
                    1 => "Artisan's Charm deactivated.",
                    2 => "Greater Artisan's Charm deactivated.",
                    3 => "Master Artisan's Charm deactivated.",
                    _ => "Artisan's Charm deactivated."
                };
            }

            if (abilityId == CharmAbilityRegistry.SplitCastAbilityId)
            {
                return activating
                    ? "Split Cast Charm activated. Streak, Arc, and Bolt spells will split to target multiple distinct enemies simultaneously."
                    : "Split Cast Charm deactivated. Spells will cast normally.";
            }

            if (abilityId == CharmAbilityRegistry.ExplosiveArrowCharmAbilityId)
            {
                return activating
                    ? level switch
                    {
                        1 => "Explosive Arrow Charm activated. Arrows that hit enemies will detonate a ring of elemental damage at the impact point.",
                        2 => "Greater Explosive Arrow Charm activated. Arrows that hit enemies will detonate an intensified elemental ring at the impact point.",
                        3 => "Master Explosive Arrow Charm activated. Arrows that hit enemies will detonate a devastating elemental ring at the impact point.",
                        _ => $"Explosive Arrow Charm (Level {level}) activated."
                    }
                    : level switch
                    {
                        1 => "Explosive Arrow Charm deactivated. Arrows will no longer detonate on impact.",
                        2 => "Greater Explosive Arrow Charm deactivated.",
                        3 => "Master Explosive Arrow Charm deactivated.",
                        _ => "Explosive Arrow Charm deactivated."
                    };
            }

            if (abilityId == CharmAbilityRegistry.OmnistrikeAbilityId)
            {
                return activating
                    ? "Omni Strike Charm activated. Your melee attacks will strike with the element or physical force your target is most vulnerable to."
                    : "Omni Strike Charm deactivated. Attacks will deal damage normally.";
            }

            if (abilityId == CharmAbilityRegistry.ForkAbilityId)
            {
                return activating
                    ? level switch
                    {
                        1 => "Fork Charm activated. Your Streak, Arc, and Bolt spells will fork to nearby enemies on hit, dealing 50% damage.",
                        2 => "Greater Fork Charm activated. Fork projectiles deal 75% damage.",
                        3 => "Master Fork Charm activated. Fork projectiles deal full damage.",
                        _ => $"Fork Charm (Level {level}) activated."
                    }
                    : level switch
                    {
                        1 => "Fork Charm deactivated.",
                        2 => "Greater Fork Charm deactivated.",
                        3 => "Master Fork Charm deactivated.",
                        _ => "Fork Charm deactivated."
                    };
            }


            var name = CharmAbilityRegistry.GetDisplayName(abilityId) ?? "Ability";
            return activating ? $"{name} Level {level} activated." : $"{name} deactivated.";
        }
    }
}
