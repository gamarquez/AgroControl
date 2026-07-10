import assert from "node:assert/strict";
import test from "node:test";

import { createCashMovementAction, openCashSessionAction } from "./actions";

test("openCashSessionAction validates opening amount", async () => {
  const formData = new FormData();
  formData.set("cashRegisterCode", "main");
  formData.set("openingAmount", "-1");

  const result = await openCashSessionAction({ error: null, success: null }, formData);

  assert.equal(result.error, "El monto de apertura no puede ser negativo.");
});

test("createCashMovementAction validates concept before calling the API", async () => {
  const formData = new FormData();
  formData.set("cashSessionId", "00000000-0000-4000-8000-000000000001");
  formData.set("movementType", "cash_out");
  formData.set("categoryCode", "ga");
  formData.set("concept", "ok");
  formData.set("paymentMethod", "cash");
  formData.set("amount", "100");

  const result = await createCashMovementAction({ error: null, success: null }, formData);

  assert.equal(result.error, "El concepto debe tener al menos 3 caracteres.");
});
