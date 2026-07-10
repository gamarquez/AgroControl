"use client";

import { useActionState } from "react";

import {
  updateOrganizationSettingsAction,
  type SettingsActionState,
} from "@/app/dashboard/settings/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { OrganizationSettings } from "@/lib/api/contracts";

const initialState: SettingsActionState = {
  error: null,
  success: null,
};

export function OrganizationSettingsForm({
  settings,
}: {
  settings: OrganizationSettings;
}) {
  const [state, formAction, isPending] = useActionState(
    updateOrganizationSettingsAction,
    initialState,
  );

  return (
    <form action={formAction} className="grid gap-4">
      <div className="grid gap-4 md:grid-cols-2">
        <Field
          label="Nombre legal"
          name="legalName"
          defaultValue={settings.legalName}
          placeholder="Forrajeria El Trigal S.A."
        />
        <Field
          label="Nombre comercial"
          name="tradeName"
          defaultValue={settings.tradeName}
          placeholder="El Trigal"
        />
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <Field label="CUIT" name="taxId" defaultValue={settings.taxId} placeholder="30-12345678-9" />
        <Field
          label="Zona horaria"
          name="timeZone"
          defaultValue={settings.timeZone}
          placeholder="America/Argentina/Buenos_Aires"
        />
        <Field label="Moneda" name="currencyCode" defaultValue={settings.currencyCode} placeholder="ARS" />
      </div>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos guardar la configuracion</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Comercio actualizado</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Guardando..." : "Guardar configuracion"}
      </Button>
    </form>
  );
}

function Field({
  label,
  name,
  defaultValue,
  placeholder,
}: {
  label: string;
  name: string;
  defaultValue: string;
  placeholder: string;
}) {
  return (
    <div className="space-y-2">
      <label className="text-sm font-medium" htmlFor={name}>
        {label}
      </label>
      <input
        id={name}
        name={name}
        defaultValue={defaultValue}
        required
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
        placeholder={placeholder}
      />
    </div>
  );
}
