export const meta = {
  name: 'etapa',
  description: 'Programa una etapa, o varias juntas, desde su plan ejecutable, en cinco fases: 1) programar: el contrato y después los carriles del back (cada uno en su copia) y el front a la vez, cada tarea con su test dirigido, el build y su commit; 2) revisar en paralelo: un revisor por cada ficha de docs/rules/ que tocan los archivos de la etapa, los transversales de arquitectura y de lo que no está en la spec, uno adversarial por cada tema de riesgo tocado y los tests enteros con los detectores de documentación; 3) arreglar lo encontrado, sin repetidos, un agente por repo; 4) una última revisión del diff entero; 5) el cierre con el código listo: el plan retirado, su sección de verificación al backlog y el commit. La verificación (contrato, E2E, galería y recorrido) va aparte, con verificar-etapa.js. Con varias etapas, la 1 corre para cada una y de la 2 a la 5, una sola vez',
  whenToUse: 'Cuando el usuario pide programar, retomar o cerrar una etapa con plan ejecutable (docs/plans/AAAA-MM-DD-etapa-*.md), o varias juntas. args: { plan | planes: [rutas], modo?: todo|programar|cerrar|planificar, solo?: [ids], usuarioListo?: true, carrilesBack?: 2, modelos?: { front, back, revisa }, topeCupo?: 85 }. Guía: docs/guides/conducir-una-etapa.md',
  phases: [
    { title: 'Programar', detail: 'leer el plan, el contrato y después los carriles del back (cada uno en su copia) y el front, a la vez' },
    { title: 'Revisión', detail: 'en paralelo: un revisor por ficha tocada, los dos transversales, uno adversarial por tema de riesgo y los tests enteros con los detectores' },
    { title: 'Arreglos', detail: 'lo encontrado, sin repetidos: un agente por repo comprueba cada hallazgo y arregla lo real' },
    { title: 'Última revisión', detail: 'el diff entero de la etapa con los arreglos: lo chico lo arregla, lo demás va al backlog' },
    { title: 'Cierre', detail: 'código listo: la spec cerrada, el plan retirado, la hoja de ruta, la verificación al backlog y el commit' },
  ],
}

// Se corre con Workflow({ scriptPath: '.agents/workflows/etapa.js', args: { plan } }) o { planes: [...] }, sin copiarlo a .claude/.
// La guía es docs/guides/conducir-una-etapa.md y los tests, scripts/arneses/conductor.test.mjs (con agentes falsos).

const A = typeof args === 'string' ? { plan: args } : (Array.isArray(args) ? { planes: args } : (args || {}))
const CADENA = Array.isArray(A.planes) && A.planes.length > 0
const PLANES = (CADENA ? A.planes : (A.plan ? [A.plan] : [])).map((p) => String(p).replace(/\\/g, '/'))
if (!PLANES.length) throw new Error('Falta args.plan: la ruta del plan ejecutable, por ejemplo docs/plans/2026-11-08-etapa-12-catalogos.md (o args.planes, con varias rutas en orden)')
if (CADENA && A.plan) throw new Error('Pasá plan o planes, no los dos.')
if (new Set(PLANES).size !== PLANES.length) throw new Error('Un plan está repetido en planes.')
const MODO = A.modo || 'todo'
if (!['planificar', 'programar', 'cerrar', 'todo'].includes(MODO)) throw new Error(`Modo desconocido: ${MODO}. Usá planificar, programar, cerrar o todo.`)
// Con planes, la fase 1 corre para cada uno y de la 2 a la 5, una vez, juntas (pedido del usuario del 2026-11-27):
// cualquier modo sirve, porque el plan siguiente no espera el cierre del anterior.
const SOLO = Array.isArray(A.solo) && A.solo.length ? A.solo : null
if (SOLO && CADENA) throw new Error('solo va con un plan: sus ids son de ese plan.')
// El usuario ya hizo lo que necesita la revisión (por ejemplo, cargar secretos). Con planes vale para todos: se revisan juntos.
const USUARIO_LISTO = A.usuarioListo === true
// Cuántos carriles del back corren a la vez, cada uno en su copia de .local/copias/ (ADR 0071). Con 1, el back va en main, como antes.
const CARRILES_BACK = A.carrilesBack === undefined ? 2 : A.carrilesBack
if (!Number.isInteger(CARRILES_BACK) || CARRILES_BACK < 1 || CARRILES_BACK > 4) throw new Error('carrilesBack va de 1 a 4.')
// El modelo de cada rol, si no es el de la sesión: { front, back, revisa } (revisa: los revisores de la fase 2).
const MODELOS = A.modelos && typeof A.modelos === 'object' ? A.modelos : {}
const PREFIJO = PLANES.length > 1
const FRONT = '../ArquitecturaBaseMutitenantFront'
// Con este porcentaje del cupo semanal no se empieza nada nuevo (AGENTS.md, «Sesiones y cupo»). Solo el usuario lo sube.
const TOPE_CUPO = A.topeCupo === undefined ? 85 : A.topeCupo
if (!Number.isInteger(TOPE_CUPO) || TOPE_CUPO < 1 || TOPE_CUPO > 100) throw new Error('topeCupo va de 1 a 100.')
// Cada paso de un agente relee todo lo que lleva: uno corto gasta mucho menos que uno largo con el mismo trabajo.
const PASOS_POR_AGENTE = 80
const VUELTAS = 6
const PRESUPUESTO = typeof budget === 'undefined' ? null : budget
// El primer hito de una etapa; el segundo, «verificada», lo da verificar-etapa.js (pedido del usuario del 2026-11-27).
const CODIGO_LISTO = 'código listo'
const MINIMO_DE_PRESUPUESTO = 250000

function claveDe(plan) {
  return plan.split('/').pop().replace(/\.md$/, '').replace(/^\d{4}-\d{2}-\d{2}-etapa-/, '')
}

// ---------- Textos que reciben todos los agentes ----------

function dondeTrabaja(carril) {
  if (!carril || !carril.copia) {
    return `- Trabajás en main, en el checkout de siempre: el back es la carpeta actual y el front es ${FRONT}. Sin ramas, worktrees, stash, reset, cambio de rama ni push. Puede haber otras sesiones trabajando: tocá y commiteá solo tus rutas, y no reviertas nada que no hiciste.`
  }
  return `- Trabajás en la copia ${carril.copia} del back: un clon de main con su propio build, para que los carriles del back no se pisen. Hacé cd a la copia y todo ahí; Read y Edit van con rutas absolutas dentro de ella (pwd -W te da su raíz). No toques archivos del back fuera de la copia, ni el front. Tus commits quedan en la copia hasta que node scripts/arneses/carril.mjs entregar ${carril.copia} los pone encima de main: si dice que chocó o que no pasa la verificación con lo que entró de main, resolvelo en la copia y volvé a entregar. Los commits que devolvés son los que imprime entregar. Sin ramas, worktrees, stash, reset, cambio de rama ni push.`
}

const LEER_POCO = `- Leé por partes: ubicá con grep -n y leé con Read (offset y limit) solo lo que necesitás, no archivos enteros, y no releas lo que ya leíste. De un diff, primero git diff --stat y después solo los archivos que importan. Lo largo de una consola va a un archivo en .local/ y se lee por partes.`

