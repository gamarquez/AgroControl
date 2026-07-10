"use server";

import { z } from "zod";

import {
  createCatalogPriceList,
  revalidateDashboardPaths,
  updateCatalogPriceList,
} from "@/lib/auth/session";

export type PriceListActionState = {
  error: string | null;
  success: string | null;
};

const createSchema = z.object({
  name: z.string().trim().min(2, "El nombre es obligatorio."),
  code: z.string().trim().min(2, "El codigo es obligatorio."),
  isDefault: z.boolean(),
  isActive: z.boolean(),
});

const updateSchema = createSchema.extend({
  priceListId: z.string().uuid(),
});

export async function createPriceListAction(
  _previousState: PriceListActionState,
  formData: FormData,
): Promise<PriceListActionState> {
  const parsed = createSchema.safeParse({
    name: formData.get("name"),
    code: formData.get("code"),
    isDefault: formData.get("isDefault") === "on",
    isActive: formData.get("isActive") === "on",
  });

  if (!parsed.success) {
    return { error: parsed.error.issues[0]?.message ?? "No pudimos crear la lista.", success: null };
  }

  try {
    await createCatalogPriceList(parsed.data);
    await revalidateDashboardPaths();
    return { error: null, success: "Lista de precios creada correctamente." };
  } catch (error) {
    return { error: error instanceof Error ? error.message : "No pudimos crear la lista.", success: null };
  }
}

export async function updatePriceListAction(
  _previousState: PriceListActionState,
  formData: FormData,
): Promise<PriceListActionState> {
  const parsed = updateSchema.safeParse({
    priceListId: formData.get("priceListId"),
    name: formData.get("name"),
    code: formData.get("code"),
    isDefault: formData.get("isDefault") === "on",
    isActive: formData.get("isActive") === "on",
  });

  if (!parsed.success) {
    return { error: parsed.error.issues[0]?.message ?? "No pudimos actualizar la lista.", success: null };
  }

  try {
    await updateCatalogPriceList(parsed.data.priceListId, parsed.data);
    await revalidateDashboardPaths();
    return { error: null, success: "Lista de precios actualizada correctamente." };
  } catch (error) {
    return { error: error instanceof Error ? error.message : "No pudimos actualizar la lista.", success: null };
  }
}
