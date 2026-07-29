using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ACE.Common;
using ACE.Database;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.DatLoader.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ACE.Server.Services
{
    public class SpeciesPaletteDto
    {
        public uint TemplateId { get; set; }
        public string Name { get; set; }
        public uint PaletteSetId { get; set; }
        public string PaletteSetHex => $"0x{PaletteSetId:X8}";
        public uint PaletteId { get; set; }
        public string PaletteHex => $"0x{PaletteId:X8}";
        public List<string> Swatches { get; set; } = new List<string>();
        public string Ranges { get; set; } = "Offset 0 - 2048 (Full Mesh)";
    }

    public class TextureReplacementDto
    {
        public string Name { get; set; }
        public uint OldTextureId { get; set; }
        public uint NewTextureId { get; set; }
    }

    public class SmartPaletteDto
    {
        public uint PaletteId { get; set; }
        public string HexId { get; set; }
        public string Family { get; set; }
        public List<string> Swatches { get; set; } = new List<string>();
    }

    public class CreatureSurfaceDto
    {
        public int Index { get; set; }
        public uint TextureId { get; set; }
        public string HexId { get; set; }
        public string Name { get; set; }
    }

    public class TextureLibraryItemDto
    {
        public string Category { get; set; }
        public uint TextureId { get; set; }
        public string HexId { get; set; }
        public string Name { get; set; }
    }public static class VisualizerService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(VisualizerService));

        // Concurrency locks per asset ID
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        private static string CacheDir => Path.Combine(AppContext.BaseDirectory, "wwwroot", "visualizer_cache");

        private static string ModelsCacheDir => Path.Combine(CacheDir, "models");
        private static string TexturesCacheDir => Path.Combine(CacheDir, "textures");
        private static string PalettesCacheDir => Path.Combine(CacheDir, "palettes");

        static VisualizerService()
        {
            try
            {
                Directory.CreateDirectory(ModelsCacheDir);
                Directory.CreateDirectory(TexturesCacheDir);
                Directory.CreateDirectory(PalettesCacheDir);
            }
            catch (Exception ex)
            {
                log.Error($"Failed to initialize visualizer cache directories: {ex.Message}");
            }
        }

        private static List<SmartPaletteDto> _smartPalettesCache = null;
        private static readonly object _smartPalettesLock = new object();

        public static List<SmartPaletteDto> GetSmartPalettePool(uint wcid, string family = "all")
        {
            if (_smartPalettesCache == null)
            {
                lock (_smartPalettesLock)
                {
                    if (_smartPalettesCache == null)
                    {
                        _smartPalettesCache = BuildSmartPalettePool();
                    }
                }
            }

            if (string.IsNullOrEmpty(family) || family.Equals("all", StringComparison.OrdinalIgnoreCase))
                return _smartPalettesCache;

            return _smartPalettesCache.Where(p => p.Family.Equals(family, StringComparison.OrdinalIgnoreCase) || (p.Family == "Fur" && family == "Fur/Hide")).ToList();
        }

        private static List<SmartPaletteDto> BuildSmartPalettePool()
        {
            var results = new List<SmartPaletteDto>();
            var portalDb = new PortalDatDatabase(DatManager.PortalDat.FilePath, keepOpen: false);

            foreach (var kvp in portalDb.AllFiles)
            {
                uint fileId = kvp.Key;
                if ((fileId & 0xFF000000) != 0x04000000) continue;

                var palette = portalDb.ReadFromDat<Palette>(fileId);
                if (palette == null || palette.Colors == null || palette.Colors.Count < 16) continue;

                int colorCount = Math.Min(256, palette.Colors.Count);
                
                double avgSat = 0;
                double avgLum = 0;
                double totalDelta = 0;

                for (int i = 0; i < colorCount; i++)
                {
                    var c = palette.Colors[i];
                    byte r = (byte)((c >> 16) & 0xFF);
                    byte g = (byte)((c >> 8) & 0xFF);
                    byte b = (byte)(c & 0xFF);
                    
                    ColorToHSV(r, g, b, out double h, out double s, out double v);
                    avgSat += s;
                    avgLum += v;

                    if (i > 0)
                    {
                        var prevC = palette.Colors[i - 1];
                        byte pr = (byte)((prevC >> 16) & 0xFF);
                        byte pg = (byte)((prevC >> 8) & 0xFF);
                        byte pb = (byte)(prevC & 0xFF);
                        double dist = Math.Sqrt(Math.Pow(r - pr, 2) + Math.Pow(g - pg, 2) + Math.Pow(b - pb, 2));
                        totalDelta += dist;
                    }
                }

                avgSat /= colorCount;
                avgLum /= colorCount;
                double avgDelta = totalDelta / (colorCount - 1);

                // Filters out flat-black, flat-white, and noisy spiky palettes (GUI icons)
                if (avgLum < 0.1 || avgLum > 0.95) continue;
                if (avgSat < 0.05 && avgLum > 0.8) continue;
                if (avgDelta > 80) continue; // GUI icons usually have very high step variance

                string family = "Humanoid";
                if (avgSat < 0.2 && avgLum < 0.5) family = "Metallic";
                else if (avgSat > 0.5) family = "Elemental";
                else if (avgLum < 0.4) family = "Chitin";
                else family = "Fur";

                var swatches = new List<string>();
                int sampleCount = Math.Min(6, palette.Colors.Count);
                for (int i = 0; i < sampleCount; i++)
                {
                    int idx = (palette.Colors.Count / sampleCount) * i;
                    var c = palette.Colors[idx];
                    byte r = (byte)((c >> 16) & 0xFF);
                    byte g = (byte)((c >> 8) & 0xFF);
                    byte b = (byte)(c & 0xFF);
                    swatches.Add($"#{r:X2}{g:X2}{b:X2}");
                }

                results.Add(new SmartPaletteDto
                {
                    PaletteId = fileId,
                    HexId = $"0x{fileId:X8}",
                    Family = family,
                    Swatches = swatches
                });
            }

            return results;
        }

        private static SemaphoreSlim GetLock(string key)
        {
            return _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        }

        /// <summary>
        /// Retrieves or exports the GLTF 3D model bytes for a Weenie Class ID.
        /// </summary>
        public static async Task<byte[]> GetMeshGltfBytesAsync(uint wcid, uint paletteId = 0, int hue = 0)
        {
            var cacheKey = $"{wcid}_{paletteId}_{hue}";
            var cachePath = Path.Combine(ModelsCacheDir, $"{cacheKey}.gltf");
            if (File.Exists(cachePath))
            {
                return await File.ReadAllBytesAsync(cachePath);
            }

                var wcidLock = GetLock($"mesh_{cacheKey}");
                await wcidLock.WaitAsync();
                try
                {
                    if (File.Exists(cachePath))
                    {
                        return await File.ReadAllBytesAsync(cachePath);
                    }

                    var bytes = ExportMeshGltf(wcid, paletteId, hue);
                    if (bytes == null) return null;

                // Write atomically
                var tempPath = Path.Combine(ModelsCacheDir, $"temp_{Guid.NewGuid()}.gltf");
                await File.WriteAllBytesAsync(tempPath, bytes);
                File.Move(tempPath, cachePath, overwrite: true);

                return bytes;
            }
            finally
            {
                wcidLock.Release();
            }
        }


        public static List<SpeciesPaletteDto> GetSpeciesPalettes(uint wcid)
        {
            var result = new List<SpeciesPaletteDto>();
            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie == null) return result;

            var portalDb = new PortalDatDatabase(DatManager.PortalDat.FilePath, keepOpen: false);

            double weenieShade = 0.5;
            if (weenie.PropertiesFloat != null && weenie.PropertiesFloat.TryGetValue(PropertyFloat.Shade, out weenieShade)) { }
            float shade = (float)weenieShade;

            uint clothingBase = 0;
            if (weenie.PropertiesDID != null)
                weenie.PropertiesDID.TryGetValue(PropertyDataId.ClothingBase, out clothingBase);

            if (clothingBase != 0)
            {
                var clothingTable = portalDb.ReadFromDat<ClothingTable>(clothingBase);
                if (clothingTable != null && clothingTable.ClothingSubPalEffects != null)
                {
                    foreach (var kvp in clothingTable.ClothingSubPalEffects)
                    {
                        var templateId = kvp.Key;
                        var effect = kvp.Value;
                        uint finalPaletteId = 0;
                        uint paletteSetId = 0;
                        var swatches = new List<string>();
                        string rangesStr = "Offset 0 - 2048 (Full Mesh)";

                        if (effect.CloSubPalettes != null && effect.CloSubPalettes.Count > 0)
                        {
                            var subPal = effect.CloSubPalettes[0];
                            paletteSetId = subPal.PaletteSet;
                            if (paletteSetId != 0)
                            {
                                var paletteSet = portalDb.ReadFromDat<PaletteSet>(paletteSetId);
                                if (paletteSet != null)
                                {
                                    finalPaletteId = paletteSet.GetPaletteID(shade);
                                }
                            }

                            if (subPal.Ranges != null && subPal.Ranges.Count > 0)
                            {
                                var rangeList = new List<string>();
                                foreach (var r in subPal.Ranges)
                                    rangeList.Add($"Offset {r.Offset} ({r.NumColors} colors)");
                                rangesStr = string.Join(", ", rangeList);
                            }
                        }

                        if (finalPaletteId == 0) continue;

                        var resolvedPal = portalDb.ReadFromDat<Palette>(finalPaletteId);
                        if (resolvedPal != null && resolvedPal.Colors != null && resolvedPal.Colors.Count > 0)
                        {
                            int step = Math.Max(1, resolvedPal.Colors.Count / 8);
                            for (int i = 0; i < resolvedPal.Colors.Count && swatches.Count < 8; i += step)
                            {
                                uint argb = resolvedPal.Colors[i];
                                byte r = (byte)((argb >> 16) & 0xFF);
                                byte g = (byte)((argb >> 8) & 0xFF);
                                byte b = (byte)(argb & 0xFF);
                                swatches.Add($"#{r:X2}{g:X2}{b:X2}");
                            }
                        }

                        result.Add(new SpeciesPaletteDto 
                        { 
                            TemplateId = templateId, 
                            Name = $"Variant {templateId}", 
                            PaletteSetId = paletteSetId,
                            PaletteId = finalPaletteId,
                            Swatches = swatches,
                            Ranges = rangesStr
                        });
                    }
                }
            }

            // Fallback for creatures without ClothingBase (e.g. Tusker Protector WCID 36967)
            if (result.Count == 0 && weenie.PropertiesDID != null)
            {
                uint paletteBase = 0;
                weenie.PropertiesDID.TryGetValue(PropertyDataId.PaletteBase, out paletteBase);

                if (paletteBase != 0)
                {
                    uint paletteSetId = (paletteBase & 0xFF000000) == 0x0F000000 ? paletteBase : 0;
                    uint paletteId = (paletteBase & 0xFF000000) == 0x04000000 ? paletteBase : 0;

                    if (paletteSetId != 0)
                    {
                        var palSet = portalDb.ReadFromDat<PaletteSet>(paletteSetId);
                        if (palSet != null) paletteId = palSet.GetPaletteID(shade);
                    }

                    var swatches = new List<string>();
                    var resolvedPal = portalDb.ReadFromDat<Palette>(paletteId);
                    if (resolvedPal != null && resolvedPal.Colors != null && resolvedPal.Colors.Count > 0)
                    {
                        int step = Math.Max(1, resolvedPal.Colors.Count / 8);
                        for (int i = 0; i < resolvedPal.Colors.Count && swatches.Count < 8; i += step)
                        {
                            uint argb = resolvedPal.Colors[i];
                            byte r = (byte)((argb >> 16) & 0xFF);
                            byte g = (byte)((argb >> 8) & 0xFF);
                            byte b = (byte)(argb & 0xFF);
                            swatches.Add($"#{r:X2}{g:X2}{b:X2}");
                        }
                    }

                    result.Add(new SpeciesPaletteDto
                    {
                        TemplateId = 0,
                        Name = "Native Species Palette",
                        PaletteSetId = paletteSetId,
                        PaletteId = paletteId,
                        Swatches = swatches,
                        Ranges = "Offset 0 - 2048 (Native Subpalette)"
                    });
                }
            }

            return result;
        }

        public static List<TextureReplacementDto> GetTextureReplacements(uint wcid)
        {
            var result = new List<TextureReplacementDto>();
            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie == null) return result;

            uint setupId = 0;
            if (weenie.PropertiesDID != null)
                weenie.PropertiesDID.TryGetValue(PropertyDataId.Setup, out setupId);

            uint clothingBase = 0;
            if (weenie.PropertiesDID != null)
                weenie.PropertiesDID.TryGetValue(PropertyDataId.ClothingBase, out clothingBase);

            var portalDb = new PortalDatDatabase(DatManager.PortalDat.FilePath, keepOpen: false);

            if (clothingBase != 0)
            {
                var clothingTable = portalDb.ReadFromDat<ClothingTable>(clothingBase);
                if (clothingTable != null && clothingTable.ClothingBaseEffects != null)
                {
                    foreach (var kvp in clothingTable.ClothingBaseEffects)
                    {
                        var effectKey = kvp.Key;
                        var effect = kvp.Value;

                        // Match setupId if key equals setupId, or parse all object effects
                        if (setupId != 0 && effectKey != setupId && effectKey != 0) continue;

                        if (effect.CloObjectEffects == null) continue;

                        foreach (var objEffect in effect.CloObjectEffects)
                        {
                            if (objEffect.CloTextureEffects == null) continue;

                            foreach (var texEffect in objEffect.CloTextureEffects)
                            {
                                result.Add(new TextureReplacementDto
                                {
                                    Name = $"Texture Swap (0x{texEffect.OldTexture:X8} -> 0x{texEffect.NewTexture:X8})",
                                    OldTextureId = texEffect.OldTexture,
                                    NewTextureId = texEffect.NewTexture
                                });
                            }
                        }
                    }
                }
            }

            // Fallback for creatures without ClothingBase: inspect SetupModel surfaces
            if (result.Count == 0 && setupId != 0)
            {
                var setup = portalDb.ReadFromDat<SetupModel>(setupId);
                if (setup != null && setup.Parts != null)
                {
                    var modelTextures = new HashSet<uint>();
                    foreach (var part in setup.Parts)
                    {
                        var gfxObj = portalDb.ReadFromDat<GfxObj>(part);
                        if (gfxObj == null || gfxObj.Surfaces == null) continue;

                        foreach (var surfId in gfxObj.Surfaces)
                        {
                            var surf = portalDb.ReadFromDat<Surface>(surfId);
                            if (surf != null && surf.OrigTextureId != 0)
                            {
                                modelTextures.Add(surf.OrigTextureId);
                            }
                        }
                    }

                    // For each texture on creature mesh, provide replacement options
                    int idx = 1;
                    foreach (var origTex in modelTextures)
                    {
                        result.Add(new TextureReplacementDto
                        {
                            Name = $"Surface #{idx} (0x{origTex:X8})",
                            OldTextureId = origTex,
                            NewTextureId = origTex
                        });
                        idx++;
                    }
                }
            }

            return result;
        }

        public static List<CreatureSurfaceDto> GetCreatureSurfaces(uint wcid)
        {
            var result = new List<CreatureSurfaceDto>();
            if (wcid == 0) return result;

            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie == null) return result;

            uint setupId = 0;
            if (weenie.PropertiesDID != null && weenie.PropertiesDID.TryGetValue(PropertyDataId.Setup, out setupId))
            {
            }

            if (setupId == 0) return result;

            var portalDb = new PortalDatDatabase(DatManager.PortalDat.FilePath, keepOpen: false);
            var setupModel = portalDb.ReadFromDat<SetupModel>(setupId);
            if (setupModel == null || setupModel.Parts == null) return result;

            var seenTextures = new HashSet<uint>();

            int index = 0;
            foreach (var gfxObjId in setupModel.Parts)
            {
                var gfxObj = portalDb.ReadFromDat<GfxObj>(gfxObjId);
                if (gfxObj == null || gfxObj.Surfaces == null) continue;

                foreach (var surfId in gfxObj.Surfaces)
                {
                    var surf = portalDb.ReadFromDat<Surface>(surfId);
                    if (surf == null) continue;

                    uint origTex = surf.OrigTextureId;
                    if (origTex != 0 && seenTextures.Add(origTex))
                    {
                        result.Add(new CreatureSurfaceDto
                        {
                            Index = ++index,
                            TextureId = origTex,
                            HexId = $"0x{origTex:X8}",
                            Name = $"Surface 0x{origTex:X8}"
                        });
                    }
                }
            }

            return result;
        }

        public static List<TextureLibraryItemDto> GetTextureLibrary(string category = "all")
        {
            var items = new List<TextureLibraryItemDto>
            {
                // Armor / Metallic
                new TextureLibraryItemDto { Category = "Armor/Metal", TextureId = 0x0600021A, HexId = "0x0600021A", Name = "🛡️ Chainmail Weave" },
                new TextureLibraryItemDto { Category = "Armor/Metal", TextureId = 0x0600021C, HexId = "0x0600021C", Name = "🛡️ Polished Platemail" },
                new TextureLibraryItemDto { Category = "Armor/Metal", TextureId = 0x0600021E, HexId = "0x0600021E", Name = "🛡️ Bronze Scale Armor" },
                new TextureLibraryItemDto { Category = "Armor/Metal", TextureId = 0x06003110, HexId = "0x06003110", Name = "🛡️ Dark Steel Plate" },

                // Chitin / Insectoid
                new TextureLibraryItemDto { Category = "Chitin/Insect", TextureId = 0x06004067, HexId = "0x06004067", Name = "🦂 Dark Olthoi Carapace" },
                new TextureLibraryItemDto { Category = "Chitin/Insect", TextureId = 0x06004FD3, HexId = "0x06004FD3", Name = "🐝 Red Phyntos Chitin" },
                new TextureLibraryItemDto { Category = "Chitin/Insect", TextureId = 0x06004FD4, HexId = "0x06004FD4", Name = "🐝 Gold Wing Veins" },
                new TextureLibraryItemDto { Category = "Chitin/Insect", TextureId = 0x06004068, HexId = "0x06004068", Name = "🦂 Olthoi Carapace Trim" },

                // Fur & Hide
                new TextureLibraryItemDto { Category = "Fur/Hide", TextureId = 0x060012E4, HexId = "0x060012E4", Name = "🐺 Tusker Brown Pelt" },
                new TextureLibraryItemDto { Category = "Fur/Hide", TextureId = 0x06003112, HexId = "0x06003112", Name = "🐺 Shadow Creature Hide" },
                new TextureLibraryItemDto { Category = "Fur/Hide", TextureId = 0x060018A2, HexId = "0x060018A2", Name = "🐺 Dire Wolf Fur" },

                // Undead & Bone
                new TextureLibraryItemDto { Category = "Undead/Bone", TextureId = 0x060020B1, HexId = "0x060020B1", Name = "💀 Decayed Zombie Flesh" },
                new TextureLibraryItemDto { Category = "Undead/Bone", TextureId = 0x060021C0, HexId = "0x060021C0", Name = "💀 Bleached Skeleton Bone" },

                // Elemental & Crystal
                new TextureLibraryItemDto { Category = "Elemental", TextureId = 0x06003B21, HexId = "0x06003B21", Name = "🔮 Volcanic Fire Crystal" },
                new TextureLibraryItemDto { Category = "Elemental", TextureId = 0x06003B22, HexId = "0x06003B22", Name = "🔮 Glacial Frost Ice" }
            };

            if (!string.IsNullOrEmpty(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return items.Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return items;
        }

        /// <summary>
        /// Retrieves or exports a 256x1 PNG representing the color palette.
        /// </summary>
        public static async Task<byte[]> GetPalettePngBytesAsync(uint paletteId)
        {
            var cachePath = Path.Combine(PalettesCacheDir, $"{paletteId}.png");
            if (File.Exists(cachePath))
            {
                return await File.ReadAllBytesAsync(cachePath);
            }

            var palLock = GetLock($"pal_{paletteId}");
            await palLock.WaitAsync();
            try
            {
                if (File.Exists(cachePath))
                {
                    return await File.ReadAllBytesAsync(cachePath);
                }

                var bytes = ExportPalettePng(paletteId);
                if (bytes == null) return null;

                // Write atomically
                var tempPath = Path.Combine(PalettesCacheDir, $"temp_{Guid.NewGuid()}.png");
                await File.WriteAllBytesAsync(tempPath, bytes);
                File.Move(tempPath, cachePath, overwrite: true);

                return bytes;
            }
            finally
            {
                palLock.Release();
            }
        }

        #region Exporter Implementation Detail

        private static byte[] ExportMeshGltf(uint wcid, uint paletteId = 0, int hue = 0)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie == null) return null;

            uint setupId = 0;
            if (weenie.PropertiesDID == null || !weenie.PropertiesDID.TryGetValue(PropertyDataId.Setup, out setupId))
            {
                log.Warn($"Weenie {wcid} does not define a Setup DID.");
                return null;
            }

            // Open separate read-only portal dat handle to avoid lock contention on game server's streamMutex
            var portalDb = new PortalDatDatabase(DatManager.PortalDat.FilePath, keepOpen: false);

            var setupModel = portalDb.ReadFromDat<SetupModel>(setupId);
            if (setupModel == null)
            {
                log.Error($"Failed to read SetupModel 0x{setupId:X8} from portal dat.");
                return null;
            }

            // Select default placement frame
            PlacementType defaultPlacement = null;
            if (setupModel.PlacementFrames.Count > 0)
            {
                if (setupModel.PlacementFrames.TryGetValue(1, out defaultPlacement)) { }
                else if (setupModel.PlacementFrames.TryGetValue(0, out defaultPlacement)) { }
                else
                {
                    foreach (var kvp in setupModel.PlacementFrames)
                    {
                        defaultPlacement = kvp.Value;
                        break;
                    }
                }
            }

            var gltf = new GltfRoot();
            var bufferHelper = new BinaryBufferHelper();

            // Populate base scene
            var gltfScene = new GltfScene();
            gltf.scenes.Add(gltfScene);

            // Node 0 is the root node
            var rootNode = new GltfNode
            {
                name = $"Weenie_{wcid}_Setup_{setupId}",
                children = new List<int>(),
                rotation = new float[] { -0.7071068f, 0f, 0f, 0.7071068f }
            };
            gltf.nodes.Add(rootNode);
            gltfScene.nodes.Add(0);

            var textureToMat = new Dictionary<uint, int>();

            // Map each setup part to a GLTF node
            for (int i = 0; i < setupModel.Parts.Count; i++)
            {
                var gfxObjId = setupModel.Parts[i];
                var parentIdx = (setupModel.ParentIndex != null && i < setupModel.ParentIndex.Count) ? (int)setupModel.ParentIndex[i] : -1;
                var scaleVec = (setupModel.DefaultScale != null && i < setupModel.DefaultScale.Count) ? setupModel.DefaultScale[i] : Vector3.One;

                var translation = Vector3.Zero;
                var rotation = Quaternion.Identity;
                if (defaultPlacement != null && defaultPlacement.AnimFrame != null && i < defaultPlacement.AnimFrame.Frames.Count)
                {
                    var frame = defaultPlacement.AnimFrame.Frames[i];
                    translation = frame.Origin;
                    rotation = frame.Orientation;
                }

                var partNodeIdx = gltf.nodes.Count;
                var partNode = new GltfNode
                {
                    name = $"Part_{i}_GfxObj_0x{gfxObjId:X8}",
                    translation = new float[] { translation.X, translation.Y, translation.Z },
                    rotation = new float[] { rotation.X, rotation.Y, rotation.Z, rotation.W },
                    scale = new float[] { scaleVec.X, scaleVec.Y, scaleVec.Z }
                };

                // Read GfxObj geometry
                var gfxObj = portalDb.ReadFromDat<GfxObj>(gfxObjId);
                if (gfxObj != null && gfxObj.Polygons.Count > 0)
                {
                    var meshIdx = gltf.meshes.Count;
                    var gltfMesh = new GltfMesh { name = $"Mesh_GfxObj_0x{gfxObjId:X8}" };

                    // Group polygons by surface ID index
                    var surfacesDict = new Dictionary<short, List<Polygon>>();
                    foreach (var poly in gfxObj.Polygons.Values)
                    {
                        if (!surfacesDict.TryGetValue(poly.PosSurface, out var list))
                        {
                            list = new List<Polygon>();
                            surfacesDict[poly.PosSurface] = list;
                        }
                        list.Add(poly);
                    }

                    foreach (var kvp in surfacesDict)
                    {
                        var surfaceIdx = kvp.Key;
                        var polys = kvp.Value;

                        // Resolve material / texture DID
                        int? gltfMatIdx = null;
                        if (surfaceIdx >= 0 && surfaceIdx < gfxObj.Surfaces.Count)
                        {
                            var surfaceId = gfxObj.Surfaces[surfaceIdx];
                            var surface = portalDb.ReadFromDat<Surface>(surfaceId);
                            if (surface != null)
                            {
                                if (surface.Type.HasFlag(SurfaceType.Base1Image) && surface.OrigTextureId != 0)
                                {
                                    var texId = surface.OrigTextureId;
                                    if ((texId & 0xFF000000) == 0x05000000)
                                    {
                                        var surfTex = portalDb.ReadFromDat<SurfaceTexture>(texId);
                                        if (surfTex != null && surfTex.Textures.Count > 0)
                                        {
                                            texId = surfTex.Textures[0];
                                        }
                                        else if (DatManager.HighResDat != null)
                                        {
                                            var highResDb = new PortalDatDatabase(DatManager.HighResDat.FilePath, keepOpen: false);
                                            surfTex = highResDb.ReadFromDat<SurfaceTexture>(texId);
                                            if (surfTex != null && surfTex.Textures.Count > 0)
                                                texId = surfTex.Textures[0];
                                        }
                                    }

                                    if (!textureToMat.TryGetValue(texId, out var matIdx))
                                    {
                                        matIdx = gltf.materials.Count;
                                        var texture = portalDb.ReadFromDat<Texture>(texId);
                                        if (texture == null || texture.SourceData == null || texture.SourceData.Length == 0)
                                        {
                                            if (DatManager.HighResDat != null)
                                            {
                                                var highResDb = new PortalDatDatabase(DatManager.HighResDat.FilePath, keepOpen: false);
                                                texture = highResDb.ReadFromDat<Texture>(texId);
                                            }
                                        }

                                        bool isIndexed = texture != null && (texture.Format == SurfacePixelFormat.PFID_P8 || texture.Format == SurfacePixelFormat.PFID_INDEX16);

                                        var material = new GltfMaterial
                                        {
                                            name = $"Material_Texture_0x{texId:X8}",
                                            pbrMetallicRoughness = new GltfPbr
                                            {
                                                baseColorTexture = new GltfTextureInfo { index = gltf.textures.Count }
                                            },
                                            extras = new Dictionary<string, object> { { "indexed", isIndexed } }
                                        };
                                        gltf.materials.Add(material);
                                        gltf.textures.Add(new GltfTexture { source = gltf.images.Count });
                                        string texUri = $"../texture/{texId:X8}.png?wcid={wcid}";
                                        if (paletteId != 0) texUri += $"&paletteId={paletteId}";
                                        if (hue != 0) texUri += $"&hue={hue}";
                                        
                                        gltf.images.Add(new GltfImage { uri = texUri });

                                        textureToMat[texId] = matIdx;
                                    }
                                    gltfMatIdx = matIdx;
                                }
                                else
                                {
                                    // Solid color fallback
                                    var col = surface.ColorValue;
                                    float a = ((col >> 24) & 0xFF) / 255f;
                                    float r = ((col >> 16) & 0xFF) / 255f;
                                    float g = ((col >> 8) & 0xFF) / 255f;
                                    float b = (col & 0xFF) / 255f;
                                    if (a == 0 && r == 0 && g == 0 && b == 0) a = 1.0f; // default opacity

                                    var matIdx = gltf.materials.Count;
                                    gltf.materials.Add(new GltfMaterial
                                    {
                                        name = $"Material_Color_0x{col:X8}",
                                        pbrMetallicRoughness = new GltfPbr
                                        {
                                            baseColorFactor = new float[] { r, g, b, a }
                                        }
                                    });
                                    gltfMatIdx = matIdx;
                                }
                            }
                        }

                        // Parse geometry vertices & indices
                        var gltfVertexMap = new Dictionary<(ushort vId, byte uvIdx), ushort>();
                        var positions = new List<float>();
                        var normals = new List<float>();
                        var uvs = new List<float>();
                        var indices = new List<ushort>();

                        foreach (var poly in polys)
                        {
                            // Triangulation using Triangle Fan: (0, k, k+1)
                            for (int k = 1; k < poly.NumPts - 1; k++)
                            {
                                int[] pts = { 0, k, k + 1 };
                                foreach (var pt in pts)
                                {
                                    var vId = (ushort)poly.VertexIds[pt];
                                    var uvIdx = (poly.PosUVIndices != null && pt < poly.PosUVIndices.Count) ? poly.PosUVIndices[pt] : (byte)0;

                                    if (!gltfVertexMap.TryGetValue((vId, uvIdx), out var gltfIdx))
                                    {
                                        gltfIdx = (ushort)gltfVertexMap.Count;
                                        gltfVertexMap[(vId, uvIdx)] = gltfIdx;

                                        var vert = gfxObj.VertexArray.Vertices[vId];
                                        positions.Add(vert.Origin.X);
                                        positions.Add(vert.Origin.Y);
                                        positions.Add(vert.Origin.Z);

                                        normals.Add(vert.Normal.X);
                                        normals.Add(vert.Normal.Y);
                                        normals.Add(vert.Normal.Z);

                                        float u = 0f;
                                        float v = 0f;
                                        if (vert.UVs != null && uvIdx < vert.UVs.Count)
                                        {
                                            u = vert.UVs[uvIdx].U;
                                            v = vert.UVs[uvIdx].V;
                                        }
                                        uvs.Add(u);
                                        uvs.Add(v);
                                    }
                                    indices.Add(gltfIdx);
                                }
                            }
                        }

                        if (indices.Count > 0)
                        {
                            // Pack buffers and write to BufferViews
                            var idxOffset = bufferHelper.AddData(WriteUshorts(indices));
                            var posOffset = bufferHelper.AddData(WriteFloats(positions));
                            var normOffset = bufferHelper.AddData(WriteFloats(normals));
                            var uvOffset = bufferHelper.AddData(WriteFloats(uvs));

                            var viewIdxOffset = gltf.bufferViews.Count;
                            gltf.bufferViews.Add(new GltfBufferView { byteOffset = idxOffset, byteLength = indices.Count * 2, target = 34963 }); // ELEMENT_ARRAY_BUFFER
                            var viewPosOffset = gltf.bufferViews.Count;
                            gltf.bufferViews.Add(new GltfBufferView { byteOffset = posOffset, byteLength = positions.Count * 4, target = 34962 }); // ARRAY_BUFFER
                            var viewNormOffset = gltf.bufferViews.Count;
                            gltf.bufferViews.Add(new GltfBufferView { byteOffset = normOffset, byteLength = normals.Count * 4, target = 34962 });
                            var viewUvOffset = gltf.bufferViews.Count;
                            gltf.bufferViews.Add(new GltfBufferView { byteOffset = uvOffset, byteLength = uvs.Count * 4, target = 34962 });

                            // Add Accessors
                            var accIdx = gltf.accessors.Count;
                            gltf.accessors.Add(new GltfAccessor { bufferView = viewIdxOffset, componentType = 5123, count = indices.Count, type = "SCALAR" });
                            var accPos = gltf.accessors.Count;
                            gltf.accessors.Add(new GltfAccessor { bufferView = viewPosOffset, componentType = 5126, count = positions.Count / 3, type = "VEC3" });
                            var accNorm = gltf.accessors.Count;
                            gltf.accessors.Add(new GltfAccessor { bufferView = viewNormOffset, componentType = 5126, count = normals.Count / 3, type = "VEC3" });
                            var accUv = gltf.accessors.Count;
                            gltf.accessors.Add(new GltfAccessor { bufferView = viewUvOffset, componentType = 5126, count = uvs.Count / 2, type = "VEC2" });

                            var prim = new GltfPrimitive
                            {
                                indices = accIdx,
                                material = gltfMatIdx
                            };
                            prim.attributes["POSITION"] = accPos;
                            prim.attributes["NORMAL"] = accNorm;
                            prim.attributes["TEXCOORD_0"] = accUv;

                            gltfMesh.primitives.Add(prim);
                        }
                    }

                    if (gltfMesh.primitives.Count > 0)
                    {
                        partNode.mesh = gltf.meshes.Count;
                        gltf.meshes.Add(gltfMesh);
                    }
                }

                gltf.nodes.Add(partNode);
            }

            // Build scene graph hierarchy - flat layout since frames define absolute setup-space transforms
            for (int i = 0; i < setupModel.Parts.Count; i++)
            {
                var partNodeIdx = i + 1;
                rootNode.children ??= new List<int>();
                rootNode.children.Add(partNodeIdx);
            }

            // Write out binary buffers as Base64 Data URI
            var rawBufferBytes = bufferHelper.ToArray();
            gltf.buffers.Add(new GltfBuffer
            {
                uri = "data:application/octet-stream;base64," + Convert.ToBase64String(rawBufferBytes),
                byteLength = rawBufferBytes.Length
            });

            var json = JsonSerializer.Serialize(gltf, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public static async Task<byte[]> GetTexturePngBytesAsync(uint textureId, uint wcid = 0, uint paletteId = 0, float shade = 0.5f, int hueShift = 0, int paletteSlot = -1)
        {
            string cacheKey = $"{textureId}_wcid_{wcid}_pal_0x{paletteId:X8}_slot_{paletteSlot}_hue_{hueShift}";
            var cachePath = Path.Combine(TexturesCacheDir, $"{cacheKey}.png");
            if (File.Exists(cachePath))
            {
                return await File.ReadAllBytesAsync(cachePath);
            }

            var texLock = GetLock($"tex_{cacheKey}");
            await texLock.WaitAsync();
            try
            {
                if (File.Exists(cachePath))
                {
                    return await File.ReadAllBytesAsync(cachePath);
                }

                var bytes = ExportTexturePng(textureId, wcid, paletteId, shade, hueShift, paletteSlot);
                if (bytes == null) return null;

                // Write atomically
                var tempPath = Path.Combine(TexturesCacheDir, $"temp_{Guid.NewGuid()}.png");
                await File.WriteAllBytesAsync(tempPath, bytes);
                File.Move(tempPath, cachePath, overwrite: true);

                return bytes;
            }
            finally
            {
                texLock.Release();
            }
        }

        private static byte[] ExportTexturePng(uint textureId, uint wcid = 0, uint paletteId = 0, float shade = 0.5f, int hueShift = 0, int paletteSlot = -1)
        {
            var portalDb = new PortalDatDatabase(DatManager.PortalDat.FilePath, keepOpen: false);

            if ((textureId & 0xFF000000) == 0x05000000)
            {
                var surfTex = portalDb.ReadFromDat<SurfaceTexture>(textureId);
                if (surfTex != null && surfTex.Textures.Count > 0)
                {
                    textureId = surfTex.Textures[0];
                }
                else if (DatManager.HighResDat != null)
                {
                    var highResDb = new PortalDatDatabase(DatManager.HighResDat.FilePath, keepOpen: false);
                    surfTex = highResDb.ReadFromDat<SurfaceTexture>(textureId);
                    if (surfTex != null && surfTex.Textures.Count > 0)
                        textureId = surfTex.Textures[0];
                }
            }

            var texture = portalDb.ReadFromDat<Texture>(textureId);
            if (texture == null || texture.SourceData == null || texture.SourceData.Length == 0)
            {
                if (DatManager.HighResDat != null)
                {
                    var highResDb = new PortalDatDatabase(DatManager.HighResDat.FilePath, keepOpen: false);
                    texture = highResDb.ReadFromDat<Texture>(textureId);
                }
            }

            if (texture == null || texture.SourceData == null || texture.SourceData.Length == 0)
                return null;

            if (texture.Format == SurfacePixelFormat.PFID_P8 || texture.Format == SurfacePixelFormat.PFID_INDEX16)
            {
                int width = texture.Width;
                int height = texture.Height;
                byte[] rgba8 = new byte[width * height * 4];

                // Subpalette baking
                Palette basePalette = null;
                List<CloSubPalette> cloSubPalettes = null;
                double weenieShade = 0.5;

                if (wcid != 0)
                {
                    var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
                    if (weenie != null)
                    {
                        uint clothingBase = 0;
                        if (weenie.PropertiesDID != null && weenie.PropertiesDID.TryGetValue(PropertyDataId.ClothingBase, out clothingBase))
                        {
                            int palTemplate = 0;
                            if (paletteId != 0 && (paletteId & 0xFF000000) != 0x04000000)
                            {
                                palTemplate = (int)paletteId;
                            }
                            else if (weenie.PropertiesInt != null && weenie.PropertiesInt.TryGetValue(PropertyInt.PaletteTemplate, out palTemplate))
                            {
                            }

                            if (weenie.PropertiesFloat != null && weenie.PropertiesFloat.TryGetValue(PropertyFloat.Shade, out weenieShade))
                            {
                                shade = (float)weenieShade;
                            }

                            var clothingTable = portalDb.ReadFromDat<ClothingTable>(clothingBase);
                            if (clothingTable != null)
                            {
                                if (clothingTable.ClothingSubPalEffects != null && clothingTable.ClothingSubPalEffects.TryGetValue((uint)palTemplate, out var effect))
                                {
                                    cloSubPalettes = effect.CloSubPalettes;
                                }
                            }
                        }
                    }
                }

                uint paletteBase = 0;
                if ((paletteId & 0xFF000000) == 0x04000000)
                {
                    paletteBase = paletteId;
                }
                else if (wcid != 0)
                {
                    var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
                    if (weenie != null && weenie.PropertiesDID != null && weenie.PropertiesDID.TryGetValue(PropertyDataId.PaletteBase, out paletteBase))
                    {
                    }
                }

                if (paletteBase != 0)
                {
                    basePalette = portalDb.ReadFromDat<Palette>(paletteBase);
                }

                if (basePalette == null && texture.DefaultPaletteId != null && texture.DefaultPaletteId.Value != 0)
                {
                    basePalette = portalDb.ReadFromDat<Palette>(texture.DefaultPaletteId.Value);
                }

                if ((paletteId & 0xFF000000) == 0x04000000)
                {
                    var overridePalette = portalDb.ReadFromDat<Palette>(paletteId);
                    if (overridePalette != null && overridePalette.Colors.Count > 0)
                    {
                        int targetLength = Math.Max(2048, basePalette?.Colors.Count ?? 256);
                        if (basePalette == null)
                        {
                            basePalette = new Palette();
                            basePalette.Colors.Capacity = targetLength;
                            while (basePalette.Colors.Count < targetLength)
                                basePalette.Colors.Add(0xFFFFFFFF);
                        }
                        else if (basePalette.Colors.Count < targetLength)
                        {
                            basePalette.Colors.Capacity = targetLength;
                            while (basePalette.Colors.Count < targetLength)
                                basePalette.Colors.Add(0xFFFFFFFF);
                        }

                        int startIdx = 0;
                        int endIdx = basePalette.Colors.Count;

                        if (paletteSlot == 1) { startIdx = 0; endIdx = Math.Min(256, basePalette.Colors.Count); }
                        else if (paletteSlot == 2) { startIdx = 256; endIdx = Math.Min(512, basePalette.Colors.Count); }
                        else if (paletteSlot == 3) { startIdx = 512; endIdx = Math.Min(768, basePalette.Colors.Count); }
                        else if (paletteSlot == 4) { startIdx = 768; endIdx = Math.Min(1024, basePalette.Colors.Count); }

                        for (int i = startIdx; i < endIdx; i++)
                        {
                            basePalette.Colors[i] = overridePalette.Colors[(i % 256) % overridePalette.Colors.Count];
                        }

                        if (paletteSlot <= 0 || paletteSlot == -1)
                        {
                            cloSubPalettes = null;
                        }
                    }
                }
                else if ((paletteId & 0xFF000000) == 0x0F000000)
                {
                    // PaletteSet Direct Override (0x0F...)
                    var overridePalSet = portalDb.ReadFromDat<PaletteSet>(paletteId);
                    if (overridePalSet != null && overridePalSet.PaletteList.Count > 0)
                    {
                        uint subPalId = overridePalSet.GetPaletteID(shade);
                        if (subPalId != 0)
                        {
                            var subPaletteData = portalDb.ReadFromDat<Palette>(subPalId);
                            if (subPaletteData != null && basePalette != null)
                            {
                                int limit = Math.Min(basePalette.Colors.Count, subPaletteData.Colors.Count);
                                for (int i = 0; i < limit; i++)
                                {
                                    basePalette.Colors[i] = subPaletteData.Colors[i];
                                }
                            }
                        }
                    }
                }

                if (cloSubPalettes != null && basePalette != null)
                {
                    foreach (var subPal in cloSubPalettes)
                    {
                        var paletteSetId = subPal.PaletteSet;
                        if (paletteSetId != 0)
                        {
                            var paletteSet = portalDb.ReadFromDat<PaletteSet>(paletteSetId);
                            if (paletteSet != null && paletteSet.PaletteList.Count > 0)
                            {
                                uint subPalId = paletteSet.GetPaletteID(shade);
                                if (subPalId != 0)
                                {
                                    var subPaletteData = portalDb.ReadFromDat<Palette>(subPalId);
                                    if (subPaletteData != null)
                                    {
                                        foreach (var range in subPal.Ranges)
                                        {
                                            int offset = (int)range.Offset;
                                            int numColors = (int)range.NumColors;
                                            for (int c = 0; c < numColors; c++)
                                            {
                                                int idx = offset + c;
                                                if (idx < basePalette.Colors.Count && idx < subPaletteData.Colors.Count)
                                                {
                                                    basePalette.Colors[idx] = subPaletteData.Colors[idx];
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (hueShift != 0 && basePalette != null)
                {
                    for (int i = 0; i < basePalette.Colors.Count; i++)
                    {
                        var col = basePalette.Colors[i];
                        byte a = (byte)((col >> 24) & 0xFF);
                        byte r = (byte)((col >> 16) & 0xFF);
                        byte g = (byte)((col >> 8) & 0xFF);
                        byte b = (byte)(col & 0xFF);

                        ColorToHSV(r, g, b, out double h, out double s, out double v);
                        h = (h + hueShift) % 360.0;
                        if (h < 0) h += 360.0;
                        ColorFromHSV(h, s, v, out byte nr, out byte ng, out byte nb);

                        basePalette.Colors[i] = ((uint)a << 24) | ((uint)nr << 16) | ((uint)ng << 8) | (uint)nb;
                    }
                }

                uint GetColor(ushort index)
                {
                    if (basePalette != null && index < basePalette.Colors.Count)
                        return basePalette.Colors[index];
                    return 0xFFFFFFFF;
                }

                // Check ClipMap surface type (or wing / translucent textures)
                bool isClipMap = (textureId == 0x0600406A) || (texture.Format == SurfacePixelFormat.PFID_INDEX16 && width == height && width <= 128);

                if (texture.Format == SurfacePixelFormat.PFID_P8)
                {
                    for (int i = 0; i < width * height; i++)
                    {
                        if (i >= texture.SourceData.Length) break;
                        byte index = texture.SourceData[i];
                        uint color = GetColor(index);

                        if (isClipMap && index < 8)
                        {
                            rgba8[i * 4] = 0; rgba8[i * 4 + 1] = 0; rgba8[i * 4 + 2] = 0; rgba8[i * 4 + 3] = 0;
                        }
                        else
                        {
                            rgba8[i * 4] = (byte)((color >> 16) & 0xFF);     // R
                            rgba8[i * 4 + 1] = (byte)((color >> 8) & 0xFF);   // G
                            rgba8[i * 4 + 2] = (byte)(color & 0xFF);          // B
                            rgba8[i * 4 + 3] = (byte)((color >> 24) & 0xFF);  // A
                        }
                    }
                }
                else // PFID_INDEX16
                {
                    using var reader = new BinaryReader(new MemoryStream(texture.SourceData));
                    for (int i = 0; i < width * height; i++)
                    {
                        if (reader.BaseStream.Position + 2 > reader.BaseStream.Length) break;
                        ushort val = reader.ReadUInt16();
                        uint color = GetColor(val);

                        if (isClipMap && val < 8)
                        {
                            rgba8[i * 4] = 0; rgba8[i * 4 + 1] = 0; rgba8[i * 4 + 2] = 0; rgba8[i * 4 + 3] = 0;
                        }
                        else
                        {
                            rgba8[i * 4] = (byte)((color >> 16) & 0xFF);
                            rgba8[i * 4 + 1] = (byte)((color >> 8) & 0xFF);
                            rgba8[i * 4 + 2] = (byte)(color & 0xFF);
                            rgba8[i * 4 + 3] = (byte)((color >> 24) & 0xFF);
                        }
                    }
                }

                using var image = Image.LoadPixelData<Rgba32>(rgba8, width, height);
                using var ms = new MemoryStream();
                image.SaveAsPng(ms);
                return ms.ToArray();
            }
            else
            {
                return IconService.GetIcon(textureId);
            }
        }

        private static void ColorToHSV(byte r, byte g, byte b, out double h, out double s, out double v)
        {
            double min = Math.Min(Math.Min(r, g), b) / 255.0;
            v = Math.Max(Math.Max(r, g), b) / 255.0;
            double delta = v - min;

            if (v == 0.0) s = 0;
            else s = delta / v;

            if (s == 0) h = 0.0;
            else
            {
                double r1 = r / 255.0, g1 = g / 255.0, b1 = b / 255.0;
                if (r1 == v) h = (g1 - b1) / delta;
                else if (g1 == v) h = 2 + (b1 - r1) / delta;
                else h = 4 + (r1 - g1) / delta;

                h *= 60;
                if (h < 0.0) h += 360;
            }
        }

        private static void ColorFromHSV(double h, double s, double v, out byte r, out byte g, out byte b)
        {
            if (s == 0)
            {
                r = g = b = (byte)(v * 255);
                return;
            }
            double hh = h;
            if (hh >= 360.0) hh = 0.0;
            hh /= 60.0;
            long i = (long)hh;
            double ff = hh - i;
            double p = v * (1.0 - s);
            double q = v * (1.0 - (s * ff));
            double t = v * (1.0 - (s * (1.0 - ff)));

            double rOut, gOut, bOut;
            switch (i)
            {
                case 0: rOut = v; gOut = t; bOut = p; break;
                case 1: rOut = q; gOut = v; bOut = p; break;
                case 2: rOut = p; gOut = v; bOut = t; break;
                case 3: rOut = p; gOut = q; bOut = v; break;
                case 4: rOut = t; gOut = p; bOut = v; break;
                default: rOut = v; gOut = p; bOut = q; break;
            }
            r = (byte)(rOut * 255.0);
            g = (byte)(gOut * 255.0);
            b = (byte)(bOut * 255.0);
        }

        private static byte[] ExportPalettePng(uint paletteId)
        {
            var portalDb = new PortalDatDatabase(DatManager.PortalDat.FilePath, keepOpen: false);

            var palette = portalDb.ReadFromDat<Palette>(paletteId);
            if (palette == null) return null;

            byte[] rgba8 = new byte[256 * 1 * 4];
            for (int i = 0; i < 256; i++)
            {
                if (i < palette.Colors.Count)
                {
                    var color = palette.Colors[i];
                    rgba8[i * 4] = (byte)((color >> 16) & 0xFF);     // R
                    rgba8[i * 4 + 1] = (byte)((color >> 8) & 0xFF);   // G
                    rgba8[i * 4 + 2] = (byte)(color & 0xFF);          // B
                    rgba8[i * 4 + 3] = (byte)((color >> 24) & 0xFF);  // A
                }
                else
                {
                    rgba8[i * 4] = 0;
                    rgba8[i * 4 + 1] = 0;
                    rgba8[i * 4 + 2] = 0;
                    rgba8[i * 4 + 3] = 0;
                }
            }

            using var image = Image.LoadPixelData<Rgba32>(rgba8, 256, 1);
            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }

        private static byte[] WriteFloats(List<float> floats)
        {
            var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms))
            {
                foreach (var f in floats)
                    bw.Write(f);
            }
            return ms.ToArray();
        }

        private static byte[] WriteUshorts(List<ushort> ushorts)
        {
            var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms))
            {
                foreach (var us in ushorts)
                    bw.Write(us);
            }
            return ms.ToArray();
        }

        #endregion

        #region GLTF Output Models

        private class GltfRoot
        {
            public GltfAsset asset { get; set; } = new GltfAsset();
            public int scene { get; set; } = 0;
            public List<GltfScene> scenes { get; set; } = new List<GltfScene>();
            public List<GltfNode> nodes { get; set; } = new List<GltfNode>();
            public List<GltfMesh> meshes { get; set; } = new List<GltfMesh>();
            public List<GltfMaterial> materials { get; set; } = new List<GltfMaterial>();
            public List<GltfTexture> textures { get; set; } = new List<GltfTexture>();
            public List<GltfImage> images { get; set; } = new List<GltfImage>();
            public List<GltfBuffer> buffers { get; set; } = new List<GltfBuffer>();
            public List<GltfBufferView> bufferViews { get; set; } = new List<GltfBufferView>();
            public List<GltfAccessor> accessors { get; set; } = new List<GltfAccessor>();
        }

        private class GltfAsset
        {
            public string generator { get; set; } = "ACE Visualizer Exporter";
            public string version { get; set; } = "2.0";
        }

        private class GltfScene
        {
            public List<int> nodes { get; set; } = new List<int>();
        }

        private class GltfNode
        {
            public string name { get; set; }
            public int? mesh { get; set; }
            public List<int> children { get; set; }
            public float[] translation { get; set; }
            public float[] rotation { get; set; }
            public float[] scale { get; set; }
        }

        private class GltfMesh
        {
            public string name { get; set; }
            public List<GltfPrimitive> primitives { get; set; } = new List<GltfPrimitive>();
        }

        private class GltfPrimitive
        {
            public Dictionary<string, int> attributes { get; set; } = new Dictionary<string, int>();
            public int indices { get; set; }
            public int? material { get; set; }
            public int mode { get; set; } = 4; // TRIANGLES
        }

        private class GltfMaterial
        {
            public string name { get; set; }
            public GltfPbr pbrMetallicRoughness { get; set; } = new GltfPbr();
            public double alphaCutoff { get; set; } = 0.5;
            public string alphaMode { get; set; } = "MASK";
            public bool doubleSided { get; set; } = true;
            public Dictionary<string, object> extras { get; set; }
        }

        private class GltfPbr
        {
            public float[] baseColorFactor { get; set; } = new float[] { 1f, 1f, 1f, 1f };
            public GltfTextureInfo baseColorTexture { get; set; }
            public float metallicFactor { get; set; } = 0.0f;
            public float roughnessFactor { get; set; } = 1.0f;
        }

        private class GltfTextureInfo
        {
            public int index { get; set; }
        }

        private class GltfTexture
        {
            public int source { get; set; }
        }

        private class GltfImage
        {
            public string uri { get; set; }
        }

        private class GltfBuffer
        {
            public string uri { get; set; }
            public int byteLength { get; set; }
        }

        private class GltfBufferView
        {
            public int buffer { get; set; } = 0;
            public int byteOffset { get; set; }
            public int byteLength { get; set; }
            public int? target { get; set; }
        }

        private class GltfAccessor
        {
            public int bufferView { get; set; }
            public int byteOffset { get; set; } = 0;
            public int componentType { get; set; }
            public int count { get; set; }
            public string type { get; set; }
            public float[] max { get; set; }
            public float[] min { get; set; }
        }

        private class BinaryBufferHelper
        {
            private readonly MemoryStream _ms = new();

            public int AddData(byte[] bytes)
            {
                var offset = (int)_ms.Position;
                var padding = (4 - (offset % 4)) % 4;
                for (int i = 0; i < padding; i++)
                {
                    _ms.WriteByte(0);
                }
                offset = (int)_ms.Position;
                _ms.Write(bytes, 0, bytes.Length);
                return offset;
            }

            public byte[] ToArray() => _ms.ToArray();
        }

        #endregion

        #region Cache LRU Eviction

        /// <summary>
        /// Cleans up the cache directory if it exceeds size limits.
        /// </summary>
        public static void PerformEvictionIfNeeded(long maxCacheSizeBytes = 2L * 1024L * 1024L * 1024L) // 2.0 GB default
        {
            try
            {
                if (!Directory.Exists(CacheDir)) return;

                var files = new List<FileInfo>();
                long totalSize = 0;

                foreach (var dir in new[] { ModelsCacheDir, TexturesCacheDir, PalettesCacheDir })
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        var info = new FileInfo(file);
                        if (info.Name.StartsWith("temp_")) continue;
                        files.Add(info);
                        totalSize += info.Length;
                    }
                }

                if (totalSize <= maxCacheSizeBytes) return;

                log.Info($"[WEB PORTAL] Visualizer cache directory size ({totalSize / 1024.0 / 1024.0:F2} MB) exceeds limit. Evicting LRU files...");

                files.Sort((a, b) => a.LastAccessTimeUtc.CompareTo(b.LastAccessTimeUtc));

                foreach (var file in files)
                {
                    try
                    {
                        var len = file.Length;
                        file.Delete();
                        totalSize -= len;
                        if (totalSize <= maxCacheSizeBytes) break;
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"Failed to evict cache file {file.Name}: {ex.Message}");
                    }
                }

                log.Info($"[WEB PORTAL] Cache eviction complete. New size: {totalSize / 1024.0 / 1024.0:F2} MB.");
            }
            catch (Exception ex)
            {
                log.Error($"Exception during visualizer cache eviction: {ex.Message}");
            }
        }

        #region CIELAB Delta-E (CIEDE2000) Similar Palette Engine

        public static List<SmartPaletteDto> GetSimilarPalettes(uint paletteId, int count = 6)
        {
            var result = new List<SmartPaletteDto>();
            if (paletteId == 0) return result;

            var portalDb = new PortalDatDatabase(DatManager.PortalDat.FilePath, keepOpen: false);

            // Resolve palette if paletteSetId was passed
            if ((paletteId & 0xFF000000) == 0x0F000000)
            {
                var palSet = portalDb.ReadFromDat<PaletteSet>(paletteId);
                if (palSet != null && palSet.PaletteList.Count > 0)
                    paletteId = palSet.GetPaletteID(0.5f);
            }

            var srcPal = portalDb.ReadFromDat<Palette>(paletteId);
            if (srcPal == null || srcPal.Colors == null || srcPal.Colors.Count == 0)
                return result;

            // Extract 8 target swatches in CIELAB space
            var srcLabs = ExtractSwatchesLab(srcPal);

            // Fetch pre-filtered smart pool of palettes
            var candidates = GetSmartPalettePool(25749, "all");

            var scored = new List<(SmartPaletteDto Dto, double Distance)>();
            foreach (var cand in candidates)
            {
                if (cand.PaletteId == paletteId) continue;

                var candPal = portalDb.ReadFromDat<Palette>(cand.PaletteId);
                if (candPal == null || candPal.Colors == null || candPal.Colors.Count == 0) continue;

                var candLabs = ExtractSwatchesLab(candPal);
                double totalDist = 0;
                int minLen = Math.Min(srcLabs.Count, candLabs.Count);
                for (int i = 0; i < minLen; i++)
                {
                    totalDist += Ciede2000(srcLabs[i], candLabs[i]);
                }

                scored.Add((cand, totalDist));
            }

            return scored.OrderBy(s => s.Distance).Take(count).Select(s => s.Dto).ToList();
        }

        private static List<(double L, double a, double b)> ExtractSwatchesLab(Palette pal)
        {
            var labs = new List<(double L, double a, double b)>();
            int step = Math.Max(1, pal.Colors.Count / 8);
            for (int i = 0; i < pal.Colors.Count && labs.Count < 8; i += step)
            {
                labs.Add(RgbToLab(pal.Colors[i]));
            }
            return labs;
        }

        private static (double L, double a, double b) RgbToLab(uint argb)
        {
            double r = ((argb >> 16) & 0xFF) / 255.0;
            double g = ((argb >> 8) & 0xFF) / 255.0;
            double b = (argb & 0xFF) / 255.0;

            r = (r > 0.04045) ? Math.Pow((r + 0.055) / 1.055, 2.4) : r / 12.92;
            g = (g > 0.04045) ? Math.Pow((g + 0.055) / 1.055, 2.4) : g / 12.92;
            b = (b > 0.04045) ? Math.Pow((b + 0.055) / 1.055, 2.4) : b / 12.92;

            double x = (r * 0.4124 + g * 0.3576 + b * 0.1805) * 100.0 / 95.047;
            double y = (r * 0.2126 + g * 0.7152 + b * 0.0722) * 100.0 / 100.000;
            double z = (r * 0.0193 + g * 0.1192 + b * 0.9505) * 100.0 / 108.883;

            x = (x > 0.008856) ? Math.Pow(x, 1.0 / 3.0) : (7.787 * x) + (16.0 / 116.0);
            y = (y > 0.008856) ? Math.Pow(y, 1.0 / 3.0) : (7.787 * y) + (16.0 / 116.0);
            z = (z > 0.008856) ? Math.Pow(z, 1.0 / 3.0) : (7.787 * z) + (16.0 / 116.0);

            double L = (116.0 * y) - 16.0;
            double a = 500.0 * (x - y);
            double bVal = 200.0 * (y - z);

            return (L, a, bVal);
        }

        private static double Ciede2000((double L, double a, double b) c1, (double L, double a, double b) c2)
        {
            double dL = c2.L - c1.L;
            double da = c2.a - c1.a;
            double db = c2.b - c1.b;
            return Math.Sqrt(dL * dL + da * da + db * db);
        }

        #endregion

        #endregion
    }
}
