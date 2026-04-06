# WorldBuilder Learnings: Landblock & World Rendering

This document tracks learnings from the WorldBuilder codebase regarding the rendering of landblocks and the world.

## 1. Coordinate Systems & Constants

Source: `WorldBuilder.Shared\Models\Position.cs` and `WorldBuilder.Shared\Services\WorldCoordinateService.cs`

### Magic Numbers & Units
- **Cell Size**: `24.0f` units.
- **Landblock Size**: `8x8` cells = `192.0f` units.
- **Landblock ID**: `0xXXYY` format where `XX` is Landblock X and `YY` is Landblock Y.
- **Global Map Offset**: `new Vector2(-24468f, -24468f)`. This aligns the world origin with ACE/AC coordinates.
- **NS/EW Conversion**: `1.0` NS/EW coordinate = `240.0` units (10 cells).
- **Chunk Size**: `8x8` landblocks (`1536x1536` units).
- **Chunk Vertex Stride**: `65` (calculated as `8 blocks * 8 vertices/block + 1`).
- **Road Width**: `5.0f` units.
- **Walkable Threshold (FloorZ)**: `0.66417414618662751f` (cos 48.38°). Any surface normal with a Z component below this is considered unwalkable.

### Coordinate Transformations
- **Local to Global**:
  - `GlobalX = (lbX * LandblockSize) + localX + MapOffset.X`
  - `GlobalY = (lbY * LandblockSize) + localY + MapOffset.Y`
- **Global to Map (NS/EW)**:
  - `NS = GlobalY / 240.0f`
  - `EW = GlobalX / 240.0f`

## 2. Rendering Infrastructure

Source: `Chorizite.OpenGLSDLBackend\GameScene.cs`

### Scene Management
`GameScene` is the central hub for 3D rendering. It orchestrates several specialized managers:
- **TerrainRenderManager**: Handles the landscape geometry and grid.
- **SceneryRenderManager**: Manages world scenery (trees, rocks, etc.).
- **StaticObjectRenderManager**: Handles buildings and other fixed objects.
- **EnvCellRenderManager**: Manages indoor environment cells.
- **PortalRenderManager**: Handles portals and stencil-based portal rendering.
- **SkyboxRenderManager**: Renders the sky and handles time-of-day lighting.

### Rendering Techniques
- **Modern Rendering**: Supports OpenGL 4.3 `Bindless Textures` and `MultiDrawIndirect` (implied by "Modern" shader naming) for performance.
- **Stencil Portals**: Uses a stencil buffer approach for rendering portals (`Shaders.PortalStencil`).
- **Frustum Culling**: Uses a `VisibilityManager` with a culling frustum to skip rendering of off-screen objects.
- **Asset Loading**: Uses an `ObjectMeshManager` to share meshes across different render managers.

### Magic Numbers (Rendering)
- **Max GPU Update Time**: `20ms` per frame is reserved for GPU uploads (to prevent stuttering).
- **Landscape Chunk Scale**: `1536.0f` units per chunk (8 landblocks).
- **Base Environment Cell ID**: `(pos.LandblockId << 16) | pos.CellId`.

## 3. Landblock Rendering (Terrain)

Source: `Chorizite.OpenGLSDLBackend\Lib\TerrainRenderManager.cs` and `TerrainGeometryGenerator.cs`

### Geometry Generation
- **Mesh Structure**: Each landblock is an `8x8` grid of cells. Each cell is rendered as two triangles (`6 vertices`).
- **Vertex Count**: `384` vertices and `384` indices per landblock. A full chunk (`8x8` landblocks) can have up to `24,576` vertices.
- **Vertex Format (`VertexLandscape`)**:
  - `Position`: `Vector3` (Global coordinates).
  - `Data0-Data3`: Four `uint` fields containing packed texture IDs, blending weights, and flags.
- **Bit-Precise Cell Splitting**: The split direction is calculated using the following deterministic formula:
  ```csharp
  uint seedA = (landblockX * 8 + cellX) * 214614067u;
  uint seedB = (landblockY * 8 + cellY) * 1109124029u;
  float splitDir = (seedA + 1813693831u) - seedB - 1369149221u;
  float splitVal = splitDir * 2.3283064e-10f;
  bool isSEtoNW = splitVal >= 0.5f;
  ```

