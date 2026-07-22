import { z } from "zod";

const postgresUuidSchema = z.guid();

export const authRoleSchema = z.object({
  roleId: postgresUuidSchema,
  code: z.string().min(1),
  name: z.string().min(1),
});

export const authUserSchema = z.object({
  userId: postgresUuidSchema,
  organizationId: postgresUuidSchema,
  email: z.email(),
  displayName: z.string().min(1),
  isActive: z.boolean(),
  isLocked: z.boolean(),
  mustChangePassword: z.boolean(),
  roles: z.array(authRoleSchema),
  permissions: z.array(z.string().min(1)),
});

export const authTokenSchema = z.object({
  accessToken: z.string().min(1),
  accessTokenExpiresAt: z.string().datetime({ offset: true }),
  refreshToken: z.string().min(1),
  refreshTokenExpiresAt: z.string().datetime({ offset: true }),
});

export const authSessionSchema = z.object({
  user: authUserSchema,
  tokens: authTokenSchema,
});

export const userListSchema = z.object({
  items: z.array(authUserSchema),
});

export const roleListSchema = z.object({
  items: z.array(authRoleSchema),
});

export const organizationSettingsSchema = z.object({
  organizationId: postgresUuidSchema,
  legalName: z.string().min(1),
  tradeName: z.string().min(1),
  taxId: z.string(),
  timeZone: z.string().min(1),
  currencyCode: z.string().min(1),
});

export const productCategorySchema = z.object({
  categoryId: postgresUuidSchema,
  name: z.string().min(1),
  description: z.string().nullable(),
  isActive: z.boolean(),
});

export const brandSchema = z.object({
  brandId: postgresUuidSchema,
  name: z.string().min(1),
  description: z.string().nullable(),
  isActive: z.boolean(),
});

export const unitSchema = z.object({
  unitId: postgresUuidSchema,
  name: z.string().min(1),
  code: z.string().min(1),
  symbol: z.string().min(1),
  allowsFraction: z.boolean(),
  isActive: z.boolean(),
});

export const priceListSchema = z.object({
  priceListId: postgresUuidSchema,
  name: z.string().min(1),
  code: z.string().min(1),
  isDefault: z.boolean(),
  isActive: z.boolean(),
});

export const productPriceSchema = z.object({
  priceListId: postgresUuidSchema,
  priceListName: z.string().min(1),
  priceListCode: z.string().min(1),
  costAmount: z.number(),
  marginPercent: z.number().nullable(),
  saleAmount: z.number(),
  currencyCode: z.string().min(1),
  effectiveFrom: z.string().datetime({ offset: true }),
});

export const productSummarySchema = z.object({
  productId: postgresUuidSchema,
  name: z.string().min(1),
  description: z.string().nullable(),
  internalCode: z.string().min(1),
  sku: z.string().nullable(),
  barcode: z.string().nullable(),
  isActive: z.boolean(),
  allowsFraction: z.boolean(),
  salesUnitLabel: z.string().nullable(),
  category: productCategorySchema.nullable(),
  brand: brandSchema.nullable(),
  baseUnit: unitSchema,
  currentPrice: productPriceSchema,
});

export const productDetailSchema = productSummarySchema.extend({
  createdAt: z.string().datetime({ offset: true }),
  updatedAt: z.string().datetime({ offset: true }),
});

export const productListSchema = z.object({
  items: z.array(productSummarySchema),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  total: z.number().int().nonnegative(),
});

export const categoryListSchema = z.object({
  items: z.array(productCategorySchema),
});

export const brandListSchema = z.object({
  items: z.array(brandSchema),
});

export const unitListSchema = z.object({
  items: z.array(unitSchema),
});

export const priceListListSchema = z.object({
  items: z.array(priceListSchema),
});

export const stockPolicySchema = z.object({
  minQuantity: z.number().nullable(),
  maxQuantity: z.number().nullable(),
  reorderPoint: z.number().nullable(),
  versionNumber: z.number().int().positive(),
  updatedAt: z.string().datetime({ offset: true }),
});

