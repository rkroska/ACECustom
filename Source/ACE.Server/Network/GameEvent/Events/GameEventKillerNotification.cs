using System;
using System.Linq;
using ACE.Entity;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Network.GameEvent.Events
{
    public class GameEventKillerNotification : GameEventMessage
    {
        public GameEventKillerNotification(Session session, string deathMessage, bool wasKilledBySplitArrow = false, string overkillSuffix = "")
            : base(GameEventType.KillerNotification, GameMessageGroup.UIQueue, session)
        {
            // 1. Apply split arrow text transformation (weapon attribution)
            var modifiedMessage = wasKilledBySplitArrow ? ModifyDeathMessageForSplitArrow(deathMessage) : deathMessage;
            // 2. Append overkill LAST so it's never buried inside split arrow suffix
            if (!string.IsNullOrEmpty(overkillSuffix))
                modifiedMessage = modifiedMessage.TrimEnd(' ') + overkillSuffix;
            Writer.WriteString16L(modifiedMessage);
        }
        
        
        private string ModifyDeathMessageForSplitArrow(string originalMessage)
        {
            if (string.IsNullOrWhiteSpace(originalMessage)) return originalMessage;
            
            // Handle all possible death message patterns by injecting "split arrow" terminology
            var modifiedMessage = originalMessage
                // Replace "your attack" patterns
                .Replace("your attack", "your split arrow")
                .Replace("your assault", "your split arrow assault")
                
                // Replace "You [verb]" patterns
                .Replace("You obliterate", "Your split arrow obliterates")
                .Replace("You smite", "Your split arrow smites")
                .Replace("You slay", "Your split arrow slays")
                .Replace("You kill", "Your split arrow kills")
                .Replace("You destroy", "Your split arrow destroys")
                .Replace("You bring", "Your split arrow brings")
                .Replace("You reduce", "Your split arrow reduces")
                .Replace("You run", "Your split arrow runs")
                .Replace("You beat", "Your split arrow beats")
                .Replace("You split", "Your split arrow splits")
                .Replace("You cleave", "Your split arrow cleaves")
                .Replace("You flatten", "Your split arrow flattens")
                .Replace("You knock", "Your split arrow knocks")
                .Replace("You stop", "Your split arrow stops")
                .Replace("You send", "Your split arrow sends")
                .Replace("You suffer", "Your split arrow suffers")
                .Replace("You liquify", "Your split arrow liquifies")
                .Replace("You blast", "Your split arrow blasts")
                .Replace("You dessicate", "Your split arrow dessicates")
                .Replace("You tear", "Your split arrow tears")
                .Replace("You crush", "Your split arrow crushes")
                .Replace("You smash", "Your split arrow smashes")
                .Replace("You bash", "Your split arrow bashes")
                .Replace("You gore", "Your split arrow gores")
                .Replace("You impale", "Your split arrow impales")
                .Replace("You stab", "Your split arrow stabs")
                .Replace("You nick", "Your split arrow nicks")
                
                // Handle passive death messages by adding split arrow context
                .Replace("is utterly destroyed", "is utterly destroyed by your split arrow")
                .Replace("is incinerated", "is incinerated by your split arrow")
                .Replace("is reduced to cinders", "is reduced to cinders by your split arrow")
                .Replace("catches your attack", "catches your split arrow attack")
                .Replace("seared corpse smolders", "seared corpse smolders from your split arrow")
                .Replace("brings to a fiery end", "brings to a fiery end with your split arrow")
                .Replace("ancestors feel it", "ancestors feel your split arrow's impact")
                .Replace("last strength dissolves before you", "last strength dissolves before your split arrow");
            
            // Only apply generic fallback if no specific pattern was matched
            if (modifiedMessage == originalMessage)
            {
                var trimmed = originalMessage.TrimEnd('!', '.', ' ');
                modifiedMessage = $"{trimmed} by your split arrow!";
            }
            
            return modifiedMessage;
        }
    }
}