function comunes(ctx, carril) {
  const varias = ctx.CLAVES && ctx.CLAVES.length > 1 ? ` Con varias etapas, <clave> es la de la etapa del cambio: ${ctx.CLAVES.join(', ')}.` : ''
  return `Reglas de esta corrida, además de AGENTS.md:
${dondeTrabaja(carril)} Los archivos nuevos van con git add antes; el commit, siempre con git commit -m "<tipo>(<área>): <qué>" -m "Tarea: ${ctx.CLAVE}/<id>" -- <rutas>.${varias}
${LEER_POCO}
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

// ---------- Los revisores de la fase 2 ----------
// Pedido del usuario del 2026-11-27: un revisor chico y enfocado por cada ficha de docs/rules/ (del back y del front)
// que tocan los archivos que cambió la etapa; dos transversales («Arquitectura, clean code y SOLID» y «Lo que no está
// en la spec»); uno adversarial por cada tema de riesgo que tocan esos archivos; y los tests enteros. Todos a la vez.
// Las pantallas contra el tablero, el E2E y el recorrido van en la verificación de la etapa (verificar-etapa.js), el
// segundo hito, que corre sobre una copia del corte mientras se programa la siguiente.

/** Un patrón de rutas (** cualquier carpeta, * dentro de una, ? un carácter) como expresión regular, sin mayúsculas. */
function patronARegex(patron) {
  let re = ''
  for (let i = 0; i < patron.length; i++) {
    const c = patron[i]
    if (c === '*' && patron[i + 1] === '*') {
      i += 1
      if (patron[i + 1] === '/') { i += 1; re += '(?:.*/)?' } else re += '.*'
    } else if (c === '*') re += '[^/]*'
    else if (c === '?') re += '[^/]'
    else re += c.replace(/[.+^${}()|[\]\\]/g, '\\$&')
  }
  return new RegExp(`^${re}$`, 'i')
}

function coincide(ruta, patrones) {
  return patrones.some((p) => patronARegex(p).test(ruta))
}

// Lo que ninguna ficha revisa: documentos, lo generado y, en el front, los tests (salvo la ficha de tests).
const FUERA = {
  back: ['**/*.md', '**/*.Designer.cs', '**/*ModelSnapshot.cs'],
  front: ['**/*.md', 'src/generated/**', 'package-lock.json'],
}
const PRUEBAS_DEL_FRONT = ['**/*.test.ts', '**/*.test.tsx', 'src/test/**']

/**
 * Cada ficha de docs/rules/ de los dos repos, con las rutas que cubre (desde la raíz de su repo). `controla` es el test
 * que la controla entera: esa ficha no lleva revisor. Ante la duda, null: lleva revisor.
 */
const REGLAS = [
  { repo: 'back', ficha: 'api-http', patrones: ['src/**/Controllers/**', 'src/**/Contracts/**', 'docs/contracts/openapi.json'], controla: null },
  { repo: 'back', ficha: 'auditoria', patrones: ['src/*.Domain/**', 'src/**/Persistence/Configurations/**', 'src/**/Persistence/Interceptors/**', 'src/**/Auditing/**'], controla: null },
  { repo: 'back', ficha: 'cache', patrones: ['src/**/Caching/**', 'src/**/*Cache*'], controla: null },
  { repo: 'back', ficha: 'capas-y-flujo', patrones: ['src/**/*.cs'], controla: 'LayerDependencyTests, ProjectReferencesTests, ControllerServiceRepositoryTests, ApplicationServicesTests (la lista cerrada de sufijos), ServiceDependencyCountTests y CodeMetricsConfig; el diseño lo mira el revisor de Arquitectura' },
  { repo: 'back', ficha: 'concurrencia', patrones: ['src/*.Domain/**', 'src/**/Contracts/**', 'src/**/BackgroundJobs/**', 'src/**/*Worker*', 'src/**/*Lease*', 'src/**/*Lock*'], controla: null },
  { repo: 'back', ficha: 'cuotas', patrones: ['src/**/RateLimiting/**', 'src/**/Controllers/**', 'src/**/*Quota*'], controla: null },
  { repo: 'back', ficha: 'datos-de-referencia', patrones: ['src/**/ReferenceData/**', 'src/**/Contracts/**', 'src/**/Models/**'], controla: null },
  { repo: 'back', ficha: 'datos-personales', patrones: ['src/**/Identity/**', 'src/**/Users/**', 'src/**/Legal/**', 'src/**/Invitations/**', 'src/**/*Account*', 'src/**/*Retention*', 'src/**/*Purge*'], controla: null },
  { repo: 'back', ficha: 'documentacion', patrones: ['docs/**', '**/AGENTS.md'], fuera: [], controla: 'los detectores de documentación, que corren con los tests enteros, y HarnessTests' },
  { repo: 'back', ficha: 'emails', patrones: ['src/**/*Email*', 'src/**/Messaging/**', 'src/**/Contracts/**'], controla: null },
  { repo: 'back', ficha: 'fechas-y-zonas', patrones: ['src/*.Domain/**', 'src/**/Contracts/**', 'src/**/Models/**', 'src/**/Messaging/**', 'src/**/Persistence/Configurations/**'], controla: null },
  { repo: 'back', ficha: 'guardado', patrones: ['src/**/Services/**', 'src/**/Persistence/**'], controla: null },
  { repo: 'back', ficha: 'idempotencia', patrones: ['src/**/Controllers/**', 'src/**/Idempotency/**'], controla: null },
  { repo: 'back', ficha: 'identificacion-fiscal', patrones: ['src/**/*TaxId*', 'src/**/Companies/**'], controla: null },
  { repo: 'back', ficha: 'logs', patrones: ['src/**/*.cs'], controla: null },
  { repo: 'back', ficha: 'modulos-habilitados', patrones: ['src/**/Features/**', 'src/**/Modules/**', 'src/**/*Feature*', 'src/**/*Limit*'], controla: null },
  { repo: 'back', ficha: 'modulos', patrones: ['src/**/Modules/**', 'src/*.Domain/WhatsApp/**', 'src/*.Domain/Assistant/**'], controla: null },
  { repo: 'back', ficha: 'multitenancy', patrones: ['src/*.Domain/**', 'src/**/Tenancy/**', 'src/**/Rls/**', 'src/**/Persistence/Configurations/**', 'src/**/Persistence/Migrations/**', 'src/**/BackgroundJobs/**', 'src/**/Controllers/**', 'src/**/RequestContext/**'], controla: null },
  { repo: 'back', ficha: 'numeros-y-moneda', patrones: ['src/*.Domain/**', 'src/**/Contracts/**', 'src/**/Models/**', 'src/**/Persistence/Configurations/**'], controla: null },
  { repo: 'back', ficha: 'paginado-y-busqueda', patrones: ['src/**/Readers/**', 'src/**/Controllers/**', 'src/**/Models/**'], controla: null },
  { repo: 'back', ficha: 'permisos', patrones: ['src/*.Domain/Authorization/**', 'src/**/Authorization/**', 'src/**/Controllers/**', 'src/**/Resources/Permissions*', 'src/**/*Seed*'], controla: null },
  { repo: 'back', ficha: 'persistencia-ef', patrones: ['src/**/Persistence/**'], controla: null },
  { repo: 'back', ficha: 'result-y-errores', patrones: ['src/*.Domain/**', 'src/**/Services/**', 'src/**/Controllers/**', 'src/**/ErrorHandling/**'], controla: null },
  { repo: 'back', ficha: 'telefonos', patrones: ['src/**/Phones/**', 'src/**/*Phone*'], controla: null },
  { repo: 'back', ficha: 'tests', patrones: ['src/**/*.cs', 'tests/**'], controla: null },
  { repo: 'back', ficha: 'textos-libres', patrones: ['src/**/Contracts/**', 'src/**/Models/**', 'src/**/Validation/**'], controla: null },
  { repo: 'back', ficha: 'textos-y-traducciones', patrones: ['src/**/Resources/**', 'src/**/*.resx', 'src/**/Services/**', 'src/**/Messaging/**', 'src/**/Modules/**'], controla: null },
  { repo: 'back', ficha: 'validacion', patrones: ['src/**/Validation/**', 'src/**/Contracts/**', 'src/**/Models/**'], controla: null },
  { repo: 'front', ficha: 'accesibilidad', patrones: ['src/**/*.tsx'], controla: null },
  { repo: 'front', ficha: 'accesos-y-permisos', patrones: ['src/app/**', 'src/auth/**', 'src/tenancy/**', 'src/areas/**', 'src/modules/**', 'src/**/*ermission*'], controla: null },
  { repo: 'front', ficha: 'datos-y-api', patrones: ['src/**/api/**', 'src/**/api.ts', 'src/**/hooks/**', 'src/**/use*.ts', 'src/**/use*.tsx'], controla: null },
  { repo: 'front', ficha: 'errores', patrones: ['src/**/*.ts', 'src/**/*.tsx'], controla: null },
  { repo: 'front', ficha: 'estructura-y-features', patrones: ['src/areas/**', 'src/modules/**', 'src/shared/**', 'src/app/**'], controla: null },
  { repo: 'front', ficha: 'formatos', patrones: ['src/**/*.tsx', 'src/shared/format/**'], controla: null },
  { repo: 'front', ficha: 'formularios', patrones: ['src/**/*Form*', 'src/**/*Dialog*', 'src/**/*Editor*', 'src/**/*Field*'], controla: null },
  { repo: 'front', ficha: 'paginado-y-listados', patrones: ['src/**/*List*', 'src/**/*Table*', 'src/**/*Filter*', 'src/**/*Pagination*'], controla: null },
  { repo: 'front', ficha: 'pantallas-y-ui', patrones: ['src/**/*.tsx', 'src/**/*.css'], controla: null },
  { repo: 'front', ficha: 'personal', patrones: ['src/areas/personal/**'], controla: null },
  { repo: 'front', ficha: 'responsive', patrones: ['src/**/*.tsx', 'src/**/*.css'], controla: null },
  { repo: 'front', ficha: 'telefonos', patrones: ['src/**/*Phone*', 'src/shared/phone/**'], controla: null },
  { repo: 'front', ficha: 'tests', patrones: ['src/**/*.ts', 'src/**/*.tsx'], conPruebas: true, controla: null },
  { repo: 'front', ficha: 'textos-y-traducciones', patrones: ['src/locales/**', 'src/modules/*/locales/**', 'src/**/*.tsx'], controla: null },
]

/** Los archivos de una ficha: los de su repo que cubren sus patrones, sin lo que ninguna ficha revisa. */
function archivosDeLaRegla(regla, archivos) {
  const fuera = (regla.fuera || FUERA[regla.repo]).concat(regla.repo === 'front' && !regla.conPruebas ? PRUEBAS_DEL_FRONT : [])
  return archivos.filter((a) => a.repo === regla.repo && coincide(a.ruta, regla.patrones) && !coincide(a.ruta, fuera))
}

/** Reparte los archivos de la etapa entre las fichas: cuáles llevan revisor y cuáles se saltean, y por qué. */
function reglasDeLaEtapa(archivos) {
  const conRevisor = []
  const controladas = []
  const sinArchivos = []
  for (const regla of REGLAS) {
    const propios = archivosDeLaRegla(regla, archivos)
    const nombre = `${regla.repo}/${regla.ficha}`
    if (!propios.length) sinArchivos.push(nombre)
    else if (regla.controla) controladas.push(`${nombre} (la controla ${regla.controla})`)
    else {
      conRevisor.push({
        id: `regla-${regla.repo}-${regla.ficha}`,
        titulo: `Regla · ${nombre}`,
        regla,
        ficha: regla.repo === 'back' ? `docs/rules/${regla.ficha}.md` : `${FRONT}/docs/rules/${regla.ficha}.md`,
        archivos: propios.map((a) => a.ruta),
        revisa: 'medium',
      })
    }
  }
  return { conRevisor, controladas, sinArchivos }
}

// Los temas de riesgo de la revisión adversarial y las rutas que los tocan. Desde el 2026-11-27 cubren también lo que
// miraba la premisa «Seguridad y aislamiento».
const ESCENARIOS = {
  datos: '- Datos: la migración es portable (el SQL en una línea y sin \\r), su Down se niega antes de perder datos y convive con la versión anterior (se agrega antes de borrar; un valor nuevo de un enum, según lo que anote el plan). Probá con filas guardadas antes del cambio. Guardar sin cambios escribe solo la fila de la entidad. En un lote, la fila que falla se registra y las demás siguen. Si hay cifrado: otra organización, una clave que no abre y un contenido ilegible.',
  permisos: '- Permisos: cada ruta declara [Access(...)] o [PublicSite], o es una anónima de la lista explícita, y lleva [HasPermission], [HasCompanyPermission] o [HasPlatformPermission]; un permiso nuevo está en el catálogo, en los dos resx y en el seed de los roles de sistema. A quien le falta un permiso recibe 403 y no ve nada del recurso, ni en un 409, un aviso o una vista previa. Nadie da lo que no tiene, el último administrador no se pierde, y lo que la pantalla ofrece sigue la condición del servidor.',
  efectos: '- Efectos fuera del sistema: cada respuesta del proveedor se clasifica como definitiva, transitoria o desconocida, y cada una tiene su salida y su test. Lo dudoso no se repite, un reintento no duplica, lo que cuesta se mide antes de pagar y cada tope sale de una sola constante. Todo con fakes, sin ninguna llamada real.',
  aislamiento: '- Aislamiento: cada tabla nueva tiene su clase (ITenantOwned, IPublishedByBusiness o IConsumerBusinessShared) y su RLS forzado con su helper, y nadie setea a mano las columnas de tenant ni ignora un filtro fuera de la lista blanca. Un id de otra organización da 404, igual que uno inventado. Plataforma entra solo con ITenantScope.Enter, después de autorizar y registrar el SecurityEvent. Los trabajos de fondo entran con su organización (TenantJobRunner o ITenantScope, nunca con una transacción abierta), y la caché y las cuotas llevan su alcance. Probá RLS con dos organizaciones y dos copias de la app a la vez: el lease y lo que corre una sola vez.',
}

const DEL_TEMA = {
  datos: {
    rutas: { back: ['src/**/Persistence/**', 'src/**/*Encrypt*', 'src/**/*Protect*', 'src/**/*Cipher*'], front: [] },
    puntos: '4 (guardar sin cambios no reescribe nada), 9 (las migraciones son portables y conviven con la versión anterior), 18 (uno malo no frena al resto) y 19 (anda con los datos que ya existen)',
    reglas: 'docs/rules/persistencia-ef.md, guardado.md y datos-personales.md, y docs/guides/migracion.md, del back',
  },
  permisos: {
    rutas: { back: ['src/**/Authorization/**', 'src/**/Controllers/**', 'src/**/Resources/Permissions*', 'src/**/*Seed*'], front: ['src/**/*ermission*', 'src/tenancy/**'] },
    puntos: '6 (nada nombra otro recurso sin permiso de leerlo) y 16 (lo que la pantalla ofrece sigue la misma condición que el servidor)',
    reglas: 'docs/rules/permisos.md y api-http.md del back, y docs/rules/accesos-y-permisos.md del front',
  },
  efectos: {
    rutas: { back: ['src/**/Integrations/**', 'src/**/Messaging/**', 'src/**/Files/**', 'src/**/Modules/WhatsApp/**', 'src/**/Modules/Assistant/**', 'src/**/*Outbox*'], front: [] },
    puntos: '12 (cada respuesta de un proveedor se clasifica) y 17 (cada tope sale de una sola constante, y lo que cuesta se mide antes de pagar)',
    reglas: 'docs/rules/modulos.md, idempotencia.md y logs.md del back',
  },
  aislamiento: {
    rutas: { back: ['src/**/Tenancy/**', 'src/**/Rls/**', 'src/**/Persistence/Migrations/**', 'src/**/Persistence/Configurations/**', 'src/**/BackgroundJobs/**', 'src/**/*Worker*', 'src/**/*TenantScope*', 'src/**/Caching/**', 'src/**/*CacheKeys*', 'src/**/RateLimiting/**'], front: [] },
    puntos: '6 (nada mezcla organizaciones) y 8 (cada exclusión o lock tiene su test con dos actores a la vez)',
    reglas: 'docs/rules/multitenancy.md, concurrencia.md, cache.md y cuotas.md del back, y de docs/guides/revisar-arquitectura.md los criterios de los bloques 5 (seguridad) y 9 (que no se mezclen las organizaciones)',
  },
}

/** Los temas de riesgo que tocan los archivos de la etapa, con sus tareas y sus archivos. */
function temasDeLaEtapa(archivos) {
  return Object.keys(ESCENARIOS).map((tema) => {
    const propios = archivos.filter((a) => coincide(a.ruta, DEL_TEMA[tema].rutas[a.repo] || []) && !coincide(a.ruta, FUERA[a.repo]))
    return { tema, tareas: [...new Set(propios.flatMap((a) => a.tareas || []))], archivos: propios.map((a) => `${a.repo === 'front' ? `${FRONT}/` : ''}${a.ruta}`) }
  }).filter((t) => t.archivos.length)
}

/** Si no se supo qué archivos cambió la etapa, van los temas que el plan marca con (R). */
function temasDelPlan(Es) {
  const temas = {}
  for (const E of Es) for (const t of E.conRiesgo) for (const tema of t.riesgo) (temas[tema] = temas[tema] || []).push(`${E.ctx.CLAVE}/${t.id}`)
  return Object.keys(temas).map((tema) => ({ tema, tareas: temas[tema], archivos: [] }))
}

/** Un revisor adversarial por cada tema de riesgo tocado; uno que no se toca no lleva revisor. */
function premisasDeRiesgo(temas, nombre) {
  const porTema = {}
  for (const x of temas || []) {
    if (!ESCENARIOS[x.tema]) continue
    const t = (porTema[x.tema] = porTema[x.tema] || { tareas: [], archivos: [] })
    for (const id of x.tareas || []) if (!t.tareas.includes(id)) t.tareas.push(id)
    for (const a of x.archivos || []) if (!t.archivos.includes(a)) t.archivos.push(a)
  }
  return Object.keys(ESCENARIOS).filter((tema) => porTema[tema]).map((tema) => {
    const { tareas, archivos } = porTema[tema]
    return {
      id: `riesgo-${tema}`,
      tema,
      titulo: `Adversarial · ${tema}`,
      revisa: 'high',
      foco: (C) => `- Sos un revisor adversarial: tu trabajo es romper lo que programaron estas tareas, que tocan ${tema}: ${tareas.length ? tareas.map(nombre).join('; ') : 'las de los commits de la etapa'}. Sus commits llevan la línea ${C.lineas}.${archivos.length ? ` Los archivos que tocan ${tema}: ${archivos.join(', ')}.` : ''}
${ESCENARIOS[tema]}
- Cada problema va con el escenario concreto que lo rompe. No lo arregles: lo arregla otro agente, con su test.`,
      puntos: DEL_TEMA[tema].puntos,
      reglas: `${DEL_TEMA[tema].reglas}, y las que nombra la spec para esas tareas`,
    }
  })
}

// Los dos revisores transversales: miran el diseño entero y lo que la spec no pide, no una ficha.
const TRANSVERSALES = [
  {
    id: 'arquitectura',
    titulo: 'Arquitectura, clean code y SOLID',
    revisa: 'high',
    foco: () => `- Capas: Domain ← Application ← Infrastructure, y Api compone. El recorrido es Controllers → I<X>Service → <X>Service → I<X>Repository/I<X>Reader/Integrations, y cada helper lleva un sufijo de la lista cerrada de capas-y-flujo.
- Una responsabilidad por clase, por componente y por hook, y nada duplicado: si ya hay una pieza que lo hace, en el back o en shared/ del front, se usa esa.
- Una sola forma de guardar: IUnitOfWork.ExecuteInTransactionAsync, una vez por método público que escribe, con locks, lecturas, reglas y escrituras en ese orden. Las reglas de negocio devuelven Result con <Entidad>Errors.
- Sin handlers, CQRS con ICommand, AutoMapper, Scrutor ni repositorio genérico.
- Tamaño y complejidad: un método o un componente que no se entiende de una leída se parte.`,
    puntos: '3 (cambiar algo en uso), 4 (guardar sin cambios no reescribe nada), 5 (ninguna lectura trae todo para quedarse con poco), 8 (cada exclusión o lock tiene su test con dos actores a la vez) y 11 (cada arreglo busca el mismo defecto en los caminos hermanos)',
    reglas: 'docs/rules/capas-y-flujo.md, guardado.md, result-y-errores.md, persistencia-ef.md, concurrencia.md y paginado-y-busqueda.md del back; docs/rules/estructura-y-features.md y datos-y-api.md del front; y, de docs/guides/revisar-arquitectura.md, los criterios de los bloques 1 a 3 (pedidos, varias copias y volumen) en el horizonte Hoy',
  },
  {
    id: 'spec',
    titulo: 'Lo que no está en la spec',
    revisa: 'medium',
    corre: true,
    foco: (C) => `- Primero corré en el back ${C.PLANES.map((p) => `node scripts/arneses/arnes.mjs conformidad --detectar --plan ${p}`).join(' y ')}. Cada línea que informe es un hallazgo: repo, archivo y línea según la línea, y en punto «conformidad». Si el arnés todavía no existe o falla, decilo en resumen con su salida en una línea y seguí.
- Después compará lo que ve o usa una persona y la etapa sumó (pantallas, tablas, columnas, filtros, combos, ventanas emergentes, rutas y permisos) contra la spec, el contrato (openapi.json) y sus tableros en ${FRONT}/docs/design/lienzo/. Lo que la etapa agregó y ninguno de los tres pide es un hallazgo; lo que piden y no está, también.
- Mirá además que ningún commit de la etapa haya deshecho lo de otro: lo de cada tarea sigue en HEAD (punto «pisados»).`,
    puntos: 'la fila «Commits que se pisan»',
    reglas: 'la spec que nombra cada plan, el contrato y los tableros: lo que no está en ninguno de los tres no se agrega, va a «Para después»',
  },
]

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
          area: TEXTO,
          migracion: { type: 'boolean' },
          archivos: LISTA,
        },
        required: ['id', 'titulo', 'carril', 'subcarril', 'riesgo', 'espera', 'estado', 'motivo', 'texto', 'area', 'migracion', 'archivos'],
      },
    },
    textos: {
      type: 'object',
      properties: {
        contrato: TEXTO,
        back: { type: 'array', items: { type: 'object', properties: { subcarril: TEXTO, texto: TEXTO }, required: ['subcarril', 'texto'] } },
        front: TEXTO,
      },
      required: ['contrato', 'back', 'front'],
    },
    pasosDelUsuario: {
      type: 'array',
      items: {
        type: 'object',
        properties: { paso: TEXTO, cuando: { type: 'string', enum: ['antes-de-revisar', 'despues'] }, soloMirar: { type: 'boolean' } },
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

const HALLAZGOS = {
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
}

const S_PREMISA = { type: 'object', properties: { hallazgos: HALLAZGOS, resumen: TEXTO }, required: ['hallazgos', 'resumen'] }

const FILA = { type: 'object', properties: { paso: TEXTO, resultado: TEXTO, numero: TEXTO }, required: ['paso', 'resultado', 'numero'] }
const S_TESTS = {
  type: 'object',
  properties: { tabla: { type: 'array', items: FILA }, hallazgos: HALLAZGOS, resumen: TEXTO },
  required: ['tabla', 'hallazgos', 'resumen'],
}

const S_ARCHIVOS = {
  type: 'object',
  properties: {
    archivos: {
      type: 'array',
      items: {
        type: 'object',
        properties: { repo: { type: 'string', enum: ['back', 'front'] }, ruta: TEXTO, tareas: LISTA },
        required: ['repo', 'ruta', 'tareas'],
      },
    },
    motivo: TEXTO,
  },
  required: ['archivos', 'motivo'],
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

const S_ULTIMA = {
  type: 'object',
  properties: {
    estado: { type: 'string', enum: ['hecha', 'bloqueada'] },
    commits: LISTA,
    arreglados: LISTA,
    alBacklog: PARA_DESPUES,
    archivosSinCommit: LISTA,
    motivo: TEXTO,
    verificacion: TEXTO,
  },
  required: ['estado', 'commits', 'arreglados', 'alBacklog', 'archivosSinCommit', 'motivo', 'verificacion'],
}

const S_CIERRE = { type: 'object', properties: { commits: LISTA, listas: LISTA, motivo: TEXTO }, required: ['commits', 'listas', 'motivo'] }

// ---------- Prompts ----------

function pLeer(ctx, anterior) {
  const sigue = anterior
    ? ` Este plan sigue a ${anterior} en la misma corrida del conductor, que solo llega a este si aquel terminó su código; la revisión, los arreglos y el cierre corren una vez al final, para todos: que esa etapa todavía no figure cerrada no es un bloqueo.`
    : ''
  return `Sos el primer paso del conductor que programa una etapa. Solo leés y corrés comandos de lectura (git log, git status, grep, listar procesos): no cambiás nada.

El plan ejecutable es ${ctx.PLAN}. La clave de sus commits es «${ctx.CLAVE}»: cada commit del conductor lleva la línea «Tarea: ${ctx.CLAVE}/<id>». Leé el plan entero y, de la spec que nombra, lo necesario para entender cada tarea y su orden. El front está en ${FRONT}.

Devolvé:
1. etapa: el nombre de la etapa, como lo dice el título del plan.
2. bloqueos: lo que impide empezar a programar hoy, uno por línea, o la lista vacía:
   - el plan dice que va después de otra etapa, y esa no tiene el código listo según la hoja de ruta (docs/plans/2026-09-27-plan-de-desarrollo.md) o su propio plan: su fila dice «Código listo», «Verificada» o «Cerrada». Que todavía corra su verificación no es un bloqueo: va a la vez que esta etapa.${sigue}
   - una decisión que el plan deja abierta en «Para decidir» y nadie tomó;
   - la app levantada: un proceso de Aspire (aspire o dcp) o de Vite (node con vite en su línea de comando);
   - cambios sin commitear de otra sesión en los archivos que la etapa va a tocar (git status de los dos repos). Los de otra área no bloquean: van en notas. Tampoco bloquean los de una tarea de este mismo plan que dejó una corrida anterior frenada (tocan los archivos de esa tarea, o están en una copia .local/copias/${ctx.CLAVE}-back-*): van en notas, con la tarea y la copia, y esa tarea los continúa.
   Los agentes que lista ListAgents (subagentes de Diseño o de otra sesión) no son un bloqueo: un conductor frenado no sigue vivo, y bloquea solo lo de esta lista.
3. cupoSemanal: si tenés la herramienta mcp__ccd_session_mgmt__get_usage (buscala con ToolSearch), el porcentaje usado del límite semanal de todos los modelos. Si no la tenés, null.
4. tareas: cada K, B, F e I del plan, en el orden en que el conductor las va a correr. Las I no corren como tareas: las cubren la revisión, los arreglos, la última revisión y el cierre del conductor; listalas igual, con carril integracion.
   - carril: contrato (K), back (B), front (F) o integracion (I). subcarril: el nombre del carril en el plan (por ejemplo «Back A»), o el carril.
   - Las del back van en UN orden que respeta cada espera y cada regla de orden que el plan escribe (por ejemplo, lo que espera el commit de una migración de otra tarea): una tarea va siempre después de lo que espera. El conductor las reparte después en carriles que corren a la vez, cada uno en su copia.
   - area: en las del back, el área de código que toca, en una o dos palabras (por ejemplo «permisos», «asistente», «identidad», «whatsapp»): las de la misma área van en el mismo carril, así no chocan. En las demás, cadena vacía.
   - migracion: true si la tarea crea o cambia una migración de EF (todas esas van en el mismo carril, porque comparten el snapshot del modelo).
   - archivos: el mapa de la tarea para quien la programe, así no recorre el repo: los archivos que crea o cambia y el modelo que copia, con su ruta desde la raíz de su repo, tal como los nombran el plan y la spec. Comprobá con ls que existen los que ya deberían existir, y corregí la ruta si cambió; marcá «(nuevo)» los que la tarea crea. Como mucho unos 15.
   - riesgo: las categorías de lo que el plan marca con (R): datos (migraciones, persistencia o cifrado), permisos, efectos (Meta, correo, archivos o IA) y aislamiento (RLS, ITenantScope, trabajos de fondo, caché y cuotas por alcance). Si el plan no marca ninguna tarea con (R), decidilo vos con esas categorías, por lo que toca cada una.
   - espera: los ids que tienen que estar commiteados antes, de otro carril, o del mismo si esta tarea los usa. El contrato va entero antes del back y del front: no lo pongas.
   - estado: «hecha» solo si en su repo hay commits con «Tarea: ${ctx.CLAVE}/<id>» y lo que pide está en el código (mirá sus archivos principales). «del-usuario» si la hace el usuario (cargar secretos, probar con Meta, aprobar plantillas). «bloqueada» si espera algo de afuera que no está: un tablero que no existe en ${FRONT}/docs/design/lienzo/ (buscalo por su nombre) o una decisión sin tomar. Si no, «pendiente».
   - motivo: por qué tiene ese estado, en una línea (sus commits, si está hecha).
   - texto: lo que el plan dice de la tarea, literal.
5. textos: los textos de chat del plan, literales: contrato, cada carril del back con su nombre y front. Si falta uno, cadena vacía.
6. pasosDelUsuario: lo que el plan deja en manos del usuario (cargar secretos, pruebas con Meta, aprobar plantillas, la prueba integral del bloque). En cada uno, cuando: «antes-de-revisar» si los tests enteros de la revisión lo necesitan, o «despues»; y soloMirar: true si es solo mirar lo que deja la corrida (la prueba integral), false si el usuario tiene que hacer o cargar algo (secretos, Meta, plantillas).
7. notas: lo que conviene que sepa quien lanza el conductor, en pocas líneas.`
}

function repoDe(t, carril) {
  if (t.carril === 'front') return FRONT
  return carril && carril.copia ? `la copia ${carril.copia} del back (hacé cd ahí)` : 'la carpeta actual (el back)'
}

function verificacionDe(t) {
  if (t.carril === 'front') {
    return `- Para los tests, node scripts/verificar.mjs en el front: muestra solo lo que falla y deja el log entero en .local/verificar/. Mientras trabajás, con --cambiados (solo lo que tocan tus cambios) o --archivos <tests>. Al terminar, solo los tipos (npx tsc -b) y el lint de lo que tocaste, para que main compile. Todos los tests corren una vez, con todo el código de la etapa, en la revisión.`
  }
  return `- Para el build y los tests, node scripts/arneses/verificar.mjs en el back (o en tu copia): muestra solo lo que falla y deja el log entero en .local/verificar/. El test dirigido: --sin-build --proyecto <Proyecto> --clase <Clase>, o --integracion --clase <Clase> si es de integración. Al terminar, solo el build de la solución (dotnet build ArquitecturaBaseMultitenant.slnx, sin advertencias), para que main compile. Los tests rápidos enteros, la integración y la seguridad no los corrés: corren una vez, con todo el código de la etapa, en la revisión.`
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

function pProgramar(ctx, t, falta, vuelta, carril) {
  const enCopia = Boolean(carril && carril.copia)
  const cambios = ctx.CAMBIOS.length ? `\nCambios al contrato que hicieron otras tareas de esta corrida:\n- ${ctx.CAMBIOS.join('\n- ')}\n` : ''
  const sigue = falta
    ? `\nOtro agente empezó esta tarea y paró a mitad (vas por la vuelta ${vuelta + 1}). Lo que dejó dicho: ${falta}. Mirá git status y git log --grep "Tarea: ${ctx.CLAVE}/${t.id}" en ${repoDe(t, carril)} para ver dónde quedó, y seguí desde ahí.\n`
    : `\nSi en su repo ya hay commits con «Tarea: ${ctx.CLAVE}/${t.id}» de una corrida anterior que quedó a medias, mirá qué hicieron con git log --grep "Tarea: ${ctx.CLAVE}/${t.id}" --stat y seguí desde ahí, sin rehacerlo. Lo que vio el lector del plan: ${t.motivo || 'nada'}.\n`
  const preparar = enCopia && !falta
    ? `\nAntes de empezar, desde la carpeta del back: node scripts/arneses/carril.mjs preparar ${carril.copia} (crea la copia o la pone al día con main).\n`
    : ''
  const mapa = (t.archivos || []).length ? `\nEl mapa de la tarea (lo que crea o cambia y el modelo que copia): ${t.archivos.join(', ')}. Empezá por ahí, sin recorrer el repo.\n` : ''
  const entrega = enCopia ? ` y entregalos con node scripts/arneses/carril.mjs entregar ${carril.copia}` : ''
  const cierre = `Commiteá cada parte coherente y verificada con la línea «Tarea: ${ctx.CLAVE}/${t.id}»${entrega}, y devolvé estado «hecha» con sus commits.`
  const parar = ` y commiteá lo que está en verde${enCopia ? ' (entregalo también)' : ''},`
  return `Sos el agente que programa la tarea ${t.id} de la etapa «${ctx.ETAPA}» (plan: ${ctx.PLAN}).
${CUPO}${preparar}

Este es el texto con que se abre el chat de tu carril (${t.subcarril || t.carril}). De ahí sale qué leer y qué reglas valen:
"""
${textoDelCarril(t, ctx.TEXTOS)}
"""

De ese texto hacés SOLO la tarea ${t.id}: ${t.titulo}. Lo que dice el plan de ella:
"""
${t.texto}
"""
Sus pasos, archivos y tests están en la spec que nombra el plan. Ya están commiteadas: ${ctx.hechas()}. Lo que el texto dice de avisarle a la Guía o de esperarla lo resuelve el conductor: no esperes a nadie, no avises y no hagas otra tarea.
${mapa}${cambios}${sigue}
El texto del chat y el plan pueden pedir, por tarea, los tests rápidos enteros, la integración de las áreas o la revisión adversarial antes del commit: eso cambió el 2026-11-26 y no se hace. Tampoco corrés el E2E real, la galería ni el recorrido: desde el 2026-11-27 corren una vez, en la verificación de la etapa, después del código listo. Vale lo de abajo.

Cómo trabajás:
- Código primero (pedido del usuario del 2026-11-26): el código y su test dirigido, en rojo y después en verde, y al terminar solo el build o los tipos, como dice abajo. Nada de suites enteras ni integración: corren una vez, al final. Si en tu copia o en tu repo hay cambios sin commitear de un agente anterior de esta misma tarea (la corrida anterior se frenó el 2026-11-26 para pasar a código primero), seguí desde ahí. Si son de otra tarea, no los toques ni los borres: devolvé bloqueada nombrándolos.
${verificacionDe(t)}
- ${cierre}
- Si llevás unos ${PASOS_POR_AGENTE} pasos y no terminaste, pará en un punto coherente${parar} y devolvé estado «parcial»: en falta va lo que queda, concreto, para el agente que sigue. Un agente corto gasta mucho menos cupo que uno largo.
- Si hace falta un cambio de contrato y estás en el back, hacelo (openapi.json regenerado) y contalo en cambiosAlContrato, una línea por cambio. Si estás en el front, no toques el back: seguí con lo que puedas, contá el cambio en cambiosAlContrato y lo hace el integrador.
- En archivosSinCommit va lo que dejás tocado y sin commitear al terminar; si commiteaste todo, la lista vacía. En verificacion va lo que corriste y su resultado, en una línea.

${SIN_AGREGAR}

${comunes(ctx, carril)}`
}

function lineaDeHallazgo(h) {
  const extra = `${h.regla ? ` Regla: ${h.regla}.` : ''}${h.de ? ` Lo vio: ${h.de}.` : ''}`
  return `- [${h.gravedad}] ${h.titulo} (${h.archivo}:${h.linea}). Escenario: ${h.escenario}.${extra}`
}

// Por si quien programa devuelve «lista» con el código sin commitear: otro agente lo commitea, sin volver a correr nada.
function pCommitear(ctx, t, archivos, carril) {
  const entrega = carril && carril.copia ? `, entregalo con node scripts/arneses/carril.mjs entregar ${carril.copia}` : ''
  return `Sos el agente que commitea la tarea ${t.id} de la etapa «${ctx.ETAPA}» (plan ${ctx.PLAN}). No la programaste.

La tarea: ${t.titulo}. Sus archivos, en ${repoDe(t, carril)}: ${archivos.join(', ')}.

Quien la programó ya corrió su test dirigido y el build. Confirmá con git status que los archivos de la tarea están ahí, sin cambios ajenos mezclados, y no vuelvas a correrlos.

Commiteá todo lo de la tarea con la línea «Tarea: ${ctx.CLAVE}/${t.id}»${entrega}, y devolvé estado hecha con los commits. Si falta algo o no compila, no commitees: devolvé bloqueada, con el motivo y archivosSinCommit.

${comunes(ctx, carril)}`
}

// Cada plan trae «Revisión y cierre»: los riesgos que suma son ítems de la lista de control de los revisores, de la
// última revisión y del cierre, y la documentación que nombra la escribe el cierre.
function delPlan(C) {
  return `De la sección «Revisión y cierre» de ${C.PLANES.join(' y de ')}: los riesgos que suma son ítems de tu lista de control; miralos si tocan lo tuyo.`
}

// El script no lee git: este agente barato le pasa la lista de archivos de la etapa, y el script la cruza con REGLAS.
function pArchivos(C) {
  return `Sos el que lista los archivos que cambió ${C.deLa}, para repartir la revisión del cierre por ficha. Solo leés: no cambiás nada, no abrís ni juzgás ningún archivo.

En el back (la carpeta actual) y en el front (${FRONT}), corré git log ${C.grep} --name-only --format="%n%H%n%b". Devolvé cada archivo una sola vez: su repo (back o front), su ruta desde la raíz de su repo, con /, y en tareas las de la línea «Tarea: <clave>/<id>» de los commits que lo cambiaron, como <clave>/<id> (por ejemplo ${C.CLAVES[0]}/B1). Los que la etapa borró van también. En motivo, una línea solo si algo no se pudo leer.`
}

// Un revisor por ficha: recibe la ficha y solo sus archivos, y busca únicamente incumplimientos de esa regla.
function pRevisarRegla(C, p) {
  return `Sos el revisor de la regla ${p.regla.repo}/${p.regla.ficha} en el cierre de ${C.deLa}. Solo leés: no cambiás nada, no compilás ni corrés tests.

La regla es la ficha ${p.ficha}: leela entera, con su «Cómo se hace» y su «Prohibido».

Tus archivos, y solo ellos, en ${dondeDe(p.regla.repo)}:
${p.archivos.map((a) => `- ${a}`).join('\n')}

Su diff, en ese repo: git log ${C.grep} -p --format="%H %s" -- <esos archivos>. Leé lo que cambió la etapa y, si hace falta, el resto del archivo para entenderlo.

Buscá únicamente incumplimientos de esa ficha en lo que cambió la etapa. Lo demás lo miran otros revisores: no lo nombres. ${delPlan(C)}

Devolvé solo problemas reales. En repo, ${p.regla.repo}; archivo y línea; en punto, la sección de la ficha; en escenario, qué hace alguien y qué pasa, en una línea; en regla, ${p.regla.ficha}; y en porque, la evidencia que te convenció. Una lista vacía es un resultado válido: no inventes.`
}

function pRevisarPremisa(C, p) {
  return `Sos el revisor de la premisa «${p.titulo}» en el cierre de ${C.deLa}. Solo leés: no cambiás nada, no compilás ni corrés tests${p.corre ? ', salvo el arnés que se nombra abajo' : ''}.

Lo que revisás son los cambios de la etapa en los dos repos: los commits con la línea ${C.lineas}. Listalos con git log ${C.grep} --format="%H %s" en el back (la carpeta actual) y en el front (${FRONT}), y leé sus diffs. De la spec que nombra ${C.CLAVES.length > 1 ? 'cada plan' : 'el plan'}, leé lo que haga falta para saber qué pidió cada tarea.

Tu premisa, y solo ella:
${p.foco(C)}

De la «Lista de control» de .agents/skills/puerta/SKILL.md, mirás ${p.puntos}. Las reglas son ${p.reglas}. Lo demás lo miran otros revisores o la verificación de la etapa: no lo nombres. ${delPlan(C)}

Devolvé solo problemas reales del código de la etapa. En repo, back o front; archivo y línea; en punto, el número de la lista o la regla; en escenario, qué hace alguien y qué pasa, en una línea; en regla, la que se rompe; y en porque, la evidencia que te convenció. Una lista vacía es un resultado válido: no inventes.`
}

// Los tests enteros son un revisor más: corren en copias de HEAD dentro de .local/, que se borran al terminar.
function pTests(C) {
  const copias = `.local/copias/${C.CLAVES[0]}-tests`
  return `Sos el que corre los tests enteros de ${C.deLa}, a la vez que los revisores del cierre. No arreglás nada: lo que falla vuelve como hallazgo y lo arregla otro agente.

1. Armá dos copias hermanas de HEAD en ${copias}/, con el nombre exacto de cada repo, porque contracts:check y los detectores buscan el otro repo al lado: git clone --local . ${copias}/ArquitecturaBaseMutitenant y git clone --local ${FRONT} ${copias}/ArquitecturaBaseMutitenantFront, cada una con git -C <copia> checkout --detach <el HEAD de su repo>. Sin worktrees ni ramas. En el front de la copia, npm ci.
2. Corré cada paso entero, de a uno y en segundo plano, con la salida en un archivo de .local/:
   - en el back de la copia, node scripts/arneses/carril.mjs fila "dotnet build ArquitecturaBaseMultitenant.slnx" (0 errores y 0 advertencias) y después node scripts/arneses/carril.mjs fila "dotnet test --no-build" (con Docker): fila espera su turno en la fila pesada de main;
   - en el front de la copia, npm run build, npm run lint, npm test (solo, sin la suite del back a la vez: con las dos, los tests con axe pasan los 5 s) y npm run contracts:check;
   - en el back de la copia, node scripts/arneses/arnes.mjs realinear-docs --detectar, estado-docs --detectar y ${C.PLANES.map((p) => `conformidad --detectar --plan ${p}`).join(' y ')}.
3. Leé solo los resúmenes y, de lo que falla, sus líneas. Un test que se pasó de tiempo se corre una vez más, solo: si así pasa, era la carga de la PC y no cuenta.
4. Al terminar, aspire stop si algo quedó levantado, y borrá ${copias}.

Sin E2E real, galería ni recorrido: van en la verificación de la etapa, el segundo hito (pedido del usuario del 2026-11-27).

Devolvé en tabla una fila por paso, con su resultado y su número (por ejemplo, «1989/1989»). Cada test que falla, cada advertencia del build y cada hallazgo de un detector es un hallazgo: repo; archivo y línea, los del test o los que da el detector; en punto, el paso; en escenario, la causa en una línea; en regla, «tests enteros» o el detector; en porque, la salida que lo muestra; y gravedad alta. Sin fallos, la lista vacía.

${comunes(C)}`
}

// Arreglos: sin fase aparte de verificación, quien arregla primero comprueba cada hallazgo contra el código.
function pArreglar(C, repo, hallazgos) {
  return `Sos el agente que arregla lo que encontró la revisión del cierre de ${C.deLa} en ${dondeDe(repo)}. No programaste la etapa ni la revisaste.

Lo que encontraron los revisores, los tests enteros y los detectores, sin repetidos:
${hallazgos.map(lineaDeHallazgo).join('\n')}

Con cada uno, primero comprobá contra el código de HEAD que es real: el archivo y la línea existen, el escenario pasa o la regla se rompe, y es de la etapa (sus commits llevan la línea ${C.lineas}). Si no es real, si ya estaba antes de la etapa o si otra pieza lo resuelve, no lo toques: va en descartados, con su motivo. Si es real, arreglalo con un test dirigido que falle sin el arreglo, buscá el mismo defecto en los caminos hermanos, y corré ese test y el build (en el front, tsc -b y el lint de lo que tocaste). Un test de los tests enteros que falla se arregla en su causa, no en el test, salvo que el test esté mal. Commiteá cada arreglo coherente con la línea ${C.commitDe('revision')}.

En arreglados, una línea por hallazgo arreglado, con su commit.

${comunes(C)}`
}

function pUltima(C, arreglos) {
  return `Sos la última revisión de código de ${C.deLa}. No la programaste ni la arreglaste. Leés el diff entero de la etapa, con los arreglos, en los dos repos: git log ${C.grep} --format="%H %s" en el back (la carpeta actual) y en el front (${FRONT}); primero el --stat y después los diffs que importan.

Los arreglos de la revisión: ${arreglos}.

Buscás dos cosas: lo que rompió un arreglo (un camino hermano, un test que dejó de probar lo que probaba, un contrato que cambió sin su lector) y lo grave que quedó (datos, permisos, organizaciones que se mezclan, efectos fuera del sistema). No repitas la revisión por ficha. ${delPlan(C)}
- Lo chico y seguro lo arreglás vos, con su test dirigido y el build (en el front, tsc -b y el lint de lo que tocaste), y lo commiteás con la línea ${C.commitDe('ultima-revision')}.
- Lo que no es chico no lo arreglás: va en alBacklog (que, porQue y donde). No hay otra ronda.

En arreglados, una línea por cosa arreglada, con su commit; en verificacion, lo que corriste y su resultado.

${comunes(C)}`
}

function lineaParaDespues(p) {
  return `- **${p.que}.** ${p.porQue} Dónde: ${p.donde} (${p.tarea}).`
}

// Con el código listo, la sección «Verificación de la etapa» del plan pasa entera al backlog antes de retirarlo:
// verificar-etapa.js escribe y corre desde ahí el E2E, el recorrido y la galería de la etapa, y la saca al verificarla.
function seccionDelBacklog(E, retira) {
  const lineas = E.PARA_DESPUES_DEL_PLAN.length ? `una línea por cosa que no esté ya, así:
${E.PARA_DESPUES_DEL_PLAN.map(lineaParaDespues).join('\n')}` : ''
  const paraDespues = lineas ? `
- En docs/backlog.md, la sección «## Para después de la etapa ${E.ctx.CLAVE}», al final si todavía no está, con ${lineas}` : ''
  if (!retira) return paraDespues
  return `
- En docs/backlog.md, la sección «## Verificación de la etapa ${E.ctx.CLAVE}», al final: antes del git rm, la sección «Verificación de la etapa» del plan (en los planes de antes del 2026-11-27, «Al cierre del bloque E12–E25») pasa ahí entera, para la verificación.${paraDespues}`
}

