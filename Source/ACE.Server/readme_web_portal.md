# ACE Web Portal - Server Side Documentation

This document describes the internal web server and API layer integrated into `ACE.Server`. The web portal provides a high-performance, administrative interface for managing accounts, characters, and server configurations.

## Project Structure

- **Backend**: Located in `ACE.Server/Controllers` and `ACE.Server/WebPortalHost.cs`.
- **Frontend**: Located in [Source/ACE.WebPortal/ClientApp](../ACE.WebPortal/ClientApp/readme.md). The production build output is expected in the `dist` folder.

## Server Initialization (`WebPortalHost.cs`)

The web server is an embedded **ASP.NET Core** instance started by `WebPortalHost.Start()`. 

### Web Asset Resolution
The server uses an **Intelligent Resolution** strategy to locate the portal's static assets:
1.  **Source-First**: It crawls the directory tree to locate `Source/ACE.WebPortal/ClientApp/dist`. This supports the "build-in-place" lifecycle used in both development and "source-copy" production environments.
2.  **Fallback**: If the source tree is missing, it looks for a local `dist` folder adjacent to the server binary.
3.  **Auto-Create**: The server always ensures the target directory exists at startup to satisfy the static file provider, allowing for post-startup builds.

### Routing & The "Fallback" Issue
For a Single Page Application (SPA) like the React-based portal, client-side routing (e.g., `/characters/list`) is managed by the browser. If a user refreshes the page on such a route, the browser sends a request for that literal path to the server.

To prevent a `404 Not Found` error, the server is configured with:
```csharp
app.MapFallbackToFile("index.html");
```
**Mechanism**: If a request does not match a static file or an API controller, the server returns `index.html`. The browser then loads the React app, which reads the current URL and renders the correct view.

## Security & Authentication

### Secure-by-Default Architecture
The portal utilizes a **professional "HttpOnly" cookie-based session model** to protect administrative credentials:
- **HttpOnly Cookies**: The authentication JWT is stored in a cookie (`ilt_auth_token`) that is inaccessible to JavaScript. This provides robust protection against Cross-Site Scripting (XSS) token theft.
- **Secure Handling**: Cookies are configured with `SameSite: Strict` and `Secure` (in production) flags.
- **In-Memory State**: Sensitive session data is never persisted to `localStorage`. Instead, the frontend "bootstraps" its in-memory user state on every page load by verifying the session with the server.

### Network Configuration & Binding
The portal is designed with a **Zero-Config** architecture:
- **Secure Binding**: The server dynamically adjusts its visibility based on the environment:
    - **Development**: Binds to all interfaces (`*`) on port **5000**.
    - **Production**: Binds to **loopback only** (`localhost`) on port **5001**, ensuring administrative traffic remains private and is only accessible via the local reverse proxy.
- **Reverse Proxy Requirement**: In production, a reverse proxy (e.g., NGINX or IIS) is required to terminate SSL/TLS and forward requests to `http://localhost:5001`.
- **JWT secret**: Set `WebPortal.JwtSecret` in `Config.js` (32+ random characters) in production so sessions survive restarts. If omitted, a new secret is generated each start.

## Production checklist (`Config.js` on the **running server**)

Edit the `Config.js` next to `ACE.Server.dll`, not only the copy under `Source/`.

### `PatchNotes` (required for MOTD / Discord / links)

| Setting | Production value |
|--------|-------------------|
| `PublicBaseUrl` | Player-facing portal root **with hash routes**, e.g. `http://76.237.151.184:5002/` (Caddy URL). Used in MOTD, Discord, `/patchnotes`. |
| `MotdEnabled` | `true` if you want in-game login lines |
| `MotdTemplate` | Tokens: `{url}`, `{lastUpdated}`, `{lastUpdatedUtc}`, `{lastUpdatedRelative}` |
| `MotdTimeZoneId` | Optional, e.g. `"Central Standard Time"` — empty + `MotdUseHostLocalTime: true` uses server local time |
| `DiscordChannelId` | Your Discord channel snowflake |
| `DiscordEnabled` | `true` |

Also ensure `Chat.EnableDiscordConnection` is `true` and `Chat.DiscordToken` / `ServerId` are set (Discord bot).

### `WebPortal` (recommended)

| Setting | Production value |
|--------|-------------------|
| `JwtSecret` | Long random string (32+ chars). **Do not leave empty in prod.** |
| `BindHost` | Leave empty for `localhost:5001` behind Caddy, or `*` only if you know you need direct bind |
| `BindPort` | `0` = default 5001 in Release |

### Server + deploy (not in `Config.js`)

