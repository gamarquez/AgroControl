# AgroControl - Resumen del proyecto

## 1. Vision general

AgroControl es un sistema de gestion para forrajerias argentinas orientado a ventas, stock, caja, cuentas corrientes, compras, proveedores, cotizaciones y reportes.

## 2. Estado actual

El baseline tecnico ahora alcanza `V0016` y deja una base funcional para identidad, administracion, catalogo, stock por deposito, caja, POS y clientes con cuenta corriente con vencimientos, notas de credito internas, devoluciones parciales, devoluciones completas por items y saldo a favor:

- Monorepo inicial listo.
- API `.NET 10` compilable y testeada.
- Frontend `Next.js` compilable y testeado.
- Migraciones SQL versionadas hasta `V0016`.
- PostgreSQL local por Docker Compose.
- CI y scripts de validacion del repositorio.
- Autenticacion propia con `JWT` y refresh token rotativo.
- Panel inicial de usuarios y configuracion del comercio.
- Catalogo operativo con productos, categorias, marcas, unidades y precios base.
- Stock por deposito con saldos, politica de reposicion, alertas y conteo fisico auditable.
- Caja con sesiones, movimientos manuales y cierre.
- POS con efectivo, cuenta corriente, checkout mixto, trazabilidad de tickets, devolucion parcial, devolucion completa por items, reversa total y consumo automatico de saldo a favor, sobre deposito default.
- Clientes y cuenta corriente con limite de credito, venta a cuenta, cobranza FIFO, estado de cuenta, vencimientos, notas de credito internas y saldo a favor.
- Slice fiscal inicial con configuracion ARCA, cola de comprobantes y prueba tecnica `FEDummy`.
- Base de despliegue inicial preparada para Supabase, Render y Vercel.

Estado operativo de Supabase al 2026-07-21:

- Se reejecutaron via MCP las migraciones locales `V0001` a `V0016` en orden, sin agregar nuevas entradas al historial de migraciones.
- Se verificaron 34 tablas y 23 funciones.
- Seeds presentes: 1 organizacion con su configuracion, 5 roles, 22 permisos, 77 asignaciones de permisos, 6 unidades de medida, 1 lista de precios, 1 caja, 1 deposito y 1 configuracion fiscal.
- No hay usuarios ni administrador (`0`); el acceso inicial requiere ejecutar el bootstrap con credenciales seguras.

## 3. Arquitectura implementada

### Backend

- `apps/api/src/AgroControl.Api`
- `apps/api/src/AgroControl.Application`
- `apps/api/src/AgroControl.Domain`
- `apps/api/src/AgroControl.Infrastructure`
- `apps/api/src/AgroControl.Contracts`

Puntos implementados:

- `ProblemDetails` y manejo global de errores.
- Logging JSON.
- `GET /health` y `GET /health/ready`.
- Autenticacion propia con login, refresh, logout y `me`.
- Administracion de usuarios, roles y configuracion de organizacion.
- Catalogo con productos, categorias, marcas, unidades y listas de precios.
- Stock con depositos, alertas, conteo fisico, consulta de detalle, movimientos y politica de reposicion por ubicacion.
- Caja con overview, movimientos, apertura y cierre.
- POS con listado de productos, ventas en efectivo, ventas a cuenta, checkout mixto, detalle, devolucion parcial, devolucion completa por items, reversa total y aplicacion automatica de saldo a favor.
- Fiscal con settings, cola de comprobantes y prueba tecnica de conectividad.
- Clientes con alta, edicion, movimientos de cuenta, cobranza, nota de credito, saldo a favor y venta a cuenta.
- Acceso a PostgreSQL/Supabase via `Npgsql`.
- Sin Entity Framework.
- Auditoria de login, refresh, logout, usuarios, catalogo, stock, caja, ventas y clientes.

### Frontend

- `apps/web` con Next.js App Router.
- TypeScript estricto.
- Tailwind CSS.
- Componentes base estilo shadcn/ui.
- Login en espanol con estados claros.
- Layout autenticado con resolucion de sesion desde la API.
- Pantallas de usuarios y configuracion del comercio.
- Dashboard de catalogo con formularios operativos y filtros.
- Dashboard de stock con filtros por deposito, alertas, conteo fisico, politicas y movimientos recientes.
- Dashboard de caja con apertura, movimientos manuales y cierre.
- Dashboard POS para ventas de mostrador con checkout mixto, devolucion parcial, devolucion completa por items y reversa total.
- Dashboard de clientes con padron comercial, saldo, saldo a favor, limite, movimientos, venta a cuenta y cobranza.
- Cookies HTTP-only del lado del servidor web para no exponer refresh tokens al cliente.
- Los contratos de autenticacion y los `organizationId` de configuracion general/fiscal validan identificadores PostgreSQL mediante `z.guid()`, evitando rechazar UUID semilla validos que no declaran variante RFC 4122.

### Base de datos

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

Tablas y aggregates principales del baseline:

- `organizations`
- `branches`
- `users`
- `roles`
- `permissions`
- `role_permissions`
- `user_roles`
- `organization_settings`
- `audit_logs`
- `refresh_sessions`
- `product_categories`
- `brands`
- `units_of_measure`
- `products`
- `price_lists`
- `product_prices`
- `stock_balances`
- `stock_movements`
- `warehouses`
- `physical_inventory_counts`
- `customer_payment_allocations`
- `cash_registers`
- `cash_sessions`
- `cash_movements`
- `sales`
- `sale_items`
- `sale_returns`
- `sale_return_items`
- `customers`
- `customer_account_movements`

