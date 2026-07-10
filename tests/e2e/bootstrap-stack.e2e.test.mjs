import assert from "node:assert/strict";
import { execFile as execFileCallback } from "node:child_process";
import test from "node:test";
import { promisify } from "node:util";
import { formatProcessError, startManagedProcess, stopManagedProcess, waitForJson, waitForText } from "../../scripts/qa/process-utils.mjs";

const execFile = promisify(execFileCallback);

test("frontend and API complete admin flow works against PostgreSQL local", async (t) => {
  const disposers = [];
  const postgresConnectionString = "Host=127.0.0.1;Port=5432;Database=agrocontrol;Username=agrocontrol;Password=agrocontrol_dev_password";
  const adminEmail = "admin-e2e@agrocontrol.local";
  const adminPassword = "AdminE2E123!";

  t.after(async () => {
    await Promise.allSettled(disposers.map((dispose) => dispose()));
  });

  if (!(await canUseDockerCompose())) {
    await runShellOnlyFlow(disposers);
    return;
  }

  await runCommand("docker", ["compose", "up", "-d", "postgres"]);
  disposers.push(() => runCommand("docker", ["compose", "down"]));
  await waitForPostgres();

  await runPwshScript("scripts/deploy/apply-supabase-migrations.ps1", [
    "-ConnectionString",
    postgresConnectionString,
  ]);
  await runPwshScript("scripts/deploy/bootstrap-admin-production.ps1", [
    "-ConnectionString",
    postgresConnectionString,
    "-Email",
    adminEmail,
    "-Password",
    adminPassword,
    "-DisplayName",
    "Administrador E2E",
  ]);

  const api = startManagedProcess({
    command: "dotnet",
    args: [
      "run",
      "--no-build",
      "--project",
      "apps/api/src/AgroControl.Api/AgroControl.Api.csproj",
      "--urls",
      "http://127.0.0.1:5080",
    ],
    cwd: process.cwd(),
    env: {
      POSTGRES_CONNECTION_STRING: postgresConnectionString,
      Auth__SigningKey: "development-signing-key-change-me-please-for-e2e",
      Auth__Issuer: "http://127.0.0.1:5080",
      Auth__Audience: "http://127.0.0.1:3100",
      AllowedHosts: "127.0.0.1;localhost",
      Cors__AllowedOrigins: "http://127.0.0.1:3100,http://127.0.0.1:3001",
    },
    name: "api",
  });

  disposers.push(() => stopManagedProcess(api));

  try {
    await waitForJson(
      "http://127.0.0.1:5080/health/ready",
      (value) => value?.status === "healthy" && value?.service === "AgroControl.Api",
    );

    await waitForJson(
      "http://127.0.0.1:5080/health",
      (value) => value?.status === "healthy" && value?.service === "AgroControl.Api",
    );
  } catch (error) {
    throw formatProcessError(
      error instanceof Error ? error.message : "The API did not become ready for the E2E test.",
      api,
    );
  }

  const web = startManagedProcess({
    command: "npm.cmd",
    args: ["run", "dev:test", "--workspace", "@agrocontrol/web"],
    cwd: process.cwd(),
    env: {
      AGROCONTROL_API_BASE_URL: "http://127.0.0.1:5080",
    },
    name: "web",
  });

  disposers.push(() => stopManagedProcess(web));

  let html;

  try {
    html = await waitForText(
      "http://127.0.0.1:3100/login",
      (value) =>
        value.includes("Incremento 2") &&
        value.includes("Identidad propia para AgroControl"),
    );
  } catch (error) {
    throw formatProcessError(
      error instanceof Error ? error.message : "The frontend did not render the login shell.",
      web,
    );
  }

  assert.match(html, /Incremento 2/i);
  assert.match(html, /Identidad propia para AgroControl/i);

  const authSession = await postJson("http://127.0.0.1:5080/api/v1/auth/login", {
    email: adminEmail,
    password: adminPassword,
  });

  assert.ok(authSession.tokens?.accessToken, "Expected JWT access token.");

  const accessToken = authSession.tokens.accessToken;
  const uniqueSuffix = Date.now().toString();

  const category = await postJson(
    "http://127.0.0.1:5080/api/v1/catalog/categories",
    {
      name: `Categoria E2E ${uniqueSuffix}`,
      description: "Categoria de prueba end to end",
    },
    accessToken,
  );

  const brand = await postJson(
    "http://127.0.0.1:5080/api/v1/catalog/brands",
    {
      name: `Marca E2E ${uniqueSuffix}`,
      description: "Marca de prueba end to end",
    },
    accessToken,
  );

  const units = await getJson("http://127.0.0.1:5080/api/v1/catalog/units", accessToken);
  const priceLists = await getJson("http://127.0.0.1:5080/api/v1/catalog/price-lists", accessToken);
  const kilograms = units.items.find((unit) => unit.code === "kg");
  const defaultPriceList = priceLists.items.find((priceList) => priceList.isDefault);

  assert.ok(kilograms, "Expected default kilogram unit.");
  assert.ok(defaultPriceList, "Expected default price list.");

  const product = await postJson(
    "http://127.0.0.1:5080/api/v1/catalog/products",
    {
      categoryId: category.categoryId,
      brandId: brand.brandId,
      baseUnitId: kilograms.unitId,
      name: `Balanceado E2E ${uniqueSuffix}`,
      description: "Producto de prueba end to end",
      internalCode: `E2E-${uniqueSuffix}`,
      sku: `SKU-${uniqueSuffix}`,
      barcode: `BAR-${uniqueSuffix}`,
      allowsFraction: true,
      salesUnitLabel: "kg",
      costAmount: 100,
      marginPercent: 25,
      saleAmount: 125,
      currencyCode: "ARS",
      priceListId: defaultPriceList.priceListId,
    },
    accessToken,
  );

  const movement = await postJson(
    "http://127.0.0.1:5080/api/v1/stock/movements",
    {
      productId: product.productId,
      movementType: "purchase_inbound",
      quantity: 5.5,
      reason: "Ingreso inicial E2E",
      referenceDocument: `REM-${uniqueSuffix}`,
      notes: "Carga automatizada para prueba",
    },
    accessToken,
  );

  assert.equal(movement.resultingQuantity, 5.5);

  const cashSession = await postJson(
    "http://127.0.0.1:5080/api/v1/cash/sessions/open",
    {
      cashRegisterCode: "main",
      openingAmount: 1000,
      openingNotes: "Apertura E2E",
    },
    accessToken,
  );

  const sale = await postJson(
    "http://127.0.0.1:5080/api/v1/pos/sales/cash",
    {
      cashSessionId: cashSession.cashSessionId,
      items: [
        {
          productId: product.productId,
          quantity: 1.5,
        },
      ],
      notes: "Venta E2E",
    },
    accessToken,
  );

  assert.equal(sale.items.length, 1);
  assert.equal(sale.totalAmount, 187.5);

  const stock = await getJson(
    `http://127.0.0.1:5080/api/v1/stock/products?search=${encodeURIComponent(`E2E-${uniqueSuffix}`)}`,
    accessToken,
  );

  assert.equal(stock.items.length, 1);
  assert.equal(stock.items[0].productId, product.productId);
  assert.equal(stock.items[0].onHandQuantity, 4);

  const cashOverview = await getJson("http://127.0.0.1:5080/api/v1/cash/overview", accessToken);
  assert.equal(cashOverview.currentSession.cashSessionId, cashSession.cashSessionId);
  assert.equal(cashOverview.currentSession.currentBalance, 1187.5);

  const sales = await getJson("http://127.0.0.1:5080/api/v1/pos/sales", accessToken);
  assert.equal(sales.items.length, 1);
  assert.equal(sales.items[0].saleId, sale.saleId);
  assert.equal(sales.items[0].status, "confirmed");

  const reversedSale = await postJson(
    `http://127.0.0.1:5080/api/v1/pos/sales/${sale.saleId}/reverse`,
    {
      cashSessionId: cashSession.cashSessionId,
      reversalNotes: "Reversa E2E",
    },
    accessToken,
  );

  assert.equal(reversedSale.status, "reversed");

  const reversedStock = await getJson(
    `http://127.0.0.1:5080/api/v1/stock/products?search=${encodeURIComponent(`E2E-${uniqueSuffix}`)}`,
    accessToken,
  );

  assert.equal(reversedStock.items[0].onHandQuantity, 5.5);

  const reversedCashOverview = await getJson("http://127.0.0.1:5080/api/v1/cash/overview", accessToken);
  assert.equal(reversedCashOverview.currentSession.currentBalance, 1000);

  const reversedSales = await getJson("http://127.0.0.1:5080/api/v1/pos/sales", accessToken);
  assert.equal(reversedSales.items[0].status, "reversed");

  const customer = await postJson(
    "http://127.0.0.1:5080/api/v1/customers",
    {
      displayName: `Cliente E2E ${uniqueSuffix}`,
      taxId: `20${uniqueSuffix.slice(-8)}`,
      phone: "3410000000",
      email: `cliente-${uniqueSuffix}@agrocontrol.local`,
      address: "Ruta 9 km 10",
      creditLimitAmount: 5000,
      notes: "Cliente de prueba para cuenta corriente",
    },
    accessToken,
  );

  const accountSale = await postJson(
    "http://127.0.0.1:5080/api/v1/pos/sales/account",
    {
      customerId: customer.customerId,
      items: [
        {
          productId: product.productId,
          quantity: 1,
        },
      ],
      dueDate: "2026-07-31",
      notes: "Venta a cuenta E2E",
    },
    accessToken,
  );

  assert.equal(accountSale.status, "confirmed");
  assert.equal(accountSale.accountBalanceAmount, 125);

  const customerPayment = await postJson(
    `http://127.0.0.1:5080/api/v1/customers/${customer.customerId}/payments`,
    {
      cashSessionId: cashSession.cashSessionId,
      amount: 125,
      notes: "Cobranza E2E",
    },
    accessToken,
  );

  assert.equal(customerPayment.creditAmount, 125);
  assert.equal(customerPayment.resultingBalance, 0);

  const accountStatement = await getJson(
    `http://127.0.0.1:5080/api/v1/customers/${customer.customerId}/statement`,
    accessToken,
  );

  assert.equal(accountStatement.customer.currentBalance, 0);
  assert.equal(accountStatement.movements.length, 2);

  const fullReturnSale = await postJson(
    "http://127.0.0.1:5080/api/v1/pos/sales/cash",
    {
      cashSessionId: cashSession.cashSessionId,
      items: [
        {
          productId: product.productId,
          quantity: 1,
        },
      ],
      notes: "Venta para devolucion completa E2E",
    },
    accessToken,
  );

  const fullyReturnedSale = await postJson(
    `http://127.0.0.1:5080/api/v1/pos/sales/${fullReturnSale.saleId}/returns`,
    {
      cashSessionId: cashSession.cashSessionId,
      items: fullReturnSale.items.map((item) => ({
        saleItemId: item.saleItemId,
        quantity: item.quantity,
      })),
      notes: "Devolucion total por items E2E",
    },
    accessToken,
  );

  assert.equal(fullyReturnedSale.status, "fully_returned");
  assert.equal(fullyReturnedSale.items[0].availableToReturnQuantity, 0);

  const finalCashOverview = await getJson("http://127.0.0.1:5080/api/v1/cash/overview", accessToken);
  assert.equal(finalCashOverview.currentSession.currentBalance, 1125);

  const finalStock = await getJson(
    `http://127.0.0.1:5080/api/v1/stock/products?search=${encodeURIComponent(`E2E-${uniqueSuffix}`)}`,
    accessToken,
  );

  assert.equal(finalStock.items[0].onHandQuantity, 4.5);
});

