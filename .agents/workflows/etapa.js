export const meta = {
  name: 'etapa',
  description: 'Programa una etapa, o varias seguidas, desde su plan ejecutable: contrato, back y front a la vez con un escritor por repo, revisión adversarial según el riesgo, revisores por premisa con su verificador y una sola puerta',
  whenToUse: 'Cuando el usuario pide programar, retomar o cerrar una etapa con plan ejecutable (docs/plans/AAAA-MM-DD-etapa-*.md), o varias seguidas. args: { plan | planes: [rutas], modo?: planificar|programar|cerrar|todo, solo?: [ids], usuarioListo?: true }. Guía: docs/guides/conducir-una-etapa.md',
  phases: [
    { title: 'Leer', detail: 'el plan, lo ya commiteado y lo que bloquea' },
    { title: 'Contrato', detail: 'las tareas K, en el back' },
    { title: 'Programar', detail: 'back y front a la vez, un escritor por repo' },
    { title: 'Integrar', detail: 'el front contra el contrato real' },
    { title: 'Revisión', detail: 'un revisor por premisa, cada uno con su verificador, y un arreglador por repo' },
    { title: 'Puerta', detail: 'suites, E2E, detectores, galería y recorrido, una vez, y después las pantallas contra el tablero' },
    { title: 'Cierre', detail: 'las tablas del plan, lo que queda para después y lo que queda para el usuario' },
  ],
}

// Se corre con Workflow({ scriptPath: '.agents/workflows/etapa.js', args: { plan } }) o { planes: [...] }, sin copiarlo a .claude/.
// La guía es docs/guides/conducir-una-etapa.md y los tests, scripts/arneses/conductor.test.mjs (con agentes falsos).

const A = typeof args === 'string' ? { plan: args } : (Array.isArray(args) ? { planes: args } : (args || {}))
const CADENA = Array.isArray(A.planes) && A.planes.length > 0
const PLANES = (CADENA ? A.planes : (A.plan ? [A.plan] : [])).map((p) => String(p).replace(/\\/g, '/'))
if (!PLANES.length) throw new Error('Falta args.plan: la ruta del plan ejecutable, por ejemplo docs/plans/2026-11-19-etapa-base-de-la-12-segunda-parte.md (o args.planes, con varias rutas en orden)')
if (CADENA && A.plan) throw new Error('Pasá plan o planes, no los dos.')
if (new Set(PLANES).size !== PLANES.length) throw new Error('Un plan está repetido en planes.')
const MODO = A.modo || 'todo'
if (!['planificar', 'programar', 'cerrar', 'todo'].includes(MODO)) throw new Error(`Modo desconocido: ${MODO}. Usá planificar, programar, cerrar o todo.`)
if (CADENA && !['planificar', 'todo'].includes(MODO)) throw new Error(`Con planes, el modo es todo o planificar: ${MODO} no corre la puerta, y sin puerta en verde no se sigue con el plan siguiente.`)
const SOLO = Array.isArray(A.solo) && A.solo.length ? A.solo : null
if (SOLO && CADENA) throw new Error('solo va con un plan: sus ids son de ese plan.')
const USUARIO_LISTO = A.usuarioListo === true
const PREFIJO = PLANES.length > 1
const FRONT = '../ArquitecturaBaseMutitenantFront'
const TOPE_CUPO = 85
const VUELTAS = 3
const PRESUPUESTO = typeof budget === 'undefined' ? null : budget
const MINIMO_DE_PRESUPUESTO = 250000

function claveDe(plan) {
  return plan.split('/').pop().replace(/\.md$/, '').replace(/^\d{4}-\d{2}-\d{2}-etapa-/, '')
}

// ---------- Textos que reciben todos los agentes ----------

function comunes(ctx) {
  return `Reglas de esta corrida, además de AGENTS.md:
- Trabajás en main, en el checkout de siempre: el back es la carpeta actual y el front es ${FRONT}. Sin ramas, worktrees, stash, reset, cambio de rama ni push. Puede haber otras sesiones trabajando: tocá y commiteá solo tus rutas, y no reviertas nada que no hiciste. Los archivos nuevos van con git add antes; el commit, siempre con git commit -m "<tipo>(<área>): <qué>" -m "Tarea: ${ctx.CLAVE}/<id>" -- <rutas>.
- Ninguna corrida contra un proveedor real (Meta, IA, Gmail): fakes y clientes falsos. Nunca leas, copies ni muestres un secreto: de user-secrets, solo los nombres.
- No toques el lienzo (${FRONT}/docs/design/lienzo) ni dibujes pantallas. Si a un tablero le falta un estado, decilo en motivo y seguí con el resto.
- Un test que se pasó de tiempo se corre una vez más, solo. Si así pasa, era la carga de la PC: puede haber otro agente corriendo la suite del otro repo.
- Un proceso se corta por PID, nunca con un filtro amplio. Si levantaste Aspire, aspire stop al terminar.
- Una decisión que cambia el producto (qué ve o puede hacer una persona, una pantalla, el modelo de accesos) no la tomás: devolvé estado bloqueada con la pregunta, un ejemplo real y la opción que recomendás primero. Lo técnico lo decidís vos y lo contás en motivo.`
}

const CUPO = `Antes de tocar nada: si tenés la herramienta mcp__ccd_session_mgmt__get_usage (buscala con ToolSearch), mirá el límite semanal de todos los modelos. Con ${TOPE_CUPO} % o más no empieces: devolvé estado sin-cupo.`

const SIN_AGREGAR = `Lo que no está en la spec no se agrega:
- Hacés solo lo que piden la spec, el contrato y el tablero. Nunca agregás una tabla, una columna, un filtro, un combo, una ventana emergente, una ruta ni un permiso que no estén ahí.
- Si te parece que falta algo, no lo agregues: anotalo en paraDespues (que, porQue y donde: el archivo, la pantalla o la sección de la spec) y seguí con lo tuyo.
- Lo que ya es de otra tarea de este plan no va en paraDespues: esa tarea lo hace. Si le deja algo que tiene que saber, va en cambiosAlContrato o en motivo.
- Solo si la pantalla no funciona sin eso, devolvé estado bloqueada con la pregunta de producto y, si tenés una captura que lo muestra, su ruta en captura.`

const ESCENARIOS = {
  datos: '- Datos: la migración es portable (el SQL en una línea y sin \\r), su Down se niega antes de perder datos y convive con la versión anterior (se agrega antes de borrar; un valor nuevo de un enum, según lo que anote el plan). Probá con filas guardadas antes del cambio. Guardar sin cambios escribe solo la fila de la entidad. En un lote, la fila que falla se registra y las demás siguen. Si hay cifrado: otra organización, una clave que no abre y un contenido ilegible.',
  permisos: '- Permisos: a quien le falta un permiso recibe 403 y no ve nada del recurso, ni en un 409, un aviso o una vista previa. Nadie da lo que no tiene, el último administrador no se pierde, y lo que la pantalla ofrece sigue la condición del servidor.',
  efectos: '- Efectos fuera del sistema: cada respuesta del proveedor se clasifica como definitiva, transitoria o desconocida, y cada una tiene su salida y su test. Lo dudoso no se repite, un reintento no duplica, lo que cuesta se mide antes de pagar y cada tope sale de una sola constante. Todo con fakes, sin ninguna llamada real.',
  aislamiento: '- Aislamiento: un id de otra organización da 404. Los trabajos de fondo entran con su organización (TenantJobRunner o ITenantScope, nunca con una transacción abierta), y la caché y las cuotas llevan su alcance. Probá RLS con dos organizaciones y dos copias de la app a la vez: el lease y lo que corre una sola vez.',
}

// ---------- Premisas de la revisión del cierre ----------
// Cada revisor ve los cambios de la etapa en los dos repos, la spec y solo las reglas y los puntos de su premisa.
// Los puntos son los de la «Lista de control» de .agents/skills/puerta/SKILL.md; van con su nombre por si la lista se renumera.
// Los puntos que no nombra ninguna premisa los cubren la revisión adversarial de cada tarea con riesgo (ESCENARIOS) y la puerta.

