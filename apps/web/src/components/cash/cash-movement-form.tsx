"use client";

import { useActionState } from "react";

import { createCashMovementAction, type CashActionState } from "@/app/dashboard/cash/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

const initialState: CashActionState = {
  error: null,
  success: null,
};

const movementOptions = [
  { value: "cash_in", label: "Ingreso manual" },
  { value: "cash_out", label: "Egreso manual" },
];

const paymentMethodOptions = [
  { value: "cash", label: "Efectivo" },
  { value: "transfer", label: "Transferencia" },
  { value: "qr", label: "QR" },
  { value: "card", label: "Tarjeta" },
  { value: "account", label: "Cuenta corriente" },
];

export function CashMovementForm({ cashSessionId }: { cashSessionId: string }) {
  const [state, formAction, isPending] = useActionState(createCashMovementAction, initialState);

  return (
    <form action={formAction} className="grid gap-4 rounded-xl border border-border/70 bg-background/70 p-4">
      <input type="hidden" name="cashSessionId" value={cashSessionId} />

      <div className="space-y-1">
        <h3 className="text-base font-semibold">Registrar movimiento</h3>
        <p className="text-sm text-muted-foreground">Libro diario manual para ingresos y egresos del turno.</p>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <SelectField label="Tipo" name="movementType" options={movementOptions} />
        <Field label="Categoria" name="categoryCode" placeholder="gasto, retiro, ingreso_varios..." required />
        <SelectField label="Medio de pago" name="paymentMethod" options={paymentMethodOptions} />
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Concepto" name="concept" placeholder="Pago de flete, retiro, ingreso extra..." required />
        <NumberField label="Importe" name="amount" />
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Referencia" name="referenceDocument" placeholder="Comprobante o nota interna" />
        <Field label="Notas" name="notes" placeholder="Detalle adicional" />
      </div>

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
        step="0.01"
        min="0.01"
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
