# ACECustom project instructions

ACECustom is an ACEmulator fork, a C# server for Asheron's Call.
Follow upstream conventions and the repository's .editorconfig and
.gitattributes for formatting and line endings.

## Custom content and databases

Put custom content in Database/Updates/ as numbered patches. Never implement
custom content by editing Database/Base/: a world refresh with
ACE_SQL_DOWNLOAD_LATEST_WORLD_RELEASE=true overwrites that directory.
Inspect the existing patch numbering and conventions before adding a patch.

The project uses MySQL/MariaDB in Docker, with ace_auth, ace_shard, and
ace_world databases on ace-db:3306. Configuration is in docker.env; never
copy its credentials into instructions or committed files.

## Shared agent instructions

AGENTS.md is the shared source of project instructions. Read applicable nested
AGENTS.md files before working in a subdirectory. CLAUDE.md imports this file
for Claude; Codex should read AGENTS.md directly.

Use skills in .agents/skills/<name>/SKILL.md when relevant. Where paired copies
exist in .claude/skills/, keep them synchronized. Likewise, keep paired
.codex/agents/*.toml and .claude/agents/*.md instructions consistent when editing
them. Do not port .claude/launch.json or .claude/settings.local.json to Codex.

Never copy .env contents or secret values into instructions, committed files,
or responses. Preserve unrelated work already present in the repository.

## ACECustom Anti-Patterns & Engineering Standards

Follow these rules on every change to prevent regressions and review defects:

### 1. Transaction-First Safety (Exploit & Dupe Prevention)
- Always verify and consume required items/currencies (`TryConsumeFromInventoryWithNetworking`) **before** applying permanent state changes, stat modifications, or granting items/buffs.
- Never modify entity state first and consume items second; any unhandled exception or abort leaves the player with modified state and unconsumed items.

### 2. Enum Integrity & Collision Prevention
- **Append-only ordinals:** Never insert new values into the middle of unnumbered or upstream enums (such as `ActionType` in `IAction.cs`). Always append custom values to the end of the appropriate section to avoid shifting upstream integer ordinals.
- **Property ID Uniqueness:** When adding custom properties (`PropertyInt`, `PropertyFloat`, `PropertyString`, `PropertyBool`, `PropertyDataId`), always perform a whole-file search to verify the integer ID is not already used elsewhere in the file. C# enums allow duplicate integer values by default, causing silent collisions.

### 3. Multi-Pipeline Combat & Damage Security
- Any damage immunity or targeting restriction MUST be guarded across all three AC combat pipelines:
  - Direct / Melee: `Player_Combat.cs` (`DamageTarget`)
  - Projectiles: `SpellProjectile.cs` (guard before calculating damage and before creating enchantment procs)
  - Magic / Spells: `WorldObject_Magic.cs`
- When scaling damage (e.g., pet maturity, boss damage resistance), always scale critical hit modifiers (`BaseDamageMod.DamageMod`, `BaseDamageMod.BaseDamage.MaxDamage`) alongside base damage so critical strikes inherit the intended multipliers.
- Beneficent spell overrides (e.g. healing combat pets in encounter areas) must explicitly verify `spell.VitalDamageType == DamageType.Health` so stamina and mana spells are not unintentionally affected.

### 4. Chat & Soul Emote Matching
- Never use loose `.Contains("phrase")` on chat/soul emote text for gameplay triggers. Always use trimmed exact equality (`trimmed.Equals("dance", StringComparison.OrdinalIgnoreCase)`) to avoid false triggers from everyday conversational words (e.g., "attendance", "guidance").

### 5. SQL Patches & Client Compatibility
- Never include virtual/generated columns (`landblock` on `position` table) in SQL `INSERT` statements; MariaDB/MySQL auto-derives them from `obj_Cell_Id`.
- Never use broad range `DELETE` statements (e.g. `variation_Id BETWEEN 2 AND 10`) on shared landblocks. Always restrict `DELETE` queries strictly to the exact `weenie_Class_Id`s being recreated.
- Strictly 7-bit ASCII and LF line endings on all SQL patches and server code.
- Never use Unicode block characters (such as full block U+2588) in chat strings or command outputs (they render as corrupted glyphs in the 1999 AC client font); use plain ASCII equivalents (e.g. `#`).

