using System;
using System.Collections.Generic;

using ACE.Entity;

namespace ACE.Server.Managers.Rifts
{
    /// <summary>
    /// JSON-serializable position for the rift config store. Variation is intentionally NOT stored —
    /// it is supplied at run time (each run gets its own ephemeral negative variation).
    /// </summary>
    public class RiftPos
    {
        public uint Cell { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float QW { get; set; }
        public float QX { get; set; }
        public float QY { get; set; }
        public float QZ { get; set; }

        public Position ToPosition(int? variation)
        {
            return new Position(Cell, X, Y, Z, QX, QY, QZ, QW, false, variation);
        }

        public static RiftPos FromPosition(Position p)
        {
            if (p == null) return null;
            return new RiftPos
            {
                Cell = p.Cell,
                X = p.PositionX, Y = p.PositionY, Z = p.PositionZ,
                QW = p.RotationW, QX = p.RotationX, QY = p.RotationY, QZ = p.RotationZ,
            };
        }
    }

    /// <summary>One curated dungeon in the rift pool. Single-landblock dungeons only (validated on add).</summary>
    public class RiftDungeonEntry
    {
        public ushort Landblock { get; set; }
        public string Name { get; set; }
        /// <summary>Where the player lands when the rift opens.</summary>
        public RiftPos Entry { get; set; }
        /// <summary>Where the guardian spawns at 100% progress (falls back to Entry when unset).</summary>
        public RiftPos Guardian { get; set; }
        /// <summary>Which variation's world-DB instance rows (statics + monster generators) populate the rift
        /// copy. Landblock instances are per-variation in this fork (exact match) — a rift's own negative
        /// variation has no rows, so the instance loads THIS variation's population instead. Null = the
        /// unlayered base rows. Captured from where the admin stands during /rift pool add here.</summary>
        public int? SourceVariation { get; set; }
    }

    /// <summary>Guardian WCID pool for a tier band (inclusive).</summary>
    public class RiftGuardianPool
    {
        public int MinTier { get; set; }
        public int MaxTier { get; set; }
        public List<uint> Wcids { get; set; } = new();
    }

    /// <summary>
    /// Persisted rift configuration (shard DB key <c>rift_config</c>, JSON). All balance knobs live here and
    /// are tunable live via /rift set — no server config properties, no weenie edits.
    /// </summary>
    public class RiftConfig
    {
        public List<RiftDungeonEntry> DungeonPool { get; set; } = new();
        public List<RiftGuardianPool> GuardianPools { get; set; } = new();

        /// <summary>Run length in seconds (default 15:00).</summary>
        public int TimerSeconds { get; set; } = 900;
        /// <summary>Seconds between clear/fail and eviction from the instance.</summary>
        public int GraceSeconds { get; set; } = 60;
        public int MaxActiveRuns { get; set; } = 40;

        /// <summary>Kill points required to summon the guardian: Base + PerTier * tier.</summary>
        public double ProgressBase { get; set; } = 40;
        public double ProgressPerTier { get; set; } = 2;
        /// <summary>Progress points per kill (flat for phase 1).</summary>
        public double ProgressPerKill { get; set; } = 1;

        /// <summary>Monster max health multiplier: HpGrowth ^ tier (geometric, uncapped by design).</summary>
        public double HpGrowth { get; set; } = 1.08;
        /// <summary>Additive DamageRating per tier (15 rating ≈ +15% damage).</summary>
        public double DamageRatingPerTier { get; set; } = 3;
        /// <summary>Extra max health multiplier on the guardian, applied on top of the tier curve.</summary>
        public double GuardianHpMult { get; set; } = 6;
        /// <summary>Extra flat DamageRating on the guardian.</summary>
        public double GuardianDamageRatingBonus { get; set; } = 20;

        /// <summary>Currency reward on clear: floor(Base + PerTier * tier) of CurrencyWcid, straight to inventory.
        /// Wcid 0 disables. 300004 = Enlightened Coin.</summary>
        public uint CurrencyWcid { get; set; } = 300004;
        public double CurrencyBase { get; set; } = 5;
        public double CurrencyPerTier { get; set; } = 1;

        /// <summary>Zone loot stats stamped on the run's ephemeral zone: value = Base + PerTier * tier.
        /// Keys are ZoneStat loot keys (weapon_cantrip_chance, bonus_currency, ...).
        /// (loot_quality_mult / rare_chance_mult defaults removed 2026-08-23 with those stats.)</summary>
        public Dictionary<string, double> LootStatBase { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, double> LootStatPerTier { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public enum RiftRunState
    {
        /// <summary>Filling the progress bar.</summary>
        Active,
        /// <summary>Guardian spawned, timer still running.</summary>
        GuardianUp,
        /// <summary>Guardian killed in time — success. Grace period, then eviction.</summary>
        Cleared,
        /// <summary>Timer expired first. Grace period, then eviction.</summary>
        Failed,
    }

    /// <summary>One live rift run. In-memory only — a server restart discards active runs.</summary>
    public class RiftRun
    {
        public int RunId { get; set; }
        /// <summary>The run's private instance key. Always negative — prestige ignores the entire range.</summary>
        public int Variation { get; set; }
        public int Tier { get; set; }
        public uint OwnerGuid { get; set; }
        public string OwnerName { get; set; }

        public ushort DungeonLb { get; set; }
        public string DungeonName { get; set; }
        public RiftPos Entry { get; set; }
        public RiftPos GuardianPos { get; set; }
        /// <summary>See <see cref="RiftDungeonEntry.SourceVariation"/> — copied at open so the landblock
        /// loader can resolve it without touching config.</summary>
        public int? SourceVariation { get; set; }

        /// <summary>Where (and at which variation) the player is returned when the run ends.</summary>
        public RiftPos ReturnPos { get; set; }
        public int? ReturnVariation { get; set; }

        public double StartTime { get; set; }
        public int DurationSeconds { get; set; }
        public double Progress { get; set; }
        public double ProgressRequired { get; set; }

        public RiftRunState State { get; set; } = RiftRunState.Active;
        public uint GuardianGuid { get; set; }
        /// <summary>Unix time at which the grace period ends and the run is torn down (0 = not ending yet).</summary>
        public double CloseAtTime { get; set; }

        /// <summary>Last whole 10%-step announced (progress chat throttle).</summary>
        public int LastAnnouncedStep { get; set; }
        /// <summary>Index into the time-remaining announcement thresholds already fired.</summary>
        public int NextTimeAnnounceIdx { get; set; }

        /// <summary>Name of the ephemeral ZoneControl runtime zone carrying this run's loot stats.</summary>
        public string ZoneName { get; set; }

        public double EndTime => StartTime + DurationSeconds;
        public int ProgressPercent => ProgressRequired > 0 ? (int)Math.Min(100, Math.Floor(Progress * 100 / ProgressRequired)) : 0;
    }
}
