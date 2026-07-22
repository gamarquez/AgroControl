import assert from "node:assert/strict";
import test from "node:test";

import { createCatalogProductAction } from "./actions";

test("createCatalogProductAction validates required fields before calling the API", async () => {
  const formData = new FormData();
  formData.set("name", "AB");
  formData.set("internalCode", "");
  formData.set("baseUnitId", "not-a-guid");
  formData.set("costAmount", "-1");
  formData.set("saleAmount", "0");
  formData.set("currencyCode", "AR");

  const result = await createCatalogProductAction({ error: null, success: null }, formData);

  assert.equal(result.error, "Selecciona una unidad base.");
});

test("createCatalogProductAction accepts PostgreSQL UUIDs without RFC variant bits", async () => {
  const formData = new FormData();
  formData.set("name", "AB");
  formData.set("internalCode", "BAL-001");
  formData.set("baseUnitId", "55555555-5555-5555-5555-555555555555");
  formData.set("costAmount", "10");
  formData.set("saleAmount", "15");
  formData.set("currencyCode", "ARS");

  const result = await createCatalogProductAction({ error: null, success: null }, formData);

  assert.equal(result.error, "El nombre debe tener al menos 3 caracteres.");
});
