import { CreateUserForm } from "@/components/admin/create-user-form";
import { UpdateUserForm } from "@/components/admin/update-user-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { listRoles, listUsers } from "@/lib/auth/session";
import { ApiError } from "@/lib/api/server";

export default async function UsersPage() {
  let users: Awaited<ReturnType<typeof listUsers>> | null = null;
  let roles: Awaited<ReturnType<typeof listRoles>> | null = null;
  let errorState: { title: string; description: string } | null = null;

  try {
    [users, roles] = await Promise.all([listUsers(), listRoles()]);
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      errorState = {
        title: "Acceso restringido",
        description: "Solo el rol administrador puede gestionar usuarios.",
      };
    } else {
      errorState = {
        title: "No pudimos cargar usuarios",
        description: error instanceof Error ? error.message : "Ocurrio un error inesperado.",
      };
    }
  }

  if (errorState || !users || !roles) {
    return (
      <Alert variant="destructive">
        <AlertTitle>{errorState?.title ?? "No pudimos cargar usuarios"}</AlertTitle>
        <AlertDescription>
          {errorState?.description ?? "Ocurrio un error inesperado."}
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="grid gap-6">
      <Card>
        <CardHeader className="space-y-3">
          <Badge className="w-fit">Administracion</Badge>
          <div className="space-y-2">
            <CardTitle className="text-2xl">Usuarios de la organizacion</CardTitle>
            <CardDescription>
              Alta rapida, activacion y asignacion de uno o varios roles existentes.
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent>
          <CreateUserForm roles={roles.items} />
        </CardContent>
      </Card>

      <section className="grid gap-4">
        {users.items.map((user) => (
          <Card key={user.userId}>
            <CardHeader className="space-y-3">
              <div className="flex flex-wrap items-center gap-2">
                <CardTitle className="text-lg">{user.displayName}</CardTitle>
                <Badge variant="outline">{user.roles.map((role) => role.name).join(", ")}</Badge>
              </div>
              <CardDescription>{user.email}</CardDescription>
            </CardHeader>
            <CardContent>
              <UpdateUserForm user={user} roles={roles.items} />
            </CardContent>
          </Card>
        ))}
      </section>
    </div>
  );
}
