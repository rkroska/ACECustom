using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ACE.Server.Managers
{
    public sealed class NetherFractureZone
    {
        [JsonRequired] public double Corrosion { get; init; }
        [JsonRequired] public double Corruption { get; init; }
        [JsonRequired] public double Curse { get; init; }
        [JsonRequired] public double Streak { get; init; }
        [JsonRequired] public double Fracture { get; init; }
        [JsonRequired] public double Radius { get; init; }
    }

    public sealed class NetherFractureConfiguration
    {
        [JsonRequired] public int SchemaVersion { get; init; }
        [JsonRequired] public bool Enabled { get; init; }
        [JsonRequired] public Dictionary<string, NetherFractureZone> Zones { get; init; }

        public static NetherFractureConfiguration Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            RejectDuplicates(document.RootElement);
            var result = JsonSerializer.Deserialize<NetherFractureConfiguration>(json,
                new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow });
            if (result == null || result.SchemaVersion != 1 || result.Zones == null || result.Enabled && result.Zones.Count == 0)
                throw new InvalidDataException("Invalid Nether Fracture configuration or schema version.");
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in result.Zones)
            {
                var parts = pair.Key.Split(':', 2);
                var z = pair.Value;
                if (parts.Length != 2 || !ushort.TryParse(parts[0], out var variation) || variation < 1 || parts[0] != variation.ToString(CultureInfo.InvariantCulture) || string.IsNullOrWhiteSpace(parts[1]) || parts[1] != parts[1].Trim() || !keys.Add(pair.Key) || z == null)
                    throw new InvalidDataException("Invalid or duplicate Nether Fracture named zone.");
                if (new[] { z.Corrosion, z.Corruption, z.Curse, z.Streak }.Any(n => !double.IsFinite(n) || n < 0 || n > 5)
                    || !double.IsFinite(z.Fracture) || z.Fracture < 0 || z.Fracture > 10
                    || !double.IsFinite(z.Radius) || z.Radius < 2 || z.Radius > 8)
                    throw new InvalidDataException("Nether Fracture tuning is out of range.");
            }
            return new NetherFractureConfiguration { SchemaVersion = 1, Enabled = result.Enabled,
                Zones = new Dictionary<string, NetherFractureZone>(result.Zones, StringComparer.OrdinalIgnoreCase) };
        }

        private static void RejectDuplicates(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object) return;
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in element.EnumerateObject())
            {
                if (!keys.Add(p.Name)) throw new InvalidDataException("Duplicate Nether Fracture configuration key.");
                RejectDuplicates(p.Value);
            }
        }
    }

    // A failed reload NEVER replaces a valid snapshot with native/untuned coefficients.
    // Initial configuration is mandatory and validated before ModManager or database startup.
    public sealed class NetherFractureConfigurationStore
    {
        private readonly object gate = new object();
        private NetherFractureConfiguration current;
        public NetherFractureConfiguration Current => System.Threading.Volatile.Read(ref current);
        public NetherFractureConfigurationStore(string json) { current = NetherFractureConfiguration.Parse(json); }
        public bool TryReload(string json, out string error)
        {
            try
            {
                var candidate = NetherFractureConfiguration.Parse(json);
                lock (gate) System.Threading.Volatile.Write(ref current, candidate);
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is JsonException || ex is InvalidDataException || ex is ArgumentException)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    public static class NetherFractureMath
    {
        public const double BankShare = .20;
        public const double GroupDotCap = 2;
        public const double BankSeconds = 8;
        public const double CooldownSeconds = 8;
        public const int TargetCap = 4;
        public static double GroupFactor(int casters, double total, double strongest) =>
            casters <= 1 || total <= 0 ? 1 : Math.Min(1, GroupDotCap * strongest / total);

        // Native history rounds contributions separately. Reconcile to actual capped health loss,
        // including mixed eligible/ineligible casters, without giving one player's damage to another.
        public static uint[] Allocate(uint actual, IReadOnlyList<(uint Caster, ulong Damage)> contributions)
        {
            var result = new uint[contributions.Count];
            var total = contributions.Sum(c => (double)c.Damage);
            if (actual == 0 || total <= 0) return result;
            var exact = contributions.Select(c => actual * (double)c.Damage / total).ToArray();
            for (var i = 0; i < result.Length; i++) result[i] = (uint)Math.Floor(exact[i]);
            var left = actual - (uint)result.Sum(n => (long)n);
            foreach (var i in Enumerable.Range(0, result.Length).OrderByDescending(i => exact[i] - result[i]).ThenBy(i => contributions[i].Caster).Take((int)left)) result[i]++;
            return result;
        }
    }
}
