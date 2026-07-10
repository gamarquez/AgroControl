# AgroControl

AgroControl es una plataforma de gestion comercial para forrajerias argentinas. La base actual queda alineada al baseline `V0016`: identidad propia, administracion inicial, catalogo, stock por deposito, caja, POS, clientes con cuenta corriente con vencimientos, notas de credito internas, devoluciones parciales, devoluciones completas por items, saldo a favor y slice fiscal inicial.

## Estado del baseline `V0016`

- API `ASP.NET Core` sobre `.NET 10` con `Npgsql`, sin Entity Framework.
- Frontend `Next.js App Router` con `TypeScript` estricto y `Tailwind CSS`.
- PostgreSQL local por Docker Compose y despliegue objetivo con Supabase.
- Auth propia con `JWT`, refresh token rotativo y panel administrativo inicial.
- Catalogo con productos, categorias, marcas, unidades y listas de precios.
- Stock por deposito con saldos, politicas de reposicion, alertas y conteos fisicos auditables.
- Caja con apertura, movimientos manuales y cierre de sesion.
- POS con efectivo, cuenta corriente, checkout mixto, detalle de tickets, devolucion parcial, devolucion completa por items, reversa total compensatoria y aplicacion automatica de saldo a favor.
- Clientes con padron comercial, limite, saldo, saldo a favor, vencimientos, venta a cuenta, cobranza y notas de credito internas.
- Preparacion fiscal ARCA con settings, cola de comprobantes y prueba tecnica `FEDummy`.

## Estructura

```text
AgroControl/
|- apps/
|  |- api/
|  `- web/
|- infra/
|  |- docker/
|  `- supabase/
|- packages/
|  |- contracts/
|  `- ui/
|- scripts/
|  |- ci/
|  |- deploy/
|  |- dev/
|  `- qa/
|- tests/
`- docs/
```

## Requisitos locales

- .NET SDK 10
- Node.js 24 con npm
- Docker Desktop con `docker compose`

## Variables de entorno

1. Copiar `.env.example` a `.env`.
2. Ajustar API, PostgreSQL y frontend para el entorno local.
3. Usar `.env.development.example`, `.env.test.example`, `.env.staging.example`, `.env.homologation.example` y `.env.production.example` como referencia por entorno.

No hay secretos versionados.

## Desarrollo local

Levantar PostgreSQL local:

```powershell
./scripts/dev/up.ps1
```

Detener PostgreSQL local:

```powershell
./scripts/dev/down.ps1
```

Ejecutar la API:

```powershell
dotnet run --project apps/api/src/AgroControl.Api/AgroControl.Api.csproj --urls http://127.0.0.1:5080
```

Ejecutar el frontend:

```powershell
npm.cmd run dev --workspace @agrocontrol/web
```

## Endpoints disponibles

- `GET /health`
- `GET /health/ready`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout`
- `GET /api/v1/auth/me`
- `GET /api/v1/users`
- `GET /api/v1/users/roles`
- `POST /api/v1/users`
- `PATCH /api/v1/users/{userId}`
- `GET /api/v1/settings/organization`
- `PUT /api/v1/settings/organization`
- `GET /api/v1/catalog/products`
- `POST /api/v1/catalog/products`
- `GET /api/v1/catalog/products/{productId}`
- `PATCH /api/v1/catalog/products/{productId}`
- `GET /api/v1/catalog/categories`
- `POST /api/v1/catalog/categories`
- `PATCH /api/v1/catalog/categories/{categoryId}`
- `GET /api/v1/catalog/brands`
- `POST /api/v1/catalog/brands`
- `PATCH /api/v1/catalog/brands/{brandId}`
- `GET /api/v1/catalog/units`
- `GET /api/v1/catalog/price-lists`
- `POST /api/v1/catalog/price-lists`
- `PATCH /api/v1/catalog/price-lists/{priceListId}`
- `GET /api/v1/stock/warehouses`
- `POST /api/v1/stock/warehouses`
- `PATCH /api/v1/stock/warehouses/{warehouseId}`
- `GET /api/v1/stock/alerts`
- `GET /api/v1/stock/products`
- `GET /api/v1/stock/products/{productId}`
- `GET /api/v1/stock/products/{productId}/movements`
- `PUT /api/v1/stock/products/{productId}/policy`
- `POST /api/v1/stock/movements`
- `POST /api/v1/stock/physical-counts`
- `GET /api/v1/cash/overview`
- `GET /api/v1/cash/movements`
- `POST /api/v1/cash/sessions/open`
- `POST /api/v1/cash/movements`
- `POST /api/v1/cash/sessions/{cashSessionId}/close`
- `GET /api/v1/pos/products`
- `GET /api/v1/pos/sales`
- `GET /api/v1/pos/sales/{saleId}`
- `POST /api/v1/pos/sales/cash`
- `POST /api/v1/pos/sales/{saleId}/returns`
- `POST /api/v1/pos/sales/{saleId}/reverse`
- `GET /api/v1/customers`
- `GET /api/v1/customers/{customerId}`
- `POST /api/v1/customers`
- `PATCH /api/v1/customers/{customerId}`
- `GET /api/v1/customers/{customerId}/statement`
- `GET /api/v1/customers/{customerId}/account-movements`
- `POST /api/v1/customers/{customerId}/payments`
- `POST /api/v1/customers/{customerId}/credit-notes`
- `POST /api/v1/pos/sales/account`

