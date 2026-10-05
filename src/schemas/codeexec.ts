import { z } from "zod";

export const ExecuteCodeInput = z
  .object({
    code: z
      .string()
      .min(1, "code is required")
      .max(20000)
      .describe(
        "Bare C# statements executed inside a static Run() (UnityEngine/UnityEditor/System/LINQ usings pre-included). " +
          "Use 'return <value>;' to send a result back. Example: 'var go = GameObject.Find(\"Player\"); return go.transform.position.ToString();'"
      ),
    timeout_s: z
      .number()
      .int()
      .min(10)
      .max(300)
      .default(60)
      .describe("Job timeout in seconds (10-300, default 60)"),
  })
  .strict();

export const CodeResultInput = z
  .object({
    job_id: z
      .string()
      .min(1)
      .max(64)
      .describe("Job id returned by unity_execute_code"),
  })
  .strict();

export type ExecuteCodeInputT = z.infer<typeof ExecuteCodeInput>;
export type CodeResultInputT = z.infer<typeof CodeResultInput>;
