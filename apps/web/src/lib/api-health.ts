import { tryGetServerApiBaseUrl } from "@/lib/api/config";

export type ApiHealthStatus = "healthy" | "unhealthy";

export type ApiHealthResponse = {
  status: ApiHealthStatus;
  service: string;
  version: string;
  environment: string;
  checkedAt: string;
  message?: string;
};

export function parseApiHealthResponse(value: unknown): ApiHealthResponse | null {
  if (typeof value !== "object" || value === null) {
    return null;
  }

  const payload = value as Record<string, unknown>;

  if (payload.status !== "healthy" && payload.status !== "unhealthy") {
    return null;
  }

  if (
    typeof payload.service !== "string" ||
    typeof payload.version !== "string" ||
    typeof payload.environment !== "string" ||
    typeof payload.checkedAt !== "string"
  ) {
    return null;
  }

  return {
    status: payload.status,
    service: payload.service,
    version: payload.version,
    environment: payload.environment,
    checkedAt: payload.checkedAt,
  };
}

function createUnavailableHealth(message: string): ApiHealthResponse {
  return {
    status: "unhealthy",
    service: "AgroControl.Api",
    version: "unknown",
    environment: "unreachable",
    checkedAt: new Date().toISOString(),
    message,
  };
}

export async function getApiHealth(fetchImpl: typeof fetch = fetch): Promise<ApiHealthResponse> {
  const apiBaseUrl = tryGetServerApiBaseUrl();

  if (!apiBaseUrl) {
    return createUnavailableHealth(
      "Falta configurar AGROCONTROL_API_BASE_URL para el health check del servidor web.",
    );
  }

  try {
    const response = await fetchImpl(new URL("health", apiBaseUrl), {
      cache: "no-store",
      headers: {
        accept: "application/json",
      },
    });

    if (!response.ok) {
      return createUnavailableHealth(`La API respondió con estado ${response.status}.`);
    }

    const payload = parseApiHealthResponse(await response.json());

    if (!payload) {
      return createUnavailableHealth("La respuesta del health check no cumple el contrato esperado.");
    }

    return payload;
  } catch (error) {
    return createUnavailableHealth(
      error instanceof Error ? error.message : "No se pudo consultar el health check de la API.",
    );
  }
}
