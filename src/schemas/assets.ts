import { z } from "zod";
import {
  limitField,
  offsetField,
  responseFormatField,
  vector3Field,
} from "./common.js";

export const assetPathField = z
  .string()
  .min(1, "Asset path is required")
  .max(500)
  .describe("Project-relative asset path, e.g. 'Assets/Prefabs/Enemy.prefab'");

export const ListAssetsInput = z
  .object({
    folder: z
      .string()
      .max(300)
      .default("Assets")
      .describe("Project-relative folder to search, e.g. 'Assets/Prefabs'"),
    filter: z
      .string()
      .max(200)
      .optional()
      .describe("Name substring filter, e.g. 'Enemy'"),
    type: z
      .string()
      .max(100)
      .optional()
      .describe("Asset type filter, e.g. 'Prefab', 'Material', 'Texture2D', 'AudioClip', 'Scene'"),
    limit: limitField,
    offset: offsetField,
    response_format: responseFormatField,
  })
  .strict();

export const ImportAssetInput = z
  .object({
    path: assetPathField,
  })
  .strict();

export const AssetDependenciesInput = z
  .object({
    path: assetPathField,
    recursive: z
      .boolean()
      .default(true)
      .describe("Include transitive dependencies (default true)"),
    response_format: responseFormatField,
  })
  .strict();

export const FolderStructureInput = z
  .object({
    folder: z
      .string()
      .max(300)
      .default("Assets")
      .describe("Project-relative folder to map, e.g. 'Assets'"),
    depth: z
      .number()
      .int()
      .min(1)
      .max(5)
      .default(3)
      .describe("How deep to recurse (1-5, default 3)"),
    response_format: responseFormatField,
  })
  .strict();

export const InstantiatePrefabInput = z
  .object({
    prefab_path: assetPathField,
    name: z
      .string()
      .max(200)
      .optional()
      .describe("Override instance name (default keeps prefab name)"),
    position: vector3Field("World position").describe("World position as [x, y, z] (default 0,0,0)"),
    rotation: vector3Field("World rotation").describe(
      "World rotation euler degrees as [x, y, z] (default 0,0,0)"
    ),
    parent_path: z
      .string()
      .max(500)
      .optional()
      .describe("Optional hierarchy path of the parent"),
  })
  .strict();

export const ApplyPrefabInput = z
  .object({
    object_path: z
      .string()
      .min(1)
      .max(500)
      .describe("Hierarchy path of the prefab instance, e.g. 'Level/Enemy_1'"),
  })
  .strict();

export type ListAssetsInputT = z.infer<typeof ListAssetsInput>;
export type ImportAssetInputT = z.infer<typeof ImportAssetInput>;
export type AssetDependenciesInputT = z.infer<typeof AssetDependenciesInput>;
export type FolderStructureInputT = z.infer<typeof FolderStructureInput>;
export type InstantiatePrefabInputT = z.infer<typeof InstantiatePrefabInput>;
export type ApplyPrefabInputT = z.infer<typeof ApplyPrefabInput>;
