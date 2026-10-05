import { z } from "zod";
import { responseFormatField } from "./common.js";

export const ConsoleLogsInput = z
  .object({
    max_logs: z
      .number()
      .int()
      .min(1)
      .max(100)
      .default(30)
      .describe("Maximum log entries to return (1-100, most recent first)"),
    log_types: z
      .array(z.enum(["log", "warning", "error"]))
      .default(["warning", "error"])
      .describe("Which Unity log types to include"),
    response_format: responseFormatField,
  })
  .strict();

export const PlayModeGetInput = z.object({}).strict();

export const PlayModeSetInput = z
  .object({
    action: z
      .enum(["play", "stop", "pause", "unpause"])
      .describe("Play-mode transition to perform"),
  })
  .strict();

export type ConsoleLogsInputT = z.infer<typeof ConsoleLogsInput>;
export type PlayModeSetInputT = z.infer<typeof PlayModeSetInput>;
