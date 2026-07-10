import { CashCloseSessionForm } from "@/components/cash/cash-close-session-form";
import { CashMovementForm } from "@/components/cash/cash-movement-form";
import { CashOpenSessionForm } from "@/components/cash/cash-open-session-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ApiError } from "@/lib/api/server";
import { getCashOverview, listCashMovements, requireSession } from "@/lib/auth/session";

export default async function CashPage() {
  const session = await requireSession();
  const isEditor = session.roles.some((role) =>
    role.code === "administrator" || role.code === "manager" || role.code === "cashier",
  );

  let overview = null;
  let movements = null;
  let errorState: { title: string; description: string } | null = null;

  try {
    [overview, movements] = await Promise.all([getCashOverview(), listCashMovements({ pageSize: 30 })]);
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      errorState = {
        title: "Acceso restringido",
        description: "No cuentas con permisos para consultar caja.",
      };
    } else {
      errorState = {
        title: "No pudimos cargar caja",
        description: error instanceof Error ? error.message : "Ocurrio un error inesperado.",
      };
    }
  }

  if (errorState || !overview || !movements) {
    return (
      <Alert variant="destructive">
        <AlertTitle>{errorState?.title ?? "No pudimos cargar caja"}</AlertTitle>
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
            <Badge className="w-fit">Caja</Badge>
            {!isEditor ? <Badge variant="outline">Lectura</Badge> : null}
            {currentSession ? <Badge variant="outline">Sesion abierta</Badge> : <Badge variant="secondary">Sin sesion</Badge>}
          </div>
          <div className="space-y-2">
            <CardTitle className="text-2xl">Apertura, movimientos y cierre</CardTitle>
            <CardDescription>
              Operacion de caja para una sola locacion, con libro diario simple y trazabilidad operativa.
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent className="grid gap-4">
          <div className="grid gap-4 md:grid-cols-4">
            <SummaryItem label="Caja" value={overview.cashRegister.name} />
            <SummaryItem label="Codigo" value={overview.cashRegister.code} />
            <SummaryItem
              label="Saldo actual"
              value={currentSession ? formatCurrency(currentSession.currentBalance) : "Sin sesion abierta"}
            />
            <SummaryItem
              label="Estado"
              value={currentSession ? "Sesion abierta" : "Pendiente de apertura"}
            />
          </div>

          {!isEditor ? (
            <Alert>
              <AlertTitle>Vista de consulta</AlertTitle>
              <AlertDescription>
                Tu rol puede consultar caja, pero no abrir, cerrar ni registrar movimientos manuales.
              </AlertDescription>
            </Alert>
          ) : null}
        </CardContent>
      </Card>

      <div className="grid gap-6 lg:grid-cols-[0.95fr_1.05fr]">
        <section className="grid gap-4">
          {!currentSession ? (
            isEditor ? (
              <CashOpenSessionForm
                cashRegisterCode={overview.cashRegister.code}
                cashRegisterName={overview.cashRegister.name}
              />
            ) : (
              <Card>
                <CardHeader>
                  <CardTitle className="text-lg">Sin sesion activa</CardTitle>
                  <CardDescription>La caja todavia no fue abierta para este turno.</CardDescription>
                </CardHeader>
              </Card>
            )
          ) : (
            <>
              <Card>
                <CardHeader>
                  <CardTitle className="text-lg">Sesion actual</CardTitle>
                  <CardDescription>
                    Abierta el {formatDate(currentSession.openedAt)} con saldo inicial de{" "}
                    {formatCurrency(currentSession.openingAmount)}.
                  </CardDescription>
                </CardHeader>
                <CardContent className="grid gap-4 md:grid-cols-2">
                  <SummaryItem label="Saldo teorico" value={formatCurrency(currentSession.currentBalance)} />
                  <SummaryItem
                    label="Notas de apertura"
                    value={currentSession.openingNotes ?? "Sin observaciones"}
                  />
                </CardContent>
              </Card>

              {isEditor ? <CashMovementForm cashSessionId={currentSession.cashSessionId} /> : null}
              {isEditor ? (
                <CashCloseSessionForm
                  cashSessionId={currentSession.cashSessionId}
                  currentBalance={currentSession.currentBalance}
                />
              ) : null}
            </>
          )}
        </section>

        <aside className="grid gap-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Libro diario reciente</CardTitle>
              <CardDescription>
                Ultimos movimientos registrados para la caja principal.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3">
              {movements.items.length === 0 ? (
                <Alert>
                  <AlertTitle>Sin movimientos</AlertTitle>
                  <AlertDescription>Todavia no se registraron movimientos de caja.</AlertDescription>
                </Alert>
              ) : (
                movements.items.map((movement) => (
                  <div
                    key={movement.cashMovementId}
                    className="rounded-lg border border-border/70 bg-background/70 p-4"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="text-sm font-medium">{translateMovementType(movement.movementType)}</p>
                      <Badge variant={movement.signedAmount >= 0 ? "outline" : "secondary"}>
                        {movement.signedAmount >= 0 ? "+" : ""}
                        {formatCurrency(movement.signedAmount)}
                      </Badge>
                    </div>
                    <p className="mt-2 text-sm text-muted-foreground">{movement.concept}</p>
                    <p className="mt-2 text-xs text-muted-foreground">
                      Categoria: {movement.categoryCode} | Medio: {translatePaymentMethod(movement.paymentMethod)}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Saldo: {formatCurrency(movement.resultingBalance)}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {formatDate(movement.createdAt)}
                      {movement.referenceDocument ? ` | Ref. ${movement.referenceDocument}` : ""}
                    </p>
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

function formatDate(value: string) {
  return new Intl.DateTimeFormat("es-AR", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(value));
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat("es-AR", {
    style: "currency",
    currency: "ARS",
  }).format(value);
}

function translateMovementType(value: string) {
  switch (value) {
    case "opening":
      return "Apertura";
    case "cash_in":
      return "Ingreso manual";
    case "cash_out":
      return "Egreso manual";
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