const PREMISAS = [
  {
    id: 'textos',
    titulo: 'Textos y errores',
    revisa: 'medium',
    verifica: 'low',
    foco: () => `- Todo lo que ve una persona está en español rioplatense con voseo, y dice «Organización», nunca «tenant».
- Cada texto sale de resources, en es y en: en el back, Application/Resources/*.resx con su .en.resx; en el front, sus archivos de traducción. Ningún texto queda escrito en el código.
- Cada error es ProblemDetails con code Area.Entidad.Motivo y traceId, y sale de <Entidad>Errors.
- El front decide por code, nunca por el texto del mensaje, y cada error en pantalla tiene un solo dueño: no lo muestran a la vez el campo, el cartel y el aviso.`,
    puntos: '1 (números y fechas con la cultura del pedido), 2 (el front no tapa la validación del servidor), 7 (nada exige un idioma fijo) y 14 (cada error en pantalla tiene un solo dueño)',
    reglas: 'docs/rules/textos-y-traducciones.md, result-y-errores.md y validacion.md del back, y docs/rules/textos-y-traducciones.md, errores.md y formatos.md del front',
  },
  {
    id: 'arquitectura',
    titulo: 'Arquitectura, clean code y SOLID',
    revisa: 'high',
    verifica: 'medium',
    foco: () => `- Capas: Domain ← Application ← Infrastructure, y Api compone. El recorrido es Controllers → I<X>Service → <X>Service → I<X>Repository/I<X>Reader/Integrations, y cada helper lleva un sufijo de la lista cerrada de capas-y-flujo.
- Una responsabilidad por clase, por componente y por hook, y nada duplicado: si ya hay una pieza que lo hace, en el back o en shared/ del front, se usa esa.
- Una sola forma de guardar: IUnitOfWork.ExecuteInTransactionAsync, una vez por método público que escribe, con locks, lecturas, reglas y escrituras en ese orden. Las reglas de negocio devuelven Result con <Entidad>Errors.
- Sin handlers, CQRS con ICommand, AutoMapper, Scrutor ni repositorio genérico.
- Tamaño y complejidad: un método o un componente que no se entiende de una leída se parte.`,
    puntos: '3 (cambiar algo en uso), 4 (guardar sin cambios no reescribe nada), 5 (ninguna lectura trae todo para quedarse con poco), 8 (cada exclusión o lock tiene su test con dos actores a la vez) y 11 (cada arreglo busca el mismo defecto en los caminos hermanos)',
    reglas: 'docs/rules/capas-y-flujo.md, guardado.md, result-y-errores.md, persistencia-ef.md, concurrencia.md y paginado-y-busqueda.md del back; docs/rules/estructura-y-features.md y datos-y-api.md del front; y, de docs/guides/revisar-arquitectura.md, los criterios de los bloques 1 a 3 (pedidos, varias copias y volumen) en el horizonte Hoy',
  },
  {
    id: 'cache',
    titulo: 'Caché, cuotas y módulos',
    revisa: 'medium',
    verifica: 'medium',
    foco: () => `- Cada clave de Redis sale de CacheKeys, con su alcance (t:, s:, u:, p:) y su generación. Nunca se invalida a mano: lo que cambia un dato sube su generación.
- Cada ruta declara su cuota con uno de los doce perfiles y su alcance; nunca un limitador por proceso.
- Un módulo (WhatsApp u otro) se enchufa solo por puertos del núcleo y se puede quitar: el núcleo nunca referencia *.Modules.*, y lo opcional pasa por los módulos habilitados.
- En el front, lo de un módulo vive en src/modules/<nombre>/ y el núcleo no lo importa, y lo que cambia un dato invalida todas las consultas que lo muestran: la lista, la ficha, los conteos y la campana.`,
    puntos: '6, solo en la caché y las cuotas con su alcance, y 15 (lo que cambia un dato invalida todo lo que lo muestra)',
    reglas: 'docs/rules/cache.md, cuotas.md, modulos.md y modulos-habilitados.md del back, y docs/rules/datos-y-api.md y estructura-y-features.md del front',
  },
  {
    id: 'seguridad',
    titulo: 'Seguridad y aislamiento',
    revisa: 'high',
    verifica: 'high',
    foco: () => `- Cada tabla nueva tiene su clase (privada con ITenantOwned, pública con IPublishedByBusiness o compartida con IConsumerBusinessShared) y su RLS forzado con su helper. Nadie setea a mano las columnas de tenant ni ignora un filtro fuera de la lista blanca.
- Cada ruta declara [Access(...)] o [PublicSite], o es una anónima de la lista explícita, y lleva [HasPermission], [HasCompanyPermission] o [HasPlatformPermission]. Un permiso nuevo está en el catálogo, en los dos resx y en el seed de los roles de sistema.
- Un id de otra organización responde 404, igual que uno inventado.
- Plataforma entra a una organización solo con ITenantScope.Enter, después de autorizar y registrar el SecurityEvent, y nunca con una transacción abierta. Los trabajos de fondo entran con su organización (TenantJobRunner o ITenantScope).
- El front no decide el acceso: lo que la pantalla ofrece sigue la condición del servidor, y nada nombra un recurso que la persona no puede leer.`,
    puntos: '6 (nada nombra otro recurso sin permiso de leerlo, y nada mezcla organizaciones) y 16 (lo que la pantalla ofrece sigue la misma condición que el servidor)',
    reglas: 'docs/rules/multitenancy.md, permisos.md, api-http.md y datos-personales.md del back; docs/rules/accesos-y-permisos.md del front; y, de docs/guides/revisar-arquitectura.md, los criterios de los bloques 5 (seguridad) y 9 (que no se mezclen las organizaciones)',
  },
  {
    id: 'spec',
    titulo: 'Lo que no está en la spec',
    revisa: 'medium',
    verifica: 'medium',
    corre: true,
    foco: (ctx) => `- Primero corré node scripts/arneses/arnes.mjs conformidad --detectar --plan ${ctx.PLAN} en el back. Cada línea que informe es un hallazgo: repo, archivo y línea según la línea, y en punto «conformidad». Si el arnés todavía no existe o falla, decilo en resumen con su salida en una línea y seguí.
- Después compará lo que ve o usa una persona y la etapa sumó (pantallas, tablas, columnas, filtros, combos, ventanas emergentes, rutas y permisos) contra la spec, el contrato (openapi.json) y sus tableros en ${FRONT}/docs/design/lienzo/. Lo que la etapa agregó y ninguno de los tres pide es un hallazgo; lo que piden y no está, también.
- Mirá además que ningún commit de la etapa haya deshecho lo de otro: lo de cada tarea sigue en HEAD (punto «pisados»).`,
    puntos: 'la fila «Commits que se pisan»',
    reglas: 'la spec que nombra el plan, el contrato y los tableros: lo que no está en ninguno de los tres no se agrega, va a «Para después»',
  },
]

// Este necesita la galería, que solo deja la puerta: corre después de ella, y sus arreglos van con los de la puerta.
const PANTALLAS = {
  id: 'pantallas',
  titulo: 'Pantallas contra el tablero',
  revisa: 'medium',
  verifica: 'medium',
  foco: (ctx, galeria) => `- La puerta acaba de generar la galería: cada pantalla nueva o cambiada de la etapa, al lado de su tablero, en ${galeria}. Abrí cada imagen con Read y compará la captura con su tablero.
- Mirá las piezas compartidas (que se use la de shared/ y no una propia), las ventanas emergentes, los colores (los tokens del tema, nada fijo), los filtros, los botones y avisos, y los estados: cargando, vacío, con error, sin permiso y con la organización suspendida.
- Cada diferencia va con el archivo y la línea del front que la causa: buscalos en el diff de la etapa.`,
  puntos: '10 (botones y avisos) y 13 (cada pantalla se compara con su tablero, con textos ciertos en todos los estados)',
  reglas: 'docs/rules/pantallas-y-ui.md, responsive.md, accesibilidad.md y paginado-y-listados.md del front',
}

// ---------- Esquemas ----------

const TEXTO = { type: 'string' }
const LISTA = { type: 'array', items: { type: 'string' } }
const PARA_DESPUES = {
  type: 'array',
  items: { type: 'object', properties: { que: TEXTO, porQue: TEXTO, donde: TEXTO }, required: ['que', 'porQue', 'donde'] },
}

const S_LEER = {
  type: 'object',
  properties: {
    etapa: TEXTO,
    bloqueos: LISTA,
    cupoSemanal: { type: ['number', 'null'] },
    tareas: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: TEXTO,
          titulo: TEXTO,
          carril: { type: 'string', enum: ['contrato', 'back', 'front', 'integracion'] },
          subcarril: TEXTO,
          riesgo: { type: 'array', items: { type: 'string', enum: ['datos', 'permisos', 'efectos', 'aislamiento'] } },
          espera: LISTA,
          estado: { type: 'string', enum: ['pendiente', 'hecha', 'bloqueada', 'del-usuario'] },
          motivo: TEXTO,
          texto: TEXTO,
        },
        required: ['id', 'titulo', 'carril', 'subcarril', 'riesgo', 'espera', 'estado', 'motivo', 'texto'],
      },
    },
    textos: {
      type: 'object',
      properties: {
        contrato: TEXTO,
        back: { type: 'array', items: { type: 'object', properties: { subcarril: TEXTO, texto: TEXTO }, required: ['subcarril', 'texto'] } },
        front: TEXTO,
        integracion: TEXTO,
      },
      required: ['contrato', 'back', 'front', 'integracion'],
    },
    pasosDelUsuario: {
      type: 'array',
      items: {
        type: 'object',
        properties: { paso: TEXTO, cuando: { type: 'string', enum: ['antes-de-la-puerta', 'despues'] }, soloMirar: { type: 'boolean' } },
        required: ['paso', 'cuando', 'soloMirar'],
      },
    },
    notas: LISTA,
  },
  required: ['etapa', 'bloqueos', 'cupoSemanal', 'tareas', 'textos', 'pasosDelUsuario', 'notas'],
}

