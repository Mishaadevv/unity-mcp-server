/** Scene / project read tools + save. */
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { makeBridgeRequest } from "../services/bridgeClient.js";
import { handleBridgeError } from "../services/errors.js";
import {
  ResponseFormat,
  paginate,
  toolResult,
} from "../services/formatter.js";
import type {
  HierarchyNode,
  ProjectInfo,
  SceneInfo,
} from "../types.js";
import { HierarchyInput } from "../schemas/gameobject.js";

const EmptyInput = z.object({}).strict();

function formatHierarchyMarkdown(
  nodes: HierarchyNode[],
  total: number,
  offset: number
): string {
  const lines = [`# Scene Hierarchy`, ``, `Showing ${nodes.length} of ${total} root objects (offset ${offset})`, ``];
  for (const n of nodes) {
    lines.push(
      `- **${n.name}** (\`${n.path}\`) — ${n.active ? "active" : "inactive"}, ${n.childCount} children, [${n.components.join(", ")}]`
    );
  }
  lines.push(
    ``,
    `Use limit/offset for more, or unity_find_gameobjects for search.`
  );
  return lines.join("\n");
}

export function registerSceneTools(server: McpServer): void {
  server.registerTool(
    "unity_ping",
    {
      title: "Ping Unity Editor",
      description: `Check whether the Unity Editor bridge is reachable and report Unity/editor versions.

Use when: before any other Unity tool, or when calls fail with connection errors.
Don't use when: you already have a fresh successful response — skip re-pinging.`,
      inputSchema: EmptyInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async () => {
      try {
        const data = await makeBridgeRequest<{ unityVersion: string; bridgeVersion: string }>("ping");
        const structured = { ok: true, ...data };
        const text = `# Unity bridge: OK\n- Unity: ${data.unityVersion}\n- Bridge: ${data.bridgeVersion}`;
        return toolResult(text, structured);
      } catch (error) {
        return { content: [{ type: "text" as const, text: handleBridgeError(error) }] };
      }
    }
  );

  server.registerTool(
    "unity_get_project_info",
    {
      title: "Get Unity Project Info",
      description: `Get project name, path, Unity version, active scene and play-mode state.

Use when: starting a session to orient yourself in the project.
Returns: { projectName, projectPath, unityVersion, activeScene{name,path,isDirty,rootCount}, playMode }.`,
      inputSchema: EmptyInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async () => {
      try {
        const data = await makeBridgeRequest<ProjectInfo>("project/info");
        const text = [
          `# Project: ${data.projectName}`,
          ``,
          `- Path: ${data.projectPath}`,
          `- Unity: ${data.unityVersion}`,
          `- Scene: ${data.activeScene.name} (\`${data.activeScene.path}\`)${data.activeScene.isDirty ? " [UNSAVED]" : ""}`,
          `- Play mode: ${data.playMode}`,
        ].join("\n");
        return toolResult(text, data);
      } catch (error) {
        return { content: [{ type: "text" as const, text: handleBridgeError(error) }] };
      }
    }
  );

  server.registerTool(
    "unity_get_scene_info",
    {
      title: "Get Active Scene Info",
      description: `Get the active scene: name, path, dirty flag, root object count, build index.

Use when: you need to know which scene is open before querying hierarchy.`,
      inputSchema: EmptyInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async () => {
      try {
        const data = await makeBridgeRequest<SceneInfo>("scene/info");
        const text = `# Scene: ${data.name}\n- Path: ${data.path}\n- Dirty: ${data.isDirty}\n- Roots: ${data.rootCount}\n- Build index: ${data.buildIndex}`;
        return toolResult(text, data);
      } catch (error) {
        return { content: [{ type: "text" as const, text: handleBridgeError(error) }] };
      }
    }
  );

  server.registerTool(
    "unity_get_hierarchy",
    {
      title: "Get Scene Hierarchy",
      description: `List root GameObjects of the active scene with components summary. Paginated.

Args:
- root_only (bool, default true): only roots; false includes one level of children in 'children'
- limit (1-100, default 20), offset (default 0)
- response_format markdown|json

Use when: exploring what is in the scene. For search use unity_find_gameobjects.
Returns markdown list + structured { total, count, offset, items, has_more, next_offset }.`,
      inputSchema: HierarchyInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (params) => {
      try {
        const all = await makeBridgeRequest<HierarchyNode[]>("scene/hierarchy", {
          root_only: params.root_only,
        });
        const page = paginate(all, params.limit, params.offset);
        const structured = { ...page };
        const text =
          params.response_format === ResponseFormat.JSON
            ? JSON.stringify(structured, null, 2)
            : formatHierarchyMarkdown(page.items, page.total, page.offset);
        return toolResult(text, structured);
      } catch (error) {
        return { content: [{ type: "text" as const, text: handleBridgeError(error) }] };
      }
    }
  );

  server.registerTool(
    "unity_save_scene",
    {
      title: "Save Active Scene",
      description: `Save the active scene to disk (AssetDatabase + EditorSceneManager.SaveOpenScenes).

Use when: after create/delete/transform batches so work is not lost.
Idempotent: saving twice has no extra effect.`,
      inputSchema: EmptyInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async () => {
      try {
        const data = await makeBridgeRequest<{ saved: boolean; path: string }>("scene/save");
        return toolResult(`# Scene saved\n- Path: ${data.path}`, data);
      } catch (error) {
        return { content: [{ type: "text" as const, text: handleBridgeError(error) }] };
      }
    }
  );
}
