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

        public static bool IsDiscordConnectionEnabled =>
            ConfigManager.Config?.Chat?.EnableDiscordConnection ?? false;

        public static bool IsDiscordClientReady =>
            _discordSocketClient?.ConnectionState == ConnectionState.Connected;

        /// <summary>
        /// Sends a message to a channel without a player prefix. Returns the message snowflake id when successful.
        /// </summary>
        public static async Task<ulong?> SendDiscordChannelMessageAsync(string message, long channelId)
        {
            if (!IsDiscordConnectionEnabled || channelId <= 0)
                return null;

            try
            {
                var channel = ResolveTextChannel(channelId);
                if (channel == null)
                    return null;

                var sent = await channel.SendMessageAsync(message);
                return sent?.Id;
            }
            catch (Exception ex)
            {
                log.Error($"Error sending discord channel message: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sends a patch-notes embed to a channel. Returns the message snowflake id when successful.
        /// </summary>
        public static async Task<ulong?> SendDiscordChannelEmbedAsync(string title, string description, string url, long channelId)
        {
            if (!IsDiscordConnectionEnabled || channelId <= 0)
                return null;

            try
            {
                var channel = ResolveTextChannel(channelId);
                if (channel == null)
                    return null;

                var embed = new EmbedBuilder()
                    .WithTitle(Truncate(title, 256))
                    .WithDescription(string.IsNullOrWhiteSpace(description) ? null : Truncate(description.Trim(), 4096))
                    .WithUrl(url)
                    .WithColor(new Color(46, 125, 50))
                    .WithFooter("Patch notes")
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                var sent = await channel.SendMessageAsync(embed: embed);
                return sent?.Id;
            }
            catch (Exception ex)
            {
                log.Error($"Error sending discord channel embed: {ex.Message}");
                return null;
            }
        }

        private static SocketTextChannel ResolveTextChannel(long channelId)
        {
            if (_discordSocketClient == null)
            {
                log.Warn("[Discord] Client is not initialized.");
                return null;
            }

            if (!IsDiscordClientReady)
            {
                log.Warn($"[Discord] Client is not connected (state: {_discordSocketClient.ConnectionState}).");
                return null;
            }

            if (ConfigManager.Config?.Chat == null)
            {
                log.Warn("[Discord] Chat configuration is not available.");
                return null;
            }

            var guild = _discordSocketClient.GetGuild((ulong)ConfigManager.Config.Chat.ServerId);
            if (guild == null)
            {
                log.Warn($"[Discord] Failed to find guild {ConfigManager.Config.Chat.ServerId}");
                return null;
            }

            var channel = guild.GetTextChannel((ulong)channelId);
            if (channel == null)
                log.Warn($"[Discord] Failed to find channel {channelId} in guild {guild.Name}");

            return channel;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;
            return value[..(maxLength - 1)] + "…";
        }

        public static async Task SendDiscordMessage(string player, string message, long channelId)
        {
            if (IsDiscordConnectionEnabled)
            {
                try
                {
                    if (ConfigManager.Config?.Chat == null) return;
                    var guild = _discordSocketClient.GetGuild((ulong)ConfigManager.Config.Chat.ServerId);
                    if (guild != null)
                    {
                        var channel = guild.GetTextChannel((ulong)channelId);
                        if (channel != null)
                        {
                            await channel.SendMessageAsync(player + " : " + message);
                        }
                        else
                        {
                            log.Warn($"[Discord] Failed to find channel {channelId} in guild {guild.Name}");
                        }
                    }
                    else
                    {
                        log.Warn($"[Discord] Failed to find guild {ConfigManager.Config.Chat.ServerId}");
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
                if (ConfigManager.Config?.Chat == null) return;
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
                if (ConfigManager.Config?.Chat == null) return res;
                var guild = _discordSocketClient.GetGuild((ulong)ConfigManager.Config.Chat.ServerId);
                if (guild == null) return res;

                var channel = guild.GetTextChannel((ulong)ConfigManager.Config.Chat.WeenieUploadsChannelId);
                if (channel == null) return res;

                var messages = await channel.GetMessagesAsync(limit: topN).FlattenAsync();

                foreach (var x in messages)
                {
                    if (!MessageMatchesSqlIdentifier(x, identifier))
                        continue;

                    if (x.Attachments.Count != 1)
                        continue;

                    var attachment = x.Attachments.First();
                    if (!attachment.Filename.EndsWith(".sql", StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    res = await DownloadAttachmentTextAsync(attachment);
                    if (!string.IsNullOrEmpty(res))
                        return res;
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
                if (ConfigManager.Config?.Chat == null) return res;
                var guild = _discordSocketClient.GetGuild((ulong)ConfigManager.Config.Chat.ServerId);
                if (guild == null) return res;

                var channel = guild.GetTextChannel((ulong)ConfigManager.Config.Chat.ClothingModUploadChannelId);
                if (channel == null) return res;

                var messages = await channel.GetMessagesAsync(limit: topN).FlattenAsync();

                foreach (var x in messages)
                {
                    if (!MessageMatchesAttachmentIdentifier(x, identifier, ".json"))
                        continue;

                    if (x.Attachments.Count != 1)
                        continue;

                    var attachment = x.Attachments.First();
                    if (!attachment.Filename.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    res = await DownloadAttachmentTextAsync(attachment);
                    if (!string.IsNullOrEmpty(res))
                        return res;
                }
            }
            catch (Exception ex)
            {
                log.Error("Error getting discord messages, " + ex.Message);
            }

            return res;
        }



        /// <summary>
        /// Matches Discord import identifier against message text or attachment name.
        /// export-discord posts content like "Player : 19853087.sql".
        /// </summary>
        private static bool MessageMatchesSqlIdentifier(IMessage x, string identifier) =>
            MessageMatchesAttachmentIdentifier(x, identifier, ".sql");

        private static bool MessageMatchesAttachmentIdentifier(IMessage x, string identifier, string extension)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return false;

            if (string.Equals(x.Content?.Trim(), identifier, StringComparison.OrdinalIgnoreCase))
                return true;

            var expectedFile = $"{identifier}{extension}";
            if (x.Attachments.Count == 1 &&
                string.Equals(x.Attachments.First().Filename, expectedFile, StringComparison.OrdinalIgnoreCase))
                return true;

            var content = x.Content?.Trim();
            if (!string.IsNullOrEmpty(content) &&
                content.EndsWith(expectedFile, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static async Task<string> DownloadAttachmentTextAsync(IAttachment attachment)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, attachment.Url);
                var token = ConfigManager.Config?.Chat?.DiscordToken;
                if (!string.IsNullOrWhiteSpace(token))
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", token);

                using var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                log.Error($"Error downloading discord attachment {attachment.Filename}: {ex.Message}");
                return string.Empty;
            }
        }

        public static void Initialize()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            _discordSocketClient = new DiscordSocketClient();
            
            // CodeRabbit: Handle fire-and-forget async failures
            Task.Run(async () => 
            {
                try
                {
                    var token = ConfigManager.Config?.Chat?.DiscordToken;
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        log.Warn("[Discord] Discord token is empty. Discord client will not start.");
                        return;
                    }
                    await _discordSocketClient.LoginAsync(Discord.TokenType.Bot, token);
                    await _discordSocketClient.StartAsync();
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to initialize Discord client: {ex.Message}");
                }
            });
        }
    }
}