const S_PROGRAMA = {
  type: 'object',
  properties: {
    estado: { type: 'string', enum: ['hecha', 'lista', 'parcial', 'bloqueada', 'sin-cupo'] },
    commits: LISTA,
    archivos: LISTA,
    archivosSinCommit: LISTA,
    falta: TEXTO,
    motivo: TEXTO,
    pregunta: TEXTO,
    captura: TEXTO,
    cambiosAlContrato: LISTA,
    paraDespues: PARA_DESPUES,
    verificacion: TEXTO,
  },
  required: ['estado', 'commits', 'archivos', 'archivosSinCommit', 'falta', 'motivo', 'pregunta', 'captura', 'cambiosAlContrato', 'paraDespues', 'verificacion'],
}

const S_HALLAZGOS = {
  type: 'object',
  properties: {
    hallazgos: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          titulo: TEXTO,
          punto: TEXTO,
          archivo: TEXTO,
          linea: { type: 'integer' },
          escenario: TEXTO,
          prueba: TEXTO,
          gravedad: { type: 'string', enum: ['alta', 'media', 'baja'] },
        },
        required: ['titulo', 'punto', 'archivo', 'linea', 'escenario', 'prueba', 'gravedad'],
      },
    },
    resumen: TEXTO,
  },
  required: ['hallazgos', 'resumen'],
}

const S_PREMISA = {
  type: 'object',
  properties: {
    hallazgos: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          titulo: TEXTO,
          punto: TEXTO,
          repo: { type: 'string', enum: ['back', 'front'] },
          archivo: TEXTO,
          linea: { type: 'integer' },
          escenario: TEXTO,
          regla: TEXTO,
          porque: TEXTO,
          gravedad: { type: 'string', enum: ['alta', 'media', 'baja'] },
        },
        required: ['titulo', 'punto', 'repo', 'archivo', 'linea', 'escenario', 'regla', 'porque', 'gravedad'],
      },
    },
    resumen: TEXTO,
  },
  required: ['hallazgos', 'resumen'],
}

const S_VERIFICA = {
  type: 'object',
  properties: {
    veredictos: {
      type: 'array',
      items: { type: 'object', properties: { numero: { type: 'integer' }, confirmado: { type: 'boolean' }, motivo: TEXTO }, required: ['numero', 'confirmado', 'motivo'] },
    },
  },
  required: ['veredictos'],
}

const S_COMMIT = {
  type: 'object',
  properties: {
    estado: { type: 'string', enum: ['hecha', 'bloqueada'] },
    commits: LISTA,
    archivos: LISTA,
    archivosSinCommit: LISTA,
    arreglados: LISTA,
    descartados: { type: 'array', items: { type: 'object', properties: { titulo: TEXTO, porque: TEXTO }, required: ['titulo', 'porque'] } },
    motivo: TEXTO,
    pregunta: TEXTO,
    cambiosAlContrato: LISTA,
    verificacion: TEXTO,
  },
  required: ['estado', 'commits', 'archivos', 'archivosSinCommit', 'arreglados', 'descartados', 'motivo', 'pregunta', 'cambiosAlContrato', 'verificacion'],
}

const S_INTEGRAR = {
  type: 'object',
  properties: {
    estado: { type: 'string', enum: ['hecha', 'bloqueada'] },
    commits: LISTA,
    diferencias: { type: 'array', items: { type: 'object', properties: { que: TEXTO, dueno: { type: 'string', enum: ['back', 'front'] }, arreglada: { type: 'boolean' } }, required: ['que', 'dueno', 'arreglada'] } },
    pasosDelUsuario: LISTA,
    cambiosAlContrato: LISTA,
    paraDespues: PARA_DESPUES,
    archivosSinCommit: LISTA,
    motivo: TEXTO,
    verificacion: TEXTO,
  },
  required: ['estado', 'commits', 'diferencias', 'pasosDelUsuario', 'cambiosAlContrato', 'paraDespues', 'archivosSinCommit', 'motivo', 'verificacion'],
}

const FILA = { type: 'object', properties: { paso: TEXTO, resultado: TEXTO, numero: TEXTO }, required: ['paso', 'resultado', 'numero'] }
const FALLO = {
  type: 'object',
  properties: { paso: TEXTO, archivo: TEXTO, linea: { type: 'integer' }, causa: TEXTO, dueno: { type: 'string', enum: ['back', 'front'] } },
  required: ['paso', 'archivo', 'linea', 'causa', 'dueno'],
}
const S_PUERTA = {
  type: 'object',
  properties: {
    corte: { type: 'object', properties: { back: TEXTO, front: TEXTO }, required: ['back', 'front'] },
    copias: { type: 'object', properties: { back: TEXTO, front: TEXTO }, required: ['back', 'front'] },
    tabla: { type: 'array', items: FILA },
    fallos: { type: 'array', items: FALLO },
    galeria: TEXTO,
    motivo: TEXTO,
  },
  required: ['corte', 'copias', 'tabla', 'fallos', 'galeria', 'motivo'],
}

const S_CIERRE = { type: 'object', properties: { commit: TEXTO, motivo: TEXTO }, required: ['commit', 'motivo'] }

// ---------- Prompts ----------

function pLeer(ctx, anterior) {
  const sigue = anterior
    ? ` Este plan sigue a ${anterior} en la misma corrida del conductor, que solo llega a este si la puerta de aquel pasó en verde: que esa etapa todavía no figure cerrada no es un bloqueo.`
    : ''
  return `Sos el primer paso del conductor que programa una etapa. Solo leés y corrés comandos de lectura (git log, git status, grep, listar procesos): no cambiás nada.

El plan ejecutable es ${ctx.PLAN}. La clave de sus commits es «${ctx.CLAVE}»: cada commit del conductor lleva la línea «Tarea: ${ctx.CLAVE}/<id>». Leé el plan entero y, de la spec que nombra, lo necesario para entender cada tarea y su orden. El front está en ${FRONT}.

Devolvé:
1. etapa: el nombre de la etapa, como lo dice el título del plan.
2. bloqueos: lo que impide empezar a programar hoy, uno por línea, o la lista vacía:
   - el plan dice que va después de otra etapa o de su puerta, y esa no cerró según la hoja de ruta (docs/plans/2026-09-27-plan-de-desarrollo.md) o su propio plan.${sigue}
   - una decisión que el plan deja abierta en «Para decidir» y nadie tomó;
   - la app levantada: un proceso de Aspire (aspire o dcp) o de Vite (node con vite en su línea de comando);
   - cambios sin commitear en los archivos que la etapa va a tocar (git status de los dos repos). Los de otra área no bloquean: van en notas.
3. cupoSemanal: si tenés la herramienta mcp__ccd_session_mgmt__get_usage (buscala con ToolSearch), el porcentaje usado del límite semanal de todos los modelos. Si no la tenés, null.
4. tareas: cada K, B, F e I del plan, en el orden en que el conductor las va a correr.
   - carril: contrato (K), back (B), front (F) o integracion (I). subcarril: el nombre del carril en el plan (por ejemplo «Back A»), o el carril.
   - El conductor tiene un solo escritor por repo, así que las del back van en UN solo orden. Si el plan tiene dos carriles del back, intercalalos respetando cada regla de orden que el plan escribe (por ejemplo, lo que espera el commit de una migración de otra tarea).
   - riesgo: las categorías de lo que el plan marca con (R): datos (migraciones, persistencia o cifrado), permisos, efectos (Meta, correo, archivos o IA) y aislamiento (RLS, ITenantScope, trabajos de fondo, caché y cuotas por alcance). Si el plan no marca ninguna tarea con (R), decidilo vos con esas categorías, por lo que toca cada una.
   - espera: los ids que tienen que estar commiteados antes, de otro carril, o del mismo si esta tarea los usa. El contrato va entero antes del back y del front: no lo pongas.
   - estado: «hecha» solo si en su repo hay commits con «Tarea: ${ctx.CLAVE}/<id>» y lo que pide está en el código (mirá sus archivos principales). «del-usuario» si la hace el usuario (cargar secretos, probar con Meta, aprobar plantillas). «bloqueada» si espera algo de afuera que no está: un tablero que no existe en ${FRONT}/docs/design/lienzo/ (buscalo por su nombre) o una decisión sin tomar. Si no, «pendiente».
   - motivo: por qué tiene ese estado, en una línea (sus commits, si está hecha).
   - texto: lo que el plan dice de la tarea, literal.
5. textos: los textos de chat del plan, literales: contrato, cada carril del back con su nombre, front e integración. Si falta uno, cadena vacía.
6. pasosDelUsuario: lo que el plan deja en manos del usuario (cargar secretos, pruebas con Meta, aprobar plantillas, mirar la galería, la prueba integral). En cada uno, cuando: «antes-de-la-puerta» si la puerta lo necesita, o «despues»; y soloMirar: true si es solo mirar lo que deja la corrida (la galería, la prueba integral), false si el usuario tiene que hacer o cargar algo (secretos, Meta, plantillas).
7. notas: lo que conviene que sepa quien lanza el conductor, en pocas líneas.`
}

