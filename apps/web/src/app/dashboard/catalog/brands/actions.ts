"use server";

import { z } from "zod";

import {
  createCatalogBrand,
  revalidateDashboardPaths,
  updateCatalogBrand,
} from "@/lib/auth/session";

export type BrandActionState = {
  error: string | null;
  success: string | null;
};

const createSchema = z.object({
  name: z.string().trim().min(2, "El nombre es obligatorio."),
  description: z.string().optional(),
});

const updateSchema = createSchema.extend({
  brandId: z.string().uuid(),
  isActive: z.boolean(),
});

export async function createBrandAction(
  _previousState: BrandActionState,
  formData: FormData,
): Promise<BrandActionState> {
  const parsed = createSchema.safeParse({
    name: formData.get("name"),
    description: formData.get("description"),
  });

  if (!parsed.success) {
    return { error: parsed.error.issues[0]?.message ?? "No pudimos crear la marca.", success: null };
  }

  try {
    await createCatalogBrand({
      name: parsed.data.name,
      description: parsed.data.description?.trim() || undefined,
    });
    await revalidateDashboardPaths();
    return { error: null, success: "Marca creada correctamente." };
  } catch (error) {
    return { error: error instanceof Error ? error.message : "No pudimos crear la marca.", success: null };
  }
}

export async function updateBrandAction(
  _previousState: BrandActionState,
  formData: FormData,
): Promise<BrandActionState> {
  const parsed = updateSchema.safeParse({
    brandId: formData.get("brandId"),
    name: formData.get("name"),
    description: formData.get("description"),
    isActive: formData.get("isActive") === "on",
  });

  if (!parsed.success) {
    return { error: parsed.error.issues[0]?.message ?? "No pudimos actualizar la marca.", success: null };
  }

  try {
    await updateCatalogBrand(parsed.data.brandId, {
      name: parsed.data.name,
      description: parsed.data.description?.trim() || undefined,
      isActive: parsed.data.isActive,
    });
    await revalidateDashboardPaths();
    return { error: null, success: "Marca actualizada correctamente." };
  } catch (error) {
    return { error: error instanceof Error ? error.message : "No pudimos actualizar la marca.", success: null };
  }
}
