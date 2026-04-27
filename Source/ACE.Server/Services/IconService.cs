using System;
using System.IO;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using BCnEncoder.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace ACE.Server.Services
{
    public static class IconService
    {
        private static readonly MemoryCache _iconCache = new(new MemoryCacheOptions
        {
            SizeLimit = 8192
        });
        private static readonly BcDecoder _bcDecoder = new();

        /// <summary>
        /// Gets the PNG bytes for a given iconId (RenderSurface 0x06), potentially composed with layers
        /// </summary>
        public static byte[] GetIcon(uint iconId, uint? underlayId = null, uint? overlayId = null, uint? overlaySecondaryId = null, uint? uiEffects = null)
        {
            // Normalize optional IDs to avoid duplicate cache entries for null vs 0
            uint normalizedUnderlay = underlayId ?? 0;
            uint normalizedOverlay = overlayId ?? 0;
            uint normalizedOverlaySecondary = overlaySecondaryId ?? 0;
            uint normalizedUiEffects = uiEffects ?? 0;

            string cacheKey = $"{iconId}_{normalizedUnderlay}_{normalizedOverlay}_{normalizedOverlaySecondary}_{normalizedUiEffects}";
            if (_iconCache.TryGetValue(cacheKey, out byte[] cached)) return cached;

            using (var baseImage = GetImage(iconId, normalizedUiEffects))
            {
                if (baseImage == null) return null;

                Image<Rgba32> resultImage = baseImage;
                bool needsDispose = false;

                try
                {
                    if (normalizedUnderlay != 0)
                    {
                        var underlayImage = GetImage(normalizedUnderlay);
                        if (underlayImage != null)
                        {
                            var newResult = underlayImage.Clone();
                            newResult.Mutate(x => x.DrawImage(resultImage, 1f));
                            resultImage = newResult;
                            needsDispose = true;
                            underlayImage.Dispose();
                        }
                    }

                    if (normalizedOverlay != 0)
                    {
                        using (var overlayImage = GetImage(normalizedOverlay))
                        {
                            if (overlayImage != null)
                            {
                                if (!needsDispose)
                                {
                                    resultImage = resultImage.Clone();
                                    needsDispose = true;
                                }
                                resultImage.Mutate(x => x.DrawImage(overlayImage, 1f));
                            }
                        }
                    }

                    if (normalizedOverlaySecondary != 0)
                    {
                        using (var overlaySecImage = GetImage(normalizedOverlaySecondary))
                        {
                            if (overlaySecImage != null)
                            {
                                if (!needsDispose)
                                {
                                    resultImage = resultImage.Clone();
                                    needsDispose = true;
                                }
                                resultImage.Mutate(x => x.DrawImage(overlaySecImage, 1f));
                            }
                        }
                    }

                    using (var ms = new MemoryStream())
                    {
                        resultImage.SaveAsPng(ms);
                        var pngBytes = ms.ToArray();
                        _iconCache.Set(cacheKey, pngBytes, new MemoryCacheEntryOptions { Size = 1 });
                        return pngBytes;
                    }
                }
                finally
                {
                    if (needsDispose && resultImage != null)
                        resultImage.Dispose();
                }
            }
        }

        private static Image<Rgba32> GetImage(uint iconId, uint? uiEffects = null)
        {
            var texture = DatManager.PortalDat.ReadFromDat<Texture>(iconId);
            if (texture == null || texture.SourceData == null || texture.SourceData.Length == 0)
                return null;

            var rgba = ToRgba8(texture, uiEffects);
            if (rgba == null) return null;

            return Image.LoadPixelData<Rgba32>(rgba, texture.Width, texture.Height);
        }

        private static byte[] ToRgba8(Texture texture, uint? uiEffects = null)
        {
            int width = texture.Width;
            int height = texture.Height;
            byte[] sourceData = texture.SourceData;
            byte[] rgba8 = new byte[width * height * 4];
            uint paletteId = texture.DefaultPaletteId ?? 0;

            switch (texture.Format)
            {
                case SurfacePixelFormat.PFID_R8G8B8:
                    for (int i = 0; i < width * height; i++)
                    {
                        rgba8[i * 4] = sourceData[i * 3 + 2];
                        rgba8[i * 4 + 1] = sourceData[i * 3 + 1];
                        rgba8[i * 4 + 2] = sourceData[i * 3];
                        rgba8[i * 4 + 3] = 255;
                    }
                    break;
                case SurfacePixelFormat.PFID_A8R8G8B8:
                    for (int i = 0; i < width * height; i++)
                    {
                        rgba8[i * 4] = sourceData[i * 4 + 2];
                        rgba8[i * 4 + 1] = sourceData[i * 4 + 1];
                        rgba8[i * 4 + 2] = sourceData[i * 4];
                        rgba8[i * 4 + 3] = sourceData[i * 4 + 3];
                    }
                    break;
                case SurfacePixelFormat.PFID_R5G6B5:
                    for (int i = 0; i < width * height; i++)
                    {
                        ushort pixel = BitConverter.ToUInt16(sourceData, i * 2);
                        rgba8[i * 4] = (byte)(((pixel >> 11) & 0x1F) << 3);
                        rgba8[i * 4 + 1] = (byte)(((pixel >> 5) & 0x3F) << 2);
                        rgba8[i * 4 + 2] = (byte)((pixel & 0x1F) << 3);
                        rgba8[i * 4 + 3] = 255;
                    }
                    break;
                case SurfacePixelFormat.PFID_A4R4G4B4:
                    for (int i = 0; i < width * height; i++)
                    {
                        ushort pixel = BitConverter.ToUInt16(sourceData, i * 2);
                        rgba8[i * 4] = (byte)(((pixel >> 8) & 0x0F) * 17);
                        rgba8[i * 4 + 1] = (byte)(((pixel >> 4) & 0x0F) * 17);
                        rgba8[i * 4 + 2] = (byte)((pixel & 0x0F) * 17);
                        rgba8[i * 4 + 3] = (byte)(((pixel >> 12) & 0x0F) * 17);
                    }
                    break;
                case SurfacePixelFormat.PFID_A8:
                case SurfacePixelFormat.PFID_CUSTOM_LSCAPE_ALPHA:
                    for (int i = 0; i < width * height; i++)
                    {
                        byte val = sourceData[i];
                        rgba8[i * 4] = val;
                        rgba8[i * 4 + 1] = val;
                        rgba8[i * 4 + 2] = val;
                        rgba8[i * 4 + 3] = 255;
                    }
                    break;
                case SurfacePixelFormat.PFID_P8:
                    if (paletteId != 0)
                    {
                        var palette = DatManager.PortalDat.ReadFromDat<Palette>(paletteId);
                        if (palette != null)
                        {
                            for (int i = 0; i < width * height; i++)
                            {
                                byte sourceIndex = sourceData[i];
                                var color = palette.Colors[sourceIndex];
                                rgba8[i * 4] = (byte)((color >> 16) & 0xFF);
                                rgba8[i * 4 + 1] = (byte)((color >> 8) & 0xFF);
                                rgba8[i * 4 + 2] = (byte)(color & 0xFF);
                                byte alpha = (byte)((color >> 24) & 0xFF);
                                if (sourceIndex == 0) rgba8[i * 4 + 3] = 0;
                                else rgba8[i * 4 + 3] = alpha > 0 ? alpha : (byte)255;
                            }
                            break;
                        }
                    }
                    // Greyscale fallback
                    for (int i = 0; i < width * height; i++)
                    {
                        byte val = sourceData[i];
                        rgba8[i * 4] = val;
                        rgba8[i * 4 + 1] = val;
                        rgba8[i * 4 + 2] = val;
                        rgba8[i * 4 + 3] = 255;
                    }
                    break;
                case SurfacePixelFormat.PFID_INDEX16:
                    if (paletteId != 0)
                    {
                        var palette = DatManager.PortalDat.ReadFromDat<Palette>(paletteId);
                        if (palette != null)
                        {
                            for (int i = 0; i < width * height; i++)
                            {
                                ushort index = BitConverter.ToUInt16(sourceData, i * 2);
                                var color = palette.Colors[index];
                                rgba8[i * 4] = (byte)((color >> 16) & 0xFF);
                                rgba8[i * 4 + 1] = (byte)((color >> 8) & 0xFF);
                                rgba8[i * 4 + 2] = (byte)(color & 0xFF);
                                byte alpha = (byte)((color >> 24) & 0xFF);
                                if (index == 0) rgba8[i * 4 + 3] = 0;
                                else rgba8[i * 4 + 3] = alpha > 0 ? alpha : (byte)255;
                            }
                            break;
                        }
                    }
                    // Greyscale fallback
                    for (int i = 0; i < width * height; i++)
                    {
                        ushort index = BitConverter.ToUInt16(sourceData, i * 2);
                        byte val = (byte)((index >> 8) & 0xFF);
                        rgba8[i * 4] = val;
                        rgba8[i * 4 + 1] = val;
                        rgba8[i * 4 + 2] = val;
                        rgba8[i * 4 + 3] = 255;
                    }
                    break;
                case SurfacePixelFormat.PFID_CUSTOM_RAW_JPEG:
                    using (var ms = new MemoryStream(sourceData))
                    {
                        using (var image = Image.Load<Rgba32>(ms))
                        {
                            image.CopyPixelDataTo(rgba8);
                        }
                    }
                    break;
                case SurfacePixelFormat.PFID_DXT1:
                case SurfacePixelFormat.PFID_DXT3:
                case SurfacePixelFormat.PFID_DXT5:
                    CompressionFormat format = texture.Format switch
                    {
                        SurfacePixelFormat.PFID_DXT1 => CompressionFormat.Bc1,
                        SurfacePixelFormat.PFID_DXT3 => CompressionFormat.Bc2,
                        SurfacePixelFormat.PFID_DXT5 => CompressionFormat.Bc3,
                        _ => throw new InvalidOperationException()
                    };
                    using (var image = _bcDecoder.DecodeRawToImageRgba32(sourceData, width, height, format))
                    {
                        image.CopyPixelDataTo(rgba8);
                    }
                    break;
                default:
                    return null;
            }
            
            byte[] effectPixels = null;
            if (uiEffects.HasValue && uiEffects.Value != 0)
            {
                uint effectIconId = 0;
                // UiEffects bitmask -> 0x06 Icon ID mapping
                // UiEffects bitmask -> 0x06 Icon ID mapping
                // Priority: Elemental/Combat > Status > Magical/Health
                if ((uiEffects.Value & 1) != 0) effectIconId = 0x060011CA; // Magical
                else if ((uiEffects.Value & 2) != 0) effectIconId = 0x060011C6; // Poisoned
                else if ((uiEffects.Value & 4) != 0) effectIconId = 0x06001B05; // Boost Health
                else if ((uiEffects.Value & 8) != 0) effectIconId = 0x060011CA; // Boost Mana
                else if ((uiEffects.Value & 16) != 0) effectIconId = 0x06001B06; // Boost Stamina
                else if ((uiEffects.Value & 32) != 0) effectIconId = 0x06001B2E; // Fire
                else if ((uiEffects.Value & 64) != 0) effectIconId = 0x06001B2D; // Lightning
                else if ((uiEffects.Value & 128) != 0) effectIconId = 0x06001B2F; // Frost
                else if ((uiEffects.Value & 256) != 0) effectIconId = 0x06001B2C; // Acid
                else if ((uiEffects.Value & 512) != 0) effectIconId = 0x060033C3; // Bludgeoning
                else if ((uiEffects.Value & 1024) != 0) effectIconId = 0x060033C2; // Slashing
                else if ((uiEffects.Value & 2048) != 0) effectIconId = 0x060033C4; // Piercing

                if (effectIconId != 0) {
                    var effectTex = DatManager.PortalDat.ReadFromDat<Texture>(effectIconId);
                    // Ensure the effect texture matches dimensions before applying it
                    if (effectTex != null && effectTex.Width == width && effectTex.Height == height) {
                        effectPixels = ToRgba8(effectTex, null);
                    }
                }
            }

            // Post-process to remove literal white masks globally across any texture format
            for (int i = 0; i < width * height; i++)
            {
                // Skip pixels that are already fully transparent (e.g., the background index 0)
                if (rgba8[i * 4 + 3] == 0) continue;
                
                if (rgba8[i * 4] == 255 && rgba8[i * 4 + 1] == 255 && rgba8[i * 4 + 2] == 255)
                {
                    if (effectPixels != null && effectPixels.Length == rgba8.Length) {
                        rgba8[i * 4] = effectPixels[i * 4];
                        rgba8[i * 4 + 1] = effectPixels[i * 4 + 1];
                        rgba8[i * 4 + 2] = effectPixels[i * 4 + 2];
                        // If the swatch texture itself is transparent at this pixel, we mirror that
                        rgba8[i * 4 + 3] = effectPixels[i * 4 + 3];
                    } else {
                        rgba8[i * 4 + 3] = 0;
                    }
                }
            }
            
            return rgba8;
        }
    }
}
