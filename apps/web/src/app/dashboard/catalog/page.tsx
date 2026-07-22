import { CatalogProductCreateForm } from "@/components/catalog/catalog-product-create-form";
import { CatalogProductUpdateForm } from "@/components/catalog/catalog-product-update-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import {
  listCatalogBrands,
  listCatalogCategories,
  listCatalogPriceLists,
  listCatalogProducts,
  listCatalogUnits,
  requireSession,
} from "@/lib/auth/session";
import { ApiError } from "@/lib/api/server";

export default async function CatalogPage({
  searchParams,
}: {
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
}) {
  const session = await requireSession();
  const resolvedSearchParams = (await searchParams) ?? {};
  const isEditor = session.roles.some((role) => role.code === "administrator" || role.code === "manager");
  let products: Awaited<ReturnType<typeof listCatalogProducts>> | null = null;
  let categories: Awaited<ReturnType<typeof listCatalogCategories>> | null = null;
  let brands: Awaited<ReturnType<typeof listCatalogBrands>> | null = null;
  let units: Awaited<ReturnType<typeof listCatalogUnits>> | null = null;
  let priceLists: Awaited<ReturnType<typeof listCatalogPriceLists>> | null = null;
  let errorState: { title: string; description: string } | null = null;

  try {
    [products, categories, brands, units, priceLists] = await Promise.all([
      listCatalogProducts({
        search: firstValue(resolvedSearchParams.search),
        categoryId: firstValue(resolvedSearchParams.categoryId),
        brandId: firstValue(resolvedSearchParams.brandId),
        isActive: firstValue(resolvedSearchParams.isActive),
      }),
      listCatalogCategories(),
      listCatalogBrands(),
      listCatalogUnits(),
      listCatalogPriceLists(),
    ]);

  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      errorState = {
        title: "Acceso restringido",
        description: "No cuentas con permisos para consultar el catalogo.",
      };
    } else {
      errorState = {
        title: "No pudimos cargar el catalogo",
        description: error instanceof Error ? error.message : "Ocurrio un error inesperado.",
      };
    }
  }

  if (errorState || !products || !categories || !brands || !units || !priceLists) {
    return (
      <Alert variant="destructive">
        <AlertTitle>{errorState?.title ?? "No pudimos cargar el catalogo"}</AlertTitle>
        <AlertDescription>{errorState?.description ?? "Ocurrio un error inesperado."}</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="grid gap-5">
      <header className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <p className="text-sm font-medium text-primary">Productos</p>
          <h1 className="text-3xl font-semibold tracking-tight">Catálogo</h1>
          <p className="mt-1 text-sm text-muted-foreground">{products.total} productos encontrados</p>
        </div>
        {!isEditor ? <Badge variant="outline">Solo lectura</Badge> : null}
      </header>

      <Card>
        <CardContent className="grid gap-4 pt-5">
          <form method="get" className="grid gap-3 lg:grid-cols-[minmax(240px,1fr)_repeat(3,minmax(150px,auto))_auto]">
            <input
              name="search"
              defaultValue={firstValue(resolvedSearchParams.search) ?? ""}
              placeholder="Buscar por nombre, código, SKU o barras"
              className="h-11 rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
            />
            <select
              name="categoryId"
              defaultValue={firstValue(resolvedSearchParams.categoryId) ?? ""}
              className="h-11 rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
            >
              <option value="">Todas las categorias</option>
              {categories.items.map((category) => (
                <option key={category.categoryId} value={category.categoryId}>
                  {category.name}
                </option>
              ))}
            </select>
            <select
              name="brandId"
              defaultValue={firstValue(resolvedSearchParams.brandId) ?? ""}
              className="h-11 rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
            >
              <option value="">Todas las marcas</option>
              {brands.items.map((brand) => (
                <option key={brand.brandId} value={brand.brandId}>
                  {brand.name}
                </option>
              ))}
            </select>
            <select
              name="isActive"
              defaultValue={firstValue(resolvedSearchParams.isActive) ?? ""}
              className="h-11 rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
            >
              <option value="">Todos los estados</option>
              <option value="true">Activos</option>
              <option value="false">Inactivos</option>
            </select>
            <button className="h-11 rounded-lg bg-primary px-5 text-sm font-medium text-primary-foreground">
              Buscar
            </button>
          </form>

          {isEditor ? (
            <details className="group rounded-lg border border-dashed border-primary/40 bg-primary/[0.03]">
              <summary className="flex cursor-pointer list-none items-center justify-between px-4 py-3 text-sm font-semibold text-primary">
                Nuevo producto
                <span className="text-lg leading-none transition-transform group-open:rotate-45">+</span>
              </summary>
              <div className="border-t border-border p-4">
                <CatalogProductCreateForm
                  categories={categories.items.filter((category) => category.isActive)}
                  brands={brands.items.filter((brand) => brand.isActive)}
                  units={units.items.filter((unit) => unit.isActive)}
                  priceLists={priceLists.items.filter((priceList) => priceList.isActive)}
                />
              </div>
            </details>
          ) : null}
        </CardContent>
      </Card>

      <section className="overflow-hidden rounded-xl border bg-card">
        {products.items.length === 0 ? (
          <Alert className="m-4">
            <AlertTitle>Sin productos</AlertTitle>
            <AlertDescription>
              Todavia no hay productos que coincidan con los filtros seleccionados.
            </AlertDescription>
          </Alert>
        ) : (
          <div className="divide-y divide-border">
            <div className="hidden grid-cols-[minmax(260px,1.5fr)_1fr_120px_120px] gap-4 bg-muted/50 px-5 py-2 text-xs font-medium uppercase tracking-wide text-muted-foreground md:grid">
              <span>Producto</span><span>Clasificación</span><span>Costo</span><span className="text-right">Precio</span>
            </div>
            {products.items.map((product) => (
              <article key={product.productId} className="px-4 py-4 transition-colors hover:bg-muted/35 sm:px-5">
                <div className="grid items-center gap-3 md:grid-cols-[minmax(260px,1.5fr)_1fr_120px_120px] md:gap-4">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <h2 className="truncate font-semibold">{product.name}</h2>
                      {!product.isActive ? <Badge>Inactivo</Badge> : null}
                    </div>
                    <p className="mt-1 truncate text-xs text-muted-foreground">
                      {product.internalCode}{product.sku ? ` · SKU ${product.sku}` : ""}{product.barcode ? ` · ${product.barcode}` : ""}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-1.5">
                    {product.category ? <Badge variant="outline">{product.category.name}</Badge> : null}
                    {product.brand ? <Badge variant="outline">{product.brand.name}</Badge> : null}
                    <Badge variant="outline">{product.baseUnit.symbol}</Badge>
                  </div>
                  <p className="text-sm tabular-nums text-muted-foreground">$ {product.currentPrice.costAmount.toFixed(2)}</p>
                  <p className="text-lg font-semibold tabular-nums md:text-right">$ {product.currentPrice.saleAmount.toFixed(2)}</p>
                </div>
                {isEditor ? (
                  <details className="group mt-3">
                    <summary className="cursor-pointer list-none text-xs font-medium text-primary hover:underline">Editar producto</summary>
                    <div className="mt-4 rounded-lg border bg-background p-4">
                      <CatalogProductUpdateForm
                        product={product}
                        categories={categories.items}
                        brands={brands.items}
                        units={units.items}
                        priceLists={priceLists.items}
                      />
                    </div>
                  </details>
                ) : null}
              </article>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function firstValue(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}
