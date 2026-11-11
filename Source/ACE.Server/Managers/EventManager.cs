using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Server.WorldObjects;

using log4net;

namespace ACE.Server.Managers
{
    public static class EventManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly object _eventsLock = new object();

        public static Dictionary<string, Event> Events;

        public static bool Debug = false;

        private static System.Threading.Timer _scheduleTimer;
        private static int _lastMonthVal = -1;
        private static int _lastWeekVal = -1;
        private static int _lastQuarterVal = -1;

        static EventManager()
        {
            Events = new Dictionary<string, Event>(StringComparer.OrdinalIgnoreCase);
        }

        // The state each event had in the world database at startup - what a tier copy's event starts from.
        private static readonly Dictionary<string, int> _initialStates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Looks an event up by its (already normalised) name. Call under _eventsLock.
        ///
        /// Tiered dungeons: a tier-tagged name ("ColoArenaOneInUse~v11") that does not exist yet is created here, in
        /// memory only, as a copy of its retail event at the retail event's DATABASE state - never its live state, so a
        /// retail arena in use does not start a tier arena as in use. It is created on the first lookup because a
        /// generator ignores an event that does not exist (WorldObject_Generators.CheckEventStatus). An untagged name
        /// never reaches the create branch, so retail lookups are exactly Events.TryGetValue.
        /// </summary>
        private static bool TryGetEvent(string eventName, out Event evnt)
        {
            if (Events.TryGetValue(eventName, out evnt))
                return true;

            if (!TieredDungeons.TieredDungeonRegistry.IsTagged(eventName))
                return false;

            var baseName = TieredDungeons.TieredDungeonRegistry.StripTag(eventName);
            if (!Events.TryGetValue(baseName, out var baseEvent))
                return false;

            evnt = new Event
            {
                Name = eventName,
                StartTime = baseEvent.StartTime,
                EndTime = baseEvent.EndTime,
                State = _initialStates.TryGetValue(baseName, out var state) ? state : baseEvent.State,
                LastModified = baseEvent.LastModified,
            };
            Events[eventName] = evnt;
            log.Debug($"[EVENT] created tier event {eventName} from {baseName} (state {(GameEventState)evnt.State})");
            return true;
        }

