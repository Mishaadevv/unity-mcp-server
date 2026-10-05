import { z } from "zod";

export const SetInput = z
  .object({
    throttle: z
      .number()
      .min(-1)
      .max(1)
      .default(0)
      .describe("Hull throttle: +1 full forward, -1 full reverse"),
    steer: z
      .number()
      .min(-1)
      .max(1)
      .default(0)
      .describe("Hull steer: +1 right, -1 left"),
    fire: z
      .boolean()
      .default(false)
      .describe("Queue one shot (edge-triggered, consumed by next frame)"),
  })
  .strict();

export const EmptyInput = z.object({}).strict();

export type SetInputT = z.infer<typeof SetInput>;
