/**
 * Shared markdown / JSON formatting + truncation + pagination helpers.
 * All tools use these so output shape stays consistent.
 */
import { CHARACTER_LIMIT } from "../constants.js";

export enum ResponseFormat {
  MARKDOWN = "markdown",
  JSON = "json",
}

export interface PaginationArgs {
  limit: number;
  offset: number;
}

export function paginate<T>(all: T[], limit: number, offset: number): {
  total: number;
  count: number;
  offset: number;
  items: T[];
  has_more: boolean;
  next_offset?: number;
} {
  const total = all.length;
  const items = all.slice(offset, offset + limit);
  const has_more = offset + items.length < total;
  return {
    total,
    count: items.length,
    offset,
    items,
    has_more,
    ...(has_more ? { next_offset: offset + items.length } : {}),
  };
}

/** Truncate an already-rendered text payload with a clear message. */
export function enforceCharacterLimit(
  text: string,
  toolHint: string
): { text: string; truncated: boolean } {
  if (text.length <= CHARACTER_LIMIT) {
    return { text, truncated: false };
  }
  const note =
    `\n\n…[truncated ${text.length - CHARACTER_LIMIT} chars — ` +
    `use limit/offset or narrower filters. ${toolHint}]`;
  return {
    text: text.slice(0, CHARACTER_LIMIT) + note,
    truncated: true,
  };
}

/** Build the standard MCP tool result from markdown text + structured object. */
export function toolResult(
  text: string,
  structured: unknown
): {
  content: Array<{ type: "text"; text: string }>;
  structuredContent: Record<string, unknown>;
} {
  const { text: safe } = enforceCharacterLimit(
    text,
    "Reduce scope with filters or pagination."
  );
  return {
    content: [{ type: "text" as const, text: safe }],
    structuredContent: (structured ?? {}) as Record<string, unknown>,
  };
}

/** Build an error tool result (isError) with actionable text. */
export function toolError(text: string): {
  content: Array<{ type: "text"; text: string }>;
  isError: true;
} {
  return {
    content: [{ type: "text" as const, text }],
    isError: true,
  };
}
