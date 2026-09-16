# ACECustom — Project Rules

## HARD RULE: No emoji or non-ASCII in anything the game client renders

The Asheron's Call client cannot render emoji or most non-ASCII characters. It draws them as
garbage glyphs — a bare carriage return shows up as a music note, and emoji render as boxes or
mojibake. This is not a style preference; it visibly corrupts the UI.

**Every string that reaches the AC client must be plain 7-bit ASCII.** That includes:

- Appraisal / ID panel text (`PropertyString.LongDesc`, `BuildBreedingAppraisalBlock`, `BuildPotencyAppraisalBlock`, and anything else assembled in `AppraiseInfo`)
- `SendMessage`, `SendTransientError`, `ChatPacket.SendServerMessage`, `GameMessageSystemChat`
- `CommandHandlerHelper.WriteOutputInfo` and all admin/developer command output
- Item, creature and weenie names, `PropertyString.Use`, and any other stored string shown in-game

Use ASCII substitutes instead:

| Instead of | Use |
|---|---|
| any emoji (star, warning, check, cross) | `[OK]`, `[WARNING]`, `[ERROR]`, or just drop it |
| `—` `–` (em/en dash) | `-` |
| `−` (minus sign U+2212) | `-` |
| `×` (multiplication sign) | `x` |
| `…` (ellipsis) | `...` |
| `✓` `✅` | `[OK]` or nothing |
| `⚠` `⚠️` | `[WARNING]` |

### Line endings matter too

`StringBuilder.AppendLine()` emits `\r\n` on Windows, and the client draws the bare `\r` as a
music note at the end of every line. **Build client-facing text with explicit `"\n"`**, or strip the
carriage returns before sending:

```csharp
return sb.ToString().Replace("\r\n", "\n").TrimEnd();
```

### Where emoji ARE allowed

- Discord webhook messages (`ActionQueue`, `SessionPoolMonitor`, `WorldManager`, `WorldObject_Database`) — Discord renders them fine.
- Console output in test harnesses and load-test runners.
- Markdown documentation and generated README files.
- Source code comments (never displayed to a player).

Git commit messages should also stay plain ASCII.

## Build and run

The server runs from the **x64 Release** output, not the AnyCPU one:

```
Source/ACE.Server/bin/x64/Release/net10.0/start_server.bat
```

Visual Studio's `Release|x64` configuration builds to `bin/x64/Release/net10.0/`. A stale AnyCPU
build may also exist at `bin/Release/net10.0/` — do not launch from there, and do not assume a
`dotnet build` without `-c Release` and the x64 platform has updated what actually runs.

When verifying a change compiles, `dotnet build ... -t:Compile` only writes to `obj/`. It does not
update any `bin/` directory, so it proves the code compiles but nothing more.

## Server config

`ServerConfig` properties (`PropertyManager.cs`) load their values from the shard database at
startup, and stored values **override the defaults declared in code**. Changing a default in source
has no effect on an existing server. Use `@showprops` to read effective values and
`@modifybool` / `@modifylong` / `@modifydouble` / `@modifystring` to change them live.
