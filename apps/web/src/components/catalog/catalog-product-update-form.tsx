"use client";

import { useActionState } from "react";

import { updateCatalogProductAction, type CatalogActionState } from "@/app/dashboard/catalog/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type {
  Brand,
  PriceList,
  ProductCategory,
  ProductSummary,
  UnitOfMeasure,
} from "@/lib/api/contracts";

const initialState: CatalogActionState = {
  error: null,
  success: null,
};

export function CatalogProductUpdateForm({
  product,
  categories,
  brands,
  units,
  priceLists,
}: {
  product: ProductSummary;
  categories: ProductCategory[];
  brands: Brand[];
  units: UnitOfMeasure[];
  priceLists: PriceList[];
}) {
  const [state, formAction, isPending] = useActionState(updateCatalogProductAction, initialState);

  return (
    <form action={formAction} className="grid gap-4 rounded-xl border border-border/70 bg-background/70 p-4">
      <input type="hidden" name="productId" value={product.productId} />

      <div className="grid gap-4 md:grid-cols-[1fr_auto] md:items-end">
        <Field label="Nombre" name="name" defaultValue={product.name} required />
        <label className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm">
          <input name="isActive" type="checkbox" className="size-4" defaultChecked={product.isActive} />
          Activo
        </label>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <Field label="Codigo interno" name="internalCode" defaultValue={product.internalCode} required />
        <Field label="SKU" name="sku" defaultValue={product.sku ?? ""} />
        <Field label="Codigo de barras" name="barcode" defaultValue={product.barcode ?? ""} />
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <SelectField
          label="Categoria"
          name="categoryId"
          defaultValue={product.category?.categoryId}
          options={categories.map((category) => ({ value: category.categoryId, label: category.name }))}
        />
        <SelectField
          label="Marca"
          name="brandId"
          defaultValue={product.brand?.brandId}
          options={brands.map((brand) => ({ value: brand.brandId, label: brand.name }))}
        />
        <SelectField
          label="Unidad base"
          name="baseUnitId"
          defaultValue={product.baseUnit.unitId}
          required
          options={units.map((unit) => ({ value: unit.unitId, label: `${unit.name} (${unit.symbol})` }))}
        />
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <NumberField label="Costo" name="costAmount" defaultValue={String(product.currentPrice.costAmount)} required />
        <NumberField label="Margen %" name="marginPercent" defaultValue={String(product.currentPrice.marginPercent ?? 0)} />
        <NumberField label="Precio de venta" name="saleAmount" defaultValue={String(product.currentPrice.saleAmount)} required />
        <Field label="Moneda" name="currencyCode" defaultValue={product.currentPrice.currencyCode} required />
      </div>

      <div className="grid gap-4 md:grid-cols-[1fr_auto] md:items-end">
        <SelectField
          label="Lista de precios"
          name="priceListId"
          defaultValue={product.currentPrice.priceListId}
          options={priceLists.map((priceList) => ({ value: priceList.priceListId, label: priceList.name }))}
        />
        <label className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm">
          <input
            name="allowsFraction"
            type="checkbox"
            className="size-4"
            defaultChecked={product.allowsFraction}
          />
          Permite fraccionado
        </label>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Descripcion" name="description" defaultValue={product.description ?? ""} />
        <Field label="Etiqueta de venta" name="salesUnitLabel" defaultValue={product.salesUnitLabel ?? ""} />
      </div>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos actualizar el producto</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Producto actualizado</AlertTitle>
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
  required = false,
}: {
  label: string;
  name: string;
  defaultValue?: string;
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
        required={required}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </div>
  );
}

function SelectField({
  label,
  name,
  options,
  defaultValue,
  required = false,
}: {
  label: string;
  name: string;
  options: Array<{ value: string; label: string }>;
  defaultValue?: string;
  required?: boolean;
}) {
  return (
    <div className="space-y-2">
      <label className="text-sm font-medium" htmlFor={`${name}-${label}`}>
        {label}
      </label>
      <select
        id={`${name}-${label}`}
        name={name}
        defaultValue={defaultValue ?? ""}
        required={required}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      >
        <option value="">Seleccionar</option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
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
    <div className="space-y-2">
      <label className="text-sm font-medium" htmlFor={`${name}-${label}`}>
        {label}
      </label>
      <input
        id={`${name}-${label}`}
        name={name}
        type="number"
        step="0.01"
        min="0"
        defaultValue={defaultValue}
        required={required}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </div>
  );
}
