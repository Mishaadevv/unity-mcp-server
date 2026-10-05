import { z } from "zod";
import { MAX_LIMIT } from "../constants.js";
import { ResponseFormat } from "../services/formatter.js";

export const responseFormatField = z
  .nativeEnum(ResponseFormat)
  .default(ResponseFormat.MARKDOWN)
  .describe("Output format: 'markdown' for human-readable or 'json' for machine-readable");

export const limitField = z
  .number()
  .int()
  .min(1)
  .max(MAX_LIMIT)
  .default(20)
  .describe("Maximum results to return (1-100)");

export const offsetField = z
  .number()
  .int()
  .min(0)
  .default(0)
  .describe("Number of results to skip for pagination");

export const objectPathField = z
  .string()
  .min(1, "Object path is required, e.g. 'Player' or 'Level/Enemies/Orc'")
  .max(500)
  .describe("Hierarchy path of the GameObject, e.g. 'Player' or 'Level/Enemies/Orc'");

export const vector3Field = (label: string): z.ZodOptional<z.ZodTuple<[z.ZodNumber, z.ZodNumber, z.ZodNumber], null>> =>
  z
    .tuple([z.number(), z.number(), z.number()])
    .describe(`${label} as [x, y, z] in metres/degrees`)
    .optional();