function repoDe(t) {
  return t.carril === 'front' ? FRONT : 'la carpeta actual (el back)'
}

function dondeDe(repo) {
  return repo === 'back' ? 'el back (la carpeta actual)' : `el front (${FRONT})`
}

function textoDelCarril(t, T) {
  if (t.carril === 'contrato') return T.contrato
  if (t.carril === 'front') return T.front
  const propio = T.back.find((b) => b.subcarril === t.subcarril)
  return (propio || T.back[0] || { texto: '' }).texto
}

function pProgramar(ctx, t, revisa, falta, vuelta) {
  const cambios = ctx.CAMBIOS.length ? `\nCambios al contrato que hicieron otras tareas de esta corrida:\n- ${ctx.CAMBIOS.join('\n- ')}\n` : ''
  const sigue = falta
    ? `\nOtro agente empezó esta tarea y paró a mitad (vas por la vuelta ${vuelta + 1}). Lo que dejó dicho: ${falta}. Mirá git status y git log --grep "Tarea: ${ctx.CLAVE}/${t.id}" en su repo para ver dónde quedó, y seguí desde ahí.\n`
    : ''
  const cierre = revisa
    ? `Esta tarea lleva revisión adversarial (${t.riesgo.join(', ')}): NO commitees. Dejá todo en el árbol, en verde, y devolvé estado «lista» con la lista completa de archivos que tocaste o creaste.`
    : `Commiteá cada parte coherente y verificada con la línea «Tarea: ${ctx.CLAVE}/${t.id}», y devolvé estado «hecha» con sus commits.`
  return `Sos el agente que programa la tarea ${t.id} de la etapa «${ctx.ETAPA}» (plan: ${ctx.PLAN}).
${CUPO}

Este es el texto con que se abre el chat de tu carril (${t.subcarril || t.carril}). De ahí sale qué leer y qué reglas valen:
"""
${textoDelCarril(t, ctx.TEXTOS)}
"""

De ese texto hacés SOLO la tarea ${t.id}: ${t.titulo}. Lo que dice el plan de ella:
"""
${t.texto}
"""
Sus pasos, archivos y tests están en la spec que nombra el plan. Ya están commiteadas: ${ctx.hechas()}. Lo que el texto dice de avisarle a la Guía o de esperarla lo resuelve el conductor: no esperes a nadie, no avises y no hagas otra tarea.
${cambios}${sigue}
Cómo trabajás:
- TDD: el test dirigido en rojo, después en verde. Al terminar, el build y los tests rápidos enteros de tu repo, sin Docker y sin filtrar por módulo. Si la tarea tocó rutas, persistencia, autenticación o aislamiento, también los tests de integración y de seguridad de esa área.
- ${cierre}
- Si llevás unos 150 pasos y no terminaste, pará en un punto coherente${revisa ? ', en verde y sin commitear,' : ' y commiteá lo que está en verde,'} y devolvé estado «parcial»: en falta va lo que queda, concreto, para el agente que sigue.
- Si hace falta un cambio de contrato y estás en el back, hacelo (openapi.json regenerado) y contalo en cambiosAlContrato, una línea por cambio. Si estás en el front, no toques el back: seguí con lo que puedas, contá el cambio en cambiosAlContrato y lo hace el integrador.
- En archivosSinCommit va lo que dejás tocado y sin commitear al terminar; si commiteaste todo, la lista vacía. En verificacion va lo que corriste y su resultado, en una línea.

${SIN_AGREGAR}

${comunes(ctx)}`
}

function pAdversarial(ctx, t, archivos) {
  return `Sos un revisor adversarial. Otro agente programó la tarea ${t.id} de la etapa «${ctx.ETAPA}» (plan ${ctx.PLAN}) y dejó el código sin commitear. No sabés qué concluyó, y no importa: tu trabajo es romperla.

La tarea: ${t.titulo}.
"""
${t.texto}
"""
Sus pasos y tests están en la spec que nombra el plan. Los archivos que tocó, en ${repoDe(t)}, son: ${archivos.join(', ')}. Leelos con git diff y git status de ese repo.

Riesgo: ${t.riesgo.join(', ')}. Probá al menos estos escenarios, y los que se te ocurran:
${t.riesgo.map((c) => ESCENARIOS[c]).join('\n')}

Por cada problema real, escribí un test que falle, dejalo en el árbol sin commitear y nombralo en «prueba». No arregles el código de producción ni commitees. Borrá los tests que escribiste y pasan. En «punto» va la categoría del riesgo. Si no encontrás nada, devolvé la lista vacía: no inventes.

${comunes(ctx)}`
}

function lineaDeHallazgo(h) {
  const extra = `${h.prueba ? ` Prueba: ${h.prueba}.` : ''}${h.regla ? ` Regla: ${h.regla}.` : ''}${h.confirmacion ? ` Lo confirmó un verificador: ${h.confirmacion}.` : ''}`
  return `- [${h.gravedad}] ${h.titulo} (${h.archivo}:${h.linea}). Escenario: ${h.escenario}.${extra}`
}

function pCorregir(ctx, t, archivos, hallazgos) {
  const cuerpo = hallazgos.length
    ? `Hallazgos de la revisión adversarial:
${hallazgos.map(lineaDeHallazgo).join('\n')}

Con cada uno, corré su prueba. Si es real, arreglalo, dejá el test y buscá el mismo defecto en los caminos hermanos y en las piezas compartidas. Si no es real, borrá su test y contá por qué en descartados. Después corré el build y los tests rápidos enteros del repo, sin Docker y sin filtrar. Si la tarea tocó rutas, persistencia, autenticación o aislamiento, corré también los de integración y seguridad del área.`
    : 'No hay hallazgos que corregir: quien la programó ya corrió las suites. Confirmá con git status que los archivos de la tarea están ahí, sin cambios ajenos mezclados, y no vuelvas a correrlas.'
  return `Sos el agente que commitea la tarea ${t.id} de la etapa «${ctx.ETAPA}» (plan ${ctx.PLAN}). No la programaste ni la revisaste.

La tarea: ${t.titulo}. Sus archivos, en ${repoDe(t)}: ${archivos.join(', ')}.

${cuerpo}

Commiteá todo lo de la tarea, los archivos de arriba más los tests que quedaron, con la línea «Tarea: ${ctx.CLAVE}/${t.id}», y devolvé estado hecha con los commits. Si no queda en verde, no commitees: devolvé bloqueada, con el motivo y archivosSinCommit.

${comunes(ctx)}`
}

function pIntegrar(ctx) {
  return `Sos el integrador de la etapa «${ctx.ETAPA}» (plan ${ctx.PLAN}). El contrato, el back y el front ya están commiteados.

El texto con que el plan abre el chat de Integración:
"""
${ctx.TEXTOS.integracion}
"""

Las tareas de integración del plan:
${ctx.INTEGRACION.map((t) => `- ${t.id} · ${t.titulo}: ${t.texto}`).join('\n')}

De todo eso hacés lo que no es correr la puerta ni lo que hace el usuario:
- El front consume el contrato real (npm run contracts y npm run contracts:check en ${FRONT}), y lo que no encaja se corrige.
- Dejás listo lo que la puerta corre después: los pasos nuevos del recorrido de usuario (scripts/recorrido-usuario.mjs), los casos de la galería de lo que cambió (scripts/capturas/etapa-<etapa>.json) y los E2E reales que pida el plan, si las tareas del front no los dejaron.
- Cambios al contrato que anotaron las tareas: ${ctx.CAMBIOS.length ? ctx.CAMBIOS.join('; ') : 'ninguno'}.
- Cada diferencia real se arregla en el repo de su dueño, con su test. Corré solo lo que el arreglo toca, y commiteá con la línea «Tarea: ${ctx.CLAVE}/integracion».
- La puerta no la corrés vos: la corre el conductor después.
- Los pasos que el plan le deja al usuario en la integración (por ejemplo, cargar sus user-secrets) van en pasosDelUsuario, comando por comando y sin ningún valor: los valores los pega él.

${SIN_AGREGAR}

${comunes(ctx)}`
}

