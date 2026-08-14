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
        public bool IsDefault { get; set; }
    }

    public class SpeciesPresetDto
    {
        public uint Wcid { get; set; }
        public string Name { get; set; }
        public string Species { get; set; }
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
        public int ConfidenceScore { get; set; } = 80;
        public string TierGrade { get; set; } = "S";
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
    }

    public class ParticleEmitterDto
    {
        public uint EmitterId { get; set; }
        public string HexId => $"0x{EmitterId:X8}";
        public int EmitterType { get; set; }
        public int ParticleType { get; set; }
        public uint GfxObjId { get; set; }
        public double Birthrate { get; set; }
        public int MaxParticles { get; set; }
        public double Lifespan { get; set; }
        public double LifespanRand { get; set; }
        public float[] OffsetDir { get; set; } = new float[3];
        public float MinOffset { get; set; }
        public float MaxOffset { get; set; }
        public float[] VelocityA { get; set; } = new float[3];
        public float MinA { get; set; }
        public float MaxA { get; set; }
        public float[] VelocityB { get; set; } = new float[3];
        public float MinB { get; set; }
        public float MaxB { get; set; }
        public float[] VelocityC { get; set; } = new float[3];
        public float MinC { get; set; }
        public float MaxC { get; set; }
        public float StartScale { get; set; }
        public float FinalScale { get; set; }
        public float ScaleRand { get; set; }
        public float StartTrans { get; set; }
        public float FinalTrans { get; set; }
        public float TransRand { get; set; }
        public int IsParentLocal { get; set; }
    }

    public class CreatureParticleHookDto
    {
        public uint EmitterId { get; set; }
        public string HexId => $"0x{EmitterId:X8}";
        public int PartIndex { get; set; }
        public ParticleEmitterDto Emitter { get; set; }
    }

    public static class VisualizerService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(VisualizerService));

        // Concurrency locks per asset ID
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private static readonly ReaderWriterLockSlim _cacheLock = new ReaderWriterLockSlim();

        private static string CacheDir => Path.Combine(AppContext.BaseDirectory, "wwwroot", "visualizer_cache");

        private static string ModelsCacheDir => Path.Combine(CacheDir, "models");
        private static string TexturesCacheDir => Path.Combine(CacheDir, "textures");
        private static string PalettesCacheDir => Path.Combine(CacheDir, "palettes");

        static VisualizerService()
        {
            try
            {
                if (Directory.Exists(CacheDir))
                {
                    Directory.Delete(CacheDir, true);
                }
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

                var (score, tier) = CalculatePaletteConfidenceScore(palette);

                results.Add(new SmartPaletteDto
                {
                    PaletteId = fileId,
                    HexId = $"0x{fileId:X8}",
                    Family = family,
                    Swatches = swatches,
                    ConfidenceScore = score,
                    TierGrade = tier
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
            var portalDb = new PortalDatDatabase(DatManager.PortalDat.FilePath, keepOpen: false);

            var weenie = DatabaseManager.World?.GetCachedWeenie(wcid);
            uint clothingBase = 0;
            uint paletteBase = 0;
            uint defaultTemplate = 0;
            float shade = 0.5f;

            if (weenie != null && weenie.PropertiesDID != null && weenie.PropertiesDID.Count > 0)
            {
                weenie.PropertiesDID.TryGetValue(PropertyDataId.ClothingBase, out clothingBase);
                weenie.PropertiesDID.TryGetValue(PropertyDataId.PaletteBase, out paletteBase);

                if (paletteBase > 0 && paletteBase < 0x01000000 && defaultTemplate == 0)
                    defaultTemplate = paletteBase;

                if (weenie.PropertiesInt != null && weenie.PropertiesInt.TryGetValue(PropertyInt.PaletteTemplate, out int pTemp))
                    defaultTemplate = (uint)pTemp;
                if (weenie.PropertiesFloat != null && weenie.PropertiesFloat.TryGetValue(PropertyFloat.Shade, out double weenieShade))
                    shade = (float)weenieShade;
            }

            if (paletteBase == 0 || clothingBase == 0)
            {
                var dbWeenie = DatabaseManager.World?.GetWeenie(wcid);
                if (dbWeenie != null)
                {
                    if (dbWeenie.WeeniePropertiesDID != null)
                    {
                        foreach (var prop in dbWeenie.WeeniePropertiesDID)
                        {
                            if (prop.Type == (ushort)PropertyDataId.ClothingBase && clothingBase == 0) clothingBase = prop.Value;
                            if (prop.Type == (ushort)PropertyDataId.PaletteBase && paletteBase == 0) 
                            {
                                paletteBase = prop.Value;
                                if (paletteBase > 0 && paletteBase < 0x01000000 && defaultTemplate == 0)
                                    defaultTemplate = paletteBase;
                            }
                        }
                    }
                    if (dbWeenie.WeeniePropertiesInt != null && defaultTemplate == 0)
                    {
                        foreach (var prop in dbWeenie.WeeniePropertiesInt)
                        {
                            if (prop.Type == (ushort)PropertyInt.PaletteTemplate) defaultTemplate = (uint)prop.Value;
                        }
                    }
                    if (dbWeenie.WeeniePropertiesFloat != null && shade == 0.5f)
                    {
                        foreach (var prop in dbWeenie.WeeniePropertiesFloat)
                        {
                            if (prop.Type == (ushort)PropertyFloat.Shade) shade = (float)prop.Value;
                        }
                    }
                }
            }

            log.Info($"[GET SPECIES PALETTES] wcid={wcid}, cachedWeenie={(weenie != null)}, clothingBase=0x{clothingBase:X8}, paletteBase=0x{paletteBase:X8}, defaultTemplate={defaultTemplate}, shade={shade}");

            if (clothingBase != 0)
            {
                var clothingTable = portalDb.ReadFromDat<ClothingTable>(clothingBase);
                var customNames = MergeCustomClothingBaseJson(clothingBase, clothingTable);

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

                        Palette resolvedPal = null;
                        try
                        {
                            resolvedPal = portalDb.ReadFromDat<Palette>(finalPaletteId);
                        }
                        catch
                        {
                            try
                            {
                                var palSet = portalDb.ReadFromDat<PaletteSet>(finalPaletteId);
                                if (palSet != null)
                                {
                                    uint subPalId = palSet.GetPaletteID(0.5f);
                                    if (subPalId != 0)
                                        resolvedPal = portalDb.ReadFromDat<Palette>(subPalId);
                                }
                            }
                            catch (Exception ex)
                            {
                                log.Warn($"Failed to read palette 0x{finalPaletteId:X8}: {ex.Message}");
                            }
                        }

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

                        string name = customNames.TryGetValue(templateId, out var customName) ? $"Variant {templateId} ({customName})" : $"Variant {templateId}";
                        bool isDefault = (defaultTemplate != 0 && templateId == defaultTemplate);
                        if (isDefault) name += " ⭐ [Default]";

                        result.Add(new SpeciesPaletteDto 
                        { 
                            TemplateId = templateId, 
                            Name = name, 
                            PaletteSetId = paletteSetId,
                            PaletteId = finalPaletteId,
                            Swatches = swatches,
                            Ranges = rangesStr,
                            IsDefault = isDefault
                        });
                    }
                }
            }

            // Fallback for creatures without ClothingBase (e.g. Tusker Protector WCID 36967, Rynthid Nullifier WCID 420600)
            if (result.Count == 0 && paletteBase != 0)
            {
                uint paletteSetId = (paletteBase & 0xFF000000) == 0x0F000000 ? paletteBase : 0;
                uint paletteId = (paletteBase & 0xFF000000) == 0x04000000 ? paletteBase : 0;

                if (paletteSetId != 0)
                {
                    var palSet = portalDb.ReadFromDat<PaletteSet>(paletteSetId);
                    if (palSet != null && palSet.PaletteList != null && palSet.PaletteList.Count > 0)
                    {
                        uint defaultPalId = palSet.GetPaletteID(shade);
                        if (defaultPalId == 0) defaultPalId = palSet.PaletteList[0];

                        for (int pIdx = 0; pIdx < palSet.PaletteList.Count; pIdx++)
                        {
                            uint currentPalId = palSet.PaletteList[pIdx];
                            bool isDefault = (currentPalId == defaultPalId) || (pIdx == 0 && defaultPalId == 0);

                            var swatches = new List<string>();
                            var resolvedPal = portalDb.ReadFromDat<Palette>(currentPalId);
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
                                TemplateId = currentPalId,
                                Name = isDefault ? $"Species Palette 0x{currentPalId:X8} ⭐ [Default]" : $"Species Palette 0x{currentPalId:X8}",
                                PaletteSetId = paletteSetId,
                                PaletteId = currentPalId,
                                Swatches = swatches,
                                Ranges = $"Palette #{pIdx + 1}",
                                IsDefault = isDefault
                            });
                        }
                    }
                }
                else if (paletteId != 0)
                {
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
                        TemplateId = paletteId,
                        Name = "Native Species Palette ⭐ [Default]",
                        PaletteSetId = 0,
                        PaletteId = paletteId,
                        Swatches = swatches,
                        Ranges = "Native Subpalette",
                        IsDefault = true
                    });
                }
            }

            return result;
        }

        private static Dictionary<uint, string> MergeCustomClothingBaseJson(uint clothingBaseId, ClothingTable clothingTable)
        {
            var customNames = new Dictionary<uint, string>();
            if (clothingTable == null) return customNames;

            string jsonPath = $@"C:\ACE\Mods\CustomClothingBase\json\{clothingBaseId:X8}.json";
            if (!File.Exists(jsonPath))
            {
                jsonPath = $@"C:\Scripting\CustomClothingBase\json\{clothingBaseId:X8}.json";
            }
            if (!File.Exists(jsonPath)) return customNames;

            try
            {
                string jsonText = File.ReadAllText(jsonPath);
                using (var doc = JsonDocument.Parse(jsonText))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("clothingSubPalEffects", out var subPalElem))
                    {
                        foreach (var prop in subPalElem.EnumerateObject())
                        {
                            if (uint.TryParse(prop.Name, out uint templateId))
                            {
                                var elem = prop.Value;
                                uint icon = 0;
                                if (elem.TryGetProperty("icon", out var iconProp)) icon = iconProp.GetUInt32();

                                string comment = "";
                                if (elem.TryGetProperty("comment", out var commentProp)) comment = commentProp.GetString();
                                else if (elem.TryGetProperty("Comment", out var commentProp2)) comment = commentProp2.GetString();

                                if (!string.IsNullOrEmpty(comment)) customNames[templateId] = comment;

                                var cloSubPalEffectObj = new CloSubPalEffect();
                                if (elem.TryGetProperty("cloSubPalettes", out var cloSubPalsElem))
                                {
                                    foreach (var subElem in cloSubPalsElem.EnumerateArray())
                                    {
                                        uint palSet = 0;
                                        if (subElem.TryGetProperty("paletteSet", out var palSetProp)) palSet = palSetProp.GetUInt32();

                                        var subPalObj = new CloSubPalette { PaletteSet = palSet };

                                        if (subElem.TryGetProperty("ranges", out var rangesElem))
                                        {
                                            foreach (var rElem in rangesElem.EnumerateArray())
                                            {
                                                uint offset = rElem.GetProperty("offset").GetUInt32();
                                                uint numColors = rElem.GetProperty("numColors").GetUInt32();
                                                subPalObj.Ranges.Add(new CloSubPaletteRange { Offset = offset, NumColors = numColors });
                                            }
                                        }

                                        cloSubPalEffectObj.CloSubPalettes.Add(subPalObj);
                                    }
                                }

                                clothingTable.ClothingSubPalEffects[templateId] = cloSubPalEffectObj;
                            }
                        }
                    }
                    if (root.TryGetProperty("clothingBaseEffects", out var baseElem) || root.TryGetProperty("ClothingBaseEffects", out baseElem))
                    {
                        foreach (var prop in baseElem.EnumerateObject())
                        {
                            if (uint.TryParse(prop.Name, out uint setupKey))
                            {
                                var cloBaseEffect = new ClothingBaseEffect();
                                if (prop.Value.TryGetProperty("cloObjectEffects", out var objElem) || prop.Value.TryGetProperty("CloObjectEffects", out objElem))
                                {
                                    foreach (var objItem in objElem.EnumerateArray())
                                    {
                                        uint modelId = 0, index = 0;
                                        if (objItem.TryGetProperty("modelId", out var mProp) || objItem.TryGetProperty("ModelId", out mProp))
                                            modelId = mProp.GetUInt32();
                                        if (objItem.TryGetProperty("index", out var iProp) || objItem.TryGetProperty("Index", out iProp))
                                            index = iProp.GetUInt32();

                                        var cloObjEffect = new CloObjectEffect();
                                        typeof(CloObjectEffect).GetProperty(nameof(CloObjectEffect.ModelId))?.SetValue(cloObjEffect, modelId, null);
                                        typeof(CloObjectEffect).GetProperty(nameof(CloObjectEffect.Index))?.SetValue(cloObjEffect, index, null);

                                        if (objItem.TryGetProperty("cloTextureEffects", out var texElem) || objItem.TryGetProperty("CloTextureEffects", out texElem))
                                        {
                                            foreach (var texItem in texElem.EnumerateArray())
                                            {
                                                uint oldTex = 0, newTex = 0;
                                                if (texItem.TryGetProperty("oldTexture", out var otProp) || texItem.TryGetProperty("OldTexture", out otProp))
                                                    oldTex = otProp.GetUInt32();
                                                if (texItem.TryGetProperty("newTexture", out var ntProp) || texItem.TryGetProperty("NewTexture", out ntProp))
                                                    newTex = ntProp.GetUInt32();

                                                if (newTex != 0)
                                                {
                                                    var cloTexEffect = new CloTextureEffect();
                                                    typeof(CloTextureEffect).GetProperty(nameof(CloTextureEffect.OldTexture))?.SetValue(cloTexEffect, oldTex, null);
                                                    typeof(CloTextureEffect).GetProperty(nameof(CloTextureEffect.NewTexture))?.SetValue(cloTexEffect, newTex, null);
                                                    cloObjEffect.CloTextureEffects.Add(cloTexEffect);
                                                }
                                            }
                                        }

                                        cloBaseEffect.CloObjectEffects.Add(cloObjEffect);
                                    }
                                }

                                clothingTable.ClothingBaseEffects[setupKey] = cloBaseEffect;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Failed to parse CustomClothingBase JSON {jsonPath}: {ex.Message}");
            }

            return customNames;
        }

        public static List<TextureReplacementDto> GetTextureReplacements(uint wcid)
        {
            var result = new List<TextureReplacementDto>();
            var weenie = DatabaseManager.World?.GetCachedWeenie(wcid);
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
                MergeCustomClothingBaseJson(clothingBase, clothingTable);

                if (clothingTable != null && clothingTable.ClothingBaseEffects != null)
                {
                    foreach (var kvp in clothingTable.ClothingBaseEffects)
                    {
                        var effect = kvp.Value;
                        if (effect?.CloObjectEffects == null) continue;

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

            uint setupId = 0;
            var weenie = DatabaseManager.World?.GetCachedWeenie(wcid);
            if (weenie != null && weenie.PropertiesDID != null)
            {
                weenie.PropertiesDID.TryGetValue(PropertyDataId.Setup, out setupId);
            }
            else
            {
                var dbWeenie = DatabaseManager.World?.GetWeenie(wcid);
                if (dbWeenie != null && dbWeenie.WeeniePropertiesDID != null)
                {
                    foreach (var prop in dbWeenie.WeeniePropertiesDID)
                        if (prop.Type == (ushort)PropertyDataId.Setup) setupId = prop.Value;
                }
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

        public static List<TextureLibraryItemDto> GetSimilarTextures(uint textureId)
        {
            var result = new List<TextureLibraryItemDto>();
            var allTextures = GetTextureLibrary("all");

            var match = allTextures.FirstOrDefault(x => x.TextureId == textureId);
            string matchCategory = match?.Category;

            if (!string.IsNullOrEmpty(matchCategory))
            {
                result.AddRange(allTextures.Where(x => x.Category.Equals(matchCategory, StringComparison.OrdinalIgnoreCase)));
            }

            // Always provide at least a varied sample of 6-8 distinct textures if no exact category match
            if (result.Count < 4)
            {
                result.AddRange(allTextures.Take(8));
            }

            return result.GroupBy(x => x.TextureId).Select(g => g.First()).ToList();
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
                new TextureLibraryItemDto { Category = "Chitin/Insect", TextureId = 0x05003306, HexId = "0x05003306", Name = "💀 Skull Mask & Orb Surface" },
                new TextureLibraryItemDto { Category = "Chitin/Insect", TextureId = 0x050030C9, HexId = "0x050030C9", Name = "🐙 Void Tentacle Skin" },
                new TextureLibraryItemDto { Category = "Chitin/Insect", TextureId = 0x0500303D, HexId = "0x0500303D", Name = "🔥 Flame Collar Aura" },

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

        public static void ClearCache()
        {
            _cacheLock.EnterWriteLock();
            try
            {
                if (Directory.Exists(CacheDir))
                {
                    Directory.Delete(CacheDir, recursive: true);
                    Directory.CreateDirectory(CacheDir);
                    Directory.CreateDirectory(ModelsCacheDir);
                    Directory.CreateDirectory(TexturesCacheDir);
                    Directory.CreateDirectory(PalettesCacheDir);
                    log.Info("Visualizer cache cleared successfully.");
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error clearing visualizer cache: {ex.Message}");
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
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
            ACE.Entity.Models.Weenie weenie = null;
            uint setupId = 0;
            uint clothingBase = 0;
            try
            {
                weenie = DatabaseManager.World?.GetCachedWeenie(wcid);
                if (weenie != null && weenie.PropertiesDID != null)
                {
                    weenie.PropertiesDID.TryGetValue(PropertyDataId.Setup, out setupId);
                    weenie.PropertiesDID.TryGetValue(PropertyDataId.ClothingBase, out clothingBase);
                }
            }
            catch { }

            if (setupId == 0 || clothingBase == 0)
            {
                try
                {
                    var dbWeenie = DatabaseManager.World?.GetWeenie(wcid);
                    if (dbWeenie != null && dbWeenie.WeeniePropertiesDID != null)
                    {
                        foreach (var prop in dbWeenie.WeeniePropertiesDID)
                        {
                            if (prop.Type == (ushort)PropertyDataId.Setup && prop.Value != 0 && setupId == 0)
                                setupId = prop.Value;
                            if (prop.Type == (ushort)PropertyDataId.ClothingBase && prop.Value != 0 && clothingBase == 0)
                                clothingBase = prop.Value;
                        }
                    }
                }
                catch { }
            }

            if (setupId == 0 && wcid == 420600)
                setupId = 0x02001BCA;

            if (setupId == 0)
            {
                log.Warn($"Weenie {wcid} does not define a Setup DID.");
                return null;
            }

            // Open separate read-only portal dat handle to avoid lock contention on game server's streamMutex
            var portalDb = new PortalDatDatabase(DatManager.PortalDat.FilePath, keepOpen: false);

            var textureSwaps = new Dictionary<uint, uint>();
            var modelTextureSwaps = new Dictionary<uint, uint>();
            var indexTextureSwaps = new Dictionary<uint, uint>();

            if (clothingBase != 0)
            {
                var clothingTable = portalDb.ReadFromDat<ClothingTable>(clothingBase);
                MergeCustomClothingBaseJson(clothingBase, clothingTable);
                if (clothingTable != null && clothingTable.ClothingBaseEffects != null)
                {
                    foreach (var kvp in clothingTable.ClothingBaseEffects)
                    {
                        if (kvp.Value?.CloObjectEffects == null) continue;
                        foreach (var objEffect in kvp.Value.CloObjectEffects)
                        {
                            if (objEffect.CloTextureEffects == null) continue;

                            foreach (var texEffect in objEffect.CloTextureEffects)
                            {
                                if (texEffect.NewTexture != 0)
                                {
                                    if (texEffect.OldTexture != 0)
                                    {
                                        textureSwaps[texEffect.OldTexture] = texEffect.NewTexture;
                                    }
                                    else
                                    {
                                        if (objEffect.ModelId != 0)
                                            modelTextureSwaps[objEffect.ModelId] = texEffect.NewTexture;

                                        indexTextureSwaps[objEffect.Index] = texEffect.NewTexture;
                                    }
                                    log.Info($"[VISUALIZER DEBUG] Registered Texture Swap: Old=0x{texEffect.OldTexture:X8} ({texEffect.OldTexture}) -> New=0x{texEffect.NewTexture:X8} ({texEffect.NewTexture}) [ModelId=0x{objEffect.ModelId:X8}, Index={objEffect.Index}]");
                                }
                            }
                        }
                    }
                }
            }

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

            var textureToMatKey = new Dictionary<string, int>();

            // Map each setup part to a GLTF node
            for (int i = 0; i < setupModel.Parts.Count; i++)
            {
                var gfxObjId = setupModel.Parts[i];
                if (gfxObjId == 0x010001EC) continue; // Skip AC target selection bounding box dummy mesh
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
                                if (surface.OrigTextureId != 0)
                                {
                                    var originalSwappedTexId = surface.OrigTextureId;

                                    // 1. Prioritize exact OldTexture DID swap (e.g. 10000976.json OldTexture 83899143 -> 83898569 Glowing Pink Tentacles)
                                    if (textureSwaps != null && textureSwaps.TryGetValue(originalSwappedTexId, out uint swappedTexId))
                                    {
                                        log.Info($"[VISUALIZER DEBUG] Part #{i} GfxObj 0x{gfxObjId:X8} Surf #{surfaceIdx}: Swapped OldTexture 0x{surface.OrigTextureId:X8} -> 0x{swappedTexId:X8}");
                                        originalSwappedTexId = swappedTexId;
                                    }
                                    // 2. Check Part Index texture swap (e.g. 10000976.json Index 9 -> 0x05003306 Red Chest Orb)
                                    else if (indexTextureSwaps != null && indexTextureSwaps.TryGetValue((uint)i, out uint indexSwappedTex))
                                    {
                                        log.Info($"[VISUALIZER DEBUG] Part #{i} GfxObj 0x{gfxObjId:X8} Surf #{surfaceIdx}: Swapped Part Index #{i} -> 0x{indexSwappedTex:X8}");
                                        originalSwappedTexId = indexSwappedTex;
                                    }
                                    // 3. Check ModelId GfxObj texture swap
                                    else if (modelTextureSwaps != null && modelTextureSwaps.TryGetValue(gfxObjId, out uint modelSwappedTex))
                                    {
                                        log.Info($"[VISUALIZER DEBUG] Part #{i} GfxObj 0x{gfxObjId:X8} Surf #{surfaceIdx}: Swapped ModelId 0x{gfxObjId:X8} -> 0x{modelSwappedTex:X8}");
                                        originalSwappedTexId = modelSwappedTex;
                                    }

                                    var actualTexId = originalSwappedTexId;
                                    if ((actualTexId & 0xFF000000) == 0x05000000)
                                    {
                                        var surfTex = portalDb.ReadFromDat<SurfaceTexture>(actualTexId);
                                        if (surfTex == null && DatManager.HighResDat != null)
                                        {
                                            var highResDb = new PortalDatDatabase(DatManager.HighResDat.FilePath, keepOpen: false);
                                            surfTex = highResDb.ReadFromDat<SurfaceTexture>(actualTexId);
                                        }

                                        if (surfTex != null && surfTex.Textures != null && surfTex.Textures.Count > 0)
                                        {
                                            uint validTex = 0;
                                            foreach (var tId in surfTex.Textures)
                                            {
                                                var tObj = portalDb.ReadFromDat<Texture>(tId);
                                                if (tObj != null && tObj.SourceData != null && tObj.SourceData.Length > 0 && tObj.Format != SurfacePixelFormat.PFID_UNKNOWN)
                                                {
                                                    validTex = tId;
                                                    break;
                                                }
                                            }
                                            actualTexId = (validTex != 0) ? validTex : surfTex.Textures[0];
                                        }
                                    }

                                    int slotNum = surfaceIdx + 1;
                                    string matKey = $"{originalSwappedTexId}_surf_{surfaceIdx}";
                                    if (!textureToMatKey.TryGetValue(matKey, out var matIdx))
                                    {
                                        matIdx = gltf.materials.Count;
                                        var texture = portalDb.ReadFromDat<Texture>(actualTexId);
                                        if (texture == null || texture.SourceData == null || texture.SourceData.Length == 0)
                                        {
                                            if (DatManager.HighResDat != null)
                                            {
                                                var highResDb = new PortalDatDatabase(DatManager.HighResDat.FilePath, keepOpen: false);
                                                texture = highResDb.ReadFromDat<Texture>(actualTexId);
                                            }
                                        }

                                        bool isSurfaceTexture = (originalSwappedTexId & 0xFF000000) == 0x05000000;
                                        bool isIndexed = isSurfaceTexture || (texture != null && (texture.Format == SurfacePixelFormat.PFID_P8 || texture.Format == SurfacePixelFormat.PFID_INDEX16));
                                        bool isAlpha = (surface != null && surface.Translucency > 0) || (texture != null && (texture.Format == SurfacePixelFormat.PFID_A8R8G8B8 || texture.Format == SurfacePixelFormat.PFID_A4R4G4B4 || texture.Format == SurfacePixelFormat.PFID_A8));

                                        var material = new GltfMaterial
                                        {
                                            name = $"Material_Texture_0x{originalSwappedTexId:X8}_surf_{surfaceIdx}",
                                            pbrMetallicRoughness = new GltfPbr
                                            {
                                                baseColorTexture = new GltfTextureInfo { index = gltf.textures.Count }
                                            },
                                            doubleSided = true,
                                            alphaMode = isAlpha ? "BLEND" : "OPAQUE",
                                            alphaCutoff = 0.05,
                                            extras = new Dictionary<string, object> 
                                            { 
                                                { "indexed", isIndexed },
                                                { "translucency", surface != null ? surface.Translucency : 0.0f },
                                                { "surfaceType", surface != null ? (int)surface.Type : 0 },
                                                { "origTextureId", $"0x{originalSwappedTexId:X8}" }
                                            }
                                        };
                                        gltf.materials.Add(material);
                                        gltf.textures.Add(new GltfTexture { source = gltf.images.Count });
                                        string texUri = $"../texture/{originalSwappedTexId:X8}.png?wcid={wcid}";
                                        if (paletteId != 0) texUri += $"&paletteId={paletteId}";
                                        if (hue != 0) texUri += $"&hue={hue}";
                                        
                                        gltf.images.Add(new GltfImage { uri = texUri });

                                        textureToMatKey[matKey] = matIdx;
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
            rootNode.children = new List<int>();
            for (int n = 1; n < gltf.nodes.Count; n++)
            {
                rootNode.children.Add(n);
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
            string cacheKey = $"v4.0_{textureId}_wcid_{wcid}_pal_0x{paletteId:X8}_slot_{paletteSlot}_hue_{hueShift}";
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
            uint originalReqTex = textureId;
            string portalPath = DatManager.PortalDat?.FilePath ?? @"c:\ACE\Dats\client_portal.dat";
            var portalDb = new PortalDatDatabase(portalPath, keepOpen: false);

            if (wcid != 0)
            {
                uint clothingBase = 0;
                var weenie = DatabaseManager.World?.GetCachedWeenie(wcid);
                if (weenie != null && weenie.PropertiesDID != null)
                {
                    weenie.PropertiesDID.TryGetValue(PropertyDataId.ClothingBase, out clothingBase);
                }
                if (clothingBase == 0)
                {
                    try
                    {
                        var dbWeenie = DatabaseManager.World?.GetWeenie(wcid);
                        if (dbWeenie != null && dbWeenie.WeeniePropertiesDID != null)
                        {
                            foreach (var prop in dbWeenie.WeeniePropertiesDID)
                            {
                                if (prop.Type == (ushort)PropertyDataId.ClothingBase && prop.Value != 0)
                                {
                                    clothingBase = prop.Value;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (clothingBase != 0)
                {
                    var clothingTable = portalDb.ReadFromDat<ClothingTable>(clothingBase);
                    MergeCustomClothingBaseJson(clothingBase, clothingTable);
                    if (clothingTable != null && clothingTable.ClothingBaseEffects != null)
                    {
                        foreach (var kvp in clothingTable.ClothingBaseEffects)
                        {
                            if (kvp.Value?.CloObjectEffects == null) continue;
                            foreach (var objEffect in kvp.Value.CloObjectEffects)
                            {
                                if (objEffect.CloTextureEffects == null) continue;
                                foreach (var texEffect in objEffect.CloTextureEffects)
                                {
                                    if (texEffect.OldTexture == textureId && texEffect.NewTexture != 0)
                                    {
                                        textureId = texEffect.NewTexture;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            List<uint> surfaceTextureLayers = null;
            if ((textureId & 0xFF000000) == 0x05000000 || (textureId & 0xFF000000) == 0x08000000)
            {
                var surf = portalDb.ReadFromDat<Surface>(textureId);
                if (surf != null && surf.OrigTextureId != 0)
                {
                    textureId = surf.OrigTextureId;
                }

                if ((textureId & 0xFF000000) == 0x05000000)
                {
                    var surfTex = portalDb.ReadFromDat<SurfaceTexture>(textureId);
                    if (surfTex == null && DatManager.HighResDat != null)
                    {
                        var highResDb = new PortalDatDatabase(DatManager.HighResDat.FilePath, keepOpen: false);
                        surfTex = highResDb.ReadFromDat<SurfaceTexture>(textureId);
                    }

                    if (surfTex != null && surfTex.Textures.Count > 0)
                    {
                        surfaceTextureLayers = surfTex.Textures;
                        
                        // Find first valid texture as base layer (skip PFID_UNKNOWN/bump maps)
                        textureId = 0;
                        foreach (var texLayerId in surfaceTextureLayers)
                        {
                            var t = portalDb.ReadFromDat<Texture>(texLayerId);
                            if (t != null && t.Format != SurfacePixelFormat.PFID_UNKNOWN)
                            {
                                textureId = texLayerId;
                                break;
                            }
                            if (DatManager.HighResDat != null)
                            {
                                var highResDb = new PortalDatDatabase(DatManager.HighResDat.FilePath, keepOpen: false);
                                var ht = highResDb.ReadFromDat<Texture>(texLayerId);
                                if (ht != null && ht.Format != SurfacePixelFormat.PFID_UNKNOWN)
                                {
                                    textureId = texLayerId;
                                    break;
                                }
                            }
                        }
                        if (textureId == 0) textureId = surfaceTextureLayers[0];
                    }
                }
            }

            var texture = portalDb.ReadFromDat<Texture>(textureId);
            if (texture == null || texture.Width == 0 || texture.Height == 0 || texture.Format == SurfacePixelFormat.PFID_UNKNOWN || texture.SourceData == null || texture.SourceData.Length == 0)
            {
                string highResPath = DatManager.HighResDat?.FilePath ?? @"c:\ACE\Dats\client_highres.dat";
                if (File.Exists(highResPath))
                {
                    var highResDb = new PortalDatDatabase(highResPath, keepOpen: false);
                    var hrTex = highResDb.ReadFromDat<Texture>(textureId);
                    if (hrTex != null && hrTex.Width > 0 && hrTex.Height > 0 && hrTex.Format != SurfacePixelFormat.PFID_UNKNOWN)
                    {
                        texture = hrTex;
                    }
                }
            }

            if (texture == null || texture.Width == 0 || texture.Height == 0 || texture.Format == SurfacePixelFormat.PFID_UNKNOWN || texture.SourceData == null || texture.SourceData.Length == 0)
                return null;

            log.Info($"🎨 [SHOWROOM TEXTURE AUDIT] reqTex=0x{textureId:X8} (wcid={wcid}, palId=0x{paletteId:X8}, slot={paletteSlot}) | Format={texture.Format}, DefaultPal=0x{(texture.DefaultPaletteId.HasValue ? texture.DefaultPaletteId.Value.ToString("X8") : "00000000")}, Size={texture.Width}x{texture.Height}");

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
                    uint clothingBase = 0;
                    int defaultPalTemplate = 0;
                    uint defaultPalBaseDID = 0;

                    var weenie = DatabaseManager.World?.GetCachedWeenie(wcid);
                    if (weenie != null)
                    {
                        if (weenie.PropertiesDID != null)
                        {
                            weenie.PropertiesDID.TryGetValue(PropertyDataId.ClothingBase, out clothingBase);
                            weenie.PropertiesDID.TryGetValue(PropertyDataId.PaletteBase, out defaultPalBaseDID);
                        }
                        if (weenie.PropertiesInt != null && weenie.PropertiesInt.TryGetValue(PropertyInt.PaletteTemplate, out int pTemp))
                        {
                            defaultPalTemplate = pTemp;
                        }
                        if (weenie.PropertiesFloat != null && weenie.PropertiesFloat.TryGetValue(PropertyFloat.Shade, out double sVal))
                        {
                            weenieShade = sVal;
                            shade = (float)sVal;
                        }
                    }

                    if (clothingBase == 0 || defaultPalTemplate == 0)
                    {
                        try
                        {
                            var dbWeenie = DatabaseManager.World?.GetWeenie(wcid);
                            if (dbWeenie != null)
                            {
                                if (dbWeenie.WeeniePropertiesDID != null)
                                {
                                    foreach (var prop in dbWeenie.WeeniePropertiesDID)
                                    {
                                        if (prop.Type == (ushort)PropertyDataId.ClothingBase && prop.Value != 0 && clothingBase == 0)
                                            clothingBase = prop.Value;
                                        if (prop.Type == (ushort)PropertyDataId.PaletteBase && prop.Value != 0 && defaultPalBaseDID == 0)
                                            defaultPalBaseDID = prop.Value;
                                    }
                                }
                                if (dbWeenie.WeeniePropertiesInt != null)
                                {
                                    foreach (var prop in dbWeenie.WeeniePropertiesInt)
                                    {
                                        if (prop.Type == (ushort)PropertyInt.PaletteTemplate && prop.Value != 0 && defaultPalTemplate == 0)
                                            defaultPalTemplate = prop.Value;
                                    }
                                }
                                if (dbWeenie.WeeniePropertiesFloat != null)
                                {
                                    foreach (var prop in dbWeenie.WeeniePropertiesFloat)
                                    {
                                        if (prop.Type == (ushort)PropertyFloat.Shade)
                                        {
                                            weenieShade = prop.Value;
                                            shade = (float)prop.Value;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    if (clothingBase != 0)
                    {
                        int targetTemplate = 0;
                        if (paletteId != 0 && (paletteId & 0xFF000000) != 0x04000000 && (paletteId & 0xFF000000) != 0x0F000000)
                        {
                            targetTemplate = (int)paletteId;
                        }
                        else if (defaultPalTemplate != 0)
                        {
                            targetTemplate = defaultPalTemplate;
                        }
                        else if (defaultPalBaseDID != 0 && defaultPalBaseDID < 0x01000000)
                        {
                            targetTemplate = (int)defaultPalBaseDID;
                        }

                        var clothingTable = portalDb.ReadFromDat<ClothingTable>(clothingBase);
                        if (clothingTable != null && clothingTable.ClothingSubPalEffects != null)
                        {
                            if (clothingTable.ClothingSubPalEffects.TryGetValue((uint)targetTemplate, out var effect))
                            {
                                cloSubPalettes = effect.CloSubPalettes;
                                log.Info($"   -> Found ClothingSubPalEffect for Template {targetTemplate}: Count={cloSubPalettes?.Count ?? 0}");
                            }
                            else if (clothingTable.ClothingSubPalEffects.Count > 0)
                            {
                                effect = clothingTable.ClothingSubPalEffects.Values.FirstOrDefault();
                                cloSubPalettes = effect?.CloSubPalettes;
                                log.Info($"   -> Template {targetTemplate} not in ClothingTable, using fallback ClothingSubPalEffect: Count={cloSubPalettes?.Count ?? 0}");
                            }
                        }
                    }
                }

                uint paletteBase = 0;
                if ((paletteId & 0xFF000000) == 0x04000000)
                {
                    paletteBase = paletteId;
                }
                else if ((paletteId & 0xFF000000) == 0x0F000000)
                {
                    var palSet = portalDb.ReadFromDat<PaletteSet>(paletteId);
                    if (palSet != null && palSet.PaletteList != null && palSet.PaletteList.Count > 0)
                    {
                        paletteBase = palSet.GetPaletteID(shade);
                        if (paletteBase == 0) paletteBase = palSet.PaletteList[0];
                    }
                }
                else if (cloSubPalettes != null && cloSubPalettes.Count > 0 && cloSubPalettes[0].PaletteSet != 0)
                {
                    var palSet = portalDb.ReadFromDat<PaletteSet>(cloSubPalettes[0].PaletteSet);
                    if (palSet != null && palSet.PaletteList != null && palSet.PaletteList.Count > 0)
                    {
                        paletteBase = palSet.GetPaletteID(shade);
                        if (paletteBase == 0) paletteBase = palSet.PaletteList[0];
                    }
                }

                if (paletteBase != 0)
                {
                    basePalette = ReadPaletteSafely(portalDb, paletteBase, shade);
                }

                if (basePalette == null && texture.DefaultPaletteId != null && texture.DefaultPaletteId.Value != 0)
                {
                    basePalette = ReadPaletteSafely(portalDb, texture.DefaultPaletteId.Value, shade);
                }

                log.Info($"[TEXTURE PNG PALETTE DEBUG] reqTex=0x{textureId:X8}, basePaletteColors={(basePalette?.Colors?.Count ?? 0)}, Color[0]=0x{(basePalette != null && basePalette.Colors.Count > 0 ? basePalette.Colors[0].ToString("X8") : "NONE")}");

                if ((paletteId & 0xFF000000) == 0x04000000)
                {
                    var overridePalette = ReadPaletteSafely(portalDb, paletteId, shade);
                    if (overridePalette != null && overridePalette.Colors.Count > 0)
                    {
                        int targetLength = Math.Max(2048, basePalette?.Colors.Count ?? 256);
                        if (basePalette == null)
                        {
                            basePalette = new Palette();
                            basePalette.Colors.Capacity = targetLength;
                            var defaultPal = texture.DefaultPaletteId.HasValue ? ReadPaletteSafely(portalDb, texture.DefaultPaletteId.Value, shade) : null;
                            if (defaultPal != null && defaultPal.Colors.Count > 0)
                            {
                                for (int i = 0; i < targetLength; i++)
                                    basePalette.Colors.Add(defaultPal.Colors[i % defaultPal.Colors.Count]);
                            }
                            else if (overridePalette != null && overridePalette.Colors.Count > 0)
                            {
                                for (int i = 0; i < targetLength; i++)
                                    basePalette.Colors.Add(overridePalette.Colors[i % overridePalette.Colors.Count]);
                            }
                        }
                        else if (basePalette.Colors.Count < targetLength)
                        {
                            int fillCount = targetLength - basePalette.Colors.Count;
                            for (int i = 0; i < fillCount; i++)
                                basePalette.Colors.Add(basePalette.Colors[i % basePalette.Colors.Count]);
                        }

                        if (overridePalette.Colors.Count >= 2048)
                        {
                            int limit = Math.Min(basePalette.Colors.Count, overridePalette.Colors.Count);
                            for (int i = 0; i < limit; i++)
                            {
                                basePalette.Colors[i] = overridePalette.Colors[i];
                            }
                        }
                        else
                        {
                            // Apply custom 0x04 palette across full palette space [0 .. 2048]
                            int limit = Math.Min(basePalette.Colors.Count, 2048);
                            for (int i = 0; i < limit; i++)
                            {
                                basePalette.Colors[i] = overridePalette.Colors[i % overridePalette.Colors.Count];
                            }
                        }
                    }
                    else
                    {
                        log.Warn($"🎨 [PALETTE OVERRIDE WARN] PaletteID 0x{paletteId:X8} was not found in DAT. Retaining default creature texture colors.");
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
                            var subPaletteData = ReadPaletteSafely(portalDb, subPalId, shade);
                            if (subPaletteData != null && basePalette != null && subPaletteData.Colors.Count > 0)
                            {
                                int targetLength = Math.Max(2048, basePalette.Colors.Count);
                                while (basePalette.Colors.Count < targetLength)
                                    basePalette.Colors.Add(0xFFFFFFFF);

                                int limit = Math.Min(basePalette.Colors.Count, 2048);
                                for (int i = 0; i < limit; i++)
                                {
                                    basePalette.Colors[i] = subPaletteData.Colors[i % subPaletteData.Colors.Count];
                                }
                            }
                        }
                    }
                }

                if ((paletteId & 0xFF000000) != 0x04000000 && (paletteId & 0xFF000000) != 0x0F000000)
                {
                    if (cloSubPalettes != null && basePalette != null && cloSubPalettes.Count > 0)
                    {
                        // Apply SubPalettes sequentially as per AC DAT spec
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
                                        var subPaletteData = ReadPaletteSafely(portalDb, subPalId, shade);
                                        if (subPaletteData != null)
                                        {
                                            // Expand basePalette to fit SubPalette ranges
                                            int maxIdxNeeded = 0;
                                            foreach (var range in subPal.Ranges)
                                            {
                                                int endIdx = (int)(range.Offset + range.NumColors);
                                                if (endIdx > maxIdxNeeded) maxIdxNeeded = endIdx;
                                            }
                                            if (basePalette.Colors.Count < maxIdxNeeded)
                                            {
                                                basePalette.Colors.Capacity = maxIdxNeeded;
                                                while (basePalette.Colors.Count < maxIdxNeeded)
                                                    basePalette.Colors.Add(0x00000000);
                                            }

                                            foreach (var range in subPal.Ranges)
                                            {
                                                int offset = (int)range.Offset;
                                                int numColors = (int)range.NumColors;

                                                if (offset == 320 && range == subPal.Ranges[0])
                                                {
                                                    for (int c = 0; c < 320; c++)
                                                    {
                                                        if (c < basePalette.Colors.Count && subPaletteData.Colors.Count > (320 + (c % 320)))
                                                        {
                                                            basePalette.Colors[c] = subPaletteData.Colors[320 + (c % 320)];
                                                        }
                                                    }
                                                }

                                                for (int c = 0; c < numColors; c++)
                                                {
                                                    int idx = offset + c;
                                                    if (idx < basePalette.Colors.Count && subPaletteData.Colors.Count > 0)
                                                    {
                                                        basePalette.Colors[idx] = subPaletteData.Colors[idx % subPaletteData.Colors.Count];
                                                    }
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
                    if (basePalette != null && basePalette.Colors.Count > 0)
                    {
                        if (index < basePalette.Colors.Count)
                            return basePalette.Colors[index];
                        return basePalette.Colors[index % basePalette.Colors.Count];
                    }
                    return 0xFFFFFFFF;
                }

                log.Info($"[TEXTURE RECOLOR LOG] reqTex=0x{originalReqTex:X8} -> resolvedTex=0x{textureId:X8} (wcid={wcid}, palId=0x{paletteId:X8}, shade={shade}) | basePalette=0x{paletteBase:X8} (count={(basePalette?.Colors?.Count ?? 0)}) | Color[0]=0x{GetColor(0):X8}, Color[72]=0x{GetColor(72):X8}, Color[320]=0x{GetColor(320):X8}, Color[640]=0x{GetColor(640):X8}");

                if (texture.Format == SurfacePixelFormat.PFID_P8)
                {
                    for (int i = 0; i < width * height; i++)
                    {
                        if (i >= texture.SourceData.Length) break;
                        byte index = texture.SourceData[i];
                        uint color = GetColor(index);

                        byte a = (byte)((color >> 24) & 0xFF);
                        if (a == 0) a = 255;

                        rgba8[i * 4] = (byte)((color >> 16) & 0xFF);     // R
                        rgba8[i * 4 + 1] = (byte)((color >> 8) & 0xFF);   // G
                        rgba8[i * 4 + 2] = (byte)(color & 0xFF);          // B
                        rgba8[i * 4 + 3] = a;                            // A
                    }
                }
                else if (texture.Format == SurfacePixelFormat.PFID_INDEX16)
                {
                    using var reader = new BinaryReader(new MemoryStream(texture.SourceData));
                    for (int i = 0; i < width * height; i++)
                    {
                        if (reader.BaseStream.Position + 2 > reader.BaseStream.Length) break;
                        ushort val = reader.ReadUInt16();
                        uint color = GetColor(val);

                        byte a = (byte)((color >> 24) & 0xFF);
                        if (a == 0) a = 255;

                        rgba8[i * 4] = (byte)((color >> 16) & 0xFF);
                        rgba8[i * 4 + 1] = (byte)((color >> 8) & 0xFF);
                        rgba8[i * 4 + 2] = (byte)(color & 0xFF);
                        rgba8[i * 4 + 3] = a;
                    }
                }
                else if (texture.Format == SurfacePixelFormat.PFID_A8R8G8B8 || texture.Format == SurfacePixelFormat.PFID_R8G8B8 || texture.Format == SurfacePixelFormat.PFID_CUSTOM_LSCAPE_R8G8B8)
                {
                    using var reader = new BinaryReader(new MemoryStream(texture.SourceData));
                    bool hasAlpha = (texture.Format == SurfacePixelFormat.PFID_A8R8G8B8);
                    for (int i = 0; i < width * height; i++)
                    {
                        if (hasAlpha)
                        {
                            if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) break;
                            uint argb = reader.ReadUInt32();
                            byte a = (byte)((argb >> 24) & 0xFF);
                            byte r = (byte)((argb >> 16) & 0xFF);
                            byte g = (byte)((argb >> 8) & 0xFF);
                            byte b = (byte)(argb & 0xFF);
                            rgba8[i * 4] = r;
                            rgba8[i * 4 + 1] = g;
                            rgba8[i * 4 + 2] = b;
                            rgba8[i * 4 + 3] = a > 0 ? a : (byte)255;
                        }
                        else
                        {
                            if (reader.BaseStream.Position + 3 > reader.BaseStream.Length) break;
                            byte r = reader.ReadByte();
                            byte g = reader.ReadByte();
                            byte b = reader.ReadByte();
                            rgba8[i * 4] = r;
                            rgba8[i * 4 + 1] = g;
                            rgba8[i * 4 + 2] = b;
                            rgba8[i * 4 + 3] = 255;
                        }
                    }
                }
                else if (texture.Format == SurfacePixelFormat.PFID_DXT1)
                {
                    try
                    {
                        byte[] decompressed = DxtUtil.DecompressDxt1(texture.SourceData, width, height);
                        for (int i = 0; i < width * height && (i * 4 + 3) < decompressed.Length; i++)
                        {
                            byte b = decompressed[i * 4];
                            byte g = decompressed[i * 4 + 1];
                            byte r = decompressed[i * 4 + 2];
                            byte a = decompressed[i * 4 + 3];
                            rgba8[i * 4] = r;
                            rgba8[i * 4 + 1] = g;
                            rgba8[i * 4 + 2] = b;
                            rgba8[i * 4 + 3] = a > 0 ? a : (byte)255;
                        }
                    }
                    catch { }
                }
                else if (texture.Format == SurfacePixelFormat.PFID_DXT3)
                {
                    try
                    {
                        byte[] decompressed = DxtUtil.DecompressDxt3(texture.SourceData, width, height);
                        for (int i = 0; i < width * height && (i * 4 + 3) < decompressed.Length; i++)
                        {
                            byte b = decompressed[i * 4];
                            byte g = decompressed[i * 4 + 1];
                            byte r = decompressed[i * 4 + 2];
                            byte a = decompressed[i * 4 + 3];
                            rgba8[i * 4] = r;
                            rgba8[i * 4 + 1] = g;
                            rgba8[i * 4 + 2] = b;
                            rgba8[i * 4 + 3] = a;
                        }
                    }
                    catch { }
                }
                else if (texture.Format == SurfacePixelFormat.PFID_DXT5)
                {
                    try
                    {
                        byte[] decompressed = DxtUtil.DecompressDxt5(texture.SourceData, width, height);
                        for (int i = 0; i < width * height && (i * 4 + 3) < decompressed.Length; i++)
                        {
                            byte b = decompressed[i * 4];
                            byte g = decompressed[i * 4 + 1];
                            byte r = decompressed[i * 4 + 2];
                            byte a = decompressed[i * 4 + 3];
                            rgba8[i * 4] = r;
                            rgba8[i * 4 + 1] = g;
                            rgba8[i * 4 + 2] = b;
                            rgba8[i * 4 + 3] = a;
                        }
                    }
                    catch { }
                }

                using var image = Image.LoadPixelData<Rgba32>(rgba8, width, height);

                // Composite secondary SurfaceTexture layers (e.g. chest red dot / decal overlays)
                if (surfaceTextureLayers != null && surfaceTextureLayers.Count > 1)
                {
                    for (int layerIdx = 1; layerIdx < surfaceTextureLayers.Count; layerIdx++)
                    {
                        uint secTexId = surfaceTextureLayers[layerIdx];
                        if (secTexId == 0) continue;

                        byte[] secPngBytes = ExportTexturePng(secTexId, wcid, paletteId, shade, hueShift, paletteSlot);
                        if (secPngBytes != null && secPngBytes.Length > 0)
                        {
                            using var secImage = Image.Load<Rgba32>(secPngBytes);
                            if (secImage.Width != image.Width || secImage.Height != image.Height)
                            {
                                secImage.Mutate(x => x.Resize(image.Width, image.Height, KnownResamplers.NearestNeighbor));
                            }
                            image.Mutate(ctx => ctx.DrawImage(secImage, 1.0f));
                        }
                    }
                }

                using var ms = new MemoryStream();
                image.SaveAsPng(ms);
                return ms.ToArray();
            }
            else
            {
                // Decode truecolor textures (PFID_R8G8B8, PFID_A8R8G8B8, DXT1/3/5, etc.) directly from unpacked DAT texture
                var rgba8 = IconService.ToRgba8(texture);
                if (rgba8 == null) return null;
                using var image = Image.LoadPixelData<Rgba32>(rgba8, texture.Width, texture.Height);

                // Composite secondary SurfaceTexture layers
                if (surfaceTextureLayers != null && surfaceTextureLayers.Count > 1)
                {
                    for (int layerIdx = 1; layerIdx < surfaceTextureLayers.Count; layerIdx++)
                    {
                        uint secTexId = surfaceTextureLayers[layerIdx];
                        if (secTexId == 0) continue;

                        byte[] secPngBytes = ExportTexturePng(secTexId, wcid, paletteId, shade, hueShift, paletteSlot);
                        if (secPngBytes != null && secPngBytes.Length > 0)
                        {
                            using var secImage = Image.Load<Rgba32>(secPngBytes);
                            if (secImage.Width != image.Width || secImage.Height != image.Height)
                            {
                                secImage.Mutate(x => x.Resize(image.Width, image.Height, KnownResamplers.NearestNeighbor));
                            }
                            image.Mutate(ctx => ctx.DrawImage(secImage, 1.0f));
                        }
                    }
                }

                using var ms = new MemoryStream();
                image.SaveAsPng(ms);
                return ms.ToArray();
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

        private static Palette ReadPaletteSafely(PortalDatDatabase portalDb, uint palId, float shade = 0.5f)
        {
            if (palId == 0 || portalDb == null) return null;
            try
            {
                return portalDb.ReadFromDat<Palette>(palId);
            }
            catch
            {
                try
                {
                    var palSet = portalDb.ReadFromDat<PaletteSet>(palId);
                    if (palSet != null && palSet.PaletteList != null && palSet.PaletteList.Count > 0)
                    {
                        uint subPalId = palSet.GetPaletteID(shade);
                        if (subPalId != 0)
                            return portalDb.ReadFromDat<Palette>(subPalId);
                    }
                }
                catch { }
            }
            return null;
        }

        private static byte[] ExportPalettePng(uint paletteId)
        {
            var portalDb = new PortalDatDatabase(DatManager.PortalDat.FilePath, keepOpen: false);

            var palette = ReadPaletteSafely(portalDb, paletteId);
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

        public static (int Score, string Tier) CalculatePaletteConfidenceScore(Palette pal, HashSet<uint> approvedIds = null, HashSet<uint> blacklistedIds = null)
        {
            if (pal == null || pal.Colors == null || pal.Colors.Count == 0) return (0, "C");

            var labs = ExtractSwatchesLab(pal);
            if (labs.Count == 0) return (0, "C");

            // 1. Luminance Ramp (30% weight): L* max - L* min
            double maxL = labs.Max(l => l.L);
            double minL = labs.Min(l => l.L);
            double lRange = maxL - minL;
            double lScore = Math.Min(100.0, (lRange / 65.0) * 100.0);

            // 2. Smoothness Variance (30% weight): step variance of Delta-E
            double stepVarianceScore = 100.0;
            if (labs.Count > 1)
            {
                var deltas = new List<double>();
                for (int i = 0; i < labs.Count - 1; i++)
                {
                    deltas.Add(Ciede2000(labs[i], labs[i + 1]));
                }
                double avgDelta = deltas.Average();
                double variance = deltas.Select(d => Math.Pow(d - avgDelta, 2)).Average();
                stepVarianceScore = Math.Max(0.0, 100.0 - Math.Min(100.0, variance * 2.0));
            }

            // 3. Saturation Balance (20% weight)
            double satScore = 80.0;
            int step = Math.Max(1, pal.Colors.Count / 8);
            var sats = new List<double>();
            for (int i = 0; i < pal.Colors.Count; i += step)
            {
                uint argb = pal.Colors[i];
                double r = ((argb >> 16) & 0xFF) / 255.0;
                double g = ((argb >> 8) & 0xFF) / 255.0;
                double b = (argb & 0xFF) / 255.0;
                double max = Math.Max(r, Math.Max(g, b));
                double min = Math.Min(r, Math.Min(g, b));
                double sat = max == 0 ? 0 : (max - min) / max;
                sats.Add(sat);
            }
            double avgSat = sats.Average();
            if (avgSat >= 0.20 && avgSat <= 0.85) satScore = 100.0;
            else satScore = Math.Max(0.0, 100.0 - Math.Abs(avgSat - 0.5) * 150.0);

            // 4. Approved/Blacklist Cluster Distance (20% weight)
            double clusterScore = 75.0;
            if (approvedIds != null && approvedIds.Contains(pal.Id))
            {
                clusterScore = 100.0;
            }
            else if (blacklistedIds != null && blacklistedIds.Contains(pal.Id))
            {
                clusterScore = 0.0;
            }

            int finalScore = (int)Math.Round((lScore * 0.30) + (stepVarianceScore * 0.30) + (satScore * 0.20) + (clusterScore * 0.20));
            finalScore = Math.Clamp(finalScore, 0, 100);

            string tier = "C";
            if (finalScore >= 85) tier = "S";
            else if (finalScore >= 70) tier = "A";
            else if (finalScore >= 50) tier = "B";

            return (finalScore, tier);
        }

        /// <summary>
        /// Generates a curated pet breeding mutation palette pool based on user approvals & blacklists.
        /// </summary>
        public static List<SmartPaletteDto> GetCuratedMutationPool(uint wcid, string family = "all")
        {
            var curations = CurationService.GetCurationsForCreature(wcid);
            var approvedPalettes = new HashSet<uint>(curations.Where(c => c.Rating == 1).Select(c => c.PaletteId));
            var blacklistedPalettes = new HashSet<uint>(curations.Where(c => c.Rating == -1).Select(c => c.PaletteId));

            var fullPool = GetSmartPalettePool(wcid, family);
            var portalDb = DatManager.PortalDat;

            // Recalculate confidence scores with approved/blacklisted cluster context
            foreach (var dto in fullPool)
            {
                var pal = portalDb.ReadFromDat<Palette>(dto.PaletteId);
                if (pal != null)
                {
                    var (score, tier) = CalculatePaletteConfidenceScore(pal, approvedPalettes, blacklistedPalettes);
                    dto.ConfidenceScore = score;
                    dto.TierGrade = tier;
                }
            }

            // Filter out blacklisted palettes completely
            var filtered = fullPool.Where(p => !blacklistedPalettes.Contains(p.PaletteId)).ToList();

            var result = new List<SmartPaletteDto>();

            // 1. Put Approved palettes at the very top
            foreach (var item in filtered)
            {
                if (approvedPalettes.Contains(item.PaletteId))
                {
                    result.Add(item);
                }
            }

            // 2. Add remaining candidate palettes sorted by ConfidenceScore descending
            var remaining = filtered.Where(p => !approvedPalettes.Contains(p.PaletteId)).OrderByDescending(p => p.ConfidenceScore).ToList();
            result.AddRange(remaining);

            return result;
        }

        private static readonly ConcurrentDictionary<uint, ParticleEmitterDto> _particleEmitterCache = new();

        public static ParticleEmitterDto GetParticleEmitterDto(uint emitterId)
        {
            if (emitterId == 0) return null;
            if (_particleEmitterCache.TryGetValue(emitterId, out var cached))
                return cached;

            try
            {
                var portalDb = new PortalDatDatabase(DatManager.PortalDat.FilePath, keepOpen: false);
                var info = portalDb.ReadFromDat<ParticleEmitterInfo>(emitterId);
                if (info == null) return null;

                var dto = new ParticleEmitterDto
                {
                    EmitterId = emitterId,
                    EmitterType = (int)info.EmitterType,
                    ParticleType = (int)info.ParticleType,
                    GfxObjId = info.GfxObjId,
                    Birthrate = info.Birthrate,
                    MaxParticles = info.MaxParticles,
                    Lifespan = info.Lifespan,
                    LifespanRand = info.LifespanRand,
                    OffsetDir = new float[] { info.OffsetDir.X, info.OffsetDir.Y, info.OffsetDir.Z },
                    MinOffset = info.MinOffset,
                    MaxOffset = info.MaxOffset,
                    VelocityA = new float[] { info.A.X, info.A.Y, info.A.Z },
                    MinA = info.MinA,
                    MaxA = info.MaxA,
                    VelocityB = new float[] { info.B.X, info.B.Y, info.B.Z },
                    MinB = info.MinB,
                    MaxB = info.MaxB,
                    VelocityC = new float[] { info.C.X, info.C.Y, info.C.Z },
                    MinC = info.MinC,
                    MaxC = info.MaxC,
                    StartScale = info.StartScale,
                    FinalScale = info.FinalScale,
                    ScaleRand = info.ScaleRand,
                    StartTrans = info.StartTrans,
                    FinalTrans = info.FinalTrans,
                    TransRand = info.TransRand,
                    IsParentLocal = info.IsParentLocal
                };

                _particleEmitterCache[emitterId] = dto;
                return dto;
            }
            catch (Exception ex)
            {
                log.Error($"Error reading ParticleEmitterInfo 0x{emitterId:X8}: {ex.Message}");
                return null;
            }
        }

        public static List<CreatureParticleHookDto> GetCreatureParticleEmitters(uint wcid)
        {
            var result = new List<CreatureParticleHookDto>();
            if (wcid == 0) return result;

            uint motionTableId = 0;
            try
            {
                var weenie = DatabaseManager.World?.GetCachedWeenie(wcid);
                if (weenie != null && weenie.PropertiesDID != null)
                {
                    weenie.PropertiesDID.TryGetValue(PropertyDataId.MotionTable, out motionTableId);
                }

                if (motionTableId == 0)
                {
                    var dbWeenie = DatabaseManager.World?.GetWeenie(wcid);
                    if (dbWeenie != null && dbWeenie.WeeniePropertiesDID != null)
                    {
                        foreach (var prop in dbWeenie.WeeniePropertiesDID)
                        {
                            if (prop.Type == (ushort)PropertyDataId.MotionTable)
                            {
                                motionTableId = prop.Value;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error resolving motion table for WCID {wcid}: {ex.Message}");
            }

            // Hardcoded MotionTable fallback for Rynthid Nullifiers (420600) if DB context is uninitialized
            if (motionTableId == 0 && wcid == 420600)
                motionTableId = 0x0900021F;

            if (motionTableId == 0) return result;

            try
            {
                var portalDb = new PortalDatDatabase(DatManager.PortalDat.FilePath, keepOpen: false);
                var motionTable = portalDb.ReadFromDat<MotionTable>(motionTableId);
                if (motionTable == null) return result;

                var checkedAnims = new HashSet<uint>();
                if (motionTable.Cycles != null)
                {
                    foreach (var cycle in motionTable.Cycles.Values)
                    {
                        if (cycle.Anims != null)
                        {
                            foreach (var anim in cycle.Anims)
                                checkedAnims.Add(anim.AnimId);
                        }
                    }
                }
                if (motionTable.Links != null)
                {
                    foreach (var dict in motionTable.Links.Values)
                    {
                        if (dict != null)
                        {
                            foreach (var link in dict.Values)
                            {
                                if (link.Anims != null)
                                {
                                    foreach (var anim in link.Anims)
                                        checkedAnims.Add(anim.AnimId);
                                }
                            }
                        }
                    }
                }

                var seenKeys = new HashSet<string>();

                foreach (var animId in checkedAnims)
                {
                    var anim = portalDb.ReadFromDat<Animation>(animId);
                    if (anim == null || anim.PartFrames == null) continue;

                    for (int f = 0; f < anim.PartFrames.Count; f++)
                    {
                        var frame = anim.PartFrames[f];
                        if (frame.Hooks == null) continue;

                        foreach (var hook in frame.Hooks)
                        {
                            uint emitterId = 0;
                            int partIdx = -1;

                            if (hook is ACE.DatLoader.Entity.AnimationHooks.CreateParticleHook particleHook)
                            {
                                emitterId = particleHook.EmitterInfoId;
                                partIdx = (int)particleHook.PartIndex;
                            }
                            else if (hook is ACE.DatLoader.Entity.AnimationHooks.TransparentPartHook transHook && wcid == 420600)
                            {
                                partIdx = (int)transHook.Part;
                                if (partIdx == 10 || partIdx == 11)
                                {
                                    emitterId = 0x3200011E; // Red chest orb particle emitter for Rynthid Nullifiers
                                }
                            }

                            if (emitterId != 0 && partIdx >= 0)
                            {
                                string key = $"{emitterId}_{partIdx}";
                                if (seenKeys.Add(key))
                                {
                                    var emitterDto = GetParticleEmitterDto(emitterId);
                                    if (emitterDto != null)
                                    {
                                        result.Add(new CreatureParticleHookDto
                                        {
                                            EmitterId = emitterId,
                                            PartIndex = partIdx,
                                            Emitter = emitterDto
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error reading creature particle emitters for WCID {wcid}: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Creature Search API

        public class CreatureSearchResultDto
        {
            public uint Wcid { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
        }

        public static List<CreatureSearchResultDto> SearchCreatures(string query, int maxResults = 15)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<CreatureSearchResultDto>();

            query = query.Trim().ToLowerInvariant();
            bool isWcid = uint.TryParse(query, out uint searchWcid);

            var results = new List<CreatureSearchResultDto>();

            try
            {
                if (DatabaseManager.World != null)
                {
                    var allNames = DatabaseManager.World.GetAllWeenieNames();
                    if (allNames != null)
                    {
                        foreach (var kvp in allNames)
                        {
                            uint wcid = kvp.Key;
                            string name = kvp.Value;
                            if (string.IsNullOrEmpty(name)) continue;

                            bool isMatch = isWcid ? wcid == searchWcid : name.ToLowerInvariant().Contains(query);
                            if (isMatch)
                            {
                                var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
                                if (weenie != null && weenie.WeenieType == WeenieType.Creature)
                                {
                                    results.Add(new CreatureSearchResultDto
                                    {
                                        Wcid = wcid,
                                        Name = name,
                                        Category = "Creature"
                                    });

                                    if (results.Count >= maxResults) break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error searching creatures for query '{query}': {ex.Message}");
            }

            return results;
        }

        public static List<SpeciesPresetDto> GetSpeciesList()
        {
            var speciesList = new List<SpeciesPresetDto>
            {
                new SpeciesPresetDto { Wcid = 17, Name = "Gromnie", Species = "Gromnie" },
                new SpeciesPresetDto { Wcid = 25749, Name = "Olthoi Harvester", Species = "Olthoi" },
                new SpeciesPresetDto { Wcid = 35427, Name = "Drudge Lurker", Species = "Drudge" },
                new SpeciesPresetDto { Wcid = 18, Name = "Mattekar", Species = "Mattekar" },
                new SpeciesPresetDto { Wcid = 35134, Name = "Kroktok Lugian", Species = "Lugian" },
                new SpeciesPresetDto { Wcid = 36967, Name = "Tusker Protector", Species = "Tusker" },
                new SpeciesPresetDto { Wcid = 35146, Name = "Banderling Slayer", Species = "Banderling" },
                new SpeciesPresetDto { Wcid = 8, Name = "Mosswart", Species = "Mosswart" },
                new SpeciesPresetDto { Wcid = 10, Name = "Phyntos Mite", Species = "Mite" },
                new SpeciesPresetDto { Wcid = 12, Name = "Phyntos Wasp", Species = "Phyntos Wasp" },
                new SpeciesPresetDto { Wcid = 13, Name = "Giant Rat", Species = "Rat" },
                new SpeciesPresetDto { Wcid = 20, Name = "Auroch", Species = "Auroch" },
                new SpeciesPresetDto { Wcid = 3110132, Name = "Cow", Species = "Cow" },
                new SpeciesPresetDto { Wcid = 194, Name = "Dust Golem", Species = "Golem" },
                new SpeciesPresetDto { Wcid = 16, Name = "Undead", Species = "Undead" },
                new SpeciesPresetDto { Wcid = 19, Name = "Armoredillo", Species = "Armoredillo" },
                new SpeciesPresetDto { Wcid = 28477, Name = "Virindi", Species = "Virindi" },
                new SpeciesPresetDto { Wcid = 1535, Name = "Wisp", Species = "Wisp" },
                new SpeciesPresetDto { Wcid = 1536, Name = "Knathtead", Species = "Knathtead" },
                new SpeciesPresetDto { Wcid = 2577, Name = "Shadow Reaper", Species = "Shadow" },
                new SpeciesPresetDto { Wcid = 2583, Name = "Sclavus Impaler", Species = "Sclavus" },
                new SpeciesPresetDto { Wcid = 2574, Name = "Monouga", Species = "Monouga" },
                new SpeciesPresetDto { Wcid = 2608, Name = "Zefir", Species = "Zefir" },
                new SpeciesPresetDto { Wcid = 1759, Name = "Skeleton", Species = "Skeleton" },
                new SpeciesPresetDto { Wcid = 4108, Name = "Shreth", Species = "Shreth" },
                new SpeciesPresetDto { Wcid = 4242, Name = "Chittick", Species = "Chittick" },
                new SpeciesPresetDto { Wcid = 4246, Name = "Moarsman", Species = "Moarsman" },
                new SpeciesPresetDto { Wcid = 4250, Name = "Olthoi Larva", Species = "Olthoi Larvae" },
                new SpeciesPresetDto { Wcid = 4256, Name = "Slithis", Species = "Slithis" },
                new SpeciesPresetDto { Wcid = 4262, Name = "Deru", Species = "Deru" },
                new SpeciesPresetDto { Wcid = 5705, Name = "Fire Elemental", Species = "Fire Elemental" },
                new SpeciesPresetDto { Wcid = 5760, Name = "Snowman", Species = "Snowman" },
                new SpeciesPresetDto { Wcid = 6078, Name = "Bunny", Species = "Bunny" },
                new SpeciesPresetDto { Wcid = 6379, Name = "Lightning Elemental", Species = "Lightning Elemental" },
                new SpeciesPresetDto { Wcid = 7618, Name = "Rockslide", Species = "Rockslide" },
                new SpeciesPresetDto { Wcid = 7978, Name = "Grievver", Species = "Grievver" },
                new SpeciesPresetDto { Wcid = 7984, Name = "Sleech", Species = "Sleech" },
                new SpeciesPresetDto { Wcid = 7989, Name = "Ursuin", Species = "Ursuin" },
                new SpeciesPresetDto { Wcid = 8269, Name = "Hollow Minion", Species = "Hollow Minion" },
                new SpeciesPresetDto { Wcid = 8271, Name = "Scarecrow", Species = "Scarecrow" },
                new SpeciesPresetDto { Wcid = 8466, Name = "Idol", Species = "Idol" },
                new SpeciesPresetDto { Wcid = 8675, Name = "Empyrean", Species = "Empyrean" },
                new SpeciesPresetDto { Wcid = 9242, Name = "Doll", Species = "Doll" },
                new SpeciesPresetDto { Wcid = 9249, Name = "Marionette", Species = "Marionette" },
                new SpeciesPresetDto { Wcid = 11468, Name = "Carenzi", Species = "Carenzi" },
                new SpeciesPresetDto { Wcid = 11486, Name = "Siraluun", Species = "Siraluun" },
                new SpeciesPresetDto { Wcid = 10950, Name = "Aun Tumerok", Species = "Aun Tumerok" },
                new SpeciesPresetDto { Wcid = 12129, Name = "Simulacrum", Species = "Simulacrum" },
                new SpeciesPresetDto { Wcid = 14516, Name = "Acid Elemental", Species = "Acid Elemental" },
                new SpeciesPresetDto { Wcid = 14512, Name = "Frost Elemental", Species = "Frost Elemental" },
                new SpeciesPresetDto { Wcid = 14876, Name = "Elemental", Species = "Elemental" },
                new SpeciesPresetDto { Wcid = 420600, Name = "Viridian Statue", Species = "Statue" },
                new SpeciesPresetDto { Wcid = 25845, Name = "Margul", Species = "Margul" },
                new SpeciesPresetDto { Wcid = 26012, Name = "Burun", Species = "Burun" },
                new SpeciesPresetDto { Wcid = 28048, Name = "Banshee / Ghost", Species = "Ghost" },
                new SpeciesPresetDto { Wcid = 28643, Name = "Fiun", Species = "Fiun" },
                new SpeciesPresetDto { Wcid = 28635, Name = "Eater", Species = "Eater" },
                new SpeciesPresetDto { Wcid = 28658, Name = "Penguin", Species = "Penguin" },
                new SpeciesPresetDto { Wcid = 28666, Name = "Ruschk", Species = "Ruschk" },
                new SpeciesPresetDto { Wcid = 28672, Name = "Thrungus", Species = "Thrungus" },
                new SpeciesPresetDto { Wcid = 28651, Name = "Viamontian Knight", Species = "Viamontian Knight" },
                new SpeciesPresetDto { Wcid = 499991, Name = "Remoran", Species = "Remoran" },
                new SpeciesPresetDto { Wcid = 55790004, Name = "Moar", Species = "Moar" },
                new SpeciesPresetDto { Wcid = 500035, Name = "Mukkir", Species = "Mukkir" },
                new SpeciesPresetDto { Wcid = 3110527, Name = "Merwart", Species = "Merwart" },
                new SpeciesPresetDto { Wcid = 694200159, Name = "Gear Knight", Species = "Gear Knight" },
                new SpeciesPresetDto { Wcid = 500020, Name = "Gurog", Species = "Gurog" },
                new SpeciesPresetDto { Wcid = 45802, Name = "Anekshay", Species = "Anekshay" },
            };
            return speciesList.OrderBy(s => s.Species).ToList();
        }

        #endregion

        #endregion
    }
}
