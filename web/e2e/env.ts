import { z } from "zod";

const schema = z.object({
  REEL_SERVER_URL: z.string().url(),
  REEL_SECRET_KEY: z.string().min(1),
  E2E_HEADLESS: z.string().default("true"),
  E2E_SLOWMO: z.string().default("0"),
});

export const env = schema.parse(process.env);
