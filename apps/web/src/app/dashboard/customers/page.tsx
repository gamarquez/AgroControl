import { CustomerCreateForm } from "@/components/customers/customer-create-form";
import { CustomerCreditNoteForm } from "@/components/customers/customer-credit-note-form";
import { CustomerPaymentForm } from "@/components/customers/customer-payment-form";
import { CustomerUpdateForm } from "@/components/customers/customer-update-form";
import { CustomerAccountSaleForm } from "@/components/customers/customer-account-sale-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ApiError } from "@/lib/api/server";
import {
  getCashOverview,
  getCustomerAccountStatement,
  getCustomer,
  listCustomers,
  listPosProducts,
  requireSession,
} from "@/lib/auth/session";

export default async function CustomersPage({
  searchParams,
}: {
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
}) {
  const session = await requireSession();
  const resolvedSearchParams = (await searchParams) ?? {};
  const canManageCustomers = session.roles.some((role) => role.code === "administrator" || role.code === "manager");
  const canOperateAccount = canManageCustomers || session.roles.some((role) => role.code === "seller" || role.code === "cashier");
  const canRegisterCreditNote = canManageCustomers;

  let customers = null;
  let selectedCustomer = null;
  let statement = null;
  let overview = null;
  let products = null;
  let errorState: { title: string; description: string } | null = null;

  try {
    customers = await listCustomers({
      search: firstValue(resolvedSearchParams.search),
      isActive: firstValue(resolvedSearchParams.isActive),
      pageSize: 30,
    });

    const selectedCustomerId =
      firstValue(resolvedSearchParams.customerId) ?? customers.items[0]?.customerId;

    if (selectedCustomerId) {
      [selectedCustomer, statement] = await Promise.all([
        getCustomer(selectedCustomerId),
        getCustomerAccountStatement(selectedCustomerId, { limit: 50 }),
      ]);
    }
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      errorState = {
        title: "Acceso restringido",
        description: "No cuentas con permisos para consultar clientes o cuenta corriente.",
      };
    } else {
      errorState = {
        title: "No pudimos cargar clientes",
        description: error instanceof Error ? error.message : "Ocurrio un error inesperado.",
      };
    }
  }

  if (!errorState && canOperateAccount) {
    try {
      [overview, products] = await Promise.all([
        getCashOverview(),
        listPosProducts({ pageSize: 24 }),
      ]);
    } catch {
      overview = null;
      products = null;
    }
  }

  if (errorState || !customers) {
    return (
      <Alert variant="destructive">
        <AlertTitle>{errorState?.title ?? "No pudimos cargar clientes"}</AlertTitle>
        <AlertDescription>{errorState?.description ?? "Ocurrio un error inesperado."}</AlertDescription>
      </Alert>
    );
  }

  const currentSession = overview?.currentSession ?? null;
  const availableCredit = selectedCustomer
    ? selectedCustomer.creditLimitAmount - selectedCustomer.currentBalance
    : 0;
  const favorBalance = selectedCustomer && selectedCustomer.currentBalance < 0
    ? Math.abs(selectedCustomer.currentBalance)
    : 0;

  return (
    <div className="grid gap-6">
      <Card>
        <CardHeader className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <Badge className="w-fit">Clientes</Badge>
            {!canManageCustomers ? <Badge variant="outline">Operacion asistida</Badge> : null}
            {currentSession ? <Badge variant="outline">Caja disponible</Badge> : <Badge variant="secondary">Cobranza sin caja</Badge>}
          </div>
          <div className="space-y-2">
            <CardTitle className="text-2xl">Padron comercial y cuenta corriente</CardTitle>
            <CardDescription>
              Alta de clientes, limite de credito, ventas a cuenta y cobranza simple para una sola locacion.
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent className="grid gap-4">
          <form method="get" className="grid gap-4 md:grid-cols-4">
            <input
              name="search"
              defaultValue={firstValue(resolvedSearchParams.search) ?? ""}
              placeholder="Nombre, CUIT, telefono o email"
              className="h-11 rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
            />
            <select
              name="isActive"
              defaultValue={firstValue(resolvedSearchParams.isActive) ?? ""}
              className="h-11 rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
            >
              <option value="">Todos los estados</option>
              <option value="true">Activos</option>
              <option value="false">Inactivos</option>
            </select>
            <input type="hidden" name="customerId" value={firstValue(resolvedSearchParams.customerId) ?? ""} />
            <button className="h-11 rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground md:col-span-4 md:w-fit">
              Aplicar filtros
            </button>
          </form>

          <div className="grid gap-4 md:grid-cols-4">
            <SummaryItem label="Clientes listados" value={String(customers.total)} />
            <SummaryItem label="Clientes activos" value={String(customers.items.filter((item) => item.isActive).length)} />
            <SummaryItem label="Caja" value={currentSession ? "Abierta" : "No disponible"} />
            <SummaryItem
              label="Venta a cuenta"
              value={canOperateAccount ? "Habilitada por rol" : "Solo consulta"}
            />
          </div>

          {!canManageCustomers ? (
            <Alert>
              <AlertTitle>Padron en modo restringido</AlertTitle>
              <AlertDescription>
                Tu rol puede operar cuenta corriente si corresponde, pero no administrar el maestro completo de clientes.
              </AlertDescription>
            </Alert>
          ) : null}

          {canManageCustomers ? (
            <Card className="border-dashed">
              <CardHeader>
                <CardTitle className="text-lg">Alta rapida de cliente</CardTitle>
                <CardDescription>
                  Crea clientes con limite de credito base y datos minimos de contacto.
                </CardDescription>
              </CardHeader>
              <CardContent>
                <CustomerCreateForm />
              </CardContent>
            </Card>
          ) : null}
        </CardContent>
      </Card>

      <div className="grid gap-6 lg:grid-cols-[0.8fr_1.2fr]">
        <aside className="grid gap-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Clientes</CardTitle>
              <CardDescription>Selecciona un cliente para revisar saldo, movimientos y operaciones.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3">
              {customers.items.length === 0 ? (
                <Alert>
                  <AlertTitle>Sin clientes</AlertTitle>
                  <AlertDescription>No encontramos clientes con esos filtros.</AlertDescription>
                </Alert>
              ) : (
                customers.items.map((customer) => (
                  <a
                    key={customer.customerId}
                    href={buildCustomerLink(resolvedSearchParams, customer.customerId)}
                    className={`rounded-lg border p-4 transition hover:border-primary ${
                      selectedCustomer?.customerId === customer.customerId
                        ? "border-primary bg-primary/5"
                        : "border-border/70 bg-background/70"
                    }`}
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="text-sm font-medium">{customer.displayName}</p>
                      <Badge variant={customer.isActive ? "outline" : "secondary"}>
                        {customer.isActive ? "Activo" : "Inactivo"}
                      </Badge>
                    </div>
                    <p className="mt-2 text-xs text-muted-foreground">
                      {customer.taxId ?? "Sin documento"} {customer.phone ? `| ${customer.phone}` : ""}
                    </p>
                    <p className="mt-2 text-xs text-muted-foreground">
                      Saldo {formatCurrency(customer.currentBalance)} | Limite {formatCurrency(customer.creditLimitAmount)}
                    </p>
                  </a>
                ))
              )}
            </CardContent>
          </Card>
        </aside>

        <section className="grid gap-4">
          {!selectedCustomer || !statement ? (
            <Card>
              <CardHeader>
                <CardTitle className="text-lg">Selecciona un cliente</CardTitle>
                <CardDescription>
                  Elige un registro del padron para ver detalle operativo y cuenta corriente.
                </CardDescription>
              </CardHeader>
            </Card>
          ) : (
            <>
              <Card>
                <CardHeader className="space-y-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <CardTitle className="text-xl">{selectedCustomer.displayName}</CardTitle>
                    <Badge variant={selectedCustomer.isActive ? "outline" : "secondary"}>
                      {selectedCustomer.isActive ? "Activo" : "Inactivo"}
                    </Badge>
                  </div>
                  <CardDescription>
                    {selectedCustomer.taxId ?? "Sin CUIT/DNI"} {selectedCustomer.email ? `| ${selectedCustomer.email}` : ""}
                  </CardDescription>
                </CardHeader>
                <CardContent className="grid gap-4">
                  <div className="grid gap-4 md:grid-cols-5">
                    <SummaryItem label="Saldo actual" value={formatCurrency(selectedCustomer.currentBalance)} />
                    <SummaryItem label="Saldo a favor" value={formatCurrency(favorBalance)} />
                    <SummaryItem label="Saldo vencido" value={formatCurrency(selectedCustomer.overdueBalance)} />
                    <SummaryItem label="Limite" value={formatCurrency(selectedCustomer.creditLimitAmount)} />
                    <SummaryItem label="Disponible" value={formatCurrency(availableCredit)} />
                    <SummaryItem
                      label="Proximo vencimiento"
                      value={selectedCustomer.nextDueDate ? formatPlainDate(selectedCustomer.nextDueDate) : "Sin vencimientos"}
                    />
                  </div>
                  <CustomerUpdateForm customer={selectedCustomer} canEdit={canManageCustomers} />
                </CardContent>
              </Card>

              {canOperateAccount ? (
                <div className="grid gap-4 xl:grid-cols-3">
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg">Cobranza</CardTitle>
                      <CardDescription>
                        Registra ingresos sobre cuenta corriente y su impacto en caja.
                      </CardDescription>
                    </CardHeader>
                    <CardContent>
                      {currentSession ? (
                        <CustomerPaymentForm
                          customer={selectedCustomer}
                          cashSessionId={currentSession.cashSessionId}
                          canEdit={selectedCustomer.isActive}
                        />
                      ) : (
                        <Alert>
                          <AlertTitle>Caja requerida</AlertTitle>
                          <AlertDescription>
                            Para registrar cobranzas debes abrir una sesion de caja.
                          </AlertDescription>
                        </Alert>
                      )}
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg">Nota de credito</CardTitle>
                      <CardDescription>
                        Acredita saldo al cliente sin impactar caja y lo aplica sobre debitos abiertos.
                      </CardDescription>
                    </CardHeader>
                    <CardContent>
                      {canRegisterCreditNote ? (
                        <CustomerCreditNoteForm
                          customer={selectedCustomer}
                          canEdit={selectedCustomer.isActive}
                        />
                      ) : (
                        <Alert>
                          <AlertTitle>Permiso requerido</AlertTitle>
                          <AlertDescription>
                            Solo administracion o gerencia puede registrar notas de credito sobre cuenta corriente.
                          </AlertDescription>
                        </Alert>
                      )}
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg">Venta a cuenta</CardTitle>
                      <CardDescription>
                        Debita saldo del cliente usando el catalogo activo del POS.
                      </CardDescription>
                    </CardHeader>
                    <CardContent>
                      {products ? (
                        <CustomerAccountSaleForm
                          customer={selectedCustomer}
                          products={products.items}
                          canEdit={selectedCustomer.isActive}
                        />
                      ) : (
                        <Alert>
                          <AlertTitle>Productos no disponibles</AlertTitle>
                          <AlertDescription>
                            No pudimos cargar el catalogo operativo para registrar la venta a cuenta.
                          </AlertDescription>
                        </Alert>
                      )}
                    </CardContent>
                  </Card>
                </div>
              ) : null}

              <Card>
                <CardHeader>
                  <CardTitle className="text-lg">Movimientos de cuenta corriente</CardTitle>
                  <CardDescription>
                    Debitos por ventas, creditos por cobranzas y saldo resultante en orden descendente.
                  </CardDescription>
                </CardHeader>
                <CardContent className="grid gap-3">
                  {statement.movements.length === 0 ? (
                    <Alert>
                      <AlertTitle>Sin movimientos</AlertTitle>
                      <AlertDescription>Este cliente todavia no tiene movimientos registrados.</AlertDescription>
                    </Alert>
                  ) : (
                    statement.movements.map((movement) => (
                      <div
                        key={movement.customerAccountMovementId}
                        className="rounded-lg border border-border/70 bg-background/70 p-4"
                      >
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <p className="text-sm font-medium">{translateAccountMovement(movement.movementType)}</p>
                          <Badge variant={movement.debitAmount > 0 ? "secondary" : "outline"}>
                            {movement.debitAmount > 0 ? "+" : "-"}
                            {formatCurrency(Math.max(movement.debitAmount, movement.creditAmount))}
                          </Badge>
                        </div>
                        <p className="mt-2 text-sm text-muted-foreground">{movement.concept}</p>
                        <p className="mt-2 text-xs text-muted-foreground">
                          Saldo resultante: {formatCurrency(movement.resultingBalance)} | Abierto: {formatCurrency(movement.openAmount)}
                        </p>
                        <p className="mt-1 text-xs text-muted-foreground">
                          {formatDate(movement.createdAt)}
                          {movement.referenceDocument ? ` | Ref. ${movement.referenceDocument}` : ""}
                          {movement.dueDate ? ` | Vence ${formatPlainDate(movement.dueDate)}` : ""}
                          {movement.isOverdue ? " | Vencido" : ""}
                        </p>
                      </div>
                    ))
                  )}
                </CardContent>
              </Card>
            </>
          )}
        </section>
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

function buildCustomerLink(
  searchParams: Record<string, string | string[] | undefined>,
  customerId: string,
) {
  const params = new URLSearchParams();
  const search = firstValue(searchParams.search);
  const isActive = firstValue(searchParams.isActive);

  if (search) params.set("search", search);
  if (isActive) params.set("isActive", isActive);
  params.set("customerId", customerId);

  const queryString = params.toString();
  return `/dashboard/customers${queryString ? `?${queryString}` : ""}`;
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

function translateAccountMovement(value: string) {
  switch (value) {
    case "sale_debit":
      return "Venta a cuenta";
    case "payment_credit":
      return "Cobranza";
    case "credit_note":
      return "Nota de credito";
    default:
      return value;
  }
}
