import { ArrowRight, Boxes, CircleDollarSign, PackageSearch, ShoppingCart, Users } from "lucide-react";
import Link from "next/link";

import { requireSession } from "@/lib/auth/session";

const operations = [
  { href: "/dashboard/pos", title: "Nueva venta", description: "Abrir el punto de venta y cobrar.", icon: ShoppingCart, primary: true },
  { href: "/dashboard/catalog", title: "Catálogo", description: "Buscar, crear y actualizar productos.", icon: PackageSearch },
  { href: "/dashboard/stock", title: "Stock", description: "Consultar saldos y registrar movimientos.", icon: Boxes },
  { href: "/dashboard/cash", title: "Caja", description: "Abrir, controlar o cerrar la caja.", icon: CircleDollarSign },
  { href: "/dashboard/customers", title: "Clientes", description: "Cuentas corrientes y cobranzas.", icon: Users },
];

export default async function DashboardPage() {
  const session = await requireSession();
  const firstName = session.displayName.split(" ")[0];

  return (
    <div className="space-y-8">
      <header className="space-y-2">
        <p className="text-sm font-medium text-primary">Panel operativo</p>
        <h1 className="text-3xl font-semibold tracking-tight sm:text-4xl">Buen día, {firstName}</h1>
        <p className="max-w-2xl text-sm text-muted-foreground sm:text-base">
          Elegí una operación para comenzar. Las tareas más frecuentes están siempre a un paso.
        </p>
      </header>

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3" aria-label="Accesos rápidos">
        {operations.map((operation) => {
          const Icon = operation.icon;
          return (
            <Link
              key={operation.href}
              href={operation.href}
              className={`group flex min-h-36 flex-col justify-between rounded-xl border p-5 transition-all hover:-translate-y-0.5 hover:border-primary/40 ${
                operation.primary ? "border-primary bg-primary text-primary-foreground" : "bg-card"
              }`}
            >
              <div className="flex items-start justify-between">
                <Icon className="size-5" aria-hidden="true" />
                <ArrowRight className="size-4 transition-transform group-hover:translate-x-1" aria-hidden="true" />
              </div>
              <div>
                <h2 className="font-semibold">{operation.title}</h2>
                <p className={`mt-1 text-sm ${operation.primary ? "text-primary-foreground/75" : "text-muted-foreground"}`}>
                  {operation.description}
                </p>
              </div>
            </Link>
          );
        })}
      </section>

      <footer className="border-t border-border pt-5 text-xs text-muted-foreground">
        Sesión iniciada como {session.email}
      </footer>
    </div>
  );
}
