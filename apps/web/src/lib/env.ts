import { z } from "zod";

const publicEnvSchema = z.object({
  NEXT_PUBLIC_API_BASE_URL: z
    .string()
    .trim()
    .url()
    .optional()
    .or(z.literal("")),
  NEXT_PUBLIC_API_HEALTH_PATH: z.string().trim().min(1).optional(),
});

export type PublicApiConfig = {
  apiBaseUrl: string;
  apiHealthPath: string;
  apiHealthUrl: string;
};

export function getPublicApiConfig(): PublicApiConfig | null {
  const parsed = publicEnvSchema.safeParse({
    NEXT_PUBLIC_API_BASE_URL: process.env.NEXT_PUBLIC_API_BASE_URL,
    NEXT_PUBLIC_API_HEALTH_PATH: process.env.NEXT_PUBLIC_API_HEALTH_PATH,
  });

  if (!parsed.success) {
    return null;
  }

  const apiBaseUrl = parsed.data.NEXT_PUBLIC_API_BASE_URL?.trim();

  if (!apiBaseUrl) {
    return null;
  }

  const apiHealthPath = parsed.data.NEXT_PUBLIC_API_HEALTH_PATH ?? "/health";
  const apiHealthUrl = new URL(
    apiHealthPath.replace(/^\//, ""),
    apiBaseUrl.endsWith("/") ? apiBaseUrl : `${apiBaseUrl}/`,
  ).toString();

  return {
    apiBaseUrl,
    apiHealthPath,
    apiHealthUrl,
  };
}

