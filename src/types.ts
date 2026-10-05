/** TypeScript interfaces for Unity bridge data structures. */

export interface Vector3 {
  x: number;
  y: number;
  z: number;
}

export interface TransformInfo {
  position: Vector3;
  rotationEuler: Vector3;
  scale: Vector3;
}

export interface ComponentSummary {
  type: string;
  enabled?: boolean;
}

export interface HierarchyNode {
  name: string;
  path: string;
  active: boolean;
  tag: string;
  layer: number;
  childCount: number;
  components: string[];
  children?: HierarchyNode[];
}

export interface GameObjectInfo {
  name: string;
  path: string;
  active: boolean;
  tag: string;
  layer: number;
  transform: TransformInfo;
  components: ComponentSummary[];
}

export interface SceneInfo {
  name: string;
  path: string;
  isDirty: boolean;
  rootCount: number;
  buildIndex: number;
}

export interface ProjectInfo {
  projectName: string;
  projectPath: string;
  unityVersion: string;
  activeScene: SceneInfo;
  playMode: PlayModeState;
}

export type PlayModeState = "stopped" | "playing" | "paused";

export type LogType = "log" | "warning" | "error";

export interface ConsoleLogEntry {
  timestamp: string;
  type: LogType;
  message: string;
  stackTrace?: string;
}

export interface PaginatedResponse<T> {
  total: number;
  count: number;
  offset: number;
  items: T[];
  has_more: boolean;
  next_offset?: number;
}

export interface BridgeRpcRequest {
  jsonrpc: "2.0";
  id: number;
  method: string;
  params: Record<string, unknown>;
}

export interface BridgeRpcResponse<T = unknown> {
  jsonrpc: "2.0";
  id: number;
  result?: T;
  error?: {
    code: number;
    message: string;
    hint?: string;
  };
}
