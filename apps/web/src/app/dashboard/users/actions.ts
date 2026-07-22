"use server";

import { z } from "zod";

import { createUser, revalidateDashboardPaths, updateUser } from "@/lib/auth/session";
import { postgresUuidSchema } from "@/lib/api/contracts";

export type UserActionState = {
  error: string | null;
  success: string | null;
};

const createUserSchema = z.object({
  email: z.email("Ingresa un email valido."),
  displayName: z.string().trim().min(3, "El nombre debe tener al menos 3 caracteres."),
  password: z.string().min(8, "La contrasena debe tener al menos 8 caracteres."),
  mustChangePassword: z.boolean(),
  roleIds: z.array(postgresUuidSchema).min(1, "Selecciona al menos un rol."),
});

const updateUserSchema = z.object({
  userId: postgresUuidSchema,
  displayName: z.string().trim().min(3, "El nombre debe tener al menos 3 caracteres."),
  isActive: z.boolean(),
  isLocked: z.boolean(),
  mustChangePassword: z.boolean(),
  roleIds: z.array(postgresUuidSchema).min(1, "Selecciona al menos un rol."),
});

export async function createUserAction(
  _previousState: UserActionState,
  formData: FormData,
): Promise<UserActionState> {
  const parsed = createUserSchema.safeParse({
    email: formData.get("email"),
    displayName: formData.get("displayName"),
    password: formData.get("password"),
    mustChangePassword: formData.get("mustChangePassword") === "on",
    roleIds: formData.getAll("roleIds"),
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos crear el usuario.",
      success: null,
    };
  }

  try {
    await createUser(parsed.data);
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Usuario creado correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos crear el usuario.",
      success: null,
    };
  }
}

export async function updateUserAction(
  _previousState: UserActionState,
  formData: FormData,
): Promise<UserActionState> {
  const parsed = updateUserSchema.safeParse({
    userId: formData.get("userId"),
    displayName: formData.get("displayName"),
    isActive: formData.get("isActive") === "on",
    isLocked: formData.get("isLocked") === "on",
    mustChangePassword: formData.get("mustChangePassword") === "on",
    roleIds: formData.getAll("roleIds"),
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "No pudimos actualizar el usuario.",
      success: null,
    };
  }

  try {
    await updateUser(parsed.data.userId, {
      displayName: parsed.data.displayName,
      isActive: parsed.data.isActive,
      isLocked: parsed.data.isLocked,
      mustChangePassword: parsed.data.mustChangePassword,
      roleIds: parsed.data.roleIds,
    });
    await revalidateDashboardPaths();
    return {
      error: null,
      success: "Usuario actualizado correctamente.",
    };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No pudimos actualizar el usuario.",
      success: null,
    };
  }
}