async function runShellOnlyFlow(disposers) {
  const api = startManagedProcess({
    command: "dotnet",
    args: [
      "run",
      "--no-build",
      "--project",
      "apps/api/src/AgroControl.Api/AgroControl.Api.csproj",
      "--urls",
      "http://127.0.0.1:5080",
    ],
    cwd: process.cwd(),
    name: "api",
  });

  disposers.push(() => stopManagedProcess(api));

  try {
    await waitForJson(
      "http://127.0.0.1:5080/health",
      (value) => value?.status === "healthy" && value?.service === "AgroControl.Api",
    );
  } catch (error) {
    throw formatProcessError(
      error instanceof Error ? error.message : "The API did not become ready for the fallback E2E test.",
      api,
    );
  }

  const web = startManagedProcess({
    command: "npm.cmd",
    args: ["run", "dev:test", "--workspace", "@agrocontrol/web"],
    cwd: process.cwd(),
    env: {
      AGROCONTROL_API_BASE_URL: "http://127.0.0.1:5080",
    },
    name: "web",
  });

  disposers.push(() => stopManagedProcess(web));

  let html;

  try {
    html = await waitForText(
      "http://127.0.0.1:3100/login",
      (value) =>
        value.includes("Incremento 2") &&
        value.includes("Identidad propia para AgroControl"),
    );
  } catch (error) {
    throw formatProcessError(
      error instanceof Error ? error.message : "The frontend did not render the login shell.",
      web,
    );
  }

  assert.match(html, /Incremento 2/i);
  assert.match(html, /Identidad propia para AgroControl/i);
}

