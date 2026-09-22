using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ACE.Common;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Database;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;
using log4net;

namespace ACE.Server.Services
{
    public enum CreaturePaletteMode
    {
        ModularClothing = 1,     // Has ClothingBase (0x10...) with ClothingSubPalEffects
        IndexedWholeBody = 2,    // PFID_INDEX16 / PFID_P8 single-texture fauna (Anekshay, Gromnie, Grievver, Bunny, Thrungus)
        ModulatedDiffuseTint = 3,// Direct DXT1/RGB or particle sprite supporting D3D diffuse tint (Eater, Remoran, Zefir)
        Incompatible = 4         // Pure particle clouds, modern shader DXT1 rigs, water golem (skip)
    }

    public class CreaturePaletteProfile
    {
        public uint SetupId { get; set; }
        public uint ClothingBaseId { get; set; }
        public CreaturePaletteMode Mode { get; set; }
        public bool IsSupported => Mode != CreaturePaletteMode.Incompatible;
        public string ModeName => Mode.ToString();
        public string Reason { get; set; }
        public List<uint> ValidPalettePool { get; set; } = new List<uint>();
    }

    public class MasterPaletteDto
    {
        public uint PaletteId { get; set; }
        public string PaletteHex { get; set; }
        public string Name { get; set; }
        public List<string> Swatches { get; set; } = new List<string>();
    }

