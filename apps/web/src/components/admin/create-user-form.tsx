"use client";

import { useActionState } from "react";

import { createUserAction, type UserActionState } from "@/app/dashboard/users/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { AuthRole } from "@/lib/api/contracts";

const initialState: UserActionState = {
  error: null,
  success: null,
};

export function CreateUserForm({ roles }: { roles: AuthRole[] }) {
  const [state, formAction, isPending] = useActionState(createUserAction, initialState);

  return (
    <form action={formAction} className="grid gap-4">
      <div className="grid gap-4 md:grid-cols-2">
        <TextField label="Nombre" name="displayName" placeholder="Maria Perez" />
        <TextField label="Email" name="email" type="email" placeholder="maria@forrajeria.local" />
      </div>

      <div className="grid gap-4 md:grid-cols-[1fr_auto]">
        <TextField label="Contrasena inicial" name="password" type="password" placeholder="********" />
        <label className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm">
          <input name="mustChangePassword" type="checkbox" className="size-4" defaultChecked />
          Cambiar al ingresar
        </label>
      </div>

      <fieldset className="grid gap-3">
        <legend className="text-sm font-medium">Roles</legend>
        <div className="grid gap-2 sm:grid-cols-2">
          {roles.map((role) => (
            <label
              key={role.roleId}
              className="flex items-center gap-3 rounded-lg border border-border/70 px-3 py-2 text-sm"
            >
              <input
                type="checkbox"
                name="roleIds"
                value={role.roleId}
                className="size-4"
                defaultChecked={role.code === "seller"}
              />
              <span>{role.name}</span>
            </label>
          ))}
        </div>
      </fieldset>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos crear el usuario</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Usuario listo</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Guardando..." : "Crear usuario"}
      </Button>
    </form>
  );
}

function TextField({
  label,
  name,
  placeholder,
  type = "text",
}: {
  label: string;
  name: string;
  placeholder: string;
  type?: string;
}) {
  return (
    <div className="space-y-2">
      <label className="text-sm font-medium" htmlFor={name}>
        {label}
      </label>
      <input
        id={name}
        name={name}
        type={type}
        required
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
        placeholder={placeholder}
      />
    </div>
  );
}
