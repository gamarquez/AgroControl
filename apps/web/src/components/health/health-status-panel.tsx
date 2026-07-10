"use client";

import { useQuery } from "@tanstack/react-query";
import {
  AlertCircle,
  CheckCircle2,
  RefreshCcw,
  Server,
  WifiOff,
} from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useNetworkStatus } from "@/hooks/use-network-status";
import {
  ApiHealthError,
  fetchApiHealth,
  type ApiHealthResponse,
} from "@/lib/api/health";
import { getPublicApiConfig } from "@/lib/env";
import { cn } from "@/lib/utils";

const statusAppearance = {
  healthy: {
    label: "Disponible",
    className: "border-emerald-200 bg-emerald-50 text-emerald-700",
    description: "La API respondio correctamente.",
  },
  degraded: {
    label: "Degradado",
    className: "border-amber-200 bg-amber-50 text-amber-800",
    description: "La API responde, pero informa un estado degradado.",
  },
  unhealthy: {
    label: "Sin servicio",
    className: "border-rose-200 bg-rose-50 text-rose-700",
    description: "La API respondio con un estado no saludable.",
  },
  unknown: {
    label: "Estado no tipado",
    className: "border-slate-200 bg-slate-100 text-slate-700",
    description: "La API respondio, pero el estado no coincide con los valores esperados.",
  },
} as const;

function normalizeStatus(status: string) {
  const normalized = status.trim().toLowerCase();

  if (normalized === "healthy" || normalized === "ok") {
    return "healthy";
  }

  if (normalized === "degraded" || normalized === "warning") {
    return "degraded";
  }

  if (normalized === "unhealthy" || normalized === "failed" || normalized === "down") {
    return "unhealthy";
  }

  return "unknown";
}

function formatTimestamp(timestamp?: string) {
  if (!timestamp) {
    return "Sin timestamp informado";
  }

  const parsed = new Date(timestamp);

  if (Number.isNaN(parsed.getTime())) {
    return timestamp;
  }

  return new Intl.DateTimeFormat("es-AR", {
    dateStyle: "short",
    timeStyle: "medium",
  }).format(parsed);
}

