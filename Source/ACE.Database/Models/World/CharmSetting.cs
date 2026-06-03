using System;

namespace ACE.Database.Models.World
{
    /// <summary>
    /// EF Core model for the <c>charm_settings</c> world DB table.
    /// Rows are keyed by (charm_name, setting_key) and store string values.
    /// </summary>
    public class CharmSetting
    {
        public string   CharmName    { get; set; }
        public string   SettingKey   { get; set; }
        public string   SettingValue { get; set; }
        public DateTime LastModified { get; set; }
    }
}