export const warehouseSchema = z.object({
  warehouseId: postgresUuidSchema,
  name: z.string().min(1),
  code: z.string().min(1),
  isDefault: z.boolean(),
  isActive: z.boolean(),
  createdAt: z.string().datetime({ offset: true }),
  updatedAt: z.string().datetime({ offset: true }),
});

export const stockMovementSchema = z.object({
  stockMovementId: postgresUuidSchema,
  warehouse: warehouseSchema,
  productId: postgresUuidSchema,
  movementType: z.string().min(1),
  quantity: z.number(),
  quantityDelta: z.number(),
  resultingQuantity: z.number(),
  reason: z.string().min(1),
  referenceDocument: z.string().nullable(),
  notes: z.string().nullable(),
  performedByUserId: postgresUuidSchema.nullable(),
  createdAt: z.string().datetime({ offset: true }),
});

export const stockSummarySchema = z.object({
  warehouse: warehouseSchema,
  productId: postgresUuidSchema,
  name: z.string().min(1),
  internalCode: z.string().min(1),
  sku: z.string().nullable(),
  barcode: z.string().nullable(),
  isActive: z.boolean(),
  allowsFraction: z.boolean(),
  category: productCategorySchema.nullable(),
  brand: brandSchema.nullable(),
  baseUnit: unitSchema,
  onHandQuantity: z.number(),
  isLowStock: z.boolean(),
  lastMovementAt: z.string().datetime({ offset: true }).nullable(),
  policy: stockPolicySchema,
});

export const stockDetailSchema = stockSummarySchema;

export const stockListSchema = z.object({
  items: z.array(stockSummarySchema),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  total: z.number().int().nonnegative(),
});

export const stockMovementListSchema = z.object({
  items: z.array(stockMovementSchema),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  total: z.number().int().nonnegative(),
});

export const warehouseListSchema = z.object({
  items: z.array(warehouseSchema),
});

export const stockAlertSchema = z.object({
  warehouse: warehouseSchema,
  productId: postgresUuidSchema,
  productName: z.string().min(1),
  internalCode: z.string().min(1),
  unitSymbol: z.string().min(1),
  onHandQuantity: z.number(),
  reorderPoint: z.number().nullable(),
  minQuantity: z.number().nullable(),
  lastMovementAt: z.string().datetime({ offset: true }).nullable(),
});

export const stockAlertListSchema = z.object({
  items: z.array(stockAlertSchema),
});

export const physicalInventoryCountSchema = z.object({
  physicalInventoryCountId: postgresUuidSchema,
  warehouse: warehouseSchema,
  productId: postgresUuidSchema,
  expectedQuantity: z.number(),
  countedQuantity: z.number(),
  differenceQuantity: z.number(),
  reason: z.string().min(1),
  notes: z.string().nullable(),
  performedByUserId: postgresUuidSchema.nullable(),
  createdAt: z.string().datetime({ offset: true }),
});

export const cashRegisterSchema = z.object({
  cashRegisterId: postgresUuidSchema,
  name: z.string().min(1),
  code: z.string().min(1),
  isActive: z.boolean(),
});

export const cashSessionSchema = z.object({
  cashSessionId: postgresUuidSchema,
  cashRegister: cashRegisterSchema,
  openingAmount: z.number(),
  closingAmount: z.number().nullable(),
  differenceAmount: z.number().nullable(),
  currentBalance: z.number(),
  status: z.string().min(1),
  openingNotes: z.string().nullable(),
  closingNotes: z.string().nullable(),
  openedByUserId: postgresUuidSchema,
  closedByUserId: postgresUuidSchema.nullable(),
  openedAt: z.string().datetime({ offset: true }),
  closedAt: z.string().datetime({ offset: true }).nullable(),
});

export const cashMovementSchema = z.object({
  cashMovementId: postgresUuidSchema,
  cashSessionId: postgresUuidSchema,
  movementType: z.string().min(1),
  categoryCode: z.string().min(1),
  concept: z.string().min(1),
  paymentMethod: z.string().min(1),
  amount: z.number(),
  signedAmount: z.number(),
  resultingBalance: z.number(),
  referenceDocument: z.string().nullable(),
  notes: z.string().nullable(),
  performedByUserId: postgresUuidSchema.nullable(),
  createdAt: z.string().datetime({ offset: true }),
});

