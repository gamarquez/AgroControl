"use server";

import { z } from "zod";

import {
  createAccountSale,
  createCustomer,
  recordCustomerCreditNote,
  recordCustomerPayment,
  revalidateDashboardPaths,
  updateCustomer,
} from "@/lib/auth/session";

export type CustomerActionState = {
  error: string | null;
  success: string | null;
};

const saleItemSchema = z.object({
  productId: z.string().uuid(),
  quantity: z.number().positive("La cantidad debe ser mayor a cero."),
});

const customerSchema = z.object({
  displayName: z.string().trim().min(3, "El nombre debe tener al menos 3 caracteres."),
  taxId: z.string().optional(),
  phone: z.string().optional(),
  email: z.union([z.email("El email no es valido."), z.literal("")]).optional(),
  address: z.string().optional(),
  creditLimitAmount: z.coerce.number().min(0, "El limite de credito no puede ser negativo."),
  notes: z.string().optional(),
});

const updateCustomerSchema = customerSchema.extend({
  customerId: z.string().uuid(),
  isActive: z.boolean(),
});

const paymentSchema = z.object({
  customerId: z.string().uuid(),
  cashSessionId: z.string().uuid(),
  amount: z.coerce.number().positive("El importe debe ser mayor a cero."),
  notes: z.string().optional(),
});

const creditNoteSchema = z.object({
  customerId: z.string().uuid(),
  amount: z.coerce.number().positive("El importe debe ser mayor a cero."),
  concept: z.string().trim().min(3, "El concepto debe tener al menos 3 caracteres."),
  referenceDocument: z.string().optional(),
  notes: z.string().optional(),
});

const accountSaleSchema = z.object({
  customerId: z.string().uuid(),
  dueDate: z.string().optional(),
  notes: z.string().optional(),
  items: z.array(saleItemSchema).min(1, "Debes agregar al menos un producto."),
});

export async function createCustomerAction(
  _previousState: CustomerActionState,
  formData: FormData,
): Promise<CustomerActionState> {
  const parsed = customerSchema.safeParse(readCustomerForm(formData));

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos crear el cliente.",
      success: null,
    };
  }

  try {
    await createCustomer(toCustomerPayload(parsed.data));
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Cliente creado correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos crear el cliente.",
      success: null,
    };
  }
}

export async function updateCustomerAction(
  _previousState: CustomerActionState,
  formData: FormData,
): Promise<CustomerActionState> {
  const parsed = updateCustomerSchema.safeParse({
    ...readCustomerForm(formData),
    customerId: readRequiredString(formData, "customerId"),
    isActive: formData.get("isActive") === "on",
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos actualizar el cliente.",
      success: null,
    };
  }

  try {
    await updateCustomer(parsed.data.customerId, {
      ...toCustomerPayload(parsed.data),
      isActive: parsed.data.isActive,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Cliente actualizado correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos actualizar el cliente.",
      success: null,
    };
  }
}

export async function recordCustomerPaymentAction(
  _previousState: CustomerActionState,
  formData: FormData,
): Promise<CustomerActionState> {
  const parsed = paymentSchema.safeParse({
    customerId: readRequiredString(formData, "customerId"),
    cashSessionId: readRequiredString(formData, "cashSessionId"),
    amount: readRequiredString(formData, "amount"),
    notes: readOptionalString(formData, "notes"),
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos registrar la cobranza.",
      success: null,
    };
  }

  try {
    await recordCustomerPayment(parsed.data.customerId, {
      cashSessionId: parsed.data.cashSessionId,
      amount: parsed.data.amount,
      notes: parsed.data.notes?.trim() || undefined,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Cobranza registrada correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos registrar la cobranza.",
      success: null,
    };
  }
}

export async function recordCustomerCreditNoteAction(
  _previousState: CustomerActionState,
  formData: FormData,
): Promise<CustomerActionState> {
  const parsed = creditNoteSchema.safeParse({
    customerId: readRequiredString(formData, "customerId"),
    amount: readRequiredString(formData, "amount"),
    concept: readRequiredString(formData, "concept"),
    referenceDocument: readOptionalString(formData, "referenceDocument"),
    notes: readOptionalString(formData, "notes"),
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos registrar la nota de credito.",
      success: null,
    };
  }

  try {
    await recordCustomerCreditNote(parsed.data.customerId, {
      amount: parsed.data.amount,
      concept: parsed.data.concept,
      referenceDocument: parsed.data.referenceDocument?.trim() || undefined,
      notes: parsed.data.notes?.trim() || undefined,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Nota de credito registrada correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos registrar la nota de credito.",
      success: null,
    };
  }
}

export async function createAccountSaleAction(
  _previousState: CustomerActionState,
  formData: FormData,
): Promise<CustomerActionState> {
  const rawItems = readRequiredString(formData, "items");
  let parsedItems: unknown;

  try {
    parsedItems = JSON.parse(rawItems || "[]");
  } catch {
    return {
      error: "No pudimos interpretar los items de la venta a cuenta.",
      success: null,
    };
  }

  const parsed = accountSaleSchema.safeParse({
    customerId: readRequiredString(formData, "customerId"),
    dueDate: readOptionalString(formData, "dueDate"),
    notes: readOptionalString(formData, "notes"),
    items: parsedItems,
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos registrar la venta a cuenta.",
      success: null,
    };
  }

  try {
    const sale = await createAccountSale({
      customerId: parsed.data.customerId,
      items: parsed.data.items,
      dueDate: parsed.data.dueDate?.trim() || undefined,
      notes: parsed.data.notes?.trim() || undefined,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: `Venta a cuenta registrada correctamente. Ticket interno #${sale.ticketNumber}.`,
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos registrar la venta a cuenta.",
      success: null,
    };
  }
}

function readCustomerForm(formData: FormData) {
  return {
    displayName: readRequiredString(formData, "displayName"),
    taxId: readOptionalString(formData, "taxId"),
    phone: readOptionalString(formData, "phone"),
    email: readOptionalString(formData, "email"),
    address: readOptionalString(formData, "address"),
    creditLimitAmount: readRequiredString(formData, "creditLimitAmount"),
    notes: readOptionalString(formData, "notes"),
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

function toCustomerPayload(input: z.infer<typeof customerSchema>) {
  return {
    displayName: input.displayName,
    taxId: input.taxId?.trim() || undefined,
    phone: input.phone?.trim() || undefined,
    email: input.email?.trim() || undefined,
    address: input.address?.trim() || undefined,
    creditLimitAmount: input.creditLimitAmount,
    notes: input.notes?.trim() || undefined,
  };
}
