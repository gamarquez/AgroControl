import { createBrandAction, updateBrandAction } from "@/app/dashboard/catalog/brands/actions";
import { CatalogMasterCreateForm, CatalogMasterUpdateForm } from "@/components/catalog/catalog-master-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { listCatalogBrands, requireSession } from "@/lib/auth/session";
import { ApiError } from "@/lib/api/server";

export default async function CatalogBrandsPage() {
  const session = await requireSession();
  const isEditor = session.roles.some((role) => role.code === "administrator" || role.code === "manager");
  let brands: Awaited<ReturnType<typeof listCatalogBrands>> | null = null;
  let errorState: { title: string; description: string } | null = null;

  try {
    brands = await listCatalogBrands();
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      errorState = {
        title: "Acceso restringido",
        description: "No cuentas con permisos para consultar marcas.",
      };
    } else {
      errorState = {
        title: "No pudimos cargar las marcas",
        description: error instanceof Error ? error.message : "Ocurrio un error inesperado.",
      };
    }
  }

  if (errorState || !brands) {
    return (
      <Alert variant="destructive">
        <AlertTitle>{errorState?.title ?? "No pudimos cargar las marcas"}</AlertTitle>
        <AlertDescription>{errorState?.description ?? "Ocurrio un error inesperado."}</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="grid gap-6">
      <Card>
        <CardHeader className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <Badge className="w-fit">Marcas</Badge>
            {!isEditor ? <Badge variant="outline">Lectura</Badge> : null}
          </div>
          <CardDescription>Maestro comercial para identificar origen y proveedor frecuente.</CardDescription>
        </CardHeader>
        <CardContent>
          {isEditor ? (
            <CatalogMasterCreateForm action={createBrandAction} submitLabel="Crear marca" title="Marca" />
          ) : (
            <Alert>
              <AlertTitle>Vista de consulta</AlertTitle>
              <AlertDescription>Tu rol solo puede ver las marcas existentes.</AlertDescription>
            </Alert>
          )}
        </CardContent>
      </Card>

      <section className="grid gap-4">
        {brands.items.map((brand) => (
          <Card key={brand.brandId}>
            <CardHeader>
              <div className="flex flex-wrap items-center gap-2">
                <CardTitle className="text-lg">{brand.name}</CardTitle>
                <Badge variant="outline">{brand.isActive ? "Activa" : "Inactiva"}</Badge>
              </div>
              <CardDescription>{brand.description ?? "Sin descripcion"}</CardDescription>
            </CardHeader>
            <CardContent>
              {isEditor ? (
                <CatalogMasterUpdateForm
                  action={updateBrandAction}
                  idFieldName="brandId"
                  idValue={brand.brandId}
                  name={brand.name}
                  description={brand.description}
                  isActive={brand.isActive}
                />
              ) : null}
            </CardContent>
          </Card>
        ))}
      </section>
    </div>
  );
}
