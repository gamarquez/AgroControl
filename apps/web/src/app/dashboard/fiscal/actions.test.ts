import assert from "node:assert/strict";
import test from "node:test";

import { queueFiscalDocumentAction, updateFiscalSettingsAction } from "./actions";

test("updateFiscalSettingsAction validates taxpayerId", async () => {
  const formData = new FormData();
  formData.set("provider", "arca_wsfev1");
  formData.set("environment", "homologation");
  formData.set("taxpayerId", "20");
  formData.set("pointOfSale", "1");
  formData.set("serviceName", "wsfe");
  formData.set("defaultDocumentType", "invoice_c");

  const result = await updateFiscalSettingsAction({ error: null, success: null }, formData);

  assert.equal(result.error, "El CUIT fiscal es obligatorio.");
});

test("queueFiscalDocumentAction validates saleId", async () => {
  const formData = new FormData();
  formData.set("saleId", "bad-guid");
  formData.set("documentKind", "invoice");

  const result = await queueFiscalDocumentAction({ error: null, success: null }, formData);

  assert.match(result.error ?? "", /uuid/i);
});
