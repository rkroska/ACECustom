using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Text;

using log4net;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// The forge as players use it. A player hands a weapon to a forge smith NPC (PropertyBool.ForgeSmith): the smith
    /// never takes it, only remembers it as that player's MAIN weapon; the next weapon handed over is offered as a
    /// forge in a confirmation popup, with the fee. Everything is re-checked when the popup is answered, because the
    /// pack, the bank and a trade window can all change while it is open.
    ///
    /// State is per player, never per smith, so any number of players can use one smith at once.
    /// </summary>
    public static partial class ForgeService
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>How long a smith remembers a player's main weapon.</summary>
        public static readonly TimeSpan SelectionLifetime = TimeSpan.FromMinutes(5);

        /// <summary>Pyreal coin weenie, for the part of a fee the bank cannot cover.</summary>
        private const uint PyrealWcid = 273;

        private static readonly ConcurrentDictionary<uint, (uint MainGuid, DateTime At)> selections = new();
        /// <summary>How long a forge, hone or unbind popup stays open before the server closes it.</summary>
        public const double PopupSeconds = 30;

        /// <summary>Grace added to a popup's own timeout before its lock is treated as abandoned.</summary>
        private const double PopupLockMargin = 15;

        /// <summary>
        /// Players with a forge popup open, and when that popup's lock runs out. One popup at a time per character.
        /// The lock is released when the popup is answered or times out - but neither happens if the player logs out
        /// or drops link with it open (the answer handler needs the player online), so every lock also EXPIRES by the
        /// clock. Without that, one disconnect at the wrong moment would shut the character out until a restart.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, DateTime> forging = new();

        /// <summary>Takes the popup lock for <paramref name="seconds"/> (the popup's timeout) plus a margin. False when a live one is held.</summary>
        public static bool TryLock(uint playerGuid, double seconds, DateTime nowUtc)
        {
            var until = nowUtc.AddSeconds(Math.Max(1, seconds) + PopupLockMargin);
            while (true)
            {
                if (forging.TryAdd(playerGuid, until))
                    return true;
                if (!forging.TryGetValue(playerGuid, out var held))
                    continue;                                   // released between the two calls: try again
                if (held > nowUtc)
                    return false;                               // a live popup
                if (forging.TryUpdate(playerGuid, until, held))
                    return true;                                // an abandoned one: taken over
            }
        }

        private static bool TryLock(Player player, double seconds) => TryLock(player.Guid.Full, seconds, DateTime.UtcNow);

        public static void Unlock(uint playerGuid) => forging.TryRemove(playerGuid, out _);

        private static void Unlock(Player player) => Unlock(player.Guid.Full);

        // ---------------------------------------------------------------- selection

        public static void Select(Player player, WorldObject main) => selections[player.Guid.Full] = (main.Guid.Full, DateTime.UtcNow);

        public static void ClearSelection(Player player) => selections.TryRemove(player.Guid.Full, out _);

        /// <summary>The player's remembered main weapon, if it is still in their pack and the selection has not expired.</summary>
        public static WorldObject GetSelection(Player player)
        {
            if (!selections.TryGetValue(player.Guid.Full, out var s))
                return null;
            if (DateTime.UtcNow - s.At > SelectionLifetime)
            {
                selections.TryRemove(player.Guid.Full, out _);
                return null;
            }
            return player.FindObject(s.MainGuid, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems);
        }

        // ---------------------------------------------------------------- the smith

        /// <summary>A weapon was handed to a forge smith. The item never leaves the player.</summary>
        public static void HandleGive(Player player, WorldObject smith, WorldObject item)
        {
            if (!ServerConfig.forge_enabled.Value || !ServerConfig.forge_smith_enabled.Value)
            {
                Say(player, smith, "The forge is cold today. Come back another time.");
                return;
            }

            var reason = ForgeWeaponReader.RefusalReason(item);
            if (reason != null)
            {
                Say(player, smith, reason);
                return;
            }

            var main = GetSelection(player);
            log.Info($"[ForgeSmith] {player.Name} handed {item.Name} (0x{item.Guid.Full:X8}) to {smith.Name}; main selected: {(main == null ? "none" : $"0x{main.Guid.Full:X8}")}");
            if (main == null || main.Guid == item.Guid)
            {
                Select(player, item);
                Say(player, smith, $"I have the measure of your {item.Name}. It will keep its shape, its tinkering and its colour. Now hand me the one to melt into it.");
                return;
            }

            Offer(player, smith, main, item, ServerConfig.forge_fee_pyreals.Value);
        }

        /// <summary>
        /// Checks the pair, then asks the player to confirm. <paramref name="smith"/> is null for the developer command,
        /// which also passes a fee of 0.
        /// </summary>
        public static void Offer(Player player, WorldObject smith, WorldObject main, WorldObject feeder, long fee)
        {
            var refusal = CheckForgeable(player, main, feeder) ?? CheckFee(player, fee);
            if (refusal != null)
            {
                // At a smith a refused pair also forgets the main weapon, so nobody is stuck with a mistaken first pick.
                if (smith != null)
                {
                    ClearSelection(player);
                    refusal += " Hand me your main piece again to start over.";
                }
                Say(player, smith, refusal);
                return;
            }

            if (!TryLock(player, PopupSeconds))
            {
                Say(player, smith, "A forge is already in progress.");
                return;
            }

            var mainId = main.Guid.Full;
            var feederId = feeder.Guid.Full;
            var price = fee > 0 ? $" for {fee:N0} pyreals" : "";
            var question = $"Forge {main.Name} with {feeder.Name}{price}?\n\n{feeder.Name} will be destroyed. The result keeps {main.Name}'s tinkers, imbue and dye, and is Attuned and Bonded.\n\nThis cannot be undone.";

            var sent = player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, (yes, timedOut) =>
            {
                log.Info($"[ForgeSmith] {player.Name} answered the forge popup: yes={yes} timedOut={timedOut} (main 0x{mainId:X8}, feeder 0x{feederId:X8})");
                try
                {
                    if (!yes || timedOut)
                    {
                        // No is also how a player backs out of a mistaken main weapon.
                        ClearSelection(player);
                        Say(player, smith, "The forge goes cold. Nothing was changed. Hand me a weapon or a piece of armour to start over.");
                        return;
                    }
                    // Everything may have changed while the popup was open: find the weapons again and re-check.
                    var m = player.FindObject(mainId, Player.SearchLocations.MyInventory);
                    var f = player.FindObject(feederId, Player.SearchLocations.MyInventory);
                    var again = m == null || f == null ? "One of the two is no longer in your pack." : CheckForgeable(player, m, f) ?? CheckFee(player, fee);
                    if (again != null)
                    {
                        Say(player, smith, $"The forge goes cold: {again}");
                        return;
                    }
                    if (Execute(player, smith, m, f, fee))
                        ClearSelection(player);
                }
                finally
                {
                    Unlock(player);
                }
            }), question, PopupSeconds);

            log.Info($"[ForgeSmith] {player.Name} forge popup {(sent ? "sent" : "NOT sent (another popup is open)")}: {main.Name} (0x{mainId:X8}) + {feeder.Name} (0x{feederId:X8}), fee {fee}");
            if (!sent)
            {
                Unlock(player);
                Say(player, smith, "Answer the popup you already have open first.");
            }
        }

        /// <summary>Null when the pair may be forged right now, else an ASCII reason.</summary>
        public static string CheckForgeable(Player player, WorldObject main, WorldObject feeder)
        {
            if (main.Guid == feeder.Guid)
                return "A thing cannot be forged with itself.";
            var reason = ForgeWeaponReader.RefusalReason(main) ?? ForgeWeaponReader.RefusalReason(feeder) ?? ForgeWeaponReader.PairRefusalReason(main, feeder);
            if (reason != null)
                return reason;
            foreach (var w in new[] { main, feeder })
            {
                if (w.CurrentWieldedLocation != null)
                    return $"Unwield {w.Name} first.";
                if (player.FindObject(w.Guid.Full, Player.SearchLocations.MyInventory) == null)
                    return $"{w.Name} must be in your pack.";
                if (player.ItemsInTradeWindow.Contains(w.Guid))
                    return $"{w.Name} is in a trade window.";
            }
            return null;
        }

        // ---------------------------------------------------------------- the fee

        /// <summary>
        /// Pyreal coins (wcid 273) in the player's packs, counted off the stacks themselves. Never Player.CoinValue: on this
        /// server that number already INCLUDES the banked pyreals (Player.UpdateCoinValue, for the wealth display) and every
        /// other coin-type item, so adding it to the bank counts the bank twice.
        /// </summary>
        public static long PackPyreals(Player player)
        {
            long total = 0;
            foreach (var stack in player.GetInventoryItemsOfWCID(PyrealWcid))
                total += stack.StackSize ?? 1;
            return total;
        }

        /// <summary>Pyreals the player can pay with: banked pyreals plus pyreal coins carried.</summary>
        public static long Funds(Player player) => (player.BankedPyreals ?? 0) + PackPyreals(player);

        /// <summary>
        /// How a fee is split: the bank first, then coins from the pack. Null when the two together cannot cover it (or
        /// the pack's share is more than one consume call can take).
        /// </summary>
        public static (long FromBank, long FromPack)? SplitFee(long bank, long pack, long fee)
        {
            if (fee <= 0)
                return (0, 0);
            var fromBank = Math.Min(Math.Max(0, bank), fee);
            var fromPack = fee - fromBank;
            if (fromPack > Math.Max(0, pack) || fromPack > int.MaxValue)
                return null;
            return (fromBank, fromPack);
        }

        private static string CheckFee(Player player, long fee)
            => fee > 0 && SplitFee(player.BankedPyreals ?? 0, PackPyreals(player), fee) == null
                ? $"The work costs {fee:N0} pyreals. You have {Funds(player):N0} between your bank and your pack."
                : null;

        /// <summary>Takes <paramref name="fee"/> from the bank first, then from pyreal coins in the pack. All or nothing.</summary>
        private static bool Charge(Player player, long fee)
        {
            if (fee <= 0)
                return true;
            var bank = player.BankedPyreals ?? 0;
            var pack = PackPyreals(player);
            var split = SplitFee(bank, pack, fee);
            if (split == null)
                return false;
            var (fromBank, fromPack) = split.Value;

            // Coins first: this is the step that can fail, so the bank is only touched once it has succeeded. The consume
            // call reports success even when it ran out of stacks early, so what was really taken is counted, not assumed.
            if (fromPack > 0)
            {
                var consumed = player.TryConsumeFromInventoryWithNetworking(PyrealWcid, (int)fromPack);
                var taken = pack - PackPyreals(player);
                if (!consumed || taken != fromPack)
                {
                    log.Error($"[Forge] {player.Name}: fee {fee} needed {fromPack} pyreal coins from the pack but {taken} were taken (consume returned {consumed}); the bank was not touched");
                    return false;
                }
            }
            if (fromBank > 0)
                player.BankedPyreals = bank - fromBank;
            return true;
        }

        // ---------------------------------------------------------------- the forge

        /// <summary>
        /// Rolls, builds and hands over the result, takes the fee, then destroys both inputs. Order matters: the new item
        /// is created and saved FIRST, so a crash in between can only leave an extra item (the [Forge] log line lets staff
        /// remove it), never destroy the player's weapons or take their pyreals for nothing.
        /// </summary>
        public static bool Execute(Player player, WorldObject smith, WorldObject main, WorldObject feeder, long fee)
        {
            var config = ForgeMath.ForgeConfig.FromServerConfig();
            var m = ForgeWeaponReader.Read(main);
            var f = ForgeWeaponReader.Read(feeder);
            var outcome = ForgeMath.Forge(m, f, config, () => ACE.Common.ThreadSafeRandom.Next(0.0f, 1.0f));
            var built = ForgeWeaponWriter.Build(main, feeder, outcome, config);
            if (built.Error != null)
            {
                Say(player, smith, $"The forge goes cold: {built.Error}. Nothing was changed.");
                log.Warn($"[Forge] {player.Name}: build failed for {main.Name} (0x{main.Guid.Full:X8}) + {feeder.Name} (0x{feeder.Guid.Full:X8}): {built.Error}");
                return false;
            }

            var item = built.Item;
            if (!player.TryCreateInInventoryWithNetworking(item))
            {
                Say(player, smith, "There is no room in your pack for the result. Nothing was changed.");
                return false;
            }

            var mainName = main.Name;
            var feederName = feeder.Name;
            var mainId = main.Guid.Full;
            var feederId = feeder.Guid.Full;

            var charged = Charge(player, fee);
            var consumedMain = player.TryConsumeFromInventoryWithNetworking(main);
            var consumedFeeder = player.TryConsumeFromInventoryWithNetworking(feeder);
            player.SaveBiotaToDatabase();

            log.Info($"[Forge] {player.Name}: {mainName} (0x{mainId:X8}) + {feederName} (0x{feederId:X8}) -> {item.Name} (0x{item.Guid.Full:X8}) " +
                     $"group '{m.Group}' mode {config.RollMode} fee {fee} charged {charged} spark {outcome.Spark} " +
                     $"draws [{string.Join(",", outcome.RngDraws.Select(d => d.ToString("R", CultureInfo.InvariantCulture)))}] " +
                     $"consumed main {consumedMain} feeder {consumedFeeder}");
            if (!charged || !consumedMain || !consumedFeeder)
            {
                log.Error($"[Forge] {player.Name}: forged 0x{item.Guid.Full:X8} but charged={charged} consumedMain={consumedMain} (0x{mainId:X8}) consumedFeeder={consumedFeeder} (0x{feederId:X8})");
                Say(player, smith, "The forge finished, but something could not be settled. Please tell a staff member.");
            }

            player.EnqueueBroadcast(new GameMessageScript(player.Guid, outcome.Spark ? PlayScript.AetheriaLevelUp : PlayScript.EnchantUpRed));

            // The reveal: what came from where.
            var sb = new StringBuilder();
            sb.Append($"The forge rings out. You made: {item.Name}\n");
            foreach (var pick in outcome.Picks.Where(p => p.Kind == ForgeMath.PickKind.Line))
            {
                var line = (ForgeMath.ForgeLine)pick.Key;
                if (!outcome.Result.Lines.TryGetValue(line, out var v))
                    continue;
                sb.Append(pick.Blended
                    ? $"  {ForgeMath.LineName(line)} {Num(v)}: between {pick.MainValue} and {pick.FeederValue}\n"
                    : $"  {ForgeMath.LineName(line)} {Num(v)}: from the {(pick.FromMain ? mainName : feederName)}\n");
            }
            if (outcome.Result.Element != m.Element)
                sb.Append(item.WeenieClassId != main.WeenieClassId ? $"  The element changed: it is now a {item.Name}.\n" : "  The element stayed: this model has no version in the other one.\n");
            foreach (var family in m.Spells.Keys.Union(f.Spells.Keys).Where(k => !outcome.Result.Spells.ContainsKey(k)))
                sb.Append($"  {ForgeWeaponReader.FamilyName(family)} was lost to the fire.\n");
            if (outcome.Spark)
                sb.Append($"  SPARK! The steel takes an edge: +1 hone on {ForgeMath.LineName(outcome.SparkLine)}.\n");
            if (fee > 0 && charged)
                sb.Append($"  You paid {fee:N0} pyreals.\n");
            player.Session.Network.EnqueueSend(new GameMessageSystemChat(sb.ToString().TrimEnd('\n'), ChatMessageType.Craft));

            if (outcome.Spark)
                player.EnqueueBroadcast(new GameMessageSystemChat($"{player.Name}'s forge flares white-hot!", ChatMessageType.Craft), WorldObject.LocalBroadcastRange, ChatMessageType.Craft);

            return true;
        }

        private static string Num(double v) => v == Math.Floor(v) ? ((long)v).ToString() : v.ToString("0.###");

        /// <summary>The smith tells the player; with no smith (developer command) it is a plain system line.</summary>
        private static void Say(Player player, WorldObject smith, string text)
        {
            if (smith != null)
                player.Session.Network.EnqueueSend(new GameEventTell(smith, text, player, ChatMessageType.Tell));
            else
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(text, ChatMessageType.System));
        }
    }
}
