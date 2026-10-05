/** Sandboxed async C# execution: submit returns job_id, poll for the result. */
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { makeBridgeRequest } from "../services/bridgeClient.js";
import { handleBridgeError } from "../services/errors.js";
import { toolResult } from "../services/formatter.js";
import {
  CodeResultInput,
  ExecuteCodeInput,
  type CodeResultInputT,
  type ExecuteCodeInputT,
} from "../schemas/codeexec.js";

export function registerCodeExecTools(server: McpServer): void {
  server.registerTool(
    "unity_execute_code",
    {
      title: "Execute C# in Editor (sandboxed)",
      description: `Compile and run bare C# statements inside the Unity Editor via Roslyn/AssemblyBuilder. Returns a job_id immediately — poll unity_get_code_result for completion (compile takes ~2-10s).

Code rules: bare statements only (wrapped into static Run(); UnityEngine/UnityEditor/System/LINQ usings included); 'return <value>;' sends JSON back. Keep snippets short and side-effect aware (runs on main thread; infinite loops hang the Editor).
Sandbox blocks: filesystem (System.IO/File./Directory.), processes, network, reflection-load (Assembly.Load/Activator/DllImport), Application.Quit, while(true)/for(;;).
Requires: mutations ON + 'Enable C# execution' in Window > Unity MCP (off by default).`,
      inputSchema: ExecuteCodeInput,
      annotations: { readOnlyHint: false, destructiveHint: true, idempotentHint: false, openWorldHint: false },
    },
    async (p: ExecuteCodeInputT) => {
      try {
        const data = await makeBridgeRequest<{ job_id: string; state: string }>("code/submit", {
          code: p.code,
          timeout_s: p.timeout_s,
        });
        return toolResult(
          `# Job ${data.job_id} submitted (state: ${data.state})\nPoll unity_get_code_result in a few seconds.`,
          data as unknown as Record<string, unknown>
        );
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );

  server.registerTool(
    "unity_get_code_result",
    {
      title: "Get C# Job Result",
      description: `Poll a code-execution job: running | done {output, logs} | error {error, logs}.

Use when: after unity_execute_code returned a job_id. If still running, wait and poll again. Output is the JSON of your 'return' value; logs are Debug.Log lines captured during Run().`,
      inputSchema: CodeResultInput,
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    },
    async (p: CodeResultInputT) => {
      try {
        const data = await makeBridgeRequest<Record<string, unknown>>("code/result", {
          job_id: p.job_id,
        });
        const state = String(data["state"] ?? "?");
        const detail =
          state === "done"
            ? `output: ${JSON.stringify(data["output"])}`
            : state === "error"
              ? `error: ${String(data["error"] ?? "").slice(0, 500)}`
              : "still running — wait and poll again";
        return toolResult(`# Job ${p.job_id}: ${state}\n${detail}`, data);
      } catch (e) {
        return { content: [{ type: "text" as const, text: handleBridgeError(e) }] };
      }
    }
  );
}
