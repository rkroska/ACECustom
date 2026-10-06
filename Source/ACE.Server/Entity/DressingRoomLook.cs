using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ACE.Server.Entity
{
    /// <summary>
    /// One sacrificed piece of a Dressing Room look: everything Creature.CalculateObjDesc reads off a worn item to draw
    /// it, plus what it was (for staff, should a look ever need restoring by hand). The item itself no longer exists.
    /// </summary>
    public sealed class DressingRoomPiece
    {
        /// <summary>Weenie class id of the sacrificed item.</summary>
        [JsonPropertyName("w")] public uint Wcid { get; set; }

        /// <summary>Its name when it was sacrificed.</summary>
        [JsonPropertyName("n")] public string Name { get; set; }

        /// <summary>Its object guid when it was sacrificed (it is gone; this only ties the look to the log line).</summary>
        [JsonPropertyName("g")] public uint Guid { get; set; }

        /// <summary>EquipMask it was worn in. Decides which real gear it hides and which pieces it replaces.</summary>
        [JsonPropertyName("l")] public uint Location { get; set; }

        /// <summary>ItemType (Armor or Clothing), which picks the layering group as it does for a worn item.</summary>
        [JsonPropertyName("t")] public uint ItemType { get; set; }

        /// <summary>Clothing table id (0x10......).</summary>
        [JsonPropertyName("c")] public uint ClothingBase { get; set; }

        [JsonPropertyName("p")] public int? PaletteTemplate { get; set; }

        [JsonPropertyName("s")] public double? Shade { get; set; }

        /// <summary>CoverageMask from PropertyInt.ClothingPriority: the sort key for clothing.</summary>
        [JsonPropertyName("cp")] public uint? ClothingPriority { get; set; }

        /// <summary>CoverageMask the item reported as WorldObject.VisualClothingPriority: the sort key for armour.</summary>
        [JsonPropertyName("vp")] public uint? VisualPriority { get; set; }

        [JsonPropertyName("tl")] public bool? TopLayer { get; set; }

        /// <summary>Unix time (seconds) it was locked in.</summary>
        [JsonPropertyName("at")] public long LockedAt { get; set; }

        /// <summary>Pyreals paid for this piece.</summary>
        [JsonPropertyName("f")] public long Fee { get; set; }
    }

    /// <summary>
    /// A character's Dressing Room look, as stored in PropertyString.DressingRoomLook. Pure data and rules, no game
    /// objects, so every rule here is unit tested (DressingRoomLookTests). Treat an instance as read-only once it has been
    /// handed out: Player caches the parsed look and the draw code reads it from other threads. Changes go through
    /// <see cref="WithLocked"/> and <see cref="WithoutPieces"/>, which return a new look.
    /// </summary>
    public sealed class DressingRoomLook
    {
        public const int CurrentVersion = 1;

        /// <summary>
        /// The EquipMask bits a look can occupy: the nine clothing slots, the six armour slots and the cloak. This is the
        /// same set Creature.CalculateObjDesc draws from (EquipMask.Clothing | Armor | Cloak) without the 0x80000000
        /// flag bit of EquipMask.Clothing, which is not a slot. Weapons, shields and jewellery are never part of a look.
        /// </summary>
        public const uint SlotMask = 0x08007FFF;

        /// <summary>A stored look with more pieces than there are slots is corrupt; it is not drawn.</summary>
        public const int MaxPieces = 16;

        [JsonPropertyName("v")] public int Version { get; set; } = CurrentVersion;

        [JsonPropertyName("pieces")] public List<DressingRoomPiece> Pieces { get; set; } = new();

        /// <summary>
        /// How many times each wear slot has been locked in, keyed by the slot's EquipMask bit in hex. It only ever
        /// grows - removing a look does not reset it - and it sets the fee for the next piece worn in that slot.
        /// </summary>
        [JsonPropertyName("locks")] public Dictionary<string, int> Locks { get; set; } = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>The slot bits of a wielded location, lowest first.</summary>
        public static IEnumerable<uint> SlotBits(uint location)
        {
            var bits = location & SlotMask;
            for (var bit = 1u; bit != 0 && bit <= bits; bit <<= 1)
                if ((bits & bit) != 0)
                    yield return bit;
        }

        public static bool Overlaps(uint locationA, uint locationB) => (locationA & locationB & SlotMask) != 0;

        private static string LockKey(uint bit) => bit.ToString("X", CultureInfo.InvariantCulture);

        /// <summary>
        /// Reads a stored look. Never throws: text that is not a usable look gives null, and the character is then drawn
        /// in their real gear exactly as if they had no look.
        /// </summary>
        public static DressingRoomLook Parse(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
                return null;
            try
            {
                var look = JsonSerializer.Deserialize<DressingRoomLook>(stored, JsonOptions);
                if (look == null || look.Version != CurrentVersion)
                    return null;
                look.Pieces ??= new();
                look.Locks ??= new();
                if (look.Pieces.Count > MaxPieces)
                    return null;
                foreach (var piece in look.Pieces)
                    if (piece == null || piece.ClothingBase == 0 || (piece.Location & SlotMask) == 0)
                        return null;
                // two saved pieces can never share a slot: WithLocked evicts on overlap
                for (var i = 0; i < look.Pieces.Count; i++)
                    for (var j = i + 1; j < look.Pieces.Count; j++)
                        if (Overlaps(look.Pieces[i].Location, look.Pieces[j].Location))
                            return null;
                return look;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);

        /// <summary>How many times the most-locked slot under <paramref name="location"/> has been locked in before.</summary>
        public int PriorLocks(uint location)
        {
            var most = 0;
            foreach (var bit in SlotBits(location))
                if (Locks.TryGetValue(LockKey(bit), out var count) && count > most)
                    most = count;
            return most;
        }

        /// <summary>
        /// The fee for one piece: <paramref name="baseFee"/> for a slot never locked before, multiplied by
        /// <paramref name="growth"/> for every earlier lock-in of that slot, and never above <paramref name="cap"/>
        /// (a cap of 0 or less means no cap). A base of 0 or less makes it free.
        /// </summary>
        public static long Fee(int priorLocks, long baseFee, double growth, long cap)
        {
            if (baseFee <= 0)
                return 0;
            if (double.IsNaN(growth) || growth < 1.0)
                growth = 1.0;
            var limit = FeeLimit(cap);
            var fee = baseFee * Math.Pow(growth, Math.Max(0, priorLocks));
            if (double.IsNaN(fee) || fee >= limit)
                return (long)limit;
            return Math.Max(baseFee, (long)Math.Round(fee));
        }

        /// <summary>
        /// The most <see cref="Fee"/> can return: the cap, or with no cap a fixed ceiling far below long.MaxValue, so the
        /// fee can be worked out in a double and cast safely.
        /// </summary>
        private static double FeeLimit(long cap)
        {
            const double ceiling = 1e18;
            return cap > 0 ? Math.Min(cap, ceiling) : ceiling;
        }

        /// <summary>
        /// True when no later lock-in of the slot can cost more than this one: the fee is free, never grows, or has
        /// reached the most it can be. Two neighbouring fees being equal does not show this - a growth just above 1
        /// rounds to the same fee for a while and then rises.
        /// </summary>
        public static bool FeeIsFinal(int priorLocks, long baseFee, double growth, long cap)
        {
            if (baseFee <= 0 || double.IsNaN(growth) || growth <= 1.0)
                return true;
            return Fee(priorLocks, baseFee, growth, cap) >= (long)FeeLimit(cap);
        }

        /// <summary>The saved pieces a newly locked piece worn in <paramref name="location"/> would replace.</summary>
        public List<DressingRoomPiece> ReplacedBy(uint location) => Pieces.Where(p => Overlaps(p.Location, location)).ToList();

        /// <summary>
        /// A new look with <paramref name="locked"/> added. A new piece replaces every saved piece it shares a slot with,
        /// whole: a breastplate locked over a saved hauberk removes the hauberk, sleeves included. Each slot a new piece
        /// occupies has its lock count raised by one.
        /// </summary>
        public DressingRoomLook WithLocked(IReadOnlyCollection<DressingRoomPiece> locked)
        {
            var next = new DressingRoomLook
            {
                Pieces = Pieces.Where(p => !locked.Any(n => Overlaps(p.Location, n.Location))).ToList(),
                Locks = new Dictionary<string, int>(Locks),
            };
            foreach (var piece in locked)
            {
                next.Pieces.Add(piece);
                foreach (var bit in SlotBits(piece.Location))
                {
                    var key = LockKey(bit);
                    next.Locks[key] = (next.Locks.TryGetValue(key, out var count) ? count : 0) + 1;
                }
            }
            return next;
        }

        /// <summary>A new look with no pieces. The lock counts are kept, so removing a look never makes the next one cheaper.</summary>
        public DressingRoomLook WithoutPieces() => new() { Locks = new Dictionary<string, int>(Locks) };
    }
}
