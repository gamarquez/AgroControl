"use server";

import { z } from "zod";

import {
  createFiscalDocument,
  probeFiscalConnectivity,
  revalidateDashboardPaths,
  updateFiscalSettings,
} from "@/lib/auth/session";

export type FiscalActionState = {
  error: string | null;
  success: string | null;
};

const fiscalSettingsSchema = z.object({
  provider: z.enum(["disabled", "arca_wsfev1"]),
  environment: z.enum(["homologation", "production"]),
  taxpayerId: z.string().trim().min(11, "El CUIT fiscal es obligatorio."),
  pointOfSale: z.coerce.number().int().min(1, "El punto de venta debe ser mayor a cero."),
  serviceName: z.string().trim().min(1, "El servicio fiscal es obligatorio."),
  defaultDocumentType: z.string().trim().min(1, "El tipo de comprobante por defecto es obligatorio."),
  isEnabled: z.boolean(),
});

const queueFiscalDocumentSchema = z.object({
  saleId: z.string().uuid(),
  documentKind: z.enum(["invoice", "credit_note"]),
});

export async function updateFiscalSettingsAction(
  _previousState: FiscalActionState,
  formData: FormData,
): Promise<FiscalActionState> {
  const parsed = fiscalSettingsSchema.safeParse({
    provider: readRequiredString(formData, "provider"),
    environment: readRequiredString(formData, "environment"),
    taxpayerId: readRequiredString(formData, "taxpayerId"),
    pointOfSale: readRequiredString(formData, "pointOfSale"),
    serviceName: readRequiredString(formData, "serviceName"),
    defaultDocumentType: readRequiredString(formData, "defaultDocumentType"),
    isEnabled: formData.get("isEnabled") === "on",
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos guardar la configuracion fiscal.",
      success: null,
    };
  }

  try {
    await updateFiscalSettings(parsed.data);
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Configuracion fiscal actualizada correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos guardar la configuracion fiscal.",
      success: null,
    };
  }
}

export async function probeFiscalConnectivityAction(
  _previousState: FiscalActionState,
  _formData: FormData,
): Promise<FiscalActionState> {
  try {
    const probe = await probeFiscalConnectivity();
    await revalidateDashboardPaths();
    return {
      error: probe.isReachable ? null : probe.summary,
      success: probe.isReachable ? probe.summary : null,
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos ejecutar la prueba fiscal.",
      success: null,
    };
  }
}

export async function queueFiscalDocumentAction(
  _previousState: FiscalActionState,
  formData: FormData,
): Promise<FiscalActionState> {
  const parsed = queueFiscalDocumentSchema.safeParse({
    saleId: readRequiredString(formData, "saleId"),
    documentKind: readRequiredString(formData, "documentKind"),
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos preparar el documento fiscal.",
      success: null,
    };
  }

  try {
    const document = await createFiscalDocument(parsed.data.saleId, {
      documentKind: parsed.data.documentKind,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: `Documento fiscal en cola para ticket #${document.ticketNumber}.`,
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos preparar el documento fiscal.",
      success: null,
    };
  }
}

function readRequiredString(formData: FormData, fieldName: string) {
  const value = formData.get(fieldName);
  return typeof value === "string" ? value : "";
}
