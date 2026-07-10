function normalizeApiBaseUrl(baseUrl: string): string {
  return baseUrl.endsWith("/") ? baseUrl : `${baseUrl}/`;
}

export function tryGetServerApiBaseUrl(): string | null {
  const baseUrl = process.env.AGROCONTROL_API_BASE_URL?.trim();

  if (!baseUrl) {
    return null;
  }

  return normalizeApiBaseUrl(baseUrl);
}

export function getServerApiBaseUrl(): string {
  const baseUrl = tryGetServerApiBaseUrl();

  if (!baseUrl) {
    throw new Error(
      "Falta configurar AGROCONTROL_API_BASE_URL para el frontend servidor. Definila en los entornos Preview y Production de Vercel.",
    );
  }

  return baseUrl;
}
