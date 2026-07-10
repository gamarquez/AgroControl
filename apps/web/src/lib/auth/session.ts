import { revalidatePath } from "next/cache";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";

import {
  authSessionSchema,
  authUserSchema,
  brandListSchema,
  brandSchema,
  categoryListSchema,
  organizationSettingsSchema,
  priceListListSchema,
  priceListSchema,
  physicalInventoryCountSchema,
  productCategorySchema,
  productDetailSchema,
  productListSchema,
  roleListSchema,
  stockDetailSchema,
  stockListSchema,
  stockAlertListSchema,
  stockMovementSchema,
  stockMovementListSchema,
  warehouseListSchema,
  warehouseSchema,
  unitListSchema,
  userListSchema,
  cashOverviewSchema,
  cashMovementListSchema,
  cashMovementSchema,
  cashSessionSchema,
  customerAccountStatementSchema,
  customerAccountMovementListSchema,
  customerAccountMovementSchema,
  customerListSchema,
  customerSchema,
  fiscalDocumentListSchema,
  fiscalDocumentSchema,
  fiscalProbeSchema,
  fiscalSettingsSchema,
  posProductListSchema,
  saleListSchema,
  saleSchema,
  type AuthSession,
  type AuthUser,
  type Brand,
  type BrandList,
  type CashMovementList,
  type CashOverview,
  type CashSession,
  type Customer,
  type CustomerAccountMovement,
  type CustomerAccountMovementList,
  type CustomerAccountStatement,
  type CustomerList,
  type FiscalDocument,
  type FiscalDocumentList,
  type FiscalProbe,
  type FiscalSettings,
  type PosProductList,
  type Sale,
  type SaleList,
  type CategoryList,
  type OrganizationSettings,
  type PriceList,
  type PriceListList,
  type PhysicalInventoryCount,
  type ProductDetail,
  type ProductList,
  type ProductSummary,
  type RoleList,
  type StockDetail,
  type StockAlertList,
  type StockList,
  type StockMovementList,
  type UnitList,
  type UserList,
  type Warehouse,
  type WarehouseList,
} from "@/lib/api/contracts";
import { ApiError, apiRequest, apiVoidRequest } from "@/lib/api/server";

const ACCESS_TOKEN_COOKIE = "agrocontrol_access_token";
const REFRESH_TOKEN_COOKIE = "agrocontrol_refresh_token";
const ACCESS_EXPIRES_AT_COOKIE = "agrocontrol_access_expires_at";

export async function loginWithPassword(email: string, password: string): Promise<AuthUser> {
  const session = await apiRequest("/api/v1/auth/login", authSessionSchema, {
    method: "POST",
    body: {
      email,
      password,
    },
  });

  await persistSession(session);
  return session.user;
}

export async function logoutCurrentSession(): Promise<void> {
  const cookieStore = await cookies();
  const refreshToken = cookieStore.get(REFRESH_TOKEN_COOKIE)?.value;

  if (refreshToken) {
    try {
      const accessToken = cookieStore.get(ACCESS_TOKEN_COOKIE)?.value;

      if (accessToken) {
        await apiVoidRequest("/api/v1/auth/logout", {
          method: "POST",
          accessToken,
          body: {
            refreshToken,
          },
        });
      }
    } catch {
      // Ignore logout errors and clear local session anyway.
    }
  }

  await clearSession();
}

export async function getSession(): Promise<AuthUser | null> {
  const cookieStore = await cookies();
  const accessToken = cookieStore.get(ACCESS_TOKEN_COOKIE)?.value;

  if (!accessToken) {
    return null;
  }

  try {
    return await apiRequest("/api/v1/auth/me", authUserSchema, {
      accessToken,
    });
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      const refreshed = await refreshSession();

      if (!refreshed) {
        await clearSession();
        return null;
      }

      return await apiRequest("/api/v1/auth/me", authUserSchema, {
        accessToken: refreshed.tokens.accessToken,
      });
    }

    throw error;
  }
}

