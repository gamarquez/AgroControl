import { OrganizationSettingsForm } from "@/components/admin/organization-settings-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getOrganizationSettings } from "@/lib/auth/session";
import { ApiError } from "@/lib/api/server";

export default async function OrganizationSettingsPage() {
  let settings: Awaited<ReturnType<typeof getOrganizationSettings>> | null = null;
  let errorState: { title: string; description: string } | null = null;

  try {
    settings = await getOrganizationSettings();
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      errorState = {
        title: "Acceso restringido",
        description: "Solo el rol administrador puede editar la configuracion del comercio.",
      };
    } else {
      errorState = {
        title: "No pudimos cargar la configuracion",
        description: error instanceof Error ? error.message : "Ocurrio un error inesperado.",
      };
    }
  }

  if (errorState || !settings) {
    return (
      <Alert variant="destructive">
        <AlertTitle>{errorState?.title ?? "No pudimos cargar la configuracion"}</AlertTitle>
        <AlertDescription>
          {errorState?.description ?? "Ocurrio un error inesperado."}
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <Card>
      <CardHeader className="space-y-3">
        <Badge className="w-fit">Comercio</Badge>
        <div className="space-y-2">
          <CardTitle className="text-2xl">Configuracion inicial</CardTitle>
          <CardDescription>
            Datos minimos que el frontend y los futuros modulos van a reutilizar como
            contexto base.
          </CardDescription>
        </div>
      </CardHeader>
      <CardContent>
        <OrganizationSettingsForm settings={settings} />
      </CardContent>
    </Card>
  );
}
