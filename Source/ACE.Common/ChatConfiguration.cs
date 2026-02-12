using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Common
{
    public class ChatConfiguration
    {
        [System.ComponentModel.DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool EnableDiscordConnection { get; set; }
        public string DiscordToken { get; set; }
        public long GeneralChannelId { get; set; }
        public long TradeChannelId { get; set; }
        public long ServerId { get; set; }
        public long AdminAuditId { get; set; }
        public long EventsChannelId { get; set; }
        public long ExportsChannelId { get; set; }
        public long WeenieUploadsChannelId { get; set; }
        public long RaffleChannelId { get; set; }
        public long AdminChannelId { get; set; }
        public string WebhookURL { get; set; }
        public long ClothingModUploadChannelId { get; set; }
        public long ClothingModExportChannelId { get; set; }
        
        [System.ComponentModel.DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public long PerformanceAlertsChannelId { get; set; }

        public enum DiscordLogLevel
        {
            None = 0,
            Info = 1,     // Standard/Important messages only
            Verbose = 2   // Everything including spammy logs
        }

        [System.ComponentModel.DefaultValue(DiscordLogLevel.Info)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public DiscordLogLevel DiscordPerformanceLevel { get; set; }

        [System.ComponentModel.DefaultValue(DiscordLogLevel.Info)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public DiscordLogLevel DiscordBroadcastLevel { get; set; }

        [System.ComponentModel.DefaultValue(DiscordLogLevel.Info)] // Default to Info (Bans/Gags only)
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public DiscordLogLevel DiscordAuditLevel { get; set; }

        [System.ComponentModel.DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool EnableDiscordChatMirroring { get; set; }

        [System.ComponentModel.DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public long LFGChannelId { get; set; }

        [System.ComponentModel.DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public long RoleplayChannelId { get; set; }

        [System.ComponentModel.DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public long SocietyCelheardtChannelId { get; set; }

        [System.ComponentModel.DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public long SocietyEldrytchWebChannelId { get; set; }

        [System.ComponentModel.DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public long SocietyRadiantBloodChannelId { get; set; }
    }
}
