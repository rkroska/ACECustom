using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

using log4net;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Dressing Room (2026-10-05): a character's armour and clothing "look", locked in at an attendant NPC and drawn from
    /// then on whatever they really wear.
    ///
    /// Locking in costs a pyreal fee per piece that grows each time the same body area is locked again. The player keeps
    /// the pieces: the look is a copy of how they are drawn, kept as a record on the character
    /// (PropertyString.DressingRoomLook). No item is ever altered or taken, and ServerConfig.dressing_room_enabled = false
    /// puts every character back in their real gear at once.
    ///
    /// Every string here reaches the game client, so all of it is plain ASCII with "\n" line breaks (CLAUDE.md).
    /// Every fee taken and every look written has a [DressingRoom] log line.
    /// </summary>
    public static class DressingRoom
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const string Tag = "[DressingRoom]";

        /// <summary>How long a yes/no popup stays up before it counts as "no".</summary>
        private const double PopupSeconds = 60;

        private static string Num(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        // ---------------------------------------------------------------- what is drawn

        /// <summary>
        /// The look to draw on <paramref name="player"/> right now, or null for "their real gear". Called for every player
        /// ObjDesc, so the cheap checks come first: the switch, then one property read.
        /// </summary>
        public static DressingRoomLook ShownLook(Player player)
        {
            if (!ServerConfig.dressing_room_enabled.Value)
                return null;

            var look = player.GetDressingRoomLook();
            if (look == null || look.Pieces.Count == 0)
                return null;

            if (player.GetProperty(PropertyBool.DressingRoomHidden) == true)
                return null;

            return look;
        }

        private static long lastDrawFailureLog;
        private static long drawFailuresSinceLog;

        /// <summary>
        /// The look draw path threw; the caller falls back to the ordinary path, so the player is simply shown in their
        /// real gear. Logged at most once every 30 seconds with a count, so a broken look cannot flood the log.
        /// </summary>
        public static void ReportDrawFailure(Player player, Exception ex)
        {
            Interlocked.Increment(ref drawFailuresSinceLog);
            var now = (long)Time.GetUnixTime();
            var last = Interlocked.Read(ref lastDrawFailureLog);
            if (now - last < 30 || Interlocked.CompareExchange(ref lastDrawFailureLog, now, last) != last)
                return;
            var count = Interlocked.Exchange(ref drawFailuresSinceLog, 0);
            log.Error($"{Tag} drawing the look of {player.Name} (0x{player.Guid.Full:X8}) failed, real gear shown instead ({count} failure(s) since the last report): {ex}");
        }

        /// <summary>The stored look could not be read. Logged once per stored value (the parse result is cached).</summary>
        public static void ReportUnreadableLook(Player player, string stored)
        {
            log.Error($"{Tag} {player.Name} (0x{player.Guid.Full:X8}) has a stored look that cannot be read; real gear is shown. Stored value: {stored}");
        }

        private static void Refresh(Player player) => player.EnqueueBroadcast(new GameMessageObjDescEvent(player));

        // ---------------------------------------------------------------- talking

        /// <summary>The attendant tells the player; with no attendant (a command) it is a plain system line.</summary>
        private static void Say(Player player, WorldObject attendant, string text)
        {
            if (player.Session == null)
                return;
            if (attendant != null)
                player.Session.Network.EnqueueSend(new GameEventTell(attendant, text, player, ChatMessageType.Tell));
            else
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(text, ChatMessageType.Broadcast));
        }

        private static string SlotNames(uint location)
        {
            var names = new List<string>();
            foreach (var bit in DressingRoomLook.SlotBits(location))
                names.Add(SlotName(bit));
            return string.Join(", ", names);
        }

        /// <summary>What the attendant and /look call one wear slot (one EquipMask bit).</summary>
        public static string SlotName(uint bit) => (EquipMask)bit switch
        {
            EquipMask.HeadWear => "head",
            EquipMask.ChestWear => "shirt",
            EquipMask.AbdomenWear => "waist (under)",
            EquipMask.UpperArmWear => "upper arms (under)",
            EquipMask.LowerArmWear => "lower arms (under)",
            EquipMask.HandWear => "hands",
            EquipMask.UpperLegWear => "upper legs (under)",
            EquipMask.LowerLegWear => "lower legs (under)",
            EquipMask.FootWear => "feet",
            EquipMask.ChestArmor => "chest",
            EquipMask.AbdomenArmor => "abdomen",
            EquipMask.UpperArmArmor => "upper arms",
            EquipMask.LowerArmArmor => "lower arms",
            EquipMask.UpperLegArmor => "upper legs",
            EquipMask.LowerLegArmor => "lower legs",
            EquipMask.Cloak => "cloak",
            _ => $"slot 0x{bit:X}",
        };

        // ---------------------------------------------------------------- the plan

        /// <summary>What a lock-in would do, worked out the same way before the popups and again after the last "yes".</summary>
        private sealed class Plan
        {
            public readonly List<(WorldObject Item, DressingRoomPiece Piece)> Entries = new();
            public readonly List<DressingRoomPiece> Replaced = new();

            /// <summary>Worn pieces that cannot be part of a look on this body (no model for it); named to the player, not charged.</summary>
            public readonly List<string> LeftOut = new();
            public long Total;

            /// <summary>The exact items and fees. If this differs between the popup and the "yes", nothing is done.</summary>
            public string Signature => string.Join(";", Entries.Select(e => $"{e.Item.Guid.Full:X8}:{e.Piece.Fee}"));
        }

        /// <summary>
        /// True when the character has stored look text that cannot be read. That is never treated as "no look": the fee
        /// counts live in it, so nothing is locked over it or cleared from it until staff have looked.
        /// </summary>
        private static bool HasUnreadableLook(Player player)
            => !string.IsNullOrEmpty(player.GetProperty(PropertyString.DressingRoomLook)) && player.GetDressingRoomLook() == null;

        private const string UnreadableLook = "Your saved look cannot be read. Please tell an admin - nothing has been changed.";

        /// <summary>Why the player cannot use the dressing room at all right now, or null.</summary>
        private static string PlayerRefusal(Player player)
        {
            if (!ServerConfig.dressing_room_enabled.Value)
                return "The dressing room is closed today. Come back another time.";
            if (HasUnreadableLook(player))
                return UnreadableLook;
            if (player.IsOlthoiPlayer || !player.CanHoldDressingRoomLook())
                return "I cannot fit a look to a form like yours.";
            if (player.Teleporting || player.IsBusy || player.IsLoggingOut || player.PKLogout)
                return "You are busy. Speak to me again when you are free.";
            if (player.IsTrading)
                return "Finish your trade first.";
            return null;
        }

        /// <summary>
        /// Works out the lock-in for what the player is wearing right now. Returns a refusal to tell the player, or null
        /// with <paramref name="plan"/> filled. Nothing is taken from the player but the fee, so a worn piece that cannot
        /// be part of a look (it has no model for this body) is simply left out and named, not a reason to refuse.
        /// </summary>
        private static string BuildPlan(Player player, out Plan plan)
        {
            plan = null;

            var refusal = PlayerRefusal(player);
            if (refusal != null)
                return refusal;

            var look = player.GetDressingRoomLook() ?? new DressingRoomLook();
            var setupId = Creature.DressingRoomClothingSetup(player.SetupTableId);
            var baseFee = ServerConfig.dressing_room_fee_base.Value;
            var growth = ServerConfig.dressing_room_fee_growth.Value;
            var cap = ServerConfig.dressing_room_fee_cap.Value;
            var now = (long)Time.GetUnixTime();

            var result = new Plan();

            // lowest slot first, so the popup, the chat lines and the log always list the outfit in the same order
            foreach (var item in player.EquippedObjects.Values.OrderBy(i => (uint)(i.CurrentWieldedLocation ?? EquipMask.None)))
            {
                var location = (uint)(item.CurrentWieldedLocation ?? EquipMask.None);
                if ((location & DressingRoomLook.SlotMask) == 0)
                    continue;   // weapons, shields, jewellery: never part of a look

                // the two layering groups Creature.CalculateObjDesc draws; anything else worn is not drawn, so not saved
                var extremityOrArmour = (item.CurrentWieldedLocation & (EquipMask.Armor | EquipMask.Extremity)) != 0;
                if (!(item.ItemType == ItemType.Armor || extremityOrArmour) && item.ItemType != ItemType.Clothing)
                    continue;

                if (item.IsDestroyed)
                    continue;

                if (!item.ClothingBase.HasValue || !Creature.DressingRoomPieceDraws(item.ClothingBase.Value, setupId))
                {
                    result.LeftOut.Add(item.Name);
                    continue;
                }

                // A dye being tried on is drawn now but is not the item's own colour yet, so the look would come out
                // in a different colour from what the player is looking at. Ask again once it is kept or washed out.
                if (item.ActiveForgeDye() != item.ForgeDyePalette)
                    return $"You are still trying a dye on your {item.Name}. Keep it or wash it out first, then speak to me again.";

                // stamp the armour sort key exactly as drawing the item does, then read it
                item.setVisualClothingPriority();

                var piece = new DressingRoomPiece
                {
                    Wcid = item.WeenieClassId,
                    Name = item.Name,
                    Guid = item.Guid.Full,
                    Location = location,
                    ItemType = (uint)item.ItemType,
                    ClothingBase = item.ClothingBase.Value,
                    PaletteTemplate = item.PaletteTemplate,
                    Shade = item.Shade,
                    ClothingPriority = (uint?)item.ClothingPriority,
                    VisualPriority = (uint?)item.VisualClothingPriority,
                    TopLayer = item.TopLayerPriority,
                    // the colour as worn: a kept forge dye goes into the look (a try-on that was not kept does not)
                    ForgeDye = item.ForgeDyePalette,
                    LockedAt = now,
                    Fee = DressingRoomLook.Fee(look.PriorLocks((uint)(item.ClothingPriority ?? 0)), baseFee, growth, cap),
                };

                // a total past what a long can hold would wrap negative and be "paid" for nothing
                if (piece.Fee < 0 || piece.Fee > long.MaxValue - result.Total)
                    return "The fee for this outfit is too large to charge. Please tell an admin - nothing has been changed.";

                result.Entries.Add((item, piece));
                result.Total += piece.Fee;
            }

            if (result.Entries.Count == 0)
            {
                if (result.LeftOut.Count > 0)
                    return $"Nothing you are wearing would show as a look on you ({string.Join(", ", result.LeftOut)}). Wear something else and speak to me again.";
                return "Wear the armour and clothing you want as your look, then speak to me again. I copy how each piece looks - you keep the pieces - and whatever you wear afterwards will look like them. Type /look to see what you have saved.";
            }

            foreach (var saved in look.Pieces)
                if (result.Entries.Any(e => DressingRoomLook.Conflicts(saved, e.Piece)))
                    result.Replaced.Add(saved);

            if (!PyrealFee.CanPay(player, result.Total))
                return $"Locking in {Pieces(result.Entries.Count)} costs {Num(result.Total)} pyreals. You have {Num(PyrealFee.Funds(player))} between your bank and your pack.";

            plan = result;
            return null;
        }

        private static string Pieces(int count) => count == 1 ? "1 piece" : $"{count} pieces";

        // ---------------------------------------------------------------- the attendant

        /// <summary>A player used a dressing room attendant (Creature.ActOnUse).</summary>
        public static void HandleUse(Player player, WorldObject attendant)
        {
            try
            {
                var refusal = BuildPlan(player, out var plan);
                if (refusal != null)
                {
                    Say(player, attendant, refusal);
                    return;
                }

                var signature = plan.Signature;
                var count = plan.Entries.Count;
                var total = plan.Total;
                var question = $"Lock in the {Pieces(count)} you are wearing as your look for {Num(total)} pyreals?\n\n"
                    + $"{NameList(plan.Entries.Select(e => e.Item.Name))}\n\n"
                    + "You keep the pieces. Only the pyreals are taken.";

                log.Info($"{Tag} {player.Name} (0x{player.Guid.Full:X8}) offered a lock-in at {attendant?.Name}: {count} piece(s), fee {total}, items {signature}");

                var sent = player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, (yes, timedOut) =>
                {
                    if (!yes || timedOut)
                    {
                        log.Info($"{Tag} {player.Name} declined the lock-in (timedOut={timedOut})");
                        Say(player, attendant, "Nothing was changed.");
                        return;
                    }
                    Execute(player, attendant, signature);
                }), question, PopupSeconds);

                if (!sent)
                {
                    Say(player, attendant, "Answer the question already in front of you first.");
                    return;
                }

                // the detail goes to chat, where it can be read at leisure; the popup stays short
                foreach (var (item, piece) in plan.Entries)
                    Say(player, attendant, $"{item.Name} ({SlotNames(piece.Location)}): {Num(piece.Fee)} pyreals.");
                if (plan.LeftOut.Count > 0)
                    Say(player, attendant, $"Left out, because it would not show on you: {string.Join(", ", plan.LeftOut)}.");
                if (plan.Replaced.Count > 0)
                    Say(player, attendant, $"This replaces what you have saved: {string.Join(", ", plan.Replaced.Select(p => p.Name))}.");
            }
            catch (Exception ex)
            {
                log.Error($"{Tag} HandleUse failed for {player.Name}: {ex}");
            }
        }

        /// <summary>The names for the popup, cut short so a long outfit cannot overflow the dialog (chat has the full list).</summary>
        private static string NameList(IEnumerable<string> names)
        {
            const int limit = 300;
            var text = string.Join(", ", names);
            return text.Length <= limit ? text : text.Substring(0, limit) + "...";
        }

        /// <summary>
        /// Takes the fee and writes the look. No item is touched: the look is a copy of how each worn piece is drawn
        /// (owner 2026-10-07; until then the pieces were destroyed, and players lost gear they had not meant to give).
        /// The plan is rebuilt first: if the outfit or its price is not exactly what the popup described, nothing happens.
        /// If the look cannot be written after the fee was taken, the fee is handed back.
        /// </summary>
        private static void Execute(Player player, WorldObject attendant, string signature)
        {
            Plan plan;
            try
            {
                var refusal = BuildPlan(player, out plan);
                if (refusal == null && plan.Signature != signature)
                    refusal = "What you are wearing, or its price, changed while I was asking. Nothing was changed - speak to me again.";
                if (refusal != null)
                {
                    log.Info($"{Tag} {player.Name} confirmed but the lock-in was refused: {refusal}");
                    Say(player, attendant, refusal);
                    return;
                }
            }
            catch (Exception ex)
            {
                log.Error($"{Tag} Execute could not rebuild the plan for {player.Name}; nothing was changed: {ex}");
                Say(player, attendant, "Something went wrong. Nothing was changed.");
                return;
            }

            var fundsBefore = PyrealFee.Funds(player);
            if (!PyrealFee.Charge(player, plan.Total, Tag))
            {
                log.Info($"{Tag} {player.Name} confirmed but could not be charged {plan.Total} (funds {fundsBefore}); nothing was changed");
                Say(player, attendant, "I could not take the fee, so nothing was changed.");
                return;
            }
            log.Info($"{Tag} {player.Name} (0x{player.Guid.Full:X8}) charged {plan.Total} pyreals for {plan.Entries.Count} piece(s): funds {fundsBefore} -> {PyrealFee.Funds(player)}");

            var before = player.GetProperty(PropertyString.DressingRoomLook);
            try
            {
                foreach (var (item, piece) in plan.Entries)
                    log.Info($"{Tag} {player.Name} copying {Describe(item, piece)}");

                var next = (player.GetDressingRoomLook() ?? new DressingRoomLook()).WithLocked(plan.Entries.Select(e => e.Piece).ToList());
                var stored = next.Serialize();

                // read it back before trusting it: a look the reader would refuse must never be left on a character
                if (DressingRoomLook.Parse(stored) == null)
                    throw new InvalidOperationException($"the look that was built cannot be read back: {stored}");

                player.SetProperty(PropertyString.DressingRoomLook, stored);
                // a look just paid for is shown, even if an earlier one had been hidden
                player.RemoveProperty(PropertyBool.DressingRoomHidden);
                log.Info($"{Tag} {player.Name} (0x{player.Guid.Full:X8}) look written. Before: {before ?? "(none)"} After: {stored}");
            }
            catch (Exception ex)
            {
                // put the stored look back as it was and hand the fee back
                if (before == null)
                    player.RemoveProperty(PropertyString.DressingRoomLook);
                else
                    player.SetProperty(PropertyString.DressingRoomLook, before);
                player.BankedPyreals = (player.BankedPyreals ?? 0) + plan.Total;
                player.RefreshCoinValueAfterBankChange();
                log.Error($"{Tag} {player.Name} (0x{player.Guid.Full:X8}): the look could not be written; the fee of {plan.Total} was refunded to the bank and the look left as it was: {ex}");
                // any pack coins the fee took are already gone from the database, so store the bank credit now too
                try
                {
                    player.SavePlayerToDatabase();
                }
                catch (Exception saveEx)
                {
                    log.Error($"{Tag} {player.Name} (0x{player.Guid.Full:X8}): the refund of {plan.Total} could not be saved at once (the next ordinary save will store it): {saveEx}");
                }
                Say(player, attendant, plan.Total > 0
                    ? "Something went wrong, so I changed nothing. Your pyreals are back in your bank."
                    : "Something went wrong, so I changed nothing.");
                return;
            }

            try
            {
                // the fee and the look are saved together, now
                player.SavePlayerToDatabase();
                Refresh(player);
            }
            catch (Exception ex)
            {
                log.Error($"{Tag} {player.Name} (0x{player.Guid.Full:X8}): the look was written and {plan.Total} pyreals charged, but the immediate save or refresh failed (the next ordinary save will store both). Look: {player.GetProperty(PropertyString.DressingRoomLook)} Error: {ex}");
            }

            Say(player, attendant, $"It is done. {Pieces(plan.Entries.Count)} locked in for {Num(plan.Total)} pyreals. You keep your gear, and whatever you wear from now on, this is how you will look. Type /look to see it listed.");
        }

        /// <summary>Everything about a worn piece that went into the look, for the log.</summary>
        private static string Describe(WorldObject item, DressingRoomPiece piece)
            => $"{item.Name} wcid {item.WeenieClassId} guid 0x{item.Guid.Full:X8} location 0x{piece.Location:X} ({SlotNames(piece.Location)}) clothingBase 0x{piece.ClothingBase:X8}"
                + $" paletteTemplate {(piece.PaletteTemplate?.ToString(CultureInfo.InvariantCulture) ?? "null")} shade {(piece.Shade?.ToString("R", CultureInfo.InvariantCulture) ?? "null")}"
                + $" forgeDye {(piece.ForgeDye.HasValue ? $"0x{piece.ForgeDye.Value:X8}" : "null")} fee {piece.Fee}";

        // ---------------------------------------------------------------- /look

        /// <summary>/look: what is saved, whether it is showing, and what the next piece costs.</summary>
        public static void ShowStatus(Player player)
        {
            if (!ServerConfig.dressing_room_enabled.Value)
            {
                Say(player, null, "The dressing room is closed. Everyone is shown in their real gear.");
                return;
            }

            if (HasUnreadableLook(player))
            {
                Say(player, null, UnreadableLook);
                return;
            }

            var look = player.GetDressingRoomLook();
            if (look == null || look.Pieces.Count == 0)
            {
                Say(player, null, "You have no saved look. Wear the armour and clothing you want to look like, then speak to a dressing room attendant. You keep the pieces; a fee is charged.");
                return;
            }

            var hidden = player.GetProperty(PropertyBool.DressingRoomHidden) == true;
            var text = $"Your saved look ({Pieces(look.Pieces.Count)}), {(hidden ? "hidden - your real gear is showing" : "showing")}:\n";
            // a saved piece that cannot draw on this body (the character changed race, or it was saved before such pieces
            // were refused) is listed as such; it hides nothing, so the real gear in its slot shows
            var setupId = Creature.DressingRoomClothingSetup(player.SetupTableId);
            foreach (var piece in look.Pieces.OrderBy(p => p.Location))
                text += $"  {piece.Name} ({SlotNames(piece.Location)}){(Creature.DressingRoomPieceDraws(piece.ClothingBase, setupId) ? "" : " - does not show on your body")}\n";
            if (!player.CanHoldDressingRoomLook())
                text += "It cannot be drawn on your current form.\n";
            text += "/look hide shows your real gear, /look show brings the look back, /look clear removes it for good.";
            Say(player, null, text);
        }

        /// <summary>/look hide and /look show: free, and the saved look is kept either way.</summary>
        public static void SetHidden(Player player, bool hidden)
        {
            if (!ServerConfig.dressing_room_enabled.Value)
            {
                Say(player, null, "The dressing room is closed. Everyone is shown in their real gear.");
                return;
            }

            var look = player.GetDressingRoomLook();
            if (look == null || look.Pieces.Count == 0)
            {
                Say(player, null, "You have no saved look.");
                return;
            }

            if (hidden)
                player.SetProperty(PropertyBool.DressingRoomHidden, true);
            else
                player.RemoveProperty(PropertyBool.DressingRoomHidden);

            log.Info($"{Tag} {player.Name} (0x{player.Guid.Full:X8}) set their look {(hidden ? "hidden" : "showing")}");
            Refresh(player);
            Say(player, null, hidden ? "Your real gear is showing. Your look is still saved - /look show brings it back." : "Your look is showing.");
        }

        /// <summary>/look clear: removes the saved look after a yes/no. Nothing is returned and the lock counts stay.</summary>
        public static void OfferClear(Player player)
        {
            if (!ServerConfig.dressing_room_enabled.Value)
            {
                Say(player, null, "The dressing room is closed. Everyone is shown in their real gear.");
                return;
            }
            if (HasUnreadableLook(player))
            {
                Say(player, null, UnreadableLook);
                return;
            }

            var look = player.GetDressingRoomLook();
            if (look == null || look.Pieces.Count == 0)
            {
                Say(player, null, "You have no saved look.");
                return;
            }

            var question = $"Remove your saved look ({Pieces(look.Pieces.Count)})?\n\nNo pyreals are refunded, and your next look will cost as much as if you had kept this one.";

            var sent = player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, (yes, timedOut) =>
            {
                try
                {
                    if (!yes || timedOut)
                    {
                        Say(player, null, "Your look was kept.");
                        return;
                    }

                    var current = ServerConfig.dressing_room_enabled.Value ? player.GetDressingRoomLook() : null;
                    if (current == null || current.Pieces.Count == 0)
                    {
                        Say(player, null, "You have no saved look.");
                        return;
                    }

                    var before = player.GetProperty(PropertyString.DressingRoomLook);
                    var stored = current.WithoutPieces().Serialize();
                    player.SetProperty(PropertyString.DressingRoomLook, stored);
                    player.RemoveProperty(PropertyBool.DressingRoomHidden);
                    log.Info($"{Tag} {player.Name} (0x{player.Guid.Full:X8}) cleared their look. Before: {before} After: {stored}");
                    Refresh(player);
                    Say(player, null, "Your saved look is gone. You are shown in your real gear.");
                }
                catch (Exception ex)
                {
                    log.Error($"{Tag} clearing the look of {player.Name} failed: {ex}");
                }
            }), question, PopupSeconds);

            if (!sent)
                Say(player, null, "Answer the question already in front of you first.");
        }
    }
}
