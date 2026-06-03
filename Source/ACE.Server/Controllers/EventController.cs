using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Common;
using Microsoft.AspNetCore.Mvc;

namespace ACE.Server.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventController : BaseController
    {
        [HttpGet("list")]
        public IActionResult GetEvents()
        {
            if (!HasPortalAccess(PortalPages.Events))
                return Forbid();

            try
            {
                var result = new List<EventMetadata>();

                foreach (var evnt in EventManager.GetEventSnapshots().OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var state = EventManager.GetEventStatus(evnt.Name);
                    var isActive = EventManager.IsEventStarted(evnt.Name, null, null);

                    result.Add(new EventMetadata
                    {
                        Name = evnt.Name,
                        State = state.ToString(),
                        IsActive = isActive,
                        CanStart = state is GameEventState.Enabled or GameEventState.Off,
                        CanStop = state is GameEventState.Enabled or GameEventState.On,
                        IsDisabled = state == GameEventState.Disabled,
                        IsScheduled = evnt.StartTime != -1 || evnt.EndTime != -1,
                        StartTime = evnt.StartTime,
                        EndTime = evnt.EndTime,
                        StartCommand = $"/event start {evnt.Name}",
                        StopCommand = $"/event stop {evnt.Name}",
                        StatusCommand = $"/event status {evnt.Name}",
                    });
                }

                var pkActive = ServerConfig.pk_server.Value;
                result.Add(new EventMetadata
                {
                    Name = "EventIsPKWorld",
                    State = pkActive ? GameEventState.On.ToString() : GameEventState.Off.ToString(),
                    IsActive = pkActive,
                    IsVirtual = true,
                    CanStart = false,
                    CanStop = false,
                    IsDisabled = false,
                    StatusCommand = "/showbool pk_server",
                    StartCommand = "/modifybool pk_server true",
                    StopCommand = "/modifybool pk_server false",
                });

                return Ok(result.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList());
            }
            catch (Exception ex)
            {
                var correlationId = Guid.NewGuid().ToString();
                Log.Error($"[Correlation ID: {correlationId}] Failed to fetch events", ex);
                return StatusCode(500, new
                {
                    Message = "An unexpected error occurred while processing your request.",
                    CorrelationId = correlationId
                });
            }
        }

        public class EventMetadata
        {
            public string Name { get; set; } = "";
            public string State { get; set; } = "";
            public bool IsActive { get; set; }
            public bool CanStart { get; set; }
            public bool CanStop { get; set; }
            public bool IsDisabled { get; set; }
            public bool IsScheduled { get; set; }
            public int StartTime { get; set; }
            public int EndTime { get; set; }
            public string StartCommand { get; set; } = "";
            public string StopCommand { get; set; } = "";
            public string StatusCommand { get; set; } = "";
            public bool IsVirtual { get; set; }
        }
    }
}
