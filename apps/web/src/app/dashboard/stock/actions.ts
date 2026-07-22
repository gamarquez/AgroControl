"use server";

import { z } from "zod";

import {
  createStockMovement,
  createStockWarehouse,
  recordPhysicalInventoryCount,
  revalidateDashboardPaths,
  updateStockPolicy,
  updateStockWarehouse,
} from "@/lib/auth/session";
import { postgresUuidSchema } from "@/lib/api/contracts";

export type StockActionState = {
  error: string | null;
  success: string | null;
};

const optionalQuantitySchema = z.union([z.coerce.number().min(0), z.nan()]).optional();

const stockPolicySchema = z.object({
  warehouseId: postgresUuidSchema,
  productId: postgresUuidSchema,
  minQuantity: optionalQuantitySchema,
  maxQuantity: optionalQuantitySchema,
  reorderPoint: optionalQuantitySchema,
});

const stockMovementSchema = z.object({
  warehouseId: postgresUuidSchema,
  productId: postgresUuidSchema,
  movementType: z.enum([
    "purchase_inbound",
    "sale_outbound",
    "adjustment_increase",
    "adjustment_decrease",
    "return_inbound",
    "loss",
    "broken",
    "expired",
  ]),
  quantity: z.coerce.number().positive("La cantidad debe ser mayor a cero."),
  reason: z.string().trim().min(3, "El motivo debe tener al menos 3 caracteres."),
  referenceDocument: z.string().optional(),
  notes: z.string().optional(),
});

const physicalCountSchema = z.object({
  warehouseId: postgresUuidSchema,
  productId: postgresUuidSchema,
  countedQuantity: z.coerce.number().min(0, "La cantidad contada no puede ser negativa."),
  reason: z.string().trim().min(3, "El motivo debe tener al menos 3 caracteres."),
  notes: z.string().optional(),
});

const warehouseSchema = z.object({
  warehouseId: postgresUuidSchema.optional(),
  name: z.string().trim().min(3, "El nombre del deposito debe tener al menos 3 caracteres."),
  code: z.string().trim().min(2, "El codigo del deposito debe tener al menos 2 caracteres."),
  isDefault: z.boolean(),
  isActive: z.boolean(),
});

export async function updateStockPolicyAction(
  _previousState: StockActionState,
  formData: FormData,
): Promise<StockActionState> {
  const parsed = stockPolicySchema.safeParse({
    warehouseId: readRequiredString(formData, "warehouseId"),
    productId: readRequiredString(formData, "productId"),
    minQuantity: readOptionalString(formData, "minQuantity") || Number.NaN,
    maxQuantity: readOptionalString(formData, "maxQuantity") || Number.NaN,
    reorderPoint: readOptionalString(formData, "reorderPoint") || Number.NaN,
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos actualizar la politica de stock.",
      success: null,
    };
  }

  const minQuantity = parsed.data.minQuantity;
  const maxQuantity = parsed.data.maxQuantity;

  if (
    minQuantity !== undefined &&
    maxQuantity !== undefined &&
    !Number.isNaN(minQuantity) &&
    !Number.isNaN(maxQuantity) &&
    maxQuantity < minQuantity
  ) {
    return {
      error: "El stock maximo no puede ser menor al stock minimo.",
      success: null,
    };
  }

  try {
    await updateStockPolicy(parsed.data.productId, {
      warehouseId: parsed.data.warehouseId,
      minQuantity: minQuantity === undefined || Number.isNaN(minQuantity) ? undefined : minQuantity,
      maxQuantity: maxQuantity === undefined || Number.isNaN(maxQuantity) ? undefined : maxQuantity,
      reorderPoint:
        parsed.data.reorderPoint === undefined || Number.isNaN(parsed.data.reorderPoint)
          ? undefined
          : parsed.data.reorderPoint,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Politica de stock actualizada correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos actualizar la politica de stock.",
      success: null,
    };
  }
}

export async function createStockMovementAction(
  _previousState: StockActionState,
  formData: FormData,
): Promise<StockActionState> {
  const parsed = stockMovementSchema.safeParse({
    warehouseId: readRequiredString(formData, "warehouseId"),
    productId: readRequiredString(formData, "productId"),
    movementType: readRequiredString(formData, "movementType"),
    quantity: readRequiredString(formData, "quantity"),
    reason: readRequiredString(formData, "reason"),
    referenceDocument: readOptionalString(formData, "referenceDocument"),
    notes: readOptionalString(formData, "notes"),
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos registrar el movimiento.",
      success: null,
    };
  }

  try {
    await createStockMovement({
      warehouseId: parsed.data.warehouseId,
      productId: parsed.data.productId,
      movementType: parsed.data.movementType,
      quantity: parsed.data.quantity,
      reason: parsed.data.reason,
      referenceDocument: parsed.data.referenceDocument?.trim() || undefined,
      notes: parsed.data.notes?.trim() || undefined,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Movimiento de stock registrado correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos registrar el movimiento.",
      success: null,
    };
  }
}

export async function recordPhysicalInventoryCountAction(
  _previousState: StockActionState,
  formData: FormData,
): Promise<StockActionState> {
  const parsed = physicalCountSchema.safeParse({
    warehouseId: readRequiredString(formData, "warehouseId"),
    productId: readRequiredString(formData, "productId"),
    countedQuantity: readRequiredString(formData, "countedQuantity"),
    reason: readRequiredString(formData, "reason"),
    notes: readOptionalString(formData, "notes"),
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos registrar el conteo fisico.",
      success: null,
    };
  }

  try {
    const result = await recordPhysicalInventoryCount({
      warehouseId: parsed.data.warehouseId,
      productId: parsed.data.productId,
      countedQuantity: parsed.data.countedQuantity,
      reason: parsed.data.reason,
      notes: parsed.data.notes?.trim() || undefined,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: `Conteo registrado. Diferencia aplicada: ${result.differenceQuantity.toFixed(3)}.`,
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos registrar el conteo fisico.",
      success: null,
    };
  }
}

export async function upsertWarehouseAction(
  _previousState: StockActionState,
  formData: FormData,
): Promise<StockActionState> {
  const parsed = warehouseSchema.safeParse({
    warehouseId: readOptionalString(formData, "warehouseId") || undefined,
    name: readRequiredString(formData, "name"),
    code: readRequiredString(formData, "code"),
    isDefault: formData.get("isDefault") === "on",
    isActive: formData.get("isActive") === "on",
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos guardar el deposito.",
      success: null,
    };
  }

  try {
    if (parsed.data.warehouseId) {
      await updateStockWarehouse(parsed.data.warehouseId, {
        name: parsed.data.name,
        code: parsed.data.code,
        isDefault: parsed.data.isDefault,
        isActive: parsed.data.isActive,
      });
    } else {
      await createStockWarehouse({
        name: parsed.data.name,
        code: parsed.data.code,
        isDefault: parsed.data.isDefault,
        isActive: parsed.data.isActive,
      });
    }

    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Deposito guardado correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos guardar el deposito.",
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
