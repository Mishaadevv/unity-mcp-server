# unity-mcp-server

MCP server for **Unity 6 Editor** integration. Exposes scene, GameObject, component, console and play-mode tools to any MCP client (Claude Code, Cursor, Windsurf, VS Code Copilot) via **stdio**, forwarding to a small **C# Editor bridge** over HTTP on `127.0.0.1`.

```text
MCP client ←stdio→ unity-mcp-server (this repo) ←HTTP 127.0.0.1:6400→ Unity 6 Editor (UnityPackage/)
```

## Requirements

- Node.js 18+ (tested on 22)
- Unity **6000.0+** (tested on 6000.6.0f1)
- `com.unity.nuget.newtonsoft-json` 3.2.1 (declared as package dependency, auto-resolved by UPM)

## Install

### 1. Editor bridge — IMPORTANT: pick the nested package

1. `Window → Package Manager → + → Add package from disk`
2. Select **`Unity-MCP/UnityPackage/package.json`** (that exact file, not the repo root!)

> The `Package '@modelcontextprotocol/sdk' is invalid / zod Version '^3.23.8' is invalid` error means the repo root was selected — it holds the npm `package.json`, which UPM cannot parse. The correct file is `UnityPackage/package.json` (`com.local.unity-mcp`).

Option B — copy folder:
1. Copy `UnityPackage/` into your project's `Packages/com.local.unity-mcp/`

Then open **Window → Unity MCP** → `Start`. Status must show *Running on 127.0.0.1:6400*.

### 2. MCP server

```bash
npm install
npm run build
```

Add to your MCP client config (Claude Code `claude_desktop_config.json` / Cursor `mcp.json`):

```json
{
  "mcpServers": {
    "unity": {
      "command": "node",
      "args": ["C:/Users/Misha Krutoj/Documents/Unity-MCP/dist/index.js"],
      "env": { "UNITY_BRIDGE_PORT": "6400" }
    }
  }
}
```

Verify: ask the agent *"Ping Unity and summarize the active scene"*.

## Tools (34)

### Scene / objects / editor (17, MVP)

| Tool | Type | Description |
|---|---|---|
| `unity_ping` | read | Bridge reachability + versions |
| `unity_get_project_info` | read | Project, Unity version, scene, play-mode |
| `unity_get_scene_info` | read | Active scene details |
| `unity_get_hierarchy` | read | Root objects, paginated (`limit`/`offset`) |
| `unity_save_scene` | write (idempotent) | Save open scenes |
| `unity_find_gameobjects` | read | Search by `name_contains`/`tag`/`with_component` |
| `unity_get_object_info` | read | Transform + components of one object |
| `unity_get_components` | read | Component list of one object |
| `unity_create_gameobject` | write | Empty or primitive, optional parent |
| `unity_delete_gameobject` | write (destructive) | Delete by path |
| `unity_set_transform` | write (idempotent) | Partial pos/rot/scale update |
| `unity_add_component` | write | Add by type name |
| `unity_set_component_property` | write (idempotent) | One serialized field via JSON value |
| `unity_get_console_logs` | read | Recent logs, filter `log/warning/error` |
| `unity_clear_console` | write (idempotent) | Clear buffer |
| `unity_get_play_mode` | read | `stopped/playing/paused` |
| `unity_set_play_mode` | write | `play/stop/pause/unpause` |

### Scripts (5)

| Tool | Type | Description |
|---|---|---|
| `unity_list_scripts` | read | `.cs` files under folder, paginated |
| `unity_read_script` | read | File content with line numbers, paginated — read before updating |
| `unity_create_script` | write | From template (`monobehaviour/scriptableobject/editor/empty`), `AssetDatabase.Refresh` |
| `unity_update_script` | write (idempotent) | Exact `old_text` → `new_text` block replace, occurrence-count guard |
| `unity_attach_script` | write | Attach compiled `MonoBehaviour` class to GameObject |

### Prefabs / assets (6)

| Tool | Type | Description |
|---|---|---|
| `unity_list_assets` | read | Browse `AssetDatabase` by folder/name/type, paginated |
| `unity_import_asset` | write (idempotent) | Force reimport (picks up external changes) |
| `unity_asset_dependencies` | read | Direct + transitive deps of an asset |
| `unity_folder_structure` | read | Folder tree map, depth 1-5 |
| `unity_instantiate_prefab` | write | Instantiate prefab, keeps prefab link |
| `unity_apply_prefab` | write | Apply instance overrides to prefab source |

### Sandboxed code execution (2)

| Tool | Type | Description |
|---|---|---|
| `unity_execute_code` | write (destructive, async) | Compile + run bare C# statements via `AssemblyBuilder`, returns `job_id` |
| `unity_get_code_result` | read | Poll job: `running` / `done {output, logs}` / `error` |

Code rules: bare statements in a static `Run()` (`return` sends JSON back); blocklist covers filesystem/network/process/reflection-load/`Application.Quit`/infinite-loop patterns. Requires mutations ON **and** `Enable C# execution` in `Window > Unity MCP` (off by default).

### AI self-play + background (4)

| Tool | Type | Description |
|---|---|---|
| `unity_set_player_input` | write | Remote drive: throttle/steer/fire override for the player tank |
| `unity_clear_player_input` | write (idempotent) | Hand control back to the human |
| `unity_get_game_state` | read | Snapshot: every tank's team/HP/pose/turret/reload |
| `unity_refresh_assets` | write (idempotent) | `AssetDatabase.Refresh` on demand (background file drops) |

All tools support `response_format: markdown | json`, return `content + structuredContent`, and use annotations (`readOnlyHint`, `destructiveHint`, `idempotentHint`).

All mutations go through Unity `Undo` — revert with `Ctrl+Z` in the Editor. Disable writes via the *Enable mutations* toggle in `Window → Unity MCP`.

## Env vars

| Var | Default | Description |
|---|---|---|
| `UNITY_BRIDGE_HOST` | `127.0.0.1` | Bridge host (keep loopback) |
| `UNITY_BRIDGE_PORT` | `6400` | Must match Editor window port |

## Development

```bash
npm run dev    # watch mode (tsx)
npm run build  # tsc → dist/
```

Test with MCP Inspector:

```bash
npx @modelcontextprotocol/inspector node dist/index.js
```

## Security

- Binds `127.0.0.1` only, 2 MB body cap, 25 s main-thread timeout.
- Arbitrary C# execution exists but is **sandboxed and off by default** (blocklist + explicit opt-in toggle + job timeouts).
- Bridge self-heals via watchdog (restarts listener if its thread dies).
- No auth (local single-user). Do not expose the port.

## Roadmap

1. **Scripts** — ✅ done (`list/read/create/update/attach`).
2. **Prefabs/assets** — ✅ done (`list/import/dependencies/folders`, `instantiate/apply`).
3. **Safe code exec** — ✅ done (sandboxed `execute_code` + `job_id` polling, off by default).
4. **Visual QA**: `take_screenshot`, scene-view control, `undo/redo`, `batch_call`.
5. **Pipeline**: `build_project`, `packages`, `run_tests`.
6. **Scale**: tool groups, multi-instance routing (port from project-path hash), Streamable HTTP variant, MCPB packaging.

## License

MIT — see [LICENSE](./LICENSE).
