"use client";

import { useActionState } from "react";

import { updateStockPolicyAction, type StockActionState } from "@/app/dashboard/stock/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { StockDetail } from "@/lib/api/contracts";

const initialState: StockActionState = {
  error: null,
  success: null,
};

export function StockPolicyForm({ item }: { item: StockDetail }) {
  const [state, formAction, isPending] = useActionState(updateStockPolicyAction, initialState);

  return (
    <form action={formAction} className="grid gap-4 rounded-xl border border-border/70 bg-background/70 p-4">
      <input type="hidden" name="warehouseId" value={item.warehouse.warehouseId} />
      <input type="hidden" name="productId" value={item.productId} />

      <div className="grid gap-4 md:grid-cols-3">
        <NumberField label="Stock minimo" name="minQuantity" defaultValue={toDefault(item.policy.minQuantity)} />
        <NumberField label="Stock maximo" name="maxQuantity" defaultValue={toDefault(item.policy.maxQuantity)} />
        <NumberField
          label="Punto de reposicion"
          name="reorderPoint"
          defaultValue={toDefault(item.policy.reorderPoint)}
        />
      </div>

      <p className="text-sm text-muted-foreground">
        Deposito: {item.warehouse.name}. Version actual: {item.policy.versionNumber}. Ultima actualizacion: {formatDate(item.policy.updatedAt)}.
      </p>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos actualizar la politica</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Politica actualizada</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Guardando..." : "Guardar politica"}
      </Button>
    </form>
  );
}

function NumberField({
  label,
  name,
  defaultValue,
}: {
  label: string;
  name: string;
  defaultValue?: string;
}) {
  return (
    <div className="space-y-2">
      <label className="text-sm font-medium" htmlFor={`${name}-${label}`}>
        {label}
      </label>
      <input
        id={`${name}-${label}`}
        name={name}
        type="number"
        step="0.001"
        min="0"
        defaultValue={defaultValue}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </div>
  );
}

function toDefault(value: number | null) {
  return value === null ? "" : String(value);
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("es-AR", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(value));
}