function pRevisarPremisa(ctx, p, galeria) {
  return `Sos el revisor de la premisa «${p.titulo}» en el cierre de la etapa «${ctx.ETAPA}» (plan ${ctx.PLAN}). Solo leés: no cambiás nada, no compilás ni corrés tests${p.corre ? ', salvo el arnés que se nombra abajo' : ''}.

Lo que revisás son los cambios de la etapa en los dos repos: los commits con la línea «Tarea: ${ctx.CLAVE}/». Listalos con git log --grep "Tarea: ${ctx.CLAVE}/" --format="%H %s" en el back (la carpeta actual) y en el front (${FRONT}), y leé sus diffs. De la spec que nombra el plan, leé lo que haga falta para saber qué pidió cada tarea.

Tu premisa, y solo ella:
${p.foco(ctx, galeria)}

De la «Lista de control» de .agents/skills/puerta/SKILL.md, mirás ${p.puntos}. Las reglas son ${p.reglas}. Lo demás lo miran otros revisores: no lo nombres.

Devolvé solo problemas reales del código de la etapa. En repo, back o front; archivo y línea; en punto, el número de la lista o la regla; en escenario, qué hace alguien y qué pasa, en una línea; en regla, la que se rompe; y en porque, la evidencia que te convenció. Una lista vacía es un resultado válido: no inventes.`
}

function pVerificar(ctx, p, hallazgos, galeria) {
  const afirmaciones = hallazgos.map((h, i) => `${i + 1}. [${h.repo}] ${h.archivo}:${h.linea}: ${h.titulo}. Escenario: ${h.escenario}.${h.regla ? ` Regla: ${h.regla}.` : ''}`).join('\n')
  return `Sos el verificador de la premisa «${p.titulo}» en el cierre de la etapa «${ctx.ETAPA}» (plan ${ctx.PLAN}). Otro agente revisó los cambios de la etapa y afirma lo de abajo. No sabés cómo llegó a cada afirmación: tu trabajo es intentar refutarla contra el código de HEAD${galeria ? ` y las capturas lado a lado de ${galeria}` : ''}. El back es la carpeta actual y el front, ${FRONT}. Solo leés: no cambiás nada.

${afirmaciones}

Por cada una devolvé su número, confirmado y el motivo en una línea. Confirmado es true solo si lo ves: el archivo y la línea existen, el escenario pasa o la regla se rompe, y el código es de la etapa (sus commits llevan la línea «Tarea: ${ctx.CLAVE}/»). Si ya estaba antes de la etapa, si otra pieza lo resuelve o si dudás, false con lo que viste.`
}

function pCorregirCierre(ctx, repo, hallazgos) {
  return `Sos el agente que corrige lo que encontró la revisión del cierre de la etapa «${ctx.ETAPA}» (plan ${ctx.PLAN}) en ${dondeDe(repo)}. No programaste la etapa ni la revisaste.

Hallazgos, cada uno confirmado por un verificador contra el código:
${hallazgos.map(lineaDeHallazgo).join('\n')}

Con cada uno, arreglalo con un test que falle sin el arreglo, y buscá el mismo defecto en los caminos hermanos. Si al arreglarlo ves que no era real, contá por qué en descartados. Corré lo que tocan los arreglos: el test, los de su área y, si cambiaste autenticación, persistencia base o la composición de la app, los tests rápidos enteros del repo. Commiteá con la línea «Tarea: ${ctx.CLAVE}/revision».

${comunes(ctx)}`
}

function pPuerta(ctx, revision) {
  return `Corré la puerta de la etapa «${ctx.ETAPA}» (plan ${ctx.PLAN}) según la skill .agents/skills/puerta/SKILL.md, sobre copias del corte (el HEAD de main de cada repo), sin worktrees ni ramas.
- La lista de control, que es la revisión de código del cierre, la hacen revisores por premisa. Los de la fase Revisión ya terminaron: ${revision}. Las pantallas contra el tablero las mira otro revisor cuando termines, sobre tu galería. No la repitas: en su fila poné ese resultado.
- La galería la miran ese revisor y el usuario. Generala y devolvé en galeria la carpeta con las capturas lado a lado.
- No arregles nada ni borres las copias: devolvé sus rutas, porque los arreglos se traen a esas mismas copias. Al terminar, aspire stop.
- Ninguna corrida contra un proveedor real. Si un paso lo necesita, no lo corras y decilo en su fila.
- Cada fallo va con archivo, línea, la causa en una línea y su dueño (back o front). Antes de contarlo, descartá el entorno, como dice la skill.

${comunes(ctx)}`
}

function pArreglarPuerta(ctx, repo, fallos, hallazgos) {
  const deLaPuerta = fallos.length ? `Fallos de la puerta:\n${fallos.map((f) => `- ${f.paso}: ${f.archivo}:${f.linea}. ${f.causa}`).join('\n')}\n` : ''
  const dePantallas = hallazgos.length ? `${deLaPuerta ? '\n' : ''}Diferencias con el tablero que vio el revisor de pantallas en la galería, confirmadas por un verificador:\n${hallazgos.map(lineaDeHallazgo).join('\n')}\n` : ''
  return `Sos el dueño de lo que encontró la puerta de la etapa «${ctx.ETAPA}» (plan ${ctx.PLAN}) en ${dondeDe(repo)}. No programaste la etapa.

${deLaPuerta}${dePantallas}
Arreglá cada uno en el checkout de siempre, no en la copia de la puerta, con su test, y buscá el mismo defecto en los caminos hermanos. Corré solo lo que toca el arreglo. Si un fallo era del entorno (un tiempo vencido que pasa al correrlo solo), contalo en descartados. Commiteá con la línea «Tarea: ${ctx.CLAVE}/puerta».

${comunes(ctx)}`
}

function pRepetir(ctx, P, arreglos) {
  const commits = arreglos.flatMap((a) => (a && a.commits) || [])
  return `Los arreglos de la puerta de la etapa «${ctx.ETAPA}» (plan ${ctx.PLAN}) ya están commiteados en main: ${commits.join(', ') || 'ninguno'}.

Traelos a las copias de la puerta (back: ${P.copias.back}; front: ${P.copias.front}) con git -C <copia> fetch y checkout --detach del commit nuevo. Corré solo los pasos que esos arreglos tocan, según la skill .agents/skills/puerta/SKILL.md: si un arreglo cambió una pantalla, también sus casos de la galería, y nada más de ella. La suite completa de un repo se repite solo si un arreglo tocó autenticación, persistencia base o la composición de la app.

Devolvé la tabla entera. Las filas que no corriste quedan como estaban:
${JSON.stringify(P.tabla)}
Al terminar, aspire stop y borrá las dos copias.

${comunes(ctx)}`
}

function lineaParaDespues(p) {
  return `- **${p.que}.** ${p.porQue} Dónde: ${p.donde} (${p.tarea}).`
}

function pCierre(ctx, resumen, puerta, copias, paraDespues) {
  const backlog = paraDespues.length
    ? `
- En docs/backlog.md, la sección «## Para después de la etapa ${ctx.CLAVE}», al final si todavía no está, con una línea por cosa que no esté ya, así:
${paraDespues.map(lineaParaDespues).join('\n')}
  Antes de tocarlo, mirá git diff --quiet -- docs/backlog.md: si tiene cambios sin commitear de otra sesión, no lo toques y decilo en motivo.`
    : ''
  return `Sos el que cierra la corrida del conductor en el plan ${ctx.PLAN}. Cambiás solo ese archivo${paraDespues.length ? ' y docs/backlog.md' : ''}, y no agregás historial ni actas.
- En la tabla de estado del principio (Parte | Tareas | Estado), cada parte dice «Hecha» si todas sus tareas están hechas; si no, «Pendiente: <ids y motivo corto>».
- ${puerta ? `En la tabla de la puerta, el resultado de cada paso: ${JSON.stringify(puerta)}.` : 'La tabla de la puerta no se toca: la puerta no corrió.'}
- En «Cambios al contrato», una línea por cambio de esta corrida que todavía no esté: ${ctx.CAMBIOS.length ? ctx.CAMBIOS.join('; ') : 'ninguno'}.${backlog}
Commiteá solo eso, con git commit -m "docs(planes): ${ctx.ETAPA}, el estado después del conductor" -m "Tarea: ${ctx.CLAVE}/cierre" -- ${ctx.PLAN}${paraDespues.length ? ', más docs/backlog.md si lo tocaste' : ''}. Si nada cambió, no commitees y devolvé commit vacío.
${copias ? `Después, si quedan las copias de la puerta (${copias}), corré aspire stop si algo quedó levantado y borralas.` : ''}

El estado de cada tarea:
${resumen.map((r) => `- ${r.id}: ${r.estado}${r.motivo ? `, ${r.motivo}` : ''}`).join('\n')}

${comunes(ctx)}`
}

