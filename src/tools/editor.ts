/** Console + play-mode tools. */
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { makeBridgeRequest } from "../services/bridgeClient.js";
import { handleBridgeError } from "../services/errors.js";
import { ResponseFormat, toolResult } from "../services/formatter.js";
import type { ConsoleLogEntry, PlayModeState } from "../types.js";
import {
  ConsoleLogsInput,
  PlayModeGetInput,
  PlayModeSetInput,
  type ConsoleLogsInputT,
  type PlayModeSetInputT,
} from "../schemas/editor.js";

export function registerEditorTools(server: McpServer): void {
  server.registerTool(
    "unity_get_console_logs",
    {
      title: "Get Console Logs",
      description: `Read buffered Unity console entries, most recent first. Filter by type.

Use when: 'summarize warnings/errors', 'check console after my change', verifying a fix.
For full stacks use response_format json (stackTrace included). Default types: warning+error.`,
      inputSchema: ConsoleLogsInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: ConsoleLogsInputT) => {
      try {
        const entries = await makeBridgeRequest<ConsoleLogEntry[]>("console/logs", {
          max_logs: p.max_logs,
          log_types: p.log_types,
        });
        const structured = { total: entries.length, logs: entries };
        const text =
          p.response_format === ResponseFormat.JSON
            ? JSON.stringify(structured, null, 2)
            : entries.length === 0
              ? `# Console: clean (no ${p.log_types.join("/")} entries)`
              : [
                  `# Console (${entries.length} entries)`,
                  ``,
                  ...entries.map(
                    (e) => `## [${e.type}] ${e.timestamp}\n${e.message.split("\n")[0]}`
                  ),
                  ``,
                  `Use response_format json for full messages + stacks.`,
                ].join("\n");
        return toolResult(text, structured);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_clear_console",
    {
      title: "Clear Console",
      description: `Clear the Unity console buffer (log cache in the bridge + Editor log view).

Use when: you want a clean baseline before reproducing an error.`,
      inputSchema: z.object({}).strict(),
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async () => {
      try {
        await makeBridgeRequest<{ cleared: boolean }>("console/clear");
        return toolResult(`# Console cleared`, { cleared: true });
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_get_play_mode",
    {
      title: "Get Play Mode State",
      description: `Report stopped|playing|paused. Use before set or before assuming edits apply (edits in play mode are discarded on stop).`,
      inputSchema: PlayModeGetInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async () => {
      try {
        const data = await makeBridgeRequest<{ state: PlayModeState }>("playmode/get");
        return toolResult(`# Play mode: ${data.state}`, data);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_set_play_mode",
    {
      title: "Set Play Mode",
      description: `Enter/exit play mode: play|stop|pause|unpause. Scene edits made while playing are lost on stop — prefer stopped for structural changes.`,
      inputSchema: PlayModeSetInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: PlayModeSetInputT) => {
      try {
        const data = await makeBridgeRequest<{ state: PlayModeState }>("playmode/set", {
          action: p.action,
        });
        return toolResult(`# Play mode -> ${data.state}`, data);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );
}
