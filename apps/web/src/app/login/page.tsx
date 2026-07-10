import { redirect } from "next/navigation";

import { HealthStatusPanel } from "@/components/health/health-status-panel";
import { LoginForm } from "@/components/auth/login-form";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getSession } from "@/lib/auth/session";

export default async function LoginPage() {
  const session = await getSession();

  if (session) {
    redirect("/dashboard");
  }

  return (
    <main className="mx-auto grid min-h-screen w-full max-w-6xl gap-6 px-4 py-10 sm:px-6 lg:grid-cols-[1.1fr_0.9fr] lg:px-8">
      <section className="space-y-6">
        <Card className="border-border/80 bg-card/90 shadow-lg shadow-primary/5">
          <CardHeader className="space-y-4">
            <Badge className="w-fit">Incremento 2</Badge>
            <div className="space-y-3">
              <CardTitle className="max-w-2xl text-3xl leading-tight sm:text-4xl">
                Identidad propia para AgroControl con sesion, usuarios y comercio listos
                para crecer.
              </CardTitle>
              <CardDescription className="max-w-2xl text-base leading-7">
                Este MVP deja autenticacion JWT con refresh token rotativo, resolucion de
                sesion actual desde la API y una base multi-organizacion lista para
                catalogo, stock y caja.
              </CardDescription>
            </div>
          </CardHeader>
          <CardContent className="grid gap-3 text-sm text-muted-foreground sm:grid-cols-3">
            <div className="rounded-lg border border-border/70 bg-background/70 p-4">
              Login en espanol con estados claros.
            </div>
            <div className="rounded-lg border border-border/70 bg-background/70 p-4">
              Administracion basica de usuarios y roles.
            </div>
            <div className="rounded-lg border border-border/70 bg-background/70 p-4">
              Configuracion inicial del comercio sin exponer tokens al cliente.
            </div>
          </CardContent>
        </Card>

        <HealthStatusPanel />
      </section>

      <section className="flex items-center">
        <Card className="w-full border-border/80 bg-card/95 shadow-xl shadow-black/5">
          <CardHeader className="space-y-2">
            <CardTitle className="text-2xl">Ingresar</CardTitle>
            <CardDescription>
              Accede con tu usuario de la API. La sesion se guarda en cookies seguras del
              servidor web.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <LoginForm />
          </CardContent>
        </Card>
      </section>
    </main>
  );
}