// ---------- Orden y esperas ----------

function esperasDe(t, porId) {
  return (t.espera || []).filter((id) => porId[id] && porId[id].carril !== 'integracion' && id !== t.id)
}

/**
 * Un ciclo entre las esperas, el orden de cada carril y el contrato (que va entero antes del back y del front)
 * dejaría a un carril esperando para siempre.
 */
function buscarCiclo(contrato, carriles, porId) {
  const dep = {}
  for (const lista of [contrato].concat(carriles)) {
    lista.forEach((t, i) => {
      dep[t.id] = esperasDe(t, porId).slice()
      if (i > 0) dep[t.id].push(lista[i - 1].id)
      if (lista !== contrato) for (const k of contrato) dep[t.id].push(k.id)
    })
  }
  const color = {}
  function visitar(id, camino) {
    if (color[id] === 1) return camino.slice(camino.indexOf(id)).concat(id)
    if (color[id] === 2 || !dep[id]) return null
    color[id] = 1
    for (const d of dep[id]) {
      const c = visitar(d, camino.concat(id))
      if (c) return c
    }
    color[id] = 2
    return null
  }
  for (const id of Object.keys(dep)) {
    const c = visitar(id, [])
    if (c) return c
  }
  return null
}

function agentesEstimados(pendientes) {
  // Integrar 1; Revisión: 5 revisores, hasta 5 verificadores y hasta 2 arregladores; Puerta: 1, el revisor de pantallas,
  // su verificador, hasta 2 arregladores y lo que tocan los arreglos; Cierre 1.
  const deCierre = MODO === 'programar' ? 2 : 1 + (PREMISAS.length * 2 + 2) + (1 + 2 + 2 + 1) + 1
  return 1 + pendientes.reduce((n, t) => n + (t.riesgo.length ? 3 : 1), 0) + deCierre
}

// ---------- Revisión por premisa ----------

/** Pasa los hallazgos de un revisor por un verificador nuevo; solo lo que este confirma llega a un arreglo. */
async function verificar(ctx, p, rev, fase, galeria) {
  const base = { id: p.id, titulo: p.titulo }
  if (!rev) return Object.assign(base, { hallazgos: null, confirmados: null, descartados: [], resumen: 'la revisión no terminó' })
  const hallazgos = rev.hallazgos || []
  if (!hallazgos.length) return Object.assign(base, { hallazgos: 0, confirmados: [], descartados: [], resumen: rev.resumen })
  const v = await agent(pVerificar(ctx, p, hallazgos, galeria), { label: ctx.et(`verifica · ${p.titulo}`), phase: fase, schema: S_VERIFICA, effort: p.verifica })
  if (!v) {
    return Object.assign(base, {
      hallazgos: hallazgos.length,
      confirmados: null,
      descartados: hallazgos.map((h) => ({ titulo: h.titulo, motivo: 'la verificación no terminó' })),
      resumen: 'la verificación no terminó: sus hallazgos no llegan a un arreglo',
    })
  }
  const confirmados = []
  const descartados = []
  hallazgos.forEach((h, i) => {
    const veredicto = (v.veredictos || []).find((x) => x.numero === i + 1)
    if (veredicto && veredicto.confirmado === true) confirmados.push(Object.assign({}, h, { confirmacion: veredicto.motivo }))
    else descartados.push({ titulo: h.titulo, motivo: veredicto ? veredicto.motivo : 'el verificador no lo contestó' })
  })
  return Object.assign(base, { hallazgos: hallazgos.length, confirmados, descartados, resumen: rev.resumen })
}

function resumenDePremisa(r) {
  if (r.hallazgos === null) return `${r.titulo}: la revisión no terminó`
  if (r.confirmados === null) return `${r.titulo}: ${r.hallazgos} hallazgos sin verificar`
  return `${r.titulo}: ${r.hallazgos} hallazgos, ${r.confirmados.length} confirmados`
}

function paraDevolver(r) {
  return { id: r.id, titulo: r.titulo, hallazgos: r.hallazgos, confirmados: r.confirmados ? r.confirmados.length : null, descartados: r.descartados, resumen: r.resumen }
}

function porRepo(lista, campo) {
  const g = {}
  for (const x of lista) if (x && (x[campo] === 'back' || x[campo] === 'front')) (g[x[campo]] = g[x[campo]] || []).push(x)
  return g
}

// ---------- Un plan ----------

