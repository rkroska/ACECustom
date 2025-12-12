using ACE.Database;
using ACE.Database.Models.Shard;
using Google.Protobuf.WellKnownTypes;
using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Timers;

#nullable enable
namespace ACE.Server.Managers
{
    // Immutable class that holds a config setting for the server.
    public class ConfigProperty<T> where T : notnull
    {
        private readonly T _value;
        public readonly bool HasValue;
        public readonly T Default;
        public readonly string Description;

        public ConfigProperty(T value, T defaultValue, string description)
        {
            _value = value;
            HasValue = true;
            Default = defaultValue;
            Description = description;
        }

        public ConfigProperty(T defaultValue, string description)
        {
            _value = default;
            HasValue = false;
            Default = defaultValue;
            Description = description;
        }

        // Returns a new ConfigProperty with the desired value.
        public ConfigProperty<T> WithValue(T newValue)
        {
            return new ConfigProperty<T>(newValue, Default, Description);
        }
        public T Value => HasValue ? _value : Default;
    }

    // ==================================================================================
    // To change these values for the server,
    // please use the /modifybool, /modifylong, /modifydouble, and /modifystring commands
    // ==================================================================================
    public static class ServerConfig
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        /// <summary>
        // Retrieve the property info by key.
        /// </summary>
        private static PropertyInfo? GetPropertyInfo<T>(string key) where T : notnull
        {
            var propInfo = typeof(ServerConfig).GetProperty(key, BindingFlags.Public | BindingFlags.Static);
            if (propInfo == null) return null;
            if (propInfo.PropertyType != typeof(ConfigProperty<T>)) return null;
            return propInfo;
        }

        /// <summary>
        /// Gets a ConfigProperty<T> for a configuration property using its string key.
        /// </summary>
        public static ConfigProperty<T>? GetConfigProperty<T>(string key) where T : notnull
        {
            var propertyInfo = GetPropertyInfo<T>(key);
            if (propertyInfo == null) return null;
            return (ConfigProperty<T>)propertyInfo.GetValue(null)!;
        }

        /// <summary>
        /// Sets a new value for a configuration property using its string key.
        /// This creates a new immutable ConfigProperty instance and atomically replaces the old one.
        /// Returns a bool representing whether the set succeeded or not.
        /// </summary>
        public static bool SetValue<T>(string key, T newValue, bool markModified = true) where T : notnull
        {
            var propInfo = GetPropertyInfo<T>(key);
            if (propInfo == null)
            {
                return false;
            }
            var oldVal = (ConfigProperty<T>)propInfo.GetValue(null)!;
            ConfigProperty<T> newVal = oldVal.WithValue(newValue);
            propInfo.SetValue(null, newVal);

            if (markModified) { 
                if (typeof(T) == typeof(bool))
                {
                    _modifiedBoolProps.TryAdd(key, (ConfigProperty<bool>)(object)newVal);
                }
                else if (typeof(T) == typeof(long))
                {
                    _modifiedLongProps.TryAdd(key, (ConfigProperty<long>)(object)newVal);
                }
                else if (typeof(T) == typeof(double))
                {
                    _modifiedDoubleProps.TryAdd(key, (ConfigProperty<double>)(object)newVal);
                }
                else if (typeof(T) == typeof(string))
                {
                    _modifiedStringProps.TryAdd(key, (ConfigProperty<string>)(object)newVal);
                }
            }
            return true;
        }


        /// <summary>
        /// Loads all config values from the database.
        /// </summary>
        public static void LoadFromDb()
        {
            foreach (ConfigPropertiesBoolean c in DatabaseManager.ShardConfig.GetAllBools())
                SetValue(c.Key, c.Value, markModified: false);

            foreach (ConfigPropertiesLong c in DatabaseManager.ShardConfig.GetAllLongs())
                SetValue(c.Key, c.Value, markModified: false);

            foreach (ConfigPropertiesDouble c in DatabaseManager.ShardConfig.GetAllDoubles())
                SetValue(c.Key, c.Value, markModified: false);

            foreach (ConfigPropertiesString c in DatabaseManager.ShardConfig.GetAllStrings())
                SetValue(c.Key, c.Value, markModified: false);
        }

        /// <summary>
        /// Writes the updated config values to the database.
        /// </summary>
        public static void WriteUpdatesToDb()
        {
            foreach (var key in _modifiedBoolProps.Keys)
            {
                if (_modifiedBoolProps.TryRemove(key, out ConfigProperty<bool>? property))
                {
                    if (DatabaseManager.ShardConfig.BoolExists(key))
                        DatabaseManager.ShardConfig.SaveBool(new ConfigPropertiesBoolean { Key = key, Value = property.Value, Description = property.Description });
                    else
                        DatabaseManager.ShardConfig.AddBool(key, property.Value, property.Description);
                }
            }
            foreach (var key in _modifiedLongProps.Keys)
            {
                if (_modifiedLongProps.TryRemove(key, out ConfigProperty<long>? property))
                {
                    if (DatabaseManager.ShardConfig.LongExists(key))
                        DatabaseManager.ShardConfig.SaveLong(new ConfigPropertiesLong { Key = key, Value = property.Value, Description = property.Description });
                    else
                        DatabaseManager.ShardConfig.AddLong(key, property.Value, property.Description);
                }
            }
            foreach (var key in _modifiedDoubleProps.Keys)
            {
                if (_modifiedDoubleProps.TryRemove(key, out ConfigProperty<double>? property))
                {
                    if (DatabaseManager.ShardConfig.DoubleExists(key))
                        DatabaseManager.ShardConfig.SaveDouble(new ConfigPropertiesDouble { Key = key, Value = property.Value, Description = property.Description });
                    else
                        DatabaseManager.ShardConfig.AddDouble(key, property.Value, property.Description);
                }
            }
            foreach (var key in _modifiedStringProps.Keys)
            {
                if (_modifiedStringProps.TryRemove(key, out ConfigProperty<string>? property))
                {
                    if (DatabaseManager.ShardConfig.StringExists(key))
                        DatabaseManager.ShardConfig.SaveString(new ConfigPropertiesString { Key = key, Value = property.Value, Description = property.Description });
                    else
                        DatabaseManager.ShardConfig.AddString(key, property.Value, property.Description);
                }
            }
        }

