# DAT Surface Rendering & Texture/Palette Rules for WebGL Showroom

## Core Architectural Rules for client_portal.dat DAT Object Types

1. **Strict Type Safety for DAT IDs**:
   - `0x08...` IDs are **Surface** objects (`DatFileType.Surface`).
   - `0x05...` IDs are **SurfaceTexture** or **Texture** objects.
   - `0x02...` IDs are **SetupModel** objects.
   - `0x01...` IDs are **GfxObj** objects.
   - **CRITICAL**: NEVER pass an `0x05...` ID to `ReadFromDat<Surface>(id)`. Doing so will attempt to parse the `SurfaceTexture` file structure into the `Surface` struct, corrupting `OrigTextureId` with garbage bytes from `SurfaceTexture.Unknown` and breaking texture/head rendering (causing creatures to render "headless").

2. **Surface (`0x08...`) Texture & Palette Resolution**:
   - When unwrapping an `0x08` `Surface`:
     ```csharp
     if ((textureId & 0xFF000000) == 0x08000000)
     {
         var surf = portalDb.ReadFromDat<Surface>(textureId);
         if (surf != null)
         {
             if (surf.OrigTextureId != 0)
             {
                 textureId = surf.OrigTextureId;
                 // MUST retain surf.OrigPaletteId if defined!
                 if (surf.OrigPaletteId != 0 && (paletteId == 0 || paletteId == 0x00000000))
                 {
                     paletteId = surf.OrigPaletteId;
                 }
             }
             else if (surf.OrigTextureId == 0 && (surf.Type.HasFlag(SurfaceType.Base1Solid) || surf.ColorValue != 0))
             {
                 // Solid color surface (Base1Solid): generate 16x16 PNG filled with surf.ColorValue
                 uint col = surf.ColorValue;
                 byte a = (byte)((col >> 24) & 0xFF);
                 byte r = (byte)((col >> 16) & 0xFF);
                 byte g = (byte)((col >> 8) & 0xFF);
                 byte b = (byte)(col & 0xFF);
                 if (a == 0 && (r != 0 || g != 0 || b != 0 || col == 0xFF000000)) a = 255;
                 
                 byte[] solidRgba = new byte[16 * 16 * 4];
                 for (int i = 0; i < 16 * 16; i++)
                 {
                     solidRgba[i * 4 + 0] = r;
                     solidRgba[i * 4 + 1] = g;
                     solidRgba[i * 4 + 2] = b;
                     solidRgba[i * 4 + 3] = a;
                 }
                 using var solidImg = Image.LoadPixelData<Rgba32>(solidRgba, 16, 16);
                 using var solidMs = new MemoryStream();
                 solidImg.SaveAsPng(solidMs);
                 return solidMs.ToArray();
             }
         }
     }
     ```

3. **Solid-Color Surface Fallback (`Base1Solid`)**:
   - Polygons pointing to `0x08` surfaces where `OrigTextureId == 0` (e.g. mouth/claw accents or eyes) are solid-color surfaces (`Base1Solid`).
   - If returned as `null` to WebGL, Three.js falls back to solid WHITE (`0xFFFFFF`).
   - Returning a 16x16 PNG filled with `surf.ColorValue` ensures they render in their intended solid color (e.g. solid black `0xFF000000`).

4. **SurfaceTexture (`0x05...`) Layer Composition**:
   - Handled separately AFTER `0x08` unwrapping.
   - Reads base texture layer and composites decal/overlay layers sequentially using ImageSharp.
