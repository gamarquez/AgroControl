import Link from "next/link";

import { LogoutForm } from "@/components/auth/logout-form";
import { Badge } from "@/components/ui/badge";
import { requireSession } from "@/lib/auth/session";

export default async function DashboardLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const session = await requireSession();
  const isAdministrator = session.roles.some((role) => role.code === "administrator");

  return (
    <div className="min-h-screen bg-background">
      <header className="border-b border-border/80 bg-card/90 backdrop-blur">
        <div className="mx-auto flex max-w-6xl flex-col gap-4 px-4 py-5 sm:px-6 lg:flex-row lg:items-center lg:justify-between lg:px-8">
          <div className="space-y-2">
            <div className="flex flex-wrap items-center gap-2">
              <Badge>Autenticado</Badge>
              <Badge variant="outline">Piloto sin ARCA</Badge>
              <span className="text-sm text-muted-foreground">{session.email}</span>
            </div>
            <div>
              <h1 className="text-2xl font-semibold tracking-tight">{session.displayName}</h1>
              <p className="text-sm text-muted-foreground">
                Organizacion activa: {session.organizationId}
              </p>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-3">
            <nav className="flex flex-wrap items-center gap-2 text-sm">
              <Link className="rounded-md px-3 py-2 hover:bg-accent" href="/dashboard">
                Resumen
              </Link>
              <Link className="rounded-md px-3 py-2 hover:bg-accent" href="/dashboard/catalog">
                Catalogo
              </Link>
              <Link className="rounded-md px-3 py-2 hover:bg-accent" href="/dashboard/catalog/categories">
                Categorias
              </Link>
              <Link className="rounded-md px-3 py-2 hover:bg-accent" href="/dashboard/catalog/brands">
                Marcas
              </Link>
              <Link className="rounded-md px-3 py-2 hover:bg-accent" href="/dashboard/catalog/price-lists">
                Precios
              </Link>
              <Link className="rounded-md px-3 py-2 hover:bg-accent" href="/dashboard/stock">
                Stock
              </Link>
              <Link className="rounded-md px-3 py-2 hover:bg-accent" href="/dashboard/cash">
                Caja
              </Link>
              <Link className="rounded-md px-3 py-2 hover:bg-accent" href="/dashboard/pos">
                POS
              </Link>
              <Link className="rounded-md px-3 py-2 hover:bg-accent" href="/dashboard/customers">
                Clientes
              </Link>
              <Link className="rounded-md px-3 py-2 hover:bg-accent" href="/dashboard/users">
                Usuarios
              </Link>
              <Link className="rounded-md px-3 py-2 hover:bg-accent" href="/dashboard/settings">
                Comercio
              </Link>
              <Link className="rounded-md px-3 py-2 hover:bg-accent" href="/dashboard/fiscal">
                Fiscal opcional
              </Link>
            </nav>
            {isAdministrator ? (
              <Badge variant="outline">Administrador</Badge>
            ) : (
              <Badge variant="outline">Acceso limitado</Badge>
            )}
            <LogoutForm />
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">{children}</main>
    </div>
  );
}
