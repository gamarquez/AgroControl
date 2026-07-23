"use client";

import { useQuery } from "@tanstack/react-query";
import { CheckCircle2, CircleAlert, LoaderCircle, RefreshCcw, WifiOff } from "lucide-react";

import { Button } from "@/components/ui/button";
import { useNetworkStatus } from "@/hooks/use-network-status";
import { fetchApiHealth } from "@/lib/api/health";
import { getPublicApiConfig } from "@/lib/env";
import { cn } from "@/lib/utils";

type IndicatorState = "checking" | "connected" | "unavailable";

const indicatorAppearance = {
  checking: {
    label: "Verificando conexión",
    icon: LoaderCircle,
    className: "text-muted-foreground",
    iconClassName: "animate-spin",
  },
  connected: {
    label: "API conectada",
    icon: CheckCircle2,
    className: "text-emerald-700",
    iconClassName: "",
  },
  unavailable: {
    label: "API no disponible",
    icon: CircleAlert,
    className: "text-amber-700",
    iconClassName: "",
  },
} as const;

function isHealthy(status: string) {
  const normalizedStatus = status.trim().toLowerCase();
  return normalizedStatus === "healthy" || normalizedStatus === "ok";
}

function ConnectionState({
  state,
  onRetry,
}: {
  state: IndicatorState;
  onRetry?: () => void;
}) {
  const appearance = indicatorAppearance[state];
  const Icon = appearance.icon;

  return (
    <div className="flex min-h-9 items-center justify-center gap-1.5" role="status" aria-live="polite">
      <Icon
        className={cn("h-3.5 w-3.5", appearance.className, appearance.iconClassName)}
        aria-hidden="true"
      />
      <span className={cn("text-xs font-medium", appearance.className)}>
        {appearance.label}
      </span>
      {onRetry ? (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="ml-1 h-7 px-2 text-xs text-muted-foreground"
          onClick={onRetry}
          aria-label="Volver a comprobar la conexión con la API"
        >
          <RefreshCcw className="h-3 w-3" aria-hidden="true" />
          Reintentar
        </Button>
      ) : null}
    </div>
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
    return <ConnectionState state="unavailable" />;
  }

  if (!isOnline) {
    return (
      <div
        className="flex min-h-9 items-center justify-center gap-1.5 text-muted-foreground"
        role="status"
        aria-live="polite"
      >
        <WifiOff className="h-3.5 w-3.5" aria-hidden="true" />
        <span className="text-xs font-medium">Sin conexión</span>
      </div>
    );
  }

  if (healthQuery.isPending) {
    return <ConnectionState state="checking" />;
  }

  if (healthQuery.isError || !isHealthy(healthQuery.data.status)) {
    return (
      <ConnectionState
        state="unavailable"
        onRetry={() => {
          void healthQuery.refetch();
        }}
      />
    );
  }

  return <ConnectionState state="connected" />;
}
