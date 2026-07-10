import { createCategoryAction, updateCategoryAction } from "@/app/dashboard/catalog/categories/actions";
import { CatalogMasterCreateForm, CatalogMasterUpdateForm } from "@/components/catalog/catalog-master-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { listCatalogCategories, requireSession } from "@/lib/auth/session";
import { ApiError } from "@/lib/api/server";

export default async function CatalogCategoriesPage() {
  const session = await requireSession();
  const isEditor = session.roles.some((role) => role.code === "administrator" || role.code === "manager");
  let categories: Awaited<ReturnType<typeof listCatalogCategories>> | null = null;
  let errorState: { title: string; description: string } | null = null;

  try {
    categories = await listCatalogCategories();
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      errorState = {
        title: "Acceso restringido",
        description: "No cuentas con permisos para consultar categorias.",
      };
    } else {
      errorState = {
        title: "No pudimos cargar las categorias",
        description: error instanceof Error ? error.message : "Ocurrio un error inesperado.",
      };
    }
  }

  if (errorState || !categories) {
    return (
      <Alert variant="destructive">
        <AlertTitle>{errorState?.title ?? "No pudimos cargar las categorias"}</AlertTitle>
        <AlertDescription>{errorState?.description ?? "Ocurrio un error inesperado."}</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="grid gap-6">
      <Card>
        <CardHeader className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <Badge className="w-fit">Categorias</Badge>
            {!isEditor ? <Badge variant="outline">Lectura</Badge> : null}
          </div>
          <CardDescription>Clasificacion comercial para productos del catalogo.</CardDescription>
        </CardHeader>
        <CardContent>
          {isEditor ? (
            <CatalogMasterCreateForm
              action={createCategoryAction}
              submitLabel="Crear categoria"
              title="Categoria"
            />
          ) : (
            <Alert>
              <AlertTitle>Vista de consulta</AlertTitle>
              <AlertDescription>Tu rol solo puede ver las categorias existentes.</AlertDescription>
            </Alert>
          )}
        </CardContent>
      </Card>

      <section className="grid gap-4">
        {categories.items.map((category) => (
          <Card key={category.categoryId}>
            <CardHeader>
              <div className="flex flex-wrap items-center gap-2">
                <CardTitle className="text-lg">{category.name}</CardTitle>
                <Badge variant="outline">{category.isActive ? "Activa" : "Inactiva"}</Badge>
              </div>
              <CardDescription>{category.description ?? "Sin descripcion"}</CardDescription>
            </CardHeader>
            <CardContent>
              {isEditor ? (
                <CatalogMasterUpdateForm
                  action={updateCategoryAction}
                  idFieldName="categoryId"
                  idValue={category.categoryId}
                  name={category.name}
                  description={category.description}
                  isActive={category.isActive}
                />
              ) : null}
            </CardContent>
          </Card>
        ))}
      </section>
    </div>
  );
}
