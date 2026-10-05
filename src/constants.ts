/** Shared constants for unity-mcp-server. */
import os from "node:os";
import fs from "node:fs";
import path from "node:path";

export const BRIDGE_HOST: string =
  process.env.UNITY_BRIDGE_HOST ?? "127.0.0.1";

export const BRIDGE_PORT: number = parseInt(
  process.env.UNITY_BRIDGE_PORT ?? "6400",
  10
);

const PORT_FILE = path.join(os.tmpdir(), "unity-mcp-bridge.port");

/**
 * Bridge port discovery: explicit UNITY_BRIDGE_PORT wins (multi-instance),
 * else the port file written by the Editor bridge (auto-picked free port),
 * else the 6400 default.
 */
function discoverPort(): number {
  if (process.env.UNITY_BRIDGE_PORT) return BRIDGE_PORT;
  try {
    const raw = fs.readFileSync(PORT_FILE, "utf8").trim();
    const parsed = raw.startsWith("{")
      ? (JSON.parse(raw) as { port?: unknown }).port
      : parseInt(raw, 10);
    if (typeof parsed === "number" && Number.isInteger(parsed) && parsed > 0) {
      return parsed;
    }
  } catch {
    /* no port file yet — fall through */
  }
  return 6400;
}

export function bridgeUrl(): string {
  return `http://${BRIDGE_HOST}:${discoverPort()}/rpc`;
}

export const BRIDGE_URL: string = `http://${BRIDGE_HOST}:${BRIDGE_PORT}/rpc`;

export const BRIDGE_TIMEOUT_MS = 30_000;

/** Maximum response size in characters before truncation kicks in. */
export const CHARACTER_LIMIT = 25_000;

/** Default page size for list operations. */
export const DEFAULT_LIMIT = 20;
export const MAX_LIMIT = 100;

export const SERVER_NAME = "unity-mcp-server";
export const SERVER_VERSION = "0.1.0";
