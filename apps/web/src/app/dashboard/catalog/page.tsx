import { CatalogProductCreateForm } from "@/components/catalog/catalog-product-create-form";
import { CatalogProductUpdateForm } from "@/components/catalog/catalog-product-update-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
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
    <div className="grid gap-6">
      <Card>
        <CardHeader className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <Badge className="w-fit">Catalogo</Badge>
            {!isEditor ? <Badge variant="outline">Lectura</Badge> : null}
          </div>
          <div className="space-y-2">
            <CardTitle className="text-2xl">Productos, categorias, marcas y precios base</CardTitle>
            <CardDescription>
              Slice operativo para una sola locacion, listo para conectar el proximo
              incremento de stock.
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent className="grid gap-4">
          <form method="get" className="grid gap-4 md:grid-cols-4">
            <input
              name="search"
              defaultValue={firstValue(resolvedSearchParams.search) ?? ""}
              placeholder="Nombre, codigo, SKU o barcode"
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
            <button className="h-11 rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground md:col-span-4 md:w-fit">
              Aplicar filtros
            </button>
          </form>

          {isEditor ? (
            <Card className="border-dashed">
              <CardHeader>
                <CardTitle className="text-lg">Alta rapida de producto</CardTitle>
                <CardDescription>
                  Crea el producto y su precio vigente en una sola operacion.
                </CardDescription>
              </CardHeader>
              <CardContent>
                <CatalogProductCreateForm
                  categories={categories.items.filter((category) => category.isActive)}
                  brands={brands.items.filter((brand) => brand.isActive)}
                  units={units.items.filter((unit) => unit.isActive)}
                  priceLists={priceLists.items.filter((priceList) => priceList.isActive)}
                />
              </CardContent>
            </Card>
          ) : (
            <Alert>
              <AlertTitle>Vista de consulta</AlertTitle>
              <AlertDescription>
                Tu rol puede consultar catalogo y precios, pero no editar productos.
              </AlertDescription>
            </Alert>
          )}
        </CardContent>
      </Card>

      <section className="grid gap-4">
        {products.items.length === 0 ? (
          <Alert>
            <AlertTitle>Sin productos</AlertTitle>
            <AlertDescription>
              Todavia no hay productos que coincidan con los filtros seleccionados.
            </AlertDescription>
          </Alert>
        ) : (
          products.items.map((product) => (
            <Card key={product.productId}>
              <CardHeader className="space-y-3">
                <div className="flex flex-wrap items-center gap-2">
                  <CardTitle className="text-lg">{product.name}</CardTitle>
                  <Badge variant={product.isActive ? "outline" : "default"}>
                    {product.isActive ? "Activo" : "Inactivo"}
                  </Badge>
                  {product.category ? <Badge variant="outline">{product.category.name}</Badge> : null}
                  {product.brand ? <Badge variant="outline">{product.brand.name}</Badge> : null}
                </div>
                <CardDescription>
                  {product.internalCode}
                  {product.sku ? ` | SKU ${product.sku}` : ""}
                  {product.barcode ? ` | BAR ${product.barcode}` : ""}
                </CardDescription>
              </CardHeader>
              <CardContent className="grid gap-4">
                <div className="grid gap-4 md:grid-cols-4">
                  <SummaryItem label="Unidad" value={product.baseUnit.symbol} />
                  <SummaryItem label="Costo" value={`$ ${product.currentPrice.costAmount.toFixed(2)}`} />
                  <SummaryItem label="Margen" value={`${product.currentPrice.marginPercent ?? 0}%`} />
                  <SummaryItem label="Precio" value={`$ ${product.currentPrice.saleAmount.toFixed(2)}`} />
                </div>

                {isEditor ? (
                  <CatalogProductUpdateForm
                    product={product}
                    categories={categories.items}
                    brands={brands.items}
                    units={units.items}
                    priceLists={priceLists.items}
                  />
                ) : null}
              </CardContent>
            </Card>
          ))
        )}
      </section>
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
