/** C# script tools: list / read / create / update / attach. */
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { makeBridgeRequest } from "../services/bridgeClient.js";
import { handleBridgeError } from "../services/errors.js";
import {
  ResponseFormat,
  paginate,
  toolResult,
} from "../services/formatter.js";
import {
  AttachScriptInput,
  CreateScriptInput,
  ListScriptsInput,
  ReadScriptInput,
  UpdateScriptInput,
  type AttachScriptInputT,
  type CreateScriptInputT,
  type ListScriptsInputT,
  type ReadScriptInputT,
  type UpdateScriptInputT,
} from "../schemas/scripts.js";

interface ScriptEntry {
  path: string;
  size: number;
  modified: string;
}

interface ScriptContent {
  path: string;
  total_lines: number;
  start_line: number;
  content: string;
}

export function registerScriptTools(server: McpServer): void {
  server.registerTool(
    "unity_list_scripts",
    {
      title: "List C# Scripts",
      description: `List .cs files under a project folder with sizes. Paginated.

Args: folder (default 'Assets'), filter substring (optional), limit/offset.
Use when: finding where game code lives before reading or editing it.`,
      inputSchema: ListScriptsInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: ListScriptsInputT) => {
      try {
        const all = await makeBridgeRequest<ScriptEntry[]>("script/list", {
          folder: p.folder,
          filter: p.filter,
        });
        const page = paginate(all, p.limit, p.offset);
        const text =
          p.response_format === ResponseFormat.JSON
            ? JSON.stringify(page, null, 2)
            : [
                `# Scripts in ${p.folder} (${page.total} total, showing ${page.count})`,
                ``,
                ...page.items.map((s) => `- \`${s.path}\` (${s.size} bytes)`),
              ].join("\n");
        return toolResult(text, page);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_read_script",
    {
      title: "Read C# Script",
      description: `Read a script with line numbers, paginated. Always read before updating.

Args: path ('Assets/Scripts/X.cs'), start_line (1-based, default 1), max_lines (default 200).
Returns { path, total_lines, start_line, content } — content lines prefixed 'line: code'.`,
      inputSchema: ReadScriptInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: ReadScriptInputT) => {
      try {
        const data = await makeBridgeRequest<ScriptContent>("script/read", {
          path: p.path,
          start_line: p.start_line,
          max_lines: p.max_lines,
        });
        const text =
          p.response_format === ResponseFormat.JSON
            ? JSON.stringify(data, null, 2)
            : [
                `# ${data.path} (lines ${data.start_line}-${data.start_line + data.content.split("\n").length - 1} of ${data.total_lines})`,
                "```csharp",
                data.content,
                "```",
              ].join("\n");
        return toolResult(text, data as unknown as Record<string, unknown>);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_create_script",
    {
      title: "Create C# Script",
      description: `Create a script from a Unity 6 template. Refreshes AssetDatabase so Unity compiles it.

Args: path ('Assets/Scripts/PlayerController.cs'), template monobehaviour|scriptableobject|editor|empty, class_name (default = file name; must match file name), overwrite (default false).
After creating, wait for compilation (console check) before attaching.`,
      inputSchema: CreateScriptInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false },
    },
    async (p: CreateScriptInputT) => {
      try {
        const data = await makeBridgeRequest<{ path: string; className: string }>("script/create", {
          path: p.path,
          template: p.template,
          class_name: p.class_name,
          overwrite: p.overwrite,
        });
        return toolResult(
          `# Created \`${data.path}\` (class ${data.className})\nWait for compilation, then attach with unity_attach_script.`,
          data as unknown as Record<string, unknown>
        );
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_update_script",
    {
      title: "Update C# Script",
      description: `Replace an exact text block in a script (like a focused diff hunk). Fails unless old_text occurs exactly expected_occurrences times.

Workflow: unity_read_script first, copy old_text verbatim (whitespace matters), then call with new_text.
Returns { path, replaced, total_lines }. Unity recompiles after the change — check console for errors.`,
      inputSchema: UpdateScriptInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: UpdateScriptInputT) => {
      try {
        const data = await makeBridgeRequest<{ path: string; replaced: number; total_lines: number }>(
          "script/update",
          {
            path: p.path,
            old_text: p.old_text,
            new_text: p.new_text,
            expected_occurrences: p.expected_occurrences,
          }
        );
        return toolResult(
          `# Updated \`${data.path}\` (${data.replaced} replacement(s), now ${data.total_lines} lines)\nCheck console for compile errors.`,
          data as unknown as Record<string, unknown>
        );
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_attach_script",
    {
      title: "Attach Script to GameObject",
      description: `Attach a MonoBehaviour class to a GameObject (Undo-able). Script must be compiled — no compile errors in console.

Args: object_path ('Player'), script_class ('PlayerController').
Fails with hint if the class is not found (typo? not compiled yet?) — read the error.`,
      inputSchema: AttachScriptInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false },
    },
    async (p: AttachScriptInputT) => {
      try {
        const data = await makeBridgeRequest<{ type: string; object: string }>("script/attach", {
          object_path: p.object_path,
          script_class: p.script_class,
        });
        return toolResult(
          `# Attached ${data.type} to \`${data.object}\``,
          data as unknown as Record<string, unknown>
        );
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );
}
