"use client";

import { useActionState } from "react";

import { openCashSessionAction, type CashActionState } from "@/app/dashboard/cash/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

const initialState: CashActionState = {
  error: null,
  success: null,
};

export function CashOpenSessionForm({
  cashRegisterCode,
  cashRegisterName,
}: {
  cashRegisterCode: string;
  cashRegisterName: string;
}) {
  const [state, formAction, isPending] = useActionState(openCashSessionAction, initialState);

  return (
    <form action={formAction} className="grid gap-4 rounded-xl border border-border/70 bg-background/70 p-4">
      <input type="hidden" name="cashRegisterCode" value={cashRegisterCode} />

      <div className="space-y-1">
        <h3 className="text-base font-semibold">Abrir caja</h3>
        <p className="text-sm text-muted-foreground">Caja activa: {cashRegisterName}.</p>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <NumberField label="Monto de apertura" name="openingAmount" />
        <Field label="Notas iniciales" name="openingNotes" placeholder="Observaciones del turno" />
      </div>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos abrir la caja</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Caja abierta</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Abriendo..." : "Abrir caja"}
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
        min="0"
        required
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </div>
  );
}
