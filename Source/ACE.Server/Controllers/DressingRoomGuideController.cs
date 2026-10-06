using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Web.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Controllers
{
    /// <summary>
    /// Public data for the player Dressing Room guide (#/dressing-room). Like the pet guide, the page hardcodes no
    /// numbers: the fee comes from the effective ServerConfig values and is priced by the same function the attendant
    /// uses, the slot names are the ones the attendant says, the attendants and where they stand come from the world
    /// database, and what each body can show is counted from the client's clothing tables.
    /// </summary>
    [ApiController]
    [Route("api/dressing-room-guide")]
    public class DressingRoomGuideController : BaseController
    {
        /// <summary>How many lock-ins of one slot the fee table lists at most (it stops early once the cap is reached).</summary>
        private const int MaxScheduleRows = 12;

        // Attendants move only with a content push; cache them so the public page is cheap.
        private static readonly TimeSpan WorldCacheLifetime = TimeSpan.FromMinutes(5);
        private static readonly object worldCacheLock = new();
        private static object attendantsCache;
        private static DateTime attendantsCacheBuiltUtc;

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Get()
        {
            var baseFee = ServerConfig.dressing_room_fee_base.Value;
            var growth = ServerConfig.dressing_room_fee_growth.Value;
            var cap = ServerConfig.dressing_room_fee_cap.Value;

            return Ok(new
            {
                enabled = ServerConfig.dressing_room_enabled.Value,
                fee = new
                {
                    baseFee,
                    cap,
                    // what the attendant charges for a piece whose most-locked slot has been locked 0, 1, 2... times before
                    schedule = FeeSchedule(baseFee, growth, cap),
                },
                slots = DressingRoomLook.SlotBits(DressingRoomLook.SlotMask).Select(bit => DressingRoom.SlotName(bit)).ToList(),
                attendants = Attendants(),
                bodies = DressingRoomBodies.All.Select(b => new
                {
                    heritage = b.Heritage,
                    gender = b.Gender,
                    areas = b.Areas.Select(a => new { key = a.Key, label = a.Label, shown = a.Shown, total = a.Total }),
                }),
            });
        }

        /// <summary>The fee per earlier lock-in of a slot, from none upward, ending at the first row that reaches the highest fee.</summary>
        public static List<long> FeeSchedule(long baseFee, double growth, long cap)
        {
            var schedule = new List<long>();
            for (var prior = 0; prior < MaxScheduleRows; prior++)
            {
                var fee = DressingRoomLook.Fee(prior, baseFee, growth, cap);
                schedule.Add(fee);
                // once a further lock-in costs the same, every later one does too
                if (DressingRoomLook.Fee(prior + 1, baseFee, growth, cap) == fee)
                    break;
            }
            return schedule;
        }

        /// <summary>Every NPC flagged as an attendant (PropertyBool.DressingRoomAttendant) and where the world database places it.</summary>
        private static object Attendants()
        {
            lock (worldCacheLock)
            {
                if (attendantsCache != null && DateTime.UtcNow - attendantsCacheBuiltUtc < WorldCacheLifetime)
                    return attendantsCache;

                List<uint> wcids;
                List<LandblockInstance> spawns;
                using (var context = new WorldDbContext())
                {
                    wcids = context.WeeniePropertiesBool.AsNoTracking()
                        .Where(p => p.Type == (ushort)PropertyBool.DressingRoomAttendant && p.Value)
                        .Select(p => p.ObjectId)
                        .ToList();
                    spawns = context.LandblockInstance.AsNoTracking()
                        .Where(i => wcids.Contains(i.WeenieClassId))
                        .ToList();
                }

                attendantsCache = wcids.OrderBy(w => w).Select(wcid => new
                {
                    wcid,
                    name = DatabaseManager.World.GetCachedWeenie(wcid)?.GetProperty(PropertyString.Name),
                    spots = spawns.Where(s => s.WeenieClassId == wcid).Select(PetGuideController.SpotDto).ToList(),
                }).ToList();
                attendantsCacheBuiltUtc = DateTime.UtcNow;
                return attendantsCache;
            }
        }
    }
}