export async function requireSession(): Promise<AuthUser> {
  const user = await getSession();

  if (!user) {
    redirect("/login");
  }

  return user;
}

export async function listUsers(): Promise<UserList> {
  return await authorizedRequest("/api/v1/users", userListSchema);
}

export async function listRoles(): Promise<RoleList> {
  return await authorizedRequest("/api/v1/users/roles", roleListSchema);
}

export async function createUser(input: {
  email: string;
  displayName: string;
  password: string;
  mustChangePassword: boolean;
  roleIds: string[];
}): Promise<AuthUser> {
  return await authorizedRequest("/api/v1/users", authUserSchema, {
    method: "POST",
    body: input,
  });
}

export async function updateUser(
  userId: string,
  input: {
    displayName: string;
    isActive: boolean;
    isLocked: boolean;
    mustChangePassword: boolean;
    roleIds: string[];
  },
): Promise<AuthUser> {
  return await authorizedRequest(`/api/v1/users/${userId}`, authUserSchema, {
    method: "PATCH",
    body: input,
  });
}

export async function getOrganizationSettings(): Promise<OrganizationSettings> {
  return await authorizedRequest("/api/v1/settings/organization", organizationSettingsSchema);
}

export async function updateOrganizationSettings(input: {
  legalName: string;
  tradeName: string;
  taxId: string;
  timeZone: string;
  currencyCode: string;
}): Promise<OrganizationSettings> {
  return await authorizedRequest("/api/v1/settings/organization", organizationSettingsSchema, {
    method: "PUT",
    body: input,
  });
}

export async function revalidateDashboardPaths(): Promise<void> {
  revalidatePath("/dashboard");
  revalidatePath("/dashboard/users");
  revalidatePath("/dashboard/settings");
  revalidatePath("/dashboard/catalog");
  revalidatePath("/dashboard/catalog/categories");
  revalidatePath("/dashboard/catalog/brands");
  revalidatePath("/dashboard/catalog/price-lists");
  revalidatePath("/dashboard/stock");
  revalidatePath("/dashboard/cash");
  revalidatePath("/dashboard/pos");
  revalidatePath("/dashboard/customers");
  revalidatePath("/dashboard/fiscal");
}

export async function listCatalogProducts(input: {
  search?: string;
  categoryId?: string;
  brandId?: string;
  isActive?: string;
  page?: number;
  pageSize?: number;
}): Promise<ProductList> {
  const params = new URLSearchParams();

  if (input.search) params.set("search", input.search);
  if (input.categoryId) params.set("categoryId", input.categoryId);
  if (input.brandId) params.set("brandId", input.brandId);
  if (input.isActive) params.set("isActive", input.isActive);
  if (input.page) params.set("page", String(input.page));
  if (input.pageSize) params.set("pageSize", String(input.pageSize));

  const queryString = params.toString();
  return await authorizedRequest(
    `/api/v1/catalog/products${queryString ? `?${queryString}` : ""}`,
    productListSchema,
  );
}

export async function getCatalogProduct(productId: string): Promise<ProductDetail> {
  return await authorizedRequest(`/api/v1/catalog/products/${productId}`, productDetailSchema);
}

export async function createCatalogProduct(input: {
  categoryId?: string;
  brandId?: string;
  baseUnitId: string;
  name: string;
  description?: string;
  internalCode: string;
  sku?: string;
  barcode?: string;
  allowsFraction: boolean;
  salesUnitLabel?: string;
  costAmount: number;
  marginPercent?: number;
  saleAmount: number;
  currencyCode: string;
  priceListId?: string;
}): Promise<ProductDetail> {
  return await authorizedRequest("/api/v1/catalog/products", productDetailSchema, {
    method: "POST",
    body: input,
  });
}

