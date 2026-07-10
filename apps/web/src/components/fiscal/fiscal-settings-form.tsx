"use client";

import { useActionState } from "react";

import { updateFiscalSettingsAction, type FiscalActionState } from "@/app/dashboard/fiscal/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { FiscalSettings } from "@/lib/api/contracts";

const initialState: FiscalActionState = {
  error: null,
  success: null,
};

export function FiscalSettingsForm({ settings }: { settings: FiscalSettings }) {
  const [state, formAction, isPending] = useActionState(updateFiscalSettingsAction, initialState);

  return (
    <form action={formAction} className="grid gap-4">
      <div className="grid gap-4 md:grid-cols-3">
        <SelectField
          label="Proveedor"
          name="provider"
          defaultValue={settings.provider}
          options={[
            { value: "disabled", label: "Deshabilitado" },
            { value: "arca_wsfev1", label: "ARCA WSFEv1" },
          ]}
        />
        <SelectField
          label="Entorno"
          name="environment"
          defaultValue={settings.environment}
          options={[
            { value: "homologation", label: "Homologacion" },
            { value: "production", label: "Produccion" },
          ]}
        />
        <TextField
          label="CUIT emisor"
          name="taxpayerId"
          defaultValue={settings.taxpayerId}
          placeholder="30123456789"
        />
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <NumberField
          label="Punto de venta"
          name="pointOfSale"
          defaultValue={String(settings.pointOfSale)}
        />
        <TextField label="Servicio" name="serviceName" defaultValue={settings.serviceName} placeholder="wsfe" />
        <TextField
          label="Documento por defecto"
          name="defaultDocumentType"
          defaultValue={settings.defaultDocumentType}
          placeholder="invoice_c"
        />
      </div>

      <label className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm">
        <input name="isEnabled" type="checkbox" defaultChecked={settings.isEnabled} className="size-4" />
        Habilitar integracion fiscal
      </label>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos guardar la configuracion fiscal</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Configuracion fiscal actualizada</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Guardando..." : "Guardar configuracion fiscal"}
      </Button>
    </form>
  );
}

function TextField({
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
    <label className="space-y-2 text-sm">
      <span className="font-medium">{label}</span>
      <input
        name={name}
        defaultValue={defaultValue}
        required
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
        placeholder={placeholder}
      />
    </label>
  );
}

function NumberField({
  label,
  name,
  defaultValue,
}: {
  label: string;
  name: string;
  defaultValue: string;
}) {
  return (
    <label className="space-y-2 text-sm">
      <span className="font-medium">{label}</span>
      <input
        name={name}
        type="number"
        min="1"
        defaultValue={defaultValue}
        required
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </label>
  );
}

function SelectField({
  label,
  name,
  defaultValue,
  options,
}: {
  label: string;
  name: string;
  defaultValue: string;
  options: Array<{ value: string; label: string }>;
}) {
  return (
    <label className="space-y-2 text-sm">
      <span className="font-medium">{label}</span>
      <select
        name={name}
        defaultValue={defaultValue}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </label>
  );
}
