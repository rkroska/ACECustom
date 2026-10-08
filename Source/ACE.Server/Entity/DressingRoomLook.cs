using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ACE.Server.Entity
{
    /// <summary>
    /// One piece of a Dressing Room look: a copy of everything Creature.CalculateObjDesc reads off a worn item to draw
    /// it, plus what it was copied from. The item itself stays with the player (pieces locked in before 2026-10-07 were
    /// destroyed instead).
    /// </summary>
    public sealed class DressingRoomPiece
    {
        /// <summary>Weenie class id of the item the look was copied from.</summary>
        [JsonPropertyName("w")] public uint Wcid { get; set; }

        /// <summary>Its name when it was locked in.</summary>
        [JsonPropertyName("n")] public string Name { get; set; }

        /// <summary>Its object guid when it was locked in (this only ties the look to the log line).</summary>
        [JsonPropertyName("g")] public uint Guid { get; set; }

        /// <summary>EquipMask it was worn in. Names the slots in chat, and applies the Show Helm / Show Cloak options.</summary>
        [JsonPropertyName("l")] public uint Location { get; set; }

        /// <summary>ItemType (Armor or Clothing), which picks the layering group as it does for a worn item.</summary>
        [JsonPropertyName("t")] public uint ItemType { get; set; }

        /// <summary>Clothing table id (0x10......).</summary>
        [JsonPropertyName("c")] public uint ClothingBase { get; set; }

        [JsonPropertyName("p")] public int? PaletteTemplate { get; set; }

        [JsonPropertyName("s")] public double? Shade { get; set; }

        /// <summary>CoverageMask from PropertyInt.ClothingPriority: the sort key for clothing, and - as in the game's own
        /// equip rule - what decides which real gear the piece hides, which saved pieces it replaces, and its fee.</summary>
        [JsonPropertyName("cp")] public uint? ClothingPriority { get; set; }

        /// <summary>CoverageMask the item reported as WorldObject.VisualClothingPriority: the sort key for armour.</summary>
        [JsonPropertyName("vp")] public uint? VisualPriority { get; set; }

        [JsonPropertyName("tl")] public bool? TopLayer { get; set; }

        /// <summary>
        /// The blacksmithing dye the piece wore when it was locked in (a 0x04 palette id), so the look keeps the colour
        /// the player saw. Only a dye that had been kept, never one still being tried on. Absent for an undyed piece and
        /// for every piece locked in before 2026-10-07.
        /// </summary>
        [JsonPropertyName("d")] public int? ForgeDye { get; set; }

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

        /// <summary>
        /// A stored look with more pieces than this is corrupt; it is not drawn. Saved pieces never share a body area, and
        /// there are 18 areas at most (the 17 CoverageMask bits and the cloak), so no real look can exceed it.
        /// </summary>
        public const int MaxPieces = 18;

        [JsonPropertyName("v")] public int Version { get; set; } = CurrentVersion;

        [JsonPropertyName("pieces")] public List<DressingRoomPiece> Pieces { get; set; } = new();

        /// <summary>
        /// How many times each body area has been locked in, keyed by its CoverageMask bit in hex (the bits of a piece's
        /// ClothingPriority). It only ever grows - removing a look does not reset it - and it sets the fee for the next
        /// piece covering that area.
        /// </summary>
        [JsonPropertyName("cover")] public Dictionary<string, int> Cover { get; set; }

        /// <summary>
        /// Counts as first stored (2026-10-05 to 07): keyed by EquipMask bit. Only read, to build <see cref="Cover"/> for
        /// a record that has none, and never written again. Those keys could not price fairly because clothes worn
        /// together share EquipMask bits - a shirt and trousers both claim the waist, trousers and boots both claim the
        /// lower leg - so one outfit counted the shared slot twice.
        /// </summary>
        [JsonPropertyName("locks")] public Dictionary<string, int> LegacyLocks { get; set; }

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

        private static IEnumerable<uint> Bits(uint mask)
        {
            for (var i = 0; i < 32; i++)
                if ((mask & (1u << i)) != 0)
                    yield return 1u << i;
        }

        /// <summary>
        /// True when two pieces could not be worn together, by the game's own rule for armour and clothing
        /// (Creature.GetEquippedItems): their coverage (ClothingPriority) overlaps. It is NOT their EquipMask - pieces worn
        /// together share EquipMask bits all the time (shirt and trousers at the waist, trousers and boots at the lower
        /// leg). Only when a piece has no coverage at all does its wielded location decide.
        /// </summary>
        public static bool Conflicts(uint coverageA, uint locationA, uint coverageB, uint locationB)
            => coverageA != 0 && coverageB != 0
                ? (coverageA & coverageB) != 0
                : (locationA & locationB & SlotMask) != 0;

        public static bool Conflicts(DressingRoomPiece a, DressingRoomPiece b)
            => Conflicts(a.ClothingPriority ?? 0, a.Location, b.ClothingPriority ?? 0, b.Location);

        private static string Key(uint bit) => bit.ToString("X", CultureInfo.InvariantCulture);

        /// <summary>
        /// Reads a stored look. Never throws: text that is not a usable look gives null, and the character is then drawn
        /// in their real gear exactly as if they had no look.
        ///
        /// It checks only what the draw code needs from each piece. It deliberately does not judge whether the pieces
        /// "fit together": a too-strict check of exactly that kind rejected every look containing a shirt and trousers
        /// on 2026-10-07, after the gear and the fee had been taken.
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
                if (look.Pieces.Count > MaxPieces)
                    return null;
                foreach (var piece in look.Pieces)
                    if (piece == null || piece.ClothingBase == 0 || (piece.Location & SlotMask) == 0)
                        return null;

                look.Cover ??= CoverFromLegacy(look.Pieces, look.LegacyLocks);
                look.LegacyLocks = null;
                return look;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Builds coverage counts for a record stored with EquipMask-keyed counts. Each saved piece was locked as many
        /// times as its LEAST-counted slot (the slots it shared with another piece of the same outfit were counted more
        /// than once), and never fewer than once since it is saved. Counts for pieces no longer in the look cannot be
        /// recovered and start again from nothing.
        /// </summary>
        private static Dictionary<string, int> CoverFromLegacy(List<DressingRoomPiece> pieces, Dictionary<string, int> legacy)
        {
            var cover = new Dictionary<string, int>();
            foreach (var piece in pieces)
            {
                var times = int.MaxValue;
                foreach (var slot in SlotBits(piece.Location))
                    times = Math.Min(times, legacy != null && legacy.TryGetValue(Key(slot), out var count) ? count : 1);
                if (times == int.MaxValue || times < 1)
                    times = 1;
                foreach (var bit in Bits(piece.ClothingPriority ?? 0))
                    cover[Key(bit)] = Math.Max(times, cover.TryGetValue(Key(bit), out var had) ? had : 0);
            }
            return cover;
        }

        public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);

        /// <summary>How many times the most-locked body area under <paramref name="coverage"/> (a piece's ClothingPriority) has been locked in before.</summary>
        public int PriorLocks(uint coverage)
        {
            var most = 0;
            if (Cover == null)
                return most;
            foreach (var bit in Bits(coverage))
                if (Cover.TryGetValue(Key(bit), out var count) && count > most)
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

        /// <summary>The saved pieces that newly locked <paramref name="piece"/> would replace.</summary>
        public List<DressingRoomPiece> ReplacedBy(DressingRoomPiece piece) => Pieces.Where(p => Conflicts(p, piece)).ToList();

        /// <summary>
        /// A new look with <paramref name="locked"/> added. A new piece replaces every saved piece it conflicts with,
        /// whole: a breastplate locked over a saved hauberk removes the hauberk, sleeves included. Each body area the new
        /// pieces cover has its count raised by one - once per lock-in, however many of the pieces cover it.
        /// </summary>
        public DressingRoomLook WithLocked(IReadOnlyCollection<DressingRoomPiece> locked)
        {
            var next = new DressingRoomLook
            {
                Pieces = Pieces.Where(p => !locked.Any(n => Conflicts(p, n))).ToList(),
                Cover = Cover == null ? new Dictionary<string, int>() : new Dictionary<string, int>(Cover),
            };
            uint covered = 0;
            foreach (var piece in locked)
            {
                next.Pieces.Add(piece);
                covered |= piece.ClothingPriority ?? 0;
            }
            foreach (var bit in Bits(covered))
                next.Cover[Key(bit)] = (next.Cover.TryGetValue(Key(bit), out var count) ? count : 0) + 1;
            return next;
        }

        /// <summary>A new look with no pieces. The counts are kept, so removing a look never makes the next one cheaper.</summary>
        public DressingRoomLook WithoutPieces()
            => new() { Cover = Cover == null ? new Dictionary<string, int>() : new Dictionary<string, int>(Cover) };
    }
}
