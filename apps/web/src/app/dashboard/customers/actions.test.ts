import assert from "node:assert/strict";
import test from "node:test";

import {
  createAccountSaleAction,
  createCustomerAction,
  recordCustomerCreditNoteAction,
  recordCustomerPaymentAction,
} from "./actions";

test("createCustomerAction validates display name length", async () => {
  const formData = new FormData();
  formData.set("displayName", "AB");
  formData.set("creditLimitAmount", "0");

  const result = await createCustomerAction({ error: null, success: null }, formData);

  assert.equal(result.error, "El nombre debe tener al menos 3 caracteres.");
});

test("recordCustomerPaymentAction validates positive amount", async () => {
  const formData = new FormData();
  formData.set("customerId", "00000000-0000-4000-8000-000000000001");
  formData.set("cashSessionId", "00000000-0000-4000-8000-000000000002");
  formData.set("amount", "0");

  const result = await recordCustomerPaymentAction({ error: null, success: null }, formData);

  assert.equal(result.error, "El importe debe ser mayor a cero.");
});

test("recordCustomerCreditNoteAction validates concept length", async () => {
  const formData = new FormData();
  formData.set("customerId", "00000000-0000-4000-8000-000000000001");
  formData.set("amount", "10");
  formData.set("concept", "AB");

  const result = await recordCustomerCreditNoteAction({ error: null, success: null }, formData);

  assert.equal(result.error, "El concepto debe tener al menos 3 caracteres.");
});

test("createAccountSaleAction validates that the cart contains at least one item", async () => {
  const formData = new FormData();
  formData.set("customerId", "00000000-0000-4000-8000-000000000001");
  formData.set("items", "[]");

  const result = await createAccountSaleAction({ error: null, success: null }, formData);

  assert.equal(result.error, "Debes agregar al menos un producto.");
});