        public static void Initialize()
        {
            var events = Database.DatabaseManager.World.GetAllEvents();

            foreach(var evnt in events)
            {
                Events.Add(evnt.Name, evnt);
                _initialStates[evnt.Name] = evnt.State;

                if (evnt.State == (int)GameEventState.On)
                    StartEvent(evnt.Name, null, null);
            }

            log.DebugFormat("EventManager Initalized.");

            try
            {
                CheckCalendarEvents();
                _scheduleTimer = new System.Threading.Timer(OnScheduleTimer, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
            }
            catch (Exception ex)
            {
                log.Error($"[EventManager] Failed to initialize calendar scheduler: {ex}");
            }
        }

        /// <summary>
        /// Reloads a single event from the database into the Events dictionary
        /// </summary>
        public static bool ReloadEvent(string eventName)
        {
            // Normalize event name (strip @comment if present)
            var normalizedName = GetEventName(eventName);

            // Clear from database cache first so we get fresh data
            Database.DatabaseManager.World.ClearCachedEvent(normalizedName);

            var evnt = Database.DatabaseManager.World.GetCachedEvent(normalizedName);

            lock (_eventsLock)
            {
                if (evnt != null)
                {
                    Events[evnt.Name] = evnt;
                    // tiered dungeons: tier copies created from now on start from the reloaded database state
                    // (ReloadAllEvents does the same), not the state the event had at startup
                    _initialStates[evnt.Name] = evnt.State;
                    log.Debug($"[EventManager] Reloaded event '{evnt.Name}' from database (State: {(GameEventState)evnt.State})");
                    return true;
                }

                _initialStates.Remove(normalizedName);
                if (Events.Remove(normalizedName))
                    log.Debug($"[EventManager] Removed event '{normalizedName}' (not found in database)");
                return false;
            }
        }

        /// <summary>
        /// Reloads all events from the database into the Events dictionary
        /// </summary>
        public static void ReloadAllEvents()
        {
            // Clear database cache
            Database.DatabaseManager.World.ClearAllCachedEvents();

            // Build new dictionary
            var events = Database.DatabaseManager.World.GetAllEvents();
            var reloaded = new Dictionary<string, Event>(StringComparer.OrdinalIgnoreCase);
            foreach (var evnt in events)
                reloaded[evnt.Name] = evnt;

            lock (_eventsLock)
            {
                // tiered dungeons: tier events exist only in memory - carry them over with their live state, or a
                // reload during a run would hand every tier arena back its starting state
                foreach (var kvp in Events)
                    if (TieredDungeons.TieredDungeonRegistry.IsTagged(kvp.Key))
                        reloaded[kvp.Key] = kvp.Value;

                foreach (var evnt in events)
                    _initialStates[evnt.Name] = evnt.State;

                Events = reloaded;
            }

            log.Debug($"[EventManager] Reloaded {reloaded.Count} events from database");
        }

        public static bool StartEvent(string e, WorldObject source, WorldObject target)
        {
            var eventName = GetEventName(e);

            if (eventName.Equals("EventIsPKWorld", StringComparison.OrdinalIgnoreCase)) // special event
                return false;

            lock (_eventsLock)
            {
                if (!TryGetEvent(eventName, out Event evnt))
                    return false;

                var state = (GameEventState)evnt.State;

                if (state == GameEventState.Disabled)
                    return false;

                if (state == GameEventState.Enabled || state == GameEventState.Off)
                {
                    evnt.State = (int)GameEventState.On;

                    if (Debug)
                        Console.WriteLine($"Starting event {evnt.Name}");
                }

                log.Debug($"[EVENT] {(source == null ? "SYSTEM" : $"{source.Name} (0x{source.Guid}|{source.WeenieClassId})")}{(target == null ? "" : $", triggered by {target.Name} (0x{target.Guid}|{target.WeenieClassId}),")} started an event: {evnt.Name}{((int)state == evnt.State ? (source == null ? ", which is the default state for this event." : ", which had already been started.") : "")}");

                return true;
            }
        }

        public static bool StopEvent(string e, WorldObject source, WorldObject target)
        {
            var eventName = GetEventName(e);

            if (eventName.Equals("EventIsPKWorld", StringComparison.OrdinalIgnoreCase)) // special event
                return false;

            lock (_eventsLock)
            {
                if (!TryGetEvent(eventName, out Event evnt))
                    return false;

                var state = (GameEventState)evnt.State;

                if (state == GameEventState.Disabled)
                    return false;

                if (state == GameEventState.Enabled || state == GameEventState.On)
                {
                    evnt.State = (int)GameEventState.Off;

                    if (Debug)
                        Console.WriteLine($"Stopping event {evnt.Name}");
                }

                log.Debug($"[EVENT] {(source == null ? "SYSTEM" : $"{source.Name} (0x{source.Guid}|{source.WeenieClassId})")}{(target == null ? "" : $", triggered by {target.Name} (0x{target.Guid}|{target.WeenieClassId}),")} stopped an event: {evnt.Name}{((int)state == evnt.State ? (source == null ? ", which is the default state for this event." : ", which had already been stopped.") : "")}");

                return true;
            }
        }

        public static bool IsEventStarted(string e, WorldObject source, WorldObject target)
        {
            var eventName = GetEventName(e);

            if (eventName.Equals("EventIsPKWorld", StringComparison.OrdinalIgnoreCase)) // special event
            {
                var serverPkState = ServerConfig.pk_server.Value;

                return serverPkState;
            }

            lock (_eventsLock)
            {
                if (!TryGetEvent(eventName, out Event evnt))
                    return false;

                if (evnt.State != (int)GameEventState.Disabled && (evnt.StartTime != -1 || evnt.EndTime != -1))
                {
                    var prevState = (GameEventState)evnt.State;

                    var now = (int)Time.GetUnixTime();

                    var start = (now > evnt.StartTime) && (evnt.StartTime > -1);
                    var end = (now > evnt.EndTime) && (evnt.EndTime > -1);

                    if (prevState == GameEventState.On && end)
                        return !StopEvent(evnt.Name, source, target);
                    else if ((prevState == GameEventState.Off || prevState == GameEventState.Enabled) && start && !end)
                        return StartEvent(evnt.Name, source, target);
                }

                return evnt.State == (int)GameEventState.On;
            }
        }

        public static bool IsEventEnabled(string e)
        {
            var eventName = GetEventName(e);

            lock (_eventsLock)
            {
                if (!TryGetEvent(eventName, out Event evnt))
                    return false;

                return evnt.State != (int)GameEventState.Disabled;
            }
        }

        public static bool IsEventAvailable(string e)
        {
            var eventName = GetEventName(e);

            lock (_eventsLock)
            {
                return TryGetEvent(eventName, out _);
            }
        }

        public static GameEventState GetEventStatus(string e)
        {
            var eventName = GetEventName(e);

            if (eventName.Equals("EventIsPKWorld", StringComparison.OrdinalIgnoreCase)) // special event
            {
                if (ServerConfig.pk_server.Value)
                    return GameEventState.On;
                else
                    return GameEventState.Off;
            }

            lock (_eventsLock)
            {
                if (!TryGetEvent(eventName, out Event evnt))
                    return GameEventState.Undef;

                return (GameEventState)evnt.State;
            }
        }

        /// <summary>
        /// Thread-safe snapshot of registered events for admin tooling (web portal).
        /// </summary>
        public static List<Event> GetEventSnapshots()
        {
            lock (_eventsLock)
                return Events.Values.ToList();
        }

        /// <summary>
        /// Returns the event name without the @ comment
        /// </summary>
        /// <param name="eventFormat">A event name with an optional @comment on the end</param>
        public static string GetEventName(string eventFormat)
        {
            var idx = eventFormat.IndexOf('@');     // strip comment
            // tiered dungeons: inside a tier copy's scope the event is the tier's own ("Name~v11"); unchanged outside one
            if (idx == -1)
                return TieredDungeons.TieredDungeonRegistry.ApplyScope(eventFormat);

            var eventName = eventFormat.Substring(0, idx);
            return TieredDungeons.TieredDungeonRegistry.ApplyScope(eventName);
        }

        private static void OnScheduleTimer(object state)
        {
            try
            {
                CheckCalendarEvents();
            }
            catch (Exception ex)
            {
                log.Error($"[EventManager] Error in calendar scheduler timer tick: {ex}");
            }
        }

        public static void CheckCalendarEvents()
        {
            var utcNow = DateTime.UtcNow;
            var currentMonthVal = utcNow.Month;
            var currentQuarterVal = (currentMonthVal - 1) / 3 + 1;
            var currentWeekVal = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                utcNow, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Sunday);
            // Cap at 53: GetWeekOfYear with FirstDay rule can return 53 in years where Jan 1 falls late in the week.
            // Clamping to 52 would incorrectly fire Week52 events during that last week.
            if (currentWeekVal > 53) currentWeekVal = 53;

            if (currentMonthVal == _lastMonthVal && currentWeekVal == _lastWeekVal && currentQuarterVal == _lastQuarterVal)
                return;

            _lastMonthVal = currentMonthVal;
            _lastWeekVal = currentWeekVal;
            _lastQuarterVal = currentQuarterVal;

            var currentMonthEvent = $"Month{currentMonthVal}";
            var currentQuarterEvent = $"Quarter{currentQuarterVal}";
            var currentWeekEvent = $"Week{currentWeekVal}";

            log.Info($"[EventManager] Updating seasonal event states. Month: {currentMonthVal}, Quarter: {currentQuarterVal}, Week: {currentWeekVal}");

            List<string> eventNames;
            lock (_eventsLock)
            {
                eventNames = new List<string>(Events.Keys);
            }

            foreach (var name in eventNames)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^Month\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    if (name.Equals(currentMonthEvent, StringComparison.OrdinalIgnoreCase))
                        StartEvent(name, null, null);
                    else
                        StopEvent(name, null, null);
                }
                else if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^Quarter\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    if (name.Equals(currentQuarterEvent, StringComparison.OrdinalIgnoreCase))
                        StartEvent(name, null, null);
                    else
                        StopEvent(name, null, null);
                }
                else if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^Week\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    if (name.Equals(currentWeekEvent, StringComparison.OrdinalIgnoreCase))
                        StartEvent(name, null, null);
                    else
                        StopEvent(name, null, null);
                }
            }
        }

        /// <summary>
        /// Disposes the background calendar timer. Should be called during server shutdown
        /// to prevent the timer from firing against stale state after the world is torn down.
        /// </summary>
        public static void Shutdown()
        {
            _scheduleTimer?.Dispose();
            _scheduleTimer = null;
        }
    }
}
