import { z } from "zod";
import {
  limitField,
  offsetField,
  responseFormatField,
} from "./common.js";

export const scriptPathField = z
  .string()
  .min(1, "Script path is required")
  .max(500)
  .describe("Project-relative script path, e.g. 'Assets/Scripts/PlayerController.cs'");

export const ListScriptsInput = z
  .object({
    folder: z
      .string()
      .max(300)
      .default("Assets")
      .describe("Project-relative folder to search, e.g. 'Assets/Scripts'"),
    filter: z
      .string()
      .max(200)
      .optional()
      .describe("Substring filter on file name, e.g. 'Player'"),
    limit: limitField,
    offset: offsetField,
    response_format: responseFormatField,
  })
  .strict();

export const ReadScriptInput = z
  .object({
    path: scriptPathField,
    start_line: z
      .number()
      .int()
      .min(1)
      .default(1)
      .describe("First line to return (1-based)"),
    max_lines: z
      .number()
      .int()
      .min(1)
      .max(2000)
      .default(200)
      .describe("Maximum lines to return"),
    response_format: responseFormatField,
  })
  .strict();

export const CreateScriptInput = z
  .object({
    path: scriptPathField,
    template: z
      .enum(["monobehaviour", "scriptableobject", "editor", "empty"])
      .default("monobehaviour")
      .describe("Unity 6 template to generate from"),
    class_name: z
      .string()
      .max(200)
      .optional()
      .describe("Class name (defaults to file name without extension; must match it)"),
    overwrite: z
      .boolean()
      .default(false)
      .describe("Overwrite if the file already exists (default false)"),
  })
  .strict();

export const UpdateScriptInput = z
  .object({
    path: scriptPathField,
    old_text: z
      .string()
      .min(1, "old_text is required — exact block from the file")
      .max(20000)
      .describe("Exact text block to replace (copy from unity_read_script)"),
    new_text: z
      .string()
      .max(20000)
      .describe("Replacement text (can be empty string to delete)"),
    expected_occurrences: z
      .number()
      .int()
      .min(1)
      .max(100)
      .default(1)
      .describe("How many occurrences of old_text must exist (default 1; fails otherwise)"),
  })
  .strict();

export const AttachScriptInput = z
  .object({
    object_path: z
      .string()
      .min(1)
      .max(500)
      .describe("Hierarchy path of the GameObject, e.g. 'Player'"),
    script_class: z
      .string()
      .min(1)
      .max(200)
      .describe("MonoBehaviour class name to attach, e.g. 'PlayerController'"),
  })
  .strict();

export type ListScriptsInputT = z.infer<typeof ListScriptsInput>;
export type ReadScriptInputT = z.infer<typeof ReadScriptInput>;
export type CreateScriptInputT = z.infer<typeof CreateScriptInput>;
export type UpdateScriptInputT = z.infer<typeof UpdateScriptInput>;
export type AttachScriptInputT = z.infer<typeof AttachScriptInput>;