        /// <summary>
        /// Prints a human-readable config string.
        /// </summary>
        public static string DebugString()
        {
            var properties = typeof(ServerConfig).GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            var propsByType = new Dictionary<System.Type, List<string>>();
            var sb = new StringBuilder();
            sb.AppendLine("Configuration Properties:");
            sb.AppendLine("-------------------------");

            foreach (var propInfo in properties)
            {
                // Check if the property type is a ConfigProperty<T>
                if (propInfo.PropertyType.IsGenericType && propInfo.PropertyType.GetGenericTypeDefinition() == typeof(ConfigProperty<>))
                {
                    // Get the generic type argument (T), e.g., bool, long, double, string
                    var propertyType = propInfo.PropertyType.GetGenericArguments()[0];
                    if (!propsByType.ContainsKey(propertyType)) propsByType[propertyType] = [];

                    dynamic prop = propInfo.GetValue(null)!;
                    propsByType[propertyType].Add(
                        string.Format("- {0}: {1} (Current: {2}, Default: {3})", prop.Name, prop.Description, prop.HasValue ? prop.Value.ToString() : "not set", prop.Default.ToString())
                    );
                }
            }

            // --- Output Formatting ---
            foreach (var kvp in propsByType.OrderBy(x => x.Key.Name))
            {
                sb.AppendLine($"\n{kvp.Key.Name} properties:");
                foreach (var detail in kvp.Value) sb.AppendLine(detail);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Modified properties that need to be written to the database.
        /// </summary>
        private static readonly ConcurrentDictionary<string, ConfigProperty<bool>> _modifiedBoolProps = [];
        private static readonly ConcurrentDictionary<string, ConfigProperty<long>> _modifiedLongProps = [];
        private static readonly ConcurrentDictionary<string, ConfigProperty<double>> _modifiedDoubleProps = [];
        private static readonly ConcurrentDictionary<string, ConfigProperty<string>> _modifiedStringProps = [];

        /// <summary>
        /// Public accessors for the current config values.
        /// These properties' names will match the key used for storing them to the database using reflection.
        /// </summary>
        public static ConfigProperty<bool> account_login_boots_in_use { get; private set; } = new(true, "if FALSE, oldest connection to account is not booted when new connection occurs");
        public static ConfigProperty<bool> advocate_fane_auto_bestow { get; private set; } = new(false, "If enabled, Advocate Fane will automatically bestow new advocates to advocate_fane_auto_bestow_level");
        public static ConfigProperty<bool> aetheria_heal_color { get; private set; } = new(false, "If enabled, changes the aetheria healing over time messages from the default retail red color to green");
        public static ConfigProperty<bool> allow_combat_mode_crafting { get; private set; } = new(false, "If enabled, allows players to do crafting (recipes) from all stances. Forces players to NonCombat first, then continues to recipe action.");
        public static ConfigProperty<bool> allow_door_hold { get; private set; } = new(true, "enables retail behavior where standing on a door while it is closing keeps the door as ethereal until it is free from collisions, effectively holding the door open for other players");
        public static ConfigProperty<bool> allow_fast_chug { get; private set; } = new(true, "enables retail behavior where a player can consume food and drink faster than normal by breaking animation");
        public static ConfigProperty<bool> allow_jump_loot { get; private set; } = new(true, "enables retail behavior where a player can quickly loot items while jumping, bypassing the 'crouch down' animation");
        public static ConfigProperty<bool> allow_negative_dispel_resist { get; private set; } = new(true, "enables retail behavior where #-# negative dispels can be resisted");
        public static ConfigProperty<bool> allow_negative_rating_curve { get; private set; } = new(true, "enables retail behavior where negative DRR from void dots didn't switch to the reverse rating formula, resulting in a possibly unintended curve that quickly ramps up as -rating goes down, eventually approaching infinity / divide by 0 for -100 rating. less than -100 rating would produce negative numbers.");
        public static ConfigProperty<bool> allow_pkl_bump { get; private set; } = new(true, "enables retail behavior where /pkl checks for entry collisions, bumping the player position over if standing on another PKLite. This effectively enables /pkl door skipping from retail");
        public static ConfigProperty<bool> allow_summoning_killtask_multicredit { get; private set; } = new(true, "enables retail behavior where a summoner can get multiple killtask credits from a monster");
        public static ConfigProperty<bool> assess_creature_mod { get; private set; } = new(false, "(non-retail function) If enabled, re-enables former skill formula, when assess creature skill is not trained or spec'ed");
        public static ConfigProperty<bool> attribute_augmentation_safety_cap { get; private set; } = new(true, "if TRUE players are not able to use attribute augmentations if the innate value of the target attribute is >= 96. All normal restrictions to these augmentations still apply.");
        public static ConfigProperty<bool> chat_disable_general { get; private set; } = new(false, "disable general global chat channel");
        public static ConfigProperty<bool> chat_disable_lfg { get; private set; } = new(false, "disable lfg global chat channel");
        public static ConfigProperty<bool> chat_disable_olthoi { get; private set; } = new(false, "disable olthoi global chat channel");
        public static ConfigProperty<bool> chat_disable_roleplay { get; private set; } = new(false, "disable roleplay global chat channel");
        public static ConfigProperty<bool> chat_disable_trade { get; private set; } = new(false, "disable trade global chat channel");
        public static ConfigProperty<bool> chat_echo_only { get; private set; } = new(false, "global chat returns to sender only");
        public static ConfigProperty<bool> chat_echo_reject { get; private set; } = new(false, "global chat returns to sender on reject");
        public static ConfigProperty<bool> chat_inform_reject { get; private set; } = new(true, "global chat informs sender on reason for reject");
        public static ConfigProperty<bool> chat_log_abuse { get; private set; } = new(false, "log abuse chat");
        public static ConfigProperty<bool> chat_log_admin { get; private set; } = new(false, "log admin chat");
        public static ConfigProperty<bool> chat_log_advocate { get; private set; } = new(false, "log advocate chat");
        public static ConfigProperty<bool> chat_log_allegiance { get; private set; } = new(false, "log allegiance chat");
        public static ConfigProperty<bool> chat_log_audit { get; private set; } = new(true, "log audit chat");
        public static ConfigProperty<bool> chat_log_debug { get; private set; } = new(false, "log debug chat");
        public static ConfigProperty<bool> chat_log_fellow { get; private set; } = new(false, "log fellow chat");
        public static ConfigProperty<bool> chat_log_general { get; private set; } = new(false, "log general chat");
        public static ConfigProperty<bool> chat_log_global { get; private set; } = new(false, "log global broadcasts");
        public static ConfigProperty<bool> chat_log_help { get; private set; } = new(false, "log help chat");
        public static ConfigProperty<bool> chat_log_lfg { get; private set; } = new(false, "log LFG chat");
        public static ConfigProperty<bool> chat_log_olthoi { get; private set; } = new(false, "log olthoi chat");
        public static ConfigProperty<bool> chat_log_qa { get; private set; } = new(false, "log QA chat");
        public static ConfigProperty<bool> chat_log_roleplay { get; private set; } = new(false, "log roleplay chat");
        public static ConfigProperty<bool> chat_log_sentinel { get; private set; } = new(false, "log sentinel chat");
        public static ConfigProperty<bool> chat_log_society { get; private set; } = new(false, "log society chat");
        public static ConfigProperty<bool> chat_log_trade { get; private set; } = new(false, "log trade chat");
        public static ConfigProperty<bool> chat_log_townchans { get; private set; } = new(false, "log advocate town chat");
        public static ConfigProperty<bool> chat_requires_account_15days { get; private set; } = new(false, "global chat privileges requires accounts to be 15 days or older");
        public static ConfigProperty<bool> chess_enabled { get; private set; } = new(true, "if FALSE then chess will be disabled");
        public static ConfigProperty<bool> use_cloak_proc_custom_scale { get; private set; } = new(false, "If TRUE, the calculation for cloak procs will be based upon the values set by the server oeprator.");
        public static ConfigProperty<bool> client_movement_formula { get; private set; } = new(false, "If enabled, server uses DoMotion/StopMotion self-client movement methods instead of apply_raw_movement");
        public static ConfigProperty<bool> container_opener_name { get; private set; } = new(false, "If enabled, when a player tries to open a container that is already in use by someone else, replaces 'someone else' in the message with the actual name of the player");
        public static ConfigProperty<bool> corpse_decay_tick_logging { get; private set; } = new(false, "If ENABLED then player corpse ticks will be logged");
        public static ConfigProperty<bool> corpse_destroy_pyreals { get; private set; } = new(true, "If FALSE then pyreals will not be completely destroyed on player death");
        public static ConfigProperty<bool> craft_exact_msg { get; private set; } = new(false, "If TRUE, and player has crafting chance of success dialog enabled, shows them an additional message in their chat window with exact %");
        public static ConfigProperty<bool> creature_name_check { get; private set; } = new(true, "if enabled, creature names in world database restricts player names during character creation");
        public static ConfigProperty<bool> creatures_drop_createlist_wield { get; private set; } = new(false, "If FALSE then Wielded items in CreateList will not drop. Retail defaulted to TRUE but there are currently data errors");
        public static ConfigProperty<bool> equipmentsetid_enabled { get; private set; } = new(true, "enable this to allow adding EquipmentSetIDs to loot armor");
        public static ConfigProperty<bool> equipmentsetid_name_decoration { get; private set; } = new(false, "enable this to add the EquipmentSet name to loot armor name");
        public static ConfigProperty<bool> fastbuff { get; private set; } = new(true, "If TRUE, enables the fast buffing trick from retail.");
        public static ConfigProperty<bool> fellow_busy_no_recruit { get; private set; } = new(true, "if FALSE, fellows can be recruited while they are busy, different from retail");
        public static ConfigProperty<bool> fellow_kt_killer { get; private set; } = new(true, "if FALSE, fellowship kill tasks will share with the fellowship, even if the killer doesn't have the quest");
        public static ConfigProperty<bool> fellow_kt_landblock { get; private set; } = new(false, "if TRUE, fellowship kill tasks will share with landblock range (192 distance radius, or entire dungeon)");
        public static ConfigProperty<bool> fellow_quest_bonus { get; private set; } = new(false, "if TRUE, applies EvenShare formula to fellowship quest reward XP (300% max bonus, defaults to false in retail)");
        public static ConfigProperty<bool> fellowship_additive { get; private set; } = new ConfigProperty<bool>(false, "changes the calculation of fellowship sharing");
        public static ConfigProperty<bool> fellowship_xp_debug_logging { get; private set; } = new(false, "if enabled, logs detailed fellowship XP distance check statistics every 10 seconds for performance monitoring");
        public static ConfigProperty<bool> fix_chest_missing_inventory_window { get; private set; } = new(false, "Very non-standard fix. This fixes an acclient bug where unlocking a chest, and then quickly opening it before the client has received the Locked=false update from server can result in the chest opening, but with the chest inventory window not displaying. Bug has a higher chance of appearing with more network latency.");
        public static ConfigProperty<bool> gateway_ties_summonable { get; private set; } = new(true, "if disabled, players cannot summon ties from gateways. defaults to enabled, as in retail");
        public static ConfigProperty<bool> house_15day_account { get; private set; } = new(true, "if disabled, houses can be purchased with accounts created less than 15 days old");
        public static ConfigProperty<bool> house_30day_cooldown { get; private set; } = new(true, "if disabled, houses can be purchased without waiting 30 days between each purchase");
        public static ConfigProperty<bool> house_hook_limit { get; private set; } = new(true, "if disabled, house hook limits are ignored");
        public static ConfigProperty<bool> house_hookgroup_limit { get; private set; } = new(true, "if disabled, house hook group limits are ignored");
        public static ConfigProperty<bool> house_per_char { get; private set; } = new(false, "if TRUE, allows 1 house per char instead of 1 house per account");
        public static ConfigProperty<bool> house_purchase_requirements { get; private set; } = new(true, "if disabled, requirements to purchase/rent house are not checked");
        public static ConfigProperty<bool> house_rent_enabled { get; private set; } = new(true, "If FALSE then rent is not required");
        public static ConfigProperty<bool> iou_trades { get; private set; } = new(false, "(non-retail function) If enabled, IOUs can be traded for objects that are missing in DB but added/restored later on");
        public static ConfigProperty<bool> item_dispel { get; private set; } = new(false, "if enabled, allows players to dispel items. defaults to end of retail, where item dispels could only target creatures");
        public static ConfigProperty<bool> legacy_loot_system { get; private set; } = new(false, "use the previous iteration of the ace lootgen system");
        public static ConfigProperty<bool> lifestone_broadcast_death { get; private set; } = new(true, "if true, player deaths are additionally broadcast to other players standing near the destination lifestone");
        public static ConfigProperty<bool> loot_quality_mod { get; private set; } = new(true, "if FALSE then the loot quality modifier of a Death Treasure profile does not affect loot generation");
        public static ConfigProperty<bool> myquest_throttle_enabled { get; private set; } = new(true, "if TRUE, then the player will be limited in their calls to this");
        public static ConfigProperty<bool> npc_hairstyle_fullrange { get; private set; } = new(false, "if TRUE, allows generated creatures to use full range of hairstyles. Retail only allowed first nine (0-8) out of 51");
        public static ConfigProperty<bool> offline_xp_passup_limit { get; private set; } = new(true, "if FALSE, allows unlimited xp to passup to offline characters in allegiances");
        public static ConfigProperty<bool> olthoi_play_disabled { get; private set; } = new(false, "if false, allows players to create and play as olthoi characters");
        public static ConfigProperty<bool> override_encounter_spawn_rates { get; private set; } = new(false, "if enabled, landblock encounter spawns are overidden by double properties below.");
        public static ConfigProperty<bool> permit_corpse_all { get; private set; } = new(false, "If TRUE, /permit grants permittees access to all corpses of the permitter. Defaults to FALSE as per retail, where /permit only grants access to 1 locked corpse");
        public static ConfigProperty<bool> persist_movement { get; private set; } = new(false, "If TRUE, persists autonomous movements such as turns and sidesteps through non-autonomous server actions. Retail didn't appear to do this, but some players may prefer this.");
        public static ConfigProperty<bool> pet_stow_replace { get; private set; } = new(false, "pet stowing for different pet devices becomes a stow and replace. defaults to retail value of false");
        public static ConfigProperty<bool> player_config_command { get; private set; } = new(false, "If enabled, players can use /config to change their settings via text commands");
        public static ConfigProperty<bool> player_receive_immediate_save { get; private set; } = new(false, "if enabled, when the player receives items from an NPC, they will be saved immediately");
        public static ConfigProperty<bool> pk_server { get; private set; } = new(false, "set this to TRUE for darktide servers");
        public static ConfigProperty<bool> pk_server_safe_training_academy { get; private set; } = new(false, "set this to TRUE to disable pk fighting in training academy and time to exit starter town safely");
        public static ConfigProperty<bool> pkl_server { get; private set; } = new(false, "set this to TRUE for pink servers");
        public static ConfigProperty<bool> quest_info_enabled { get; private set; } = new(false, "toggles the /myquests player command");
        public static ConfigProperty<bool> rares_real_time { get; private set; } = new(true, "allow for second chance roll based on an rng seeded timestamp for a rare on rare eligible kills that do not generate a rare, rares_max_seconds_between defines maximum seconds before second chance kicks in");
        public static ConfigProperty<bool> rares_real_time_v2 { get; private set; } = new(false, "chances for a rare to be generated on rare eligible kills are modified by the last time one was found per each player, rares_max_days_between defines maximum days before guaranteed rare generation");
        public static ConfigProperty<bool> runrate_add_hooks { get; private set; } = new(false, "if TRUE, adds some runrate hooks that were missing from retail (exhaustion done, raise skill/attribute");
        public static ConfigProperty<bool> reportbug_enabled { get; private set; } = new(false, "toggles the /reportbug player command");
        public static ConfigProperty<bool> require_spell_comps { get; private set; } = new(true, "if FALSE, spell components are no longer required to be in inventory to cast spells. defaults to enabled, as in retail");
        public static ConfigProperty<bool> safe_spell_comps { get; private set; } = new(false, "if TRUE, disables spell component burning for everyone");
        public static ConfigProperty<bool> salvage_handle_overages { get; private set; } = new(false, "in retail, if 2 salvage bags were combined beyond 100 structure, the overages would be lost");
        public static ConfigProperty<bool> show_ammo_buff { get; private set; } = new(false, "shows active enchantments such as blood drinker on equipped missile ammo during appraisal");
        public static ConfigProperty<bool> show_aura_buff { get; private set; } = new(false, "shows active aura enchantments on wielded items during appraisal");
        public static ConfigProperty<bool> show_dat_warning { get; private set; } = new(false, "if TRUE, will alert player (dat_warning_msg) when client attempts to download from server and boot them from game, disabled by default");
        public static ConfigProperty<bool> show_dot_messages { get; private set; } = new(false, "enabled, shows combat messages for DoT damage ticks. defaults to disabled, as in retail");
        public static ConfigProperty<bool> show_first_login_gift { get; private set; } = new(false, "if TRUE, will show on first login that the player earned bonus item (Blackmoor's Favor and/or Asheron's Benediction), disabled by default because msg is kind of odd on an emulator");
        public static ConfigProperty<bool> show_mana_conv_bonus_0 { get; private set; } = new(true, "if disabled, only shows mana conversion bonus if not zero, during appraisal of casting items");
        public static ConfigProperty<bool> smite_uses_takedamage { get; private set; } = new(false, "if enabled, smite applies damage via TakeDamage");
        public static ConfigProperty<bool> spellcast_recoil_queue { get; private set; } = new(false, "if true, players can queue the next spell to cast during recoil animation");
        public static ConfigProperty<bool> spell_projectile_ethereal { get; private set; } = new(false, "broadcasts all spell projectiles as ethereal to clients only, and manually send stop velocity on collision. can fix various issues with client missing target id.");
        public static ConfigProperty<bool> suicide_instant_death { get; private set; } = new(false, "if enabled, @die command kills player instantly. defaults to disabled, as in retail");
        public static ConfigProperty<bool> taboo_table { get; private set; } = new(true, "if enabled, taboo table restricts player names during character creation");
        public static ConfigProperty<bool> tailoring_intermediate_uieffects { get; private set; } = new(false, "If true, tailoring intermediate icons retain the magical/elemental highlight of the original item");
        public static ConfigProperty<bool> trajectory_alt_solver { get; private set; } = new(false, "use the alternate trajectory solver for missiles and spell projectiles");
        public static ConfigProperty<bool> universal_masteries { get; private set; } = new(true, "if TRUE, matches end of retail masteries - players wielding almost any weapon get +5 DR, except if the weapon \"seems tough to master\". " +
                                                                                                 "if FALSE, players start with mastery of 1 melee and 1 ranged weapon type based on heritage, and can later re-select these 2 masteries");
        public static ConfigProperty<bool> use_generator_rotation_offset { get; private set; } = new(true, "enables or disables using the generator's current rotation when offseting relative positions");
        public static ConfigProperty<bool> use_turbine_chat { get; private set; } = new(true, "enables or disables global chat channels (General, LFG, Roleplay, Trade, Olthoi, Society, Allegience)");
        public static ConfigProperty<bool> use_wield_requirements { get; private set; } = new(true, "disable this to bypass wield requirements. mostly for dev debugging");
        public static ConfigProperty<bool> version_info_enabled { get; private set; } = new(false, "toggles the /aceversion player command");
        public static ConfigProperty<bool> vendor_shop_uses_generator { get; private set; } = new(false, "enables or disables vendors using generator system in addition to createlist to create artificial scarcity");
        public static ConfigProperty<bool> world_closed { get; private set; } = new(false, "enable this to startup world as a closed to players world");
        public static ConfigProperty<bool> enl_removes_society { get; private set; } = new(true, "if true, enlightenment will remove society flags");
        public static ConfigProperty<bool> action_queue_tracking_enabled { get; private set; } = new(false, "if TRUE, enables runtime performance tracking for ActionQueue to identify slow actions. Zero overhead when disabled.");
        public static ConfigProperty<long> char_delete_time { get; private set; } = new(3600, "the amount of time in seconds a deleted character can be restored");
        public static ConfigProperty<long> chat_requires_account_time_seconds { get; private set; } = new(0, "the amount of time in seconds an account is required to have existed for for global chat privileges");
        public static ConfigProperty<long> chat_requires_player_age { get; private set; } = new(0, "the amount of time in seconds a player is required to have played for global chat privileges");
        public static ConfigProperty<long> chat_requires_player_level { get; private set; } = new(0, "the level a player is required to have for global chat privileges");
        public static ConfigProperty<long> corpse_spam_limit { get; private set; } = new(15, "the number of corpses a player is allowed to leave on a landblock at one time");
        public static ConfigProperty<long> empty_corpse_decay_seconds { get; private set; } = new(3, "the amount of time in seconds an empty corpse will take to decay (including corpses that have been looted empty)");
        public static ConfigProperty<long> default_subscription_level { get; private set; } = new(1, "retail defaults to 1, 1 = standard subscription (same as 2 and 3), 4 grants ToD pre-order bonus item Asheron's Benediction");
        public static ConfigProperty<long> fellowship_even_share_level { get; private set; } = new(50, "level when fellowship XP sharing is no longer restricted");
        public static ConfigProperty<long> mansion_min_rank { get; private set; } = new(6, "overrides the default allegiance rank required to own a mansion");
        public static ConfigProperty<long> max_chars_per_account { get; private set; } = new(11, "retail defaults to 11, client supports up to 20");
        public static ConfigProperty<long> pk_timer { get; private set; } = new(20, "the number of seconds where a player cannot perform certain actions (ie. teleporting) after becoming involved in a PK battle");
        public static ConfigProperty<long> player_save_interval { get; private set; } = new(300, "the number of seconds between automatic player saves");
        public static ConfigProperty<long> rares_max_days_between { get; private set; } = new(45, "for rares_real_time_v2: the maximum number of days a player can go before a rare is generated on rare eligible creature kills");
        public static ConfigProperty<long> rares_max_seconds_between { get; private set; } = new(5256000, "for rares_real_time: the maximum number of seconds a player can go before a second chance at a rare is allowed on rare eligible creature kills that did not generate a rare");
        public static ConfigProperty<long> summoning_killtask_multicredit_cap { get; private set; } = new(2, "if allow_summoning_killtask_multicredit is enabled, the maximum # of killtask credits a player can receive from 1 kill");
        public static ConfigProperty<long> teleport_visibility_fix { get; private set; } = new(0, "Fixes some possible issues with invisible players and mobs. 0 = default / disabled, 1 = players only, 2 = creatures, 3 = all world objects");
        public static ConfigProperty<long> enl_50_base_lum_cost { get; private set; } = new(100000000, "the base luminance cost for each enlighten after 50, this will be multiplied by the target enlightenment level");
        public static ConfigProperty<long> enl_150_base_lum_cost { get; private set; } = new(1000000000, "the base luminance cost for each enlighten after 150, this will be multiplied by the target enlightenment level");
        public static ConfigProperty<long> enl_300_base_lum_cost { get; private set; } = new(2000000000, "the base luminance cost for each enlighten after 300, this will be multiplied by the target enlightenment level");
        public static ConfigProperty<long> dynamic_quest_repeat_hours { get; private set; } = new(20, "the number of hours before a player can do another dynamic quest");
        public static ConfigProperty<long> dynamic_quest_max_xp { get; private set; } = new(5000000000, "the maximum base xp rewarded from a dynamic quest");
        public static ConfigProperty<long> max_nether_dot_damage_rating { get; private set; } = new(50, "the maximum damage rating from Void DoTs");
        public static ConfigProperty<long> bank_command_limit { get; private set; } = new(5, "The number of seconds a player must wait between making a bank deposit or withdrawl");
        public static ConfigProperty<long> clap_command_limit { get; private set; } = new(60, "The number of seconds a player must wait between using the clap command");
        public static ConfigProperty<long> qb_command_limit { get; private set; } = new(60, "The number of seconds a player must wait between using the qb list command");
        public static ConfigProperty<long> monster_tick_throttle_limit { get; private set; } = new(75, "Maximum number of monsters to process per tick per landblock. Higher = faster AI reactions but larger spikes during mass spawns. Adjust based on Discord alerts.");
        public static ConfigProperty<long> action_queue_throttle_limit { get; private set; } = new(300, "Maximum number of actions to process per tick. Higher = faster queue clearing but larger CPU spikes during heavy load. Adjust based on Discord alerts.");
        public static ConfigProperty<long> action_queue_track_threshold_ms { get; private set; } = new(10, "ActionQueue tracking: Only track actions taking longer than this many milliseconds. Lower = more detailed tracking but more overhead.");
        public static ConfigProperty<long> action_queue_warn_threshold_ms { get; private set; } = new(100, "ActionQueue tracking: Log warnings and send Discord alerts for actions exceeding this threshold in milliseconds.");
        public static ConfigProperty<long> action_queue_report_interval_minutes { get; private set; } = new(5, "ActionQueue tracking: Generate aggregated performance reports every N minutes.");
        public static ConfigProperty<long> action_queue_discord_max_alerts_per_minute { get; private set; } = new(3, "ActionQueue tracking: Maximum number of Discord alerts per minute to prevent API throttling. 0 = disable Discord alerts.");
        public static ConfigProperty<long> login_block_discord_max_alerts_per_minute { get; private set; } = new(3, "Item loss prevention: Max Discord alerts per minute for login blocking. Set to 0 to disable Discord alerts.");
        public static ConfigProperty<long> db_slow_discord_max_alerts_per_minute { get; private set; } = new(5, "DB diagnostics: Max Discord alerts per minute for slow saves. 0 = disable.");
        public static ConfigProperty<long> db_slow_threshold_ms { get; private set; } = new(1000, "DB diagnostics: Item saves slower than this (ms) trigger warnings and Discord alerts.");
        public static ConfigProperty<long> db_queue_alert_threshold { get; private set; } = new(100, "DB diagnostics: Send Discord alert when database queue count exceeds this value. 0 = disable.");
        public static ConfigProperty<long> db_queue_discord_max_alerts_per_minute { get; private set; } = new(2, "DB diagnostics: Max Discord alerts per minute for high database queue. 0 = disable.");
        public static ConfigProperty<long> vitae_per_level { get; private set; } = new(5, "the number of vitae lost on death per level, for VPHardcore only");
        public static ConfigProperty<double> cantrip_drop_rate { get; private set; } = new(1.0, "Scales the chance for cantrips to drop in each tier. Defaults to 1.0, as per end of retail");
        public static ConfigProperty<double> cloak_cooldown_seconds { get; private set; } = new(5.0, "The number of seconds between possible cloak procs.");
        public static ConfigProperty<double> cloak_min_proc_base { get; private set; } = new(0, "The min proc chance of a cloak.");
        public static ConfigProperty<double> cloak_max_proc_base { get; private set; } = new(0.25, "The max proc chance of a cloak.");
        public static ConfigProperty<double> cloak_max_proc_damage_percentage { get; private set; } = new(0.30, "The damage percentage at which cloak proc chance plateaus.");
        public static ConfigProperty<double> minor_cantrip_drop_rate { get; private set; } = new(1.0, "Scales the chance for minor cantrips to drop, relative to other cantrip levels in the tier. Defaults to 1.0, as per end of retail");
        public static ConfigProperty<double> major_cantrip_drop_rate { get; private set; } = new(1.0, "Scales the chance for major cantrips to drop, relative to other cantrip levels in the tier. Defaults to 1.0, as per end of retail");
        public static ConfigProperty<double> epic_cantrip_drop_rate { get; private set; } = new(1.0, "Scales the chance for epic cantrips to drop, relative to other cantrip levels in the tier. Defaults to 1.0, as per end of retail");
        public static ConfigProperty<double> legendary_cantrip_drop_rate { get; private set; } = new(1.0, "Scales the chance for legendary cantrips to drop, relative to other cantrip levels in the tier. Defaults to 1.0, as per end of retail");
        public static ConfigProperty<double> advocate_fane_auto_bestow_level { get; private set; } = new(1, "the level that advocates are automatically bestowed by Advocate Fane if advocate_fane_auto_bestow is true");
        public static ConfigProperty<double> aetheria_drop_rate { get; private set; } = new(1.0, "Modifier for Aetheria drop rate, 1 being normal");
        public static ConfigProperty<double> chess_ai_start_time { get; private set; } = new(-1.0, "the number of seconds for the chess ai to start. defaults to -1 (disabled)");
        public static ConfigProperty<double> encounter_delay { get; private set; } = new(1800, "the number of seconds a generator profile for regions is delayed from returning to free slots");
        public static ConfigProperty<double> encounter_regen_interval { get; private set; } = new(600, "the number of seconds a generator for regions at which spawns its next set of objects");
        public static ConfigProperty<double> equipmentsetid_drop_rate { get; private set; } = new(1.0, "Modifier for EquipmentSetID drop rate, 1 being normal");
        public static ConfigProperty<double> fast_missile_modifier { get; private set; } = new(1.2, "The speed multiplier applied to fast missiles. Defaults to retail value of 1.2");
        public static ConfigProperty<double> hardcore_xp_multiplier { get; private set; } = new(0.05, "the number of vitae lost on death per level, for VPHardcore only");
        public static ConfigProperty<double> ignore_magic_armor_pvp_scalar { get; private set; } = new(1.0, "Scales the effectiveness of IgnoreMagicArmor (ie. hollow weapons) in pvp battles. 1.0 = full effectiveness / ignore all enchantments on armor (default), 0.5 = half effectiveness / use half enchantments from armor, 0.0 = no effectiveness / use full enchantments from armor");
        public static ConfigProperty<double> ignore_magic_resist_pvp_scalar { get; private set; } = new(1.0, "Scales the effectiveness of IgnoreMagicResist (ie. hollow weapons) in pvp battles. 1.0 = full effectiveness / ignore all resistances from life enchantments (default), 0.5 = half effectiveness / use half resistances from life enchantments, 0.0 = no effectiveness / use full resistances from life enchantments");
        public static ConfigProperty<double> luminance_modifier { get; private set; } = new(1.0, "Scales the amount of luminance received by players");
        public static ConfigProperty<double> lum_passup_mult { get; private set; } = new(0.5, "Scales the amount of luminance passed up in the chain");
        public static ConfigProperty<double> nether_resist_rating_scalar { get; private set; } = new(0.25, "Multiplier for nether resistance rating effectiveness. 1.0 = normal effectiveness, 0.25 = 75% less effective (default), 0.5 = half as effective");
        public static ConfigProperty<double> melee_max_angle { get; private set; } = new(0.0, "for melee players, the maximum angle before a TurnTo is required. retail appeared to have required a TurnTo even for the smallest of angle offsets.");
        public static ConfigProperty<double> mob_awareness_range { get; private set; } = new(1.0, "Scales the distance the monsters become alerted and aggro the players");
        public static ConfigProperty<double> pk_new_character_grace_period { get; private set; } = new(300, "the number of seconds, in addition to pk_respite_timer, that a player killer is set to non-player killer status after first exiting training academy");
        public static ConfigProperty<double> pk_respite_timer { get; private set; } = new(300, "the number of seconds that a player killer is set to non-player killer status after dying to another player killer");
        public static ConfigProperty<double> quest_lum_modifier { get; private set; } = new(1.0, "Scale multiplier for amount of quest luminance received by players.  Quest lum is also modified by 'luminance_modifier'.");
        public static ConfigProperty<double> quest_mindelta_rate { get; private set; } = new(1.0, "scales all quest min delta time between solves, 1 being normal");
        public static ConfigProperty<double> quest_xp_modifier { get; private set; } = new(1.0, "Scale multiplier for amount of quest XP received by players.  Quest XP is also modified by 'xp_modifier'.");
        public static ConfigProperty<double> rare_drop_rate_percent { get; private set; } = new(0.04, "Adjust the chance of a rare to spawn as a percentage. Default is 0.04, or 1 in 2,500. Max is 100, or every eligible drop.");
        public static ConfigProperty<double> spellcast_max_angle { get; private set; } = new(20.0, "for advanced player spell casting, the maximum angle to target release a spell projectile. retail seemed to default to value of around 20, although some players seem to prefer a higher 45 degree angle");
        public static ConfigProperty<double> trophy_drop_rate { get; private set; } = new(1.0, "Modifier for trophies dropped on creature death");
        public static ConfigProperty<double> unlocker_window { get; private set; } = new(10.0, "The number of seconds a player unlocking a chest has exclusive access to first opening the chest.");
        public static ConfigProperty<double> vendor_unique_rot_time { get; private set; } = new(300, "the number of seconds before unique items sold to vendors disappear");
        public static ConfigProperty<double> vitae_penalty { get; private set; } = new(0.05, "the amount of vitae penalty a player gets per death");
        public static ConfigProperty<double> vitae_penalty_max { get; private set; } = new(0.40, "the maximum vitae penalty a player can have");
        public static ConfigProperty<double> void_dot_duration_aug_effect { get; private set; } = new(0.1, "the scaling factor by which DoTs are scaled by aug levels");
        public static ConfigProperty<double> void_pvp_modifier { get; private set; } = new(0.5, "Scales the amount of damage players take from Void Magic. Defaults to 0.5, as per retail. For earlier content where DRR isn't as readily available, this can be adjusted for balance.");
        public static ConfigProperty<double> xp_modifier { get; private set; } = new(1.0, "scales the amount of xp received by players");
        public static ConfigProperty<double> xp_batch_window_seconds { get; private set; } = new(5.0, "collects xp into batches to send as groups of packets");
        public static ConfigProperty<double> melee_missile_aug_crit_modifier { get; private set; } = new(0.002, "the maximum crit damage bonus from melee and missile augs");
        public static ConfigProperty<double> finesse_attribute_multiplier { get; private set; } = new(1.5, "the multiplier applied to coordination for calculating finesse weapons attribute damage modifiers");
        public static ConfigProperty<double> light_attribute_multiplier { get; private set; } = new(1.0, "the multiplier applied to strength for calculating light weapons attribute damage modifiers");
        public static ConfigProperty<double> heavy_attribute_multiplier { get; private set; } = new(1.0, "the multiplier applied to strength for calculating heavy weapons attribute damage modifiers");
        public static ConfigProperty<double> twohanded_attribute_multiplier { get; private set; } = new(0.8, "the multiplier applied to strength for calculating two handed weapons attribute damage modifiers");
        public static ConfigProperty<double> missile_attribute_multiplier { get; private set; } = new(1.0, "the multiplier applied to coordination for calculating missile weapons attribute damage modifiers");
        public static ConfigProperty<double> new_life_aug_curve_pct { get; private set; } = new(0.0, "a value between 0 and 1 representing the amount of the new curve to apply. 0 means the old curve will be used, 1 means the new curve will be used, and 0.5 means the midpoint between the curves will be used.");
        public static ConfigProperty<double> life_aug_prot_tuning_constant { get; private set; } = new(0.0034597, "the tuning constant r used in the  (1.0 - (1.0 - r)^a) life aug scaling formula - controls the size of step for each augmentation, relative to remaining cap (0.0034597 means every 200 augs halves the remaining bonus)");
        public static ConfigProperty<double> life_aug_prot_max_bonus { get; private set; } = new(0.32, "the maximum bonus that the life aug scaling can approach at infinite augs - T8 protection spells provide 68% base, so a bonus above 32% makes it possible to achieve full protection");
        public static ConfigProperty<string> content_folder { get; private set; } = new("Content", "for content creators to live edit weenies. defaults to Content folder found in same directory as ACE.Server.dll");
        public static ConfigProperty<string> dat_older_warning_msg { get; private set; } = new("Your DAT files are incomplete.\nThis server does not support dynamic DAT updating at this time.\nPlease visit https://emulator.ac/how-to-play to download the complete DAT files.", "Warning message displayed (if show_dat_warning is true) to player if client attempts DAT download from server");
        public static ConfigProperty<string> dat_newer_warning_msg { get; private set; } = new("Your DAT files are newer than expected.\nPlease visit https://emulator.ac/how-to-play to download the correct DAT files.", "Warning message displayed (if show_dat_warning is true) to player if client connects to this server");
        public static ConfigProperty<string> popup_header { get; private set; } = new("Welcome to Asheron's Call!", "Welcome message displayed when you log in");
        public static ConfigProperty<string> popup_welcome { get; private set; } = new("To begin your training, speak to the Society Greeter. Walk up to the Society Greeter using the 'W' key, then double-click on her to initiate a conversation.", "Welcome message popup in training halls");
        public static ConfigProperty<string> popup_welcome_olthoi { get; private set; } = new("Welcome to the Olthoi hive! Be sure to talk to the Olthoi Queen to receive the Olthoi protections granted by the energies of the hive.", "Welcome message displayed on the first login for an Olthoi Player");
        public static ConfigProperty<string> popup_motd { get; private set; } = new("", "Popup message of the day");
        public static ConfigProperty<string> server_motd { get; private set; } = new("", "Server message of the day");
    }

    public static class PropertyManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        private static readonly Timer _workerThread = new(300000);

        /// <summary>
        /// Initializes the PropertyManager.
        /// Run this only once per server instance.
        /// </summary>
        public static void Initialize()
        {
            ServerConfig.LoadFromDb();

            if (Program.IsRunningInContainer && !ServerConfig.content_folder.HasValue)
                ServerConfig.SetValue("content_folder", "/ace/Content");

            // Subscribe the worker thread to  execute DoWork whenever it elapses.
            _workerThread.Elapsed += (Object source, ElapsedEventArgs e) => DoWork();
            _workerThread.AutoReset = true;
            _workerThread.Start();
        }

        /// <summary>
        /// Force syncs the variables with the database manually.
        /// Disables the timer so that the elapsed event cannot run during the update operation.
        /// </summary>
        public static void ResyncVariables()
        {
            _workerThread.Stop();
            DoWork();
            _workerThread.Start();
        }

        /// <summary>
        /// Stops updating the cached store from the database.
        /// </summary>
        public static void StopUpdating()
        {
            _workerThread?.Stop();
        }

        /// <summary>
        /// Flushes pending writes to the datbase, then refreshes our view to be consistent.
        /// </summary>
        private static void DoWork()
        {
            var startTime = DateTime.UtcNow;
            ServerConfig.WriteUpdatesToDb();
            ServerConfig.LoadFromDb();
            log.Debug($"PropertyManager DoWork took {(DateTime.UtcNow - startTime).TotalMilliseconds:N0} ms");
        }
    }
}
