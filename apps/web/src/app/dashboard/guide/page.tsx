import {
  ArrowRight,
  BadgeDollarSign,
  BanknoteArrowDown,
  BookOpenText,
  Boxes,
  Building2,
  Check,
  CircleAlert,
  CircleDollarSign,
  ClipboardCheck,
  PackagePlus,
  ReceiptText,
  RotateCcw,
  Settings,
  ShieldCheck,
  ShoppingCart,
  Tags,
  Users,
} from "lucide-react";
import Link from "next/link";

const quickStart = [
  {
    title: "Prepará el comercio",
    description: "Completá los datos generales y creá los usuarios con sus roles.",
    href: "/dashboard/settings",
    icon: Building2,
  },
  {
    title: "Armá el catálogo",
    description: "Creá categoría, marca, producto y precio antes de cargar existencias.",
    href: "/dashboard/catalog",
    icon: PackagePlus,
  },
  {
    title: "Cargá stock",
    description: "Registrá el ingreso inicial o un conteo físico en el depósito.",
    href: "/dashboard/stock",
    icon: Boxes,
  },
  {
    title: "Abrí la caja",
    description: "Iniciá la sesión del turno para habilitar cobros y ventas.",
    href: "/dashboard/cash",
    icon: CircleDollarSign,
  },
];

