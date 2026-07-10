"use client";

import { useActionState } from "react";

import { recordPhysicalInventoryCountAction, type StockActionState } from "@/app/dashboard/stock/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { StockDetail } from "@/lib/api/contracts";

const initialState: StockActionState = {
  error: null,
  success: null,
};

export function PhysicalInventoryCountForm({ item }: { item: StockDetail }) {
  const [state, formAction, isPending] = useActionState(recordPhysicalInventoryCountAction, initialState);

  return (
    <form action={formAction} className="grid gap-4 rounded-xl border border-border/70 bg-background/70 p-4">
      <input type="hidden" name="warehouseId" value={item.warehouse.warehouseId} />
      <input type="hidden" name="productId" value={item.productId} />

      <div className="space-y-1">
        <h3 className="text-base font-semibold">Inventario fisico</h3>
        <p className="text-sm text-muted-foreground">
          Registra un conteo real en {item.warehouse.name}. Si hay diferencia, se aplica un ajuste automatico y auditable.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Field
          label="Cantidad contada"
          name="countedQuantity"
          type="number"
          step="0.001"
          min="0"
          placeholder={`Actual ${item.onHandQuantity.toFixed(3)}`}
          required
        />
        <Field label="Motivo" name="reason" placeholder="Conteo semanal, cierre, auditoria..." required />
      </div>

      <Field label="Notas" name="notes" placeholder="Observaciones del conteo" />

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos registrar el conteo</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Conteo registrado</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Registrando..." : "Registrar conteo"}
      </Button>
    </form>
  );
}

function Field({
  label,
  name,
  placeholder,
  required = false,
  type = "text",
  step,
  min,
}: {
  label: string;
  name: string;
  placeholder?: string;
  required?: boolean;
  type?: string;
  step?: string;
  min?: string;
}) {
  return (
    <div className="space-y-2">
      <label className="text-sm font-medium" htmlFor={`${name}-${label}`}>
        {label}
      </label>
      <input
        id={`${name}-${label}`}
        name={name}
        type={type}
        step={step}
        min={min}
        placeholder={placeholder}
        required={required}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </div>
  );
}
