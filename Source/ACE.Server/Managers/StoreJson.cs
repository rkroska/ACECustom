using System.Globalization;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Newtonsoft JSON for shard-config stores, immune to <see cref="JsonConvert.DefaultSettings"/>.
    /// A mod may set process-wide defaults (CustomClothingBase sets a camelCase contract resolver);
    /// JsonConvert.SerializeObject/DeserializeObject apply those even when a settings object is
    /// passed, and camelCase also rewrites DICTIONARY KEYS ("S" -> "s", "A+" -> "a+"), so a store
    /// with case-sensitive string keys loses entries on its next save. <see cref="JsonSerializer.Create(JsonSerializerSettings)"/>
    /// ignores the defaults, so a store written here always reads back the way it was built.
    /// </summary>
    public static class StoreJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver(),
            Formatting = Formatting.None,
            CheckAdditionalContent = true, // same as JsonConvert.DeserializeObject: trailing junk is an error
        };

        public static string Serialize(object value)
        {
            var serializer = JsonSerializer.Create(Settings);
            using var sw = new StringWriter(CultureInfo.InvariantCulture);
            using (var writer = new JsonTextWriter(sw) { Formatting = serializer.Formatting })
                serializer.Serialize(writer, value);
            return sw.ToString();
        }

        public static T Deserialize<T>(string json)
        {
            if (json == null)
                return default;
            var serializer = JsonSerializer.Create(Settings);
            using var reader = new JsonTextReader(new StringReader(json));
            return serializer.Deserialize<T>(reader);
        }
    }
}
