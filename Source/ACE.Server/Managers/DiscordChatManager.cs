using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using ACE.Common;
using Discord;

namespace ACE.Server.Managers
{
    public class DiscordChatManager
    {

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static DiscordSocketClient _discordSocketClient;

        public static void SendDiscordMessage(string player, string message, long channelId)
        {
            if (ConfigManager.Config.Chat.EnableDiscordConnection)
            {
                try
                {
                    _discordSocketClient.GetGuild((ulong)ConfigManager.Config.Chat.ServerId).GetTextChannel((ulong)channelId).SendMessageAsync(player + " : " + message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error sending discord message, " + ex.Message);
                }
            }
            
        }

        public static void SendDiscordFile(string player, string message, long channelId, FileAttachment fileContent)
        {
            if (ConfigManager.Config.Chat.EnableDiscordConnection)
            {
                try
                {
                    var res = _discordSocketClient.GetGuild((ulong)ConfigManager.Config.Chat.ServerId).GetTextChannel((ulong)channelId).SendFileAsync(fileContent, player + " : " + message).Result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error sending discord message, " + ex.Message);
                }
            }

        }

        public static void Initialize()
        {
            _discordSocketClient = new DiscordSocketClient();
            _discordSocketClient.LoginAsync(Discord.TokenType.Bot, ConfigManager.Config.Chat.DiscordToken);
            _discordSocketClient.StartAsync();
            
        }
    }
}
