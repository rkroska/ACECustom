using System;

using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Pet tailoring: move a combat pet essence's entire look onto another combat pet essence.
    ///
    /// Extract: use the Pet Tailoring Kit on a source essence. A filled kit is created that carries
    /// every visual property the capture system writes (model, tables, palettes, clothing, scale,
    /// name, variant, equipment, captured appearance strings, portrait icon). The source essence
    /// and the tool are consumed, but only AFTER the filled kit is safely in the player's pack.
    ///
    /// Apply: use the filled kit on a target essence. Only the visual properties are written; the
    /// target keeps its own stats, potency, bond, sex, mutations, maturity and imprint. The kit is
    /// consumed once, after the apply succeeds. Same ordering rules as MonsterCapture: snapshot,
    /// verify, write, then consume.
    /// </summary>
    public static class PetTailoring
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public const uint NeuteringKitWcid = 98760399;
        public const uint TailoringKitWcid = 98760400;
        public const uint FilledTailoringKitWcid = 98760401;

        public static bool IsTailoringTool(uint wcid) => wcid == TailoringKitWcid || wcid == FilledTailoringKitWcid;

        /// <summary>True if this device's pet is currently summoned by the player.</summary>
        private static bool IsSummoned(Player player, PetDevice device)
        {
            return player.CurrentActivePet is CombatPet pet && pet.SummoningDeviceGuid == device.Guid;
        }

        // ------------------------------------------------------------------
        // Extract
        // ------------------------------------------------------------------

        /// <summary>[PetTrace] one refusal record for either direction, with the reason.</summary>
        private static void TraceRefused(string evt, Player player, WorldObject tool, WorldObject target, string reason)
        {
            PetTrace.Begin(evt, PetTrace.NewSessionId()).AddPlayer("p.", player)
                .Add("tool", tool?.Name).AddGuid("toolGuid", tool?.Guid.Full ?? 0).Add("target", target?.Name).AddGuid("targetGuid", target?.Guid.Full ?? 0)
                .Add("applied", false).Add("consumed", false).Add("reason", reason).Emit();
        }

        public static void HandleExtract(Player player, WorldObject tool, WorldObject target)
        {
            if (target is not PetDevice source || !source.IsCombatPetDevice())
            {
                if (PetTrace.Enabled) TraceRefused("tailoring.extract", player, tool, target, "target is not a combat pet essence");
                player.SendTransientError("The Pet Tailoring Kit can only extract the appearance of a combat pet essence.");
                return;
            }

            if (!source.VisualOverrideSetup.HasValue)
            {
                if (PetTrace.Enabled) TraceRefused("tailoring.extract", player, tool, target, "no captured appearance");
                player.SendTransientError($"{source.Name} has no captured appearance to extract.");
                return;
            }

            if (IsSummoned(player, source))
            {
                if (PetTrace.Enabled) TraceRefused("tailoring.extract", player, tool, target, "pet is summoned");
                player.SendTransientError($"Dismiss {source.Name}'s pet before extracting its appearance.");
                return;
            }

            if (source.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow) || tool.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            {
                if (PetTrace.Enabled) TraceRefused("tailoring.extract", player, tool, target, "item in trade window");
                player.SendTransientError("You cannot tailor an item that is in the trade window.");
                return;
            }

            // Build the filled kit from a snapshot of the source. Nothing is consumed yet.
            var kit = WorldObjectFactory.CreateNewWorldObject(FilledTailoringKitWcid);
            if (kit == null)
            {
                player.SendTransientError("The filled Pet Tailoring Kit could not be created.");
                log.Error($"[PetTailoring] Weenie {FilledTailoringKitWcid} is missing from the world database.");
                return;
            }

            CopyVisuals(source, kit, source.IconId);

            var creatureName = source.VisualOverrideName ?? source.Name;
            kit.Name = $"Pet Tailoring Kit ({creatureName})";
            // The kit shows the creature's portrait so a pack full of kits is legible.
            if (source.IconId != 0)
                kit.IconId = source.IconId;

            if (!player.TryCreateInInventoryWithNetworking(kit))
            {
                if (PetTrace.Enabled) TraceRefused("tailoring.extract", player, tool, target, "pack full for the filled kit");
                player.SendTransientError("Your pack is full. Make room for the filled kit before extracting.");
                kit.Destroy();
                return;
            }

            // The kit is in the pack: now consume the source essence and the tool. If either consume
            // fails, back the kit out so nothing is duplicated.
            if (!player.TryConsumeFromInventoryWithNetworking(source, 1))
            {
                player.TryConsumeFromInventoryWithNetworking(kit, 1);
                if (PetTrace.Enabled) TraceRefused("tailoring.extract", player, tool, target, "source essence consume failed; kit backed out");
                player.SendTransientError("Failed to consume the source essence.");
                return;
            }
            var toolConsumed = player.TryConsumeFromInventoryWithNetworking(tool, 1);
            if (!toolConsumed)
            {
                // The source is gone; keep the kit rather than punish the player for a tool hiccup.
                log.Warn($"[PetTailoring] Consumed source essence but could not consume tool {tool.Guid} for {player.Name}; kit kept.");
            }

            player.PlayParticleEffect(PlayScript.AttribDownRed, player.Guid);
            player.SendMessage($"You extract the appearance of {creatureName} into the kit. The source essence is consumed.");
            if (PetTrace.Enabled)
                PetTrace.Begin("tailoring.extract", PetTrace.NewSessionId()).AddPlayer("p.", player)
                    .Add("tool", tool.Name).AddGuid("toolGuid", tool.Guid.Full).Add("toolConsumed", toolConsumed)
                    .Add("source", source.Name).AddGuid("sourceGuid", source.Guid.Full).Add("sourceWcid", source.WeenieClassId).Add("sourceConsumed", true)
                    .Add("kit", kit.Name).AddGuid("kitGuid", kit.Guid.Full).Add("kitWcid", kit.WeenieClassId).AddGuid("kitIcon", kit.IconId)
                    .Add("applied", true).AddVisuals("read.", kit).Emit();
            else
                log.Info($"[PetTailoring] {player.Name} extracted {creatureName} (setup 0x{source.VisualOverrideSetup:X8}) into kit 0x{kit.Guid.Full:X8}.");
        }

        // ------------------------------------------------------------------
        // Apply
        // ------------------------------------------------------------------

        public static void HandleApply(Player player, WorldObject kit, WorldObject target)
        {
            if (target is not PetDevice device || !device.IsCombatPetDevice())
            {
                if (PetTrace.Enabled) TraceRefused("tailoring.apply", player, kit, target, "target is not a combat pet essence");
                player.SendTransientError("A filled Pet Tailoring Kit can only be applied to a combat pet essence.");
                return;
            }

            var setup = kit.GetProperty(PropertyDataId.VisualOverrideSetup) ?? 0;
            if (setup == 0)
            {
                if (PetTrace.Enabled) TraceRefused("tailoring.apply", player, kit, target, "kit holds no appearance");
                player.SendTransientError("This kit holds no appearance.");
                return;
            }

            if (IsSummoned(player, device))
            {
                if (PetTrace.Enabled) TraceRefused("tailoring.apply", player, kit, target, "pet is summoned");
                player.SendTransientError($"Dismiss {device.Name}'s pet before tailoring it.");
                return;
            }

            if (device.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow) || kit.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            {
                if (PetTrace.Enabled) TraceRefused("tailoring.apply", player, kit, target, "item in trade window");
                player.SendTransientError("You cannot tailor an item that is in the trade window.");
                return;
            }

            // [PetTrace] the device's look before the kit overwrites it (the record is written after).
            PetTrace.Record traceBefore = null;
            if (PetTrace.Enabled)
                traceBefore = PetTrace.Begin("tailoring.apply", PetTrace.NewSessionId()).AddPlayer("p.", player)
                    .Add("kit", kit.Name).AddGuid("kitGuid", kit.Guid.Full).Add("kitWcid", kit.WeenieClassId)
                    .Add("target", device.Name).AddGuid("targetGuid", device.Guid.Full).Add("targetWcid", device.WeenieClassId)
                    .AddVisuals("read.", kit).AddVisuals("before.", device);

            var nameBefore = device.Name;
            var previousCreatureName = device.VisualOverrideName;

            // Visuals only. Everything else on the device is untouched.
            CopyVisuals(kit, device, kit.GetProperty(PropertyDataId.VisualOverrideIcon) ?? 0);

            // Portrait: the kit carries the source essence's icon.
            var portrait = kit.GetProperty(PropertyDataId.VisualOverrideIcon) ?? 0;
            if (portrait != 0)
                device.IconId = portrait;

            // Name: same rebuild the capture system uses, so "Fire Drudge Essence (200)" style names
            // swap the creature head and keep the template tail.
            var rebuilt = PetDevice.BuildDisplayNameAfterCaptureApply(device.Name, previousCreatureName, device.VisualOverrideName);
            if (!string.IsNullOrEmpty(rebuilt))
                device.Name = rebuilt;
            SyncUseString(device, nameBefore, device.Name);

            // Push name and icon to the client. Per-property updates alone are not enough: the client
            // caches inventory names and art, so the capture path also sends a full object snapshot and
            // mirrors the slot move the client performs when it receives one. Same here.
            player.UpdateProperty(device, PropertyString.Name, device.Name ?? "");
            var useStr = device.GetProperty(PropertyString.Use);
            if (useStr != null)
                player.UpdateProperty(device, PropertyString.Use, useStr);
            player.UpdateProperty(device, PropertyDataId.Icon, device.IconId);

            player.EnqueueBroadcast(new ACE.Server.Network.GameMessages.Messages.GameMessageUpdateObject(device));
            if (device.CurrentWieldedLocation != null)
                player.EnqueueBroadcast(new ACE.Server.Network.GameMessages.Messages.GameMessageObjDescEvent(player));
            else if (player.FindObject(device.Guid.Full, Player.SearchLocations.MyInventory) != null)
                player.MoveItemToFirstContainerSlot(device);

            // Keep the device dirty until the next batched player save so a fast relog cannot load a
            // stale copy (SaveBiotaToDatabase is async and clears the flag early).
            device.SaveBiotaToDatabase();
            device.ChangesDetected = true;
            player.RushNextPlayerSave(0);

            var kitConsumed = player.TryConsumeFromInventoryWithNetworking(kit, 1);
            if (!kitConsumed)
                log.Warn($"[PetTailoring] Applied kit 0x{kit.Guid.Full:X8} to {device.Name} but could not consume it for {player.Name}.");

            player.PlayParticleEffect(PlayScript.EnchantUpPurple, device.Guid);
            player.SendMessage($"You tailor the appearance onto {device.Name}. Summon it to see the new look.");
            if (traceBefore != null)
                traceBefore.AddVisuals("after.", device).Add("nameBefore", nameBefore).Add("nameAfter", device.Name).AddGuid("iconAfter", device.IconId)
                    .Add("applied", true).Add("consumed", kitConsumed).Emit();
            else
                log.Info($"[PetTailoring] {player.Name} applied kit 0x{kit.Guid.Full:X8} (setup 0x{setup:X8}) to {device.Name} (0x{device.Guid.Full:X8}).");
        }

        // ------------------------------------------------------------------
        // The visual property set. One list, used in both directions, so extract and apply can
        // never drift apart. Mirrors what MonsterCapture writes to an essence.
        // ------------------------------------------------------------------

        private static void CopyVisuals(WorldObject from, WorldObject to, uint portraitIcon)
        {
            CopyDid(from, to, PropertyDataId.VisualOverrideSetup);
            CopyDid(from, to, PropertyDataId.VisualOverrideMotionTable);
            CopyDid(from, to, PropertyDataId.VisualOverrideCombatTable);
            CopyDid(from, to, PropertyDataId.VisualOverrideSoundTable);
            CopyDid(from, to, PropertyDataId.VisualOverridePaletteBase);
            CopyDid(from, to, PropertyDataId.VisualOverrideClothingBase);

            CopyInt(from, to, PropertyInt.VisualOverridePaletteTemplate);
            CopyFloat(from, to, PropertyFloat.VisualOverrideShade);
            CopyFloat(from, to, PropertyFloat.VisualOverrideScale);

            CopyString(from, to, PropertyString.CapturedCreatureName);
            CopyString(from, to, PropertyString.CapturedItems);
            CopyString(from, to, PropertyString.CapturedObjDescAnimParts);
            CopyString(from, to, PropertyString.CapturedObjDescPalettes);
            CopyString(from, to, PropertyString.CapturedObjDescTextures);

            CopyInt(from, to, PropertyInt.CapturedCreatureVariant);
            CopyInt(from, to, PropertyInt.CapturedCreatureType);
            CopyInt(from, to, PropertyInt.CapturedCreatureWCID);
            CopyInt(from, to, PropertyInt.CapturedSourceDamageType);

            // The portrait travels as VisualOverrideIcon on the kit and is applied to IconId on the device.
            if (portraitIcon != 0)
                to.SetProperty(PropertyDataId.VisualOverrideIcon, portraitIcon);
            else
                to.RemoveProperty(PropertyDataId.VisualOverrideIcon);
        }

        private static void CopyDid(WorldObject from, WorldObject to, PropertyDataId p)
        {
            var v = from.GetProperty(p);
            if (v.HasValue && v.Value != 0) to.SetProperty(p, v.Value); else to.RemoveProperty(p);
        }

        private static void CopyInt(WorldObject from, WorldObject to, PropertyInt p)
        {
            var v = from.GetProperty(p);
            if (v.HasValue) to.SetProperty(p, v.Value); else to.RemoveProperty(p);
        }

        private static void CopyFloat(WorldObject from, WorldObject to, PropertyFloat p)
        {
            var v = from.GetProperty(p);
            if (v.HasValue) to.SetProperty(p, v.Value); else to.RemoveProperty(p);
        }

        private static void CopyString(WorldObject from, WorldObject to, PropertyString p)
        {
            var v = from.GetProperty(p);
            if (!string.IsNullOrEmpty(v)) to.SetProperty(p, v); else to.RemoveProperty(p);
        }

        /// <summary>Same as MonsterCapture's use-string sync: swap the creature head in the Use text.</summary>
        private static void SyncUseString(PetDevice device, string nameBefore, string nameAfter)
        {
            var use = device.GetProperty(PropertyString.Use);
            if (string.IsNullOrEmpty(use) || string.IsNullOrEmpty(nameBefore) || string.IsNullOrEmpty(nameAfter))
                return;

            static string Head(string name)
            {
                var idx = name.LastIndexOf(" Essence", StringComparison.OrdinalIgnoreCase);
                return idx < 0 ? name : name.Substring(0, idx);
            }

            var oldHead = Head(nameBefore);
            var newHead = Head(nameAfter);
            if (string.IsNullOrEmpty(oldHead) || oldHead.Equals(newHead, StringComparison.Ordinal))
                return;

            var updated = use.Replace(oldHead, newHead, StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(updated, use, StringComparison.Ordinal))
                device.SetProperty(PropertyString.Use, updated);
        }
    }
}