async function runPwshScript(scriptPath, args = []) {
  const command = process.platform === "win32" ? "powershell.exe" : "pwsh";
  return await runCommand(command, ["-File", scriptPath, ...args]);
}

async function runCommand(command, args) {
  const { stderr } = await execFile(command, args, {
    cwd: process.cwd(),
    env: {
      ...process.env,
      NEXT_TELEMETRY_DISABLED: "1",
    },
  });

  if (stderr?.trim()) {
    process.stderr.write(stderr);
  }
}

async function canUseDockerCompose() {
  try {
    await runCommand("docker", ["info"]);
    return true;
  } catch {
    return false;
  }
}

async function waitForPostgres(timeoutMs = 60000, intervalMs = 1000) {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    try {
      await runCommand("docker", ["compose", "exec", "-T", "postgres", "pg_isready", "-U", "agrocontrol", "-d", "agrocontrol"]);
      return;
    } catch {
      await new Promise((resolve) => setTimeout(resolve, intervalMs));
    }
  }

  throw new Error("PostgreSQL local no estuvo listo a tiempo para la prueba E2E.");
}

async function getJson(url, accessToken) {
  const response = await fetch(url, {
    headers: {
      Accept: "application/json",
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
    },
  });

  if (!response.ok) {
    throw new Error(`Unexpected status ${response.status} for ${url}.`);
  }

  return await response.json();
}

async function postJson(url, body, accessToken) {
  const response = await fetch(url, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
    },
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    throw new Error(`Unexpected status ${response.status} for ${url}.`);
  }

  return await response.json();
}
