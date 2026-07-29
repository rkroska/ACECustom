using System;
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
    public static class VisualizerService
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

        private static SemaphoreSlim GetLock(string key)
        {
            return _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        }

        /// <summary>
        /// Retrieves or exports the GLTF 3D model bytes for a Weenie Class ID.
        /// </summary>
        public static async Task<byte[]> GetMeshGltfBytesAsync(uint wcid)
        {
            var cachePath = Path.Combine(ModelsCacheDir, $"{wcid}.gltf");
            if (File.Exists(cachePath))
            {
                return await File.ReadAllBytesAsync(cachePath);
            }

            var wcidLock = GetLock($"mesh_{wcid}");
            await wcidLock.WaitAsync();
            try
            {
                if (File.Exists(cachePath))
                {
                    return await File.ReadAllBytesAsync(cachePath);
                }

                var bytes = ExportMeshGltf(wcid);
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

        /// <summary>
        /// Retrieves or exports the texture PNG bytes for a given texture ID.
        /// </summary>
        public static async Task<byte[]> GetTexturePngBytesAsync(uint textureId, uint wcid = 0, uint paletteId = 0, float shade = 0.5f)
        {
            string cacheKey = wcid != 0 ? $"{textureId}_{wcid}_{paletteId}_{shade}" : $"{textureId}";
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

                var bytes = ExportTexturePng(textureId, wcid, paletteId, shade);
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

        private static byte[] ExportMeshGltf(uint wcid)
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
                                            v = 1.0f - vert.UVs[uvIdx].V;
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

        private static byte[] ExportTexturePng(uint textureId, uint wcid = 0, uint paletteId = 0, float shade = 0.5f)
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
                            if (weenie.PropertiesInt != null && weenie.PropertiesInt.TryGetValue(PropertyInt.PaletteTemplate, out palTemplate))
                            {
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

                            uint paletteBase = 0;
                            if (weenie.PropertiesDID != null && weenie.PropertiesDID.TryGetValue(PropertyDataId.PaletteBase, out paletteBase))
                            {
                                basePalette = portalDb.ReadFromDat<Palette>(paletteBase);
                            }
                        }
                    }
                }

                if (basePalette == null && texture.DefaultPaletteId != null && texture.DefaultPaletteId.Value != 0)
                {
                    basePalette = portalDb.ReadFromDat<Palette>(texture.DefaultPaletteId.Value);
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
                                        int srcColorIndex = 0;
                                        foreach (var range in subPal.Ranges)
                                        {
                                            int offset = (int)range.Offset;
                                            int numColors = (int)range.NumColors;
                                            for (int c = 0; c < numColors; c++)
                                            {
                                                if (offset + c < basePalette.Colors.Count && srcColorIndex < subPaletteData.Colors.Count)
                                                {
                                                    basePalette.Colors[offset + c] = subPaletteData.Colors[srcColorIndex];
                                                }
                                                srcColorIndex++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                uint GetColor(byte index)
                {
                    if (basePalette != null && index < basePalette.Colors.Count)
                        return basePalette.Colors[index];
                    return 0; // Default black/transparent
                }

                if (texture.Format == SurfacePixelFormat.PFID_P8)
                {
                    for (int i = 0; i < width * height; i++)
                    {
                        if (i >= texture.SourceData.Length) break;
                        byte index = texture.SourceData[i];
                        uint color = GetColor(index);
                        rgba8[i * 4] = (byte)((color >> 16) & 0xFF);     // R
                        rgba8[i * 4 + 1] = (byte)((color >> 8) & 0xFF);   // G
                        rgba8[i * 4 + 2] = (byte)(color & 0xFF);          // B
                        rgba8[i * 4 + 3] = (byte)((color >> 24) & 0xFF);  // A
                    }
                }
                else // PFID_INDEX16
                {
                    using var reader = new BinaryReader(new MemoryStream(texture.SourceData));
                    for (int i = 0; i < width * height; i++)
                    {
                        if (reader.BaseStream.Position + 2 > reader.BaseStream.Length) break;
                        ushort val = reader.ReadUInt16();
                        byte index = (byte)(val & 0xFF);
                        uint color = GetColor(index);
                        rgba8[i * 4] = (byte)((color >> 16) & 0xFF);
                        rgba8[i * 4 + 1] = (byte)((color >> 8) & 0xFF);
                        rgba8[i * 4 + 2] = (byte)(color & 0xFF);
                        rgba8[i * 4 + 3] = (byte)((color >> 24) & 0xFF);
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

        #endregion
    }
}
