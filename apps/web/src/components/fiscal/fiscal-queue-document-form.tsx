"use client";

import { useActionState } from "react";

import { queueFiscalDocumentAction, type FiscalActionState } from "@/app/dashboard/fiscal/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

const initialState: FiscalActionState = {
  error: null,
  success: null,
};

export function FiscalQueueDocumentForm({
  saleId,
  ticketNumber,
  canEdit,
}: {
  saleId: string;
  ticketNumber: number;
  canEdit: boolean;
}) {
  const [state, formAction, isPending] = useActionState(queueFiscalDocumentAction, initialState);

  return (
    <form action={formAction} className="grid gap-3">
      <input type="hidden" name="saleId" value={saleId} />
      <input type="hidden" name="documentKind" value="invoice" />

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos preparar el comprobante</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Comprobante en cola</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full" disabled={!canEdit || isPending}>
        {isPending ? "Preparando..." : `Preparar ticket #${ticketNumber}`}
      </Button>
    </form>
  );
}
