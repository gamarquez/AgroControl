"use client";

import { useActionState, useMemo, useState } from "react";

import { createAccountSaleAction, type CustomerActionState } from "@/app/dashboard/customers/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { Customer, PosProduct } from "@/lib/api/contracts";

const initialState: CustomerActionState = {
  error: null,
  success: null,
};

type CartItem = {
  productId: string;
  name: string;
  unitSymbol: string;
  quantity: number;
  price: number;
  stock: number;
  allowsFraction: boolean;
};

export function CustomerAccountSaleForm({
  customer,
  products,
  canEdit,
}: {
  customer: Customer;
  products: PosProduct[];
  canEdit: boolean;
}) {
  const [state, formAction, isPending] = useActionState(createAccountSaleAction, initialState);
  const [cart, setCart] = useState<CartItem[]>([]);

  const total = useMemo(
    () => cart.reduce((sum, item) => sum + item.quantity * item.price, 0),
    [cart],
  );

  return (
    <div className="grid gap-4">
      <div className="grid gap-3">
        {products.length === 0 ? (
          <Alert>
            <AlertTitle>Sin productos disponibles</AlertTitle>
            <AlertDescription>No encontramos productos activos para vender a cuenta.</AlertDescription>
          </Alert>
        ) : (
          products.map((product) => (
            <div
              key={product.productId}
              className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-border/70 bg-background/70 p-4"
            >
              <div className="space-y-1">
                <p className="text-sm font-medium">{product.name}</p>
                <p className="text-xs text-muted-foreground">
                  {product.internalCode}
                  {product.sku ? ` | SKU ${product.sku}` : ""}
                  {product.barcode ? ` | BAR ${product.barcode}` : ""}
                </p>
                <p className="text-xs text-muted-foreground">
                  Stock {product.onHandQuantity.toFixed(3)} {product.unitSymbol} | {formatCurrency(product.saleAmount)}
                </p>
              </div>
              <Button
                type="button"
                variant="outline"
                disabled={!canEdit || product.onHandQuantity <= 0}
                onClick={() => addToCart(product, setCart)}
              >
                Agregar
              </Button>
            </div>
          ))
        )}
      </div>

      <form action={formAction} className="grid gap-4 rounded-xl border border-border/70 bg-background/70 p-4">
        <input type="hidden" name="customerId" value={customer.customerId} />
        <input
          type="hidden"
          name="items"
          value={JSON.stringify(
            cart.map((item) => ({
              productId: item.productId,
              quantity: item.quantity,
            })),
          )}
        />

        <div className="space-y-1">
          <h3 className="text-base font-semibold">Venta a cuenta</h3>
          <p className="text-sm text-muted-foreground">
            Debita saldo del cliente y descuenta stock en una sola operacion.
          </p>
        </div>

        <label className="space-y-2 text-sm">
          <span>Vencimiento</span>
          <input
            name="dueDate"
            type="date"
            min={new Date().toISOString().slice(0, 10)}
            className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
          />
        </label>

        {cart.length === 0 ? (
          <Alert>
            <AlertTitle>Carrito vacio</AlertTitle>
            <AlertDescription>Agrega productos para registrar una venta a cuenta.</AlertDescription>
          </Alert>
        ) : (
          <div className="grid gap-3">
            {cart.map((item) => (
              <div
                key={item.productId}
                className="grid gap-3 rounded-lg border border-border/70 p-3 md:grid-cols-[1fr_120px_120px_auto]"
              >
                <div>
                  <p className="text-sm font-medium">{item.name}</p>
                  <p className="text-xs text-muted-foreground">
                    Stock disponible: {item.stock.toFixed(3)} {item.unitSymbol}
                  </p>
                </div>
                <label className="space-y-1 text-sm">
                  <span>Cantidad</span>
                  <input
                    type="number"
                    min={item.allowsFraction ? "0.001" : "1"}
                    step={item.allowsFraction ? "0.001" : "1"}
                    value={item.quantity}
                    onChange={(event) => updateQuantity(item.productId, event.target.value, setCart)}
                    className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
                  />
                </label>
                <div className="space-y-1 text-sm">
                  <span className="text-muted-foreground">Subtotal</span>
                  <p className="h-10 rounded-lg border border-border bg-background px-3 py-2">
                    {formatCurrency(item.quantity * item.price)}
                  </p>
                </div>
                <div className="flex items-end">
                  <Button type="button" variant="ghost" onClick={() => removeFromCart(item.productId, setCart)}>
                    Quitar
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}

        <label className="space-y-2 text-sm">
          <span>Notas</span>
          <textarea
            name="notes"
            rows={3}
            placeholder={`Observaciones internas para ${customer.displayName}`}
            className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none transition focus:border-primary"
          />
        </label>

        <div className="grid gap-4 rounded-lg border border-border/70 bg-background p-3 md:grid-cols-4">
          <SummaryItem label="Total a debitar" value={formatCurrency(total)} />
          <SummaryItem label="Saldo actual" value={formatCurrency(customer.currentBalance)} />
          <SummaryItem label="Saldo vencido" value={formatCurrency(customer.overdueBalance)} />
          <SummaryItem
            label="Disponible estimado"
            value={formatCurrency(customer.creditLimitAmount - customer.currentBalance - total)}
          />
        </div>

        {state.error ? (
          <Alert variant="destructive">
            <AlertTitle>No pudimos registrar la venta a cuenta</AlertTitle>
            <AlertDescription>{state.error}</AlertDescription>
          </Alert>
        ) : null}

        {state.success ? (
          <Alert>
            <AlertTitle>Venta a cuenta confirmada</AlertTitle>
            <AlertDescription>{state.success}</AlertDescription>
          </Alert>
        ) : null}

        <Button type="submit" className="w-full md:w-fit" disabled={!canEdit || isPending}>
          {isPending ? "Confirmando..." : "Registrar venta a cuenta"}
        </Button>
      </form>
    </div>
  );
}

function SummaryItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-border/70 bg-background/70 p-4">
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className="mt-2 text-base font-medium">{value}</p>
    </div>
  );
}

function addToCart(product: PosProduct, setCart: React.Dispatch<React.SetStateAction<CartItem[]>>) {
  setCart((current) => {
    const existing = current.find((item) => item.productId === product.productId);
    if (existing) {
      return current.map((item) =>
        item.productId === product.productId
          ? {
              ...item,
              quantity: clampQuantity(item.quantity + 1, item.stock, item.allowsFraction),
            }
          : item,
      );
    }

    return [
      ...current,
      {
        productId: product.productId,
        name: product.name,
        unitSymbol: product.unitSymbol,
        quantity: product.allowsFraction ? 0.001 : 1,
        price: product.saleAmount,
        stock: product.onHandQuantity,
        allowsFraction: product.allowsFraction,
      },
    ];
  });
}

function updateQuantity(
  productId: string,
  rawValue: string,
  setCart: React.Dispatch<React.SetStateAction<CartItem[]>>,
) {
  setCart((current) =>
    current.map((item) => {
      if (item.productId !== productId) {
        return item;
      }

      const nextQuantity = Number(rawValue || "0");
      if (Number.isNaN(nextQuantity)) {
        return item;
      }

      return {
        ...item,
        quantity: clampQuantity(nextQuantity, item.stock, item.allowsFraction),
      };
    }),
  );
}

function removeFromCart(productId: string, setCart: React.Dispatch<React.SetStateAction<CartItem[]>>) {
  setCart((current) => current.filter((item) => item.productId !== productId));
}

function clampQuantity(value: number, stock: number, allowsFraction: boolean) {
  if (value <= 0) {
    return allowsFraction ? 0.001 : 1;
  }

  const bounded = Math.min(value, stock);
  return allowsFraction ? Number(bounded.toFixed(3)) : Math.max(1, Math.trunc(bounded));
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat("es-AR", {
    style: "currency",
    currency: "ARS",
  }).format(value);
}
