import assert from "node:assert/strict";
import test from "node:test";

import { createStockMovementAction } from "./actions";

test("createStockMovementAction validates quantity before calling the API", async () => {
  const formData = new FormData();
  formData.set("warehouseId", "00000000-0000-4000-8000-000000000099");
  formData.set("productId", "00000000-0000-4000-8000-000000000001");
  formData.set("movementType", "adjustment_decrease");
  formData.set("quantity", "0");
  formData.set("reason", "aj");

  const result = await createStockMovementAction({ error: null, success: null }, formData);

  assert.equal(result.error, "La cantidad debe ser mayor a cero.");
});
