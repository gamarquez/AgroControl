import assert from "node:assert/strict";
import test from "node:test";

import { authSessionSchema, productListSchema, stockListSchema } from "./contracts";

const postgresIds = {
  product: "22222222-2222-2222-2222-222222222222",
  category: "33333333-3333-3333-3333-333333333333",
  brand: "44444444-4444-4444-4444-444444444444",
  unit: "55555555-5555-5555-5555-555555555555",
  priceList: "66666666-6666-6666-6666-666666666666",
  warehouse: "77777777-7777-7777-7777-777777777777",
};

const category = {
  categoryId: postgresIds.category,
  name: "Alimentos",
  description: null,
  isActive: true,
};

const brand = {
  brandId: postgresIds.brand,
  name: "Campo Sur",
  description: null,
  isActive: true,
};

const baseUnit = {
  unitId: postgresIds.unit,
  name: "Kilogramo",
  code: "kg",
  symbol: "kg",
  allowsFraction: true,
  isActive: true,
};

test("authSessionSchema accepts PostgreSQL UUID values used by seeded organizations", () => {
  const result = authSessionSchema.safeParse({
    user: {
      userId: "e272d991-d0ea-4c23-afdf-2000ac088407",
      organizationId: "11111111-1111-1111-1111-111111111111",
      email: "admin@agrocontrol.local",
      displayName: "Administrador AgroControl",
      isActive: true,
      isLocked: false,
      mustChangePassword: false,
      roles: [
        {
          roleId: "04d31241-df85-4930-8cae-3e89857a163d",
          code: "administrator",
          name: "Administrador",
        },
      ],
      permissions: ["auth:session"],
    },
    tokens: {
      accessToken: "access-token",
      accessTokenExpiresAt: "2026-07-21T18:00:00+00:00",
      refreshToken: "refresh-token",
      refreshTokenExpiresAt: "2026-08-04T18:00:00+00:00",
    },
  });

  assert.equal(result.success, true);
});

test("productListSchema accepts PostgreSQL UUID values without RFC variant bits", () => {
  const result = productListSchema.safeParse({
    items: [{
      productId: postgresIds.product,
      name: "Balanceado premium",
      description: null,
      internalCode: "BAL-001",
      sku: null,
      barcode: null,
      isActive: true,
      allowsFraction: true,
      salesUnitLabel: "kg",
      category,
      brand,
      baseUnit,
      currentPrice: {
        priceListId: postgresIds.priceList,
        priceListName: "Minorista",
        priceListCode: "MIN",
        costAmount: 800,
        marginPercent: 25,
        saleAmount: 1000,
        currencyCode: "ARS",
        effectiveFrom: "2026-07-21T18:00:00+00:00",
      },
    }],
    page: 1,
    pageSize: 20,
    total: 1,
  });

  assert.equal(result.success, true);
});

test("stockListSchema accepts PostgreSQL UUID values throughout nested contracts", () => {
  const result = stockListSchema.safeParse({
    items: [{
      warehouse: {
        warehouseId: postgresIds.warehouse,
        name: "Deposito principal",
        code: "MAIN",
        isDefault: true,
        isActive: true,
        createdAt: "2026-07-21T18:00:00+00:00",
        updatedAt: "2026-07-21T18:00:00+00:00",
      },
      productId: postgresIds.product,
      name: "Balanceado premium",
      internalCode: "BAL-001",
      sku: null,
      barcode: null,
      isActive: true,
      allowsFraction: true,
      category,
      brand,
      baseUnit,
      onHandQuantity: 12.5,
      isLowStock: false,
      lastMovementAt: null,
      policy: {
        minQuantity: 5,
        maxQuantity: 40,
        reorderPoint: 10,
        versionNumber: 1,
        updatedAt: "2026-07-21T18:00:00+00:00",
      },
    }],
    page: 1,
    pageSize: 20,
    total: 1,
  });

  assert.equal(result.success, true);
});