## Migraciones SQL del baseline

- `infra/supabase/migrations/V0001__bootstrap.sql`
- `infra/supabase/migrations/V0002__auth_users_settings.sql`
- `infra/supabase/migrations/V0003__catalog_products_pricing.sql`
- `infra/supabase/migrations/V0004__stock_balances_movements.sql`
- `infra/supabase/migrations/V0005__cash_sessions_movements.sql`
- `infra/supabase/migrations/V0006__sales_pos_cash.sql`
- `infra/supabase/migrations/V0007__sales_reversals.sql`
- `infra/supabase/migrations/V0008__customers_accounts.sql`
- `infra/supabase/migrations/V0009__sales_checkout_payments.sql`
- `infra/supabase/migrations/V0010__fiscal_arca_readiness.sql`
- `infra/supabase/migrations/V0011__warehouses_inventory_alerts.sql`
- `infra/supabase/migrations/V0012__sales_customer_statements.sql`
- `infra/supabase/migrations/V0013__customer_credit_notes.sql`
- `infra/supabase/migrations/V0014__sales_partial_returns.sql`
- `infra/supabase/migrations/V0015__customer_credit_balances.sql`
- `infra/supabase/migrations/V0016__sales_full_returns_status.sql`

## Administrador local

Para crear o actualizar el administrador de desarrollo sobre PostgreSQL local:

```powershell
./scripts/dev/bootstrap-admin.ps1
```

Con parametros explicitos:

```powershell
./scripts/dev/bootstrap-admin.ps1 -Email admin@miempresa.local -Password "ClaveSegura123!" -DisplayName "Admin Local"
```

## Validaciones del repo

Backend:

```powershell
./scripts/ci/run-dotnet.ps1 restore
./scripts/ci/run-dotnet.ps1 build
./scripts/ci/run-dotnet.ps1 test
```

Frontend:

```powershell
npm.cmd install
npm.cmd run lint:web
npm.cmd run typecheck:web
npm.cmd run test:web
npm.cmd run build:web
```

Smoke y E2E:

```powershell
npm.cmd run qa:api:smoke
npm.cmd run test:e2e
docker compose -f docker-compose.yml config
```

## Notas

- El backend usa `Npgsql` y no usa Entity Framework.
- El frontend usa cookies HTTP-only del lado del servidor web para conservar `access token` y `refresh token`.
- El login y el panel administrativo estan en espanol y consumen la API desde el servidor de Next.js.
- El baseline `V0011` ya modela depositos e inventario fisico, pero el POS todavia consume el deposito default y no selecciona deposito explicitamente.
- OpenAPI/Swagger sigue diferido para un incremento posterior validado especificamente.

## Documentacion relacionada

- `AGENTS.md`
- `PROMPT_MAESTRO.md`
- `docs/CHANGELOG_LOCAL.txt`
- `docs/PROJECT_SUMMARY_NOTION.md`
- `docs/DECISIONS.md`
- `docs/ROADMAP.md`
- `docs/operations/local-setup.md`
- `docs/operations/production-deploy.md`
