# Architectural Diagnostic Report: WCID 51724 (Discorporate Rynthid of Blind Rage) Palette Failure

## 1. Weenie & DAT Properties for WCID 51724
Upon querying the `ace_world` database and correlating with the client DAT files, the following properties define WCID 51724:

**Weenie Properties:**
* **Setup (0x02):** `0x02001BCE`
* **PaletteBase (0x04):** `0x040009B2`
* **ClothingBase (0x10):** `0x10000854`
* **PaletteTemplate:** Not explicitly defined (missing PropertyInt 40).
* **PropertiesPalette:** `3` (PropertyInt 68)
* **Shade:** `0.5` (PropertyFloat 4)

**Mesh & Texture Mappings (Setup 0x02001BCE):**
The parts list for the Setup maps to various GfxObjs, which contain surfaces mapping to specific Truecolor textures:
* **GfxObj 0x01004CEF / 0x01004CF1-0x01004CF5:** 
  - Surface `0x080018F5` (Type: `Base1Image, Alpha, Additive`) -> SurfaceTexture `0x05003305` -> Texture `0x06007471` (Format: **PFID_A8R8G8B8**, 256x256).
* **GfxObj 0x01004CF9:**
  - Surface `0x080018F7` (Type: `Base1Image`) -> SurfaceTexture `0x05003307` -> Texture `0x06007473` (Format: **PFID_R8G8B8**, 512x512).
* **GfxObj 0x01004D0D:**
  - Surface `0x080018FA` (Type: `Base1Image`) -> SurfaceTexture `0x0500330A` -> Texture `0x060074C8` (Format: **PFID_R8G8B8**, 256x256).
* **GfxObj 0x010001EC:**
  - Surface `0x08000015` (Type: `Base1ClipMap, Translucent`) -> Texture `0x060037A3` (Format: **PFID_INDEX16**, 8x8).

## 2. Texture Format & Pixel Encoding
The investigation confirms that the Rynthid uses **32-bit ARGB (PFID_A8R8G8B8)** and **24-bit RGB (PFID_R8G8B8)** truecolor textures for its primary visual body parts. These textures have hardcoded spectral channels (dark purple and glowing red pixel data). 
Additionally, the surface types use additive render FX (`Alpha, Additive`) to generate the glowing particle overlays. Because they are truecolor textures, they completely bypass the traditional 8-bit indexed palette tables that legacy AC assets use.

## 3. ExportTexturePng & Palette Application in VisualizerService.cs
The root cause of the visualizer failing to show color changes lies in `VisualizerService.cs` lines 968-1225.
The `ExportTexturePng` method has the following condition:
```csharp
if (texture.Format == SurfacePixelFormat.PFID_P8 || texture.Format == SurfacePixelFormat.PFID_INDEX16)
{
    // Subpalette baking ...
}
else
{
    return IconService.GetIcon(textureId);
}
```
**Why the 0x04 palettes fail to alter the appearance:**
1. **Truecolor Ignored:** The visualizer's palette replacement logic is strictly implemented for 8-bit indexed textures (`PFID_P8` and `PFID_INDEX16`). When the Truecolor Rynthid textures (`PFID_A8R8G8B8` / `PFID_R8G8B8`) are processed, they fall into the `else` block, skipping palette processing entirely and returning an icon fallback.
2. **Hardcoded Colors:** Even if the logic attempted to modify them, truecolor ARGB/RGB textures define precise, static pixel colors. They don't use indices mapped to a `0x04` palette file. Consequently, applying a `0x04` PaletteBase or a `0x10` ClothingBase sub-palette to WCID 51724 mathematically cannot work unless the engine uses a specialized shader for colorizing truecolor RGB channels (which the current C# visualizer does not).