const flows = [
  {
    id: "venta",
    eyebrow: "Flujo diario",
    title: "Realizar una venta",
    description: "El circuito principal conecta caja, stock, pagos y cuenta corriente.",
    href: "/dashboard/pos",
    linkLabel: "Ir a Nueva venta",
    icon: ShoppingCart,
    requirements: ["Caja abierta", "Producto activo, con precio y stock", "Cliente creado si usás cuenta corriente"],
    steps: [
      "Entrá en Nueva venta y buscá por nombre, código, SKU o código de barras.",
      "Agregá los productos y ajustá las cantidades del carrito.",
      "Elegí uno o más medios de pago: efectivo, transferencia, QR, tarjeta o cuenta corriente.",
      "Si corresponde, seleccioná el cliente y completá las referencias del cobro.",
      "Revisá que la suma de pagos coincida con el total y confirmá la venta.",
    ],
    result: "Se genera el ticket interno, se descuenta stock y se registran en forma consistente los pagos, la caja y la cuenta del cliente.",
  },
  {
    id: "caja",
    eyebrow: "Inicio y cierre del turno",
    title: "Operar la caja",
    description: "Abrí, controlá y cerrá la caja sin perder trazabilidad.",
    href: "/dashboard/cash",
    linkLabel: "Ir a Caja",
    icon: CircleDollarSign,
    requirements: ["Rol con permiso de caja", "Una única sesión abierta por caja"],
    steps: [
      "Al comenzar el turno, indicá el saldo inicial y abrí la caja.",
      "Registrá ingresos o egresos manuales solo cuando no provengan de una venta o cobranza.",
      "Consultá el libro diario y compará el saldo teórico durante el turno.",
      "Al finalizar, ingresá el efectivo contado y cerrá la sesión con una observación si hay diferencia.",
    ],
    result: "La sesión queda cerrada con saldo teórico, monto contado, diferencia y movimientos históricos disponibles para consulta.",
  },
  {
    id: "catalogo",
    eyebrow: "Preparación comercial",
    title: "Crear un producto listo para vender",
    description: "Seguí el orden de carga para evitar productos incompletos.",
    href: "/dashboard/catalog",
    linkLabel: "Ir a Catálogo",
    icon: Tags,
    requirements: ["Categoría y marca activas", "Lista de precios disponible", "Unidad de medida definida"],
    steps: [
      "Creá o elegí una categoría y una marca desde la carga guiada.",
      "Completá nombre, SKU o código interno, código de barras y unidad de medida.",
      "Elegí la lista de precios y definí costo y margen, o cargá el precio manual.",
      "Guardá el producto y verificá que figure activo en el catálogo.",
      "Entrá en Stock para registrar su existencia inicial y política de reposición.",
    ],
    result: "El producto queda disponible para búsquedas y, cuando tiene stock, puede incorporarse a una venta.",
  },
  {
    id: "stock",
    eyebrow: "Existencias",
    title: "Controlar y corregir stock",
    description: "Cada ajuste crea un movimiento inmutable y auditable.",
    href: "/dashboard/stock",
    linkLabel: "Ir a Stock",
    icon: Boxes,
    requirements: ["Producto existente", "Depósito seleccionado", "Motivo claro para todo ajuste"],
    steps: [
      "Buscá el producto y abrí su detalle para consultar saldo, alertas y movimientos.",
      "Definí stock mínimo, punto de reposición y existencia máxima si necesitás alertas.",
      "Usá Registrar movimiento para ingresos, egresos, ajustes o devoluciones manuales.",
      "Para una toma general, registrá el conteo físico: el sistema calcula y documenta la diferencia.",
    ],
    result: "El saldo se actualiza conservando el historial. Las ventas y devoluciones también generan sus propios movimientos automáticamente.",
  },
  {
    id: "clientes",
    eyebrow: "Cuenta corriente",
    title: "Gestionar clientes, deuda y cobranzas",
    description: "Centralizá ventas a cuenta, vencimientos, créditos y pagos.",
    href: "/dashboard/customers",
    linkLabel: "Ir a Clientes",
    icon: Users,
    requirements: ["Cliente activo", "Caja abierta para cobranzas", "Límite y condiciones de crédito revisados"],
    steps: [
      "Creá el cliente con sus datos fiscales y comerciales.",
      "Seleccioná el registro para revisar saldo, vencido, límite, disponible y movimientos.",
      "Registrá una venta a cuenta desde Clientes o usá ese medio de pago en el POS.",
      "Para cobrar, elegí el medio de pago, ingresá el importe y confirmá la cobranza.",
      "Usá una nota de crédito solo para acreditar saldo sin ingreso de dinero; requiere administración o gerencia.",
    ],
    result: "La cuenta corriente y, cuando corresponde, la caja se actualizan juntas. Un pago excedente puede quedar como saldo a favor.",
  },
  {
    id: "devoluciones",
    eyebrow: "Correcciones",
    title: "Devolver o revertir una venta",
    description: "Corregí operaciones confirmadas mediante movimientos compensatorios.",
    href: "/dashboard/pos",
    linkLabel: "Buscar el ticket",
    icon: RotateCcw,
    requirements: ["Ticket localizado", "Cantidad todavía disponible para devolver", "Caja abierta si existe reintegro"],
    steps: [
      "Buscá el ticket por número o cliente y abrí su detalle.",
      "Para una devolución, indicá las cantidades de cada producto y el motivo.",
      "Confirmá el reintegro: vuelve el stock y se compensa caja o cuenta corriente según el pago original.",
      "Usá Revertir venta únicamente para anular la operación completa cuando la opción esté disponible.",
    ],
    result: "La venta original no se borra: queda con estado parcial, devuelta o revertida, junto con todos los contramovimientos.",
  },
  {
    id: "fiscal",
    eyebrow: "Preparación fiscal",
    title: "Preparar comprobantes para ARCA",
    description: "Configurá y auditá la cola fiscal con el alcance actual del piloto.",
    href: "/dashboard/fiscal",
    linkLabel: "Ir a Configuración fiscal",
    icon: ReceiptText,
    requirements: ["Rol administrador o gerente para editar", "Datos fiscales y certificado configurados fuera del código"],
    steps: [
      "Completá CUIT emisor, entorno, punto de venta, servicio y tipo de comprobante predeterminado.",
      "Ejecutá la prueba técnica para validar certificado y conectividad con FEDummy.",
      "Seleccioná una venta confirmada y prepará su documento en la cola.",
      "Revisá el estado, los intentos y el último error antes de cualquier reintento.",
    ],
    result: "Importante: el módulo actual prepara y audita documentos, pero todavía no autoriza CAE. No tomes el ticket interno como comprobante fiscal.",
    warning: true,
  },
];

const roleNotes = [
  { title: "Administrador", text: "Configura el comercio, usuarios, catálogo, operación y módulo fiscal." },
  { title: "Gerencia", text: "Opera los módulos principales y puede gestionar notas de crédito y configuración fiscal." },
  { title: "Ventas / Caja", text: "Accede a las tareas del mostrador según los permisos asignados." },
  { title: "Solo lectura", text: "Puede consultar los módulos habilitados, sin confirmar cambios." },
];

