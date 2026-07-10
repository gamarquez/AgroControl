import assert from "node:assert/strict";
import test from "node:test";

import { loginAction } from "./actions";

test("loginAction validates email and password before calling the API", async () => {
  const formData = new FormData();
  formData.set("email", "invalid-email");
  formData.set("password", "123");

  const result = await loginAction({ error: null }, formData);

  assert.equal(result.error, "Ingresa un email valido.");
});
