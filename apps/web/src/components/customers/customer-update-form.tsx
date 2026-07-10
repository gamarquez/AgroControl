"use client";

import { useActionState } from "react";

import { updateCustomerAction, type CustomerActionState } from "@/app/dashboard/customers/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { Customer } from "@/lib/api/contracts";

const initialState: CustomerActionState = {
  error: null,
  success: null,
};

export function CustomerUpdateForm({
  customer,
  canEdit,
}: {
  customer: Customer;
  canEdit: boolean;
}) {
  const [state, formAction, isPending] = useActionState(updateCustomerAction, initialState);

  return (
    <form action={formAction} className="grid gap-4">
      <input type="hidden" name="customerId" value={customer.customerId} />

      <div className="grid gap-4 md:grid-cols-2">
        <TextField label="Nombre o razon social" name="displayName" defaultValue={customer.displayName} disabled={!canEdit} required />
        <TextField label="CUIT / DNI" name="taxId" defaultValue={customer.taxId ?? ""} disabled={!canEdit} />
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <TextField label="Telefono" name="phone" defaultValue={customer.phone ?? ""} disabled={!canEdit} />
        <TextField label="Email" name="email" type="email" defaultValue={customer.email ?? ""} disabled={!canEdit} />
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <TextField label="Direccion" name="address" defaultValue={customer.address ?? ""} disabled={!canEdit} />
        <NumberField
          label="Limite de credito"
          name="creditLimitAmount"
          defaultValue={customer.creditLimitAmount.toFixed(2)}
          disabled={!canEdit}
          required
        />
      </div>

      <TextAreaField
        label="Notas"
        name="notes"
        defaultValue={customer.notes ?? ""}
        disabled={!canEdit}
      />

      <label className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm">
        <input name="isActive" type="checkbox" defaultChecked={customer.isActive} disabled={!canEdit} className="size-4" />
        Cliente activo
      </label>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos actualizar el cliente</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Cliente actualizado</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      {canEdit ? (
        <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
          {isPending ? "Guardando..." : "Actualizar cliente"}
        </Button>
      ) : null}
    </form>
  );
}

function TextField({
  label,
  name,
  defaultValue,
  type = "text",
  disabled = false,
  required = false,
}: {
  label: string;
  name: string;
  defaultValue?: string;
  type?: string;
  disabled?: boolean;
  required?: boolean;
}) {
  return (
    <label className="space-y-2 text-sm">
      <span className="font-medium">{label}</span>
      <input
        name={name}
        type={type}
        defaultValue={defaultValue}
        disabled={disabled}
        required={required}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary disabled:cursor-not-allowed disabled:opacity-70"
      />
    </label>
  );
}

function NumberField({
  label,
  name,
  defaultValue,
  disabled = false,
  required = false,
}: {
  label: string;
  name: string;
  defaultValue?: string;
  disabled?: boolean;
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
        disabled={disabled}
        required={required}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary disabled:cursor-not-allowed disabled:opacity-70"
      />
    </label>
  );
}

function TextAreaField({
  label,
  name,
  defaultValue,
  disabled = false,
}: {
  label: string;
  name: string;
  defaultValue?: string;
  disabled?: boolean;
}) {
  return (
    <label className="space-y-2 text-sm">
      <span className="font-medium">{label}</span>
      <textarea
        name={name}
        rows={3}
        defaultValue={defaultValue}
        disabled={disabled}
        className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none transition focus:border-primary disabled:cursor-not-allowed disabled:opacity-70"
      />
    </label>
  );
}
