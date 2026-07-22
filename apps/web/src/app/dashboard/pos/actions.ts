"use server";

import { z } from "zod";

import { createCheckoutSale, revalidateDashboardPaths, returnSaleItems, reverseCashSale } from "@/lib/auth/session";
import { postgresUuidSchema } from "@/lib/api/contracts";

export type PosActionState = {
  error: string | null;
  success: string | null;
};

const saleItemSchema = z.object({
  productId: postgresUuidSchema,
  quantity: z.number().positive("La cantidad debe ser mayor a cero."),
});

const salePaymentSchema = z.object({
  paymentMethod: z.enum(["cash", "transfer", "qr", "card", "account"]),
  amount: z.number().positive("Cada importe de pago debe ser mayor a cero."),
  reference: z.string().optional(),
  providerName: z.string().optional(),
});

const createCheckoutSaleSchema = z.object({
  cashSessionId: postgresUuidSchema.optional().or(z.literal("")),
  customerId: postgresUuidSchema.optional().or(z.literal("")),
  dueDate: z.string().optional(),
  notes: z.string().optional(),
  items: z.array(saleItemSchema).min(1, "Debes agregar al menos un producto."),
  payments: z.array(salePaymentSchema).min(1, "Debes informar al menos un medio de pago."),
});

const reverseCashSaleSchema = z.object({
  saleId: postgresUuidSchema,
  cashSessionId: postgresUuidSchema,
  reversalNotes: z.string().optional(),
});

const returnSaleItemSchema = z.object({
  saleItemId: postgresUuidSchema,
  quantity: z.number().positive("La cantidad debe ser mayor a cero."),
});

const returnSaleSchema = z.object({
  saleId: postgresUuidSchema,
  cashSessionId: postgresUuidSchema.optional().or(z.literal("")),
  notes: z.string().optional(),
  items: z.array(returnSaleItemSchema).min(1, "Debes seleccionar al menos un item para devolver."),
});

export async function createCheckoutSaleAction(
  _previousState: PosActionState,
  formData: FormData,
): Promise<PosActionState> {
  const rawItems = readRequiredString(formData, "items");
  const rawPayments = readRequiredString(formData, "payments");
  let parsedItems: unknown;
  let parsedPayments: unknown;

  try {
    parsedItems = JSON.parse(rawItems || "[]");
  } catch {
    return {
      error: "No pudimos interpretar los items del carrito.",
      success: null,
    };
  }

  try {
    parsedPayments = JSON.parse(rawPayments || "[]");
  } catch {
    return {
      error: "No pudimos interpretar los medios de pago.",
      success: null,
    };
  }

  const parsed = createCheckoutSaleSchema.safeParse({
    cashSessionId: readOptionalString(formData, "cashSessionId"),
    customerId: readOptionalString(formData, "customerId"),
    dueDate: readOptionalString(formData, "dueDate"),
    notes: readOptionalString(formData, "notes"),
    items: parsedItems,
    payments: parsedPayments,
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos registrar la venta.",
      success: null,
    };
  }

  try {
    const sale = await createCheckoutSale({
      cashSessionId: parsed.data.cashSessionId || undefined,
      customerId: parsed.data.customerId || undefined,
      items: parsed.data.items,
      payments: parsed.data.payments.map((payment) => ({
        paymentMethod: payment.paymentMethod,
        amount: payment.amount,
        reference: payment.reference?.trim() || undefined,
        providerName: payment.providerName?.trim() || undefined,
      })),
      dueDate: parsed.data.dueDate?.trim() || undefined,
      notes: parsed.data.notes?.trim() || undefined,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: `Venta registrada correctamente. Ticket interno #${sale.ticketNumber}.`,
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos registrar la venta.",
      success: null,
    };
  }
}

export async function reverseCashSaleAction(
  _previousState: PosActionState,
  formData: FormData,
): Promise<PosActionState> {
  const parsed = reverseCashSaleSchema.safeParse({
    saleId: readRequiredString(formData, "saleId"),
    cashSessionId: readRequiredString(formData, "cashSessionId"),
    reversalNotes: readOptionalString(formData, "reversalNotes"),
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos revertir la venta.",
      success: null,
    };
  }

  try {
    const sale = await reverseCashSale(parsed.data.saleId, {
      cashSessionId: parsed.data.cashSessionId,
      reversalNotes: parsed.data.reversalNotes?.trim() || undefined,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: `Venta revertida correctamente. Ticket #${sale.ticketNumber}.`,
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos revertir la venta.",
      success: null,
    };
  }
}

export async function returnSaleItemsAction(
  _previousState: PosActionState,
  formData: FormData,
): Promise<PosActionState> {
  const rawItems = readRequiredString(formData, "items");
  let parsedItems: unknown;

  try {
    parsedItems = JSON.parse(rawItems || "[]");
  } catch {
    return {
      error: "No pudimos interpretar los items a devolver.",
      success: null,
    };
  }

  const parsed = returnSaleSchema.safeParse({
    saleId: readRequiredString(formData, "saleId"),
    cashSessionId: readOptionalString(formData, "cashSessionId"),
    notes: readOptionalString(formData, "notes"),
    items: parsedItems,
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos registrar la devolucion parcial.",
      success: null,
    };
  }

  try {
    const sale = await returnSaleItems(parsed.data.saleId, {
      cashSessionId: parsed.data.cashSessionId || undefined,
      items: parsed.data.items,
      notes: parsed.data.notes?.trim() || undefined,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: `Devolucion parcial registrada correctamente sobre el ticket #${sale.ticketNumber}.`,
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos registrar la devolucion parcial.",
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
