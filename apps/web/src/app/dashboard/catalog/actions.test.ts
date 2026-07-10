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
