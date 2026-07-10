import { PosCheckoutForm } from "@/components/pos/pos-checkout-form";
import { PosPartialReturnForm } from "@/components/pos/pos-partial-return-form";
import { PosReverseSaleForm } from "@/components/pos/pos-reverse-sale-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ApiError } from "@/lib/api/server";
import { getCashOverview, getSale, listCustomers, listPosProducts, listSales, requireSession } from "@/lib/auth/session";

export default async function PosPage({
  searchParams,
}: {
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
}) {
  const session = await requireSession();
  const resolvedSearchParams = (await searchParams) ?? {};
  const canEdit = session.roles.some((role) =>
    role.code === "administrator" || role.code === "manager" || role.code === "seller" || role.code === "cashier",
  );

  let overview = null;
  let products = null;
  let sales = null;
  let selectedSale = null;
  let customers = null;
  let errorState: { title: string; description: string } | null = null;

  try {
    [overview, products, sales, customers] = await Promise.all([
      getCashOverview(),
      listPosProducts({
        search: firstValue(resolvedSearchParams.search),
        pageSize: 24,
      }),
      listSales({
        search: firstValue(resolvedSearchParams.saleSearch),
        status: firstValue(resolvedSearchParams.status),
        customerId: firstValue(resolvedSearchParams.customerId),
        pageSize: 10,
      }),
      listCustomers({ isActive: "true", pageSize: 50 }),
    ]);

    const selectedSaleId = firstValue(resolvedSearchParams.saleId) ?? sales.items[0]?.saleId;
    if (selectedSaleId) {
      selectedSale = await getSale(selectedSaleId);
    }
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      errorState = {
        title: "Acceso restringido",
        description: "No cuentas con permisos para operar o consultar ventas.",
      };
    } else {
      errorState = {
        title: "No pudimos cargar el punto de venta",
        description: error instanceof Error ? error.message : "Ocurrio un error inesperado.",
      };
    }
  }

  if (errorState || !overview || !products || !sales || !customers) {
    return (
      <Alert variant="destructive">
        <AlertTitle>{errorState?.title ?? "No pudimos cargar el punto de venta"}</AlertTitle>
        <AlertDescription>{errorState?.description ?? "Ocurrio un error inesperado."}</AlertDescription>
      </Alert>
    );
  }

  const currentSession = overview.currentSession;

  return (
    <div className="grid gap-6">
      <Card>
        <CardHeader className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <Badge className="w-fit">POS checkout</Badge>
            {!canEdit ? <Badge variant="outline">Lectura</Badge> : null}
            {currentSession ? <Badge variant="outline">Caja abierta</Badge> : <Badge variant="secondary">Caja requerida</Badge>}
          </div>
          <div className="space-y-2">
            <CardTitle className="text-2xl">Venta mostrador con pagos mixtos</CardTitle>
            <CardDescription>
              Ticket interno, descuento automatico de stock y cobro con efectivo, transferencia, QR, tarjeta o cuenta corriente.
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent className="grid gap-4">
          <form method="get" className="grid gap-4 md:grid-cols-4">
            <input
              name="search"
              defaultValue={firstValue(resolvedSearchParams.search) ?? ""}
              placeholder="Buscar por nombre, codigo, SKU o barcode"
              className="h-11 rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
            />
            <input
              name="saleSearch"
              defaultValue={firstValue(resolvedSearchParams.saleSearch) ?? ""}
              placeholder="Buscar ticket o cliente"
              className="h-11 rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
            />
            <select
              name="status"
              defaultValue={firstValue(resolvedSearchParams.status) ?? ""}
              className="h-11 rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
            >
              <option value="">Todos los estados</option>
              <option value="confirmed">Confirmadas</option>
              <option value="partially_returned">Parcialmente devueltas</option>
              <option value="fully_returned">Devueltas por completo</option>
              <option value="reversed">Revertidas</option>
            </select>
            <button className="h-11 rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground">
              Buscar productos y tickets
            </button>
          </form>

          <div className="grid gap-4 md:grid-cols-5">
            <SummaryItem label="Caja" value={overview.cashRegister.name} />
            <SummaryItem label="Estado" value={currentSession ? "Lista para vender" : "Abrir caja"} />
            <SummaryItem
              label="Saldo actual"
              value={currentSession ? formatCurrency(currentSession.currentBalance) : "Sin sesion abierta"}
            />
            <SummaryItem label="Productos listados" value={String(products.total)} />
            <SummaryItem label="Clientes disponibles" value={String(customers.total)} />
          </div>

          {!currentSession ? (
            <Alert variant="destructive">
              <AlertTitle>No hay caja abierta</AlertTitle>
              <AlertDescription>
                Debes abrir una sesion en caja antes de confirmar ventas en efectivo.
              </AlertDescription>
            </Alert>
          ) : null}

          {!canEdit ? (
            <Alert>
              <AlertTitle>Vista de consulta</AlertTitle>
              <AlertDescription>
                Tu rol puede consultar ventas, pero no confirmar nuevas operaciones.
              </AlertDescription>
            </Alert>
          ) : null}
        </CardContent>
      </Card>

      <div className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
        <section>
          {currentSession ? (
            <PosCheckoutForm
              cashSessionId={currentSession.cashSessionId}
              products={products.items}
              customers={customers.items}
              canEdit={canEdit}
            />
          ) : (
            <Card>
              <CardHeader>
                <CardTitle className="text-lg">Caja pendiente</CardTitle>
                <CardDescription>
                  Abre una sesion de caja desde el modulo de caja para habilitar el POS.
                </CardDescription>
              </CardHeader>
            </Card>
          )}
        </section>

        <aside>
          {selectedSale ? (
            <Card className="mb-4">
              <CardHeader>
                <CardTitle className="text-lg">Detalle del ticket #{selectedSale.ticketNumber}</CardTitle>
                <CardDescription>
                  {selectedSale.customerName} | {translateSaleStatus(selectedSale.status)} | {translateSaleChannel(selectedSale.saleChannel)}
                </CardDescription>
              </CardHeader>
              <CardContent className="grid gap-4">
                <div className="grid gap-4 md:grid-cols-3">
                  <SummaryItem label="Total" value={formatCurrency(selectedSale.totalAmount)} />
                  <SummaryItem label="Cobrado" value={formatCurrency(selectedSale.paidAmount)} />
                  <SummaryItem label="Cuenta" value={formatCurrency(selectedSale.accountBalanceAmount)} />
                </div>
                {selectedSale.creditBalanceAppliedAmount > 0 ? (
                  <Alert>
                    <AlertTitle>Saldo a favor aplicado</AlertTitle>
                    <AlertDescription>
                      Esta venta consumio {formatCurrency(selectedSale.creditBalanceAppliedAmount)} de saldo a favor del cliente.
                    </AlertDescription>
                  </Alert>
                ) : null}
                <div className="grid gap-2">
                  {selectedSale.items.map((item) => (
                    <div key={item.saleItemId} className="rounded-lg border border-border/70 bg-background/70 p-3">
                      <p className="text-sm font-medium">{item.productName}</p>
                      <p className="text-xs text-muted-foreground">
                        {item.quantity.toFixed(3)} {item.unitSymbol} x {formatCurrency(item.unitPrice)} = {formatCurrency(item.lineTotal)}
                      </p>
                      <p className="text-xs text-muted-foreground">
                        Devuelto {item.returnedQuantity.toFixed(3)} | Disponible {item.availableToReturnQuantity.toFixed(3)}
                      </p>
                    </div>
                  ))}
                </div>
                <div className="grid gap-2">
                  {selectedSale.payments.map((payment) => (
                    <div key={payment.salePaymentId} className="rounded-lg border border-border/70 bg-background/70 p-3">
                      <p className="text-sm font-medium">{translatePaymentMethod(payment.paymentMethod)} - {formatCurrency(payment.amount)}</p>
                      <p className="text-xs text-muted-foreground">
                        {payment.providerName ?? "Sin proveedor"} {payment.reference ? `| Ref. ${payment.reference}` : ""}
                      </p>
                    </div>
                  ))}
                </div>
                <p className="text-xs text-muted-foreground">
                  {formatDate(selectedSale.createdAt)}
                  {selectedSale.dueDate ? ` | Vence ${formatPlainDate(selectedSale.dueDate)}` : ""}
                </p>
                {canEdit && selectedSale.status !== "reversed" && selectedSale.status !== "fully_returned" ? (
                  <PosPartialReturnForm
                    sale={selectedSale}
                    cashSessionId={currentSession?.cashSessionId}
                    canEdit={canEdit}
                  />
                ) : null}
                {selectedSale.returns.length > 0 ? (
                  <div className="grid gap-2">
                    {selectedSale.returns.map((saleReturn) => (
                      <div key={saleReturn.saleReturnId} className="rounded-lg border border-border/70 bg-background/70 p-3">
                        <p className="text-sm font-medium">
                          Devolucion {formatCurrency(saleReturn.returnTotalAmount)}
                        </p>
                        <p className="text-xs text-muted-foreground">
                          Caja {formatCurrency(saleReturn.refundedPaidAmount)} | Cuenta {formatCurrency(saleReturn.creditedAccountAmount)}
                        </p>
                        <p className="text-xs text-muted-foreground">{formatDate(saleReturn.createdAt)}</p>
                      </div>
                    ))}
                  </div>
                ) : null}
              </CardContent>
            </Card>
          ) : null}

          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Ventas recientes</CardTitle>
              <CardDescription>
                Ultimos tickets internos confirmados en el mostrador.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3">
              {sales.items.length === 0 ? (
                <Alert>
                  <AlertTitle>Sin ventas</AlertTitle>
                  <AlertDescription>Todavia no hay ventas confirmadas en este entorno.</AlertDescription>
                </Alert>
              ) : (
                sales.items.map((sale) => (
                  <div
                    key={sale.saleId}
                    className="rounded-lg border border-border/70 bg-background/70 p-4"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="text-sm font-medium">Ticket #{sale.ticketNumber}</p>
                      <div className="flex items-center gap-2">
                        <Badge variant={sale.status === "reversed" ? "secondary" : "outline"}>
                          {translateSaleStatus(sale.status)}
                        </Badge>
                        <Badge variant="outline">{formatCurrency(sale.totalAmount)}</Badge>
                      </div>
                    </div>
                    <p className="mt-2 text-xs text-muted-foreground">
                      {sale.itemCount} item(s) | {sale.customerName} | {translateSaleChannel(sale.saleChannel)}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {formatDate(sale.createdAt)}
                      {sale.dueDate ? ` | Vence ${formatPlainDate(sale.dueDate)}` : ""}
                    </p>
                    <a
                      href={buildSaleLink(resolvedSearchParams, sale.saleId)}
                      className="mt-2 inline-block text-xs font-medium text-primary underline-offset-4 hover:underline"
                    >
                      Ver ticket
                    </a>
                    {canEdit && currentSession && sale.status === "confirmed" ? (
                      <div className="mt-3">
                        <PosReverseSaleForm
                          saleId={sale.saleId}
                          ticketNumber={sale.ticketNumber}
                          cashSessionId={currentSession.cashSessionId}
                        />
                      </div>
                    ) : null}
                  </div>
                ))
              )}
            </CardContent>
          </Card>
        </aside>
      </div>
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

function firstValue(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat("es-AR", {
    style: "currency",
    currency: "ARS",
  }).format(value);
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("es-AR", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(value));
}

function formatPlainDate(value: string) {
  return new Intl.DateTimeFormat("es-AR", {
    dateStyle: "short",
  }).format(new Date(`${value}T00:00:00`));
}

function buildSaleLink(
  searchParams: Record<string, string | string[] | undefined>,
  saleId: string,
) {
  const params = new URLSearchParams();
  const search = firstValue(searchParams.search);
  const saleSearch = firstValue(searchParams.saleSearch);
  const status = firstValue(searchParams.status);

  if (search) params.set("search", search);
  if (saleSearch) params.set("saleSearch", saleSearch);
  if (status) params.set("status", status);
  params.set("saleId", saleId);

  const queryString = params.toString();
  return `/dashboard/pos${queryString ? `?${queryString}` : ""}`;
}

function translateSaleStatus(value: string) {
  switch (value) {
    case "confirmed":
      return "Confirmada";
    case "reversed":
      return "Revertida";
    case "partially_returned":
      return "Parcialmente devuelta";
    case "fully_returned":
      return "Devuelta por completo";
    default:
      return value;
  }
}

function translateSaleChannel(value: string) {
  switch (value) {
    case "pos_cash":
      return "POS efectivo";
    case "pos_account":
      return "Cuenta corriente";
    case "pos_checkout":
      return "Checkout mixto";
    default:
      return value;
  }
}

function translatePaymentMethod(value: string) {
  switch (value) {
    case "cash":
      return "Efectivo";
    case "transfer":
      return "Transferencia";
    case "qr":
      return "QR";
    case "card":
      return "Tarjeta";
    case "account":
      return "Cuenta corriente";
    default:
      return value;
  }
}
