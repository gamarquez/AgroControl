"use client";

import { useActionState } from "react";

import { reverseCashSaleAction, type PosActionState } from "@/app/dashboard/pos/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

const initialState: PosActionState = {
  error: null,
  success: null,
};

export function PosReverseSaleForm({
  saleId,
  ticketNumber,
  cashSessionId,
}: {
  saleId: string;
  ticketNumber: number;
  cashSessionId: string;
}) {
  const [state, formAction, isPending] = useActionState(reverseCashSaleAction, initialState);

  return (
    <form action={formAction} className="grid gap-3 rounded-lg border border-border/70 bg-background/70 p-4">
      <input type="hidden" name="saleId" value={saleId} />
      <input type="hidden" name="cashSessionId" value={cashSessionId} />

      <div className="space-y-1">
        <p className="text-sm font-medium">Revertir ticket #{ticketNumber}</p>
        <p className="text-xs text-muted-foreground">
          Restituye stock y registra un egreso compensatorio en la caja abierta.
        </p>
      </div>

      <textarea
        name="reversalNotes"
        rows={2}
        placeholder="Motivo de la reversa"
        className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none transition focus:border-primary"
      />

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos revertir la venta</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Venta revertida</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" variant="outline" disabled={isPending}>
        {isPending ? "Revirtiendo..." : "Revertir venta"}
      </Button>
    </form>
  );
}
