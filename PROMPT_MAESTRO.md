# Prompt maestro para Codex — AgroControl

Actúa como un equipo senior de producto y desarrollo responsable de diseñar e implementar **AgroControl**, una plataforma web de gestión comercial para forrajerías argentinas.

## 1. Objetivo del producto

AgroControl debe centralizar ventas, stock, clientes, proveedores, cuentas corrientes, caja, facturación electrónica y análisis del negocio.

El sistema debe ser simple para el uso diario en mostrador, pero suficientemente robusto para operar múltiples sucursales en una etapa posterior.

## 2. Stack obligatorio

### Backend
- .NET 10
- ASP.NET Core Web API
- C#
- Arquitectura modular basada en Clean Architecture, sin sobreingeniería
- Acceso a datos sin Entity Framework
- Npgsql
- Stored Procedures/PostgreSQL functions siempre que sea posible
- SQL directo solo en casos justificados
- OpenAPI/Swagger
- FluentValidation
- Serilog
- JWT con refresh tokens
- Tests con xUnit

### Frontend
- Next.js con App Router
- TypeScript estricto
- React
- Tailwind CSS
- shadcn/ui
- React Hook Form
- Zod
- TanStack Query
- Diseño responsive y accesible
- Tests unitarios y de componentes
- Tests E2E para flujos críticos

### Persistencia e infraestructura
- Supabase PostgreSQL
- Supabase Storage para archivos y comprobantes
- Migraciones controladas mediante scripts SQL versionados o herramienta liviana aprobada, incluyendo tablas, índices, permisos y Stored Procedures/PostgreSQL functions
- Row Level Security cuando corresponda
- Docker para desarrollo local
- Variables de entorno documentadas
- CI con build, lint y tests

## 3. Contexto funcional

El producto está orientado inicialmente a una forrajería argentina que comercializa alimentos balanceados, semillas, accesorios, productos veterinarios permitidos, artículos rurales y productos vendidos por unidad, bolsa, kilo, litro u otras unidades.

La moneda inicial es ARS. Las fechas y horarios deben funcionar con la zona horaria de Argentina.

## 4. Módulos funcionales

### 4.1 Autenticación y usuarios
- Inicio y cierre de sesión.
- Recuperación de contraseña.
- Administración de usuarios.
- Roles iniciales:
  - Administrador.
  - Encargado.
  - Vendedor.
  - Cajero.
  - Consulta.
- Permisos configurables por módulo y acción.
- Auditoría de operaciones sensibles.

### 4.2 Productos y stock
- Alta, baja lógica y modificación de productos.
- Categorías, marcas, unidades de medida y presentaciones.
- Código interno, SKU y código de barras.
- Precio de costo, margen y precio de venta.
- Múltiples listas de precios.
- Stock actual, mínimo, máximo y punto de reposición.
- Stock por depósito o sucursal.
- Movimientos:
  - Ingreso por compra.
  - Egreso por venta.
  - Ajuste positivo o negativo.
  - Devolución.
  - Transferencia.
  - Pérdida, rotura o vencimiento.
- Trazabilidad completa de movimientos.
- Productos vendidos por peso o cantidad fraccionada.
- Alertas de stock bajo.
- Inventarios y conciliación física.
- Historial de costos y precios.

### 4.3 Punto de venta
- Interfaz optimizada para atención rápida.
- Búsqueda por descripción, SKU o código de barras.
- Carrito de venta.
- Descuentos por ítem o venta total, sujetos a permisos.
- Venta a consumidor final o cliente registrado.
- Formas de pago:
  - Efectivo.
  - Transferencia.
  - QR.
  - Tarjeta mediante POS/posnet.
  - Cuenta corriente.
  - Pago combinado.
- Registro de referencia, operación, terminal y proveedor de pago.
- Apertura, arqueo y cierre de caja.
- Cancelación y devolución con trazabilidad.
- Emisión o impresión de ticket interno.
- Generación del comprobante fiscal correspondiente mediante ARCA.

### 4.4 Integración fiscal con ARCA
- Diseñar una abstracción `IFiscalService`.
- Implementar un adaptador para facturación electrónica de ARCA.
- No acoplar reglas fiscales al módulo de ventas.
- Soportar inicialmente:
  - Factura.
  - Nota de crédito.
  - Consulta del último comprobante autorizado.
  - Obtención y almacenamiento de CAE y vencimiento.
- Guardar request, response, estado, errores y reintentos.
- Diferenciar homologación y producción.
- Proteger certificados, claves y secretos.
- Implementar idempotencia para evitar comprobantes duplicados.
- Permitir contingencia: una venta puede quedar pendiente de fiscalización y reintentarse.
- No inventar endpoints, contratos o reglas de ARCA. Toda implementación debe basarse en documentación oficial vigente y quedar aislada detrás del adaptador.

### 4.5 Clientes y cuenta corriente
- Registro y administración de clientes.
- Datos fiscales y comerciales.
- Límite de crédito.
- Saldo actual.
- Movimientos de débito y crédito.
- Ventas a cuenta corriente.
- Pagos parciales o totales.
- Ajustes autorizados.
- Estado de cuenta.
- Antigüedad de saldos.
- Alertas por deuda vencida o límite superado.
- Bloqueo o autorización especial de nuevas ventas.
- Impresión y exportación del estado de cuenta.

