"use server";

import { z } from "zod";

import {
  createCatalogCategory,
  revalidateDashboardPaths,
  updateCatalogCategory,
} from "@/lib/auth/session";
import { postgresUuidSchema } from "@/lib/api/contracts";

export type CategoryActionState = {
  error: string | null;
  success: string | null;
};

const createSchema = z.object({
  name: z.string().trim().min(2, "El nombre es obligatorio."),
  description: z.string().optional(),
});

const updateSchema = createSchema.extend({
  categoryId: postgresUuidSchema,
  isActive: z.boolean(),
});

export async function createCategoryAction(
  _previousState: CategoryActionState,
  formData: FormData,
): Promise<CategoryActionState> {
  const parsed = createSchema.safeParse({
    name: formData.get("name"),
    description: formData.get("description"),
  });

  if (!parsed.success) {
    return { error: parsed.error.issues[0]?.message ?? "No pudimos crear la categoria.", success: null };
  }

  try {
    await createCatalogCategory({
      name: parsed.data.name,
      description: parsed.data.description?.trim() || undefined,
    });
    await revalidateDashboardPaths();
    return { error: null, success: "Categoria creada correctamente." };
  } catch (error) {
    return { error: error instanceof Error ? error.message : "No pudimos crear la categoria.", success: null };
  }
}

export async function updateCategoryAction(
  _previousState: CategoryActionState,
  formData: FormData,
): Promise<CategoryActionState> {
  const parsed = updateSchema.safeParse({
    categoryId: formData.get("categoryId"),
    name: formData.get("name"),
    description: formData.get("description"),
    isActive: formData.get("isActive") === "on",
  });

  if (!parsed.success) {
    return { error: parsed.error.issues[0]?.message ?? "No pudimos actualizar la categoria.", success: null };
  }

  try {
    await updateCatalogCategory(parsed.data.categoryId, {
      name: parsed.data.name,
      description: parsed.data.description?.trim() || undefined,
      isActive: parsed.data.isActive,
    });
    await revalidateDashboardPaths();
    return { error: null, success: "Categoria actualizada correctamente." };
  } catch (error) {
    return { error: error instanceof Error ? error.message : "No pudimos actualizar la categoria.", success: null };
  }
}