function estadoDeLasTareas(E) {
  return E.tareas.filter((t) => t.carril !== 'integracion').map((t) => {
    const r = E.R[t.id] || { estado: t.estado, motivo: t.motivo }
    return `  - ${t.id}: ${r.estado}${r.motivo ? `, ${r.motivo}` : ''}`
  }).join('\n')
}

/**
 * El cierre, uno para todos los planes del lanzamiento. Si la etapa quedó lista (código, arreglos y última revisión
 * hechos), queda con el código listo, el primer hito: la spec cerrada, el plan retirado, la hoja de ruta y el backlog,
 * como en 82087a6a, con su sección de verificación en el backlog para verificar-etapa.js. Si no, solo la tabla de
 * estado del plan y lo de para después.
 */
function pCierre(Es, K) {
  const C = contextoDeCierre(Es)
  const compartidos = 'Antes de tocar docs/backlog.md, mirá git diff --quiet -- docs/backlog.md: si tiene cambios sin commitear de otra sesión, no lo toques y decilo en motivo. Lo mismo con la hoja de ruta y con ignorados.json.'
  const copias = `Si quedan copias de esta corrida en .local/copias/, corré ${Es.map((E) => `node scripts/arneses/carril.mjs limpiar ${E.ctx.CLAVE}-back`).join(' y ')}: borra las que ya entregaron todo. Las que no, nombralas en motivo.`
  const cambios = (E) => (E.ctx.CAMBIOS.length ? E.ctx.CAMBIOS.join('; ') : 'ninguno')
  if (!K.cerrable) {
    return `Sos el que deja al día ${Es.length > 1 ? 'los planes' : 'el plan'} después de la corrida del conductor. La etapa no se cierra: ${K.porQueNoCierra.join('; ') || 'falta lo de abajo'}. Cambiás solo ${Es.length > 1 ? 'esos planes' : 'ese plan'} y docs/backlog.md si hay algo para después, sin historial ni actas.
${Es.map((E) => `
Plan ${E.ctx.PLAN} («${E.ctx.ETAPA}»):
- En la tabla de estado del principio (Parte | Tareas | Estado), cada parte dice «Hecha» si todas sus tareas están hechas; si no, «Pendiente: <ids y motivo corto>». Sus tareas:
${estadoDeLasTareas(E)}
- En «Cambios al contrato», una línea por cambio de esta corrida que todavía no esté: ${cambios(E)}.${seccionDelBacklog(E, false)}`).join('\n')}

${compartidos}
Commiteá solo eso, con git commit -m "docs(planes): ${Es.map((E) => E.ctx.ETAPA).join(' y ')}, el estado después del conductor" -m "Tarea: ${Es[0].ctx.CLAVE}/cierre" -- <los planes>, más docs/backlog.md si lo tocaste. Si nada cambió, no commitees y devolvé commits vacío. En listas, la lista vacía.
${copias}

${comunes(C)}`
  }
  return `Sos el que deja ${C.deLa} con el código listo, el primer hito: el código está, la revisión y sus arreglos corrieron y la última revisión terminó. La verificación (el contrato, el E2E real, la galería y el recorrido) es el segundo hito y va aparte, con verificar-etapa.js. La fecha es la de hoy (date +%F). Cambiás solo lo que dice abajo, sin historial ni actas y en el tono de cada documento. Mirá cómo se cerraron Acciones del asistente y Costuras de módulos en el commit 82087a6a.

Los tests enteros: ${JSON.stringify(K.tests ? K.tests.tabla : [])}.
La revisión: ${K.resumen}.
${Es.map((E) => `
Plan ${E.ctx.PLAN} («${E.ctx.ETAPA}»), con estas tareas:
${estadoDeLasTareas(E)}
- La spec que nombra pasa a «**Estado:** cerrado · se cerró el <fecha> …», y su sección del cierre dice, en una tabla corta, los tests enteros y la revisión. Lo que la etapa deja a otras queda dicho ahí, y los cambios al contrato de esta corrida también: ${cambios(E)}.
- git rm del plan. Lo que apuntaba a él (la hoja de ruta, otras specs, el índice de formularios, el backlog, las fichas de docs/features/) pasa a apuntar a la spec.
- En la hoja de ruta (docs/plans/2026-09-27-plan-de-desarrollo.md), la fila de la etapa dice «Código listo el <fecha>; falta la verificación», con lo que trajo en una o dos líneas, y cuál es la siguiente.
- Si la etapa tiene número (E12, E13…), se suma a ClosedStages en los dos repos (tests/ArquitecturaBaseMultitenant.ArchitectureTests/HarnessStage.cs y, en el front, src/test/HarnessStage.ts), como dice docs/rules/documentacion.md.
- La documentación que nombra la sección «Revisión y cierre» del plan la escribís vos, en su lugar.${seccionDelBacklog(E, true)}`).join('\n')}

Después corré node scripts/arneses/arnes.mjs estado-docs --detectar y realinear-docs --detectar, y dejalos en 0: si estado-docs pide retirar una spec cerrada que se queda como referencia, va a scripts/arneses/estado-docs/ignorados.json con su motivo y la fecha.
${compartidos}
Commiteá todo junto en el back, con git commit -m "docs(planes): ${Es.map((E) => E.ctx.ETAPA).join(' y ')}, código listo el <fecha>: se retira${Es.length > 1 ? 'n sus planes y sus verificaciones pasan' : ' su plan y su verificación pasa'} al backlog" -m "Tarea: ${Es[0].ctx.CLAVE}/cierre" -- <rutas>; en el front, otro commit si tocaste HarnessStage.ts. Devolvé los commits y en listas las claves de lo que dejaste con el código listo.
${copias}

${comunes(C)}`
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

/**
 * Reparte las tareas del back en carriles que corren a la vez, cada uno en su copia (ADR 0071). Van juntas las de
 * la misma área (tocan los mismos archivos) y todas las que traen migración (comparten el snapshot del modelo).
 * Cada grupo va al carril con menos tareas pendientes (con código primero, una tarea con riesgo es un agente, como
 * cualquiera), y adentro de cada carril se respeta el orden del plan: como cada tarea va después de lo que espera,
 * nadie espera a algo que su propio carril todavía no corrió.
 */
function armarCarriles(back, cuantos, clave) {
  const grupo = {}
  const raiz = (id) => (grupo[id] === id ? id : (grupo[id] = raiz(grupo[id])))
  const unir = (a, b) => { grupo[raiz(a)] = raiz(b) }
  for (const t of back) grupo[t.id] = t.id
  const porClave = {}
  for (const t of back) {
    for (const k of [t.area ? `area:${String(t.area).toLowerCase()}` : null, t.migracion ? 'migracion' : null].filter(Boolean)) {
      if (porClave[k]) unir(t.id, porClave[k])
      else porClave[k] = t.id
    }
  }
  // Pesa solo lo que falta: una tarea ya hecha no carga a su carril (en la base 2, un carril se llevó cuatro de seis pendientes).
  const peso = (t) => (t.estado !== 'pendiente' ? 0 : 1)
  const grupos = {}
  back.forEach((t, i) => {
    const g = (grupos[raiz(t.id)] = grupos[raiz(t.id)] || { ids: new Set(), peso: 0, primero: i })
    g.ids.add(t.id)
    g.peso += peso(t)
  })
  const n = Math.max(1, Math.min(cuantos, Object.keys(grupos).length))
  const carga = Array.from({ length: n }, () => ({ ids: new Set(), peso: 0 }))
  for (const g of Object.values(grupos).sort((a, b) => b.peso - a.peso || a.primero - b.primero)) {
    const destino = carga.reduce((menor, c) => (c.peso < menor.peso ? c : menor), carga[0])
    g.ids.forEach((id) => destino.ids.add(id))
    destino.peso += g.peso
  }
  return carga
    .filter((c) => c.ids.size)
    .map((c, i, todos) => ({
      nombre: todos.length > 1 ? `back-${i + 1}` : 'back',
      copia: todos.length > 1 ? `.local/copias/${clave}-back-${i + 1}` : null,
      tareas: back.filter((t) => c.ids.has(t.id)),
    }))
}

// ---------- Lo que cuesta ----------

// Un plan: leer 1 y un agente por tarea pendiente (código primero: cada una commitea sin revisión aparte).
function agentesDelPlan(pendientes) {
  return 1 + pendientes.length
}

// Las fases 2 a 5 van una vez por lanzamiento: el que lista los archivos, una por ficha con revisor, los transversales,
// una por tema de riesgo, los tests enteros, hasta 2 arregladores, la última revisión y el cierre.
function agentesDelCierre(reglas, temasDeRiesgo) {
  return 1 + reglas + TRANSVERSALES.length + temasDeRiesgo + 1 + 2 + 1 + 1
}

/** Lo que el plan dice que van a tocar sus tareas (su mapa de archivos), para estimar qué fichas llevan revisor. */
function archivosPrevistos(tareas) {
  return tareas.filter((t) => t.carril !== 'integracion').flatMap((t) => (t.archivos || []).map((r) => ({
    repo: t.carril === 'front' ? 'front' : 'back',
    ruta: String(r).replace(/\\/g, '/').replace(/\s*\(nuevo\)\s*$/, '').trim(),
    tareas: [t.id],
  })))
}

function modelo(opciones, rol) {
  return MODELOS[rol] ? Object.assign(opciones, { model: MODELOS[rol] }) : opciones
}

function porRepo(lista, campo) {
  const g = {}
  for (const x of lista) if (x && (x[campo] === 'back' || x[campo] === 'front')) (g[x[campo]] = g[x[campo]] || []).push(x)
  return g
}

// ---------- Fase 1: programar cada plan ----------

/**
 * Lee el plan y programa sus tareas: el contrato y después los carriles del back y el front, a la vez. Las tareas
 * de integración del plan no corren: las cubren las fases 2 a 5. Devuelve su estado para el cierre, o { fin } con lo
 * que se devuelve tal cual: lo planificado o lo que no empezó.
 */
async function programarPlan(PLAN, anterior) {
  const CLAVE = claveDe(PLAN)
  const ctx = {
    PLAN,
    CLAVE,
    ETAPA: '',
    TEXTOS: null,
    CAMBIOS: [],
    corrieron: 0,
    et: (s) => (PREFIJO ? `${CLAVE} · ${s}` : s),
    hechas: () => 'ninguna',
  }

  phase('Programar')
  const L = await agent(pLeer(ctx, anterior), { label: ctx.et('leer el plan'), phase: 'Programar', schema: S_LEER })
  if (!L) throw new Error(`El agente que lee el plan ${PLAN} no terminó`)

  const tareas = L.tareas
  const porId = Object.fromEntries(tareas.map((t) => [t.id, t]))
  const contrato = tareas.filter((t) => t.carril === 'contrato')
  const back = tareas.filter((t) => t.carril === 'back')
  const front = tareas.filter((t) => t.carril === 'front')
  // Lo que el plan marca con (R): si no se sabe qué archivos cambió la etapa, van estos temas.
  const conRiesgo = [...contrato, ...back, ...front].filter((t) => t.riesgo.length)

  const bloqueos = L.bloqueos.slice()
  if (typeof L.cupoSemanal === 'number' && L.cupoSemanal >= TOPE_CUPO) bloqueos.push(`El cupo semanal va en ${L.cupoSemanal} %: con ${TOPE_CUPO} % no se empieza a programar`)
  // Si el reparto en carriles dejara a uno esperando para siempre, el back vuelve a un solo carril, en main.
  let carrilesBack = armarCarriles(back, CARRILES_BACK, CLAVE)
  let ciclo = buscarCiclo(contrato, carrilesBack.map((c) => c.tareas).concat([front]), porId)
  if (ciclo && carrilesBack.length > 1) {
    carrilesBack = armarCarriles(back, 1, CLAVE)
    ciclo = buscarCiclo(contrato, [back, front], porId)
  }
  if (ciclo) bloqueos.push(`Las esperas del plan forman un ciclo: ${ciclo.join(' → ')}`)

  const pendientes = tareas.filter((t) => t.carril !== 'integracion' && t.estado === 'pendiente' && (!SOLO || SOLO.includes(t.id)))
  const pasosAntes = L.pasosDelUsuario.filter((p) => p.cuando === 'antes-de-revisar').map((p) => p.paso)

  if (MODO === 'planificar') {
    const temasDeRiesgo = [...new Set(conRiesgo.flatMap((t) => t.riesgo))]
    const reglasPrevistas = reglasDeLaEtapa(archivosPrevistos(tareas)).conRevisor.map((p) => `${p.regla.repo}/${p.regla.ficha}`)
    return {
      fin: {
        etapa: L.etapa,
        clave: CLAVE,
        bloqueos,
        cupoSemanal: L.cupoSemanal,
        carriles: { contrato: contrato.map((t) => t.id), back: back.map((t) => t.id), front: front.map((t) => t.id) },
        carrilesBack: carrilesBack.map((c) => ({ nombre: c.nombre, copia: c.copia, tareas: c.tareas.map((t) => t.id) })),
        tareas: tareas.map((t) => ({ id: t.id, carril: t.carril, estado: t.estado, riesgo: t.riesgo, espera: t.espera, motivo: t.motivo, area: t.area, migracion: t.migracion })),
        pasosDelUsuario: L.pasosDelUsuario,
        temasDeRiesgo,
        reglasPrevistas,
        // Con planes, las fases 2 a 5 se cuentan una vez, en la cadena.
        agentesEstimados: agentesDelPlan(pendientes) + (CADENA ? 0 : agentesDelCierre(reglasPrevistas.length, temasDeRiesgo.length)),
        notas: L.notas,
      },
    }
  }
  if (bloqueos.length) return { fin: { etapa: L.etapa, clave: CLAVE, resultado: 'no empezó', bloqueos, notas: L.notas } }

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
  ctx.hechas = () => {
    const h = Object.values(R).filter((r) => r.estado === 'hecha').map((r) => (r.commits[0] ? `${r.id} (${r.commits[0]})` : r.id))
    return h.length ? h.join(', ') : 'ninguna'
  }
  const PREGUNTAS = []
  const PASOS = L.pasosDelUsuario.map((p) => p.paso)
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

  async function programarTarea(t, carril) {
    // Código primero (2026-11-26): toda tarea commitea al terminar; la revisión va una vez, en la fase 2.
    const rol = t.carril === 'front' ? 'front' : (t.carril === 'back' ? 'back' : null)
    let r = null
    let falta = ''
    let vuelta = 0
    while (true) {
      const opciones = { label: ctx.et(`${t.id} · programa${vuelta ? ` (${vuelta + 1})` : ''}`), phase: 'Programar', schema: S_PROGRAMA }
      r = await agent(pProgramar(ctx, t, falta, vuelta, carril), rol ? modelo(opciones, rol) : opciones)
      if (!r) return { estado: 'bloqueada', motivo: 'el agente que programaba no terminó', archivosSinCommit: ['(revisar git status)'] }
      juntar(t.id, r)
      vuelta += 1
      if (r.estado !== 'parcial' || vuelta >= VUELTAS) break
      falta = r.falta
    }
    if (r.estado === 'parcial') return Object.assign({}, r, { motivo: `quedó a medias después de ${VUELTAS} agentes: ${r.falta}` })
    if (r.estado === 'sin-cupo' || r.estado === 'bloqueada') return r
    if (r.estado === 'hecha') return r

    // Si quedó «lista», con el código sin commitear, otro agente solo la commitea.
    const archivos = r.archivos.length ? r.archivos : r.archivosSinCommit
    const c = await agent(pCommitear(ctx, t, archivos, carril), { label: ctx.et(`${t.id} · commitea`), phase: 'Programar', schema: S_COMMIT, effort: 'low' })
    if (!c) return { estado: 'bloqueada', motivo: 'el agente que cerraba la tarea no terminó', archivosSinCommit: archivos }
    juntar(t.id, c)
    return Object.assign({}, c, { commits: (r.commits || []).concat(c.commits) })
  }

  // Un carril solo programa: los tests enteros corren una vez, en la fase 2.
  async function correrCarril(lista, carril) {
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
        const res = fin(t, await programarTarea(t, carril))
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

  if (MODO === 'programar' || MODO === 'todo') {
    await correrCarril(contrato, null)
    const kMal = contrato.map((k) => R[k.id]).find((r) => r && !['hecha', 'del-usuario'].includes(r.estado))
    if (kMal) for (const t of back.concat(front)) fin(t, { estado: 'sin-correr', motivo: `espera el contrato: ${kMal.id} quedó ${kMal.estado}` })
    if (carrilesBack.length > 1) log(ctx.et(`El back va en ${carrilesBack.length} carriles: ${carrilesBack.map((c) => `${c.nombre} (${c.tareas.map((t) => t.id).join(', ')})`).join('; ')}`))
    await parallel(carrilesBack.map((c) => () => correrCarril(c.tareas, c)).concat([() => correrCarril(front, null)]))
  }

  return {
    ctx,
    L,
    R,
    tareas,
    conRiesgo,
    carrilesBack,
    pasosAntes,
    detenido,
    PREGUNTAS,
    PASOS,
    PARA_DESPUES_DEL_PLAN,
    faltan,
    juntar,
    // Terminó su código: puede entrar en las fases 2 a 5.
    listo: !detenido && !faltan().length,
  }
}

/** Lo que el usuario tiene que hacer (no solo mirar) antes de que corra el plan siguiente. */
function pendientesDe(E) {
  return E.L.pasosDelUsuario.filter((p) => p.cuando === 'despues' && p.soloMirar !== true).map((p) => p.paso)
    .concat(E.tareas.filter((t) => E.R[t.id] && E.R[t.id].estado === 'del-usuario').map((t) => `${t.id}: ${t.titulo}`))
}

/** El contexto de los prompts de las fases 2 a 5: una etapa, o varias que se revisan y se cierran juntas. */
function contextoDeCierre(Es) {
  const claves = Es.map((E) => E.ctx.CLAVE)
  const una = claves.length === 1
  const titulos = {}
  for (const E of Es) for (const t of E.tareas) titulos[`${E.ctx.CLAVE}/${t.id}`] = t.titulo
  return {
    CLAVE: una ? claves[0] : '<clave>',
    CLAVES: claves,
    PLANES: Es.map((E) => E.ctx.PLAN),
    deLa: una ? `la etapa «${Es[0].ctx.ETAPA}» (plan ${Es[0].ctx.PLAN})` : `las etapas ${Es.map((E) => `«${E.ctx.ETAPA}» (plan ${E.ctx.PLAN})`).join(', ')}, que se revisan y se cierran juntas`,
    grep: claves.map((c) => `--grep "Tarea: ${c}/"`).join(' '),
    lineas: claves.map((c) => `«Tarea: ${c}/»`).join(' o '),
    commitDe: (fase) => (una ? `«Tarea: ${claves[0]}/${fase}»` : `«Tarea: <clave>/${fase}», con la clave de la etapa de cada arreglo (${claves.join(', ')})`),
    nombre: (ref) => {
      const r = String(ref).trim()
      const llave = r.includes('/') || !una ? r : `${claves[0]}/${r}`
      const id = una ? llave.replace(`${claves[0]}/`, '') : llave
      return titulos[llave] ? `${id} · ${titulos[llave]}` : id
    },
    // Las etiquetas de las fases 2 a 5 no llevan la clave de un plan: son del lanzamiento.
    et: (s) => s,
  }
}

// ---------- Fases 2 a 4: revisión en paralelo, arreglos y última revisión ----------

function revisar(C, p) {
  return agent(p.regla ? pRevisarRegla(C, p) : pRevisarPremisa(C, p), modelo({ label: C.et(`revisión · ${p.titulo}`), phase: 'Revisión', schema: S_PREMISA, effort: p.revisa }, 'revisa'))
}

function lineaDelRevisor(p, r) {
  return { id: p.id, titulo: p.titulo, hallazgos: r ? (r.hallazgos || []).length : null, resumen: r ? r.resumen : 'no terminó' }
}

/**
 * Pedido del usuario del 2026-11-27: los revisores de todas las fichas y premisas corren a la vez, con los tests
 * enteros; lo que encuentran va a un arreglador por repo, que comprueba cada hallazgo; y una última revisión lee el
 * diff entero. Corre una vez para todos los planes del lanzamiento, con el código de todos terminado.
 */
async function revisarEtapas(Es, usuarioListo) {
  const K = { corrio: false, esperaAlUsuario: false, pasosAntes: [], cerrable: false, porQueNoCierra: [], revision: null, tests: null, arreglos: [], ultima: null, resumen: '' }
  if (!Es.length) return K
  if (!(MODO === 'cerrar' || MODO === 'todo')) { K.porQueNoCierra.push('se lanzó solo para programar'); return K }
  K.pasosAntes = Es.flatMap((E) => E.pasosAntes)
  K.esperaAlUsuario = K.pasosAntes.length > 0 && !usuarioListo
  const sinTerminar = Es.filter((E) => !E.listo)
  if (sinTerminar.length) { K.porQueNoCierra.push(`faltan tareas de ${sinTerminar.map((E) => E.ctx.CLAVE).join(', ')}`); return K }
  if (K.esperaAlUsuario) {
    log(`La revisión espera al usuario: ${K.pasosAntes.join('; ')}. Relanzá con modo cerrar y usuarioListo: true.`)
    K.porQueNoCierra.push(`espera al usuario: ${K.pasosAntes.join('; ')}`)
    return K
  }
  K.corrio = true
  const C = contextoDeCierre(Es)
  // Lo que traen los arregladores y la última revisión (cambios al contrato, preguntas, para después) va al último plan.
  const ultimo = Es[Es.length - 1]

  // ---------- Fase 2: todo a la vez ----------
  phase('Revisión')
  const [porArchivos, transversales, tests] = await parallel([
    async () => {
      const A = await agent(pArchivos(C), { label: C.et('archivos de la etapa'), phase: 'Revisión', schema: S_ARCHIVOS, effort: 'low' })
      const archivos = A ? A.archivos.map((x) => Object.assign({}, x, { ruta: String(x.ruta).replace(/\\/g, '/').replace(/^\.\//, '') })) : null
      const reglas = archivos ? reglasDeLaEtapa(archivos) : null
      if (!reglas) log('El agente que lista los archivos de la etapa no terminó: no corre la revisión por ficha, y los temas de riesgo son los que marca el plan')
      else {
        log(`Revisión por ficha: ${reglas.conRevisor.map((p) => `${p.regla.repo}/${p.regla.ficha}`).join(', ') || 'ninguna'}`)
        if (reglas.controladas.length) log(`Sin revisor, porque un test la controla entera: ${reglas.controladas.join('; ')}`)
        if (reglas.sinArchivos.length) log(`Sin revisor, porque la etapa no tocó sus archivos: ${reglas.sinArchivos.join(', ')}`)
      }
      const adversariales = premisasDeRiesgo(archivos ? temasDeLaEtapa(archivos) : temasDelPlan(Es), C.nombre)
      log(adversariales.length ? `Revisión adversarial: ${adversariales.map((p) => p.tema).join(', ')}` : 'Los archivos de la etapa no tocan ningún tema de riesgo: no va revisión adversarial')
      const ps = (reglas ? reglas.conRevisor : []).concat(adversariales)
      const rs = await parallel(ps.map((p) => () => revisar(C, p)))
      return {
        reparto: {
          origen: archivos ? 'archivos' : 'plan',
          archivos: archivos ? archivos.length : null,
          fichas: reglas ? { conRevisor: reglas.conRevisor.map((p) => `${p.regla.repo}/${p.regla.ficha}`), controladas: reglas.controladas, sinArchivos: reglas.sinArchivos } : null,
          temasDeRiesgo: adversariales.map((p) => p.tema),
        },
        listado: Boolean(reglas),
        ps,
        rs,
      }
    },
    () => parallel(TRANSVERSALES.map((p) => () => revisar(C, p))),
    () => agent(pTests(C), { label: C.et('tests enteros'), phase: 'Revisión', schema: S_TESTS }),
  ])

  const revisores = TRANSVERSALES.map((p, i) => ({ p, r: transversales ? transversales[i] : null }))
    .concat(porArchivos ? porArchivos.ps.map((p, i) => ({ p, r: porArchivos.rs[i] })) : [])
  K.tests = tests ? { tabla: tests.tabla, resumen: tests.resumen } : null
  const deTodos = revisores.flatMap(({ p, r }) => ((r && r.hallazgos) || []).map((h) => Object.assign({}, h, { de: p.titulo })))
    .concat(((tests && tests.hallazgos) || []).map((h) => Object.assign({}, h, { de: 'Tests enteros' })))

  // Los repetidos (mismo repo, archivo y línea) se juntan: queda el primero, con lo que dijeron los demás.
  const hallazgos = []
  const vistos = {}
  let juntados = 0
  for (const h of deTodos) {
    const llave = `${h.repo}|${h.archivo}|${h.linea}`
    if (vistos[llave]) { vistos[llave].regla = `${vistos[llave].regla}; también ${h.de}: ${h.titulo}`; juntados += 1 } else hallazgos.push((vistos[llave] = h))
  }
  if (juntados) log(`Se juntaron ${juntados} hallazgos que repetían archivo y línea`)
  const sinRevisar = revisores.filter(({ r }) => !r).map(({ p }) => p.titulo)
  if (!porArchivos || !porArchivos.listado) sinRevisar.push('Revisión por ficha (no se supo qué archivos cambió la etapa)')
  if (!tests) sinRevisar.push('Tests enteros')
  K.revision = {
    reparto: porArchivos ? porArchivos.reparto : null,
    revisores: revisores.map(({ p, r }) => lineaDelRevisor(p, r)),
    tests: K.tests,
    hallazgos: hallazgos.length,
    juntados,
    sinTerminar: sinRevisar,
  }

  // ---------- Fase 3: un arreglador por repo, que comprueba cada hallazgo ----------
  const aArreglar = porRepo(hallazgos, 'repo')
  const repos = Object.keys(aArreglar)
  if (repos.length) {
    phase('Arreglos')
    const hechos = await parallel(repos.map((repo) => () =>
      agent(pArreglar(C, repo, aArreglar[repo]), { label: C.et(`arreglos · ${repo}`), phase: 'Arreglos', schema: S_COMMIT })))
    K.arreglos = repos.map((repo, i) => {
      const c = hechos[i]
      if (c) ultimo.juntar('revision', c)
      return c
        ? { repo, hallazgos: aArreglar[repo].length, estado: c.estado, arreglados: c.arreglados, descartados: c.descartados, commits: c.commits, motivo: c.motivo }
        : { repo, hallazgos: aArreglar[repo].length, estado: 'no terminó', arreglados: [], descartados: [], commits: [], motivo: '' }
    })
  }
  const resumenDeArreglos = K.arreglos.length
    ? K.arreglos.map((a) => `${a.repo}: ${a.arreglados.length} arreglados y ${a.descartados.length} descartados de ${a.hallazgos} (${a.estado})${a.commits.length ? `, commits ${a.commits.join(', ')}` : ''}`).join('; ')
    : 'no hubo hallazgos'

  // ---------- Fase 4: la última revisión del diff entero ----------
  phase('Última revisión')
  const u = await agent(pUltima(C, resumenDeArreglos), { label: C.et('última revisión'), phase: 'Última revisión', schema: S_ULTIMA, effort: 'high' })
  if (u) ultimo.juntar('ultima-revision', { paraDespues: u.alBacklog })
  K.ultima = u ? { estado: u.estado, arreglados: u.arreglados, alBacklog: u.alBacklog, commits: u.commits, motivo: u.motivo } : null

  K.resumen = `${revisores.length} revisores y los tests enteros, ${hallazgos.length} hallazgos${juntados ? ` (${juntados} repetidos juntados)` : ''}; arreglos: ${resumenDeArreglos}; la última revisión: ${u ? `${u.arreglados.length} arreglados y ${u.alBacklog.length} al backlog (${u.estado})` : 'no terminó'}`
  if (sinRevisar.length) K.porQueNoCierra.push(`no terminaron: ${sinRevisar.join(', ')}`)
  for (const a of K.arreglos) if (a.estado !== 'hecha') K.porQueNoCierra.push(`los arreglos del ${a.repo} quedaron ${a.estado}${a.motivo ? `: ${a.motivo}` : ''}`)
  if (!u) K.porQueNoCierra.push('la última revisión no terminó')
  else if (u.estado !== 'hecha') K.porQueNoCierra.push(`la última revisión quedó ${u.estado}${u.motivo ? `: ${u.motivo}` : ''}`)
  const preguntas = Es.reduce((n, E) => n + E.PREGUNTAS.length, 0)
  if (preguntas) K.porQueNoCierra.push(`preguntas abiertas para el usuario: ${preguntas}`)
  K.cerrable = !K.porQueNoCierra.length
  return K
}

// ---------- Fase 5: el cierre y el commit ----------

async function cerrarPlanes(Es, K) {
  if (!Es.length || (!K.corrio && !Es.some((E) => E.ctx.corrieron))) return null
  phase('Cierre')
  const c = await agent(pCierre(Es, K), { label: 'cierre', phase: 'Cierre', schema: S_CIERRE, effort: 'medium' })
  if (K.cerrable && c) log(`Código listo: ${(c.listas || []).join(', ') || 'ninguna'}. Falta la verificación de cada una, con verificar-etapa.js`)
  return c
}

function resultadoDe(E, K, cierre) {
  const queFalta = E.faltan().map((t) => `${t.id}: ${E.R[t.id] ? `${E.R[t.id].estado}, ${E.R[t.id].motivo}` : 'sin correr'}`)
  if (queFalta.length) return { resultado: 'faltan tareas', queFalta }
  if (MODO === 'programar') return { resultado: 'programada', queFalta }
  if (K.esperaAlUsuario) return { resultado: 'espera al usuario antes de revisar', queFalta }
  // Código listo solo si el cierre lo dice: la revisión en orden no alcanza si el agente del cierre no terminó.
  if (K.cerrable) return { resultado: cierre && (cierre.listas || []).includes(E.ctx.CLAVE) ? CODIGO_LISTO : 'revisada, sin cerrar', queFalta }
  return { resultado: K.corrio ? 'revisada, sin cerrar' : 'sin revisar', queFalta }
}

function planParaDevolver(E, K, cierre) {
  const { resultado, queFalta } = resultadoDe(E, K, cierre)
  return {
    etapa: E.L.etapa,
    clave: E.ctx.CLAVE,
    modo: MODO,
    resultado,
    verificacion: resultado === CODIGO_LISTO ? `falta: Workflow({ scriptPath: '.agents/workflows/verificar-etapa.js', args: { clave: '${E.ctx.CLAVE}' } })` : '',
    tareas: E.tareas.filter((t) => t.carril !== 'integracion').map((t) => {
      const r = E.R[t.id] || { estado: t.estado, motivo: t.motivo, commits: [] }
      return { id: t.id, estado: r.estado, motivo: r.motivo, commits: r.commits || [], verificacion: r.verificacion || '' }
    }),
    faltan: queFalta,
    detenido: E.detenido,
    preguntas: E.PREGUNTAS,
    pasosDelUsuario: E.PASOS,
    pasosAntesDeRevisar: E.pasosAntes,
    pendientesDelUsuario: pendientesDe(E),
    paraDespues: E.PARA_DESPUES_DEL_PLAN,
    cambiosAlContrato: E.ctx.CAMBIOS,
    carrilesBack: E.carrilesBack.map((c) => ({ nombre: c.nombre, tareas: c.tareas.map((t) => t.id) })),
  }
}

function cierreParaDevolver(K, cierre) {
  return { revision: K.revision, arreglos: K.arreglos, ultimaRevision: K.ultima, pendiente: K.porQueNoCierra, cierre, tokensDeSalida: PRESUPUESTO ? PRESUPUESTO.spent() : null }
}

// ---------- Un plan solo ----------

if (!CADENA) {
  const E = await programarPlan(PLANES[0], null)
  if (E.fin) return E.fin
  const K = await revisarEtapas([E], USUARIO_LISTO)
  const cierre = await cerrarPlanes([E], K)
  return Object.assign(planParaDevolver(E, K, cierre), cierreParaDevolver(K, cierre))
}

// ---------- Varios planes: la fase 1 de cada uno, en orden, y de la 2 a la 5 una vez, juntas ----------

/** El plan siguiente se programa solo si este terminó su código sin nada pendiente para el usuario. */
function porQueNoSigue(E) {
  if (E.fin) return [`no empezó: ${(E.fin.bloqueos || []).join('; ')}`]
  const motivos = []
  const faltan = E.faltan()
  if (E.detenido) motivos.push(`se detuvo: ${E.detenido}`)
  else if (faltan.length) motivos.push(`faltan tareas: ${faltan.map((t) => `${t.id} (${E.R[t.id] ? E.R[t.id].estado : 'sin correr'})`).join(', ')}`)
  if (E.PREGUNTAS.length) motivos.push(`preguntas abiertas para el usuario: ${E.PREGUNTAS.length}`)
  const pendientes = pendientesDe(E)
  if (pendientes.length) motivos.push(`el usuario tiene que: ${pendientes.join('; ')}`)
  return motivos
}

const corridos = []
let frenoEn = null
for (let i = 0; i < PLANES.length; i++) {
  let E
  try {
    E = await programarPlan(PLANES[i], i ? PLANES[i - 1] : null)
  } catch (e) {
    // Lo de los planes anteriores no se pierde: este cuenta como que no empezó, con el motivo.
    E = { fin: { clave: claveDe(PLANES[i]), resultado: 'no empezó', bloqueos: [e && e.message ? e.message : String(e)] } }
  }
  corridos.push(E)
  if (MODO === 'planificar') continue
  const motivos = porQueNoSigue(E)
  if (!motivos.length && i < PLANES.length - 1 && PRESUPUESTO && PRESUPUESTO.total && PRESUPUESTO.remaining() < MINIMO_DE_PRESUPUESTO) motivos.push('se acabó el presupuesto de la corrida')
  if (motivos.length) {
    frenoEn = { plan: PLANES[i], clave: claveDe(PLANES[i]), porque: motivos }
    log(`Frena en ${claveDe(PLANES[i])}: ${motivos.join(' · ')}`)
    break
  }
  if (i < PLANES.length - 1) log(`${claveDe(PLANES[i])} terminó su código: sigue ${claveDe(PLANES[i + 1])}. La revisión, los arreglos y el cierre van una vez, al final.`)
}

if (MODO === 'planificar') {
  const planes = corridos.map((E) => E.fin)
  const temas = new Set(planes.flatMap((p) => p.temasDeRiesgo || []))
  const reglas = new Set(planes.flatMap((p) => p.reglasPrevistas || []))
  return {
    modo: MODO,
    resultado: 'planificados',
    planes,
    frenoEn: null,
    sinCorrer: [],
    agentesEstimados: planes.reduce((n, p) => n + (p.agentesEstimados || 0), 0) + agentesDelCierre(reglas.size, temas.size),
  }
}

// Las fases 2 a 5 corren una vez, sobre los planes que se programaron, si todos terminaron su código.
const programados = corridos.filter((E) => !E.fin)
const K = await revisarEtapas(programados, USUARIO_LISTO)
const cierre = await cerrarPlanes(programados, K)
const planes = corridos.map((E) => E.fin || planParaDevolver(E, K, cierre))

let resultadoDeLaCadena = planes.every((p) => p.resultado === CODIGO_LISTO) ? CODIGO_LISTO : 'revisadas, sin cerrar'
if (frenoEn) resultadoDeLaCadena = 'frenó'
else if (MODO === 'programar') resultadoDeLaCadena = 'programadas'
else if (K.esperaAlUsuario) resultadoDeLaCadena = 'espera al usuario antes de revisar'
else if (!K.corrio) resultadoDeLaCadena = 'sin revisar'

return Object.assign({ modo: MODO, resultado: resultadoDeLaCadena, planes, frenoEn, sinCorrer: PLANES.slice(corridos.length) }, cierreParaDevolver(K, cierre))