export async function updateCatalogProduct(
  productId: string,
  input: {
    categoryId?: string;
    brandId?: string;
    baseUnitId: string;
    name: string;
    description?: string;
    internalCode: string;
    sku?: string;
    barcode?: string;
    isActive: boolean;
    allowsFraction: boolean;
    salesUnitLabel?: string;
    costAmount: number;
    marginPercent?: number;
    saleAmount: number;
    currencyCode: string;
    priceListId?: string;
  },
): Promise<ProductDetail> {
  return await authorizedRequest(`/api/v1/catalog/products/${productId}`, productDetailSchema, {
    method: "PATCH",
    body: input,
  });
}

export async function listCatalogCategories(): Promise<CategoryList> {
  return await authorizedRequest("/api/v1/catalog/categories", categoryListSchema);
}

export async function createCatalogCategory(input: {
  name: string;
  description?: string;
}) {
  return await authorizedRequest("/api/v1/catalog/categories", productCategorySchema, {
    method: "POST",
    body: input,
  });
}

export async function updateCatalogCategory(
  categoryId: string,
  input: { name: string; description?: string; isActive: boolean },
) {
  return await authorizedRequest(`/api/v1/catalog/categories/${categoryId}`, productCategorySchema, {
    method: "PATCH",
    body: input,
  });
}

export async function listCatalogBrands(): Promise<BrandList> {
  return await authorizedRequest("/api/v1/catalog/brands", brandListSchema);
}

export async function createCatalogBrand(input: {
  name: string;
  description?: string;
}): Promise<Brand> {
  return await authorizedRequest("/api/v1/catalog/brands", brandSchema, {
    method: "POST",
    body: input,
  });
}

export async function updateCatalogBrand(
  brandId: string,
  input: { name: string; description?: string; isActive: boolean },
): Promise<Brand> {
  return await authorizedRequest(`/api/v1/catalog/brands/${brandId}`, brandSchema, {
    method: "PATCH",
    body: input,
  });
}

export async function listCatalogUnits(): Promise<UnitList> {
  return await authorizedRequest("/api/v1/catalog/units", unitListSchema);
}

export async function listCatalogPriceLists(): Promise<PriceListList> {
  return await authorizedRequest("/api/v1/catalog/price-lists", priceListListSchema);
}

export async function createCatalogPriceList(input: {
  name: string;
  code: string;
  isDefault: boolean;
  isActive: boolean;
}): Promise<PriceList> {
  return await authorizedRequest("/api/v1/catalog/price-lists", priceListSchema, {
    method: "POST",
    body: input,
  });
}

export async function updateCatalogPriceList(
  priceListId: string,
  input: { name: string; code: string; isDefault: boolean; isActive: boolean },
): Promise<PriceList> {
  return await authorizedRequest(`/api/v1/catalog/price-lists/${priceListId}`, priceListSchema, {
    method: "PATCH",
    body: input,
  });
}

export async function listStockProducts(input: {
  search?: string;
  warehouseId?: string;
  categoryId?: string;
  brandId?: string;
  isLowStock?: string;
  page?: number;
  pageSize?: number;
}): Promise<StockList> {
  const params = new URLSearchParams();

  if (input.search) params.set("search", input.search);
  if (input.warehouseId) params.set("warehouseId", input.warehouseId);
  if (input.categoryId) params.set("categoryId", input.categoryId);
  if (input.brandId) params.set("brandId", input.brandId);
  if (input.isLowStock) params.set("isLowStock", input.isLowStock);
  if (input.page) params.set("page", String(input.page));
  if (input.pageSize) params.set("pageSize", String(input.pageSize));

  const queryString = params.toString();
  return await authorizedRequest(
    `/api/v1/stock/products${queryString ? `?${queryString}` : ""}`,
    stockListSchema,
  );
}

export async function listStockWarehouses(): Promise<WarehouseList> {
  return await authorizedRequest("/api/v1/stock/warehouses", warehouseListSchema);
}

