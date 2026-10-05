/** Shared constants for unity-mcp-server. */

export const BRIDGE_HOST: string =
  process.env.UNITY_BRIDGE_HOST ?? "127.0.0.1";

export const BRIDGE_PORT: number = parseInt(
  process.env.UNITY_BRIDGE_PORT ?? "6400",
  10
);

export const BRIDGE_URL: string = `http://${BRIDGE_HOST}:${BRIDGE_PORT}/rpc`;

export const BRIDGE_TIMEOUT_MS = 30_000;

/** Maximum response size in characters before truncation kicks in. */
export const CHARACTER_LIMIT = 25_000;

/** Default page size for list operations. */
export const DEFAULT_LIMIT = 20;
export const MAX_LIMIT = 100;

export const SERVER_NAME = "unity-mcp-server";
export const SERVER_VERSION = "0.1.0";
