# Changelog

All notable changes to `unity-mcp-server` and the Unity Editor bridge package
(`com.local.unity-mcp`) are documented here.

## [0.1.2] - 2026-10-05

### Added
- Port auto-pick: bridge tries the preferred port then scans upward (+100),
  exposes `ActivePort`, writes `%TEMP%/unity-mcp-bridge.port`; the TS server
  discovers it automatically (`UNITY_BRIDGE_PORT` still wins when set).
- `Samples~/SelfPlayGlue/MCPGameGlue.cs` documents `RegisterHandler`.

### Fixed
- Watchdog backoff + give-up after 8 failed starts (no console spam).
- Suppressed benign `UAC0007` analyzer warning in code-exec references.

## [0.1.1] - 2026-10-05

### Fixed
- Bridge package is now fully game-agnostic: removed direct references to
  project game classes (`TankHealth`, `TankController`, `TankAI`,
  `TankMCPInput`), which broke compilation of the package in every project
  (UPM assemblies cannot reference project code). Game methods
  (`input/set`, `input/clear`, `game/state`) moved to project-side glue.
- Added `UnityMCPBridge.RegisterHandler` / `UnregisterHandler` so games
  register custom JSON-RPC methods at editor load.
- Added `Samples~/SelfPlayGlue/MCPGameGlue.cs` optional sample.
- Watchdog: exponential backoff + give-up after 8 failures (no more console spam).

## [0.1.0] - 2026-10-05

Initial release.

### MCP server (TypeScript, stdio)
- 30 tools: scene/project info, hierarchy, save; GameObject find/info/components/
  create/delete/transform/add-component/set-property; console logs/clear;
  play-mode get/set; C# scripts list/read/create/update/attach; assets
  list/import/dependencies/folders; prefab instantiate/apply; sandboxed
  `execute_code` + result polling.
- Zod `.strict()` validation, `markdown|json` formats, pagination, character
  limit truncation, actionable bridge-unreachable errors.

### Unity Editor bridge (C# UPM package)
- `HttpListener` on `127.0.0.1:6400`, main-thread dispatcher, `Undo` for all
  mutations, mutations toggle, auto-start, `Window > Unity MCP` status window.
- Watchdog self-heal: restarts the listener if its thread dies.
- Sandboxed async C# execution via `AssemblyBuilder` (substring blocklist for
  filesystem/network/process/reflection-load, off by default, job ids + polling).

### Demo content (external test project, not shipped)
- Third-person character controller + orbit camera (new Input System).
- WoT-style tank battle: player `TankController` + `TankAI` bot, HP pool,
  ±25% damage RNG, angle-based armor factor, reload timers, splash, tracers,
  wreck + respawn, OnGUI HP/reload bars.
