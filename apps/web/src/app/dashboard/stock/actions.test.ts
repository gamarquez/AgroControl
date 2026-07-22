import assert from "node:assert/strict";
import test from "node:test";

import { createStockMovementAction } from "./actions";

test("createStockMovementAction validates quantity before calling the API", async () => {
  const formData = new FormData();
  formData.set("warehouseId", "77777777-7777-7777-7777-777777777777");
  formData.set("productId", "22222222-2222-2222-2222-222222222222");
  formData.set("movementType", "adjustment_decrease");
  formData.set("quantity", "0");
  formData.set("reason", "aj");

  const result = await createStockMovementAction({ error: null, success: null }, formData);

  assert.equal(result.error, "La cantidad debe ser mayor a cero.");
});
