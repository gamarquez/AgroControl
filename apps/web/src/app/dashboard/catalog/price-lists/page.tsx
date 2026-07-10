import { createPriceListAction, updatePriceListAction } from "@/app/dashboard/catalog/price-lists/actions";
import {
  CatalogPriceListCreateForm,
  CatalogPriceListUpdateForm,
} from "@/components/catalog/catalog-master-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { listCatalogPriceLists, requireSession } from "@/lib/auth/session";
import { ApiError } from "@/lib/api/server";

export default async function CatalogPriceListsPage() {
  const session = await requireSession();
  const isEditor = session.roles.some((role) => role.code === "administrator" || role.code === "manager");
  let priceLists: Awaited<ReturnType<typeof listCatalogPriceLists>> | null = null;
  let errorState: { title: string; description: string } | null = null;

  try {
    priceLists = await listCatalogPriceLists();
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      errorState = {
        title: "Acceso restringido",
        description: "No cuentas con permisos para consultar listas de precios.",
      };
    } else {
      errorState = {
        title: "No pudimos cargar las listas",
        description: error instanceof Error ? error.message : "Ocurrio un error inesperado.",
      };
    }
  }

  if (errorState || !priceLists) {
    return (
      <Alert variant="destructive">
        <AlertTitle>{errorState?.title ?? "No pudimos cargar las listas"}</AlertTitle>
        <AlertDescription>{errorState?.description ?? "Ocurrio un error inesperado."}</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="grid gap-6">
      <Card>
        <CardHeader className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <Badge className="w-fit">Listas de precios</Badge>
            {!isEditor ? <Badge variant="outline">Lectura</Badge> : null}
          </div>
          <CardDescription>
            El incremento usa una sola lista default, pero deja el modelo listo para crecer.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {isEditor ? (
            <CatalogPriceListCreateForm action={createPriceListAction} />
          ) : (
            <Alert>
              <AlertTitle>Vista de consulta</AlertTitle>
              <AlertDescription>Tu rol solo puede ver las listas de precios existentes.</AlertDescription>
            </Alert>
          )}
        </CardContent>
      </Card>

      <section className="grid gap-4">
        {priceLists.items.map((priceList) => (
          <Card key={priceList.priceListId}>
            <CardHeader>
              <div className="flex flex-wrap items-center gap-2">
                <CardTitle className="text-lg">{priceList.name}</CardTitle>
                {priceList.isDefault ? <Badge>Default</Badge> : <Badge variant="outline">Secundaria</Badge>}
                <Badge variant="outline">{priceList.isActive ? "Activa" : "Inactiva"}</Badge>
              </div>
              <CardDescription>Codigo: {priceList.code}</CardDescription>
            </CardHeader>
            <CardContent>
              {isEditor ? (
                <CatalogPriceListUpdateForm
                  action={updatePriceListAction}
                  priceListId={priceList.priceListId}
                  name={priceList.name}
                  code={priceList.code}
                  isDefault={priceList.isDefault}
                  isActive={priceList.isActive}
                />
              ) : null}
            </CardContent>
          </Card>
        ))}
      </section>
    </div>
  );
}
