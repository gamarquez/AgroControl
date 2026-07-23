import { redirect } from "next/navigation";

import { HealthStatusPanel } from "@/components/health/health-status-panel";
import { LoginForm } from "@/components/auth/login-form";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { getSession } from "@/lib/auth/session";

export default async function LoginPage() {
  const session = await getSession();

  if (session) {
    redirect("/dashboard");
  }

  return (
    <main className="flex min-h-screen items-center justify-center px-4 py-10 sm:px-6">
      <Card className="w-full max-w-md border-border/80 bg-card shadow-xl shadow-black/5">
        <CardHeader className="space-y-2 px-6 pt-6 sm:px-8 sm:pt-8">
          <p className="text-sm font-semibold tracking-wide text-primary">AgroControl</p>
          <CardTitle className="text-2xl">Ingresar</CardTitle>
          <CardDescription>Accedé con tus credenciales para continuar.</CardDescription>
        </CardHeader>
        <CardContent className="px-6 sm:px-8">
          <LoginForm />
        </CardContent>
        <CardFooter className="border-t border-border/70 px-6 py-3 sm:px-8">
          <div className="w-full">
            <HealthStatusPanel />
          </div>
        </CardFooter>
      </Card>
    </main>
  );
}