export async function createStockWarehouse(input: {
  name: string;
  code: string;
  isDefault: boolean;
  isActive: boolean;
}): Promise<Warehouse> {
  return await authorizedRequest("/api/v1/stock/warehouses", warehouseSchema, {
    method: "POST",
    body: input,
  });
}

export async function updateStockWarehouse(
  warehouseId: string,
  input: {
    name: string;
    code: string;
    isDefault: boolean;
    isActive: boolean;
  },
): Promise<Warehouse> {
  return await authorizedRequest(`/api/v1/stock/warehouses/${warehouseId}`, warehouseSchema, {
    method: "PATCH",
    body: input,
  });
}

export async function listStockAlerts(input?: {
  warehouseId?: string;
  limit?: number;
}): Promise<StockAlertList> {
  const params = new URLSearchParams();

  if (input?.warehouseId) params.set("warehouseId", input.warehouseId);
  if (input?.limit) params.set("limit", String(input.limit));

  const queryString = params.toString();
  return await authorizedRequest(`/api/v1/stock/alerts${queryString ? `?${queryString}` : ""}`, stockAlertListSchema);
}

export async function getStockProduct(productId: string, warehouseId: string): Promise<StockDetail> {
  const params = new URLSearchParams({ warehouseId });
  return await authorizedRequest(`/api/v1/stock/products/${productId}?${params.toString()}`, stockDetailSchema);
}

export async function listStockMovements(
  productId: string,
  warehouseId: string,
  input?: { page?: number; pageSize?: number },
): Promise<StockMovementList> {
  const params = new URLSearchParams({ warehouseId });

  if (input?.page) params.set("page", String(input.page));
  if (input?.pageSize) params.set("pageSize", String(input.pageSize));

  const queryString = params.toString();
  return await authorizedRequest(
    `/api/v1/stock/products/${productId}/movements${queryString ? `?${queryString}` : ""}`,
    stockMovementListSchema,
  );
}

export async function updateStockPolicy(
  productId: string,
  input: {
    warehouseId: string;
    minQuantity?: number;
    maxQuantity?: number;
    reorderPoint?: number;
  },
): Promise<StockDetail> {
  return await authorizedRequest(`/api/v1/stock/products/${productId}/policy`, stockDetailSchema, {
    method: "PUT",
    body: input,
  });
}

export async function createStockMovement(input: {
  warehouseId: string;
  productId: string;
  movementType: string;
  quantity: number;
  reason: string;
  referenceDocument?: string;
  notes?: string;
}) {
  return await authorizedRequest("/api/v1/stock/movements", stockMovementSchema, {
    method: "POST",
    body: input,
  });
}

export async function recordPhysicalInventoryCount(input: {
  warehouseId: string;
  productId: string;
  countedQuantity: number;
  reason: string;
  notes?: string;
}) {
  return await authorizedRequest("/api/v1/stock/physical-counts", physicalInventoryCountSchema, {
    method: "POST",
    body: input,
  });
}

export async function getCashOverview(): Promise<CashOverview> {
  return await authorizedRequest("/api/v1/cash/overview", cashOverviewSchema);
}

export async function listCashMovements(input?: {
  page?: number;
  pageSize?: number;
}): Promise<CashMovementList> {
  const params = new URLSearchParams();

  if (input?.page) params.set("page", String(input.page));
  if (input?.pageSize) params.set("pageSize", String(input.pageSize));

  const queryString = params.toString();
  return await authorizedRequest(
    `/api/v1/cash/movements${queryString ? `?${queryString}` : ""}`,
    cashMovementListSchema,
  );
}

export async function openCashSession(input: {
  cashRegisterCode: string;
  openingAmount: number;
  openingNotes?: string;
}): Promise<CashSession> {
  return await authorizedRequest("/api/v1/cash/sessions/open", cashSessionSchema, {
    method: "POST",
    body: input,
  });
}

export async function createCashMovement(input: {
  cashSessionId: string;
  movementType: string;
  categoryCode: string;
  concept: string;
  paymentMethod: string;
  amount: number;
  referenceDocument?: string;
  notes?: string;
}) {
  return await authorizedRequest("/api/v1/cash/movements", cashMovementSchema, {
    method: "POST",
    body: input,
  });
}

