"use server";

import { z } from "zod";

import {
  createCatalogProduct,
  revalidateDashboardPaths,
  updateCatalogProduct,
} from "@/lib/auth/session";
import { postgresUuidSchema } from "@/lib/api/contracts";

export type CatalogActionState = {
  error: string | null;
  success: string | null;
};

const productSchema = z.object({
  categoryId: postgresUuidSchema.optional().or(z.literal("")),
  brandId: postgresUuidSchema.optional().or(z.literal("")),
  baseUnitId: z.string().refine(
    (value) => postgresUuidSchema.safeParse(value).success,
    "Selecciona una unidad base.",
  ),
  name: z.string().trim().min(3, "El nombre debe tener al menos 3 caracteres."),
  description: z.string().optional(),
  internalCode: z.string().trim().min(1, "El codigo interno es obligatorio."),
  sku: z.string().optional(),
  barcode: z.string().optional(),
  allowsFraction: z.boolean(),
  salesUnitLabel: z.string().optional(),
  costAmount: z.coerce.number().min(0, "El costo no puede ser negativo."),
  marginPercent: z.union([z.coerce.number().min(0), z.nan()]).optional(),
  saleAmount: z.coerce.number().min(0, "El precio no puede ser negativo."),
  currencyCode: z.string().trim().length(3, "La moneda debe tener 3 caracteres."),
  priceListId: postgresUuidSchema.optional().or(z.literal("")),
});

const updateProductSchema = productSchema.extend({
  productId: postgresUuidSchema,
  isActive: z.boolean(),
});

export async function createCatalogProductAction(
  _previousState: CatalogActionState,
  formData: FormData,
): Promise<CatalogActionState> {
  const parsed = productSchema.safeParse(readProductForm(formData));

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos crear el producto.",
      success: null,
    };
  }

  try {
    await createCatalogProduct(toProductPayload(parsed.data));
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Producto creado correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos crear el producto.",
      success: null,
    };
  }
}

export async function updateCatalogProductAction(
  _previousState: CatalogActionState,
  formData: FormData,
): Promise<CatalogActionState> {
  const parsed = updateProductSchema.safeParse({
    ...readProductForm(formData),
    productId: formData.get("productId"),
    isActive: formData.get("isActive") === "on",
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos actualizar el producto.",
      success: null,
    };
  }

  try {
    await updateCatalogProduct(parsed.data.productId, {
      ...toProductPayload(parsed.data),
      isActive: parsed.data.isActive,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Producto actualizado correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos actualizar el producto.",
      success: null,
    };
  }
}

function readProductForm(formData: FormData) {
  return {
    categoryId: readOptionalString(formData, "categoryId"),
    brandId: readOptionalString(formData, "brandId"),
    baseUnitId: readRequiredString(formData, "baseUnitId"),
    name: readRequiredString(formData, "name"),
    description: readOptionalString(formData, "description"),
    internalCode: readRequiredString(formData, "internalCode"),
    sku: readOptionalString(formData, "sku"),
    barcode: readOptionalString(formData, "barcode"),
    allowsFraction: formData.get("allowsFraction") === "on",
    salesUnitLabel: readOptionalString(formData, "salesUnitLabel"),
    costAmount: readRequiredString(formData, "costAmount"),
    marginPercent: readOptionalString(formData, "marginPercent") || Number.NaN,
    saleAmount: readRequiredString(formData, "saleAmount"),
    currencyCode: readRequiredString(formData, "currencyCode"),
    priceListId: readOptionalString(formData, "priceListId"),
  };
}

function readOptionalString(formData: FormData, fieldName: string) {
  const value = formData.get(fieldName);
  return typeof value === "string" ? value : "";
}

function readRequiredString(formData: FormData, fieldName: string) {
  const value = formData.get(fieldName);
  return typeof value === "string" ? value : "";
}

function toProductPayload(input: z.infer<typeof productSchema>) {
  return {
    categoryId: input.categoryId || undefined,
    brandId: input.brandId || undefined,
    baseUnitId: input.baseUnitId,
    name: input.name,
    description: input.description?.trim() || undefined,
    internalCode: input.internalCode,
    sku: input.sku?.trim() || undefined,
    barcode: input.barcode?.trim() || undefined,
    allowsFraction: input.allowsFraction,
    salesUnitLabel: input.salesUnitLabel?.trim() || undefined,
    costAmount: input.costAmount,
    marginPercent: Number.isNaN(input.marginPercent) ? undefined : input.marginPercent,
    saleAmount: input.saleAmount,
    currencyCode: input.currencyCode.toUpperCase(),
    priceListId: input.priceListId || undefined,
  };
}
