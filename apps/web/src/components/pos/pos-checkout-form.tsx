"use client";

import { useActionState, useMemo, useState } from "react";

import { createCheckoutSaleAction, type PosActionState } from "@/app/dashboard/pos/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { Customer, PosProduct } from "@/lib/api/contracts";

const initialState: PosActionState = {
  error: null,
  success: null,
};

const paymentMethodOptions = [
  { value: "cash", label: "Efectivo" },
  { value: "transfer", label: "Transferencia" },
  { value: "qr", label: "QR" },
  { value: "card", label: "Tarjeta" },
  { value: "account", label: "Cuenta corriente" },
] as const;

type CartItem = {
  productId: string;
  name: string;
  unitSymbol: string;
  quantity: number;
  price: number;
  stock: number;
  allowsFraction: boolean;
};

type PaymentDraft = {
  id: string;
  paymentMethod: (typeof paymentMethodOptions)[number]["value"];
  amount: string;
  reference: string;
  providerName: string;
};

export function PosCheckoutForm({
  cashSessionId,
  products,
  customers,
  canEdit,
}: {
  cashSessionId: string;
  products: PosProduct[];
  customers: Customer[];
  canEdit: boolean;
}) {
  const [state, formAction, isPending] = useActionState(createCheckoutSaleAction, initialState);
  const [cart, setCart] = useState<CartItem[]>([]);
  const [selectedCustomerId, setSelectedCustomerId] = useState("");
  const [payments, setPayments] = useState<PaymentDraft[]>([
    createPaymentDraft("cash"),
  ]);

  const total = useMemo(
    () => cart.reduce((sum, item) => sum + item.quantity * item.price, 0),
    [cart],
  );

  const totalPayments = useMemo(
    () =>
      payments.reduce((sum, payment) => {
        const amount = Number(payment.amount || "0");
        return Number.isNaN(amount) ? sum : sum + amount;
      }, 0),
    [payments],
  );

  const remaining = useMemo(
    () => Number((total - totalPayments).toFixed(2)),
    [total, totalPayments],
  );
  const hasAccountPayment = payments.some((payment) => payment.paymentMethod === "account");

  const serializedItems = JSON.stringify(
    cart.map((item) => ({
      productId: item.productId,
      quantity: item.quantity,
    })),
  );

  const serializedPayments = JSON.stringify(
    payments
      .map((payment) => ({
        paymentMethod: payment.paymentMethod,
        amount: Number(payment.amount || "0"),
        reference: payment.reference || undefined,
        providerName: payment.providerName || undefined,
      }))
      .filter((payment) => payment.amount > 0),
  );

  return (
    <div className="grid gap-4">
      <div className="grid gap-3">
        {products.length === 0 ? (
          <Alert>
            <AlertTitle>Sin productos para venta</AlertTitle>
            <AlertDescription>No encontramos productos activos con ese criterio.</AlertDescription>
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
        <input type="hidden" name="cashSessionId" value={cashSessionId} />
        <input type="hidden" name="customerId" value={selectedCustomerId} />
        <input type="hidden" name="items" value={serializedItems} />
        <input type="hidden" name="payments" value={serializedPayments} />

        <div className="space-y-1">
          <h3 className="text-base font-semibold">Checkout POS con pagos mixtos</h3>
          <p className="text-sm text-muted-foreground">
            Combina efectivo, transferencia, QR, tarjeta y cuenta corriente en una sola venta.
          </p>
        </div>

        <label className="space-y-2 text-sm">
          <span>Cliente</span>
          <select
            value={selectedCustomerId}
            onChange={(event) => handleCustomerChange(event.target.value, setSelectedCustomerId, setPayments)}
            className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
          >
            <option value="">Consumidor final</option>
            {customers.map((customer) => (
              <option key={customer.customerId} value={customer.customerId}>
                {customer.displayName}
              </option>
            ))}
          </select>
        </label>

        {hasAccountPayment ? (
          <label className="space-y-2 text-sm">
            <span>Vencimiento de saldo en cuenta</span>
            <input
              name="dueDate"
              type="date"
              min={new Date().toISOString().slice(0, 10)}
              required={hasAccountPayment}
              className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
            />
          </label>
        ) : null}

        {cart.length === 0 ? (
          <Alert>
            <AlertTitle>Carrito vacio</AlertTitle>
            <AlertDescription>Agrega productos desde el panel de la izquierda para confirmar una venta.</AlertDescription>
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

        <div className="grid gap-3">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h4 className="text-sm font-semibold">Medios de pago</h4>
              <p className="text-xs text-muted-foreground">
                Usa una o varias lineas hasta cubrir el total.
              </p>
            </div>
            <Button type="button" variant="outline" onClick={() => addPaymentRow(setPayments, remaining)}>
              Agregar medio
            </Button>
          </div>

          {payments.map((payment) => (
            <div
              key={payment.id}
              className="grid gap-3 rounded-lg border border-border/70 p-3 md:grid-cols-[160px_120px_1fr_1fr_auto]"
            >
              <label className="space-y-1 text-sm">
                <span>Medio</span>
                <select
                  value={payment.paymentMethod}
                  onChange={(event) =>
                    updatePayment(payment.id, { paymentMethod: event.target.value as PaymentDraft["paymentMethod"] }, setPayments)
                  }
                  className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
                >
                  {paymentMethodOptions.map((option) => (
                    <option
                      key={option.value}
                      value={option.value}
                      disabled={option.value === "account" && !selectedCustomerId}
                    >
                      {option.label}
                    </option>
                  ))}
                </select>
              </label>

              <label className="space-y-1 text-sm">
                <span>Importe</span>
                <input
                  type="number"
                  min="0.01"
                  step="0.01"
                  value={payment.amount}
                  onChange={(event) => updatePayment(payment.id, { amount: event.target.value }, setPayments)}
                  className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
                />
              </label>

              <label className="space-y-1 text-sm">
                <span>Referencia</span>
                <input
                  value={payment.reference}
                  onChange={(event) => updatePayment(payment.id, { reference: event.target.value }, setPayments)}
                  placeholder="Operacion, cupón o alias"
                  className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
                />
              </label>

              <label className="space-y-1 text-sm">
                <span>Proveedor / terminal</span>
                <input
                  value={payment.providerName}
                  onChange={(event) => updatePayment(payment.id, { providerName: event.target.value }, setPayments)}
                  placeholder="Banco, billetera o POS"
                  className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
                />
              </label>

              <div className="flex items-end">
                <Button
                  type="button"
                  variant="ghost"
                  disabled={payments.length === 1}
                  onClick={() => removePaymentRow(payment.id, setPayments)}
                >
                  Quitar
                </Button>
              </div>
            </div>
          ))}
        </div>

        <label className="space-y-2 text-sm">
          <span>Notas</span>
          <textarea
            name="notes"
            rows={3}
            placeholder="Observaciones internas del ticket"
            className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none transition focus:border-primary"
          />
        </label>

        <div className="grid gap-4 rounded-lg border border-border/70 bg-background p-3 md:grid-cols-3">
          <SummaryItem label="Total venta" value={formatCurrency(total)} />
          <SummaryItem label="Pagos informados" value={formatCurrency(totalPayments)} />
          <SummaryItem label="Diferencia" value={formatCurrency(remaining)} />
        </div>

        {hasAccountPayment && !selectedCustomerId ? (
          <Alert variant="destructive">
            <AlertTitle>Cliente requerido</AlertTitle>
            <AlertDescription>
              Debes seleccionar un cliente cuando uno de los medios de pago es cuenta corriente.
            </AlertDescription>
          </Alert>
        ) : null}

        {remaining !== 0 ? (
          <Alert variant="destructive">
            <AlertTitle>Pagos incompletos o excedidos</AlertTitle>
            <AlertDescription>
              La suma de medios de pago debe coincidir exactamente con el total de la venta.
            </AlertDescription>
          </Alert>
        ) : null}

        {state.error ? (
          <Alert variant="destructive">
            <AlertTitle>No pudimos registrar la venta</AlertTitle>
            <AlertDescription>{state.error}</AlertDescription>
          </Alert>
        ) : null}

        {state.success ? (
          <Alert>
            <AlertTitle>Venta confirmada</AlertTitle>
            <AlertDescription>{state.success}</AlertDescription>
          </Alert>
        ) : null}

        <Button
          type="submit"
          className="w-full md:w-fit"
          disabled={!canEdit || isPending || cart.length === 0 || remaining !== 0 || (hasAccountPayment && !selectedCustomerId)}
        >
          {isPending ? "Confirmando..." : "Confirmar venta POS"}
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

function createPaymentDraft(paymentMethod: PaymentDraft["paymentMethod"], amount = "0.00"): PaymentDraft {
  return {
    id: crypto.randomUUID(),
    paymentMethod,
    amount,
    reference: "",
    providerName: "",
  };
}

function handleCustomerChange(
  customerId: string,
  setSelectedCustomerId: React.Dispatch<React.SetStateAction<string>>,
  setPayments: React.Dispatch<React.SetStateAction<PaymentDraft[]>>,
) {
  setSelectedCustomerId(customerId);

  if (customerId) {
    return;
  }

  setPayments((current) =>
    current.map((payment) =>
      payment.paymentMethod === "account"
        ? { ...payment, paymentMethod: "cash" }
        : payment,
    ),
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

function addPaymentRow(
  setPayments: React.Dispatch<React.SetStateAction<PaymentDraft[]>>,
  remaining: number,
) {
  setPayments((current) => [
    ...current,
    createPaymentDraft("cash", remaining > 0 ? remaining.toFixed(2) : "0.00"),
  ]);
}

function updatePayment(
  paymentId: string,
  patch: Partial<PaymentDraft>,
  setPayments: React.Dispatch<React.SetStateAction<PaymentDraft[]>>,
) {
  setPayments((current) =>
    current.map((payment) => (payment.id === paymentId ? { ...payment, ...patch } : payment)),
  );
}

function removePaymentRow(
  paymentId: string,
  setPayments: React.Dispatch<React.SetStateAction<PaymentDraft[]>>,
) {
  setPayments((current) => current.filter((payment) => payment.id !== paymentId));
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat("es-AR", {
    style: "currency",
    currency: "ARS",
  }).format(value);
}