export const cashOverviewSchema = z.object({
  cashRegister: cashRegisterSchema,
  currentSession: cashSessionSchema.nullable(),
  recentMovements: z.array(cashMovementSchema),
});

export const cashMovementListSchema = z.object({
  items: z.array(cashMovementSchema),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  total: z.number().int().nonnegative(),
});

export const customerSchema = z.object({
  customerId: postgresUuidSchema,
  displayName: z.string().min(1),
  taxId: z.string().nullable(),
  phone: z.string().nullable(),
  email: z.string().nullable(),
  address: z.string().nullable(),
  creditLimitAmount: z.number(),
  currentBalance: z.number(),
  overdueBalance: z.number(),
  nextDueDate: z.string().nullable(),
  isActive: z.boolean(),
  notes: z.string().nullable(),
  createdAt: z.string().datetime({ offset: true }),
  updatedAt: z.string().datetime({ offset: true }),
});

export const customerListSchema = z.object({
  items: z.array(customerSchema),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  total: z.number().int().nonnegative(),
});

export const customerAccountMovementSchema = z.object({
  customerAccountMovementId: postgresUuidSchema,
  customerId: postgresUuidSchema,
  saleId: postgresUuidSchema.nullable(),
  cashSessionId: postgresUuidSchema.nullable(),
  movementType: z.string().min(1),
  concept: z.string().min(1),
  referenceDocument: z.string().nullable(),
  debitAmount: z.number(),
  creditAmount: z.number(),
  openAmount: z.number(),
  isOverdue: z.boolean(),
  dueDate: z.string().nullable(),
  resultingBalance: z.number(),
  notes: z.string().nullable(),
  performedByUserId: postgresUuidSchema.nullable(),
  createdAt: z.string().datetime({ offset: true }),
});

export const customerAccountMovementListSchema = z.object({
  items: z.array(customerAccountMovementSchema),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  total: z.number().int().nonnegative(),
});

export const customerAccountStatementSchema = z.object({
  customer: customerSchema,
  movements: z.array(customerAccountMovementSchema),
});

export const fiscalSettingsSchema = z.object({
  organizationId: postgresUuidSchema,
  provider: z.string().min(1),
  environment: z.string().min(1),
  taxpayerId: z.string().min(1),
  pointOfSale: z.number().int().positive(),
  serviceName: z.string().min(1),
  defaultDocumentType: z.string().min(1),
  isEnabled: z.boolean(),
  updatedAt: z.string().datetime({ offset: true }),
});

export const fiscalDocumentSchema = z.object({
  fiscalDocumentId: postgresUuidSchema,
  saleId: postgresUuidSchema,
  ticketNumber: z.number().int().nonnegative(),
  customerId: postgresUuidSchema.nullable(),
  customerName: z.string().min(1),
  totalAmount: z.number(),
  currencyCode: z.string().min(1),
  documentKind: z.string().min(1),
  provider: z.string().min(1),
  environment: z.string().min(1),
  serviceName: z.string().min(1),
  taxpayerId: z.string().min(1),
  pointOfSale: z.number().int().positive(),
  status: z.string().min(1),
  documentNumber: z.number().int().nullable(),
  cae: z.string().nullable(),
  caeExpiresOn: z.string().nullable(),
  externalReference: z.string().nullable(),
  lastError: z.string().nullable(),
  attemptsCount: z.number().int().nonnegative(),
  lastAttemptAt: z.string().datetime({ offset: true }).nullable(),
  createdAt: z.string().datetime({ offset: true }),
  updatedAt: z.string().datetime({ offset: true }),
});

export const fiscalDocumentListSchema = z.object({
  items: z.array(fiscalDocumentSchema),
  total: z.number().int().nonnegative(),
});