async function correrPlan(PLAN, anterior, usuarioListo) {
  const CLAVE = claveDe(PLAN)
  const ctx = {
    PLAN,
    CLAVE,
    ETAPA: '',
    TEXTOS: null,
    INTEGRACION: [],
    CAMBIOS: [],
    corrieron: 0,
    et: (s) => (PREFIJO ? `${CLAVE} · ${s}` : s),
    hechas: () => 'ninguna',
  }

  phase('Leer')
  const L = await agent(pLeer(ctx, anterior), { label: ctx.et('leer el plan'), phase: 'Leer', schema: S_LEER })
  if (!L) throw new Error(`El agente que lee el plan ${PLAN} no terminó`)

  const tareas = L.tareas
  const porId = Object.fromEntries(tareas.map((t) => [t.id, t]))
  const contrato = tareas.filter((t) => t.carril === 'contrato')
  const back = tareas.filter((t) => t.carril === 'back')
  const front = tareas.filter((t) => t.carril === 'front')

  const bloqueos = L.bloqueos.slice()
  if (typeof L.cupoSemanal === 'number' && L.cupoSemanal >= TOPE_CUPO) bloqueos.push(`El cupo semanal va en ${L.cupoSemanal} %: con ${TOPE_CUPO} % no se empieza a programar`)
  const ciclo = buscarCiclo(contrato, [back, front], porId)
  if (ciclo) bloqueos.push(`Las esperas del plan forman un ciclo: ${ciclo.join(' → ')}`)

  const pendientes = tareas.filter((t) => t.carril !== 'integracion' && t.estado === 'pendiente' && (!SOLO || SOLO.includes(t.id)))
  const pasosAntes = L.pasosDelUsuario.filter((p) => p.cuando === 'antes-de-la-puerta').map((p) => p.paso)

  if (MODO === 'planificar') {
    return {
      etapa: L.etapa,
      clave: CLAVE,
      bloqueos,
      cupoSemanal: L.cupoSemanal,
      carriles: { contrato: contrato.map((t) => t.id), back: back.map((t) => t.id), front: front.map((t) => t.id) },
      tareas: tareas.map((t) => ({ id: t.id, carril: t.carril, estado: t.estado, riesgo: t.riesgo, espera: t.espera, motivo: t.motivo })),
      pasosDelUsuario: L.pasosDelUsuario,
      agentesEstimados: agentesEstimados(pendientes),
      notas: L.notas,
    }
  }
  if (bloqueos.length) return { etapa: L.etapa, clave: CLAVE, resultado: 'no empezó', bloqueos, notas: L.notas }

  // ---------- Estado compartido ----------

  const R = {}
  const listo = {}
  for (const t of tareas) {
    let resolver
    const p = new Promise((res) => { resolver = res })
    listo[t.id] = { p, resolver }
  }
  function fin(t, res) {
    if (R[t.id]) return R[t.id]
    R[t.id] = Object.assign({ id: t.id, titulo: t.titulo, carril: t.carril, commits: [], motivo: '' }, res)
    listo[t.id].resolver(R[t.id])
    return R[t.id]
  }
  for (const t of tareas) if (t.carril !== 'integracion' && t.estado !== 'pendiente') fin(t, { estado: t.estado, motivo: t.motivo })

  ctx.ETAPA = L.etapa
  ctx.TEXTOS = L.textos
  ctx.INTEGRACION = tareas.filter((t) => t.carril === 'integracion')
  ctx.hechas = () => {
    const h = Object.values(R).filter((r) => r.estado === 'hecha').map((r) => (r.commits[0] ? `${r.id} (${r.commits[0]})` : r.id))
    return h.length ? h.join(', ') : 'ninguna'
  }
  const PREGUNTAS = []
  const PASOS = L.pasosDelUsuario.map((p) => p.paso)
  const PASOS_DE_INTEGRACION = []
  const PARA_DESPUES_DEL_PLAN = []
  let detenido = null

  function anotarCambio(c, quien) {
    const linea = `${c} (${quien})`
    if (!ctx.CAMBIOS.includes(linea)) ctx.CAMBIOS.push(linea)
  }

  function anotarParaDespues(lista, quien) {
    for (const p of lista || []) {
      if (!PARA_DESPUES_DEL_PLAN.some((x) => x.que === p.que && x.donde === p.donde)) PARA_DESPUES_DEL_PLAN.push({ tarea: quien, que: p.que, porQue: p.porQue, donde: p.donde })
    }
  }

  function juntar(quien, r) {
    for (const c of r.cambiosAlContrato || []) anotarCambio(c, quien)
    anotarParaDespues(r.paraDespues, quien)
    if (r.pregunta && !PREGUNTAS.some((p) => p.tarea === quien && p.pregunta === r.pregunta)) {
      PREGUNTAS.push(Object.assign({ tarea: quien, pregunta: r.pregunta }, r.captura ? { captura: r.captura } : {}))
    }
  }

  async function programarTarea(t, fase) {
    const revisa = t.riesgo.length > 0
    let r = null
    let falta = ''
    let vuelta = 0
    while (true) {
      r = await agent(pProgramar(ctx, t, revisa, falta, vuelta), { label: ctx.et(`${t.id} · programa${vuelta ? ` (${vuelta + 1})` : ''}`), phase: fase, schema: S_PROGRAMA })
      if (!r) return { estado: 'bloqueada', motivo: 'el agente que programaba no terminó', archivosSinCommit: ['(revisar git status)'] }
      juntar(t.id, r)
      vuelta += 1
      if (r.estado !== 'parcial' || vuelta >= VUELTAS) break
      falta = r.falta
    }
    if (r.estado === 'parcial') return Object.assign({}, r, { motivo: `quedó a medias después de ${VUELTAS} agentes: ${r.falta}` })
    if (r.estado === 'sin-cupo' || r.estado === 'bloqueada') return r
    if (!revisa && r.estado === 'hecha') return r

    // Una tarea con riesgo pasa por la revisión adversarial; una sin riesgo que quedó «lista» sin commitear, solo por el commit.
    const archivos = r.archivos.length ? r.archivos : r.archivosSinCommit
    let hallazgos = []
    if (revisa) {
      let rev = await agent(pAdversarial(ctx, t, archivos), { label: ctx.et(`${t.id} · revisión adversarial`), phase: fase, schema: S_HALLAZGOS })
      if (!rev) rev = await agent(pAdversarial(ctx, t, archivos), { label: ctx.et(`${t.id} · revisión adversarial (2)`), phase: fase, schema: S_HALLAZGOS })
      if (!rev) return { estado: 'bloqueada', motivo: 'la revisión adversarial no terminó: el código quedó sin commitear', archivosSinCommit: archivos }
      hallazgos = rev.hallazgos || []
    }
    const opciones = { label: ctx.et(`${t.id} · ${hallazgos.length ? `corrige ${hallazgos.length} y commitea` : 'commitea'}`), phase: fase, schema: S_COMMIT }
    if (!hallazgos.length) opciones.effort = 'low'
    const c = await agent(pCorregir(ctx, t, archivos, hallazgos), opciones)
    if (!c) return { estado: 'bloqueada', motivo: 'el agente que cerraba la tarea no terminó', archivosSinCommit: archivos }
    juntar(t.id, c)
    return Object.assign({}, c, { commits: (r.commits || []).concat(c.commits), hallazgos: hallazgos.length, descartados: c.descartados.length })
  }

  async function correrCarril(lista, fase) {
    let sucio = false
    try {
      for (const t of lista) {
        if (R[t.id]) continue
        const previas = await Promise.all(esperasDe(t, porId).map((id) => listo[id].p))
        const mala = previas.find((p) => p.estado !== 'hecha')
        if (mala) { fin(t, { estado: 'sin-correr', motivo: `espera ${mala.id}, que quedó ${mala.estado}` }); continue }
        if (sucio) { fin(t, { estado: 'sin-correr', motivo: 'una tarea anterior de este repo dejó cambios sin commitear' }); continue }
        if (detenido) { fin(t, { estado: 'sin-correr', motivo: detenido }); continue }
        if (SOLO && !SOLO.includes(t.id)) { fin(t, { estado: 'sin-correr', motivo: 'fuera de esta corrida' }); continue }
        log(ctx.et(`${t.id} · ${t.titulo}${t.riesgo.length ? ` (R: ${t.riesgo.join(', ')})` : ''}`))
        ctx.corrieron += 1
        const res = fin(t, await programarTarea(t, fase))
        log(ctx.et(`${t.id}: ${res.estado}${res.motivo ? ` · ${res.motivo}` : ''}`))
        if (res.estado === 'sin-cupo') detenido = `el cupo semanal llegó al ${TOPE_CUPO} %`
        if (res.estado !== 'hecha' && (res.archivosSinCommit || []).length) sucio = true
        if (PRESUPUESTO && PRESUPUESTO.total && PRESUPUESTO.remaining() < MINIMO_DE_PRESUPUESTO) detenido = 'se acabó el presupuesto de la corrida'
      }
    } catch (e) {
      for (const t of lista) fin(t, { estado: 'bloqueada', motivo: `el conductor falló en este carril: ${e && e.message ? e.message : e}` })
    }
  }

  function faltan() {
    return [...contrato, ...back, ...front].filter((t) => !R[t.id] || !['hecha', 'del-usuario'].includes(R[t.id].estado))
  }

  // ---------- Contrato y programar ----------

  const programa = MODO === 'programar' || MODO === 'todo'
  const cierra = MODO === 'cerrar' || MODO === 'todo'
  let integracion = null

  if (programa) {
    phase('Contrato')
    await correrCarril(contrato, 'Contrato')
    const kMal = contrato.map((k) => R[k.id]).find((r) => r && !['hecha', 'del-usuario'].includes(r.estado))
    if (kMal) for (const t of back.concat(front)) fin(t, { estado: 'sin-correr', motivo: `espera el contrato: ${kMal.id} quedó ${kMal.estado}` })

    phase('Programar')
    await parallel([() => correrCarril(back, 'Programar'), () => correrCarril(front, 'Programar')])

    if (!faltan().length && !detenido) {
      phase('Integrar')
      integracion = await agent(pIntegrar(ctx), { label: ctx.et('integrar'), phase: 'Integrar', schema: S_INTEGRAR })
      if (integracion) {
        for (const c of integracion.cambiosAlContrato) anotarCambio(c, 'integración')
        anotarParaDespues(integracion.paraDespues, 'integracion')
        for (const p of integracion.pasosDelUsuario) {
          if (!PASOS.includes(p)) PASOS.push(p)
          if (!PASOS_DE_INTEGRACION.includes(p)) PASOS_DE_INTEGRACION.push(p)
        }
      }
    }
  }

  // ---------- Revisión por premisa y puerta ----------

  let revision = null
  let puerta = null
  let copias = ''
  const integro = !programa || (integracion && integracion.estado === 'hecha')
  const puedeCerrar = cierra && !detenido && !faltan().length && integro
  const esperaAlUsuario = pasosAntes.length > 0 && !usuarioListo

  if (puedeCerrar && esperaAlUsuario) log(ctx.et(`La puerta espera al usuario: ${pasosAntes.join('; ')}. Relanzá con modo cerrar y usuarioListo: true.`))

  if (puedeCerrar && !esperaAlUsuario) {
    phase('Revisión')
    // Cada premisa se verifica apenas termina su revisión; los arreglos esperan a todas, porque hay un solo escritor por repo.
    const revisadas = await pipeline(
      PREMISAS,
      (p) => agent(pRevisarPremisa(ctx, p), { label: ctx.et(`revisión · ${p.titulo}`), phase: 'Revisión', schema: S_PREMISA, effort: p.revisa }),
      (rev, p) => verificar(ctx, p, rev, 'Revisión'),
    )
    const premisas = PREMISAS.map((p, i) => revisadas[i] || { id: p.id, titulo: p.titulo, hallazgos: null, confirmados: null, descartados: [], resumen: 'la revisión no terminó' })
    const aArreglar = porRepo(premisas.flatMap((r) => r.confirmados || []), 'repo')
    const repos = Object.keys(aArreglar)
    const arreglosDeRevision = await parallel(repos.map((repo) => () =>
      agent(pCorregirCierre(ctx, repo, aArreglar[repo]), { label: ctx.et(`arreglos de la revisión · ${repo}`), phase: 'Revisión', schema: S_COMMIT })))
    const arreglos = repos.map((repo, i) => {
      const c = arreglosDeRevision[i]
      if (c) juntar('revision', c)
      return c ? { repo, fase: 'revision', estado: c.estado, arreglados: c.arreglados, descartados: c.descartados, commits: c.commits } : { repo, fase: 'revision', estado: 'no terminó', arreglados: [], descartados: [], commits: [] }
    })
    const resumenRevision = premisas.map(resumenDePremisa).concat(arreglos.map((a) => `arreglos del ${a.repo}: ${a.arreglados.length} (${a.estado})`)).join('; ')
    revision = { premisas: premisas.map(paraDevolver), arreglos }

    phase('Puerta')
    puerta = await agent(pPuerta(ctx, resumenRevision), { label: ctx.et('puerta'), phase: 'Puerta', schema: S_PUERTA })
    if (puerta) {
      copias = `${puerta.copias.back} y ${puerta.copias.front}`
      // Las pantallas contra el tablero necesitan la galería: corren cuando la puerta ya la dejó.
      let pantallas
      if (puerta.galeria) {
        const revP = await agent(pRevisarPremisa(ctx, PANTALLAS, puerta.galeria), { label: ctx.et(`revisión · ${PANTALLAS.titulo}`), phase: 'Puerta', schema: S_PREMISA, effort: PANTALLAS.revisa })
        pantallas = await verificar(ctx, PANTALLAS, revP, 'Puerta', puerta.galeria)
      } else {
        pantallas = { id: PANTALLAS.id, titulo: PANTALLAS.titulo, hallazgos: 0, confirmados: [], descartados: [], resumen: 'la puerta no dejó galería' }
      }
      revision.premisas.push(paraDevolver(pantallas))
      puerta.tabla = puerta.tabla.map((f) => (/lista de control/i.test(f.paso) ? Object.assign({}, f, { resultado: `${f.resultado}; ${resumenDePremisa(pantallas)}` }) : f))

      const fallosPorDueno = porRepo(puerta.fallos, 'dueno')
      const pantallasPorRepo = porRepo(pantallas.confirmados || [], 'repo')
      const duenos = ['back', 'front'].filter((repo) => fallosPorDueno[repo] || pantallasPorRepo[repo])
      if (duenos.length) {
        const arreglosDePuerta = await parallel(duenos.map((repo) => () =>
          agent(pArreglarPuerta(ctx, repo, fallosPorDueno[repo] || [], pantallasPorRepo[repo] || []), { label: ctx.et(`arreglos de la puerta · ${repo}`), phase: 'Puerta', schema: S_COMMIT })))
        duenos.forEach((repo, i) => {
          const c = arreglosDePuerta[i]
          if (c) juntar('puerta', c)
          if (pantallasPorRepo[repo]) revision.arreglos.push(c ? { repo, fase: 'puerta', estado: c.estado, arreglados: c.arreglados, descartados: c.descartados, commits: c.commits } : { repo, fase: 'puerta', estado: 'no terminó', arreglados: [], descartados: [], commits: [] })
        })
        const otra = await agent(pRepetir(ctx, puerta, arreglosDePuerta), { label: ctx.et('puerta · lo que tocan los arreglos'), phase: 'Puerta', schema: S_PUERTA })
        if (otra) { puerta = Object.assign({}, otra, { corte: puerta.corte, copias: puerta.copias, galeria: otra.galeria || puerta.galeria }); copias = '' }
      }
    }
  }

  // ---------- Cierre ----------

  const resumen = tareas.filter((t) => t.carril !== 'integracion').map((t) => {
    const r = R[t.id] || { estado: t.estado, motivo: t.motivo, commits: [] }
    return { id: t.id, estado: r.estado, motivo: r.motivo, commits: r.commits || [], verificacion: r.verificacion || '' }
  })

  let cierre = null
  if (ctx.corrieron || integracion || puerta) {
    phase('Cierre')
    cierre = await agent(pCierre(ctx, resumen, puerta ? puerta.tabla : null, copias, PARA_DESPUES_DEL_PLAN), { label: ctx.et('cierre del plan'), phase: 'Cierre', schema: S_CIERRE, effort: 'low' })
  }

  const queFalta = faltan().map((t) => `${t.id}: ${R[t.id] ? `${R[t.id].estado}, ${R[t.id].motivo}` : 'sin correr'}`)
  let resultado = 'puerta corrida'
  if (!puerta) resultado = queFalta.length ? 'faltan tareas' : (esperaAlUsuario && cierra ? 'espera al usuario antes de la puerta' : 'programada, sin puerta')
  if (puerta && puerta.fallos.length) resultado = 'puerta con fallos'

  // Lo que el usuario tiene que hacer (no solo mirar) antes de que corra el plan siguiente.
  const pendientesDelUsuario = L.pasosDelUsuario.filter((p) => p.cuando === 'despues' && p.soloMirar !== true).map((p) => p.paso)
    .concat(PASOS_DE_INTEGRACION)
    .concat(tareas.filter((t) => R[t.id] && R[t.id].estado === 'del-usuario').map((t) => `${t.id}: ${t.titulo}`))

  return {
    etapa: L.etapa,
    clave: CLAVE,
    modo: MODO,
    resultado,
    tareas: resumen,
    faltan: queFalta,
    detenido,
    preguntas: PREGUNTAS,
    pasosDelUsuario: PASOS,
    pasosAntesDeLaPuerta: pasosAntes,
    pendientesDelUsuario,
    paraDespues: PARA_DESPUES_DEL_PLAN,
    cambiosAlContrato: ctx.CAMBIOS,
    integracion: integracion ? { estado: integracion.estado, diferencias: integracion.diferencias, motivo: integracion.motivo } : null,
    revision,
    puerta: puerta ? { tabla: puerta.tabla, fallos: puerta.fallos, galeria: puerta.galeria, corte: puerta.corte } : null,
    cierre,
    tokensDeSalida: PRESUPUESTO ? PRESUPUESTO.spent() : null,
  }
}