### Height & Interpolation
- **Height Lookup**: Terrain heights are stored as bytes and mapped through `region.LandHeights`.
- **Interpolation**: The engine uses barycentric interpolation within the triangles of a cell to provide smooth `GetHeight` and `GetNormal` values for scenery placement and physics.

### Texturing & Materials
- **PalCode (Palette Code)**: A unique code derived from the terrain types and road flags of the four corners of a cell (`LandSurfaceManager.GetPalCode`).
- **Surface Smoothing**: The `LandSurfaceManager` translates PalCodes into texture sets.
- **Shader Logic**: The terrain shader uses the packed `Data` fields to blend multiple textures (base, detail, roads) in a single pass.

### Performance & Memory
- **Slot System**: The `TerrainRenderManager` uses a pre-allocated global VBO/EBO divided into slots. Each chunk occupies one slot.
- **Throttling**: GPU uploads are throttled to `20ms` per frame to maintain high FPS.
- **Partial Updates**: When a landblock is edited, only that landblock's vertices in the chunk's VBO are updated using `glBufferSubData`.

## 4. Scenery & Object Rendering

Source: `Chorizite.OpenGLSDLBackend\Lib\SceneryRenderManager.cs`, `StaticObjectRenderManager.cs`, and `ObjectMeshManager.cs`

### Scenery Generation (Procedural)
Unlike static objects, scenery (foliage, rocks, small debris) is procedurally generated at runtime.
- **Data Source**: The `TerrainEntry` for each vertex contains a `Scenery` ID and a `Type` (Terrain Type).
- **Deterministic Placement**:
  - A pseudo-random seed is generated from the global cell coordinates: `cellMat = globalCellY * (712977289u * globalCellX + 1813693831u) - 1109124029u * globalCellX + 2139937281u`.
  - This seed is used to select a `Scene` from the terrain's scene list and to calculate displacement, rotation, and scale variations.
- **Disqualification Logic**: The engine automatically hides scenery if it conflicts with other features:
  - **Roads**: Scenery is suppressed on road-flagged vertices.
  - **Buildings**: A spatial grid check prevents scenery from clipping through buildings.
  - **Slope**: Objects are disqualified if the terrain normal's Z component exceeds a threshold (too steep).
  - **Boundaries**: Objects displaced outside their parent landblock are typically disqualified.

### Static Objects & Buildings
- **Data Source**: These are explicitly stored in the landblock's object list (static objects, buildings).
- **Instancing**: Both procedural scenery and static objects use **Hardware Instancing** (specifically `MultiDrawIndirect`) to render thousands of objects with minimal draw calls.
- **Mesh Management**: The `ObjectMeshManager` handles loading and caching 3D models from the Dat files. It supports both standard models and "Setups" (multi-part models).

### Performance Optimization
- **MDI (Multi-Draw Indirect)**: Commands are built on the CPU and sent to a GPU buffer, allowing the GPU to draw all instances of the same mesh across a landblock in a single call.
- **Async Mesh Loading**: Meshes are loaded on background threads to prevent UI freezes.
- **Spatial Indexing**: Buildings use an internal `8x8` grid per landblock for fast collision and scenery disqualification checks.
 
## 5. Advanced Systems (Portals & Interiors)

Source: `Chorizite.OpenGLSDLBackend\Lib\EnvCellRenderManager.cs` and `PortalRenderManager.cs`

### Environment Cells (EnvCells)
EnvCells represent the interiors of buildings or dungeons.
- **Discovery**: The engine recursively discovers connected EnvCells starting from a building's portals.
- **Coordinate System**: EnvCell positions are stored as local offsets within their parent landblock.
- **SeenOutside Flag**: This flag in the `EnvCell` data determines if the interior is visible from the exterior world.
- **Geometry Deduplication**: Geometry is hashed by environment ID, cell structure, and surface types to prevent redundant mesh generation.

### Portal Rendering (Stencil System)
The world viewer uses a **Stencil Buffer** technique to render interiors only through their corresponding portal polygons.
- **Stencil Mask**:
  - Portal polygons are converted into triangle fans on the CPU.
  - These triangles are rendered into the stencil buffer (no color/depth write) to create a mask.
  - Interior geometry is then rendered only where the stencil mask is set.
- **Depth Clamping**: Uses `GL_DEPTH_CLAMP` during stencil mask generation to ensure portals remain valid even if the camera is extremely close.
- **Portal Service**: The `IPortalService` provides the polygon vertex data for each cell-to-cell or cell-to-landscape connection.