1. `enable_web_portal` server property = true  
2. `cd Source/ACE.WebPortal/ClientApp && npm run build` then `dotnet publish` / copy `wwwroot`  
3. Reverse proxy (Caddy) → `http://127.0.0.1:5001` with TLS on the public port  
4. `ace_auth` tables: `portal_page_access` and `patch_notes` auto-create on startup if the DB user can `CREATE`; otherwise run `Database/Updates/Authentication/2026-05-31-00-Portal-Page-Access.sql` and `2026-05-31-01-Patch-Notes.sql`  
5. Restart ACE after changing `Config.js`

## Production Deployment (HTTPS)

To securely expose the Web Portal to external users, you **MUST** configure a reverse proxy to handle SSL/TLS termination. 

For detailed, step-by-step instructions on setting up a hardened Linux environment with NGINX and Let's Encrypt, see the:

### [➜ Hardened Linux Setup Guide (readme_linux_setup.md)](./readme_linux_setup.md)

This guide covers:
- Installing Node.js LTS for frontend builds.
- Hardening NGINX with security headers and large-buffer support.
- Automated SSL certificate management via Certbot.

### Web portal bind (`Config.WebPortal`)

Optional in `Config.js`:

- `BindHost`: empty = Debug `*`, Release `localhost`. Set `*` or `0.0.0.0` to listen on all interfaces (e.g. LAN testing without a reverse proxy).
- `BindPort`: `0` = Debug `5000`, Release `5001`.

Production should still use a reverse proxy (Caddy/NGINX) for TLS when exposed to the internet.

### Portal page access (`PortalAccessManager`)

Per-page minimum access levels (0–5) gate both the React UI and API controllers via `HasPortalAccess(pageKey)`.

- **Code defaults**: `characters`, `leaderboards`, and `patch-notes` = 0 (all logged-in users); every other page = **4** (Developer) until overridden.
- **Runtime overrides**: Stored in **`ace_auth.portal_page_access`**. The table is **auto-created on server startup** (same pattern as `patch_notes`); the SQL file under `Database/Updates/Authentication/` is for manual/CI deploys. Changes from the Portal Security UI take effect **immediately** in memory — no restart, no JSON file on disk.
- **Deploy safety**: Settings live in the auth database with accounts and leaderboards; deploying a new binary does not reset them. New portal pages added in code use default level 4 until an admin saves a row.
- **Legacy JSON**: If a row-free table is found and `portal_page_access.json` exists from an older build, values are imported once into the database.
- **Editing**: Portal Security UI (`/portal-security`) or `PUT /api/portal-access/pages` with `{ "levels": { "quest-builder": 4 } }` — **Admin (5)** only.

### Account Scoping
Controllers inherit from `BaseController` to ensure standardized account-scoped authorization and logging:
- `HasPortalAccess(pageKey)`: Per-page minimum level from `PortalAccessManager` (preferred for portal routes).
- `IsAuthorizedForCharacter(guid)`: Own characters always; other accounts require `players` page access.
- `IsAdmin`: `CurrentAccessLevel > Player` (legacy helper; prefer page keys for new endpoints).

## Controller Reference

| Controller | Purpose | Data Source |
| :--- | :--- | :--- |
| **`AuthController`** | Handles login, secure cookie issuance, and session identity (`/api/auth/me`). | `DatabaseManager.Authentication` |
| **`CharacterController`** | Personal character list, Global Player List (Admin), and detail views (Stats/Skills/Inventory/Stamps). | `PlayerManager` (Online/Offline), `DatabaseManager.Shard`, `Account.GetCharacterQuestCompletions()` |
| **`PropertyController`** | Provides metadata and lookup for Game Properties. | `ACE.Entity.Enum.Properties` via Reflection |
| **`EnumController`** | List and details for system enums. | `ACE.Common.ReflectionCache` |
| **`ServerParamController`** | Read/Update server configuration settings. | `ACE.Common.ServerConfig` |
| **`IconController`** | Generates and serves high-fidelity PNG icons with support for underlays, overlays, and magical UiEffects. | `IconService` via `DatManager.PortalDat` |

## Architectural Hardening (The Snapshot Pattern)

To ensure thread-safety and high performance under load, the portal utilizes a **Captured Snapshot** pattern for all dynamic character data (Stats, Skills, Inventory).

1. **Atomic Cloning**: The server enters the character's `BiotaDatabaseLock` exactly once (via `GetBiotaSnapshot`) to perform a deep copy of all internal dictionaries and properties via `Biota.Clone(lock)`. 
2. **Lock-Free Processing**: Once the snapshot is captured, the lock is released. The remaining JSON generation for the web request proceeds on the private clone using the **Unified Logic Layer** (`IWeenieExtensions`), which is completely lock-free.
3. **Model Synchronization**: Data retrieved directly from the `ShardDatabase` (e.g., inventory assets) is automatically converted to logic-rich entity models using `BiotaConverter.ConvertToEntityBiota`. This ensures that all assets—whether live or archived—are processed with the same authoritative logic engines.

