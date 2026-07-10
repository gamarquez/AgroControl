"use client";

import { useActionState } from "react";

import { loginAction, type LoginActionState } from "@/app/login/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

const initialState: LoginActionState = {
  error: null,
};

export function LoginForm() {
  const [state, formAction, isPending] = useActionState(loginAction, initialState);

  return (
    <form action={formAction} className="space-y-4">
      <div className="space-y-2">
        <label className="text-sm font-medium text-foreground" htmlFor="email">
          Email
        </label>
        <input
          id="email"
          name="email"
          type="email"
          autoComplete="email"
          required
          className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none ring-0 transition focus:border-primary"
          placeholder="admin@agrocontrol.local"
        />
      </div>

      <div className="space-y-2">
        <label className="text-sm font-medium text-foreground" htmlFor="password">
          Contrasena
        </label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          required
          minLength={8}
          className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none ring-0 transition focus:border-primary"
          placeholder="********"
        />
      </div>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos iniciar sesion</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full" disabled={isPending}>
        {isPending ? "Ingresando..." : "Ingresar"}
      </Button>
    </form>
  );
}
