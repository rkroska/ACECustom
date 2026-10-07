using System;
using System.Collections.Generic;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Which kinds of armour and clothing each player body can show, worked out from the client's own clothing tables.
    ///
    /// Some bodies have no model for some gear: a Lugian shows only about a third of helms and boots (found in game
    /// 2026-10-06), an Umbraen shows almost nothing below the waist, and an
    /// attendant refuses to take a piece that would not show. The Dressing Room guide page lists this so a player knows
    /// before they travel. Nothing here is a hand-kept list - it is counted from the DAT. An area with a count of zero
    /// is one where no piece changes that body at all, which is exactly when the attendant's own test
    /// (Creature.DressingRoomPieceDraws) refuses a piece that only covers that area.
    /// </summary>
    public static class DressingRoomBodies
    {
        /// <summary>A part of the body, as the human model numbers its parts. A piece belongs to every area it changes on a human.</summary>
        public sealed record Area(string Key, string Label, uint[] HumanParts);

        public static readonly Area[] Areas =
        {
            new("head", "Head", new uint[] { 16 }),
            new("chest", "Chest", new uint[] { 9 }),
            new("abdomen", "Abdomen", new uint[] { 0 }),
            new("upperArms", "Upper arms", new uint[] { 10, 13 }),
            new("lowerArms", "Lower arms", new uint[] { 11, 14 }),
            new("hands", "Hands", new uint[] { 12, 15 }),
            new("upperLegs", "Upper legs", new uint[] { 1, 5 }),
            new("lowerLegs", "Lower legs", new uint[] { 2, 6 }),
            new("feet", "Feet", new uint[] { 3, 4, 7, 8 }),
        };

        /// <summary>The body every piece is measured against: a piece counts if it draws on a human.</summary>
        private const uint ReferenceSetup = 0x02000001;

        public sealed record AreaCount(string Key, string Label, int Shown, int Total);

        public sealed record Body(string Heritage, string Gender, uint SetupId, IReadOnlyList<AreaCount> Areas);

        private static readonly Lazy<IReadOnlyList<Body>> all = new(Build);

        /// <summary>Every heritage and gender a player can be, with how many pieces show per body area. DAT data never changes at runtime, so this is built once.</summary>
        public static IReadOnlyList<Body> All => all.Value;

        private static IReadOnlyList<Body> Build()
        {
            // each wearable clothing table: the areas it covers on a human, and the table itself
            var pieces = new List<(ClothingTable Table, bool[] Covers)>();
            foreach (var id in DatManager.PortalDat.AllFiles.Keys.Where(k => (k & 0xFF000000) == 0x10000000))
            {
                if (!DatManager.PortalDat.TryReadClothingTable(id, out var table))
                    continue;
                if (!table.ClothingBaseEffects.TryGetValue(ReferenceSetup, out var human) || human.CloObjectEffects.Count == 0)
                    continue;
                var covers = Areas.Select(a => human.CloObjectEffects.Any(e => a.HumanParts.Contains(e.Index))).ToArray();
                if (covers.Any(c => c))
                    pieces.Add((table, covers));
            }

            var bodies = new List<Body>();
            foreach (var heritage in DatManager.PortalDat.CharGen.HeritageGroups.OrderBy(h => h.Key))
            {
                foreach (var gender in heritage.Value.Genders.OrderBy(g => g.Key))
                {
                    var setupId = Creature.DressingRoomClothingSetup(gender.Value.SetupID);
                    var shown = new int[Areas.Length];
                    var total = new int[Areas.Length];
                    foreach (var (table, covers) in pieces)
                    {
                        // Player bodies share the human part numbering, so "shows on the head" is the same question for
                        // each: does this body's entry change a head part? Asked per area, not per piece, because a
                        // hooded robe can draw on a body's torso and still leave its head bare.
                        table.ClothingBaseEffects.TryGetValue(setupId, out var effect);
                        for (var i = 0; i < Areas.Length; i++)
                        {
                            if (!covers[i])
                                continue;
                            total[i]++;
                            if (effect != null && effect.CloObjectEffects.Any(e => Areas[i].HumanParts.Contains(e.Index)))
                                shown[i]++;
                        }
                    }
                    bodies.Add(new Body(heritage.Value.Name, gender.Value.Name, setupId,
                        Areas.Select((a, i) => new AreaCount(a.Key, a.Label, shown[i], total[i])).ToList()));
                }
            }
            return bodies;
        }
    }
}
