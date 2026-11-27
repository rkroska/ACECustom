using Newtonsoft.Json;

namespace ACE.Common
{
    public class PreloadedLandblocks
    {
        /// <summary>
        /// id of landblock to preload
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// description of landblock
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// whether or not this landblock is permaloaded
        /// </summary>
        [System.ComponentModel.DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool Permaload { get; set; }

        /// <summary>
        /// whether or not this landblock should also load adjacents, if Permaload is true, the ajacent landblocks will also be marked permaload
        /// </summary>
        [System.ComponentModel.DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool IncludeAdjacents { get; set; }

        /// <summary>
        /// the variation (layer) to preload; omitted or null = the base world. (2026-11-17: a big dungeon layer such as
        /// 00E5 v3 can be preloaded + permaloaded, so a login there never has to load the whole dungeon on the world thread.)
        /// </summary>
        public int? Variation { get; set; }

        /// <summary>
        /// whether or not this landblock is included for preload.
        /// </summary>
        [System.ComponentModel.DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool Enabled { get; set; }
    }
}
