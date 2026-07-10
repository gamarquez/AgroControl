"use client";

import { useActionState } from "react";

import { createStockMovementAction, type StockActionState } from "@/app/dashboard/stock/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { StockDetail } from "@/lib/api/contracts";

const initialState: StockActionState = {
  error: null,
  success: null,
};

const movementOptions = [
  { value: "purchase_inbound", label: "Ingreso por compra" },
  { value: "sale_outbound", label: "Egreso por venta" },
  { value: "adjustment_increase", label: "Ajuste positivo" },
  { value: "adjustment_decrease", label: "Ajuste negativo" },
  { value: "return_inbound", label: "Devolucion" },
  { value: "loss", label: "Perdida" },
  { value: "broken", label: "Rotura" },
  { value: "expired", label: "Vencimiento" },
];

export function StockMovementForm({ item }: { item: StockDetail }) {
  const [state, formAction, isPending] = useActionState(createStockMovementAction, initialState);

  return (
    <form action={formAction} className="grid gap-4 rounded-xl border border-border/70 bg-background/70 p-4">
      <input type="hidden" name="warehouseId" value={item.warehouse.warehouseId} />
      <input type="hidden" name="productId" value={item.productId} />

      <div className="grid gap-4 md:grid-cols-3">
        <SelectField label="Tipo de movimiento" name="movementType" options={movementOptions} />
        <NumberField label="Cantidad" name="quantity" />
        <Field label="Motivo" name="reason" placeholder="Compra local, ajuste de conteo, merma..." required />
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Referencia" name="referenceDocument" placeholder="Remito, ticket o nota interna" />
        <Field label="Notas" name="notes" placeholder="Observaciones operativas" />
      </div>

      <p className="text-sm text-muted-foreground">
        Deposito: {item.warehouse.name}. Stock actual: {item.onHandQuantity.toFixed(3)} {item.baseUnit.symbol}
      </p>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos registrar el movimiento</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Movimiento registrado</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Registrando..." : "Registrar movimiento"}
      </Button>
    </form>
  );
}

function Field({
  label,
  name,
  placeholder,
  required = false,
}: {
  label: string;
  name: string;
  placeholder?: string;
  required?: boolean;
}) {
  return (
    <div className="space-y-2">
      <label className="text-sm font-medium" htmlFor={`${name}-${label}`}>
        {label}
      </label>
      <input
        id={`${name}-${label}`}
        name={name}
        placeholder={placeholder}
        required={required}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </div>
  );
}

function NumberField({ label, name }: { label: string; name: string }) {
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
        min="0.001"
        required
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </div>
  );
}

function SelectField({
  label,
  name,
  options,
}: {
  label: string;
  name: string;
  options: Array<{ value: string; label: string }>;
}) {
  return (
    <div className="space-y-2">
      <label className="text-sm font-medium" htmlFor={`${name}-${label}`}>
        {label}
      </label>
      <select
        id={`${name}-${label}`}
        name={name}
        defaultValue={options[0]?.value ?? ""}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  );
}
