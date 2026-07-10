import assert from "node:assert/strict";
import test from "node:test";

import { createCheckoutSaleAction, returnSaleItemsAction, reverseCashSaleAction } from "./actions";

test("createCheckoutSaleAction validates that the cart contains at least one item", async () => {
  const formData = new FormData();
  formData.set("cashSessionId", "00000000-0000-4000-8000-000000000001");
  formData.set("items", "[]");
  formData.set("payments", JSON.stringify([{ paymentMethod: "cash", amount: 1 }]));

  const result = await createCheckoutSaleAction({ error: null, success: null }, formData);

  assert.equal(result.error, "Debes agregar al menos un producto.");
});

test("reverseCashSaleAction validates saleId", async () => {
  const formData = new FormData();
  formData.set("saleId", "not-a-guid");
  formData.set("cashSessionId", "00000000-0000-4000-8000-000000000001");

  const result = await reverseCashSaleAction({ error: null, success: null }, formData);

  assert.match(result.error ?? "", /uuid/i);
});

test("returnSaleItemsAction validates that at least one item is selected", async () => {
  const formData = new FormData();
  formData.set("saleId", "00000000-0000-4000-8000-000000000001");
  formData.set("items", "[]");

  const result = await returnSaleItemsAction({ error: null, success: null }, formData);

  assert.equal(result.error, "Debes seleccionar al menos un item para devolver.");
});
