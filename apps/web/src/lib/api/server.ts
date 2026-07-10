import { z } from "zod";

import { getServerApiBaseUrl } from "@/lib/api/config";

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

type RequestOptions = {
  method?: "GET" | "POST" | "PATCH" | "PUT";
  body?: unknown;
  accessToken?: string;
  cache?: RequestCache;
};

const problemSchema = z.object({
  title: z.string().optional(),
  detail: z.string().optional(),
});

export async function apiRequest<TSchema extends z.ZodTypeAny>(
  path: string,
  schema: TSchema,
  options: RequestOptions = {},
): Promise<z.infer<TSchema>> {
  const response = await fetch(new URL(path.replace(/^\//, ""), getServerApiBaseUrl()), {
    method: options.method ?? "GET",
    headers: {
      Accept: "application/json",
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...(options.accessToken ? { Authorization: `Bearer ${options.accessToken}` } : {}),
    },
    body: options.body ? JSON.stringify(options.body) : undefined,
    cache: options.cache ?? "no-store",
  });

  if (!response.ok) {
    throw await toApiError(response);
  }

  const payload = await response.json();
  const parsed = schema.safeParse(payload);

  if (!parsed.success) {
    throw new ApiError("La API devolvio un contrato invalido.", 502);
  }

  return parsed.data;
}

export async function apiVoidRequest(
  path: string,
  options: RequestOptions = {},
): Promise<void> {
  const response = await fetch(new URL(path.replace(/^\//, ""), getServerApiBaseUrl()), {
    method: options.method ?? "POST",
    headers: {
      Accept: "application/json",
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...(options.accessToken ? { Authorization: `Bearer ${options.accessToken}` } : {}),
    },
    body: options.body ? JSON.stringify(options.body) : undefined,
    cache: options.cache ?? "no-store",
  });

  if (!response.ok) {
    throw await toApiError(response);
  }
}

async function toApiError(response: Response): Promise<ApiError> {
  try {
    const payload = await response.json();
    const parsed = problemSchema.safeParse(payload);

    if (parsed.success) {
      return new ApiError(
        parsed.data.detail || parsed.data.title || "La API devolvio un error.",
        response.status,
      );
    }
  } catch {
    // Ignore JSON parsing errors and fall back to plain text.
  }

  const text = await response.text().catch(() => "");
  return new ApiError(text || "La API devolvio un error.", response.status);
}