### Estado de seguridad y advisors de Supabase

- RLS se encontro deshabilitado en las 34 tablas inspeccionadas. No se habilito durante la recuperacion: antes requiere definir politicas explicitas, alcance por organizacion y pruebas de autorizacion. La aplicacion actualmente accede a PostgreSQL desde la API mediante `Npgsql`.
- El advisor de seguridad reporto 23 warnings `function_search_path_mutable`; deben corregirse mediante cambios SQL versionados y revision de compatibilidad de cada funcion.
- El advisor de performance reporto 38 `unindexed_foreign_keys` y 43 `unused_index`.
- Los indices faltantes deben priorizarse segun filtros y joins reales. Los indices marcados como no usados no deben eliminarse solo con esta medicion, porque el proyecto recuperado aun no tiene una carga representativa.

## 4. Infraestructura local y CI

- `docker-compose.yml` y `infra/docker/docker-compose.yml` para PostgreSQL local.
- `.env.example` y variantes por entorno sin secretos.
- GitHub Actions con validacion del repositorio, restore/build/test backend y lint/test/build frontend.
- Scripts en `scripts/ci`, `scripts/dev`, `scripts/deploy` y `scripts/qa`.
- El `Dockerfile` de la API para Render debe copiar todos los `.csproj` del grafo referenciado antes de `dotnet restore`; omitir `AgroControl.Domain` provoca `NETSDK1004` durante `dotnet publish --no-restore`.
- La API se despliega en Render creando un `Web Service` Docker desde el Dashboard y conectandolo al repositorio; el flujo vigente no usa Blueprint ni `render.yaml`. Todas las variables y secretos del servicio se configuran desde `Environment`; `Auth__Issuer` y `AllowedHosts` usan los valores concretos de la URL y el host publicos que Render asigna al servicio.

## 5. Validaciones esperadas

Validacion del fix de contratos de login al 2026-07-21:

- 19 pruebas web aprobadas, incluida la regresion para el `organization_id` semilla de PostgreSQL.
- Lint web aprobado.
- Typecheck web aprobado.
- Build web de produccion aprobado.
- No se realizo despliegue ni sincronizacion con Notion.

Validacion operativa mas reciente:

- Reejecucion via MCP de `V0001` a `V0016` durante el 2026-07-20/21.
- Verificacion posterior de 34 tablas, 23 funciones y los seeds del baseline.
- Inspeccion de advisors de seguridad y performance.
- No se ejecutaron en esta recuperacion builds, pruebas de API/web ni flujos funcionales end-to-end.

- `./scripts/ci/run-dotnet.ps1 restore`
- `./scripts/ci/run-dotnet.ps1 build`
- `./scripts/ci/run-dotnet.ps1 test`
- `npm.cmd install`
- `npm.cmd run lint:web`
- `npm.cmd run typecheck:web`
- `npm.cmd run test:web`
- `npm.cmd run build:web`
- `npm.cmd run qa:api:smoke`
- `npm.cmd run test:e2e`
- `docker compose -f docker-compose.yml config`

## 6. Decisiones vigentes

- Persistencia con `Npgsql` y sin Entity Framework.
- Rutinas SQL reservadas para workflows transaccionales de negocio.
- `StockBalance` como proyeccion operativa derivada, no como fuente editable de verdad.
- PostgreSQL local en Docker Compose con API y frontend corriendo fuera de Docker.
- OpenAPI diferido hasta validar una variante estable para este stack.
- Sesion web mediada por Next.js con cookies HTTP-only y auth propietaria en la API.
- Los identificadores provenientes de PostgreSQL en contratos web usan validacion de GUID compatible con PostgreSQL; no se exige variante RFC 4122 cuando la base no la garantiza.
- Catalogo, stock, caja y POS del baseline operan en una sola locacion.
- Reversa inicial de ventas solo en modalidad total, mediante compensaciones.
- Base de despliegue preparada para Supabase, Render y Vercel sin versionar secretos.

## 7. Riesgos y preguntas abiertas

- El login MVP asume una organizacion activa por email; falta selector explicito para escenarios multi-tenant con emails repetidos.
- Definir politica de RLS y acceso directo o no del frontend a Supabase.
- Crear el primer usuario/administrador en el proyecto Supabase recuperado; el bootstrap esta pendiente porque requiere credenciales.
- Resolver mediante migraciones versionadas los 23 warnings `function_search_path_mutable` y revisar los 38 `unindexed_foreign_keys`.
- Reevaluar los 43 `unused_index` cuando exista carga e historial de consultas representativos.
- Implementar recuperacion de contrasena y endurecimiento adicional de seguridad.
- Implementar OpenAPI/Swagger en un incremento posterior.
- El POS aun consume el deposito default; falta selector operativo por caja/POS cuando se habilite multi-deposito real.
- Faltan refinanciaciones, acuerdos de pago con vista historica de anticipos y acople fiscal de las devoluciones parciales.
- Faltan transferencias entre depositos, compras/proveedores y reportes consolidados multi-deposito.
- Definir si los previews de Vercel apuntaran a la misma API de produccion o a una API separada con politica CORS dedicada.
