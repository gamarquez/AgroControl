import { FiscalQueueDocumentForm } from "@/components/fiscal/fiscal-queue-document-form";
import { FiscalProbeForm } from "@/components/fiscal/fiscal-probe-form";
import { FiscalSettingsForm } from "@/components/fiscal/fiscal-settings-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ApiError } from "@/lib/api/server";
import { getFiscalSettings, listFiscalDocuments, listSales, requireSession } from "@/lib/auth/session";

export default async function FiscalPage() {
  const session = await requireSession();
  const canEdit = session.roles.some((role) => role.code === "administrator" || role.code === "manager");

  let settings = null;
  let documents = null;
  let sales = null;
  let errorState: { title: string; description: string } | null = null;

  try {
    [settings, documents, sales] = await Promise.all([
      getFiscalSettings(),
      listFiscalDocuments({ limit: 20 }),
      listSales({ pageSize: 12 }),
    ]);
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      errorState = {
        title: "Acceso restringido",
        description: "No cuentas con permisos para consultar el modulo fiscal.",
      };
    } else {
      errorState = {
        title: "No pudimos cargar ARCA",
        description: error instanceof Error ? error.message : "Ocurrio un error inesperado.",
      };
    }
  }

  if (errorState || !settings || !documents || !sales) {
    return (
      <Alert variant="destructive">
        <AlertTitle>{errorState?.title ?? "No pudimos cargar ARCA"}</AlertTitle>
        <AlertDescription>{errorState?.description ?? "Ocurrio un error inesperado."}</AlertDescription>
      </Alert>
    );
  }

  const documentsBySaleId = new Map(documents.items.map((document) => [document.saleId, document]));

  return (
    <div className="grid gap-6">
      <Card>
        <CardHeader className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <Badge className="w-fit">ARCA</Badge>
            {canEdit ? <Badge variant="outline">Configuracion operativa</Badge> : <Badge variant="outline">Solo lectura</Badge>}
            <Badge variant={settings.isEnabled ? "outline" : "secondary"}>
              {settings.isEnabled ? "Integracion habilitada" : "Integracion pausada"}
            </Badge>
          </div>
          <div className="space-y-2">
            <CardTitle className="text-2xl">Preparacion fiscal y cola de comprobantes</CardTitle>
            <CardDescription>
              Este slice deja configuracion persistente, auditoria, trazabilidad y una prueba tecnica contra FEDummy.
              La autorizacion final de CAE sigue diferida hasta cerrar la parametrizacion completa de WSAA y WSFE.
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent className="grid gap-4">
          <div className="grid gap-4 md:grid-cols-4">
            <SummaryItem label="Proveedor" value={settings.provider} />
            <SummaryItem label="Entorno" value={translateEnvironment(settings.environment)} />
            <SummaryItem label="Punto de venta" value={String(settings.pointOfSale)} />
            <SummaryItem label="Documentos registrados" value={String(documents.total)} />
          </div>

          {!canEdit ? (
            <Alert>
              <AlertTitle>Vista de consulta</AlertTitle>
              <AlertDescription>
                Tu rol puede revisar configuracion y documentos preparados, pero no modificar settings ni ejecutar pruebas.
              </AlertDescription>
            </Alert>
          ) : null}
        </CardContent>
      </Card>

      <div className="grid gap-6 lg:grid-cols-[1.05fr_0.95fr]">
        <section className="grid gap-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Configuracion fiscal</CardTitle>
              <CardDescription>
                Define CUIT emisor, entorno, punto de venta y el servicio operativo para la integracion.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <FiscalSettingsForm settings={settings} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Prueba tecnica</CardTitle>
              <CardDescription>
                Verifica carga del certificado configurado y conectividad SOAP con el endpoint oficial `FEDummy`.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4">
              <div className="grid gap-4 md:grid-cols-3">
                <SummaryItem label="Servicio" value={settings.serviceName} />
                <SummaryItem label="Comprobante default" value={settings.defaultDocumentType} />
                <SummaryItem label="Ultima actualizacion" value={formatDate(settings.updatedAt)} />
              </div>
              <FiscalProbeForm canEdit={canEdit} />
            </CardContent>
          </Card>
        </section>

        <aside className="grid gap-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Cola de comprobantes</CardTitle>
              <CardDescription>
                Documentos preparados para fiscalizacion posterior, con estado, error operativo y ticket asociado.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3">
              {documents.items.length === 0 ? (
                <Alert>
                  <AlertTitle>Sin comprobantes preparados</AlertTitle>
                  <AlertDescription>Todavia no se registraron documentos fiscales en cola.</AlertDescription>
                </Alert>
              ) : (
                documents.items.map((document) => (
                  <div
                    key={document.fiscalDocumentId}
                    className="rounded-lg border border-border/70 bg-background/70 p-4"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="text-sm font-medium">Ticket #{document.ticketNumber}</p>
                      <div className="flex items-center gap-2">
                        <Badge variant="outline">{translateDocumentKind(document.documentKind)}</Badge>
                        <Badge variant={document.status === "pending" ? "secondary" : "outline"}>
                          {translateDocumentStatus(document.status)}
                        </Badge>
                      </div>
                    </div>
                    <p className="mt-2 text-sm text-muted-foreground">{document.customerName}</p>
                    <p className="mt-2 text-xs text-muted-foreground">
                      {formatCurrency(document.totalAmount)} | PV {document.pointOfSale} | {translateEnvironment(document.environment)}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">{document.lastError ?? "Sin observaciones"}</p>
                    <p className="mt-1 text-xs text-muted-foreground">{formatDate(document.updatedAt)}</p>
                  </div>
                ))
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Ventas listas para preparar</CardTitle>
              <CardDescription>
                Toma tickets confirmados del POS para dejar el comprobante en cola antes de la fiscalizacion final.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3">
              {sales.items.length === 0 ? (
                <Alert>
                  <AlertTitle>Sin ventas recientes</AlertTitle>
                  <AlertDescription>No encontramos ventas confirmadas en este entorno.</AlertDescription>
                </Alert>
              ) : (
                sales.items.map((sale) => {
                  const existingDocument = documentsBySaleId.get(sale.saleId);
                  const isPendingCandidate = sale.status === "confirmed" && !existingDocument;

                  return (
                    <div
                      key={sale.saleId}
                      className="rounded-lg border border-border/70 bg-background/70 p-4"
                    >
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <p className="text-sm font-medium">Ticket #{sale.ticketNumber}</p>
                        <div className="flex items-center gap-2">
                          <Badge variant="outline">{formatCurrency(sale.totalAmount)}</Badge>
                          <Badge variant={sale.status === "confirmed" ? "outline" : "secondary"}>
                            {translateSaleStatus(sale.status)}
                          </Badge>
                        </div>
                      </div>
                      <p className="mt-2 text-xs text-muted-foreground">
                        {sale.itemCount} item(s) | {formatDate(sale.createdAt)}
                      </p>

                      {existingDocument ? (
                        <Alert className="mt-3">
                          <AlertTitle>Comprobante ya preparado</AlertTitle>
                          <AlertDescription>
                            Este ticket ya tiene un documento fiscal en estado {translateDocumentStatus(existingDocument.status).toLowerCase()}.
                          </AlertDescription>
                        </Alert>
                      ) : null}

                      {isPendingCandidate ? (
                        <div className="mt-3">
                          <FiscalQueueDocumentForm
                            saleId={sale.saleId}
                            ticketNumber={sale.ticketNumber}
                            canEdit={canEdit}
                          />
                        </div>
                      ) : null}
                    </div>
                  );
                })
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

function translateEnvironment(value: string) {
  switch (value) {
    case "homologation":
      return "Homologacion";
    case "production":
      return "Produccion";
    default:
      return value;
  }
}

function translateDocumentKind(value: string) {
  switch (value) {
    case "invoice":
      return "Factura";
    case "credit_note":
      return "Nota de credito";
    default:
      return value;
  }
}

function translateDocumentStatus(value: string) {
  switch (value) {
    case "pending":
      return "Pendiente";
    case "authorized":
      return "Autorizado";
    case "rejected":
      return "Rechazado";
    default:
      return value;
  }
}

function translateSaleStatus(value: string) {
  switch (value) {
    case "confirmed":
      return "Confirmada";
    case "reversed":
      return "Revertida";
    default:
      return value;
  }
}
