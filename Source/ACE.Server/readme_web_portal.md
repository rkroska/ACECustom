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
    - **Production**: Binds to **loopback only** (`localhost`) on port **80**, ensuring administrative traffic remains private.
- **Reverse Proxy Requirement**: In production, a reverse proxy (e.g., NGINX or IIS) is required to terminate SSL/TLS and forward requests to `http://localhost:80`.
- **Dynamic Secret**: Automatically generated each time the server starts (`WebPortalHost.Secret`).

## Production Deployment (HTTPS)

To securely expose the Web Portal to external users, configure a reverse proxy to handle encryption. 

### Sample NGINX Configuration
```nginx
server {
    listen 443 ssl;
    server_name portal.yourdomain.com;

    ssl_certificate /path/to/fullchain.pem;
    ssl_certificate_key /path/to/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:80;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### Account Scoping
Controllers inherit from `BaseController` to ensure standardized account-scoped authorization and logging:
- `IsAuthorizedForAccount(accountId)`: Automatically extracts the `AccountId` claim from the user's JWT and compares it against the provided `accountId`. 
- `IsAdmin`: A helper property that verifies if the `CurrentAccessLevel` is greater than `Player`. When `true`, all account-scoping checks for character details, stats, and skills are bypassed, allowing global administrative visibility.
- `AccessLevel` checking: Extends authorization to verify specific administrative roles (e.g., `Admin`, `Developer`) for sensitive operations like global player searches.

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

## Authoritative Data Sources

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

