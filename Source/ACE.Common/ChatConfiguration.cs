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

    }
}
