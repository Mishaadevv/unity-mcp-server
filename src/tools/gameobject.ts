/** GameObject CRUD + components + transform tools. */
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { makeBridgeRequest } from "../services/bridgeClient.js";
import { handleBridgeError } from "../services/errors.js";
import {
  ResponseFormat,
  paginate,
  toolResult,
} from "../services/formatter.js";
import type {
  ComponentSummary,
  GameObjectInfo,
  HierarchyNode,
} from "../types.js";
import {
  AddComponentInput,
  ComponentsInput,
  CreateObjectInput,
  DeleteObjectInput,
  FindObjectsInput,
  ObjectInfoInput,
  SetPropertyInput,
  SetTransformInput,
  type AddComponentInputT,
  type ComponentsInputT,
  type CreateObjectInputT,
  type DeleteObjectInputT,
  type FindObjectsInputT,
  type ObjectInfoInputT,
  type SetPropertyInputT,
  type SetTransformInputT,
} from "../schemas/gameobject.js";

function objectInfoMarkdown(o: GameObjectInfo): string {
  const t = o.transform;
  return [
    `# ${o.name} (\`${o.path}\`)`,
    ``,
    `- Active: ${o.active}, tag: ${o.tag}, layer: ${o.layer}`,
    `- Position: [${t.position.x}, ${t.position.y}, ${t.position.z}]`,
    `- Rotation: [${t.rotationEuler.x}, ${t.rotationEuler.y}, ${t.rotationEuler.z}]`,
    `- Scale: [${t.scale.x}, ${t.scale.y}, ${t.scale.z}]`,
    ``,
    `## Components`,
    ...o.components.map((c) => `- ${c.type}${c.enabled === false ? " (disabled)" : ""}`),
  ].join("\n");
}