## Technical Gotchas & Standards

### Unsigned 32-bit ID Handling
Game IDs (e.g., Landblock IDs) are 32-bit unsigned integers. Because JavaScript bitwise operators default to **signed** 32-bit integers, standard right shifts (`>>`) will cause negative hex representations if the high bit is set.
- **Requirement**: Always use the **Unsigned Zero-Fill Right Shift (`>>>`)** in the frontend when extracting landblock prefixes or manipulating game IDs.

### Location Categorization
The portal uses a tiered category system for location resolution:
- **Category 1 (Special)**: High-priority hubs (Marketplace, Apartments).
- **Category 2 (Outdoors)**: General landscapes and islands.
- **Category 3 (Dungeons)**: Interior locations grouped by their 16-bit normalized landblock.

1. **`PlayerManager` / Memory-First Inventory**: The primary source for locating online players. For inventory retrieval, the server now follows a **Memory-First** strategy: if a player is online, their live `Inventory` and `EquippedObjects` are captured under a short read-lock from the `Player` object itself, ensuring the portal is authoritative for "dirty" items not yet flushed to the database.
2. **`AttributeFormula`**: The single source of truth for vitals and skill base-values. Manual math in controllers has been replaced with calls to this centralized logic layer to prevent "logic drift" between the server engine and the web portal.
3. **`ShardDatabase` (Performance Search)**: Partial name searches (e.g., `/api/character/search-all/{name}`) are performed at the SQL level using `GetCharacterStubsByPartialName`. This avoids the high CPU/Memory overhead of loading thousands of `OfflinePlayer` objects into memory to perform a `.Contains()` check.
4. **`ReflectionCache`**: Used to dynamically discover and document properties and enums without hardcoding lists in the API.

## Troubleshooting

### "Unexpected token '<', \"<!doctype \"... is not valid JSON"
This is a standard error encountered when the frontend `fetch` attempts to parse a response as JSON, but receives the `index.html` file (HTML) instead.

**Common Reasons**:
1. **Misspelled API Route**: The frontend requested a URL that doesn't exist (e.g., `/api/characers`), and the server's SPA fallback returned `index.html`.
2. **Missing Controller Mapping**: The API route exists, but the controller was not correctly registered (ensure `AddApplicationPart` is used in `WebPortalHost.cs`).

**The Safeguard (Implemented in `WebPortalHost.cs`)**:
To prevent this, the server's fallback logic is restricted to non-API paths. Any request starting with `/api/` that fails to match a controller will now return a proper **404 Not Found** instead of falling back to the HTML app.

```csharp
// Current implementation prevents /api fallback interception
app.MapWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"), builder => {
    builder.UseEndpoints(endpoints => {
        endpoints.MapFallbackToFile("index.html");
    });
});
```

- **404 on Refresh**: Ensure `MapFallbackToFile("index.html")` is present in the pipeline after `MapControllers`.
- **UI Not Updating**: Ensure `npm run build` is executed in `ACE.WebPortal/ClientApp` and the `dist` folder is correctly located by `WebPortalHost`.
- **Authorization Failures**: Verify the `AccountId` claim in the JWT matches the `AccountId` in the shard database for the requested character.

## Advanced Icon Rendering (`IconService.cs`)

The portal implements a custom rendering pipeline to transform raw game textures into web-ready PNGs with several high-fidelity features:

### 1. Pixel-Perfect Transparency
To avoid the "white box" artifact common in legacy AC assets, the `IconService` performs a post-processing sweep on all decoded textures. Any pixel with literal white values (`255, 255, 255`) that is **not** already transparent has its alpha forced to `0`. This surgically removes baked-in white outlines while preserving the item's internal shadows.

### 2. Composite Layering
The `/api/icon/{id}` endpoint supports dynamic layering via query parameters:
- `underlay`: Bases (e.g., ring bands, backpack slots).
- `overlay` / `overlaySecondary`: Additional sprite layers.
- **Order**: Underlay → Base Icon → Overlay.

### 3. Native UiEffects (Magic Outlines)
The server replicates the original client's "magical glow" by utilizing the `UiEffects` bitmask:
- If an item has `UiEffects` (e.g., Fire, Frost, Poison), the server loads the corresponding **swatch texture** from the `0x06` Portal Dat range.
- The "outline mask" pixels (the pure white pixels identified in the transparency step) are replaced with the corresponding patterned pixels from the swatch texture.
- This results in authentic, animated-style patterned outlines directly in the served PNG.

| Effect | Mask | Swatch Texture ID |
| :--- | :--- | :--- |
| **Magical** | `0x01` | `0x060011C5` |
| **Fire** | `0x20` | `0x06001B2E` |
| **Frost** | `0x80` | `0x06001B2F` |
| **Slashing** | `0x400` | `0x060033C2` |