function HealthSummary({ health }: { health: ApiHealthResponse }) {
  const normalizedStatus = normalizeStatus(health.status);
  const appearance = statusAppearance[normalizedStatus];

  return (
    <Card className="border-border/80 bg-card/90 shadow-lg shadow-primary/5">
      <CardHeader className="gap-4">
        <div className="flex items-start justify-between gap-3">
          <div className="space-y-1">
            <CardTitle className="flex items-center gap-2 text-xl">
              <CheckCircle2 className="h-5 w-5 text-primary" aria-hidden="true" />
              Salud de la API
            </CardTitle>
            <CardDescription className="leading-6">
              {appearance.description}
            </CardDescription>
          </div>
          <Badge className={cn("border", appearance.className)} variant="outline">
            {appearance.label}
          </Badge>
        </div>
      </CardHeader>

      <CardContent className="grid gap-4">
        <dl className="grid gap-3 text-sm sm:grid-cols-2">
          <div className="rounded-lg border border-border/70 bg-background/70 p-4">
            <dt className="text-xs uppercase tracking-[0.18em] text-muted-foreground">
              Servicio
            </dt>
            <dd className="mt-2 font-medium text-foreground">
              {health.service ?? "API sin nombre informado"}
            </dd>
          </div>

          <div className="rounded-lg border border-border/70 bg-background/70 p-4">
            <dt className="text-xs uppercase tracking-[0.18em] text-muted-foreground">
              Ultima lectura
            </dt>
            <dd className="mt-2 font-medium text-foreground">
              {formatTimestamp(health.timestamp)}
            </dd>
          </div>
        </dl>

        <div className="rounded-lg border border-dashed border-border bg-background/60 p-4">
          <p className="text-xs uppercase tracking-[0.18em] text-muted-foreground">
            Endpoint consultado
          </p>
          <code className="mt-2 block break-all font-mono text-sm text-foreground">
            {health.endpoint}
          </code>
        </div>

        {health.checks && Object.keys(health.checks).length > 0 ? (
          <div className="grid gap-3">
            <p className="text-xs uppercase tracking-[0.18em] text-muted-foreground">
              Checks informados por la API
            </p>
            <div className="grid gap-2 sm:grid-cols-2">
              {Object.entries(health.checks).map(([name, value]) => (
                <div
                  key={name}
                  className="flex items-center justify-between rounded-lg border border-border/70 bg-background/70 px-4 py-3 text-sm"
                >
                  <span className="font-medium text-foreground">{name}</span>
                  <span className="text-muted-foreground">{value}</span>
                </div>
              ))}
            </div>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

function LoadingState() {
  return (
    <Card className="border-border/80 bg-card/90 shadow-lg shadow-primary/5" aria-busy="true">
      <CardHeader className="space-y-3">
        <Skeleton className="h-6 w-36" />
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-5/6" />
      </CardHeader>
      <CardContent className="grid gap-3">
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-16 w-full" />
      </CardContent>
    </Card>
  );
}

function EmptyState() {
  return (
    <Card className="border-border/80 bg-card/90 shadow-lg shadow-primary/5">
      <CardHeader className="space-y-2">
        <CardTitle className="flex items-center gap-2 text-xl">
          <Server className="h-5 w-5 text-primary" aria-hidden="true" />
          Falta configurar la API
        </CardTitle>
        <CardDescription className="leading-6">
          Defini `NEXT_PUBLIC_API_BASE_URL` en `apps/web/.env.local` para habilitar la
          consulta del health check.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Alert>
          <AlertCircle className="h-4 w-4" aria-hidden="true" />
          <AlertTitle>Estado inicial</AlertTitle>
          <AlertDescription>
            El frontend quedo listo para conectarse a una API externa, pero todavia no
            tiene una URL publica configurada.
          </AlertDescription>
        </Alert>
      </CardContent>
    </Card>
  );
}

function OfflineState({ endpoint }: { endpoint: string }) {
  return (
    <Card className="border-border/80 bg-card/90 shadow-lg shadow-primary/5">
      <CardHeader className="space-y-2">
        <CardTitle className="flex items-center gap-2 text-xl">
          <WifiOff className="h-5 w-5 text-primary" aria-hidden="true" />
          Sin conexion
        </CardTitle>
        <CardDescription className="leading-6">
          El navegador esta offline. Cuando vuelva la conexion, la consulta podra
          retomarse sin recargar la pantalla.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Alert>
          <WifiOff className="h-4 w-4" aria-hidden="true" />
          <AlertTitle>Chequeo pendiente</AlertTitle>
          <AlertDescription>
            El endpoint configurado es <code className="font-mono">{endpoint}</code>.
          </AlertDescription>
        </Alert>
      </CardContent>
    </Card>
  );
}

function ErrorState({
  endpoint,
  error,
  onRetry,
}: {
  endpoint: string;
  error: Error;
  onRetry: () => void;
}) {
  const description =
    error instanceof ApiHealthError && error.statusCode
      ? `La API devolvio ${error.statusCode}: ${error.message}`
      : error.message;

  return (
    <Card className="border-border/80 bg-card/90 shadow-lg shadow-primary/5">
      <CardHeader className="space-y-2">
        <CardTitle className="flex items-center gap-2 text-xl">
          <AlertCircle className="h-5 w-5 text-destructive" aria-hidden="true" />
          No pudimos consultar la API
        </CardTitle>
        <CardDescription className="leading-6">
          La pantalla sigue operativa y permite reintentar la verificacion del health
          check.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" aria-hidden="true" />
          <AlertTitle>Fallo de conectividad</AlertTitle>
          <AlertDescription>{description}</AlertDescription>
        </Alert>

        <div className="rounded-lg border border-dashed border-border bg-background/60 p-4">
          <p className="text-xs uppercase tracking-[0.18em] text-muted-foreground">
            Endpoint consultado
          </p>
          <code className="mt-2 block break-all font-mono text-sm text-foreground">
            {endpoint}
          </code>
        </div>

        <Button type="button" onClick={onRetry}>
          <RefreshCcw className="h-4 w-4" aria-hidden="true" />
          Reintentar
        </Button>
      </CardContent>
    </Card>
  );
}

export function HealthStatusPanel() {
  const apiConfig = getPublicApiConfig();
  const isOnline = useNetworkStatus();

  const healthQuery = useQuery({
    queryKey: ["api-health", apiConfig?.apiHealthUrl],
    queryFn: ({ signal }) => fetchApiHealth(apiConfig!, signal),
    enabled: Boolean(apiConfig?.apiHealthUrl) && isOnline,
  });

  if (!apiConfig) {
    return <EmptyState />;
  }

  if (!isOnline) {
    return <OfflineState endpoint={apiConfig.apiHealthUrl} />;
  }

  if (healthQuery.isPending) {
    return <LoadingState />;
  }

  if (healthQuery.isError) {
    return (
      <ErrorState
        endpoint={apiConfig.apiHealthUrl}
        error={healthQuery.error}
        onRetry={() => {
          void healthQuery.refetch();
        }}
      />
    );
  }

  return <HealthSummary health={healthQuery.data} />;
}

