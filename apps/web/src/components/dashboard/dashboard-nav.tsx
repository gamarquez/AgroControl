"use client";

import {
  Boxes,
  ChevronDown,
  CircleDollarSign,
  LayoutDashboard,
  PackageSearch,
  Settings,
  ShoppingCart,
  Tags,
  Users,
  Warehouse,
} from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";

import { LogoutForm } from "@/components/auth/logout-form";
import { cn } from "@/lib/utils";

const primaryLinks = [
  { href: "/dashboard", label: "Resumen", icon: LayoutDashboard, exact: true },
  { href: "/dashboard/pos", label: "Nueva venta", icon: ShoppingCart },
  { href: "/dashboard/catalog", label: "Catálogo", icon: PackageSearch, exact: true },
  { href: "/dashboard/stock", label: "Stock", icon: Boxes },
  { href: "/dashboard/cash", label: "Caja", icon: CircleDollarSign },
  { href: "/dashboard/customers", label: "Clientes", icon: Users },
];

const managementLinks = [
  { href: "/dashboard/catalog/categories", label: "Categorías", icon: Tags },
  { href: "/dashboard/catalog/brands", label: "Marcas", icon: Tags },
  { href: "/dashboard/catalog/price-lists", label: "Listas de precios", icon: CircleDollarSign },
  { href: "/dashboard/users", label: "Usuarios", icon: Users },
  { href: "/dashboard/settings", label: "Comercio", icon: Settings },
  { href: "/dashboard/fiscal", label: "Configuración fiscal", icon: Settings },
];

export function DashboardNav({
  displayName,
  email,
}: {
  displayName: string;
  email: string;
}) {
  const pathname = usePathname();

  return (
    <aside className="border-b border-border bg-card lg:fixed lg:inset-y-0 lg:left-0 lg:z-30 lg:flex lg:w-64 lg:flex-col lg:border-b-0 lg:border-r">
      <div className="flex h-16 items-center justify-between px-4 lg:h-auto lg:px-6 lg:py-7">
        <Link href="/dashboard" className="flex items-center gap-3" aria-label="Ir al resumen">
          <span className="grid size-9 place-items-center rounded-lg bg-primary text-primary-foreground">
            <Warehouse className="size-4" aria-hidden="true" />
          </span>
          <span>
            <span className="block text-sm font-semibold tracking-tight">AgroControl</span>
            <span className="block text-[11px] text-muted-foreground">Gestión de mostrador</span>
          </span>
        </Link>
        <div className="flex items-center gap-2 lg:hidden">
          <details className="group relative">
            <summary className="flex size-10 cursor-pointer list-none items-center justify-center rounded-lg border bg-background text-muted-foreground" aria-label="Abrir menú de gestión">
              <Settings className="size-4" aria-hidden="true" />
            </summary>
            <div className="absolute right-0 top-12 z-40 w-56 space-y-1 rounded-xl border bg-card p-2 shadow-lg">
              {managementLinks.map((link) => (
                <Link key={link.href} href={link.href} className="block rounded-md px-3 py-2 text-sm text-muted-foreground hover:bg-muted hover:text-foreground">
                  {link.label}
                </Link>
              ))}
            </div>
          </details>
          <LogoutForm />
        </div>
      </div>

      <nav className="flex gap-1 overflow-x-auto px-3 pb-3 lg:block lg:flex-1 lg:space-y-1 lg:overflow-visible lg:px-3 lg:pb-0" aria-label="Operaciones principales">
        {primaryLinks.map((link) => {
          const active = link.exact ? pathname === link.href : pathname.startsWith(link.href);
          const Icon = link.icon;
          return (
            <Link
              key={link.href}
              href={link.href}
              className={cn(
                "flex shrink-0 items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors",
                active
                  ? "bg-primary text-primary-foreground"
                  : "text-muted-foreground hover:bg-muted hover:text-foreground",
              )}
            >
              <Icon className="size-4" aria-hidden="true" />
              {link.label}
            </Link>
          );
        })}

        <details className="group hidden pt-2 lg:block" open={managementLinks.some((link) => pathname.startsWith(link.href))}>
          <summary className="flex cursor-pointer list-none items-center justify-between rounded-lg px-3 py-2.5 text-sm font-medium text-muted-foreground hover:bg-muted hover:text-foreground">
            <span className="flex items-center gap-3"><Settings className="size-4" /> Gestión</span>
            <ChevronDown className="size-4 transition-transform group-open:rotate-180" />
          </summary>
          <div className="mt-1 space-y-1 border-l border-border pl-3 ml-5">
            {managementLinks.map((link) => {
              const active = pathname.startsWith(link.href);
              return (
                <Link
                  key={link.href}
                  href={link.href}
                  className={cn(
                    "block rounded-md px-3 py-2 text-sm transition-colors",
                    active ? "bg-muted font-medium text-foreground" : "text-muted-foreground hover:text-foreground",
                  )}
                >
                  {link.label}
                </Link>
              );
            })}
          </div>
        </details>
      </nav>

      <div className="hidden border-t border-border p-4 lg:block">
        <p className="truncate text-sm font-medium">{displayName}</p>
        <p className="mb-3 truncate text-xs text-muted-foreground">{email}</p>
        <LogoutForm />
      </div>
    </aside>
  );
}
