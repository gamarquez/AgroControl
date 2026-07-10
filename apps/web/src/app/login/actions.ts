"use server";

import { redirect } from "next/navigation";
import { z } from "zod";

import { loginWithPassword } from "@/lib/auth/session";

export type LoginActionState = {
  error: string | null;
};

const loginSchema = z.object({
  email: z.email("Ingresa un email valido."),
  password: z.string().min(8, "La contrasena debe tener al menos 8 caracteres."),
});

export async function loginAction(
  _previousState: LoginActionState,
  formData: FormData,
): Promise<LoginActionState> {
  const parsed = loginSchema.safeParse({
    email: formData.get("email"),
    password: formData.get("password"),
  });

  if (!parsed.success) {
    return {
      error: parsed.error.issues[0]?.message ?? "Revisa los datos ingresados.",
    };
  }

  try {
    await loginWithPassword(parsed.data.email, parsed.data.password);
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "No fue posible iniciar sesion.",
    };
  }

  redirect("/dashboard");
}