### 4.6 Proveedores
- Alta, modificación y baja lógica.
- Datos fiscales y de contacto.
- Productos ofrecidos.
- Condiciones de pago.
- Historial de compras.
- Cuenta corriente del proveedor.
- Evaluación básica:
  - Precio.
  - Tiempo de entrega.
  - Cumplimiento.
  - Calidad.
- Documentos y observaciones.

### 4.7 Cotizaciones y comparación de proveedores
- Crear solicitudes de cotización.
- Incluir múltiples productos y cantidades.
- Enviar o registrar cotizaciones recibidas.
- Comparar proveedores por:
  - Precio unitario.
  - Impuestos.
  - Bonificaciones.
  - Flete.
  - Plazo de entrega.
  - Forma de pago.
  - Vigencia.
- Normalizar el costo total comparable.
- Seleccionar una oferta completa o dividir la compra entre proveedores.
- Flujo de estados:
  - Borrador.
  - Solicitada.
  - Recibida.
  - Evaluada.
  - Aprobada.
  - Rechazada.
  - Convertida en orden de compra.
- Registrar quién evaluó y aprobó.

### 4.8 Compras
- Órdenes de compra.
- Recepción total o parcial.
- Actualización de stock.
- Registro de costo real.
- Diferencias entre pedido, recepción y factura.
- Cuentas a pagar.
- Vencimientos.
- Asociación con cotizaciones.

### 4.9 Caja y libro diario
- Apertura y cierre de caja.
- Ingresos y egresos manuales categorizados.
- Ingresos automáticos por ventas y cobranzas.
- Egresos automáticos por pagos y gastos.
- Libro diario con:
  - Fecha.
  - Concepto.
  - Categoría.
  - Medio de pago.
  - Ingreso.
  - Egreso.
  - Saldo.
  - Usuario.
  - Referencia.
- Conciliación entre efectivo, QR, transferencia, tarjetas y cuenta corriente.
- No permitir modificar movimientos confirmados sin contramovimiento y auditoría.

### 4.10 Reportes
- Stock actual valorizado.
- Stock bajo y productos sin movimiento.
- Kardex por producto.
- Consumos y ventas por período.
- Ventas por producto, categoría, vendedor y medio de pago.
- Rentabilidad estimada.
- Facturación.
- Comprobantes fiscales pendientes o rechazados.
- Cuentas corrientes de clientes.
- Deudas con proveedores.
- Caja y flujo de fondos.
- Comparativa de proveedores.
- Exportación CSV y PDF.
- Filtros persistentes y paginación.

### 4.11 Configuración
- Datos del comercio.
- Punto de venta fiscal.
- Condición tributaria.
- Certificados y parámetros de ARCA.
- Medios de pago.
- Terminales POS.
- Proveedores de QR.
- Categorías contables.
- Listas de precios.
- Numeración interna de documentos.

## 5. Requisitos no funcionales

- API REST versionada.
- Contratos explícitos mediante DTOs.
- Validación del lado servidor y cliente.
- Operaciones monetarias con `decimal`, nunca `float` o `double`.
- Guardar importes con precisión definida.
- Usar transacciones en venta, pagos, stock, caja y cuenta corriente.
- Idempotencia en integraciones externas y operaciones críticas.
- Soft delete para maestros cuando sea necesario.
- Auditoría con usuario, fecha, acción y cambios relevantes.
- Concurrencia optimista en stock.
- Paginación y filtros en listados.
- Índices de base de datos para búsquedas habituales.
- Logs estructurados sin exponer secretos ni datos sensibles.
- Manejo centralizado de errores con Problem Details.
- Health checks.
- Rate limiting para endpoints sensibles.
- Protección contra OWASP Top 10.
- Accesibilidad WCAG AA en interfaces principales.
- Rendimiento aceptable con catálogos de al menos 100.000 productos y millones de movimientos.
- Código, nombres técnicos y commits en inglés.
- Interfaz y documentación funcional en español.


### 5.1 Persistencia sin Entity Framework

- No usar Entity Framework ni EF Core.
- Usar Npgsql para conectarse a Supabase/PostgreSQL.
- Priorizar Stored Procedures/PostgreSQL functions para operaciones de negocio.
- Implementar como rutinas de base de datos, siempre que sea posible:
  - Venta completa.
  - Movimiento de stock.
  - Pagos y pagos combinados.
  - Apertura, movimientos y cierre de caja.
  - Cuenta corriente de clientes.
  - Cuenta corriente de proveedores.
  - Compras y recepción de mercadería.
  - Generación de movimientos contables/libro diario.
  - Registro fiscal y reintentos.
  - Reversas, anulaciones y devoluciones.
- Cada rutina debe tener contrato documentado:
  - Nombre.
  - Parámetros de entrada.
  - Resultado esperado.
  - Tablas afectadas.
  - Validaciones.
  - Manejo de errores.
  - Reglas de idempotencia.
  - Comportamiento transaccional.
