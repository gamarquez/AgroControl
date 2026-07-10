"use client";

import { useActionState } from "react";

import { closeCashSessionAction, type CashActionState } from "@/app/dashboard/cash/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

const initialState: CashActionState = {
  error: null,
  success: null,
};

export function CashCloseSessionForm({
  cashSessionId,
  currentBalance,
}: {
  cashSessionId: string;
  currentBalance: number;
}) {
  const [state, formAction, isPending] = useActionState(closeCashSessionAction, initialState);

  return (
    <form action={formAction} className="grid gap-4 rounded-xl border border-border/70 bg-background/70 p-4">
      <input type="hidden" name="cashSessionId" value={cashSessionId} />

      <div className="space-y-1">
        <h3 className="text-base font-semibold">Cerrar caja</h3>
        <p className="text-sm text-muted-foreground">Saldo teorico actual: {formatCurrency(currentBalance)}.</p>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <NumberField label="Monto contado al cierre" name="closingAmount" defaultValue={String(currentBalance)} />
        <Field label="Notas de cierre" name="closingNotes" placeholder="Diferencias, novedades del turno..." />
      </div>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos cerrar la caja</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Caja cerrada</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Cerrando..." : "Cerrar caja"}
      </Button>
    </form>
  );
}

function Field({
  label,
  name,
  placeholder,
}: {
  label: string;
  name: string;
  placeholder?: string;
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
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </div>
  );
}

function NumberField({
  label,
  name,
  defaultValue,
}: {
  label: string;
  name: string;
  defaultValue: string;
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
        step="0.01"
        min="0"
        defaultValue={defaultValue}
        required
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </div>
  );
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat("es-AR", {
    style: "currency",
    currency: "ARS",
  }).format(value);
}
