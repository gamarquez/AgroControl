"use client";

import { useActionState } from "react";

import { createCustomerAction, type CustomerActionState } from "@/app/dashboard/customers/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

const initialState: CustomerActionState = {
  error: null,
  success: null,
};

export function CustomerCreateForm() {
  const [state, formAction, isPending] = useActionState(createCustomerAction, initialState);

  return (
    <form action={formAction} className="grid gap-4">
      <div className="grid gap-4 md:grid-cols-2">
        <TextField label="Nombre o razon social" name="displayName" required />
        <TextField label="CUIT / DNI" name="taxId" />
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <TextField label="Telefono" name="phone" />
        <TextField label="Email" name="email" type="email" />
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <TextField label="Direccion" name="address" />
        <NumberField label="Limite de credito" name="creditLimitAmount" defaultValue="0" required />
      </div>

      <TextAreaField label="Notas" name="notes" placeholder="Observaciones comerciales o de cobranza" />

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos crear el cliente</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Cliente listo</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Guardando..." : "Crear cliente"}
      </Button>
    </form>
  );
}

function TextField({
  label,
  name,
  type = "text",
  required = false,
}: {
  label: string;
  name: string;
  type?: string;
  required?: boolean;
}) {
  return (
    <label className="space-y-2 text-sm">
      <span className="font-medium">{label}</span>
      <input
        name={name}
        type={type}
        required={required}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </label>
  );
}

function NumberField({
  label,
  name,
  defaultValue,
  required = false,
}: {
  label: string;
  name: string;
  defaultValue?: string;
  required?: boolean;
}) {
  return (
    <label className="space-y-2 text-sm">
      <span className="font-medium">{label}</span>
      <input
        name={name}
        type="number"
        min="0"
        step="0.01"
        defaultValue={defaultValue}
        required={required}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </label>
  );
}

function TextAreaField({
  label,
  name,
  placeholder,
}: {
  label: string;
  name: string;
  placeholder?: string;
}) {
  return (
    <label className="space-y-2 text-sm">
      <span className="font-medium">{label}</span>
      <textarea
        name={name}
        rows={3}
        placeholder={placeholder}
        className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none transition focus:border-primary"
      />
    </label>
  );
}