- El API debe invocar las rutinas mediante Npgsql con parámetros.
- SQL directo desde el API solo debe usarse en casos justificados como health checks, lecturas simples o catálogos livianos.
- No duplicar reglas de negocio complejas entre C# y PostgreSQL.

## 6. Modelo de dominio inicial

Considerar como mínimo las siguientes entidades:

- Organization
- Branch
- Warehouse
- User
- Role
- Permission
- Customer
- CustomerAccount
- CustomerAccountMovement
- Supplier
- SupplierAccount
- SupplierAccountMovement
- Product
- ProductCategory
- Brand
- UnitOfMeasure
- PriceList
- ProductPrice
- StockBalance
- StockMovement
- InventoryCount
- Sale
- SaleItem
- Payment
- PaymentMethod
- CashRegister
- CashSession
- CashMovement
- FiscalDocument
- FiscalRequestLog
- PurchaseQuoteRequest
- SupplierQuote
- SupplierQuoteItem
- PurchaseOrder
- PurchaseOrderItem
- GoodsReceipt
- Expense
- AuditLog

Definir agregados, invariantes, relaciones y estados antes de implementar.

## 7. Arquitectura sugerida

Usar un monorepo:

```text
AgroControl/
├── AGENTS.md
├── .codex/
│   ├── config.toml
│   └── agents/
├── apps/
│   ├── api/
│   └── web/
├── packages/
│   ├── contracts/
│   └── ui/
├── infra/
│   ├── docker/
│   └── supabase/
├── docs/
└── tests/
```

Backend:

```text
apps/api/src/
├── AgroControl.Api
├── AgroControl.Application
├── AgroControl.Domain
├── AgroControl.Infrastructure
└── AgroControl.Contracts
```

Evitar repositorios genéricos innecesarios. No usar Entity Framework. Priorizar Stored Procedures/PostgreSQL functions para operaciones de negocio y transacciones críticas. Usar SQL directo desde la API solo cuando esté justificado. Separar lectura y escritura solamente donde aporte claridad o rendimiento.

## 8. Estrategia de implementación

Implementar por incrementos verticales funcionales:

1. Base del monorepo, autenticación, usuarios y configuración.
2. Catálogo de productos, unidades, categorías y precios.
3. Stock y movimientos.
4. Caja y sesiones.
5. Punto de venta con efectivo.
6. Clientes y cuenta corriente.
7. Pagos QR, transferencia, POS y pagos combinados.
8. Integración fiscal ARCA.
9. Proveedores y compras.
10. Cotizaciones comparativas.
11. Reportes y panel.
12. Fortalecimiento de seguridad, rendimiento y observabilidad.

Cada incremento debe incluir:
- Migración de base de datos.
- Backend.
- Frontend.
- Validaciones.
- Autorización.
- Tests.
- Documentación.
- Criterios de aceptación verificables.

## 9. Reglas de trabajo para Codex

Antes de modificar código:

1. Leer `AGENTS.md`.
2. Revisar la estructura y convenciones existentes.
3. Explicar brevemente el alcance y los archivos afectados.
4. Identificar supuestos y riesgos.
5. Implementar el cambio más pequeño que complete el objetivo.
6. No modificar archivos no relacionados.
7. Ejecutar build, lint y tests relevantes.
8. Documentar cualquier tarea pendiente real.
9. No simular que una integración externa funciona sin pruebas.
10. No agregar dependencias sin justificar su uso.

Cuando una tarea abarque varias disciplinas, usar los agentes personalizados del proyecto y consolidar sus resultados.



### 9.1 Agente de documentación

Usar el agente `documentation_notion` después de cambios relevantes para:

- Documentar cambios en `docs/CHANGELOG_LOCAL.txt`.
- Mantener actualizado `docs/PROJECT_SUMMARY_NOTION.md` como resumen organizado y listo para Notion.
- Registrar decisiones duraderas en `docs/DECISIONS.md`.
- Preparar o actualizar Notion cuando el conector esté disponible y se indique la página destino.
- No incluir secretos, certificados, claves privadas, tokens ni payloads fiscales sensibles.

## 10. Primera tarea para Codex

Inicializa el repositorio de AgroControl:

- Crea la estructura del monorepo.
- Genera la solución .NET 10 y los proyectos indicados, sin paquetes de Entity Framework.
- Genera la aplicación Next.js con TypeScript, Tailwind y App Router.
- Configura Supabase PostgreSQL mediante variables de entorno.
- Agrega Docker Compose para dependencias locales necesarias.
- Configura formato, lint, tests y CI.
- Implementa health check de API.
- Implementa una pantalla inicial del frontend que consulte el health check.
- Crea documentación de instalación en `README.md`.
- Crea y actualiza `docs/CHANGELOG_LOCAL.txt`, `docs/PROJECT_SUMMARY_NOTION.md` y `docs/DECISIONS.md` usando el agente `documentation_notion`.
- Incluye `.env.example` sin secretos.
- Ejecuta todas las validaciones disponibles y corrige los errores.
- Entrega un resumen de decisiones, comandos ejecutados, archivos creados y próximos pasos.
