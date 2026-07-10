"use client";

import { useActionState } from "react";

import { recordCustomerCreditNoteAction, type CustomerActionState } from "@/app/dashboard/customers/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { Customer } from "@/lib/api/contracts";

const initialState: CustomerActionState = {
  error: null,
  success: null,
};

export function CustomerCreditNoteForm({
  customer,
  canEdit,
}: {
  customer: Customer;
  canEdit: boolean;
}) {
  const [state, formAction, isPending] = useActionState(recordCustomerCreditNoteAction, initialState);

  return (
    <form action={formAction} className="grid gap-4">
      <input type="hidden" name="customerId" value={customer.customerId} />

      <div className="grid gap-4 md:grid-cols-2">
        <label className="space-y-2 text-sm">
          <span className="font-medium">Importe a acreditar</span>
          <input
            name="amount"
            type="number"
            min="0.01"
            step="0.01"
            defaultValue={customer.currentBalance > 0 ? customer.currentBalance.toFixed(2) : ""}
            disabled={!canEdit}
            className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary disabled:cursor-not-allowed disabled:opacity-70"
          />
        </label>

        <label className="space-y-2 text-sm">
          <span className="font-medium">Referencia</span>
          <input
            name="referenceDocument"
            type="text"
            disabled={!canEdit}
            placeholder="NC interna, remito, ajuste"
            className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary disabled:cursor-not-allowed disabled:opacity-70"
          />
        </label>
      </div>

      <label className="space-y-2 text-sm">
        <span className="font-medium">Concepto</span>
        <input
          name="concept"
          type="text"
          defaultValue="Nota de credito por ajuste comercial"
          disabled={!canEdit}
          className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary disabled:cursor-not-allowed disabled:opacity-70"
        />
      </label>

      <label className="space-y-2 text-sm">
        <span className="font-medium">Notas</span>
        <textarea
          name="notes"
          rows={3}
          disabled={!canEdit}
          placeholder="Motivo del ajuste y respaldo interno"
          className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none transition focus:border-primary disabled:cursor-not-allowed disabled:opacity-70"
        />
      </label>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos registrar la nota de credito</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Nota de credito registrada</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={!canEdit || isPending}>
        {isPending ? "Guardando..." : "Registrar nota de credito"}
      </Button>
    </form>
  );
}
