# AgroControl - Decisiones

## ADR-0001 - Persistencia sin Entity Framework

- Estado: aprobada.
- Decision: el backend usa `Npgsql` y no usa Entity Framework.
- Consecuencia: el acceso a datos queda controlado por infraestructura explicita y preparado para rutinas SQL de negocio.

## ADR-0002 - Rutinas SQL para workflows criticos

- Estado: aprobada.
- Decision: ventas, stock, pagos, caja, cuentas corrientes, compras, fiscalizacion y reversas deberan implementarse con Stored Procedures/PostgreSQL functions cuando el slice lo requiera.
- Consecuencia: el bootstrap inicial no duplica reglas de negocio entre C# y PostgreSQL.

## ADR-0003 - Bootstrap tecnico acotado en el incremento 1

- Estado: aprobada.
- Decision: el incremento 1 se limita a monorepo, health checks, conectividad, frontend inicial, CI, migracion bootstrap y documentacion.
- Consecuencia: autenticacion, usuarios operativos, catalogo, stock y demas modulos quedan para incrementos posteriores.

## ADR-0004 - Organization-aware desde el dia 1

- Estado: aprobada.
- Decision: el modelo base incorpora `organizations`, `branches`, autorizacion y auditoria desde la primera migracion.
- Consecuencia: los siguientes modulos podran crecer sin asumir un sistema mono-tenant accidental.

## ADR-0005 - Stock y saldos futuros basados en movimientos

- Estado: aprobada.
- Decision: balances futuros de stock, caja y cuentas corrientes deben derivarse de movimientos inmutables.
- Consecuencia: el bootstrap no introduce tablas de saldos como fuente editable de verdad.

## ADR-0006 - Frontend inicial con App Router y estados visibles

- Estado: aprobada.
- Decision: `apps/web` usa Next.js App Router, TypeScript estricto, Tailwind y componentes base con estados de carga/error/success visibles desde la primera pantalla.
- Consecuencia: QA puede validar el slice `web -> api -> health` desde el primer incremento.

## ADR-0007 - PostgreSQL local minimo con Docker Compose

- Estado: aprobada.
- Decision: el entorno local del bootstrap usa Docker Compose solo para PostgreSQL.
- Consecuencia: API y frontend corren fuera de Docker, reduciendo complejidad de arranque.

## ADR-0008 - OpenAPI diferido

- Estado: aprobada.
- Decision: OpenAPI/Swagger queda temporalmente fuera del bootstrap final para priorizar una base estable en el entorno actual.
- Consecuencia: la documentacion HTTP automatica debe reincorporarse en una tarea posterior validada especificamente.

## ADR-0009 - Auth propia en la API con JWT y refresh rotativo

- Estado: aprobada.
- Decision: el incremento 2 usa autenticacion propia de la API con `JWT` corto, refresh token rotativo persistido en base y resolucion del usuario actual desde claims.
- Consecuencia: catalogo, stock y caja pueden construirse sobre una identidad estable sin depender de Supabase Auth.

## ADR-0010 - Sesion web mediada por Next.js

- Estado: aprobada.
- Decision: el frontend conserva `access token` y `refresh token` en cookies HTTP-only del lado del servidor web y resuelve la sesion desde la API.
- Consecuencia: se evita guardar tokens sensibles en el cliente y se centraliza el acceso tipado a la API en un BFF liviano.

## ADR-0011 - Catalogo inicial single-location con scope por organizacion

- Estado: aprobada.
- Decision: el incremento 3 conserva `organization_id` por compatibilidad del modelo, pero opera el catalogo como sistema de una sola locacion sin `branch_id`, `warehouse_id` ni stock por ubicacion.
- Consecuencia: el siguiente slice puede sumar movimientos y stock sin rehacer producto, manteniendo el costo conceptual bajo para el MVP actual.

## ADR-0012 - Precio vigente simple por lista

- Estado: aprobada.
- Decision: el catalogo inicial mantiene un unico precio vigente por `product_id + price_list_id`, con una lista `standard` seeded y una sola lista default activa por organizacion.
- Consecuencia: se habilita operacion comercial basica sin introducir todavia versionado historico de precios, promociones ni reglas avanzadas de redondeo.

