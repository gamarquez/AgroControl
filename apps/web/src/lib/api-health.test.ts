import assert from "node:assert/strict";
import test from "node:test";
import { getApiHealth, parseApiHealthResponse } from "./api-health";

test.afterEach(() => {
  delete process.env.AGROCONTROL_API_BASE_URL;
});

test("parseApiHealthResponse accepts the expected API contract", () => {
  const payload = {
    status: "healthy",
    service: "AgroControl.Api",
    version: "1.0.0",
    environment: "Development",
    checkedAt: "2026-07-07T00:00:00.0000000+00:00",
  };

  assert.deepEqual(parseApiHealthResponse(payload), payload);
});

test("parseApiHealthResponse rejects an invalid contract", () => {
  assert.equal(parseApiHealthResponse({ status: "healthy" }), null);
});

test("getApiHealth returns an unhealthy fallback when the contract is invalid", async () => {
  process.env.AGROCONTROL_API_BASE_URL = "https://api.example.com";

  const health = await getApiHealth(
    (async () =>
      ({
        ok: true,
        json: async () => ({ status: "healthy" }),
      }) as Response) as typeof fetch,
  );

  assert.equal(health.status, "unhealthy");
  assert.match(health.message ?? "", /contrato esperado/i);
});

test("getApiHealth returns an unhealthy fallback when the server API base URL is missing", async () => {
  const health = await getApiHealth();

  assert.equal(health.status, "unhealthy");
  assert.match(health.message ?? "", /AGROCONTROL_API_BASE_URL/i);
});
