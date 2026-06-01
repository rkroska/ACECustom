using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

using Biota = ACE.Entity.Models.Biota;

namespace ACE.Server.Controllers
{
    /// <summary>
    /// Web Portal: death/vitae snapshot (biota), live position (online player), and active player corpses (loaded landblocks).
    /// </summary>
    internal static class CharacterPortalStatusHelper
    {
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
                indoors = loc.Indoors
            };
        }

        public static List<object> FindPlayerCorpses(uint victimGuid)
        {
            var result = new List<object>();
            var landblocks = LandblockManager.loadedLandblocks.Values.ToList();

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
                            result.Add(new
                            {
                                objectGuid = corpse.Guid.Full,
                                name = corpse.Name,
                                longDesc = corpse.LongDesc,
                                killerId = corpse.KillerId,
                                position = SerializePosition(corpse.Location),
                                timeToRotSeconds = corpse.TimeToRot,
                                creationTimestamp = corpse.CreationTimestamp
                            });
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

            return result;
        }
    }
}
