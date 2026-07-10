"use client";

import { useActionState } from "react";

import { probeFiscalConnectivityAction, type FiscalActionState } from "@/app/dashboard/fiscal/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

const initialState: FiscalActionState = {
  error: null,
  success: null,
};

export function FiscalProbeForm({ canEdit }: { canEdit: boolean }) {
  const [state, formAction, isPending] = useActionState(probeFiscalConnectivityAction, initialState);

  return (
    <form action={formAction} className="grid gap-4">
      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>Prueba fiscal con observaciones</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Conectividad fiscal validada</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={!canEdit || isPending}>
        {isPending ? "Probando..." : "Probar conectividad ARCA"}
      </Button>
    </form>
  );
}
