"use server";

import { z } from "zod";

import {
  closeCashSession,
  createCashMovement,
  openCashSession,
  revalidateDashboardPaths,
} from "@/lib/auth/session";
import { postgresUuidSchema } from "@/lib/api/contracts";

export type CashActionState = {
  error: string | null;
  success: string | null;
};

const openSessionSchema = z.object({
  cashRegisterCode: z.string().trim().min(1, "La caja es obligatoria."),
  openingAmount: z.coerce.number().min(0, "El monto de apertura no puede ser negativo."),
  openingNotes: z.string().optional(),
});

const createMovementSchema = z.object({
  cashSessionId: postgresUuidSchema,
  movementType: z.enum(["cash_in", "cash_out"]),
  categoryCode: z.string().trim().min(2, "La categoria debe tener al menos 2 caracteres."),
  concept: z.string().trim().min(3, "El concepto debe tener al menos 3 caracteres."),
  paymentMethod: z.enum(["cash", "transfer", "qr", "card", "account"]),
  amount: z.coerce.number().positive("El importe debe ser mayor a cero."),
  referenceDocument: z.string().optional(),
  notes: z.string().optional(),
});

const closeSessionSchema = z.object({
  cashSessionId: postgresUuidSchema,
  closingAmount: z.coerce.number().min(0, "El monto de cierre no puede ser negativo."),
  closingNotes: z.string().optional(),
});

export async function openCashSessionAction(
  _previousState: CashActionState,
  formData: FormData,
): Promise<CashActionState> {
  const parsed = openSessionSchema.safeParse({
    cashRegisterCode: readRequiredString(formData, "cashRegisterCode"),
    openingAmount: readRequiredString(formData, "openingAmount"),
    openingNotes: readOptionalString(formData, "openingNotes"),
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos abrir la caja.",
      success: null,
    };
  }

  try {
    await openCashSession({
      cashRegisterCode: parsed.data.cashRegisterCode,
      openingAmount: parsed.data.openingAmount,
      openingNotes: parsed.data.openingNotes?.trim() || undefined,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Caja abierta correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos abrir la caja.",
      success: null,
    };
  }
}

export async function createCashMovementAction(
  _previousState: CashActionState,
  formData: FormData,
): Promise<CashActionState> {
  const parsed = createMovementSchema.safeParse({
    cashSessionId: readRequiredString(formData, "cashSessionId"),
    movementType: readRequiredString(formData, "movementType"),
    categoryCode: readRequiredString(formData, "categoryCode"),
    concept: readRequiredString(formData, "concept"),
    paymentMethod: readRequiredString(formData, "paymentMethod"),
    amount: readRequiredString(formData, "amount"),
    referenceDocument: readOptionalString(formData, "referenceDocument"),
    notes: readOptionalString(formData, "notes"),
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos registrar el movimiento de caja.",
      success: null,
    };
  }

  try {
    await createCashMovement({
      cashSessionId: parsed.data.cashSessionId,
      movementType: parsed.data.movementType,
      categoryCode: parsed.data.categoryCode,
      concept: parsed.data.concept,
      paymentMethod: parsed.data.paymentMethod,
      amount: parsed.data.amount,
      referenceDocument: parsed.data.referenceDocument?.trim() || undefined,
      notes: parsed.data.notes?.trim() || undefined,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Movimiento de caja registrado correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos registrar el movimiento de caja.",
      success: null,
    };
  }
}

export async function closeCashSessionAction(
  _previousState: CashActionState,
  formData: FormData,
): Promise<CashActionState> {
  const parsed = closeSessionSchema.safeParse({
    cashSessionId: readRequiredString(formData, "cashSessionId"),
    closingAmount: readRequiredString(formData, "closingAmount"),
    closingNotes: readOptionalString(formData, "closingNotes"),
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos cerrar la caja.",
      success: null,
    };
  }

  try {
    await closeCashSession(parsed.data.cashSessionId, {
      closingAmount: parsed.data.closingAmount,
      closingNotes: parsed.data.closingNotes?.trim() || undefined,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Caja cerrada correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos cerrar la caja.",
      success: null,
    };
  }
}

function readOptionalString(formData: FormData, fieldName: string) {
  const value = formData.get(fieldName);
  return typeof value === "string" ? value : "";
}

function readRequiredString(formData: FormData, fieldName: string) {
  const value = formData.get(fieldName);
  return typeof value === "string" ? value : "";
}
