using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Services;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// The forge tools a player uses ON a weapon: hone stones (at a grindstone), dyes (at a dye vat), flux (spent
    /// automatically by honing) and unbinding oil. A tool is any item with PropertyInt.ForgeTool; it is a reusable
    /// instrument that picks WHAT to do - the pyreals are charged by the action itself, never by using the tool up.
    /// Every action is offered in a confirmation popup and re-checked when it is answered.
    /// </summary>
    public static partial class ForgeService
    {
        /// <summary>PropertyInt.ForgeTool values. Stored on items: never renumber.</summary>
        public enum ForgeTool
        {
            None = 0,
            HoneStone = 1,
            Dye = 2,
            Flux = 3,
            UnbindingOil = 4,
        }

        public static ForgeTool ToolOf(WorldObject item) => (ForgeTool)(item?.GetProperty(PropertyInt.ForgeTool) ?? 0);

        public static bool IsForgeTool(WorldObject item) => ToolOf(item) != ForgeTool.None;

        /// <summary>A forge tool was used on <paramref name="target"/>.</summary>
        public static void HandleToolUse(Player player, WorldObject tool, WorldObject target)
        {
            if (!ServerConfig.forge_enabled.Value)
            {
                Say(player, null, "The smithy is closed for now.");
                return;
            }

            // Each part of the smithy has its own switch on top of forge_enabled, so a server can open the dye vats
            // alone: a stray grindstone or a traded hone stone then still does nothing.
            var kind = ToolOf(tool);
            var closed = kind switch
            {
                ForgeTool.HoneStone or ForgeTool.Flux => !ServerConfig.forge_hone_enabled.Value ? "The grindstones are not turning yet." : null,
                ForgeTool.Dye => !ServerConfig.forge_dye_enabled.Value ? "The dye vats are not open yet." : null,
                ForgeTool.UnbindingOil => !ServerConfig.forge_unbind_enabled.Value ? "The oil has no use yet." : null,
                _ => null,
            };
            if (closed != null)
            {
                Say(player, null, closed);
                return;
            }

            var arg = tool.GetProperty(PropertyInt.ForgeToolArg) ?? 0;
            switch (kind)
            {
                case ForgeTool.HoneStone:
                    OfferHone(player, target, (ForgeMath.ForgeLine)arg);
                    break;
                case ForgeTool.Dye:
                    // used on yourself: every worn piece of armour and clothing takes the same colour
                    if (target == player)
                        OfferDye(player, player.EquippedObjects.Values.Where(w => IsGarment(w) && ForgeDyes.GarmentIndices(w).Count > 0).ToList(), (ForgeDyes.Family)arg, true);
                    else
                        OfferDye(player, new List<WorldObject> { target }, (ForgeDyes.Family)arg, false);
                    break;
                case ForgeTool.UnbindingOil:
                    OfferUnbind(player, target);
                    break;
                case ForgeTool.Flux:
                    Say(player, null, "Flux works by itself: keep it in your pack and one is used each time you hone.");
                    break;
            }
        }

        /// <summary>True when a station of this kind (a world object flagged with <paramref name="flag"/>) is within forge_station_range.</summary>
        public static bool IsNearStation(Player player, PropertyBool flag)
        {
            var range = ServerConfig.forge_station_range.Value;
            var known = player.PhysicsObj?.ObjMaint?.GetKnownObjectsValues();
            if (known == null)
                return false;
            foreach (var obj in known)
            {
                var wo = obj.WeenieObj?.WorldObject;
                if (wo != null && wo.GetProperty(flag) == true && wo.Location != null && player.Location.DistanceTo(wo.Location) <= range)
                    return true;
            }
            return false;
        }

        /// <summary>The player's own weapon, in the pack or in hand and not in a trade: null when fine, else an ASCII reason.</summary>
        private static string CheckOwnWeapon(Player player, WorldObject weapon)
        {
            if (!(weapon is MeleeWeapon || weapon is MissileLauncher || weapon is Caster))
                return $"{weapon?.Name ?? "That"} is not a weapon.";
            if (player.FindObject(weapon.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) == null)
                return $"{weapon.Name} must be in your pack or in your hand.";
            if (player.ItemsInTradeWindow.Contains(weapon.Guid))
                return $"{weapon.Name} is in a trade window.";
            return null;
        }

        /// <summary>Sends the weapon's new look and numbers to the player and everyone nearby, and saves it.</summary>
        public static void Refresh(Player player, WorldObject weapon)
        {
            weapon.ChangesDetected = true;
            weapon.SaveBiotaToDatabase();
            player.EnqueueBroadcast(new GameMessageUpdateObject(weapon));
            if (weapon.CurrentWieldedLocation != null)
                player.EnqueueBroadcast(new GameMessageObjDescEvent(player));
            else if (player.FindObject(weapon.Guid.Full, Player.SearchLocations.MyInventory) != null)
                player.MoveItemToFirstContainerSlot(weapon);
        }

        /// <summary>
        /// Redraws <paramref name="items"/> for the player and everyone nearby: one update per item and ONE redraw of the
        /// wearer however many pieces are worn. Saves only when <paramref name="save"/> is set. A dye being tried on is
        /// free and can be repeated without limit, so it must not cost a database write per piece per try; the try-on
        /// properties expire by the clock, and ride along with the item's next ordinary save.
        /// </summary>
        public static void Redraw(Player player, IReadOnlyList<WorldObject> items, bool save)
        {
            var worn = false;
            foreach (var item in items)
            {
                if (save)
                {
                    item.ChangesDetected = true;
                    item.SaveBiotaToDatabase();
                }
                player.EnqueueBroadcast(new GameMessageUpdateObject(item));
                worn |= item.CurrentWieldedLocation != null;
            }
            if (worn)
                player.EnqueueBroadcast(new GameMessageObjDescEvent(player));
        }

        // ---------------------------------------------------------------- honing

        private static WorldObject FindFlux(Player player)
            => player.GetAllPossessions().FirstOrDefault(i => ToolOf(i) == ForgeTool.Flux);

        /// <summary>Everything a hone attempt needs right now, or the reason it cannot happen.</summary>
        private static string PlanHone(Player player, WorldObject weapon, ForgeMath.ForgeLine line, out ForgeMath.ForgeWeapon state,
                                       out ForgeMath.ForgeConfig config, out long cost, out double chance, out double fluxBonus)
        {
            state = null; config = null; cost = 0; chance = 0; fluxBonus = 0;
            var reason = CheckOwnWeapon(player, weapon) ?? ForgeWeaponReader.RefusalReason(weapon);
            if (reason != null)
                return reason;
            if (!IsNearStation(player, PropertyBool.ForgeGrindstone))
                return "You need to stand at a grindstone to hone a weapon.";

            config = ForgeMath.ForgeConfig.FromServerConfig();
            state = ForgeWeaponReader.Read(weapon);
            if (!ForgeMath.IsHonable(line))
                return "That cannot be honed.";
            if (!state.Lines.ContainsKey(line))
                return $"{weapon.Name} has no {ForgeMath.LineName(line)} to hone.";
            // A stat of 0 gains nothing from a percentage, so the pyreals would buy nothing - unless this is a tier 11+
            // weapon whose quality carries that stat, where the hone scales the quality result instead.
            if (state.Lines[line] == 0 && !ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.HoneScalesQuality(weapon, line))
                return $"{weapon.Name} has no {ForgeMath.LineName(line)} to hone.";
            if (state.HoneTotal >= config.HoneMaxLevels)
                return $"{weapon.Name} is honed as far as steel can go (+{state.HoneTotal}).";

            fluxBonus = FindFlux(player) != null ? ServerConfig.forge_flux_bonus.Value : 0;
            cost = ForgeMath.HoneCost(state.HoneTotal, config);
            chance = ForgeMath.HoneChance(state.HoneTotal, state.HoneMisfortune, fluxBonus, config);
            return CheckFee(player, cost);
        }

        public static void OfferHone(Player player, WorldObject weapon, ForgeMath.ForgeLine line)
        {
            var reason = PlanHone(player, weapon, line, out var state, out _, out var cost, out var chance, out var fluxBonus);
            if (reason != null)
            {
                Say(player, null, reason);
                return;
            }
            if (!TryLock(player, PopupSeconds))
            {
                Say(player, null, "Finish what you are doing at the forge first.");
                return;
            }

            var weaponId = weapon.Guid.Full;
            var lineName = ForgeMath.LineName(line);
            var question = $"Hone {weapon.Name}: {lineName} +{state.HoneLevel(line) + 1}?\n\n" +
                           $"Chance {chance:P0}. Cost {cost:N0} pyreals, paid whether it takes or not." +
                           (state.HoneMisfortune > 0 ? $"\nYour last {state.HoneMisfortune} failed attempt{(state.HoneMisfortune == 1 ? "" : "s")} are counted in that chance." : "") +
                           (fluxBonus > 0 ? $"\nOne Smith's Flux will be used (+{fluxBonus:P0})." : "") +
                           "\n\nThe weapon is never harmed. Honing binds it to you.";

            var sent = player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, (yes, timedOut) =>
            {
                try
                {
                    if (!yes || timedOut)
                        return;
                    var w = player.FindObject(weaponId, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems);
                    if (w == null)
                    {
                        Say(player, null, "The weapon is no longer with you.");
                        return;
                    }
                    ExecuteHone(player, w, line);
                }
                finally
                {
                    Unlock(player);
                }
            }), question, PopupSeconds);

            if (!sent)
            {
                Unlock(player);
                Say(player, null, "Answer the popup you already have open first.");
            }
        }

        private static void ExecuteHone(Player player, WorldObject weapon, ForgeMath.ForgeLine line)
        {
            // planned again: the popup may have been open while pyreals, flux or the weapon changed
            var reason = PlanHone(player, weapon, line, out var state, out var config, out var cost, out _, out var fluxBonus);
            if (reason != null)
            {
                Say(player, null, reason);
                return;
            }

            var roll = (double)ACE.Common.ThreadSafeRandom.Next(0.0f, 1.0f);
            var outcome = ForgeMath.Hone(state, line, fluxBonus, config, () => roll);
            if (outcome.Refused != ForgeMath.HoneRefusal.None)
            {
                Say(player, null, "That cannot be honed.");
                return;
            }

            if (!Charge(player, cost))
            {
                Say(player, null, "You cannot cover the cost.");
                return;
            }
            if (fluxBonus > 0)
            {
                var flux = FindFlux(player);
                if (flux != null)
                    player.TryConsumeFromInventoryWithNetworking(flux, 1);
            }

            var lineName = ForgeMath.LineName(line);
            string applyError = null;
            if (outcome.Success)
                applyError = ForgeWeaponWriter.ApplyHone(weapon, line, outcome.Result.HoneLevel(line), config);

            if (outcome.Result.HoneMisfortune > 0 && applyError == null)
                weapon.SetProperty(PropertyInt.ForgeHoneMisfortune, outcome.Result.HoneMisfortune);
            else
                weapon.RemoveProperty(PropertyInt.ForgeHoneMisfortune);
            ForgeWeaponWriter.Bind(weapon);
            Refresh(player, weapon);
            player.SaveBiotaToDatabase();

            log.Info($"[Hone] {player.Name}: {weapon.Name} (0x{weapon.Guid.Full:X8}) {lineName} total {state.HoneTotal} -> {outcome.Result.HoneTotal} " +
                     $"cost {cost} chance {outcome.Chance:0.###} roll {outcome.Roll:R} success {outcome.Success} flux {fluxBonus} misfortune {outcome.Result.HoneMisfortune}" +
                     (applyError != null ? $" APPLY FAILED: {applyError}" : ""));

            if (applyError != null)
            {
                log.Error($"[Hone] {player.Name}: paid {cost} but the hone could not be written on 0x{weapon.Guid.Full:X8}: {applyError}");
                Say(player, null, "The hone took, but could not be set. Please tell a staff member.");
                return;
            }

            if (outcome.Success)
            {
                player.EnqueueBroadcast(new GameMessageScript(player.Guid, PlayScript.EnchantUpRed));
                Say(player, null, $"The edge takes. {weapon.Name}: {lineName} +{outcome.Result.HoneLevel(line)} (honed +{outcome.Result.HoneTotal} in all). You paid {cost:N0} pyreals.");
                if (outcome.Result.HoneTotal >= config.HoneMaxLevels)
                    player.EnqueueBroadcast(new GameMessageSystemChat($"{player.Name} has honed a weapon to perfection!", ChatMessageType.Craft), WorldObject.LocalBroadcastRange, ChatMessageType.Craft);
            }
            else
                Say(player, null, $"The edge does not take. The steel remembers your effort: your next attempt is {config.HoneMisfortuneStep:P0} likelier. You paid {cost:N0} pyreals.");
        }

        // ---------------------------------------------------------------- dyes

        /// <summary>Whether a dye would actually show on this model (after the dyeable-twin texture swaps).</summary>
        public static bool TakesDye(WorldObject weapon)
        {
            var swaps = string.Join(",", WorldObject.GetDyeTwinSwaps(weapon.SetupTableId).Select(t => $"{t.Part}:{t.Old}:{t.New}"));
            var (fixedPolys, drawnPolys) = PetMutationService.MeasureFixedColourPolygons(weapon.SetupTableId, null, swaps);
            return !PetMutationService.IsFixedColourModel(fixedPolys, drawnPolys);
        }

        /// <summary>Armour or clothing that is drawn on the wearer's body (shields and jewellery are not).</summary>
        public static bool IsGarment(WorldObject wo)
            => wo != null && !(wo is MeleeWeapon || wo is MissileLauncher || wo is Caster)
               && ((wo.ValidLocations ?? EquipMask.None) & (EquipMask.Clothing | EquipMask.Armor | EquipMask.Cloak)) != 0;

        /// <summary>Why this one item cannot be dyed right now, or null.</summary>
        private static string CheckDyeable(Player player, WorldObject item)
        {
            if (!IsGarment(item))
            {
                var reason = CheckOwnWeapon(player, item);
                if (reason != null)
                    return item is MeleeWeapon || item is MissileLauncher || item is Caster ? reason : $"{item?.Name ?? "That"} is not a weapon, armour or clothing.";
                return TakesDye(item) ? null : $"{item.Name}'s finish does not take dye.";
            }
            if (player.FindObject(item.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) == null)
                return $"{item.Name} must be in your pack or worn.";
            if (player.ItemsInTradeWindow.Contains(item.Guid))
                return $"{item.Name} is in a trade window.";
            return ForgeDyes.GarmentIndices(item).Count > 0 ? null : $"{item.Name}'s finish does not take dye.";
        }

        /// <summary>
        /// Tries one colour on every item in <paramref name="items"/> (one weapon, one garment, or with
        /// <paramref name="suit"/> everything the player wears) and asks whether to keep it. The fee is per piece.
        /// </summary>
        public static void OfferDye(Player player, List<WorldObject> items, ForgeDyes.Family family, bool suit)
        {
            string reason = null;
            if (items.Count == 0)
                reason = suit ? "You are wearing nothing that takes dye." : "That cannot be dyed.";
            if (reason == null && !IsNearStation(player, PropertyBool.ForgeDyeVat))
                reason = "You need to stand at a dye vat to dye anything.";
            if (reason == null && !suit)
                reason = CheckDyeable(player, items[0]);
            if (reason == null && suit)
                items = items.Where(i => CheckDyeable(player, i) == null).ToList();
            if (reason == null && items.Count == 0)
                reason = "You are wearing nothing that takes dye.";

            var each = family == ForgeDyes.Family.Any ? ServerConfig.forge_dye_fee_pyreals.Value : ServerConfig.forge_dye_family_fee_pyreals.Value;
            var fee = each * Math.Max(1, items.Count);
            reason ??= CheckFee(player, fee);
            uint? palette = null;
            var what = suit ? "what you are wearing" : items.Count > 0 ? items[0].Name : "that";
            if (reason == null)
            {
                var roll = ACE.Common.ThreadSafeRandom.Next(0.0f, 1.0f);
                palette = IsGarment(items[0]) ? ForgeDyes.RollGarments(items, family, roll) : ForgeDyes.Roll(items[0].SetupTableId, family, roll);
                if (palette == null)
                    reason = $"No {ForgeDyes.Name(family)} dye shows on {what}.";
            }
            if (reason != null)
            {
                Say(player, null, reason);
                return;
            }
            if (!TryLock(player, Math.Max(5, ServerConfig.forge_dye_preview_seconds.Value)))
            {
                Say(player, null, "Finish what you are doing at the forge first.");
                return;
            }

            void ClearPreview(WorldObject w)
            {
                w.RemoveProperty(PropertyInt.ForgeDyePreview);
                w.RemoveProperty(PropertyInt64.ForgeDyePreviewUntil);
            }

            // The price of a dye is the price of a TRY (owner, 2026-10-07): the pyreals go when the colour is put on,
            // per piece, and are spent whether the player then keeps it or not. Taken before the popup so a logout or a
            // dropped link cannot turn a try into a free look. forge_dye_charge_on_try FALSE restores the older rule,
            // where trying is free and only a kept colour is paid for.
            var chargeOnTry = ServerConfig.forge_dye_charge_on_try.Value;
            if (chargeOnTry)
            {
                if (!Charge(player, fee))
                {
                    Unlock(player);
                    Say(player, null, "You cannot cover the cost.");
                    return;
                }
                player.SaveBiotaToDatabase();
            }

            // Try it on: drawn until the preview runs out, kept only on Yes.
            var seconds = Math.Max(5, ServerConfig.forge_dye_preview_seconds.Value);
            foreach (var item in items)
            {
                item.SetProperty(PropertyInt.ForgeDyePreview, (int)palette.Value);
                item.SetProperty(PropertyInt64.ForgeDyePreviewUntil, (long)ACE.Common.Time.GetUnixTime() + seconds);
            }
            Redraw(player, items, false);

            var ids = items.Select(i => i.Guid.Full).ToList();
            var previewed = (int)palette.Value;
            string question;
            if (chargeOnTry)
                question = (suit
                    ? $"You paid {fee:N0} pyreals ({each:N0} each) to try this colour on the {items.Count} piece{(items.Count == 1 ? "" : "s")} you are wearing. Keep it?"
                    : $"You paid {fee:N0} pyreals to try this colour on {items[0].Name}. Keep it?")
                    + "\n\nNo washes it out. The pyreals are spent either way.";
            else
                question = (suit
                    ? $"Keep this colour on the {items.Count} piece{(items.Count == 1 ? "" : "s")} you are wearing for {fee:N0} pyreals ({each:N0} each)?"
                    : $"Keep this colour on {items[0].Name} for {fee:N0} pyreals?")
                    + "\n\nNo washes it out at no cost. You can try as many colours as you like.";
            var sent = player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, (yes, timedOut) =>
            {
                try
                {
                    var found = ids.Select(id => player.FindObject(id, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems)).Where(w => w != null).ToList();
                    if (found.Count == 0)
                        return;
                    // only the pieces still showing this try-on are kept and paid for
                    var keep = found.Where(w => w.GetProperty(PropertyInt.ForgeDyePreview) == previewed && CheckDyeable(player, w) == null).ToList();
                    var paid = each * keep.Count;
                    var kept = false;
                    if (yes && !timedOut && keep.Count > 0)
                    {
                        if (chargeOnTry)
                        {
                            // already paid for when the colour went on
                            foreach (var w in keep)
                                w.ForgeDyePalette = previewed;
                            kept = true;
                        }
                        else
                        {
                            var again = CheckFee(player, paid);
                            if (again != null)
                                Say(player, null, again);
                            else if (Charge(player, paid))
                            {
                                foreach (var w in keep)
                                    w.ForgeDyePalette = previewed;
                                kept = true;
                            }
                        }
                    }
                    foreach (var w in found)
                        ClearPreview(w);
                    // a kept colour is paid for, so it is saved at once with the pyreals; a washed-out try-on is not
                    Redraw(player, found, kept);
                    if (kept)
                        player.SaveBiotaToDatabase();
                    log.Info($"[Dye] {player.Name}: {string.Join(", ", found.Select(w => $"{w.Name} (0x{w.Guid.Full:X8})"))} palette 0x{previewed:X8} family {family} fee {(chargeOnTry ? fee : kept ? paid : 0)} {(chargeOnTry ? "paid on try" : "paid on keep")} kept {kept}");
                    if (chargeOnTry)
                        Say(player, null, kept ? $"The colour holds. This try cost {fee:N0} pyreals." : $"The dye washes out. This try cost {fee:N0} pyreals.");
                    else
                        Say(player, null, kept ? $"The colour holds. You paid {paid:N0} pyreals." : "The dye washes out.");
                }
                finally
                {
                    Unlock(player);
                }
            }), question, seconds);

            if (!sent)
            {
                Unlock(player);
                foreach (var item in items)
                    ClearPreview(item);
                Redraw(player, items, false);
                if (chargeOnTry && fee > 0)
                {
                    // the colour was never offered, so the try is not owed: back to the bank, whichever purse paid
                    player.BankedPyreals = (player.BankedPyreals ?? 0) + fee;
                    player.SaveBiotaToDatabase();
                    log.Info($"[Dye] {player.Name}: popup could not be shown; {fee} pyreals returned to the bank");
                }
                Say(player, null, "Answer the popup you already have open first.");
            }
        }

        // ---------------------------------------------------------------- unbinding

        public static void OfferUnbind(Player player, WorldObject weapon)
        {
            // forged armour is bound too, so the oil takes anything the forge made
            var reason = ForgeWeaponReader.IsArmorKind(weapon)
                ? (player.FindObject(weapon.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) == null ? $"{weapon.Name} must be in your pack."
                    : player.ItemsInTradeWindow.Contains(weapon.Guid) ? $"{weapon.Name} is in a trade window." : null)
                : CheckOwnWeapon(player, weapon);
            var forged = (weapon.GetProperty(PropertyInt.ForgeCount) ?? 0) > 0 || !string.IsNullOrEmpty(weapon.GetProperty(PropertyString.ForgeHoneLevels));
            if (reason == null && !forged)
                reason = $"{weapon.Name} was never forged or honed; the oil does nothing to it.";
            if (reason == null && weapon.GetProperty(PropertyInt.Attuned) != (int)AttunedStatus.Attuned)
                reason = $"{weapon.Name} is not bound.";
            if (reason == null && weapon.CurrentWieldedLocation != null)
                reason = $"Unwield {weapon.Name} first: wielding it binds it again.";
            long fee = 0;
            if (reason == null)
            {
                fee = ForgeMath.UnbindFee(ForgeWeaponReader.Read(weapon), ForgeMath.ForgeConfig.FromServerConfig());
                reason = CheckFee(player, fee);
            }
            if (reason != null)
            {
                Say(player, null, reason);
                return;
            }
            if (!TryLock(player, PopupSeconds))
            {
                Say(player, null, "Finish what you are doing at the forge first.");
                return;
            }

            var weaponId = weapon.Guid.Full;
            var question = $"Unbind {weapon.Name} for {fee:N0} pyreals?\n\nIt can then be traded or given away. Whoever wields it next is bound to it again.";
            var sent = player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, (yes, timedOut) =>
            {
                try
                {
                    if (!yes || timedOut)
                        return;
                    var w = player.FindObject(weaponId, Player.SearchLocations.MyInventory);
                    if (w == null || w.GetProperty(PropertyInt.Attuned) != (int)AttunedStatus.Attuned)
                    {
                        Say(player, null, "The weapon is no longer in your pack, or is no longer bound.");
                        return;
                    }
                    var again = CheckFee(player, fee);
                    if (again != null)
                    {
                        Say(player, null, again);
                        return;
                    }
                    if (!Charge(player, fee))
                        return;
                    w.RemoveProperty(PropertyInt.Attuned);
                    w.RemoveProperty(PropertyInt.Bonded);
                    w.SetProperty(PropertyBool.ForgeRebindOnWield, true);
                    Refresh(player, w);
                    player.SaveBiotaToDatabase();
                    log.Info($"[Unbind] {player.Name}: {w.Name} (0x{w.Guid.Full:X8}) fee {fee}");
                    Say(player, null, $"The binding loosens. {w.Name} can change hands until it is next wielded. You paid {fee:N0} pyreals.");
                }
                finally
                {
                    Unlock(player);
                }
            }), question, PopupSeconds);

            if (!sent)
            {
                Unlock(player);
                Say(player, null, "Answer the popup you already have open first.");
            }
        }

        /// <summary>Called when any item is wielded: an unbound forge weapon binds to its new wielder.</summary>
        public static void OnWield(WorldObject item, Creature wielder)
        {
            if (item.GetProperty(PropertyBool.ForgeRebindOnWield) != true)
                return;
            ForgeWeaponWriter.Bind(item);
            item.ChangesDetected = true;
            if (wielder is Player player)
            {
                player.Session.Network.EnqueueSend(new GameMessageUpdateObject(item));
                Say(player, null, $"{item.Name} binds itself to you.");
                log.Info($"[Unbind] {player.Name}: {item.Name} (0x{item.Guid.Full:X8}) re-bound on wield");
            }
        }
    }
}
