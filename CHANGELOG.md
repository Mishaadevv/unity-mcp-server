# Changelog

All notable changes to `unity-mcp-server` and the Unity Editor bridge package
(`com.local.unity-mcp`) are documented here.

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