    public static class PetMutationService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PetMutationService));

        private static readonly ConcurrentDictionary<uint, CreaturePaletteProfile> _profileCache = new ConcurrentDictionary<uint, CreaturePaletteProfile>();
        private static readonly List<uint> _masterVerifiedPalettePool = new List<uint>();
        private static readonly List<MasterPaletteDto> _masterPaletteDtos = new List<MasterPaletteDto>();
        private static readonly List<MasterPaletteDto> _vibrantPaletteDtos = new List<MasterPaletteDto>();
        private static readonly object _initLock = new object();
        private static bool _isInitialized = false;

        public static List<MasterPaletteDto> GetMasterPalettePool()
        {
            if (!_isInitialized) Initialize();
            return _masterPaletteDtos;
        }

        public static List<MasterPaletteDto> GetVibrantPalettePool()
        {
            if (!_isInitialized) Initialize();
            return _vibrantPaletteDtos.Count > 0 ? _vibrantPaletteDtos : _masterPaletteDtos;
        }

        // Known hard-incompatible Setup Model DIDs
        private static readonly HashSet<uint> _blacklistedSetups = new HashSet<uint>
        {
            0x02000BEE, // Acid Elemental
            0x020006A3, // Fire Elemental
            0x02000BEF, // Frost Elemental
            0x020006AC, // Lightning Elemental
            0x02000C54, // Generic Elemental Cloud
            0x0200059A, // Wisp
            0x02001499, // Swarm
            0x02001A2B, // Gurog Minion
            0x02001A2C, // Gurog Henchman
            0x02001A2D, // Gurog Soldier
            0x02001A2E, // Gurog Destroyer
            0x02001B5F, // Gurog Defender
            0x02001B60, // Gurog Slayer
            0x02001B61, // Gurog King / Champion
            0x02001A10, // Fae / Sprite
            0x02001BCA, // Viridian Statue
            0x02000B4C, // Wood Golem
            0x02000219, // Water Golem
            0x020007E6  // Water Golem Subtype
        };

        // Known DXT1/particle entities that support Direct3D hardware diffuse color modulation
        private static readonly HashSet<uint> _diffuseTintSetups = new HashSet<uint>
        {
            0x02001251, // Eater
            0x02001494, // Remoran
            0x0200049A, // Zefir
            0x0200101A, // Margul
            0x020017D6  // Touched
        };

        public static void Initialize()
        {
            if (_isInitialized) return;

            lock (_initLock)
            {
                if (_isInitialized) return;

                try
                {
                    log.Info("Initializing PetMutationService and pre-caching palette compatibility profiles...");

                    _masterVerifiedPalettePool.Clear();
                    _masterPaletteDtos.Clear();
                    _vibrantPaletteDtos.Clear();

                    // Collect master verified palette list (all 0x04... palettes from portal dat)
                    var portalDb = DatManager.PortalDat;
                    if (portalDb != null)
                    {
                        // Cache common palettes in 0x04000000 - 0x04002500 range
                        for (uint p = 0x04000001; p <= 0x04002500; p++)
                        {
                            var pal = portalDb.ReadFromDat<Palette>(p);
                            if (IsUsableCreaturePalette(pal))
                            {
                                _masterVerifiedPalettePool.Add(p);

                                var swatches = new List<string>();
                                int step = Math.Max(1, pal.Colors.Count / 8);
                                for (int i = 0; i < pal.Colors.Count && swatches.Count < 8; i += step)
                                {
                                    uint col = pal.Colors[i];
                                    byte r = (byte)((col >> 16) & 0xFF);
                                    byte g = (byte)((col >> 8) & 0xFF);
                                    byte b = (byte)(col & 0xFF);
                                    swatches.Add($"#{r:X2}{g:X2}{b:X2}");
                                }

                                var dto = new MasterPaletteDto
                                {
                                    PaletteId = p,
                                    PaletteHex = $"0x{p:X8}",
                                    Name = $"Exotic Mutation 0x{p:X7}",
                                    Swatches = swatches
                                };
                                _masterPaletteDtos.Add(dto);

                                if (IsVibrantCreaturePalette(pal))
                                    _vibrantPaletteDtos.Add(dto);
                            }
                        }
                    }

                    log.Info($"PetMutationService initialized with {_masterVerifiedPalettePool.Count} verified master palette lookup entries ({_vibrantPaletteDtos.Count} vibrant).");
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    log.Error($"Error initializing PetMutationService: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// A palette only produces a VISIBLE creature recolour if it carries real colour across the
        /// full 2048-entry range that creature textures index into. Many DAT palettes are colourful
        /// in their first 256 entries but mostly black beyond that; painted onto a creature they render
        /// as a near-black silhouette that reads as "no change". Measured in-game on an Ursuin:
        /// working palettes had 0-30% black slots and mean luminance >= 0.24 at the indices its
        /// textures use; failing ones were 60-92% black with luminance <= 0.10. Filter on the whole
        /// range, NOT the first 256 entries - that is the mistake the old smart pool made.
        /// </summary>
        private static bool IsUsableCreaturePalette(Palette pal)
        {
            if (pal?.Colors == null || pal.Colors.Count < 2048)
                return false;

            int black = 0;
            double lum = 0;
            foreach (var c in pal.Colors)
            {
                int r = (int)(c >> 16) & 0xFF, g = (int)(c >> 8) & 0xFF, b = (int)c & 0xFF;
                if (r == 0 && g == 0 && b == 0) black++;
                lum += Math.Max(r, Math.Max(g, b)) / 255.0;
            }

            double blackFrac = (double)black / pal.Colors.Count;
            double meanLum = lum / pal.Colors.Count;
            return blackFrac < 0.40 && meanLum >= 0.15;
        }

        /// <summary>
        /// Algorithmic HSL saturation filter for Chromatic Catalyst mutations.
        /// Ensures selected palettes have high color saturation and richness across their color range,
        /// avoiding murky greys, washed out pastels, or near-monochrome palettes.
        /// </summary>
        private static bool IsVibrantCreaturePalette(Palette pal)
        {
            if (!IsUsableCreaturePalette(pal))
                return false;

            double totalSat = 0;
            double maxSat = 0;
            int vibrantCount = 0;

            foreach (var c in pal.Colors)
            {
                float r = ((c >> 16) & 0xFF) / 255.0f;
                float g = ((c >> 8) & 0xFF) / 255.0f;
                float b = (c & 0xFF) / 255.0f;

                float max = Math.Max(r, Math.Max(g, b));
                float min = Math.Min(r, Math.Min(g, b));
                float delta = max - min;

                float l = (max + min) / 2.0f;
                float s = 0;
                if (delta > 0.001f)
                {
                    s = l > 0.5f ? delta / (2.0f - max - min) : delta / (max + min);
                }

                totalSat += s;
                if (s > maxSat) maxSat = s;
                if (s >= 0.40f && l >= 0.18f && l <= 0.85f)
                    vibrantCount++;
            }

            double meanSat = totalSat / pal.Colors.Count;
            double vibrantFrac = (double)vibrantCount / pal.Colors.Count;

            // Vibrant palettes feature strong peak saturation (>=0.65), healthy average saturation (>=0.25),
            // and at least 20% of their entries in the rich/vibrant color range.
            return maxSat >= 0.65 && meanSat >= 0.25 && vibrantFrac >= 0.20;
        }

        public static CreaturePaletteProfile GetProfile(uint wcid)
        {
            if (!_isInitialized) Initialize();

            if (_profileCache.TryGetValue(wcid, out var cached))
                return cached;

            uint setupId = 0, clothingBaseId = 0;
            var cachedWeenie = DatabaseManager.World?.GetCachedWeenie(wcid);
            if (cachedWeenie != null && cachedWeenie.PropertiesDID != null)
            {
                cachedWeenie.PropertiesDID.TryGetValue(PropertyDataId.Setup, out setupId);
                cachedWeenie.PropertiesDID.TryGetValue(PropertyDataId.ClothingBase, out clothingBaseId);
            }

            if (setupId == 0)
            {
                var dbWeenie = DatabaseManager.World?.GetWeenie(wcid);
                if (dbWeenie != null && dbWeenie.WeeniePropertiesDID != null)
                {
                    foreach (var prop in dbWeenie.WeeniePropertiesDID)
                    {
                        if (prop.Type == (ushort)PropertyDataId.Setup && setupId == 0) setupId = prop.Value;
                        if (prop.Type == (ushort)PropertyDataId.ClothingBase && clothingBaseId == 0) clothingBaseId = prop.Value;
                    }
                }
            }

            if (setupId == 0 && (wcid == 41224 || wcid == 41244)) { setupId = 0x02000001; clothingBaseId = 0x10000764; }

            var profile = EvaluateProfile(setupId, clothingBaseId);
            _profileCache[wcid] = profile;
            return profile;
        }

        public static uint GetDefaultClothingBaseForSetup(uint setupId)
        {
            switch (setupId)
            {
                case 0x02000E08: return 0x100000B3; // Banderling
                case 0x02000486: return 0x100000B2; // Mattekar
                case 0x0200047B: return 0x100000BD; // Rabbit / Bunny
                case 0x02000925: return 0x10000213; // Ursuin
                case 0x02000E66: return 0x1000020B; // Chittick
                case 0x02000926: return 0x10000214; // Niffis
                case 0x02000041: return 0x100000B4; // Virindi
                case 0x02000059: return 0x100000B6; // Skeleton
                case 0x02000197: return 0x100000B5; // Undead / Zombie
                case 0x020008DA: return 0x1000021A; // Statue
                case 0x020007DD: return 0x100000AF; // Drudge
                case 0x02000AAC: return 0x100000AE; // Olthoi
                case 0x02000A0B: return 0x100000B1; // Lugian
                case 0x02000964: return 0x100000B7; // Tusker
                case 0x02001036: return 0x10000780; // Burun
                case 0x0200190F: return 0x10000764; // Gear Knight
                case 0x020007CA: return 0x1000020D; // Copper Golem
                case 0x020007D7: return 0x10000229; // Granite Golem
                case 0x02001120: return 0x100007A1; // Ghost
                default: return 0;
            }
        }

        public static CreaturePaletteProfile EvaluateProfile(uint setupId, uint clothingBaseId)
        {
            if (setupId == 0)
            {
                return new CreaturePaletteProfile
                {
                    SetupId = 0,
                    Mode = CreaturePaletteMode.Incompatible,
                    Reason = "Pure non-mesh or invisible entity (no SetupModel DID)."
                };
            }

            if (clothingBaseId == 0)
                clothingBaseId = GetDefaultClothingBaseForSetup(setupId);

            if (_blacklistedSetups.Contains(setupId))
            {
                string reason = "Model does not support palette recoloring in Asheron's Call (Elementals, Wisps, Swarms, Gurogs, Fae, Viridians, Water/Wood Golems).";
                if (setupId == 0x02000219 || setupId == 0x020007E6)
                    reason = "Water Golems use a static unpalettized water caustics texture that cannot be recolored via palette swaps.";
                else if (setupId == 0x02000B4C)
                    reason = "Wood Golems use a unique DXT1 bark texture model that ignores palette packets.";
                else if (setupId >= 0x02001A2B && setupId <= 0x02001A2E)
                    reason = "Gurogs use late-retail multi-part DXT1 meshes with modern shaders that ignore palette modulation.";
                else if (setupId == 0x02001A10)
                    reason = "Fae Sprites use direct DXT1 textures with shaders that ignore palette modulation.";
                else if (setupId == 0x02001BCA)
                    reason = "Viridian Statues use direct DXT1 textures with modern shaders that ignore palette modulation.";

                return new CreaturePaletteProfile
                {
                    SetupId = setupId,
                    ClothingBaseId = clothingBaseId,
                    Mode = CreaturePaletteMode.Incompatible,
                    Reason = reason
                };
            }

            if (_diffuseTintSetups.Contains(setupId))
            {
                return new CreaturePaletteProfile
                {
                    SetupId = setupId,
                    ClothingBaseId = clothingBaseId,
                    Mode = CreaturePaletteMode.ModulatedDiffuseTint,
                    Reason = "Supports whole-model diffuse color modulation tint in DirectX render loop.",
                    ValidPalettePool = _masterVerifiedPalettePool
                };
            }

            // Check if it has a Modular ClothingBase Table
            if (clothingBaseId != 0)
            {
                var cloTable = DatManager.PortalDat?.ReadFromDat<ClothingTable>(clothingBaseId);
                if (cloTable?.ClothingSubPalEffects != null && cloTable.ClothingSubPalEffects.Count > 0)
                {
                    var validTemplates = cloTable.ClothingSubPalEffects.Keys.ToList();
                    return new CreaturePaletteProfile
                    {
                        SetupId = setupId,
                        ClothingBaseId = clothingBaseId,
                        Mode = CreaturePaletteMode.ModularClothing,
                        Reason = "Modular creature with ClothingBase sub-palette effects.",
                        ValidPalettePool = validTemplates
                    };
                }
            }

            // Check if SetupModel contains indexed 256-color textures (PFID_INDEX16 / PFID_P8)
            var setup = DatManager.PortalDat?.ReadFromDat<SetupModel>(setupId);
            if (setup == null || setup.Parts == null || setup.Parts.Count == 0)
            {
                return new CreaturePaletteProfile
                {
                    SetupId = setupId,
                    Mode = CreaturePaletteMode.Incompatible,
                    Reason = "SetupModel record missing or empty in Portal DAT."
                };
            }

            bool hasIndexed = false;
            if (DatManager.PortalDat != null)
            {
                foreach (var partGfxId in setup.Parts)
                {
                    var gfx = DatManager.PortalDat.ReadFromDat<GfxObj>(partGfxId);
                    if (gfx?.Surfaces == null) continue;

                    foreach (var sId in gfx.Surfaces)
                    {
                        var sf = DatManager.PortalDat.ReadFromDat<Surface>(sId);
                        uint oTex = sf?.OrigTextureId ?? 0;

                        if ((oTex & 0xFF000000) == 0x05000000)
                        {
                            var surfTex = DatManager.PortalDat?.ReadFromDat<SurfaceTexture>(oTex);
                            if (surfTex?.Textures != null && surfTex.Textures.Count > 0)
                                oTex = surfTex.Textures[0];
                        }

                        if (oTex != 0)
                        {
                            var tex = DatManager.PortalDat.ReadFromDat<Texture>(oTex);
                            if (tex != null && (tex.Format == SurfacePixelFormat.PFID_INDEX16 || tex.Format == SurfacePixelFormat.PFID_P8))
                            {
                                hasIndexed = true;
                                break;
                            }
                        }
                    }
                    if (hasIndexed) break;
                }
            }

            if (hasIndexed)
            {
                return new CreaturePaletteProfile
                {
                    SetupId = setupId,
                    ClothingBaseId = clothingBaseId,
                    Mode = CreaturePaletteMode.IndexedWholeBody,
                    Reason = "Standalone fauna with indexed 256-color texture (whole-body palette replacement).",
                    ValidPalettePool = _masterVerifiedPalettePool
                };
            }

            // Fallback for direct unindexed meshes
            return new CreaturePaletteProfile
            {
                SetupId = setupId,
                ClothingBaseId = clothingBaseId,
                Mode = CreaturePaletteMode.Incompatible,
                Reason = "Mesh contains unindexed textures that do not support palette swapping."
            };
        }

        // =====================================================================================
        // Mutation palette write. Shared by PetDevice_Breeding.CompleteBirth's rule (the reference),
        // @mutate_pet and the Mutagenic Serum so the three cannot drift.
        // =====================================================================================

        /// <summary>WCID of the Mutagenic Serum: a colour-only re-roll from the master palette pool.</summary>
        public const uint MutagenicSerumWcid = 78780257;

        /// <summary>What a mutation-palette write changed, so a caller can print or log it.</summary>
        public sealed class PaletteRecolour
        {
            /// <summary>Setup the native base was resolved for (0 when the target carries none).</summary>
            public uint SetupId;
            public uint PaletteId;
            public uint OldPaletteBase;
            public uint NewPaletteBase;
            public int? OldPaletteTemplate;
            public int NewPaletteTemplate;
            /// <summary>The setup has a native DefaultPaletteId and the base was re-pointed at it.</summary>
            public bool NativeBaseApplied;
            /// <summary>CapturedObjDescPalettes was present and has been removed.</summary>
            public bool CapturedPalettesCleared;
        }

        /// <summary>
        /// How much of a pet's body its captured textures cover, and therefore whether a palette
        /// change can be seen at all.
        /// </summary>
        public enum ColourCoverage
        {
            /// <summary>No captured textures, or few enough that the palette still shows on the rest.</summary>
            Visible,
            /// <summary>Captured textures cover essentially every part; a palette cannot show through.</summary>
            Hidden,
            /// <summary>Textures are present but the capture stored no part list, so coverage is unknowable.</summary>
            Unknown,
            /// <summary>
            /// Most of the model is drawn with full-colour textures (R8G8B8 and similar) that ignore palettes
            /// entirely, so a palette change is applied but cannot show (e.g. the Spectral Nanjou Shou-jen).
            /// </summary>
            FixedColour,
        }

        /// <summary>What <see cref="GetColourChangeVisibility"/> measured, so a caller can refuse, warn or print.</summary>
        public sealed class ColourChangeVisibility
        {
            public ColourCoverage Coverage;
            /// <summary>Number of surface replacements the capture holds.</summary>
            public int TextureCount;
            /// <summary>Distinct body parts those replacements land on.</summary>
            public int TexturedParts;
            /// <summary>Body parts the capture defines at all; 0 when it stored no part list.</summary>
            public int TotalParts;
            /// <summary>TexturedParts / TotalParts, or null when TotalParts is 0.</summary>
            public double? Fraction;
            /// <summary>Share (0-100) of the model's drawn polygons whose textures cannot take a palette; -1 when not measured.</summary>
            public int FixedColourPercent = -1;
            /// <summary>True when a colour change cannot usefully be seen: covered by textures, or full-colour textures.</summary>
            public bool BlocksColour => Coverage == ColourCoverage.Hidden || Coverage == ColourCoverage.FixedColour;
        }

        /// <summary>
        /// A texture replacement hides the palette for the ONE part it lands on, so a colour change is
        /// invisible only when the capture retextures essentially every part. Anything less and the
        /// remaining parts still tint: a Sawato Bandit with 3 replacements across 34 parts recolours
        /// normally. Requiring merely that textures exist refused 784 of this shard's essences, 44 of
        /// them wrongly.
        ///
        /// Slack is deliberate: a handful of parts are never visible anyway (mouth interiors, surfaces
        /// under armour), so demanding 100% would keep near-invisible pets eligible.
        /// </summary>
        private const double ColourHiddenPartFraction = 0.90;

        /// <summary>
        /// Measures whether a colour change on this essence would be visible. Read from the device
        /// rather than a species profile: two pets of the same creature differ here, because it depends
        /// on what the capture stored.
        /// </summary>
        public static ColourChangeVisibility GetColourChangeVisibility(PetDevice device)
        {
            if (device == null)
                return new ColourChangeVisibility { Coverage = ColourCoverage.Visible };

            var result = GetColourChangeVisibility(device.CapturedObjDescTextures, device.CapturedObjDescAnimParts);

            // Covered by textures already says it all. Otherwise check whether the model's own textures take a
            // palette at all.
            if (result.Coverage != ColourCoverage.Hidden && (device.VisualOverrideSetup ?? 0) != 0)
            {
                var (fixedPolys, drawnPolys) = MeasureFixedColourPolygons(device.VisualOverrideSetup.Value,
                    device.CapturedObjDescAnimParts, device.CapturedObjDescTextures);
                if (drawnPolys > 0)
                    result.FixedColourPercent = (int)Math.Round(100.0 * fixedPolys / drawnPolys);
                if (IsFixedColourModel(fixedPolys, drawnPolys))
                    result.Coverage = ColourCoverage.FixedColour;
            }

            return result;
        }

        /// <summary>
        /// Share of a model's drawn polygons that must use full-colour (non-palette) textures before a colour
        /// change counts as not visible. Measured by polygons, not parts, because parts differ wildly in size.
        /// The Spectral Nanjou Shou-jen is 66% (its body is R8G8B8; two INDEX16 parts are the rest) and a
        /// palette change does not visibly change it; a Drudge Skulker is 0%.
        /// </summary>
        private const double FixedColourPolygonFraction = 0.60;

        /// <summary>The empty placeholder GfxObj many setups use for unused part slots (as in CreatureVariant).</summary>
        private const uint NullPart = 0x010001EC;

        private static readonly ConcurrentDictionary<string, (int FixedPolys, int DrawnPolys)> _fixedColourCache = new();

        /// <summary>Pure rule, so it can be tested without the DAT.</summary>
        public static bool IsFixedColourModel(int fixedPolygons, int drawnPolygons) =>
            drawnPolygons > 0 && (double)fixedPolygons / drawnPolygons >= FixedColourPolygonFraction;

        /// <summary>
        /// How much of the model (by polygon count) cannot take a palette. Reads the Portal DAT: the setup's
        /// parts with the capture's anim-part overrides ("part:gfxObjId") applied, and each surface's texture with
        /// the capture's texture swaps ("part:oldTexture:newTexture") applied. A part takes a palette when at
        /// least one of its surfaces resolves to a palette-indexed texture (PFID_INDEX16 or PFID_P8). Cached per
        /// setup and capture strings. Any DAT failure counts as "not measured" (0, 0), which never blocks.
        /// </summary>
        public static (int FixedPolys, int DrawnPolys) MeasureFixedColourPolygons(uint setupId, string capturedAnimParts, string capturedTextures)
        {
            var key = setupId + "|" + (capturedAnimParts ?? "") + "|" + (capturedTextures ?? "");
            return _fixedColourCache.GetOrAdd(key, _ =>
            {
                try
                {
                    var setup = DatManager.PortalDat?.ReadFromDat<SetupModel>(setupId);
                    if (setup?.Parts == null)
                        return (0, 0);

                    var parts = new List<uint>(setup.Parts);
                    foreach (var (idx, gfxId) in ParsePairs(capturedAnimParts))
                    {
                        while (parts.Count <= idx) parts.Add(0);
                        parts[idx] = gfxId;
                    }

                    // (part, oldTexture) -> newTexture
                    var swaps = new Dictionary<(int, uint), uint>();
                    if (!string.IsNullOrEmpty(capturedTextures))
                    {
                        foreach (var entry in capturedTextures.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var bits = entry.Split(':');
                            if (bits.Length == 3 && int.TryParse(bits[0].Trim(), out var p) &&
                                uint.TryParse(bits[1].Trim(), out var oldTex) && uint.TryParse(bits[2].Trim(), out var newTex))
                                swaps[(p, oldTex)] = newTex;
                        }
                    }

                    int fixedPolys = 0, drawnPolys = 0;
                    for (int i = 0; i < parts.Count; i++)
                    {
                        var partId = parts[i];
                        if (partId == 0 || partId == NullPart)
                            continue;
                        var gfx = DatManager.PortalDat.ReadFromDat<GfxObj>(partId);
                        if (gfx?.Surfaces == null || gfx.Surfaces.Count == 0)
                            continue;

                        var polys = Math.Max(1, gfx.Polygons?.Count ?? 1);
                        drawnPolys += polys;

                        var takesPalette = false;
                        foreach (var surfaceId in gfx.Surfaces)
                        {
                            var surface = DatManager.PortalDat.ReadFromDat<Surface>(surfaceId);
                            if (surface == null)
                                continue;
                            var texId = surface.OrigTextureId;
                            if (swaps.TryGetValue((i, texId), out var swapped))
                                texId = swapped;
                            if (IsPaletteIndexedTexture(texId))
                            {
                                takesPalette = true;
                                break;
                            }
                        }
                        if (!takesPalette)
                            fixedPolys += polys;
                    }
                    return (fixedPolys, drawnPolys);
                }
                catch (Exception ex)
                {
                    log.Warn($"[PetMutation] Could not measure palette use for setup 0x{setupId:X8}: {ex.Message}");
                    return (0, 0);
                }
            });
        }

        /// <summary>True when a surface texture (0x05) or texture (0x06) resolves to a palette-indexed image.</summary>
        private static bool IsPaletteIndexedTexture(uint texId)
        {
            if (texId == 0)
                return false;

            IEnumerable<uint> textures = (texId & 0xFF000000) == 0x05000000
                ? (IEnumerable<uint>)DatManager.PortalDat.ReadFromDat<SurfaceTexture>(texId)?.Textures ?? new List<uint>()
                : new List<uint> { texId };

            foreach (var t in textures)
            {
                var tex = DatManager.PortalDat.ReadFromDat<Texture>(t);
                if (tex != null && (tex.Format == SurfacePixelFormat.PFID_INDEX16 || tex.Format == SurfacePixelFormat.PFID_P8))
                    return true;
            }
            return false;
        }

        /// <summary>Parses "index:value" pairs, skipping anything malformed.</summary>
        private static IEnumerable<(int Index, uint Value)> ParsePairs(string packed)
        {
            if (string.IsNullOrEmpty(packed))
                yield break;
            foreach (var entry in packed.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = entry.Split(':');
                if (bits.Length == 2 && int.TryParse(bits[0].Trim(), out var idx) && idx >= 0 && uint.TryParse(bits[1].Trim(), out var val))
                    yield return (idx, val);
            }
        }

        /// <summary>
        /// The pure half of <see cref="GetColourChangeVisibility(PetDevice)"/>, taking the two stored
        /// strings directly so the coverage rule can be tested without a live device.
        /// </summary>
        public static ColourChangeVisibility GetColourChangeVisibility(string capturedTextures, string capturedAnimParts)
        {
            var result = new ColourChangeVisibility { Coverage = ColourCoverage.Visible };

            var captured = capturedTextures;
            if (string.IsNullOrEmpty(captured))
                return result;

            // Textures are stored as "part:oldTexture:newTexture", anim parts as "part:animationId".
            var texturedParts = ParseLeadingIndices(captured, out var textureCount);
            result.TextureCount = textureCount;
            result.TexturedParts = texturedParts.Count;

            if (textureCount == 0)
                return result;

            var allParts = ParseLeadingIndices(capturedAnimParts, out _);
            result.TotalParts = allParts.Count;

            if (result.TotalParts == 0)
            {
                // Textures exist but there is no part list to measure them against. Callers warn
                // instead of refusing: blocking the feature outright is the worse failure.
                result.Coverage = ColourCoverage.Unknown;
                return result;
            }

            // Only parts the capture actually defines can be covered.
            texturedParts.IntersectWith(allParts);
            result.TexturedParts = texturedParts.Count;
            result.Fraction = (double)result.TexturedParts / result.TotalParts;

            if (result.Fraction >= ColourHiddenPartFraction)
                result.Coverage = ColourCoverage.Hidden;

            return result;
        }

        /// <summary>
        /// Collects the leading "part" index of each comma-separated "index:rest" entry.
        /// <paramref name="entryCount"/> counts entries, including repeats on the same part.
        /// </summary>
        private static HashSet<int> ParseLeadingIndices(string packed, out int entryCount)
        {
            var parts = new HashSet<int>();
            entryCount = 0;
            if (string.IsNullOrEmpty(packed))
                return parts;

            foreach (var entry in packed.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;
                entryCount++;

                var colon = entry.IndexOf(':');
                var head = colon < 0 ? entry : entry.Substring(0, colon);
                if (int.TryParse(head.Trim(), out var idx))
                    parts.Add(idx);
            }
            return parts;
        }

        /// <summary>
        /// True only when a colour change genuinely cannot be seen. Kept for callers that just need the
        /// yes/no; use <see cref="GetColourChangeVisibility"/> when Unknown must be told apart from
        /// Visible. <paramref name="textureCount"/> is how many surface replacements the capture holds.
        /// </summary>
        public static bool ColourChangeIsHidden(PetDevice device, out int textureCount)
        {
            var v = GetColourChangeVisibility(device);
            textureCount = v.TextureCount;
            return v.BlocksColour;
        }

        /// <summary>
        /// Draws one palette from the master pool: the same fully random, unfiltered draw a bred
        /// mutation makes (not the vibrant Chromatic Catalyst pool). False when the pool is empty.
        /// </summary>
        public static bool TryRollMasterPalette(out uint paletteId, out int poolIndex, out int poolCount)
        {
            paletteId = 0;
            poolIndex = -1;
            poolCount = 0;

            var pool = GetMasterPalettePool();
            if (pool == null || pool.Count == 0)
                return false;

            poolCount = pool.Count;
            poolIndex = ThreadSafeRandom.Next(0, pool.Count - 1); // Next(min, max) is inclusive of max
            paletteId = pool[poolIndex].PaletteId;
            return paletteId != 0;
        }

        /// <summary>
        /// Writes a mutation palette onto a pet device exactly the way PetDevice_Breeding.CompleteBirth
        /// does. The mutation goes in the TEMPLATE (overlay); the BASE is re-pointed at the setup's
        /// native DefaultPaletteId when it has one, otherwise left alone (a mutation written into the
        /// base renders nothing). CapturedObjDescPalettes is removed because it forces
        /// Creature.CalculateObjDesc to early-return before the PaletteTemplate recolour branch.
        /// Anim-part and texture rows are untouched. Nothing else on the device changes.
        /// </summary>
        /// <param name="setupId">Explicit setup to write into VisualOverrideSetup, or null to keep the device's own.</param>
        public static PaletteRecolour ApplyMutationPalette(PetDevice device, uint? setupId, uint paletteId)
        {
            if (device == null) throw new ArgumentNullException(nameof(device));
            if (paletteId == 0) throw new ArgumentOutOfRangeException(nameof(paletteId), "A mutation palette must be non-zero.");

            var r = new PaletteRecolour
            {
                PaletteId = paletteId,
                OldPaletteBase = device.VisualOverridePaletteBase ?? 0,
                OldPaletteTemplate = device.VisualOverridePaletteTemplate,
            };

            if (setupId.HasValue)
                device.VisualOverrideSetup = setupId.Value;
            r.SetupId = device.VisualOverrideSetup ?? 0;

            var nativeBase = Creature.GetSetupDefaultPaletteId(r.SetupId);
            if (nativeBase != 0)
            {
                device.VisualOverridePaletteBase = nativeBase;
                r.NativeBaseApplied = true;
            }

            device.VisualOverridePaletteTemplate = (int)paletteId;

            r.CapturedPalettesCleared = !string.IsNullOrEmpty(device.GetProperty(PropertyString.CapturedObjDescPalettes));
            device.RemoveProperty(PropertyString.CapturedObjDescPalettes);

            r.NewPaletteBase = device.VisualOverridePaletteBase ?? 0;
            r.NewPaletteTemplate = (int)paletteId;
            return r;
        }

        /// <summary>
        /// The live-pet twin of <see cref="ApplyMutationPalette(PetDevice, uint?, uint)"/>: writes the
        /// same base/template/captured-palette rule onto a summoned pet. Does not push the change to
        /// clients; call <see cref="ForceClientRedraw"/> afterwards.
        /// </summary>
        public static PaletteRecolour ApplyMutationPalette(Pet pet, uint setupId, uint paletteId)
        {
            if (pet == null) throw new ArgumentNullException(nameof(pet));
            if (paletteId == 0) throw new ArgumentOutOfRangeException(nameof(paletteId), "A mutation palette must be non-zero.");

            var r = new PaletteRecolour
            {
                SetupId = setupId,
                PaletteId = paletteId,
                OldPaletteBase = pet.PaletteBaseId ?? 0,
                OldPaletteTemplate = pet.PaletteTemplate,
            };

            pet.SetupTableId = setupId;

            var nativeBase = Creature.GetSetupDefaultPaletteId(setupId);
            if (nativeBase != 0)
            {
                pet.PaletteBaseId = nativeBase;
                r.NativeBaseApplied = true;
            }

            pet.PaletteTemplate = (int)paletteId;

            r.CapturedPalettesCleared = !string.IsNullOrEmpty(pet.GetProperty(PropertyString.CapturedObjDescPalettes));
            pet.RemoveProperty(PropertyString.CapturedObjDescPalettes);

            r.NewPaletteBase = pet.PaletteBaseId ?? 0;
            r.NewPaletteTemplate = (int)paletteId;
            return r;
        }

        /// <summary>
        /// Forces every client that knows the creature to redraw it by cycling object tracking. This is
        /// the redraw @mutate_pet has always used for a live pet. Safe only from the thread that owns
        /// the creature's landblock group; callers on another thread must not use it.
        /// </summary>
        public static void ForceClientRedraw(Creature creature)
        {
            var objMaint = creature?.PhysicsObj?.ObjMaint;
            if (objMaint == null)
                return;

            foreach (var viewer in objMaint.GetKnownPlayersValuesAsPlayer())
            {
                viewer.RemoveTrackedObject(creature, false);
                viewer.AddTrackedObject(creature);
            }
        }
    }
}
