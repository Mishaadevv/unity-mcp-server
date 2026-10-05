/**
 * Single HTTP client for the Unity Editor C# bridge.
 * All tools must go through this — never duplicate fetch logic.
 */
import { BRIDGE_TIMEOUT_MS, bridgeUrl } from "../constants.js";
import type { BridgeRpcResponse } from "../types.js";
import { BridgeRpcError } from "./errors.js";

let rpcId = 1;

/** Methods supported by the C# Editor bridge (see UnityPackage/Editor). */
export type BridgeMethod =
  | "ping"
  | "project/info"
  | "scene/info"
  | "scene/hierarchy"
  | "scene/save"
  | "object/find"
  | "object/info"
  | "object/components"
  | "object/create"
  | "object/delete"
  | "object/setTransform"
  | "object/addComponent"
  | "object/setProperty"
  | "console/logs"
  | "console/clear"
  | "playmode/get"
  | "playmode/set"
  | "script/list"
  | "script/read"
  | "script/create"
  | "script/update"
  | "script/attach"
  | "asset/list"
  | "asset/import"
  | "asset/dependencies"
  | "asset/folders"
  | "prefab/instantiate"
  | "prefab/apply"
  | "code/submit"
  | "code/result"
  | "asset/refresh"
  | "input/set"
  | "input/clear"
  | "game/state";

export async function makeBridgeRequest<T>(
  method: BridgeMethod,
  params: Record<string, unknown> = {}
): Promise<T> {
  const id = rpcId++;
  const body = JSON.stringify({
    jsonrpc: "2.0",
    id,
    method,
    params,
  });

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), BRIDGE_TIMEOUT_MS);

  let res: Response;
  try {
    res = await fetch(bridgeUrl(), {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body,
      signal: controller.signal,
    });
  } finally {
    clearTimeout(timeout);
  }

  if (!res.ok) {
    throw new Error(
      `BRIDGE_ERROR: Unity bridge HTTP ${res.status}. ` +
        `Is the Editor bridge running? Check Window > Unity MCP.`
    );
  }

  const payload = (await res.json()) as BridgeRpcResponse<T>;

  if (payload.error) {
    throw new BridgeRpcError(
      payload.error.code,
      payload.error.message,
      payload.error.hint
    );
  }

  return payload.result as T;
}
