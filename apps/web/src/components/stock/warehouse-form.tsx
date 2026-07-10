"use client";

import { useActionState } from "react";

import { upsertWarehouseAction, type StockActionState } from "@/app/dashboard/stock/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { Warehouse } from "@/lib/api/contracts";

const initialState: StockActionState = {
  error: null,
  success: null,
};

export function WarehouseForm({ warehouse }: { warehouse?: Warehouse }) {
  const [state, formAction, isPending] = useActionState(upsertWarehouseAction, initialState);

  return (
    <form action={formAction} className="grid gap-4 rounded-xl border border-border/70 bg-background/70 p-4">
      {warehouse ? <input type="hidden" name="warehouseId" value={warehouse.warehouseId} /> : null}

      <div className="space-y-1">
        <h3 className="text-base font-semibold">{warehouse ? "Editar deposito" : "Nuevo deposito"}</h3>
        <p className="text-sm text-muted-foreground">
          Mantiene la operacion preparada para multiples ubicaciones sin afectar el POS actual.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Nombre" name="name" defaultValue={warehouse?.name} placeholder="Deposito centro" required />
        <Field label="Codigo" name="code" defaultValue={warehouse?.code} placeholder="CTR" required />
      </div>

      <div className="flex flex-wrap gap-6">
        <ToggleField
          label="Deposito default"
          name="isDefault"
          defaultChecked={warehouse?.isDefault ?? !warehouse}
        />
        <ToggleField
          label="Activo"
          name="isActive"
          defaultChecked={warehouse?.isActive ?? true}
        />
      </div>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos guardar el deposito</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Deposito guardado</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Guardando..." : warehouse ? "Actualizar deposito" : "Crear deposito"}
      </Button>
    </form>
  );
}

function Field({
  label,
  name,
  defaultValue,
  placeholder,
  required = false,
}: {
  label: string;
  name: string;
  defaultValue?: string;
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
        defaultValue={defaultValue}
        placeholder={placeholder}
        required={required}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </div>
  );
}

function ToggleField({
  label,
  name,
  defaultChecked,
}: {
  label: string;
  name: string;
  defaultChecked: boolean;
}) {
  return (
    <label className="flex items-center gap-2 text-sm font-medium">
      <input name={name} type="checkbox" defaultChecked={defaultChecked} className="h-4 w-4" />
      {label}
    </label>
  );
}
