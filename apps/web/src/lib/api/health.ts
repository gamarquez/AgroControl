import { z } from "zod";

import type { PublicApiConfig } from "@/lib/env";

const healthPayloadSchema = z.object({
  status: z.string(),
  service: z.string().optional(),
  version: z.string().optional(),
  timestamp: z.string().optional(),
  checkedAt: z.string().optional(),
  message: z.string().optional(),
  checks: z.record(z.string(), z.string()).optional(),
});

export type ApiHealthResponse = z.infer<typeof healthPayloadSchema> & {
  endpoint: string;
};

export class ApiHealthError extends Error {
  constructor(
    message: string,
    public readonly statusCode?: number,
  ) {
    super(message);
    this.name = "ApiHealthError";
  }
}

function parseHealthPayload(body: string, endpoint: string): ApiHealthResponse {
  if (!body.trim()) {
    return {
      endpoint,
      status: "unknown",
      message: "La API respondi\xF3 sin contenido.",
    };
  }

  try {
    const payload = healthPayloadSchema.parse(JSON.parse(body));
    return {
      endpoint,
      ...payload,
      timestamp: payload.timestamp ?? payload.checkedAt,
    };
  } catch {
    return {
      endpoint,
      status: body.trim(),
      message: body.trim(),
    };
  }
}

export async function fetchApiHealth(
  config: PublicApiConfig,
  signal?: AbortSignal,
) {
  const response = await fetch(config.apiHealthUrl, {
    cache: "no-store",
    headers: {
      Accept: "application/json, text/plain;q=0.9, */*;q=0.1",
    },
    signal,
  });

  const body = await response.text();

  if (!response.ok) {
    throw new ApiHealthError(
      body.trim() || "La API devolvi\xF3 un error inesperado.",
      response.status,
    );
  }

  return parseHealthPayload(body, config.apiHealthUrl);
}
