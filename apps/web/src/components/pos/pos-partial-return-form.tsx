"use client";

import { useActionState, useState } from "react";

import { returnSaleItemsAction, type PosActionState } from "@/app/dashboard/pos/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { Sale } from "@/lib/api/contracts";

const initialState: PosActionState = {
  error: null,
  success: null,
};

export function PosPartialReturnForm({
  sale,
  cashSessionId,
  canEdit,
}: {
  sale: Sale;
  cashSessionId?: string;
  canEdit: boolean;
}) {
  const [state, formAction, isPending] = useActionState(returnSaleItemsAction, initialState);
  const [quantities, setQuantities] = useState<Record<string, string>>({});

  const availableItems = sale.items.filter((item) => item.availableToReturnQuantity > 0);
  const selectedItemsJson = JSON.stringify(
    availableItems
      .map((item) => ({
        saleItemId: item.saleItemId,
        quantity: Number(quantities[item.saleItemId] || "0"),
      }))
      .filter((item) => item.quantity > 0),
  );

  const requiresCashSession = sale.paidAmount > 0;

  return (
    <form action={formAction} className="grid gap-3 rounded-lg border border-border/70 bg-background/70 p-4">
      <input type="hidden" name="saleId" value={sale.saleId} />
      <input type="hidden" name="cashSessionId" value={cashSessionId ?? ""} />
      <input type="hidden" name="items" value={selectedItemsJson} />

      <div className="space-y-1">
        <p className="text-sm font-medium">Devolucion de items</p>
        <p className="text-xs text-muted-foreground">
          Reingresa stock y compensa proporcionalmente caja y cuenta corriente segun el ticket original.
          Si devuelves todo el ticket por este flujo, la venta queda cerrada como devuelta por completo.
        </p>
      </div>

      {availableItems.length === 0 ? (
        <Alert>
          <AlertTitle>Sin saldo para devolver</AlertTitle>
          <AlertDescription>Todos los items de este ticket ya fueron devueltos por completo.</AlertDescription>
        </Alert>
      ) : (
        <div className="grid gap-3">
          {availableItems.map((item) => (
            <label key={item.saleItemId} className="grid gap-2 rounded-lg border border-border/70 p-3 text-sm">
              <span className="font-medium">{item.productName}</span>
              <span className="text-xs text-muted-foreground">
                Vendido {item.quantity.toFixed(3)} {item.unitSymbol} | Devuelto {item.returnedQuantity.toFixed(3)} | Disponible{" "}
                {item.availableToReturnQuantity.toFixed(3)}
              </span>
              <input
                type="number"
                min="0"
                max={item.availableToReturnQuantity}
                step="0.001"
                value={quantities[item.saleItemId] ?? ""}
                disabled={!canEdit}
                onChange={(event) =>
                  setQuantities((current) => ({
                    ...current,
                    [item.saleItemId]: event.target.value,
                  }))
                }
                placeholder="Cantidad a devolver"
                className="h-11 rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary disabled:cursor-not-allowed disabled:opacity-70"
              />
            </label>
          ))}
        </div>
      )}

      <textarea
        name="notes"
        rows={2}
        disabled={!canEdit}
        placeholder="Motivo de la devolucion parcial"
        className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none transition focus:border-primary disabled:cursor-not-allowed disabled:opacity-70"
      />

      {requiresCashSession && !cashSessionId ? (
        <Alert>
          <AlertTitle>Caja requerida</AlertTitle>
          <AlertDescription>Esta venta tuvo importe cobrado y necesita una caja abierta para reintegrar el monto proporcional.</AlertDescription>
        </Alert>
      ) : null}

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos registrar la devolucion parcial</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Devolucion registrada</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button
        type="submit"
        variant="outline"
        disabled={!canEdit || availableItems.length === 0 || (requiresCashSession && !cashSessionId) || isPending}
      >
        {isPending ? "Registrando..." : "Registrar devolucion"}
      </Button>
    </form>
  );
}