## ADR-0013 - Stock inicial single-location por producto

- Estado: aprobada.
- Decision: el incremento 4 modela stock con un unico saldo por `product_id` y `organization_id`, sin `warehouse_id` ni `branch_id` mientras el sistema siga operando en una sola locacion.
- Consecuencia: se habilitan consultas, alertas y ajustes de stock sin introducir complejidad prematura de depositos o transferencias.

## ADR-0014 - Movimientos de stock inmutables y politica separada

- Estado: aprobada.
- Decision: cada cambio de stock se registra como movimiento inmutable en `stock_movements`, mientras la politica de min/max/reposicion se mantiene en `stock_balances` con versionado optimista simple.
- Consecuencia: ventas, compras, devoluciones e inventarios futuros podran apoyarse en la misma trazabilidad sin editar historiales confirmados.

## ADR-0015 - Despliegue inicial separado: Supabase + Render + Vercel

- Estado: aprobada.
- Decision: la primera salida a produccion usa Supabase solo como PostgreSQL gestionado, la API en un Web Service Docker de Render creado desde el Dashboard y conectado al repositorio, y el frontend en Vercel con subdominios separados. `render.yaml` no forma parte del procedimiento operativo vigente.
- Consecuencia: se reduce complejidad operativa inicial, la configuracion efectiva de Render queda explicita en el Dashboard y se evita acoplar el frontend a secretos o capacidades no usadas todavia de Supabase.

## ADR-0016 - Migraciones y bootstrap fuera del arranque de la API

- Estado: aprobada.
- Decision: las migraciones SQL y el bootstrap del administrador se ejecutan mediante scripts operativos externos al ciclo de arranque de la API.
- Consecuencia: el despliegue queda mas controlable y auditable, y se evita mezclar startup HTTP con cambios de esquema en produccion.

## ADR-0017 - Caja MVP con una sola caja principal y sesiones auditables

- Estado: aprobada.
- Decision: el incremento actual modela una sola caja principal por organizacion con sesiones de apertura/cierre, movimientos manuales inmutables y saldo teorico mantenido en base mediante rutinas SQL.
- Consecuencia: el POS efectivo puede apoyarse sobre una base operativa y auditable sin introducir todavia conciliacion avanzada, multiples cajas ni automatizacion de ventas/cobranzas.

## ADR-0018 - POS inicial solo efectivo con venta transaccional

- Estado: aprobada.
- Decision: la primera venta de mostrador se implementa como `POS efectivo` para consumidor final, con confirmacion atomica en base de datos: venta, items, salida de stock y acreditacion de caja en una sola rutina SQL.
- Consecuencia: se habilita operacion comercial real con ticket interno y bajo riesgo de inconsistencias, difiriendo descuentos, pagos combinados, devoluciones y fiscalizacion para incrementos posteriores.

## ADR-0019 - Reversa total por compensacion, no por edicion

- Estado: aprobada.
- Decision: una venta confirmada del POS no se modifica ni se elimina; la correccion inicial se hace mediante reversa total con compensacion transaccional de stock y caja, y cambio de estado de la venta original a `reversed`.
- Consecuencia: se preserva la trazabilidad operativa y financiera, dejando las devoluciones parciales y la compensacion fiscal para incrementos posteriores.

## ADR-0020 - Cuenta corriente inicial integrada a ventas y caja

- Estado: aprobada.
- Decision: el primer slice de clientes y cuenta corriente modela `customers` y `customer_account_movements` con limite de credito, venta a cuenta y cobranza como operaciones transaccionales resueltas en rutinas SQL.
- Consecuencia: las ventas a cuenta descuentan stock y debitan saldo del cliente en una sola operacion, mientras las cobranzas acreditan cuenta corriente e impactan caja sin editar movimientos confirmados.

## ADR-0021 - Stock por deposito con default operativo