export const fiscalProbeSchema = z.object({
  isConfigured: z.boolean(),
  isReachable: z.boolean(),
  provider: z.string().min(1),
  environment: z.string().min(1),
  summary: z.string().min(1),
  tokenExpiresAt: z.string().nullable(),
  authServer: z.string().nullable(),
  appServer: z.string().nullable(),
  dbServer: z.string().nullable(),
});

export const posProductSchema = z.object({
  productId: postgresUuidSchema,
  name: z.string().min(1),
  internalCode: z.string().min(1),
  sku: z.string().nullable(),
  barcode: z.string().nullable(),
  allowsFraction: z.boolean(),
  unitSymbol: z.string().min(1),
  onHandQuantity: z.number(),
  saleAmount: z.number(),
  currencyCode: z.string().min(1),
});

export const posProductListSchema = z.object({
  items: z.array(posProductSchema),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  total: z.number().int().nonnegative(),
});

export const saleItemSchema = z.object({
  saleItemId: postgresUuidSchema,
  productId: postgresUuidSchema,
  productName: z.string().min(1),
  unitSymbol: z.string().min(1),
  quantity: z.number(),
  returnedQuantity: z.number(),
  availableToReturnQuantity: z.number(),
  unitPrice: z.number(),
  lineTotal: z.number(),
});

export const saleReturnItemSchema = z.object({
  saleReturnItemId: postgresUuidSchema,
  saleItemId: postgresUuidSchema,
  productId: postgresUuidSchema,
  quantity: z.number(),
  unitPrice: z.number(),
  lineTotal: z.number(),
  createdAt: z.string().datetime({ offset: true }),
});

export const saleReturnSchema = z.object({
  saleReturnId: postgresUuidSchema,
  saleId: postgresUuidSchema,
  cashSessionId: postgresUuidSchema.nullable(),
  customerAccountMovementId: postgresUuidSchema.nullable(),
  returnedByUserId: postgresUuidSchema,
  returnTotalAmount: z.number(),
  refundedPaidAmount: z.number(),
  creditedAccountAmount: z.number(),
  notes: z.string().nullable(),
  createdAt: z.string().datetime({ offset: true }),
  items: z.array(saleReturnItemSchema),
});

export const salePaymentSchema = z.object({
  salePaymentId: postgresUuidSchema,
  paymentMethod: z.string().min(1),
  amount: z.number(),
  reference: z.string().nullable(),
  providerName: z.string().nullable(),
  createdAt: z.string().datetime({ offset: true }),
});

export const saleSchema = z.object({
  saleId: postgresUuidSchema,
  ticketNumber: z.number().int().nonnegative(),
  cashSessionId: postgresUuidSchema.nullable(),
  customerId: postgresUuidSchema.nullable(),
  soldByUserId: postgresUuidSchema,
  saleChannel: z.string().min(1),
  status: z.string().min(1),
  customerName: z.string().min(1),
  subtotalAmount: z.number(),
  discountAmount: z.number(),
  totalAmount: z.number(),
  paidAmount: z.number(),
  accountBalanceAmount: z.number(),
  creditBalanceAppliedAmount: z.number(),
  dueDate: z.string().nullable(),
  currencyCode: z.string().min(1),
  reversalCashSessionId: postgresUuidSchema.nullable(),
  reversedByUserId: postgresUuidSchema.nullable(),
  reversedAt: z.string().datetime({ offset: true }).nullable(),
  notes: z.string().nullable(),
  reversalNotes: z.string().nullable(),
  createdAt: z.string().datetime({ offset: true }),
  items: z.array(saleItemSchema),
  payments: z.array(salePaymentSchema),
  returns: z.array(saleReturnSchema),
});

export const saleSummarySchema = z.object({
  saleId: postgresUuidSchema,
  ticketNumber: z.number().int().nonnegative(),
  cashSessionId: postgresUuidSchema.nullable(),
  customerId: postgresUuidSchema.nullable(),
  customerName: z.string().min(1),
  saleChannel: z.string().min(1),
  status: z.string().min(1),
  totalAmount: z.number(),
  paidAmount: z.number(),
  accountBalanceAmount: z.number(),
  creditBalanceAppliedAmount: z.number(),
  dueDate: z.string().nullable(),
  currencyCode: z.string().min(1),
  itemCount: z.number().int().nonnegative(),
  createdAt: z.string().datetime({ offset: true }),
});

