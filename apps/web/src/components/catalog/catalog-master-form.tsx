"use client";

import { useActionState } from "react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

type ActionState = {
  error: string | null;
  success: string | null;
};

export function CatalogMasterCreateForm({
  action,
  submitLabel,
  title,
  namePlaceholder,
  descriptionPlaceholder,
}: {
  action: (state: ActionState, formData: FormData) => Promise<ActionState>;
  submitLabel: string;
  title: string;
  namePlaceholder?: string;
  descriptionPlaceholder?: string;
}) {
  const [state, formAction, isPending] = useActionState(action, { error: null, success: null });

  return (
    <form action={formAction} className="grid gap-4">
      <div className="grid gap-4 md:grid-cols-2">
        <Field label={title} name="name" placeholder={namePlaceholder} required />
        <Field label="Descripción" name="description" placeholder={descriptionPlaceholder} />
      </div>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos guardar</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Guardado</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Guardando..." : submitLabel}
      </Button>
    </form>
  );
}

export function CatalogMasterUpdateForm({
  action,
  idFieldName,
  idValue,
  name,
  description,
  isActive,
}: {
  action: (state: ActionState, formData: FormData) => Promise<ActionState>;
  idFieldName: string;
  idValue: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}) {
  const [state, formAction, isPending] = useActionState(action, { error: null, success: null });

  return (
    <form action={formAction} className="grid gap-4 rounded-xl border border-border/70 bg-background/70 p-4">
      <input type="hidden" name={idFieldName} value={idValue} />

      <div className="grid gap-4 md:grid-cols-[1fr_auto] md:items-end">
        <Field label="Nombre" name="name" defaultValue={name} required />
        <label className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm">
          <input name="isActive" type="checkbox" className="size-4" defaultChecked={isActive} />
          Activo
        </label>
      </div>

      <Field label="Descripcion" name="description" defaultValue={description ?? ""} />

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos actualizar</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Actualizado</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Actualizando..." : "Guardar cambios"}
      </Button>
    </form>
  );
}

export function CatalogPriceListCreateForm({
  action,
}: {
  action: (state: ActionState, formData: FormData) => Promise<ActionState>;
}) {
  const [state, formAction, isPending] = useActionState(action, { error: null, success: null });

  return (
    <form action={formAction} className="grid gap-4">
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Nombre" name="name" required />
        <Field label="Codigo" name="code" required />
      </div>
      <div className="flex flex-wrap gap-3">
        <label className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm">
          <input name="isDefault" type="checkbox" className="size-4" />
          Lista default
        </label>
        <label className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm">
          <input name="isActive" type="checkbox" className="size-4" defaultChecked />
          Activa
        </label>
      </div>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos guardar</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Guardado</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Guardando..." : "Crear lista"}
      </Button>
    </form>
  );
}

export function CatalogPriceListUpdateForm({
  action,
  priceListId,
  name,
  code,
  isDefault,
  isActive,
}: {
  action: (state: ActionState, formData: FormData) => Promise<ActionState>;
  priceListId: string;
  name: string;
  code: string;
  isDefault: boolean;
  isActive: boolean;
}) {
  const [state, formAction, isPending] = useActionState(action, { error: null, success: null });

  return (
    <form action={formAction} className="grid gap-4 rounded-xl border border-border/70 bg-background/70 p-4">
      <input type="hidden" name="priceListId" value={priceListId} />
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Nombre" name="name" defaultValue={name} required />
        <Field label="Codigo" name="code" defaultValue={code} required />
      </div>
      <div className="flex flex-wrap gap-3">
        <label className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm">
          <input name="isDefault" type="checkbox" className="size-4" defaultChecked={isDefault} />
          Lista default
        </label>
        <label className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm">
          <input name="isActive" type="checkbox" className="size-4" defaultChecked={isActive} />
          Activa
        </label>
      </div>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos actualizar</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Actualizado</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Actualizando..." : "Guardar cambios"}
      </Button>
    </form>
  );
}

function Field({
  label,
  name,
  defaultValue,
  placeholder,
  required = false,
}: {
  label: string;
  name: string;
  defaultValue?: string;
  placeholder?: string;
  required?: boolean;
}) {
  return (
    <div className="space-y-2">
      <label className="text-sm font-medium" htmlFor={`${name}-${label}`}>
        {label}
      </label>
      <input
        id={`${name}-${label}`}
        name={name}
        defaultValue={defaultValue}
        placeholder={placeholder}
        required={required}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </div>
  );
}