export default function GuidePage() {
  return (
    <div className="space-y-10 pb-10">
      <header className="relative overflow-hidden rounded-2xl border bg-card px-6 py-8 sm:px-9 sm:py-10">
        <div className="absolute -right-16 -top-20 size-64 rounded-full bg-accent/70 blur-3xl" aria-hidden="true" />
        <div className="relative max-w-3xl">
          <div className="mb-5 flex size-11 items-center justify-center rounded-xl border border-primary/20 bg-accent text-primary">
            <BookOpenText className="size-5" aria-hidden="true" />
          </div>
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-primary">Centro de ayuda</p>
          <h1 className="mt-2 text-3xl font-semibold tracking-tight sm:text-4xl">Guía de uso de AgroControl</h1>
          <p className="mt-4 max-w-2xl text-sm leading-6 text-muted-foreground sm:text-base">
            Recorré los circuitos de la aplicación en el orden recomendado. Cada operación enlaza directamente
            con el módulo correspondiente y explica qué necesitás antes de empezar.
          </p>
        </div>
      </header>

      <nav className="rounded-xl border bg-card p-4" aria-label="Índice de la guía">
        <p className="mb-3 text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">Ir a un flujo</p>
        <div className="flex flex-wrap gap-2">
          {flows.map((flow) => (
            <a
              key={flow.id}
              href={`#${flow.id}`}
              className="rounded-full border bg-background px-3 py-1.5 text-sm font-medium transition-colors hover:border-primary/40 hover:text-primary"
            >
              {flow.title}
            </a>
          ))}
        </div>
      </nav>

      <section aria-labelledby="primeros-pasos">
        <div className="mb-5 flex items-end justify-between gap-4">
          <div>
            <p className="text-sm font-medium text-primary">Primera puesta en marcha</p>
            <h2 id="primeros-pasos" className="mt-1 text-2xl font-semibold tracking-tight">Antes de la primera venta</h2>
          </div>
          <span className="hidden text-sm text-muted-foreground sm:block">Seguí el orden 1 → 4</span>
        </div>
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          {quickStart.map((item, index) => {
            const Icon = item.icon;
            return (
              <Link
                key={item.href}
                href={item.href}
                className="group flex min-h-48 flex-col justify-between rounded-xl border bg-card p-5 transition-all hover:-translate-y-0.5 hover:border-primary/40"
              >
                <div className="flex items-start justify-between">
                  <span className="font-mono text-xs font-semibold text-muted-foreground">0{index + 1}</span>
                  <Icon className="size-5 text-primary" aria-hidden="true" />
                </div>
                <div>
                  <h3 className="font-semibold">{item.title}</h3>
                  <p className="mt-2 text-sm leading-5 text-muted-foreground">{item.description}</p>
                  <span className="mt-4 inline-flex items-center gap-1 text-sm font-medium text-primary">
                    Abrir módulo <ArrowRight className="size-3.5 transition-transform group-hover:translate-x-1" aria-hidden="true" />
                  </span>
                </div>
              </Link>
            );
          })}
        </div>
      </section>

      <section aria-labelledby="flujos-operativos" className="space-y-5">
        <div>
          <p className="text-sm font-medium text-primary">Trabajo cotidiano</p>
          <h2 id="flujos-operativos" className="mt-1 text-2xl font-semibold tracking-tight">Flujos operativos</h2>
        </div>

        {flows.map((flow, flowIndex) => {
          const Icon = flow.icon;
          return (
            <article id={flow.id} key={flow.id} className="scroll-mt-6 overflow-hidden rounded-2xl border bg-card">
              <div className="grid lg:grid-cols-[0.72fr_1.28fr]">
                <div className="border-b bg-muted/35 p-6 lg:border-b-0 lg:border-r lg:p-8">
                  <div className="flex items-center justify-between">
                    <span className="font-mono text-xs font-semibold text-muted-foreground">
                      FLUJO {String(flowIndex + 1).padStart(2, "0")}
                    </span>
                    <span className="grid size-10 place-items-center rounded-xl border bg-background text-primary">
                      <Icon className="size-4.5" aria-hidden="true" />
                    </span>
                  </div>
                  <p className="mt-8 text-xs font-semibold uppercase tracking-[0.16em] text-primary">{flow.eyebrow}</p>
                  <h3 className="mt-2 text-xl font-semibold tracking-tight">{flow.title}</h3>
                  <p className="mt-3 text-sm leading-6 text-muted-foreground">{flow.description}</p>

                  <div className="mt-7">
                    <p className="text-xs font-semibold uppercase tracking-[0.14em] text-muted-foreground">Antes de empezar</p>
                    <ul className="mt-3 space-y-2">
                      {flow.requirements.map((requirement) => (
                        <li key={requirement} className="flex gap-2 text-sm leading-5">
                          <Check className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" />
                          {requirement}
                        </li>
                      ))}
                    </ul>
                  </div>
                </div>

                <div className="p-6 lg:p-8">
                  <ol className="space-y-5">
                    {flow.steps.map((step, stepIndex) => (
                      <li key={step} className="grid grid-cols-[2rem_1fr] gap-3">
                        <span className="grid size-8 place-items-center rounded-full border bg-background font-mono text-xs font-semibold text-primary">
                          {stepIndex + 1}
                        </span>
                        <p className="pt-1 text-sm leading-6">{step}</p>
                      </li>
                    ))}
                  </ol>

                  <div className={`mt-7 rounded-xl border p-4 ${flow.warning ? "border-amber-500/30 bg-amber-500/5" : "border-primary/20 bg-accent/35"}`}>
                    <div className="flex gap-3">
                      {flow.warning ? (
                        <CircleAlert className="mt-0.5 size-4 shrink-0 text-amber-700" aria-hidden="true" />
                      ) : (
                        <ClipboardCheck className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" />
                      )}
                      <div>
                        <p className="text-sm font-semibold">{flow.warning ? "Alcance actual" : "Resultado"}</p>
                        <p className="mt-1 text-sm leading-5 text-muted-foreground">{flow.result}</p>
                      </div>
                    </div>
                  </div>

                  <Link href={flow.href} className="mt-6 inline-flex items-center gap-2 text-sm font-semibold text-primary hover:underline">
                    {flow.linkLabel} <ArrowRight className="size-4" aria-hidden="true" />
                  </Link>
                </div>
              </div>
            </article>
          );
        })}
      </section>

      <section className="grid gap-4 lg:grid-cols-[1.2fr_0.8fr]" aria-labelledby="permisos">
        <div className="rounded-2xl border bg-card p-6 sm:p-8">
          <div className="flex items-center gap-3">
            <ShieldCheck className="size-5 text-primary" aria-hidden="true" />
            <h2 id="permisos" className="text-xl font-semibold tracking-tight">Roles y permisos</h2>
          </div>
          <p className="mt-3 text-sm leading-6 text-muted-foreground">
            La interfaz puede mostrarse en modo consulta o bloquear acciones según el rol. Si una opción no aparece,
            pedile a un administrador que revise tus roles; nunca compartas credenciales.
          </p>
          <div className="mt-6 grid gap-3 sm:grid-cols-2">
            {roleNotes.map((role) => (
              <div key={role.title} className="rounded-xl border bg-background p-4">
                <h3 className="text-sm font-semibold">{role.title}</h3>
                <p className="mt-1 text-sm leading-5 text-muted-foreground">{role.text}</p>
              </div>
            ))}
          </div>
          <Link href="/dashboard/users" className="mt-5 inline-flex items-center gap-2 text-sm font-semibold text-primary hover:underline">
            Gestionar usuarios <ArrowRight className="size-4" aria-hidden="true" />
          </Link>
        </div>

        <aside className="rounded-2xl border border-primary/20 bg-primary p-6 text-primary-foreground sm:p-8">
          <Settings className="size-5" aria-hidden="true" />
          <h2 className="mt-5 text-xl font-semibold tracking-tight">Reglas para operar con seguridad</h2>
          <ul className="mt-5 space-y-4 text-sm leading-6 text-primary-foreground/85">
            <li className="flex gap-3"><BanknoteArrowDown className="mt-1 size-4 shrink-0" aria-hidden="true" />No registres manualmente en caja un cobro que ya ingresó por una venta o cobranza.</li>
            <li className="flex gap-3"><BadgeDollarSign className="mt-1 size-4 shrink-0" aria-hidden="true" />Revisá importes, medios de pago y cliente antes de confirmar.</li>
            <li className="flex gap-3"><RotateCcw className="mt-1 size-4 shrink-0" aria-hidden="true" />Corregí operaciones con devolución o reversa; no intentes borrar su historial.</li>
            <li className="flex gap-3"><CircleAlert className="mt-1 size-4 shrink-0" aria-hidden="true" />Ante un error, conservá el mensaje y el número de ticket para facilitar el seguimiento.</li>
          </ul>
        </aside>
      </section>
    </div>
  );
}