### Visibility & Occlusion
- **Frustum Culling**: Building-level bounding boxes are used to cull entire sets of interior cells.
- **Recursion**: Portals connect cells (`OtherCellId`). The engine walks this graph to determine which cells should be processed for rendering.
- **Spatial Queries**: `GetEnvCellAt` uses a 3x3 landblock neighborhood and bounding box containment tests to determine which EnvCell a given world coordinate resides in.

## 6. Shader Pipeline

Source: `Chorizite.OpenGLSDLBackend\Shaders\Landscape.frag/vert`, `PortalStencil.frag/vert`

### Terrain Shader (`Landscape.frag`)
The terrain shader is responsible for the complex multi-texture blending and UI overlays.
- **Multi-Texturing**:
  - Supports a base texture plus up to 3 overlays and 2 road layers.
  - Blending is performed using alpha masks from the `xAlphas` texture array.
  - Layer Priority: `Roads > Overlays > Base Texture`.
- **Procedural Grid**:
  - The landblock (`192 units`) and cell (`24 units`) grids are drawn procedurally in the fragment shader.
  - Line thickness is dynamically scaled based on camera distance to maintain a constant 1-pixel (or user-defined) width.
- **Low-Poly Lighting**:
  - Normals are often calculated per-face using `normalize(cross(dFdx(worldPos), dFdy(worldPos)))` for a faceted look.
  - Simple Lambertian reflectance with adjustable sunlight and ambient colors.
- **Interactive Overlays**:
  - **Brushes**: Circle, square, and crosshair brushes are rendered using Signed Distance Functions (SDFs).
  - **Slope Analysis**: Fragments are tinted red if the surface normal's Z component is below the `uFloorZ` threshold (unwalkable).

### Portal & Interior Shaders
- **Portal Stencil**: A minimalist shader that writes only to the stencil buffer to define the "window" into an interior cell.
- **Stencil Masking**: Interior objects are rendered with `glStencilFunc(GL_EQUAL, value, 0xFF)`, ensuring they only appear inside the portal boundaries.
- **Depth Clamping**: `GL_DEPTH_CLAMP` is used to prevent near-plane clipping of portal polygons when the camera passes through.

---

## 7. Logical Spatial Systems

Source: `WorldBuilder.Shared\Modules\Landscape\Models\LandscapeDocument.cs`

### Layer Merging
- **Composition Strategy**: Visible layers are applied sequentially on top of the base terrain data (`LandscapeDocument.RecalculateChunkFull`).
- **Merging Protocol**: Each layer edit is merged into the result via `TerrainEntry.Merge`. This allows for sparse edits (e.g., only changing height or only changing texture) to be combined and swapped atomically to the renderer.
- **Incremental Updates**: For performance during editing, only the affected local vertex indices are recalculated and swapped in the merged entry array (`RecalculateChunkIncremental`).

### Environment Cell Detection (Logical)
- **Bounding Box Query**: To determine which `EnvCell` a camera or object is inside *without* relying on the renderer, the engine performs a logical check:
  - Identifies the current 3x3 surrounding landblocks.
  - Queries all `EnvCell` bounding boxes within those landblocks.
  - Checks if `worldPos` is within `MinBounds` and `MaxBounds` (with a `0.01f` epsilon for Z-robustness).
- **Coordinate Alignment**: `Min/MaxBounds` from the Dat file are local to the landblock; they must be added to the `lbOrigin` (calculated from `lbX * lbSize + offset.X`) for world-space comparison.

---

## 8. Environmental Lighting & Skybox

Source: `Chorizite.OpenGLSDLBackend\Lib\SkyboxRenderManager.cs`

### Skybox Mechanics
- **Relative Positioning**: The sky is always centered on the camera by zeroing out the translation component of the view matrix (`M41=0, M42=0, M43=0`).
- **Huge Projection**: Uses a dedicated projection matrix with a far plane of `1,000,000.0f` to prevent celestial objects from being clipped.
- **Depth Handling**: Renders with `glDepthMask(false)` so the skybox never occludes world geometry.

### Celestial Objects (Sun, Moon, Stars)
- **Time of Day**: Managed as a float from `0.0` to `1.0`. AC uses `SkyTimeOfDay` records to define specific environmental states (e.g., Morning, Night).
- **Arc Movement**: Objects move along celestial arcs defined by a `BeginAngle` and `EndAngle`.
- **CalcFrame Logic**: The world transform for a sky object is calculated by scaling, then rotating around the Z-axis (heading), then rotating around the global Y-axis (the arc across the sky).

