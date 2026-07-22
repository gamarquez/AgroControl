"use client";

import { useActionState, useState } from "react";

import { createCatalogProductAction, type CatalogActionState } from "@/app/dashboard/catalog/actions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type {
  Brand,
  PriceList,
  ProductCategory,
  UnitOfMeasure,
} from "@/lib/api/contracts";

const initialState: CatalogActionState = {
  error: null,
  success: null,
};

export function CatalogProductCreateForm({
  categories,
  brands,
  units,
  priceLists,
}: {
  categories: ProductCategory[];
  brands: Brand[];
  units: UnitOfMeasure[];
  priceLists: PriceList[];
}) {
  const [state, formAction, isPending] = useActionState(createCatalogProductAction, initialState);
  const defaultPriceList = priceLists.find((priceList) => priceList.isDefault) ?? priceLists[0];
  const [costAmount, setCostAmount] = useState("0");
  const [marginPercent, setMarginPercent] = useState("0");
  const [saleAmount, setSaleAmount] = useState("0.00");
  const [automaticPrice, setAutomaticPrice] = useState(true);

  const suggestedPrice = calculateSuggestedPrice(costAmount, marginPercent);

  const effectiveSaleAmount = automaticPrice ? suggestedPrice : saleAmount;

  return (
    <form action={formAction} className="grid gap-4">
      <div className="grid gap-4 md:grid-cols-2">
        <TextField label="Nombre" name="name" placeholder="Balanceado Premium 20 kg" required />
        <TextField label="Codigo interno" name="internalCode" placeholder="BAL-0001" required />
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <TextField label="SKU" name="sku" placeholder="SKU-001" />
        <TextField label="Codigo de barras" name="barcode" placeholder="7790000000012" />
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <SelectField
          label="Categoria"
          name="categoryId"
          options={categories.map((category) => ({ value: category.categoryId, label: category.name }))}
        />
        <SelectField
          label="Marca"
          name="brandId"
          options={brands.map((brand) => ({ value: brand.brandId, label: brand.name }))}
        />
        <SelectField
          label="Unidad base"
          name="baseUnitId"
          required
          options={units.map((unit) => ({
            value: unit.unitId,
            label: `${unit.name} (${unit.symbol})`,
          }))}
        />
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <TextField label="Descripcion" name="description" placeholder="Uso bovino o mascotas rurales" />
        <TextField label="Etiqueta de venta" name="salesUnitLabel" placeholder="bolsa, kg, unidad" />
      </div>

      <fieldset className="grid gap-4 rounded-xl border bg-muted/20 p-4">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <legend className="font-semibold">Costo y precio de venta</legend>
            <p className="text-sm text-muted-foreground">El precio se calcula con costo + margen y podés ajustarlo manualmente.</p>
          </div>
          <label className="flex items-center gap-2 text-sm font-medium">
            <input
              type="checkbox"
              checked={automaticPrice}
              onChange={(event) => setAutomaticPrice(event.target.checked)}
              className="size-4"
            />
            Calcular automáticamente
          </label>
        </div>
        <div className="grid gap-4 md:grid-cols-4">
        <NumberField
          label="Costo"
          name="costAmount"
          defaultValue="0"
          onValueChange={setCostAmount}
          required
        />
        <NumberField
          label="Margen %"
          name="marginPercent"
          defaultValue="0"
          onValueChange={setMarginPercent}
        />
        <NumberField
          label="Precio de venta"
          name="saleAmount"
          value={effectiveSaleAmount}
          onValueChange={(value) => {
            setSaleAmount(value);
            setAutomaticPrice(false);
          }}
          required
        />
        <TextField label="Moneda" name="currencyCode" defaultValue="ARS" required />
        </div>
        <p className="text-sm text-muted-foreground">
          Precio sugerido: <strong className="font-semibold text-foreground">$ {suggestedPrice}</strong>
        </p>
      </fieldset>

      <div className="grid gap-4 md:grid-cols-[1fr_auto] md:items-end">
        <SelectField
          label="Lista de precios"
          name="priceListId"
          defaultValue={defaultPriceList?.priceListId}
          options={priceLists.map((priceList) => ({
            value: priceList.priceListId,
            label: `${priceList.name}${priceList.isDefault ? " (Default)" : ""}`,
          }))}
        />
        <label className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm">
          <input name="allowsFraction" type="checkbox" className="size-4" />
          Permite fraccionado
        </label>
      </div>

      {state.error ? (
        <Alert variant="destructive">
          <AlertTitle>No pudimos crear el producto</AlertTitle>
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      ) : null}

      {state.success ? (
        <Alert>
          <AlertTitle>Producto listo</AlertTitle>
          <AlertDescription>{state.success}</AlertDescription>
        </Alert>
      ) : null}

      <Button type="submit" className="w-full md:w-fit" disabled={isPending}>
        {isPending ? "Guardando..." : "Crear producto"}
      </Button>
    </form>
  );
}

function calculateSuggestedPrice(costAmount: string, marginPercent: string) {
  const cost = Number(costAmount || "0");
  const margin = Number(marginPercent || "0");

  if (Number.isNaN(cost) || Number.isNaN(margin)) {
    return "0.00";
  }

  return (cost * (1 + margin / 100)).toFixed(2);
}

function TextField({
  label,
  name,
  placeholder,
  defaultValue,
  required = false,
}: {
  label: string;
  name: string;
  placeholder?: string;
  defaultValue?: string;
  required?: boolean;
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
        required={required}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
        placeholder={placeholder}
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
      <label className="text-sm font-medium" htmlFor={name}>
        {label}
      </label>
      <select
        id={name}
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
  value,
  onValueChange,
  required = false,
}: {
  label: string;
  name: string;
  defaultValue?: string;
  value?: string;
  onValueChange?: (value: string) => void;
  required?: boolean;
}) {
  return (
    <div className="space-y-2">
      <label className="text-sm font-medium" htmlFor={name}>
        {label}
      </label>
      <input
        id={name}
        name={name}
        type="number"
        step="0.01"
        min="0"
        defaultValue={defaultValue}
        value={value}
        required={required}
        onChange={(event) => onValueChange?.(event.target.value)}
        className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition focus:border-primary"
      />
    </div>
  );
}
