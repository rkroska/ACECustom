using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;
using ACE.Server.Entity;
using ACE.Database;
using ACE.Database.Models.Shard;

using Biota = ACE.Entity.Models.Biota;

namespace ACE.Server.Controllers
{
    /// <summary>
    /// Web Portal: death/vitae snapshot (biota), live position (online player), and active player corpses (loaded landblocks).
    /// </summary>
    internal static class CharacterPortalStatusHelper
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(CharacterPortalStatusHelper));
        private static readonly int VitaeSpellId = (int)SpellId.Vitae;

        public static object BuildStatusJson(uint characterGuid, Biota snapshot)
        {
            var numDeaths = snapshot.GetProperty(PropertyInt.NumDeaths);
            var deathLevel = snapshot.GetProperty(PropertyInt.DeathLevel);
            var deathTsFloat = snapshot.GetProperty(PropertyFloat.DeathTimestamp);
            var deathTsInt = snapshot.GetProperty(PropertyInt.DeathTimestamp);

            string deathTimeUtcIso = null;
            double? deathUnix = null;
            if (deathTsFloat.HasValue && deathTsFloat.Value > 0)
            {
                deathUnix = deathTsFloat.Value;
                deathTimeUtcIso = DateTime.SpecifyKind(Time.GetDateTimeFromTimestamp(deathTsFloat.Value), DateTimeKind.Utc).ToString("o");
            }
            else if (deathTsInt.HasValue && deathTsInt.Value > 0)
            {
                deathUnix = deathTsInt.Value;
                deathTimeUtcIso = DateTime.SpecifyKind(Time.GetDateTimeFromTimestamp(deathTsInt.Value), DateTimeKind.Utc).ToString("o");
            }

            var vitaeEntry = snapshot.PropertiesEnchantmentRegistry?.FirstOrDefault(e => e.SpellId == VitaeSpellId);
            float? vitaeMult = vitaeEntry?.StatModValue;
            double? penaltyPct = vitaeMult.HasValue ? Math.Round((1.0 - vitaeMult.Value) * 100.0, 2) : null;

            object livePos = null;
            var online = PlayerManager.GetOnlinePlayer(characterGuid);
            if (online != null)
            {
                online.BiotaDatabaseLock.EnterReadLock();
                try
                {
                    livePos = SerializePosition(online.Location);
                }
                finally
                {
                    online.BiotaDatabaseLock.ExitReadLock();
                }
            }

            var corpses = FindPlayerCorpses(characterGuid);

            return new
            {
                death = new
                {
                    numDeaths = numDeaths ?? 0,
                    deathLevel,
                    deathTimestampUnix = deathUnix,
                    deathTimeUtc = deathTimeUtcIso
                },
                vitae = new
                {
                    hasVitae = vitaeEntry != null,
                    multiplier = vitaeMult,
                    penaltyPercent = penaltyPct,
                    vitaeCpPool = snapshot.GetProperty(PropertyInt.VitaeCpPool)
                },
                live = new
                {
                    isOnline = online != null,
                    position = livePos,
                    note = online != null
                        ? "Live position while this character is in-game."
                        : "Position is only available while the character is online."
                },
                corpses,
                narrativeNote = "Killer text and corpse rows exist only while a player corpse is in the world; after decay that context is gone unless you add server-side logging."
            };
        }

        public static object SerializePosition(Position loc)
        {
            if (loc == null)
                return null;

            string description = null;
            if (loc.Indoors)
            {
                description = DungeonNameResolver.Resolve(loc.Landblock, loc.Variation ?? 0);
            }
            else
            {
                description = loc.GetMapCoordStr();
            }

            return new
            {
                cell = loc.Cell,
                landblockX = loc.LandblockX,
                landblockY = loc.LandblockY,
                cellX = loc.CellX,
                cellY = loc.CellY,
                positionX = loc.PositionX,
                positionY = loc.PositionY,
                positionZ = loc.PositionZ,
                variation = loc.Variation,
                indoors = loc.Indoors,
                description = description
            };
        }

        public static List<PlayerCorpseInfo> FindPlayerCorpses(uint victimGuid)
        {
            var result = new List<PlayerCorpseInfo>();
            var loadedGuids = new HashSet<uint>();
            var landblocks = LandblockManager.loadedLandblocks.Values;

            foreach (var lb in landblocks)
            {
                if (lb == null)
                    continue;

                IEnumerable<WorldObject> objects;
                try
                {
                    objects = lb.GetWorldObjectsForDiagnostics();
                }
                catch
                {
                    continue;
                }

                foreach (var wo in objects)
                {
                    if (wo is not Corpse corpse || corpse.IsMonster)
                        continue;
                    if (!corpse.VictimId.HasValue || corpse.VictimId.Value != victimGuid)
                        continue;

                    try
                    {
                        corpse.BiotaDatabaseLock.EnterReadLock();
                        try
                        {
                            result.Add(new PlayerCorpseInfo
                            {
                                objectGuid = corpse.Guid.Full,
                                name = corpse.Name,
                                longDesc = corpse.LongDesc,
                                killerId = corpse.KillerId,
                                position = SerializePosition(corpse.Location),
                                positionObj = corpse.Location,
                                timeToRotSeconds = corpse.TimeToRot,
                                creationTimestamp = corpse.CreationTimestamp,
                                isLoaded = true
                            });
                            loadedGuids.Add(corpse.Guid.Full);
                        }
                        finally
                        {
                            corpse.BiotaDatabaseLock.ExitReadLock();
                        }
                    }
                    catch
                    {
                        // Corpse may be mid-mutation; skip this frame.
                    }
                }
            }

            // Database lookup for unloaded/inactive corpses
            try
            {
                using (var shard = new ShardDbContext())
                {
                    var dbCorpsesQuery = shard.Biota.AsNoTracking()
                        .Include(b => b.BiotaPropertiesString)
                        .Include(b => b.BiotaPropertiesPosition)
                        .Include(b => b.BiotaPropertiesFloat)
                        .Include(b => b.BiotaPropertiesInt)
                        .Include(b => b.BiotaPropertiesIID)
                        .Where(biota => biota.WeenieType == (int)WeenieType.Corpse
                            && biota.BiotaPropertiesIID.Any(victim => victim.Type == (ushort)PropertyInstanceId.Victim && victim.Value == victimGuid))
                        .AsSplitQuery();

                    var dbCorpses = dbCorpsesQuery.ToList();

                    foreach (var biota in dbCorpses)
                    {
                        if (loadedGuids.Contains(biota.Id))
                            continue;

                        // Load properties from memory
                        // Name
                        var name = biota.BiotaPropertiesString
                            .Where(s => s.Type == (ushort)PropertyString.Name)
                            .Select(s => s.Value)
                            .FirstOrDefault() ?? "Corpse";

                        // Location
                        var dbPos = biota.BiotaPropertiesPosition
                            .FirstOrDefault(p => p.PositionType == (ushort)PositionType.Location);

                        object serializedPos = null;
                        Position locObj = null;
                        if (dbPos != null)
                        {
                            locObj = new Position(dbPos.ObjCellId, dbPos.OriginX, dbPos.OriginY, dbPos.OriginZ, dbPos.AnglesX, dbPos.AnglesY, dbPos.AnglesZ, dbPos.AnglesW, false, dbPos.VariationId);
                            serializedPos = SerializePosition(locObj);
                        }

                        // Time to rot
                        var timeToRot = biota.BiotaPropertiesFloat
                            .Where(f => f.Type == (ushort)PropertyFloat.TimeToRot)
                            .Select(f => f.Value)
                            .FirstOrDefault();

                        // Creation timestamp
                        var creationTimestamp = biota.BiotaPropertiesInt
                            .Where(i => i.Type == (ushort)PropertyInt.CreationTimestamp)
                            .Select(i => i.Value)
                            .FirstOrDefault();

                        // Killer Id (optional)
                        var killerId = biota.BiotaPropertiesIID
                            .Where(i => i.Type == (ushort)PropertyInstanceId.Killer)
                            .Select(i => i.Value)
                            .FirstOrDefault();

                        result.Add(new PlayerCorpseInfo
                        {
                            objectGuid = biota.Id,
                            name = name,
                            longDesc = null,
                            killerId = killerId == 0 ? (uint?)null : killerId,
                            position = serializedPos,
                            positionObj = locObj,
                            timeToRotSeconds = timeToRot,
                            creationTimestamp = creationTimestamp == 0 ? (int?)null : creationTimestamp,
                            isLoaded = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Failed to fetch offline corpses from database.", ex);
            }

            return result;
        }
    }

    public class PlayerCorpseInfo
    {
        public uint objectGuid { get; set; }
        public string name { get; set; }
        public string longDesc { get; set; }
        public uint? killerId { get; set; }
        public object position { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public Position positionObj { get; set; }

        public double? timeToRotSeconds { get; set; }
        public int? creationTimestamp { get; set; }
        public bool isLoaded { get; set; }
    }
}