---

## 9. Admin Portal 3D-Tiles Pipeline

Source: `ACE.AdminPortal/Controllers/TileController.cs`, `TilesetController.cs`, `WorldViewer.tsx`

### Coordinate System (CRITICAL)
The `3d-tiles-renderer` Babylon.js plugin has an internal rotation applied based on `gltfUpAxis`:
- **`gltfUpAxis = "Y"` or missing**: Library applies `Matrix.RotationXToRef(Math.PI / 2)` — rotates GLBs +90° around X (BREAKS flat terrain!)
- **`gltfUpAxis = "Z"`**: No rotation applied (no `case 'z'` in the switch — stays Identity)

**Architecture**: Set `gltfUpAxis = "Z"` in the tileset JSON, but export GLB meshes in **Y-Up** format (`X=East, Y=Height, Z=North`). This matches Babylon's native camera without any rotation. Tileset transforms also use Y-Up: `offsetX, 0, offsetZ, 1`.

### Height Data (CellLandblock)
- **Array Size**: 81 entries (9×9 vertices per landblock)
- **Index Order**: `(cellX * 9) + cellY` — where X is East-West, Y is South-North
- **Comment from source**: "Height 0-9 is Western most edge. 10-18 is S-to-N strip just to the East."
- **Height Table**: `LandHeightTable` (256 floats) maps byte values to actual heights. The 2× multiplier is baked in.
- **Fallback**: If height table unavailable, use `hByte * 2.0f`

### Bounding Volumes
- Must use generous height extents (2000m+) to prevent LOD culling of terrain
- Center at Y=500 with Y half-axis of 2000 covers -1500 to 2500 range

### Refinement
- Use `ADD` refinement (not `REPLACE`) to prevent visible gaps during LOD transitions
- With `REPLACE`, parent tiles are hidden before all children are visible, causing black gaps

### Texture Pipeline
- Backend generates "white" meshes (no ImageBuilder, no file paths)
- Frontend injects the terrain atlas (`/api/atlas/terrain.png`) into materials via `onLoadTile`
- `SharpGLTF.Materials.ImageBuilder` causes `DirectoryNotFoundException` — never use it

---

## 10. Icon Rendering Implementation

We have successfully integrated the `WorldBuilder` icon rendering logic into `ACE.Server`. This allows the Web Portal to display high-fidelity item icons directly from the Portal Dat.

### Key Components

- **Dependencies**: Added `SixLabors.ImageSharp` and `BCnEncoder.Net` to `ACE.Server`.
- **IconService**: A new service in `ACE.Server/Services/IconService.cs` that handles:
    - `RenderSurface` (0x06) parsing and DXT1/3/5 decompression.
    - Paletted (P8/Index16) mapping to ARGB.
    - **Alpha channel normalization**: Handles both 8-bit palette transparency and 32-bit ARGB texture unpacking.
    - **Pixel-Perfect Transparency**: Performs a post-process sweep on every icon. Any pixel with literal white values (`255, 255, 255`) that isn't already transparent is forced to `alpha = 0`. This dynamic removal of legacy outlines preserves high-fidelity transparency against web backgrounds.
    - **UiEffects Swatch Substitution**: Replicates the native patterned gleam. If an item has `UiEffects` (Fire, Frost, Slashing, etc.), the service loads the corresponding swatch texture (e.g., `0x06001B2E`) and substitutes its pixels into the mask coordinates.
    - **Composite Layering**: Supports dynamic composition of an `underlay`, `base icon`, and `overlays` into a single PNG response.
- **IconController**: A new controller in `ACE.Server/Controllers/IconController.cs` serving PNGs at `/api/icon/{iconId}`.
- **Inventory Integration**: `CharacterController.GetInventory` now includes `iconId`, `iconUnderlayId`, and `uiEffects` for both online and offline characters.

### Usage in Frontend

To display a high-fidelity magical icon:
```tsx
const url = `/api/icon/${item.iconId}?underlay=${item.iconUnderlayId}&uiEffects=${item.uiEffects}`;
<img src={url} className="w-7 h-7 object-contain" alt={item.name} />
```

---

*End of Documentation*
