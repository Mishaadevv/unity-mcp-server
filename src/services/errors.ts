/** Centralized error handling with actionable messages. */

import { BRIDGE_HOST, BRIDGE_PORT } from "../constants.js";

export function handleBridgeError(error: unknown): string {
  const bridgeHint = `Unity bridge at ${BRIDGE_HOST}:${BRIDGE_PORT} is unreachable. ` +
    `Make sure: 1) Unity 6 Editor is open, 2) the Unity MCP Bridge package is installed, ` +
    `3) Window > Unity MCP shows Running, 4) UNITY_BRIDGE_PORT matches the Editor port.`;

  if (error instanceof Error) {
    const msg = error.message;

    // fetch network errors
    if (
      msg.includes("ECONNREFUSED") ||
      msg.includes("fetch failed") ||
      msg.includes("ETIMEDOUT")
    ) {
      return `Error: Cannot reach Unity Editor. ${bridgeHint}`;
    }

    if (msg.includes("BRIDGE_ERROR:")) {
      return `Error: ${msg.replace("BRIDGE_ERROR:", "").trim()}`;
    }

    if (msg.includes("timed out") || msg.includes("aborted")) {
      return (
        "Error: Request to Unity timed out after 30s. " +
        "The Editor may be compiling scripts or in a modal dialog. " +
        "Wait for compilation to finish and try again."
      );
    }

    return `Error: Unexpected error: ${msg}. If Unity was compiling, wait and retry.`;
  }

  return `Error: Unexpected error of type ${typeof error}. ${bridgeHint}`;
}

/** Error thrown when the C# bridge returns a JSON-RPC error payload. */
export class BridgeRpcError extends Error {
  code: number;
  hint?: string;

  constructor(code: number, message: string, hint?: string) {
    super(`BRIDGE_ERROR: ${message}${hint ? ` Hint: ${hint}` : ""}`);
    this.name = "BridgeRpcError";
    this.code = code;
    this.hint = hint;
  }
}
