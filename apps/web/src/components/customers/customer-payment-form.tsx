"use client";

import { useActionState } from "react";

import { recordCustomerPaymentAction, type CustomerActionState } from "@/app/dashboard/customers/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { Customer } from "@/lib/api/contracts";

const initialState: CustomerActionState = {
  error: null,
  success: null,
};

export function CustomerPaymentForm({
  customer,
  cashSessionId,
  canEdit,
}: {
  customer: Customer;
  cashSessionId: string;
  canEdit: boolean;
}) {
  const [state, formAction, isPending] = useActionState(recordCustomerPaymentAction, initialState);

  return (
    <form action={formAction} className="grid gap-4">
      <input type="hidden" name="customerId" value={customer.customerId} />
      <input type="hidden" name="cashSessionId" value={cashSessionId} />

      <div className="grid gap-4 md:grid-cols-2">
        <label className="space-y-2 text-sm">
          <span className="font-medium">Importe cobrado</span>
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
          <span className="font-medium">Saldo pendiente</span>
          <div className="h-11 rounded-lg border border-border bg-background px-3 py-2 text-sm">
            {formatCurrency(customer.currentBalance)}
          </div>
        </label>
      </div>

      <label className="space-y-2 text-sm">
        <span className="font-medium">Notas</span>
        <textarea
          name="notes"
          rows={3}
          disabled={!canEdit}
          placeholder="Medio de cobro, comprobante o comentario interno"
          className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none transition focus:border-primary disabled:cursor-not-allowed disabled:opacity-70"
        />
      </label>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos registrar la cobranza</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Cobranza registrada</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={!canEdit || isPending}>
        {isPending ? "Guardando..." : "Registrar cobranza"}
      </Button>
    </form>
  );
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat("es-AR", {
    style: "currency",
    currency: "ARS",
  }).format(value);
}
