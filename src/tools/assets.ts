/** Prefab + asset database tools. */
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { makeBridgeRequest } from "../services/bridgeClient.js";
import { handleBridgeError } from "../services/errors.js";
import {
  ResponseFormat,
  paginate,
  toolResult,
} from "../services/formatter.js";
import {
  ApplyPrefabInput,
  AssetDependenciesInput,
  FolderStructureInput,
  ImportAssetInput,
  InstantiatePrefabInput,
  ListAssetsInput,
  type ApplyPrefabInputT,
  type AssetDependenciesInputT,
  type FolderStructureInputT,
  type ImportAssetInputT,
  type InstantiatePrefabInputT,
  type ListAssetsInputT,
} from "../schemas/assets.js";

interface AssetEntry {
  path: string;
  type: string;
  size: number;
}

export function registerAssetTools(server: McpServer): void {
  server.registerTool(
    "unity_list_assets",
    {
      title: "List Project Assets",
      description: `Browse AssetDatabase: find assets by folder, name substring and type. Paginated.

Args: folder (default 'Assets'), filter (name substring, optional), type (Prefab/Material/Texture2D/AudioClip/Scene/..., optional), limit/offset.
Use when: locating prefabs, materials or textures before instantiating or assigning them.`,
      inputSchema: ListAssetsInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: ListAssetsInputT) => {
      try {
        const all = await makeBridgeRequest<AssetEntry[]>("asset/list", {
          folder: p.folder,
          filter: p.filter,
          type: p.type,
        });
        const page = paginate(all, p.limit, p.offset);
        const text =
          p.response_format === ResponseFormat.JSON
            ? JSON.stringify(page, null, 2)
            : [
                `# Assets in ${p.folder} (${page.total} total, showing ${page.count})`,
                ``,
                ...page.items.map((a) => `- \`${a.path}\` [${a.type}]`),
              ].join("\n");
        return toolResult(text, page);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_import_asset",
    {
      title: "Reimport Asset",
      description: `Force Unity to reimport an asset (picks up external file changes).

Args: path ('Assets/Textures/logo.png'). Idempotent — reimporting twice is harmless.`,
      inputSchema: ImportAssetInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: ImportAssetInputT) => {
      try {
        const data = await makeBridgeRequest<{ path: string; imported: boolean }>("asset/import", {
          path: p.path,
        });
        return toolResult(
          `# Reimported \`${data.path}\``,
          data as unknown as Record<string, unknown>
        );
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_asset_dependencies",
    {
      title: "Get Asset Dependencies",
      description: `List what an asset depends on (textures of a material, scripts of a prefab, ...). Recursive by default.

Args: path, recursive (default true).
Use when: checking what will break if you move/delete an asset, or why a prefab pulls in half the project.`,
      inputSchema: AssetDependenciesInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: AssetDependenciesInputT) => {
      try {
        const deps = await makeBridgeRequest<string[]>("asset/dependencies", {
          path: p.path,
          recursive: p.recursive,
        });
        const structured: Record<string, unknown> = { path: p.path, count: deps.length, dependencies: deps };
        const text =
          p.response_format === ResponseFormat.JSON
            ? JSON.stringify(structured, null, 2)
            : [
                `# Dependencies of \`${p.path}\` (${deps.length})`,
                ``,
                ...deps.map((d) => `- \`${d}\``),
              ].join("\n");
        return toolResult(text, structured);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_folder_structure",
    {
      title: "Get Folder Structure",
      description: `Map the project folder tree up to a depth (default 3). Read-only overview of how the project is organized.

Args: folder (default 'Assets'), depth 1-5 (default 3).
Large folders are truncated with a note — drill into subfolders for detail.`,
      inputSchema: FolderStructureInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: FolderStructureInputT) => {
      try {
        const tree = await makeBridgeRequest<unknown>("asset/folders", {
          folder: p.folder,
          depth: p.depth,
        });
        const structured: Record<string, unknown> = { folder: p.folder, tree };
        const text =
          p.response_format === ResponseFormat.JSON
            ? JSON.stringify(structured, null, 2)
            : `# ${p.folder}/\n\`\`\`\n${renderTree(tree, "", 0)}\n\`\`\``;
        return toolResult(text, structured);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_instantiate_prefab",
    {
      title: "Instantiate Prefab",
      description: `Instantiate a prefab from an asset path into the active scene (Undo-able). Keeps the prefab link so overrides can be applied later.

Args: prefab_path ('Assets/Prefabs/Enemy.prefab'), name override (optional), position/rotation, parent_path (optional).
Find prefabs first with unity_list_assets type='Prefab'.`,
      inputSchema: InstantiatePrefabInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false },
    },
    async (p: InstantiatePrefabInputT) => {
      try {
        const data = await makeBridgeRequest<{ path: string; name: string }>("prefab/instantiate", {
          prefab_path: p.prefab_path,
          name: p.name,
          position: p.position ?? [0, 0, 0],
          rotation: p.rotation ?? [0, 0, 0],
          parent_path: p.parent_path,
        });
        return toolResult(
          `# Instantiated \`${data.path}\``,
          data as unknown as Record<string, unknown>
        );
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_apply_prefab",
    {
      title: "Apply Prefab Overrides",
      description: `Apply an instance's overrides back to its prefab source (Undo-able, but affects all instances once saved).

Args: object_path of the instance ('Level/Enemy_1'). Fails with hint if the object is not a prefab instance.`,
      inputSchema: ApplyPrefabInput,
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: ApplyPrefabInputT) => {
      try {
        const data = await makeBridgeRequest<{ applied: boolean; path: string }>("prefab/apply", {
          object_path: p.object_path,
        });
        return toolResult(
          `# Applied overrides of \`${data.path}\` to prefab source`,
          data as unknown as Record<string, unknown>
        );
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );
}

function renderTree(node: unknown, prefix: string, _depth: number): string {
  if (typeof node === "string") return `${prefix}${node}\n`;
  if (node !== null && typeof node === "object") {
    const rec = node as { name?: string; children?: unknown[]; truncated?: boolean };
    let out = `${prefix}${rec.name ?? "?"}/\n`;
    for (const child of rec.children ?? []) out += renderTree(child, prefix + "  ", _depth + 1);
    if (rec.truncated) out += `${prefix}  …(truncated)\n`;
    return out;
  }
  return "";
}
