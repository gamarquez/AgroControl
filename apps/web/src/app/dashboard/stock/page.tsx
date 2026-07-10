import Link from "next/link";

import { PhysicalInventoryCountForm } from "@/components/stock/physical-inventory-count-form";
import { StockMovementForm } from "@/components/stock/stock-movement-form";
import { StockPolicyForm } from "@/components/stock/stock-policy-form";
import { WarehouseForm } from "@/components/stock/warehouse-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import {
  getStockProduct,
  listCatalogBrands,
  listCatalogCategories,
  listStockAlerts,
  listStockMovements,
  listStockProducts,
  listStockWarehouses,
  requireSession,
} from "@/lib/auth/session";
import { ApiError } from "@/lib/api/server";

export default async function StockPage({
  searchParams,
}: {
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
}) {
  const session = await requireSession();
  const resolvedSearchParams = (await searchParams) ?? {};
  const isEditor = session.roles.some((role) => role.code === "administrator" || role.code === "manager");

  let warehouses = null;
  let stock = null;
  let alerts = null;
  let categories = null;
  let brands = null;
  let selectedItem = null;
  let selectedMovements = null;
  let errorState: { title: string; description: string } | null = null;

  try {
    warehouses = await listStockWarehouses();
    const selectedWarehouseId = firstValue(resolvedSearchParams.warehouseId) ?? warehouses.items[0]?.warehouseId;
    const selectedProductId = firstValue(resolvedSearchParams.productId);

    [stock, alerts, categories, brands] = await Promise.all([
      listStockProducts({
        search: firstValue(resolvedSearchParams.search),
        warehouseId: selectedWarehouseId,
        categoryId: firstValue(resolvedSearchParams.categoryId),
        brandId: firstValue(resolvedSearchParams.brandId),
        isLowStock: firstValue(resolvedSearchParams.isLowStock),
      }),
      listStockAlerts({
        warehouseId: selectedWarehouseId,
        limit: 8,
      }),
      listCatalogCategories(),
      listCatalogBrands(),
    ]);

    if (selectedProductId && selectedWarehouseId) {
      [selectedItem, selectedMovements] = await Promise.all([
        getStockProduct(selectedProductId, selectedWarehouseId),
        listStockMovements(selectedProductId, selectedWarehouseId, { pageSize: 20 }),
      ]);
    }
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      errorState = {
        title: "Acceso restringido",
        description: "No cuentas con permisos para consultar stock.",
      };
    } else {
      errorState = {
        title: "No pudimos cargar stock",
        description: error instanceof Error ? error.message : "Ocurrio un error inesperado.",
      };
    }
  }

  if (errorState || !warehouses || !stock || !alerts || !categories || !brands) {
    return (
      <Alert variant="destructive">
        <AlertTitle>{errorState?.title ?? "No pudimos cargar stock"}</AlertTitle>
        <AlertDescription>{errorState?.description ?? "Ocurrio un error inesperado."}</AlertDescription>
      </Alert>
    );
  }

  const selectedWarehouseId = firstValue(resolvedSearchParams.warehouseId) ?? warehouses.items[0]?.warehouseId ?? "";
  const selectedWarehouse = warehouses.items.find((warehouse) => warehouse.warehouseId === selectedWarehouseId) ?? warehouses.items[0];

  return (
    <div className="grid gap-6">
      <Card>
        <CardHeader className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <Badge className="w-fit">Stock</Badge>
            <Badge variant="outline">Depositos</Badge>
            {!isEditor ? <Badge variant="outline">Lectura</Badge> : null}
          </div>
          <div className="space-y-2">
            <CardTitle className="text-2xl">Depositos, alertas e inventario fisico</CardTitle>
            <CardDescription>
              Stock por deposito, conteos fisicos auditables y alertas de reposicion sobre la ubicacion operativa seleccionada.
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent className="grid gap-4">
          <form method="get" className="grid gap-4 md:grid-cols-5">
            <select
              name="warehouseId"
              defaultValue={selectedWarehouseId}
              className="h-11 rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
            >
              {warehouses.items.map((warehouse) => (
                <option key={warehouse.warehouseId} value={warehouse.warehouseId}>
                  {warehouse.name} ({warehouse.code})
                </option>
              ))}
            </select>
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
              name="isLowStock"
              defaultValue={firstValue(resolvedSearchParams.isLowStock) ?? ""}
              className="h-11 rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
            >
              <option value="">Todos los estados</option>
              <option value="true">Solo stock bajo</option>
              <option value="false">Solo stock normal</option>
            </select>
            <button className="h-11 rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground md:col-span-5 md:w-fit">
              Aplicar filtros
            </button>
          </form>

          <div className="grid gap-4 md:grid-cols-4">
            <SummaryItem label="Deposito activo" value={selectedWarehouse?.name ?? "Sin deposito"} />
            <SummaryItem label="Codigo" value={selectedWarehouse?.code ?? "-"} />
            <SummaryItem label="Alertas activas" value={String(alerts.items.length)} />
            <SummaryItem label="Productos visibles" value={String(stock.total)} />
          </div>

          {!isEditor ? (
            <Alert>
              <AlertTitle>Vista de consulta</AlertTitle>
              <AlertDescription>
                Tu rol puede consultar stock por deposito, pero no editar politicas ni registrar movimientos o conteos.
              </AlertDescription>
            </Alert>
          ) : null}
        </CardContent>
      </Card>

      <div className="grid gap-6 lg:grid-cols-[1.3fr_0.9fr]">
        <section className="grid gap-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Alertas de stock</CardTitle>
              <CardDescription>
                Productos por debajo del punto de reposicion o stock minimo del deposito seleccionado.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3">
              {alerts.items.length === 0 ? (
                <Alert>
                  <AlertTitle>Sin alertas</AlertTitle>
                  <AlertDescription>No encontramos productos en reposicion para este deposito.</AlertDescription>
                </Alert>
              ) : (
                alerts.items.map((alertItem) => (
                  <div key={`${alertItem.warehouse.warehouseId}-${alertItem.productId}`} className="rounded-lg border border-border/70 bg-background/70 p-4">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="text-sm font-medium">{alertItem.productName}</p>
                      <Badge>Reponer</Badge>
                    </div>
                    <p className="mt-2 text-xs text-muted-foreground">{alertItem.internalCode}</p>
                    <p className="mt-2 text-sm text-muted-foreground">
                      Disponible: {alertItem.onHandQuantity.toFixed(3)} {alertItem.unitSymbol}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Reposicion: {formatQuantity(alertItem.reorderPoint, alertItem.unitSymbol)} | Minimo: {formatQuantity(alertItem.minQuantity, alertItem.unitSymbol)}
                    </p>
                  </div>
                ))
              )}
            </CardContent>
          </Card>

          {stock.items.length === 0 ? (
            <Alert>
              <AlertTitle>Sin productos de stock</AlertTitle>
              <AlertDescription>Todavia no hay productos que coincidan con los filtros seleccionados.</AlertDescription>
            </Alert>
          ) : (
            stock.items.map((item) => (
              <Card key={`${item.warehouse.warehouseId}-${item.productId}`}>
                <CardHeader className="space-y-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <CardTitle className="text-lg">{item.name}</CardTitle>
                    {item.isLowStock ? <Badge>Stock bajo</Badge> : <Badge variant="outline">Stock normal</Badge>}
                    {!item.isActive ? <Badge>Inactivo</Badge> : null}
                  </div>
                  <CardDescription>
                    {item.internalCode}
                    {item.sku ? ` | SKU ${item.sku}` : ""}
                    {item.barcode ? ` | BAR ${item.barcode}` : ""}
                    {` | ${item.warehouse.name}`}
                  </CardDescription>
                </CardHeader>
                <CardContent className="grid gap-4">
                  <div className="grid gap-4 md:grid-cols-4">
                    <SummaryItem label="Disponible" value={`${item.onHandQuantity.toFixed(3)} ${item.baseUnit.symbol}`} />
                    <SummaryItem label="Minimo" value={formatQuantity(item.policy.minQuantity, item.baseUnit.symbol)} />
                    <SummaryItem label="Reposicion" value={formatQuantity(item.policy.reorderPoint, item.baseUnit.symbol)} />
                    <SummaryItem label="Ultimo mov." value={item.lastMovementAt ? formatDate(item.lastMovementAt) : "Sin movimientos"} />
                  </div>

                  <div className="flex flex-wrap gap-2">
                    {item.category ? <Badge variant="outline">{item.category.name}</Badge> : null}
                    {item.brand ? <Badge variant="outline">{item.brand.name}</Badge> : null}
                    <Badge variant="outline">{item.baseUnit.name}</Badge>
                    <Badge variant="outline">{item.warehouse.code}</Badge>
                  </div>

                  <Link
                    className="text-sm font-medium text-primary underline-offset-4 hover:underline"
                    href={`/dashboard/stock?warehouseId=${item.warehouse.warehouseId}&productId=${item.productId}`}
                  >
                    Ver detalle y movimientos
                  </Link>
                </CardContent>
              </Card>
            ))
          )}
        </section>

        <aside className="grid gap-4">
          {isEditor ? <WarehouseForm warehouse={selectedWarehouse} /> : null}

          {selectedItem && selectedMovements ? (
            <>
              <Card>
                <CardHeader>
                  <CardTitle className="text-lg">{selectedItem.name}</CardTitle>
                  <CardDescription>
                    Deposito: {selectedItem.warehouse.name} | Stock actual: {selectedItem.onHandQuantity.toFixed(3)} {selectedItem.baseUnit.symbol}
                  </CardDescription>
                </CardHeader>
                <CardContent className="grid gap-4">
                  <div className="grid gap-4 sm:grid-cols-2">
                    <SummaryItem label="Codigo" value={selectedItem.internalCode} />
                    <SummaryItem label="Stock bajo" value={selectedItem.isLowStock ? "Si" : "No"} />
                  </div>
                </CardContent>
              </Card>

              {isEditor ? <StockPolicyForm item={selectedItem} /> : null}
              {isEditor ? <PhysicalInventoryCountForm item={selectedItem} /> : null}
              {isEditor ? <StockMovementForm item={selectedItem} /> : null}

              <Card>
                <CardHeader>
                  <CardTitle className="text-lg">Ultimos movimientos</CardTitle>
                  <CardDescription>Historial reciente del producto seleccionado en este deposito.</CardDescription>
                </CardHeader>
                <CardContent className="grid gap-3">
                  {selectedMovements.items.length === 0 ? (
                    <Alert>
                      <AlertTitle>Sin movimientos</AlertTitle>
                      <AlertDescription>Todavia no se registraron movimientos para este producto en este deposito.</AlertDescription>
                    </Alert>
                  ) : (
                    selectedMovements.items.map((movement) => (
                      <div key={movement.stockMovementId} className="rounded-lg border border-border/70 bg-background/70 p-4">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <p className="text-sm font-medium">{translateMovementType(movement.movementType)}</p>
                          <Badge variant={movement.quantityDelta >= 0 ? "outline" : "secondary"}>
                            {movement.quantityDelta >= 0 ? "+" : ""}
                            {movement.quantityDelta.toFixed(3)}
                          </Badge>
                        </div>
                        <p className="mt-2 text-sm text-muted-foreground">{movement.reason}</p>
                        <p className="mt-2 text-xs text-muted-foreground">
                          Saldo resultante: {movement.resultingQuantity.toFixed(3)} {selectedItem.baseUnit.symbol}
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
            </>
          ) : (
            <Card>
              <CardHeader>
                <CardTitle className="text-lg">Detalle operativo</CardTitle>
                <CardDescription>
                  Selecciona un producto para consultar politica, movimientos e inventario fisico del deposito elegido.
                </CardDescription>
              </CardHeader>
            </Card>
          )}
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

function firstValue(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("es-AR", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(value));
}

function formatQuantity(value: number | null, symbol: string) {
  return value === null ? "No definido" : `${value.toFixed(3)} ${symbol}`;
}

function translateMovementType(value: string) {
  switch (value) {
    case "purchase_inbound":
      return "Ingreso por compra";
    case "sale_outbound":
      return "Egreso por venta";
    case "adjustment_increase":
      return "Ajuste positivo";
    case "adjustment_decrease":
      return "Ajuste negativo";
    case "return_inbound":
      return "Devolucion";
    case "loss":
      return "Perdida";
    case "broken":
      return "Rotura";
    case "expired":
      return "Vencimiento";
    default:
      return value;
  }
}
