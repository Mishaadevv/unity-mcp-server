import { z } from "zod";
import {
  limitField,
  objectPathField,
  offsetField,
  responseFormatField,
  vector3Field,
} from "./common.js";

export const PingInput = z.object({}).strict();

export const HierarchyInput = z
  .object({
    root_only: z
      .boolean()
      .default(true)
      .describe("If true return only root objects; if false include full tree"),
    limit: limitField,
    offset: offsetField,
    response_format: responseFormatField,
  })
  .strict();

export const FindObjectsInput = z
  .object({
    name_contains: z
      .string()
      .max(200)
      .optional()
      .describe("Substring to match against GameObject names (case-insensitive)"),
    tag: z.string().max(100).optional().describe("Filter by Unity tag, e.g. 'Player'"),
    with_component: z
      .string()
      .max(200)
      .optional()
      .describe("Filter by component type name, e.g. 'Rigidbody' or 'BoxCollider'"),
    limit: limitField,
    offset: offsetField,
    response_format: responseFormatField,
  })
  .strict()
  .refine(
    (v) => v.name_contains !== undefined || v.tag !== undefined || v.with_component !== undefined,
    "Provide at least one of name_contains, tag or with_component"
  );

export const ObjectInfoInput = z
  .object({
    path: objectPathField,
    response_format: responseFormatField,
  })
  .strict();

export const ComponentsInput = z
  .object({
    path: objectPathField,
    response_format: responseFormatField,
  })
  .strict();

export const CreateObjectInput = z
  .object({
    name: z
      .string()
      .min(1, "Name is required")
      .max(200)
      .describe("Name for the new GameObject, e.g. 'PlayerSpawn'"),
    primitive_type: z
      .enum(["none", "cube", "sphere", "capsule", "cylinder", "plane", "quad"])
      .default("none")
      .describe("Empty object ('none') or a Unity primitive to create"),
    position: vector3Field("World position").describe("World position as [x, y, z] (default 0,0,0)"),
    rotation: vector3Field("World rotation").describe(
      "World rotation euler degrees as [x, y, z] (default 0,0,0)"
    ),
    scale: vector3Field("Local scale").describe("Local scale as [x, y, z] (default 1,1,1)"),
    parent_path: z
      .string()
      .max(500)
      .optional()
      .describe("Optional hierarchy path of the parent, e.g. 'Level/Spawns'"),
  })
  .strict();

export const DeleteObjectInput = z
  .object({
    path: objectPathField,
  })
  .strict();

export const SetTransformInput = z
  .object({
    path: objectPathField,
    position: vector3Field("World position"),
    rotation: vector3Field("World rotation euler degrees"),
    scale: vector3Field("Local scale"),
  })
  .strict()
  .refine(
    (v) => v.position !== undefined || v.rotation !== undefined || v.scale !== undefined,
    "Provide at least one of position, rotation or scale"
  );

export const AddComponentInput = z
  .object({
    path: objectPathField,
    component_type: z
      .string()
      .min(1)
      .max(200)
      .describe("Component type name, e.g. 'Rigidbody', 'BoxCollider', 'Light'"),
  })
  .strict();

export const SetPropertyInput = z
  .object({
    path: objectPathField,
    component_type: z
      .string()
      .min(1)
      .max(200)
      .describe("Component type name, e.g. 'Transform' or 'Rigidbody'"),
    property: z
      .string()
      .min(1)
      .max(200)
      .describe("Property/field name, e.g. 'mass', 'isKinematic', 'intensity'"),
    value_json: z
      .string()
      .min(1)
      .max(5000)
      .describe("New value as JSON, e.g. '5.0', 'true', '\"hello\"', '[0,1,0]'"),
  })
  .strict();

export type PingInputT = z.infer<typeof PingInput>;
export type HierarchyInputT = z.infer<typeof HierarchyInput>;
export type FindObjectsInputT = z.infer<typeof FindObjectsInput>;
export type ObjectInfoInputT = z.infer<typeof ObjectInfoInput>;
export type ComponentsInputT = z.infer<typeof ComponentsInput>;
export type CreateObjectInputT = z.infer<typeof CreateObjectInput>;
export type DeleteObjectInputT = z.infer<typeof DeleteObjectInput>;
export type SetTransformInputT = z.infer<typeof SetTransformInput>;
export type AddComponentInputT = z.infer<typeof AddComponentInput>;
export type SetPropertyInputT = z.infer<typeof SetPropertyInput>;
