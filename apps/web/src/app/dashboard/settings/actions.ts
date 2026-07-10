"use server";

import { z } from "zod";

import { revalidateDashboardPaths, updateOrganizationSettings } from "@/lib/auth/session";

export type SettingsActionState = {
  error: string | null;
  success: string | null;
};

const settingsSchema = z.object({
  legalName: z.string().trim().min(3, "El nombre legal es obligatorio."),
  tradeName: z.string().trim().min(3, "El nombre comercial es obligatorio."),
  taxId: z.string().trim().min(1, "El CUIT es obligatorio."),
  timeZone: z.string().trim().min(1, "La zona horaria es obligatoria."),
  currencyCode: z.string().trim().length(3, "La moneda debe tener 3 caracteres."),
});

export async function updateOrganizationSettingsAction(
  _previousState: SettingsActionState,
  formData: FormData,
): Promise<SettingsActionState> {
  const parsed = settingsSchema.safeParse({
    legalName: formData.get("legalName"),
    tradeName: formData.get("tradeName"),
    taxId: formData.get("taxId"),
    timeZone: formData.get("timeZone"),
    currencyCode: formData.get("currencyCode"),
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos actualizar el comercio.",
      success: null,
    };
  }

  try {
    await updateOrganizationSettings(parsed.data);
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Configuracion actualizada correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos actualizar el comercio.",
      success: null,
    };
  }
}