export function registerGameObjectTools(server: McpServer): void {
  server.registerTool(
    "unity_find_gameobjects",
    {
      title: "Find GameObjects",
      description: `Search GameObjects by name substring, tag, or component type. At least one filter required. Paginated.

Examples:
- name_contains "Player" -> players, spawn points
- tag "Enemy" -> all enemies
- with_component "Rigidbody" -> physics objects
Returns { total, count, offset, items[{name,path,active}], has_more, next_offset }.`,
      inputSchema: FindObjectsInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: FindObjectsInputT) => {
      try {
        const all = await makeBridgeRequest<HierarchyNode[]>("object/find", {
          name_contains: p.name_contains,
          tag: p.tag,
          with_component: p.with_component,
        });
        const page = paginate(all, p.limit, p.offset);
        const text =
          p.response_format === ResponseFormat.JSON
            ? JSON.stringify(page, null, 2)
            : [
                `# Found ${page.total} objects (showing ${page.count})`,
                ``,
                ...page.items.map(
                  (n) => `- **${n.name}** (\`${n.path}\`) — ${n.active ? "active" : "inactive"}`
                ),
              ].join("\n");
        return toolResult(text, page);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_get_object_info",
    {
      title: "Get GameObject Info",
      description: `Full details of one GameObject by hierarchy path: transform, tag/layer, components list.

Use path like 'Player' or 'Level/Enemies/Orc'. For search use unity_find_gameobjects first.`,
      inputSchema: ObjectInfoInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: ObjectInfoInputT) => {
      try {
        const data = await makeBridgeRequest<GameObjectInfo>("object/info", { path: p.path });
        const text =
          p.response_format === ResponseFormat.JSON
            ? JSON.stringify(data, null, 2)
            : objectInfoMarkdown(data);
        return toolResult(text, data);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_get_components",
    {
      title: "Get Components",
      description: `List all components on a GameObject with enabled flags.

Use when: inspecting what behaviour/rendering an object has before modifying it.`,
      inputSchema: ComponentsInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: ComponentsInputT) => {
      try {
        const data = await makeBridgeRequest<ComponentSummary[]>("object/components", {
          path: p.path,
        });
        const structured = { path: p.path, components: data };
        const text =
          p.response_format === ResponseFormat.JSON
            ? JSON.stringify(structured, null, 2)
            : [`# Components of \`${p.path}\``, ``, ...data.map((c) => `- ${c.type}`)].join("\n");
        return toolResult(text, structured);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_create_gameobject",
    {
      title: "Create GameObject",
      description: `Create an empty GameObject or a Unity primitive (cube/sphere/capsule/cylinder/plane/quad), optionally parented. Uses Undo so it can be reverted in Editor.

Args: name (required), primitive_type (default none), position/rotation/scale arrays, parent_path (optional).
Returns { path, name }. Save with unity_save_scene when done.`,
      inputSchema: CreateObjectInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false },
    },
    async (p: CreateObjectInputT) => {
      try {
        const data = await makeBridgeRequest<{ path: string; name: string }>("object/create", {
          name: p.name,
          primitive_type: p.primitive_type,
          position: p.position ?? [0, 0, 0],
          rotation: p.rotation ?? [0, 0, 0],
          scale: p.scale ?? [1, 1, 1],
          parent_path: p.parent_path,
        });
        return toolResult(`# Created\n- Path: \`${data.path}\``, data);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_delete_gameobject",
    {
      title: "Delete GameObject",
      description: `Delete a GameObject by path (Undo-able in Editor, but file-wise destructive once scene is saved).

Provide exact path from unity_find_gameobjects. Double-check path before calling.`,
      inputSchema: DeleteObjectInput,
      annotations: { readOnlyHint: false, destructiveHint: true, idempotentHint: false, openWorldHint: false },
    },
    async (p: DeleteObjectInputT) => {
      try {
        const data = await makeBridgeRequest<{ deleted: boolean; path: string }>("object/delete", {
          path: p.path,
        });
        return toolResult(`# Deleted \`${data.path}\``, data);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_set_transform",
    {
      title: "Set Transform",
      description: `Partially update position (world), rotation euler degrees (world), scale (local). Provide only fields to change.

Example: {"path":"Player","position":[0,1,0]} moves player without touching rotation.`,
      inputSchema: SetTransformInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: SetTransformInputT) => {
      try {
        const data = await makeBridgeRequest<GameObjectInfo>("object/setTransform", {
          path: p.path,
          position: p.position,
          rotation: p.rotation,
          scale: p.scale,
        });
        return toolResult(objectInfoMarkdown(data), data);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_add_component",
    {
      title: "Add Component",
      description: `Add a component by type name to a GameObject. Uses Undo.

Example: {"path":"Player","component_type":"Rigidbody"}. Check unity_get_components first to avoid duplicates.`,
      inputSchema: AddComponentInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false },
    },
    async (p: AddComponentInputT) => {
      try {
        const data = await makeBridgeRequest<ComponentSummary>("object/addComponent", {
          path: p.path,
          component_type: p.component_type,
        });
        return toolResult(`# Added ${data.type} to \`${p.path}\``, data);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_set_component_property",
    {
      title: "Set Component Property",
      description: `Set one serialized property/field on a component. Value is parsed as JSON.

Examples:
- {"path":"Player","component_type":"Rigidbody","property":"mass","value_json":"5.0"}
- {"path":"Sun","component_type":"Light","property":"intensity","value_json":"2.5"}
Fails with allowed-type hint if the property is not settable — read the error and retry.`,
      inputSchema: SetPropertyInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: SetPropertyInputT) => {
      try {
        let value: unknown;
        try {
          value = JSON.parse(p.value_json);
        } catch {
          return {
            content: [
              {
                type: "text" as const,
                text: `Error: value_json is not valid JSON: '${p.value_json}'. Wrap strings in quotes, e.g. '"hello"'.`,
              },
            ],
          };
        }
        const data = await makeBridgeRequest<{ ok: boolean; property: string }>(
          "object/setProperty",
          { path: p.path, component_type: p.component_type, property: p.property, value }
        );
        return toolResult(`# Set ${p.component_type}.${p.property} on \`${p.path}\``, data);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );
}
