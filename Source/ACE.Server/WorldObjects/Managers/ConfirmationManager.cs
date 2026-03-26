using System.Collections.Concurrent;

using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.Sequence;
using log4net;

namespace ACE.Server.WorldObjects.Managers
{
    public class ConfirmationManager
    {
        private Player Player;

        private ConcurrentDictionary<ConfirmationType, Confirmation> confirmations = new ConcurrentDictionary<ConfirmationType, Confirmation>();

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly double confirmationTimeout = 30;

        private UIntSequence contextSequence = new UIntSequence();

        public ConfirmationManager(Player player)
        {
            Player = player;
        }

        /// <summary>
        /// Builds a new confirmation request on the server,
        /// and sends the request to the client
        /// </summary>
        /// <param name="timeoutSeconds">If set, seconds until the server forces dialog close via <see cref="EnqueueAbort"/>; otherwise <see cref="confirmationTimeout"/>.</param>
        public bool EnqueueSend(Confirmation confirmation, string text, double? timeoutSeconds = null)
        {
            confirmation.ContextId = contextSequence.NextValue;
            if (confirmations.TryAdd(confirmation.ConfirmationType, confirmation))
            {
                Player.Session.Network.EnqueueSend(new GameEventConfirmationRequest(Player.Session, confirmation.ConfirmationType, confirmation.ContextId, text));
                var timeoutConfirmation = new ActionChain();
                var delay = timeoutSeconds ?? confirmationTimeout;
                timeoutConfirmation.AddDelaySeconds(delay);
                timeoutConfirmation.AddAction(Player, ActionType.ConfirmationManager_EnqueueAbort, () => EnqueueAbort(confirmation.ConfirmationType, confirmation.ContextId));
                timeoutConfirmation.EnqueueChain();
            }
            else
            {
                //log.Error($"{Player.Name}.ConfirmationManager.EnqueueSend({confirmation.ConfirmationType}, {confirmation.ContextId}) - duplicate confirmation type");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Removes a pending confirmation and tells the client to close the dialog, without
        /// invoking <see cref="Confirmation.ProcessConfirmation"/> or sending the generic timeout chat.
        /// Used when server logic (e.g. UCM tick timeout) ends the flow before the scheduled <see cref="EnqueueAbort"/> runs.
        /// Always drops the server-side entry when the context matches so the scheduled abort becomes a no-op.
        /// If the session is gone, the packet is skipped (no NRE); the client may still show the dialog until dismissed locally.
        /// </summary>
        public bool TryDismissConfirmation(ConfirmationType confirmationType, uint contextId)
        {
            if (!confirmations.TryRemove(confirmationType, out var confirm) || confirm.ContextId != contextId)
                return false;

            if (Player.Session?.Network != null)
                Player.Session.Network.EnqueueSend(new GameEventConfirmationDone(Player.Session, confirmationType, contextId));

            return true;
        }

        /// <summary>
        /// This only needs to be sent in the rare event the server needs to force close
        /// a confirmation dialog that is still active on the client
        /// </summary>
        public void EnqueueAbort(ConfirmationType confirmationType, uint contextId)
        {
            if (confirmations.TryGetValue(confirmationType, out var confirm) && confirm.ContextId == contextId)
            {
                Player.Session.Network.EnqueueSend(new GameEventConfirmationDone(Player.Session, confirmationType, contextId));

                switch (confirm.ConfirmationType)
                {
                    case ConfirmationType.SwearAllegiance:
                        // This event automatically triggers a response from client, however due to the way ACE works, we want it to process as a timeout to match pcap output as best we can and inform players of results.
                        HandleResponse(confirm.ConfirmationType, confirm.ContextId, false, true);
                        break;
                    case ConfirmationType.AlterSkill:
                    case ConfirmationType.AlterAttribute:
                    case ConfirmationType.CraftInteraction:
                    case ConfirmationType.Augmentation:
                    case ConfirmationType.Yes_No:
                        Player.SendMessage("You waited too long to answer the question!");
                        // These events automatically trigger a response from client, others do not.
                        // do nothing further
                        break;

                    default:
                        HandleResponse(confirm.ConfirmationType, confirm.ContextId, false, true);
                        break;
                }
            }
        }

        /// <summary>
        /// The client has responded to a confirmation box
        /// </summary>
        public bool HandleResponse(ConfirmationType confirmType, uint contextId, bool response, bool timeout = false)
        {
            if (!confirmations.TryRemove(confirmType, out var confirm))
            {
                switch (confirmType)
                {
                    case ConfirmationType.SwearAllegiance:
                        // do nothing.
                        break;
                    case ConfirmationType.Fellowship:
                        // dialog box does not dismiss on ConfirmationDone, unlike on all other types, so we must let the player know when they click either yes or no, nothing occured because the offer has already expired.
                        Player.SendMessage("That offer of fellowship has expired."); // still looking for pcap accurate response
                        break;

                    case ConfirmationType.Yes_No:
                        // Stale client packet after server dismissed the dialog (e.g. UCM tick timeout).
                        return false;

                    default:
                        log.Error($"{Player.Name}.ConfirmationManager.HandleResponse({confirmType}, {contextId}, {response}, {timeout}) - confirmType not found");
                        break;
                }

                return false;
            }

            if (confirm.ContextId != contextId)
            {
                if (confirm.ConfirmationType == ConfirmationType.Fellowship)
                {
                    // dialog box does not dismiss on ConfirmationDone, unlike on all other types, so we must let the player know when they click either yes or no, nothing occured because the offer has already expired.
                    if (!confirmations.TryAdd(confirm.ConfirmationType, confirm))
                        log.Error($"{Player.Name}.ConfirmationManager.HandleResponse({confirm.ConfirmationType}, {confirm.ContextId}) - Unable to re-add confirmation, duplicate confirmation type");

                    Player.SendMessage("That offer of fellowship has expired."); // still looking for pcap accurate response

                    return false;
                }    

                log.Error($"{Player.Name}.ConfirmationManager.HandleResponse({confirmType}, {contextId}, {response}, {timeout}) - contextId != confirm.ContextId");

                if (!confirmations.TryAdd(confirm.ConfirmationType, confirm))
                    log.Error($"{Player.Name}.ConfirmationManager.HandleResponse({confirm.ConfirmationType}, {confirm.ContextId}) - Unable to re-add confirmation, duplicate confirmation type");

                return false;
            }

            confirm.ProcessConfirmation(response, timeout);

            return true;
        }
    }
}