export async function closeCashSession(
  cashSessionId: string,
  input: {
    closingAmount: number;
    closingNotes?: string;
  },
): Promise<CashSession> {
  return await authorizedRequest(`/api/v1/cash/sessions/${cashSessionId}/close`, cashSessionSchema, {
    method: "POST",
    body: input,
  });
}

export async function listPosProducts(input?: {
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<PosProductList> {
  const params = new URLSearchParams();

  if (input?.search) params.set("search", input.search);
  if (input?.page) params.set("page", String(input.page));
  if (input?.pageSize) params.set("pageSize", String(input.pageSize));

  const queryString = params.toString();
  return await authorizedRequest(
    `/api/v1/pos/products${queryString ? `?${queryString}` : ""}`,
    posProductListSchema,
  );
}

export async function listSales(input?: {
  search?: string;
  status?: string;
  customerId?: string;
  page?: number;
  pageSize?: number;
}): Promise<SaleList> {
  const params = new URLSearchParams();

  if (input?.search) params.set("search", input.search);
  if (input?.status) params.set("status", input.status);
  if (input?.customerId) params.set("customerId", input.customerId);
  if (input?.page) params.set("page", String(input.page));
  if (input?.pageSize) params.set("pageSize", String(input.pageSize));

  const queryString = params.toString();
  return await authorizedRequest(`/api/v1/pos/sales${queryString ? `?${queryString}` : ""}`, saleListSchema);
}

export async function getSale(saleId: string): Promise<Sale> {
  return await authorizedRequest(`/api/v1/pos/sales/${saleId}`, saleSchema);
}

export async function createCashSale(input: {
  cashSessionId: string;
  items: Array<{ productId: string; quantity: number }>;
  notes?: string;
}): Promise<Sale> {
  return await authorizedRequest("/api/v1/pos/sales/cash", saleSchema, {
    method: "POST",
    body: input,
  });
}

export async function createCheckoutSale(input: {
  cashSessionId?: string;
  customerId?: string;
  items: Array<{ productId: string; quantity: number }>;
  payments: Array<{
    paymentMethod: string;
    amount: number;
    reference?: string;
    providerName?: string;
  }>;
  dueDate?: string;
  notes?: string;
}): Promise<Sale> {
  return await authorizedRequest("/api/v1/pos/sales/checkout", saleSchema, {
    method: "POST",
    body: input,
  });
}

export async function getFiscalSettings(): Promise<FiscalSettings> {
  return await authorizedRequest("/api/v1/fiscal/settings", fiscalSettingsSchema);
}

export async function updateFiscalSettings(input: {
  provider: string;
  environment: string;
  taxpayerId: string;
  pointOfSale: number;
  serviceName: string;
  defaultDocumentType: string;
  isEnabled: boolean;
}): Promise<FiscalSettings> {
  return await authorizedRequest("/api/v1/fiscal/settings", fiscalSettingsSchema, {
    method: "PUT",
    body: input,
  });
}

export async function listFiscalDocuments(input?: { limit?: number }): Promise<FiscalDocumentList> {
  const params = new URLSearchParams();
  if (input?.limit) params.set("limit", String(input.limit));
  const queryString = params.toString();

  return await authorizedRequest(
    `/api/v1/fiscal/documents${queryString ? `?${queryString}` : ""}`,
    fiscalDocumentListSchema,
  );
}

export async function createFiscalDocument(
  saleId: string,
  input: { documentKind: string },
): Promise<FiscalDocument> {
  return await authorizedRequest(`/api/v1/fiscal/documents/sales/${saleId}`, fiscalDocumentSchema, {
    method: "POST",
    body: input,
  });
}

export async function probeFiscalConnectivity(): Promise<FiscalProbe> {
  return await authorizedRequest("/api/v1/fiscal/probe", fiscalProbeSchema, {
    method: "POST",
  });
}

export async function reverseCashSale(
  saleId: string,
  input: {
    cashSessionId: string;
    reversalNotes?: string;
  },
): Promise<Sale> {
  return await authorizedRequest(`/api/v1/pos/sales/${saleId}/reverse`, saleSchema, {
    method: "POST",
    body: input,
  });
}

export async function returnSaleItems(
  saleId: string,
  input: {
    cashSessionId?: string;
    items: Array<{ saleItemId: string; quantity: number }>;
    notes?: string;
  },
): Promise<Sale> {
  return await authorizedRequest(`/api/v1/pos/sales/${saleId}/returns`, saleSchema, {
    method: "POST",
    body: input,
  });
}

export async function listCustomers(input?: {
  search?: string;
  isActive?: string;
  page?: number;
  pageSize?: number;
}): Promise<CustomerList> {
  const params = new URLSearchParams();

  if (input?.search) params.set("search", input.search);
  if (input?.isActive) params.set("isActive", input.isActive);
  if (input?.page) params.set("page", String(input.page));
  if (input?.pageSize) params.set("pageSize", String(input.pageSize));

  const queryString = params.toString();
  return await authorizedRequest(
    `/api/v1/customers${queryString ? `?${queryString}` : ""}`,
    customerListSchema,
  );
}

export async function getCustomer(customerId: string): Promise<Customer> {
  return await authorizedRequest(`/api/v1/customers/${customerId}`, customerSchema);
}

export async function createCustomer(input: {
  displayName: string;
  taxId?: string;
  phone?: string;
  email?: string;
  address?: string;
  creditLimitAmount: number;
  notes?: string;
}): Promise<Customer> {
  return await authorizedRequest("/api/v1/customers", customerSchema, {
    method: "POST",
    body: input,
  });
}

export async function updateCustomer(
  customerId: string,
  input: {
    displayName: string;
    taxId?: string;
    phone?: string;
    email?: string;
    address?: string;
    creditLimitAmount: number;
    isActive: boolean;
    notes?: string;
  },
): Promise<Customer> {
  return await authorizedRequest(`/api/v1/customers/${customerId}`, customerSchema, {
    method: "PATCH",
    body: input,
  });
}

export async function listCustomerAccountMovements(
  customerId: string,
  input?: {
    page?: number;
    pageSize?: number;
  },
): Promise<CustomerAccountMovementList> {
  const params = new URLSearchParams();

  if (input?.page) params.set("page", String(input.page));
  if (input?.pageSize) params.set("pageSize", String(input.pageSize));

  const queryString = params.toString();
  return await authorizedRequest(
    `/api/v1/customers/${customerId}/account-movements${queryString ? `?${queryString}` : ""}`,
    customerAccountMovementListSchema,
  );
}

export async function getCustomerAccountStatement(
  customerId: string,
  input?: { limit?: number },
): Promise<CustomerAccountStatement> {
  const params = new URLSearchParams();
  if (input?.limit) params.set("limit", String(input.limit));
  const queryString = params.toString();

  return await authorizedRequest(
    `/api/v1/customers/${customerId}/statement${queryString ? `?${queryString}` : ""}`,
    customerAccountStatementSchema,
  );
}

export async function recordCustomerPayment(
  customerId: string,
  input: {
    cashSessionId: string;
    amount: number;
    notes?: string;
  },
): Promise<CustomerAccountMovement> {
  return await authorizedRequest(
    `/api/v1/customers/${customerId}/payments`,
    customerAccountMovementSchema,
    {
      method: "POST",
      body: input,
    },
  );
}

export async function recordCustomerCreditNote(
  customerId: string,
  input: {
    amount: number;
    concept: string;
    referenceDocument?: string;
    notes?: string;
  },
): Promise<CustomerAccountMovement> {
  return await authorizedRequest(
    `/api/v1/customers/${customerId}/credit-notes`,
    customerAccountMovementSchema,
    {
      method: "POST",
      body: input,
    },
  );
}

export async function createAccountSale(input: {
  customerId: string;
  items: Array<{ productId: string; quantity: number }>;
  dueDate?: string;
  notes?: string;
}): Promise<Sale> {
  return await authorizedRequest("/api/v1/pos/sales/account", saleSchema, {
    method: "POST",
    body: input,
  });
}

async function authorizedRequest<TSchema extends import("zod").ZodTypeAny>(
  path: string,
  schema: TSchema,
  options?: {
    method?: "GET" | "POST" | "PATCH" | "PUT";
    body?: unknown;
    cache?: RequestCache;
  },
): Promise<import("zod").infer<TSchema>> {
  const session = await ensureFreshSession();

  if (!session) {
    redirect("/login");
  }

  return await apiRequest(path, schema, {
    ...options,
    accessToken: session.tokens.accessToken,
  });
}

async function ensureFreshSession(): Promise<AuthSession | null> {
  const cookieStore = await cookies();
  const accessToken = cookieStore.get(ACCESS_TOKEN_COOKIE)?.value;
  const expiresAt = cookieStore.get(ACCESS_EXPIRES_AT_COOKIE)?.value;

  if (!accessToken) {
    return await refreshSession();
  }

  if (!expiresAt) {
    return {
      user: await apiRequest("/api/v1/auth/me", authUserSchema, {
        accessToken,
      }),
      tokens: {
        accessToken,
        accessTokenExpiresAt: new Date(Date.now() + 60_000).toISOString(),
        refreshToken: cookieStore.get(REFRESH_TOKEN_COOKIE)?.value ?? "",
        refreshTokenExpiresAt: new Date(Date.now() + 60_000).toISOString(),
      },
    };
  }

  const isExpired = Date.parse(expiresAt) <= Date.now() + 30_000;

  if (!isExpired) {
    return {
      user: await apiRequest("/api/v1/auth/me", authUserSchema, {
        accessToken,
      }),
      tokens: {
        accessToken,
        accessTokenExpiresAt: expiresAt,
        refreshToken: cookieStore.get(REFRESH_TOKEN_COOKIE)?.value ?? "",
        refreshTokenExpiresAt: expiresAt,
      },
    };
  }

  return await refreshSession();
}

async function refreshSession(): Promise<AuthSession | null> {
  const cookieStore = await cookies();
  const refreshToken = cookieStore.get(REFRESH_TOKEN_COOKIE)?.value;

  if (!refreshToken) {
    return null;
  }

  try {
    const session = await apiRequest("/api/v1/auth/refresh", authSessionSchema, {
      method: "POST",
      body: {
        refreshToken,
      },
    });

    await persistSession(session);
    return session;
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      await clearSession();
      return null;
    }

    throw error;
  }
}

async function persistSession(session: AuthSession): Promise<void> {
  const cookieStore = await cookies();
  const accessExpiresAt = new Date(session.tokens.accessTokenExpiresAt);
  const refreshExpiresAt = new Date(session.tokens.refreshTokenExpiresAt);

  cookieStore.set(ACCESS_TOKEN_COOKIE, session.tokens.accessToken, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    expires: accessExpiresAt,
    path: "/",
  });
  cookieStore.set(REFRESH_TOKEN_COOKIE, session.tokens.refreshToken, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    expires: refreshExpiresAt,
    path: "/",
  });
  cookieStore.set(ACCESS_EXPIRES_AT_COOKIE, session.tokens.accessTokenExpiresAt, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    expires: refreshExpiresAt,
    path: "/",
  });
}

async function clearSession(): Promise<void> {
  const cookieStore = await cookies();
  cookieStore.delete(ACCESS_TOKEN_COOKIE);
  cookieStore.delete(REFRESH_TOKEN_COOKIE);
  cookieStore.delete(ACCESS_EXPIRES_AT_COOKIE);
}
