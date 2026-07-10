"use client";

import { useActionState } from "react";

import { updateUserAction, type UserActionState } from "@/app/dashboard/users/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { AuthRole, AuthUser } from "@/lib/api/contracts";

const initialState: UserActionState = {
  error: null,
  success: null,
};

export function UpdateUserForm({
  user,
  roles,
}: {
  user: AuthUser;
  roles: AuthRole[];
}) {
  const [state, formAction, isPending] = useActionState(updateUserAction, initialState);
  const assignedRoleIds = new Set(user.roles.map((role) => role.roleId));

  return (
    <form action={formAction} className="grid gap-4 rounded-xl border border-border/70 bg-background/70 p-4">
      <input type="hidden" name="userId" value={user.userId} />

      <div className="grid gap-4 md:grid-cols-[1fr_auto_auto] md:items-end">
        <div className="space-y-2">
          <label className="text-sm font-medium" htmlFor={`displayName-${user.userId}`}>
            Nombre
          </label>
          <input
            id={`displayName-${user.userId}`}
            name="displayName"
            defaultValue={user.displayName}
            required
            className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
          />
        </div>

        <label className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm">
          <input name="isActive" type="checkbox" className="size-4" defaultChecked={user.isActive} />
          Activo
        </label>

        <label className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm">
          <input name="isLocked" type="checkbox" className="size-4" defaultChecked={user.isLocked} />
          Bloqueado
        </label>
      </div>

      <div className="flex flex-wrap gap-2 text-sm text-muted-foreground">
        <span>{user.email}</span>
        <span>•</span>
        <span>{user.permissions.length} permisos efectivos</span>
      </div>

      <label className="flex items-center gap-2 text-sm">
        <input
          name="mustChangePassword"
          type="checkbox"
          className="size-4"
          defaultChecked={user.mustChangePassword}
        />
        Solicitar cambio de contrasena
      </label>

      <fieldset className="grid gap-2">
        <legend className="text-sm font-medium">Roles</legend>
        <div className="grid gap-2 sm:grid-cols-2">
          {roles.map((role) => (
            <label
              key={`${user.userId}-${role.roleId}`}
              className="flex items-center gap-3 rounded-lg border border-border/70 px-3 py-2 text-sm"
            >
              <input
                type="checkbox"
                name="roleIds"
                value={role.roleId}
                className="size-4"
                defaultChecked={assignedRoleIds.has(role.roleId)}
              />
              <span>{role.name}</span>
            </label>
          ))}
        </div>
      </fieldset>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos actualizar el usuario</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Usuario actualizado</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Actualizando..." : "Guardar cambios"}
      </Button>
    </form>
  );
}
