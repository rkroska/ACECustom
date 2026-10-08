using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Server.Services;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Which dye palettes look like which colour ON A GIVEN WEAPON MODEL. A weapon's textures use only about a hundred of
    /// a palette's 2048 colours, so the same palette can read red on one model and brown on another; a "Crimson" dye has
    /// to be judged against the model it lands on. For a setup this reads the colour indices its palette-indexed textures
    /// use (with the dyeable-twin swaps applied, since that is what a dyed weapon draws), takes each vibrant palette's
    /// usage-weighted mean colour over those indices and sorts it into a family. Cached per setup: DAT data never changes.
    ///
    /// Measured on loot weapons 2026-09-29: a uniform roll is about 40% orange/red and 2% blue, which is why a plain roll
    /// feels samey; family dyes exist so every colour can actually be had.
    /// </summary>
    public static class ForgeDyes
    {
        /// <summary>Colour families. The numbers are stored on dye items (PropertyInt.ForgeToolArg): never renumber.</summary>
        public enum Family
        {
            Any = 0,
            Red = 1,
            Orange = 2,
            Yellow = 3,
            Green = 4,
            Teal = 5,
            Blue = 6,
            Violet = 7,
            Pink = 8,
            Grey = 9,
        }

        /// <summary>ASCII display name.</summary>
        public static string Name(Family family) => family == Family.Any ? "any colour" : family.ToString().ToLowerInvariant();

        private static readonly ConcurrentDictionary<uint, Dictionary<Family, List<uint>>> cache = new();

        /// <summary>The family of a hue / saturation / lightness (h in degrees, s and l 0-1).</summary>
        public static Family Classify(double h, double s, double l)
        {
            if (s < 0.15)
                return Family.Grey;
            if (h < 15 || h >= 345) return Family.Red;
            if (h < 45) return Family.Orange;
            if (h < 70) return Family.Yellow;
            if (h < 165) return Family.Green;
            if (h < 200) return Family.Teal;
            if (h < 260) return Family.Blue;
            if (h < 300) return Family.Violet;
            return Family.Pink;
        }

        /// <summary>RGB (0-1) to hue (degrees), saturation and lightness.</summary>
        public static (double H, double S, double L) ToHsl(double r, double g, double b)
        {
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min, l = (max + min) / 2;
            if (d < 1e-6)
                return (0, 0, l);
            var s = d / (1 - Math.Abs(2 * l - 1));
            var h = max == r ? 60 * (((g - b) / d) % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
            return (h < 0 ? h + 360 : h, s, l);
        }

        /// <summary>Every vibrant palette, by the colour family it shows on this model.</summary>
        public static Dictionary<Family, List<uint>> ForSetup(uint setupId) => cache.GetOrAdd(setupId, Build);

        /// <summary>
        /// A random palette of <paramref name="family"/> for this model (Any = the whole vibrant pool), or null when the
        /// model shows no palette in that family. <paramref name="roll"/> is uniform in [0, 1).
        /// </summary>
        public static uint? Roll(uint setupId, Family family, double roll)
        {
            List<uint> pool;
            if (family == Family.Any)
                pool = PetMutationService.GetVibrantPalettePool().Select(p => p.PaletteId).ToList();
            else if (!ForSetup(setupId).TryGetValue(family, out pool))
                return null;
            if (pool.Count == 0)
                return null;
            return pool[Math.Max(0, Math.Min(pool.Count - 1, (int)Math.Floor(roll * pool.Count)))];
        }

        private static readonly ConcurrentDictionary<string, Dictionary<Family, List<uint>>> garmentCache = new();

        /// <summary>
        /// The palette colours a worn garment recolours: every index inside the ranges of the ClothingBase colour
        /// option it is drawn with. Empty when the piece has no ClothingBase or no colour option (it cannot be dyed).
        /// </summary>
        public static Dictionary<int, int> GarmentIndices(WorldObject wo)
        {
            var used = new Dictionary<int, int>();
            if (wo?.ClothingBase == null || !DatManager.PortalDat.TryReadClothingTable(wo.ClothingBase.Value, out ClothingTable table) || table.ClothingSubPalEffects.Count == 0)
                return used;
            var key = (uint)(wo.PaletteTemplate ?? 0);
            var option = table.ClothingSubPalEffects.TryGetValue(key, out var o) ? o : table.ClothingSubPalEffects[table.ClothingSubPalEffects.Keys.ElementAt(0)];
            foreach (var sub in option.CloSubPalettes)
                foreach (var range in sub.Ranges)
                    for (var i = (int)range.Offset; i < range.Offset + range.NumColors && i < 2048; i++)
                        used[i] = 1;
            return used;
        }

        /// <summary>
        /// As <see cref="Roll"/>, for armour and clothing: the family is judged over the colours the pieces recolour,
        /// taken together, so one palette reads as that colour across a whole suit.
        /// </summary>
        public static uint? RollGarments(IReadOnlyList<WorldObject> pieces, Family family, double roll)
        {
            List<uint> pool;
            if (family == Family.Any)
                pool = PetMutationService.GetVibrantPalettePool().Select(p => p.PaletteId).ToList();
            else
            {
                var key = string.Join(";", pieces.Select(p => $"{p.ClothingBase ?? 0}:{p.PaletteTemplate ?? 0}").Distinct().OrderBy(k => k));
                var families = garmentCache.GetOrAdd(key, _ =>
                {
                    var used = new Dictionary<int, int>();
                    foreach (var piece in pieces)
                        foreach (var index in GarmentIndices(piece).Keys)
                            used[index] = used.TryGetValue(index, out var n) ? n + 1 : 1;
                    return Build(used);
                });
                if (!families.TryGetValue(family, out pool))
                    return null;
            }
            if (pool.Count == 0)
                return null;
            return pool[Math.Max(0, Math.Min(pool.Count - 1, (int)Math.Floor(roll * pool.Count)))];
        }

        private static Dictionary<Family, List<uint>> Build(uint setupId) => Build(UsedIndices(setupId));

        private static Dictionary<Family, List<uint>> Build(Dictionary<int, int> used)
        {
            var result = new Dictionary<Family, List<uint>>();
            if (used.Count == 0)
                return result;
            long total = used.Values.Sum(v => (long)v);

            foreach (var dto in PetMutationService.GetVibrantPalettePool())
            {
                var palette = DatManager.PortalDat.ReadFromDat<Palette>(dto.PaletteId);
                if (palette?.Colors == null)
                    continue;
                double r = 0, g = 0, b = 0;
                foreach (var (index, count) in used)
                {
                    var c = index < palette.Colors.Count ? palette.Colors[index] : 0;
                    r += ((c >> 16) & 255) * (double)count;
                    g += ((c >> 8) & 255) * (double)count;
                    b += (c & 255) * (double)count;
                }
                var (h, s, l) = ToHsl(r / (total * 255.0), g / (total * 255.0), b / (total * 255.0));
                var family = Classify(h, s, l);
                if (!result.TryGetValue(family, out var list))
                    result[family] = list = new List<uint>();
                list.Add(dto.PaletteId);
            }
            return result;
        }

        /// <summary>Colour index -> pixel count over the model's palette-indexed textures, as a dyed weapon draws them.</summary>
        private static Dictionary<int, int> UsedIndices(uint setupId)
        {
            var used = new Dictionary<int, int>();
            try
            {
                var setup = DatManager.PortalDat.ReadFromDat<SetupModel>(setupId);
                if (setup?.Parts == null)
                    return used;
                var swaps = WorldObject.GetDyeTwinSwaps(setupId).ToDictionary(s => (s.Part, s.Old), s => s.New);
                var seen = new HashSet<uint>();
                for (var i = 0; i < setup.Parts.Count && i <= byte.MaxValue; i++)
                {
                    var gfx = DatManager.PortalDat.ReadFromDat<GfxObj>(setup.Parts[i]);
                    if (gfx?.Surfaces == null)
                        continue;
                    foreach (var surfaceId in gfx.Surfaces)
                    {
                        var textureId = DatManager.PortalDat.ReadFromDat<Surface>(surfaceId)?.OrigTextureId ?? 0;
                        if (swaps.TryGetValue(((byte)i, textureId), out var twin))
                            textureId = twin;
                        if (textureId == 0 || !seen.Add(textureId))
                            continue;
                        var images = DatManager.PortalDat.ReadFromDat<SurfaceTexture>(textureId)?.Textures ?? new List<uint>();
                        var texture = images.Select(id => DatManager.PortalDat.ReadFromDat<Texture>(id))
                            .FirstOrDefault(t => t != null && (t.Format == SurfacePixelFormat.PFID_INDEX16 || t.Format == SurfacePixelFormat.PFID_P8));
                        if (texture?.SourceData == null)
                            continue;
                        var bytesPerPixel = texture.Format == SurfacePixelFormat.PFID_P8 ? 1 : 2;
                        for (var o = 0; o + bytesPerPixel <= texture.SourceData.Length; o += bytesPerPixel)
                        {
                            var index = bytesPerPixel == 1 ? texture.SourceData[o] : BitConverter.ToUInt16(texture.SourceData, o);
                            used[index] = used.TryGetValue(index, out var n) ? n + 1 : 1;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // an unreadable model simply offers no family dyes; the any-colour dye still works
                used.Clear();
            }
            return used;
        }
    }
}