- Estado: aprobada.
- Decision: el sistema deja de asumir un unico saldo por producto y pasa a modelar `warehouses` y `stock_balances` por `warehouse_id`, manteniendo un deposito default obligatorio por organizacion para preservar compatibilidad con el POS actual.
- Consecuencia: el modulo stock ya soporta multiples depositos e inventario fisico, mientras ventas y reversas siguen operando de forma segura sobre la ubicacion default hasta introducir seleccion explicita en caja/POS.

## ADR-0022 - Conteo fisico como ajuste auditable

- Estado: aprobada.
- Decision: cada inventario fisico se registra en `physical_inventory_counts` y, si existe diferencia, genera un movimiento inmutable de ajuste en `stock_movements` en la misma transaccion.
- Consecuencia: el conteo real corrige el saldo operativo sin editar historiales previos y deja trazabilidad separada entre acto de conteo y ajuste aplicado.

## ADR-0023 - Cuenta corriente con vencimientos y asignacion FIFO

- Estado: aprobada.
- Decision: los debitos de cuenta corriente registran `due_date` y las cobranzas se asignan por FIFO mediante `customer_payment_allocations`, calculando saldo abierto y saldo vencido por documento.
- Consecuencia: el estado de cuenta deja de ser solo un saldo agregado y pasa a soportar vencimientos reales, seguimiento operativo y futuras extensiones como refinanciaciones o notas de credito.

## ADR-0024 - Notas de credito internas como creditos compensatorios

- Estado: aprobada.
- Decision: las notas de credito de cuenta corriente se registran como movimientos `credit_note` inmutables, sin impacto en caja y con asignacion FIFO sobre debitos abiertos mediante `customer_payment_allocations`.
- Consecuencia: se puede corregir saldo comercial del cliente sin editar ventas ni cobranzas confirmadas, manteniendo trazabilidad y dejando para una etapa posterior el soporte explicito de saldo a favor.

## ADR-0025 - Devolucion parcial como entidad propia sobre la venta

- Estado: aprobada.
- Decision: la devolucion parcial no modifica ni elimina `sale_items`; se modela con `sale_returns` y `sale_return_items`, reingresa stock, reintegra proporcionalmente importes cobrados y acredita cuenta corriente cuando corresponde.
- Consecuencia: la venta original preserva trazabilidad completa y el sistema puede diferenciar reversa total de devolucion parcial, dejando para una etapa posterior el acople fiscal y la eleccion manual del medio exacto de reintegro.

## ADR-0026 - Saldo a favor como balance negativo consumible

- Estado: aprobada.
- Decision: la cuenta corriente del cliente puede quedar con `resulting_balance` negativo para representar saldo a favor, y nuevas ventas a cuenta o con componente `account` deben consumir primero ese credito disponible mediante asignaciones en `customer_payment_allocations`.
- Consecuencia: cobranzas, notas de credito y devoluciones parciales ya no necesitan coincidir estrictamente con deuda abierta, y el sistema mantiene una unica mecanica de aplicacion de creditos tanto para saldar debitos existentes como para anticipar ventas futuras.

## ADR-0027 - Devolucion total por items con estado propio

- Estado: aprobada.
- Decision: cuando una devolucion por `sale_returns` consume la cantidad completa de todos los `sale_items`, la venta pasa a estado `fully_returned` en lugar de reutilizar `reversed`.
- Consecuencia: el sistema preserva la diferencia operativa entre una reversa total compensatoria y una devolucion completa construida item por item, lo que simplifica soporte, auditoria y futura integracion fiscal.

## ADR-0028 - Validacion web de identificadores PostgreSQL como GUID

- Estado: aprobada.
- Decision: los IDs de autenticacion y los `organizationId` de configuracion general y fiscal que provienen de PostgreSQL se validan en Zod 4 con `z.guid()`, sin exigir la variante RFC 4122 impuesta por `z.string().uuid()`.
- Consecuencia: el frontend acepta UUID validos para PostgreSQL, incluidos identificadores semilla sin bits de variante RFC 4122, sin relajar la validacion a una cadena arbitraria; otros contratos mantienen `z.string().uuid()` hasta que exista una necesidad equivalente demostrada.