export const saleListSchema = z.object({
  items: z.array(saleSummarySchema),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  total: z.number().int().nonnegative(),
});

export type AuthRole = z.infer<typeof authRoleSchema>;
export type AuthUser = z.infer<typeof authUserSchema>;
export type AuthToken = z.infer<typeof authTokenSchema>;
export type AuthSession = z.infer<typeof authSessionSchema>;
export type UserList = z.infer<typeof userListSchema>;
export type RoleList = z.infer<typeof roleListSchema>;
export type OrganizationSettings = z.infer<typeof organizationSettingsSchema>;
export type ProductCategory = z.infer<typeof productCategorySchema>;
export type Brand = z.infer<typeof brandSchema>;
export type UnitOfMeasure = z.infer<typeof unitSchema>;
export type PriceList = z.infer<typeof priceListSchema>;
export type ProductPrice = z.infer<typeof productPriceSchema>;
export type ProductSummary = z.infer<typeof productSummarySchema>;
export type ProductDetail = z.infer<typeof productDetailSchema>;
export type ProductList = z.infer<typeof productListSchema>;
export type CategoryList = z.infer<typeof categoryListSchema>;
export type BrandList = z.infer<typeof brandListSchema>;
export type UnitList = z.infer<typeof unitListSchema>;
export type PriceListList = z.infer<typeof priceListListSchema>;
export type StockPolicy = z.infer<typeof stockPolicySchema>;
export type Warehouse = z.infer<typeof warehouseSchema>;
export type StockMovement = z.infer<typeof stockMovementSchema>;
export type StockSummary = z.infer<typeof stockSummarySchema>;
export type StockDetail = z.infer<typeof stockDetailSchema>;
export type StockList = z.infer<typeof stockListSchema>;
export type StockMovementList = z.infer<typeof stockMovementListSchema>;
export type WarehouseList = z.infer<typeof warehouseListSchema>;
export type StockAlert = z.infer<typeof stockAlertSchema>;
export type StockAlertList = z.infer<typeof stockAlertListSchema>;
export type PhysicalInventoryCount = z.infer<typeof physicalInventoryCountSchema>;
export type CashRegister = z.infer<typeof cashRegisterSchema>;
export type CashSession = z.infer<typeof cashSessionSchema>;
export type CashMovement = z.infer<typeof cashMovementSchema>;
export type CashOverview = z.infer<typeof cashOverviewSchema>;
export type CashMovementList = z.infer<typeof cashMovementListSchema>;
export type Customer = z.infer<typeof customerSchema>;
export type CustomerList = z.infer<typeof customerListSchema>;
export type CustomerAccountMovement = z.infer<typeof customerAccountMovementSchema>;
export type CustomerAccountMovementList = z.infer<typeof customerAccountMovementListSchema>;
export type CustomerAccountStatement = z.infer<typeof customerAccountStatementSchema>;
export type FiscalSettings = z.infer<typeof fiscalSettingsSchema>;
export type FiscalDocument = z.infer<typeof fiscalDocumentSchema>;
export type FiscalDocumentList = z.infer<typeof fiscalDocumentListSchema>;
export type FiscalProbe = z.infer<typeof fiscalProbeSchema>;
export type PosProduct = z.infer<typeof posProductSchema>;
export type PosProductList = z.infer<typeof posProductListSchema>;
export type SaleItem = z.infer<typeof saleItemSchema>;
export type SalePayment = z.infer<typeof salePaymentSchema>;
export type SaleReturnItem = z.infer<typeof saleReturnItemSchema>;
export type SaleReturn = z.infer<typeof saleReturnSchema>;
export type Sale = z.infer<typeof saleSchema>;
export type SaleSummary = z.infer<typeof saleSummarySchema>;
export type SaleList = z.infer<typeof saleListSchema>;
