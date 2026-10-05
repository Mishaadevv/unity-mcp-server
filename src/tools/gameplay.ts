/** AI self-play: remote player input, game-state snapshot, asset refresh. */
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { makeBridgeRequest } from "../services/bridgeClient.js";
import { handleBridgeError } from "../services/errors.js";
import { ResponseFormat, toolResult } from "../services/formatter.js";
import { EmptyInput, SetInput, type SetInputT } from "../schemas/gameplay.js";

export function registerGameplayTools(server: McpServer): void {
  server.registerTool(
    "unity_set_player_input",
    {
      title: "Set Player Input (AI self-play)",
      description: `Drive the player tank remotely: throttle/steer become the TankController input (physical keyboard ignored while override is on). Fire is edge-triggered — one call queues exactly one shot.

Loop: unity_get_game_state -> decide -> unity_set_player_input (repeat ~2-5x/sec) -> poll result.
Call unity_clear_player_input to hand control back to the human.`,
      inputSchema: SetInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false },
    },
    async (p: SetInputT) => {
      try {
        const data = await makeBridgeRequest<Record<string, unknown>>("input/set", {
          throttle: p.throttle,
          steer: p.steer,
          fire: p.fire,
        });
        return toolResult(
          `# Override ON (throttle=${p.throttle}, steer=${p.steer}, fire=${p.fire})`,
          data
        );
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_clear_player_input",
    {
      title: "Clear Player Input Override",
      description: `Hand control back to the human: disables the remote override, zeroes throttle/steer, drops queued shots.`,
      inputSchema: EmptyInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async () => {
      try {
        const data = await makeBridgeRequest<Record<string, unknown>>("input/clear");
        return toolResult(`# Override OFF — human drives again`, data);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_get_game_state",
    {
      title: "Get Game State Snapshot",
      description: `One-call snapshot for self-play: every tank's team, hp/maxHP, alive, position [x,y,z], hull yaw, turret yaw, reload fraction (1 = ready).

Use in the drive loop before each unity_set_player_input. JSON format recommended for parsing.`,
      inputSchema: EmptyInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async () => {
      try {
        const data = await makeBridgeRequest<{ tanks: unknown[] }>("game/state");
        const tanks = data.tanks ?? [];
        const lines = [`# Game state (${tanks.length} tanks)`, ``];
        for (const t of tanks as Array<Record<string, unknown>>) {
          const p = t["position"] as number[];
          lines.push(
            `- **${t["name"]}** [${t["team"]}] HP ${t["hp"]}/${t["maxHP"]}${t["alive"] ? "" : " DEAD"} @ [${p?.map((n) => Number(n).toFixed(1)).join(", ")}] reload ${(Number(t["reloadFrac"]) * 100).toFixed(0)}%`
          );
        }
        return toolResult(lines.join("\n"), data as unknown as Record<string, unknown>);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_refresh_assets",
    {
      title: "Refresh AssetDatabase",
      description: `Trigger Unity asset reimport + script recompilation on demand (AssetDatabase.Refresh).

Use when: files were written to the project from outside the Editor (scripts, models, textures) and Unity hasn't picked them up yet — e.g. background work while the Editor is unfocused. Idempotent.`,
      inputSchema: EmptyInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async () => {
      try {
        await makeBridgeRequest<{ refreshed: boolean }>("asset/refresh");
        return toolResult(`# AssetDatabase refreshed`, { refreshed: true });
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );
}
