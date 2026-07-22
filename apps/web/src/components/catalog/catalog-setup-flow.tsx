import { Check, CircleDollarSign, PackagePlus, Tags } from "lucide-react";

import { createBrandAction } from "@/app/dashboard/catalog/brands/actions";
import { createCategoryAction } from "@/app/dashboard/catalog/categories/actions";
import { CatalogMasterCreateForm } from "@/components/catalog/catalog-master-form";
import { CatalogProductCreateForm } from "@/components/catalog/catalog-product-create-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import type { Brand, PriceList, ProductCategory, UnitOfMeasure } from "@/lib/api/contracts";

export function CatalogSetupFlow({
  categories,
  brands,
  units,
  priceLists,
  productCount,
}: {
  categories: ProductCategory[];
  brands: Brand[];
  units: UnitOfMeasure[];
  priceLists: PriceList[];
  productCount: number;
}) {
  const activeCategories = categories.filter((item) => item.isActive);
  const activeBrands = brands.filter((item) => item.isActive);
  const activeUnits = units.filter((item) => item.isActive);
  const activePriceLists = priceLists.filter((item) => item.isActive);
  const canCreateProduct = activeUnits.length > 0 && activePriceLists.length > 0;

  return (
    <section className="overflow-hidden rounded-xl border bg-card" aria-labelledby="catalog-setup-title">
      <header className="border-b bg-primary/[0.04] px-5 py-5 sm:px-6">
        <p className="text-sm font-medium text-primary">Carga guiada</p>
        <h2 id="catalog-setup-title" className="mt-1 text-xl font-semibold tracking-tight">Preparar un producto para vender</h2>
        <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
          Completá la clasificación y luego registrá el producto con su precio. Cada alta queda disponible en el paso siguiente.
        </p>
        <div className="mt-5 grid gap-2 sm:grid-cols-3" aria-label="Progreso de configuración">
          <ProgressStep number="1" label="Clasificación" complete={activeCategories.length > 0 && activeBrands.length > 0} />
          <ProgressStep number="2" label="Producto" complete={productCount > 0} />
          <ProgressStep number="3" label="Precio" complete={productCount > 0} />
        </div>
      </header>

      <div className="divide-y">
        <div className="grid gap-5 p-5 sm:p-6 lg:grid-cols-2">
          <SetupStep icon={Tags} number="1A" title="Crear categoría" description={`${activeCategories.length} categorías activas`}>
            <CatalogMasterCreateForm
              action={createCategoryAction}
              submitLabel="Guardar categoría"
              title="Nombre de la categoría"
              namePlaceholder="Ej. Alimentos balanceados"
              descriptionPlaceholder="Uso o agrupación comercial"
            />
          </SetupStep>
          <SetupStep icon={Tags} number="1B" title="Registrar marca" description={`${activeBrands.length} marcas activas`}>
            <CatalogMasterCreateForm
              action={createBrandAction}
              submitLabel="Guardar marca"
              title="Nombre de la marca"
              namePlaceholder="Ej. Campo Sur"
              descriptionPlaceholder="Fabricante o proveedor habitual"
            />
          </SetupStep>
        </div>

        <div className="p-5 sm:p-6">
          <SetupStep
            icon={PackagePlus}
            number="2–3"
            title="Registrar producto y definir precio"
            description={`${activeUnits.length} unidades · ${activePriceLists.length} listas disponibles`}
          >
            {canCreateProduct ? (
              <CatalogProductCreateForm
                categories={activeCategories}
                brands={activeBrands}
                units={activeUnits}
                priceLists={activePriceLists}
              />
            ) : (
              <Alert variant="destructive">
                <AlertTitle>Falta configuración comercial</AlertTitle>
                <AlertDescription>
                  Necesitás al menos una unidad de medida y una lista de precios activa antes de crear productos.
                </AlertDescription>
              </Alert>
            )}
          </SetupStep>
        </div>
      </div>
    </section>
  );
}

function ProgressStep({ number, label, complete }: { number: string; label: string; complete: boolean }) {
  return (
    <div className={`flex items-center gap-3 rounded-lg border px-3 py-2.5 ${complete ? "border-primary/30 bg-primary/5" : "bg-background"}`}>
      <span className={`grid size-7 place-items-center rounded-full text-xs font-semibold ${complete ? "bg-primary text-primary-foreground" : "border text-muted-foreground"}`}>
        {complete ? <Check className="size-3.5" aria-label="Completo" /> : number}
      </span>
      <span className="text-sm font-medium">{label}</span>
    </div>
  );
}

function SetupStep({
  icon: Icon,
  number,
  title,
  description,
  children,
}: {
  icon: typeof CircleDollarSign;
  number: string;
  title: string;
  description: string;
  children: React.ReactNode;
}) {
  return (
    <div>
      <div className="mb-4 flex items-start gap-3">
        <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-muted text-primary"><Icon className="size-4" /></span>
        <div>
          <p className="text-xs font-semibold uppercase tracking-wider text-primary">Paso {number}</p>
          <h3 className="font-semibold">{title}</h3>
          <p className="text-xs text-muted-foreground">{description}</p>
        </div>
      </div>
      {children}
    </div>
  );
}
