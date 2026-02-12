using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using ACE.Common;
using Discord;
using static System.Net.Mime.MediaTypeNames;
using System.Net.Http;

namespace ACE.Server.Managers
{
    public class DiscordChatManager
    {

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static DiscordSocketClient _discordSocketClient;
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async void SendDiscordMessage(string player, string message, long channelId)
        {
            if (ConfigManager.Config.Chat.EnableDiscordConnection)
            {
                try
                {
                    var guild = _discordSocketClient.GetGuild((ulong)ConfigManager.Config.Chat.ServerId);
                    if (guild != null)
                    {
                        var channel = guild.GetTextChannel((ulong)channelId);
                        if (channel != null)
                        {
                            await channel.SendMessageAsync(player + " : " + message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Error sending discord message, " + ex.Message);
                }
            }
            
        }

        public static async Task SendDiscordFileAsync(string player, string message, long channelId, FileAttachment fileContent)
        {

            try
            {
                var guild = _discordSocketClient.GetGuild((ulong)ConfigManager.Config.Chat.ServerId);
                if (guild != null)
                {
                    var channel = guild.GetTextChannel((ulong)channelId);
                    if (channel != null)
                    {
                        await channel.SendFileAsync(fileContent, player + " : " + message);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Error sending discord message, " + ex.Message);
            }
            

        }

        public static async Task<string> GetSqlFromDiscordMessageAsync(int topN, string identifier)
        {
            string res = "";

            try
            {
                var guild = _discordSocketClient.GetGuild((ulong)ConfigManager.Config.Chat.ServerId);
                if (guild == null) return res;

                var channel = guild.GetTextChannel((ulong)ConfigManager.Config.Chat.WeenieUploadsChannelId);
                if (channel == null) return res;

                var messages = await channel.GetMessagesAsync(limit: topN).FlattenAsync();

                foreach (var x in messages)
                {
                    if(x.Content == identifier)
                    {
                        if(x.Attachments.Count == 1)
                        {
                            IAttachment attachment = x.Attachments.First();
                            if (attachment.Filename.Contains(".sql", StringComparison.InvariantCultureIgnoreCase))
                            {
                                res = await _httpClient.GetStringAsync(attachment.Url);
                                return res;
                            }
                        }
                    }    
                }
            }
            catch (Exception ex)
            {

                log.Error("Error getting discord messages, " + ex.Message);
            }
            

            return res;
        }

        public static async Task<string> GetJsonFromDiscordMessageAsync(int topN, string identifier)
        {
            string res = "";

            try
            {
                var guild = _discordSocketClient.GetGuild((ulong)ConfigManager.Config.Chat.ServerId);
                if (guild == null) return res;

                var channel = guild.GetTextChannel((ulong)ConfigManager.Config.Chat.ClothingModUploadChannelId);
                if (channel == null) return res;

                var messages = await channel.GetMessagesAsync(limit: topN).FlattenAsync();

                foreach (var x in messages)
                {
                    if (x.Content == identifier)
                    {
                        if (x.Attachments.Count == 1)
                        {
                            IAttachment attachment = x.Attachments.First();
                            if (attachment.Filename.Contains(".json", StringComparison.InvariantCultureIgnoreCase))
                            {
                                res = await _httpClient.GetStringAsync(attachment.Url);
                                return res;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Error getting discord messages, " + ex.Message);
            }

            return res;
        }



        public static void Initialize()
        {
            _discordSocketClient = new DiscordSocketClient();
            _discordSocketClient.LoginAsync(Discord.TokenType.Bot, ConfigManager.Config.Chat.DiscordToken);
            _discordSocketClient.StartAsync();
            
        }
    }
}
