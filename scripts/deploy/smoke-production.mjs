import assert from "node:assert/strict";

const apiBaseUrl = process.env.DEPLOY_API_BASE_URL?.trim();
const appBaseUrl = process.env.DEPLOY_APP_BASE_URL?.trim();
const adminEmail = process.env.DEPLOY_ADMIN_EMAIL?.trim();
const adminPassword = process.env.DEPLOY_ADMIN_PASSWORD?.trim();

if (!apiBaseUrl) {
  throw new Error("Set DEPLOY_API_BASE_URL before running the production smoke.");
}

if (!adminEmail || !adminPassword) {
  throw new Error("Set DEPLOY_ADMIN_EMAIL and DEPLOY_ADMIN_PASSWORD before running the production smoke.");
}

const health = await getJson(new URL("/health", ensureTrailingSlash(apiBaseUrl)));
assert.equal(health.status, "healthy");

const ready = await getJson(new URL("/health/ready", ensureTrailingSlash(apiBaseUrl)));
assert.equal(ready.status, "healthy");

const session = await postJson(new URL("/api/v1/auth/login", ensureTrailingSlash(apiBaseUrl)), {
  email: adminEmail,
  password: adminPassword,
});

assert.ok(session.tokens?.accessToken, "Expected access token from login.");

const me = await getJson(new URL("/api/v1/auth/me", ensureTrailingSlash(apiBaseUrl)), {
  headers: {
    Authorization: `Bearer ${session.tokens.accessToken}`,
  },
});

assert.equal(me.email, adminEmail);

const catalog = await getJson(new URL("/api/v1/catalog/products", ensureTrailingSlash(apiBaseUrl)), {
  headers: {
    Authorization: `Bearer ${session.tokens.accessToken}`,
  },
});

assert.ok(Array.isArray(catalog.items), "Expected catalog list payload.");

const stock = await getJson(new URL("/api/v1/stock/products", ensureTrailingSlash(apiBaseUrl)), {
  headers: {
    Authorization: `Bearer ${session.tokens.accessToken}`,
  },
});

assert.ok(Array.isArray(stock.items), "Expected stock list payload.");

if (appBaseUrl) {
  const response = await fetch(appBaseUrl, { redirect: "follow" });
  assert.ok(response.ok, "Expected the deployed frontend to respond.");
}

console.log("Production smoke completed successfully.");

async function getJson(url, options = {}) {
  const response = await fetch(url, {
    headers: {
      Accept: "application/json",
      ...(options.headers ?? {}),
    },
  });

  if (!response.ok) {
    throw new Error(`Unexpected status ${response.status} for ${url}.`);
  }

  return await response.json();
}

async function postJson(url, body) {
  const response = await fetch(url, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    throw new Error(`Unexpected status ${response.status} for ${url}.`);
  }

  return await response.json();
}

function ensureTrailingSlash(value) {
  return value.endsWith("/") ? value : `${value}/`;
}