// ---------- Varios planes seguidos ----------

/** El plan siguiente corre solo si este cerró con la puerta en verde y sin nada pendiente para el usuario. */
function porQueFrena(r) {
  const motivos = []
  if (r.resultado === 'no empezó') motivos.push(`no empezó: ${(r.bloqueos || []).join('; ')}`)
  else if (r.resultado !== 'puerta corrida') motivos.push(`la puerta no pasó en verde: ${r.resultado}`)
  if ((r.preguntas || []).length) motivos.push(`preguntas abiertas para el usuario: ${r.preguntas.length}`)
  if ((r.pendientesDelUsuario || []).length) motivos.push(`el usuario tiene que: ${r.pendientesDelUsuario.join('; ')}`)
  if (r.revision) {
    for (const p of r.revision.premisas) if (p.hallazgos === null || p.confirmados === null) motivos.push(`${p.titulo}: ${p.resumen}`)
    for (const a of r.revision.arreglos) if (a.estado !== 'hecha') motivos.push(`los arreglos del ${a.repo} (${a.fase}) quedaron ${a.estado}`)
  }
  return motivos
}

if (!CADENA) return await correrPlan(PLANES[0], null, USUARIO_LISTO)

const corridos = []
let frenoEn = null
for (let i = 0; i < PLANES.length; i++) {
  let r
  try {
    r = await correrPlan(PLANES[i], i ? PLANES[i - 1] : null, i === 0 && USUARIO_LISTO)
  } catch (e) {
    // Lo de los planes anteriores no se pierde: este cuenta como que no empezó, con el motivo.
    r = { clave: claveDe(PLANES[i]), resultado: 'no empezó', bloqueos: [e && e.message ? e.message : String(e)] }
  }
  corridos.push(r)
  if (MODO === 'planificar') continue
  const motivos = porQueFrena(r)
  if (!motivos.length && i < PLANES.length - 1 && PRESUPUESTO && PRESUPUESTO.total && PRESUPUESTO.remaining() < MINIMO_DE_PRESUPUESTO) motivos.push('se acabó el presupuesto de la corrida')
  if (motivos.length) {
    frenoEn = { plan: PLANES[i], clave: r.clave, porque: motivos }
    log(`Frena en ${r.clave}: ${motivos.join(' · ')}`)
    break
  }
  if (i < PLANES.length - 1) log(`${r.clave} cerró con la puerta en verde: sigue ${claveDe(PLANES[i + 1])}`)
}

let resultadoDeLaCadena = 'todos con la puerta en verde'
if (MODO === 'planificar') resultadoDeLaCadena = 'planificados'
else if (frenoEn) resultadoDeLaCadena = 'frenó'

const cadena = { modo: MODO, resultado: resultadoDeLaCadena, planes: corridos, frenoEn, sinCorrer: PLANES.slice(corridos.length) }
if (MODO === 'planificar') cadena.agentesEstimados = corridos.reduce((n, r) => n + (r.agentesEstimados || 0), 0)
return cadena
