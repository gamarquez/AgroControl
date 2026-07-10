import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { requireSession } from "@/lib/auth/session";

export default async function DashboardPage() {
  const session = await requireSession();

  return (
    <div className="grid gap-6">
      <Card className="border-border/80 bg-card/95">
        <CardHeader className="space-y-3">
          <div className="flex flex-wrap gap-2">
            <Badge className="w-fit">Sesion actual</Badge>
            <Badge variant="outline" className="w-fit">Piloto operativo sin ARCA</Badge>
          </div>
          <div className="space-y-2">
            <CardTitle className="text-3xl">Base operativa lista para salir a prueba</CardTitle>
            <CardDescription className="max-w-3xl text-base leading-7">
              La app ya permite operar catalogo, stock, caja, POS y clientes con cuenta corriente
              sobre un piloto interno sin depender de ARCA para el circuito diario.
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-3">
          <SummaryItem label="Usuario" value={session.displayName} />
          <SummaryItem label="Email" value={session.email} />
          <SummaryItem
            label="Roles efectivos"
            value={session.roles.map((role) => role.name).join(", ") || "Sin roles"}
          />
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">Catalogo</CardTitle>
            <CardDescription>
              Productos, categorias, marcas y precios base listos para enlazar stock.
            </CardDescription>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">Stock</CardTitle>
            <CardDescription>
              Saldos por producto, puntos de reposicion y movimientos auditables para una sola locacion.
            </CardDescription>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">POS y pagos mixtos</CardTitle>
            <CardDescription>
              Venta mostrador con ticket interno, salida de stock y cobro en efectivo, transferencia, QR, tarjeta o cuenta corriente.
            </CardDescription>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">Clientes y cuenta corriente</CardTitle>
            <CardDescription>
              Padron comercial, limite de credito, ventas a cuenta y cobranza simple con trazabilidad.
            </CardDescription>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">ARCA y fiscalizacion</CardTitle>
            <CardDescription>
              Slice opcional y todavia no requerido para esta salida a prueba.
            </CardDescription>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">Usuarios</CardTitle>
            <CardDescription>
              Alta, activacion, bloqueo y asignacion de roles dentro de la organizacion.
            </CardDescription>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">Configuracion del comercio</CardTitle>
            <CardDescription>
              Nombre legal, nombre comercial, zona horaria, moneda y datos base para la UI.
            </CardDescription>
          </CardHeader>
        </Card>
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
