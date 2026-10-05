#!/usr/bin/env node
/**
 * unity-mcp-server — MCP bridge to Unity 6 Editor.
 * Transport: stdio (local single-user). Bridge: HTTP 127.0.0.1:6400 to C# Editor plugin.
 */
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { SERVER_NAME, SERVER_VERSION } from "./constants.js";
import { registerSceneTools } from "./tools/scene.js";
import { registerGameObjectTools } from "./tools/gameobject.js";
import { registerEditorTools } from "./tools/editor.js";
import { registerScriptTools } from "./tools/scripts.js";
import { registerAssetTools } from "./tools/assets.js";
import { registerCodeExecTools } from "./tools/codeexec.js";
import { registerGameplayTools } from "./tools/gameplay.js";

const server = new McpServer({ name: SERVER_NAME, version: SERVER_VERSION });

registerSceneTools(server);
registerGameObjectTools(server);
registerEditorTools(server);
registerScriptTools(server);
registerAssetTools(server);
registerCodeExecTools(server);
registerGameplayTools(server);

async function main(): Promise<void> {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error(`${SERVER_NAME} v${SERVER_VERSION} running via stdio`);
}

main().catch((error) => {
  console.error("Server fatal:", error);
  process.exit(1);
});
