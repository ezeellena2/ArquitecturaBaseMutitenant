export const meta = {
  name: 'etapa',
  description: 'Programa una etapa desde su plan ejecutable: contrato, back y front a la vez con un escritor por repo, revisión adversarial según el riesgo, revisión del cierre y una sola puerta',
  whenToUse: 'Cuando el usuario pide programar, retomar o cerrar una etapa con plan ejecutable (docs/plans/AAAA-MM-DD-etapa-*.md). args: { plan, modo?: planificar|programar|cerrar|todo, solo?: [ids], usuarioListo?: true }. Guía: docs/guides/conducir-una-etapa.md',
  phases: [
    { title: 'Leer', detail: 'el plan, lo ya commiteado y lo que bloquea' },
    { title: 'Contrato', detail: 'las tareas K, en el back' },
    { title: 'Programar', detail: 'back y front a la vez, un escritor por repo' },
    { title: 'Integrar', detail: 'el front contra el contrato real' },
    { title: 'Revisión', detail: 'la lista de control del cierre, una vez por repo' },
    { title: 'Puerta', detail: 'suites, E2E, detectores, galería y recorrido, una vez' },
    { title: 'Cierre', detail: 'las tablas del plan y lo que queda para el usuario' },
  ],
}

// Se corre con Workflow({ scriptPath: '.agents/workflows/etapa.js', args: { plan } }), sin copiarlo a .claude/.
// La guía es docs/guides/conducir-una-etapa.md y los tests, scripts/arneses/conductor.test.mjs (con agentes falsos).

const A = typeof args === 'string' ? { plan: args } : (args || {})
if (!A.plan) throw new Error('Falta args.plan: la ruta del plan ejecutable, por ejemplo docs/plans/2026-11-19-etapa-base-de-la-12-segunda-parte.md')
const PLAN = String(A.plan).replace(/\\/g, '/')
const MODO = A.modo || 'todo'
if (!['planificar', 'programar', 'cerrar', 'todo'].includes(MODO)) throw new Error(`Modo desconocido: ${MODO}. Usá planificar, programar, cerrar o todo.`)
const SOLO = Array.isArray(A.solo) && A.solo.length ? A.solo : null
const USUARIO_LISTO = A.usuarioListo === true
const CLAVE = PLAN.split('/').pop().replace(/\.md$/, '').replace(/^\d{4}-\d{2}-\d{2}-etapa-/, '')
const FRONT = '../ArquitecturaBaseMutitenantFront'
const TOPE_CUPO = 85
const VUELTAS = 3
const PRESUPUESTO = typeof budget === 'undefined' ? null : budget

// ---------- Textos que reciben todos los agentes ----------

const COMUNES = `Reglas de esta corrida, además de AGENTS.md:
- Trabajás en main, en el checkout de siempre: el back es la carpeta actual y el front es ${FRONT}. Sin ramas, worktrees, stash, reset, cambio de rama ni push. Puede haber otras sesiones trabajando: tocá y commiteá solo tus rutas, y no reviertas nada que no hiciste. Los archivos nuevos van con git add antes; el commit, siempre con git commit -m "<tipo>(<área>): <qué>" -m "Tarea: ${CLAVE}/<id>" -- <rutas>.
- Ninguna corrida contra un proveedor real (Meta, IA, Gmail): fakes y clientes falsos. Nunca leas, copies ni muestres un secreto: de user-secrets, solo los nombres.
- No toques el lienzo (${FRONT}/docs/design/lienzo) ni dibujes pantallas. Si a un tablero le falta un estado, decilo en motivo y seguí con el resto.
- Un test que se pasó de tiempo se corre una vez más, solo. Si así pasa, era la carga de la PC: puede haber otro agente corriendo la suite del otro repo.
- Un proceso se corta por PID, nunca con un filtro amplio. Si levantaste Aspire, aspire stop al terminar.
- Una decisión que cambia el producto (qué ve o puede hacer una persona, una pantalla, el modelo de accesos) no la tomás: devolvé estado bloqueada con la pregunta, un ejemplo real y la opción que recomendás primero. Lo técnico lo decidís vos y lo contás en motivo.`

const CUPO = `Antes de tocar nada: si tenés la herramienta mcp__ccd_session_mgmt__get_usage (buscala con ToolSearch), mirá el límite semanal de todos los modelos. Con ${TOPE_CUPO} % o más no empieces: devolvé estado sin-cupo.`

const ESCENARIOS = {
  datos: '- Datos: la migración es portable (el SQL en una línea y sin \\r), su Down se niega antes de perder datos y convive con la versión anterior (se agrega antes de borrar; un valor nuevo de un enum, según lo que anote el plan). Probá con filas guardadas antes del cambio. Guardar sin cambios escribe solo la fila de la entidad. En un lote, la fila que falla se registra y las demás siguen. Si hay cifrado: otra organización, una clave que no abre y un contenido ilegible.',
  permisos: '- Permisos: a quien le falta un permiso recibe 403 y no ve nada del recurso, ni en un 409, un aviso o una vista previa. Nadie da lo que no tiene, el último administrador no se pierde, y lo que la pantalla ofrece sigue la condición del servidor.',
  efectos: '- Efectos fuera del sistema: cada respuesta del proveedor se clasifica como definitiva, transitoria o desconocida, y cada una tiene su salida y su test. Lo dudoso no se repite, un reintento no duplica, lo que cuesta se mide antes de pagar y cada tope sale de una sola constante. Todo con fakes, sin ninguna llamada real.',
  aislamiento: '- Aislamiento: un id de otra organización da 404. Los trabajos de fondo entran con su organización (TenantJobRunner o ITenantScope, nunca con una transacción abierta), y la caché y las cuotas llevan su alcance. Probá RLS con dos organizaciones y dos copias de la app a la vez: el lease y lo que corre una sola vez.',
}

// ---------- Esquemas ----------

const TEXTO = { type: 'string' }
const LISTA = { type: 'array', items: { type: 'string' } }

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
      items: { type: 'object', properties: { paso: TEXTO, cuando: { type: 'string', enum: ['antes-de-la-puerta', 'despues'] } }, required: ['paso', 'cuando'] },
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
    cambiosAlContrato: LISTA,
    verificacion: TEXTO,
  },
  required: ['estado', 'commits', 'archivos', 'archivosSinCommit', 'falta', 'motivo', 'pregunta', 'cambiosAlContrato', 'verificacion'],
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
    archivosSinCommit: LISTA,
    motivo: TEXTO,
    verificacion: TEXTO,
  },
  required: ['estado', 'commits', 'diferencias', 'pasosDelUsuario', 'cambiosAlContrato', 'archivosSinCommit', 'motivo', 'verificacion'],
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

function pLeer() {
  return `Sos el primer paso del conductor que programa una etapa. Solo leés y corrés comandos de lectura (git log, git status, grep, listar procesos): no cambiás nada.

El plan ejecutable es ${PLAN}. La clave de sus commits es «${CLAVE}»: cada commit del conductor lleva la línea «Tarea: ${CLAVE}/<id>». Leé el plan entero y, de la spec que nombra, lo necesario para entender cada tarea y su orden. El front está en ${FRONT}.

Devolvé:
1. etapa: el nombre de la etapa, como lo dice el título del plan.
2. bloqueos: lo que impide empezar a programar hoy, uno por línea, o la lista vacía:
   - el plan dice que va después de otra etapa o de su puerta, y esa no cerró según la hoja de ruta (docs/plans/2026-09-27-plan-de-desarrollo.md) o su propio plan;
   - una decisión que el plan deja abierta en «Para decidir» y nadie tomó;
   - la app levantada: un proceso de Aspire (aspire o dcp) o de Vite (node con vite en su línea de comando);
   - cambios sin commitear en los archivos que la etapa va a tocar (git status de los dos repos). Los de otra área no bloquean: van en notas.
3. cupoSemanal: si tenés la herramienta mcp__ccd_session_mgmt__get_usage (buscala con ToolSearch), el porcentaje usado del límite semanal de todos los modelos. Si no la tenés, null.
4. tareas: cada K, B, F e I del plan, en el orden en que el conductor las va a correr.
   - carril: contrato (K), back (B), front (F) o integracion (I). subcarril: el nombre del carril en el plan (por ejemplo «Back A»), o el carril.
   - El conductor tiene un solo escritor por repo, así que las del back van en UN solo orden. Si el plan tiene dos carriles del back, intercalalos respetando cada regla de orden que el plan escribe (por ejemplo, lo que espera el commit de una migración de otra tarea).
   - riesgo: las categorías de lo que el plan marca con (R): datos (migraciones, persistencia o cifrado), permisos, efectos (Meta, correo, archivos o IA) y aislamiento (RLS, ITenantScope, trabajos de fondo, caché y cuotas por alcance). Si el plan no marca ninguna tarea con (R), decidilo vos con esas categorías, por lo que toca cada una.
   - espera: los ids que tienen que estar commiteados antes, de otro carril, o del mismo si esta tarea los usa. El contrato va entero antes del back y del front: no lo pongas.
   - estado: «hecha» solo si en su repo hay commits con «Tarea: ${CLAVE}/<id>» y lo que pide está en el código (mirá sus archivos principales). «del-usuario» si la hace el usuario (cargar secretos, probar con Meta, aprobar plantillas). «bloqueada» si espera algo de afuera que no está: un tablero que no existe en ${FRONT}/docs/design/lienzo/ (buscalo por su nombre) o una decisión sin tomar. Si no, «pendiente».
   - motivo: por qué tiene ese estado, en una línea (sus commits, si está hecha).
   - texto: lo que el plan dice de la tarea, literal.
5. textos: los textos de chat del plan, literales: contrato, cada carril del back con su nombre, front e integración. Si falta uno, cadena vacía.
6. pasosDelUsuario: lo que el plan deja en manos del usuario (cargar secretos, pruebas con Meta, aprobar plantillas, mirar la galería, la prueba integral). En cada uno, cuando: «antes-de-la-puerta» si la puerta lo necesita, o «despues».
7. notas: lo que conviene que sepa quien lanza el conductor, en pocas líneas.`
}

function repoDe(t) {
  return t.carril === 'front' ? FRONT : 'la carpeta actual (el back)'
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
    ? `\nOtro agente empezó esta tarea y paró a mitad (vas por la vuelta ${vuelta + 1}). Lo que dejó dicho: ${falta}. Mirá git status y git log --grep "Tarea: ${CLAVE}/${t.id}" en su repo para ver dónde quedó, y seguí desde ahí.\n`
    : ''
  const cierre = revisa
    ? `Esta tarea lleva revisión adversarial (${t.riesgo.join(', ')}): NO commitees. Dejá todo en el árbol, en verde, y devolvé estado «lista» con la lista completa de archivos que tocaste o creaste.`
    : `Commiteá cada parte coherente y verificada con la línea «Tarea: ${CLAVE}/${t.id}», y devolvé estado «hecha» con sus commits.`
  return `Sos el agente que programa la tarea ${t.id} de la etapa «${ctx.ETAPA}» (plan: ${PLAN}).
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

${COMUNES}`
}

function pAdversarial(ctx, t, archivos) {
  return `Sos un revisor adversarial. Otro agente programó la tarea ${t.id} de la etapa «${ctx.ETAPA}» (plan ${PLAN}) y dejó el código sin commitear. No sabés qué concluyó, y no importa: tu trabajo es romperla.

La tarea: ${t.titulo}.
"""
${t.texto}
"""
Sus pasos y tests están en la spec que nombra el plan. Los archivos que tocó, en ${repoDe(t)}, son: ${archivos.join(', ')}. Leelos con git diff y git status de ese repo.

Riesgo: ${t.riesgo.join(', ')}. Probá al menos estos escenarios, y los que se te ocurran:
${t.riesgo.map((c) => ESCENARIOS[c]).join('\n')}

Por cada problema real, escribí un test que falle, dejalo en el árbol sin commitear y nombralo en «prueba». No arregles el código de producción ni commitees. Borrá los tests que escribiste y pasan. En «punto» va la categoría del riesgo. Si no encontrás nada, devolvé la lista vacía: no inventes.

${COMUNES}`
}

function lineaDeHallazgo(h) {
  return `- [${h.gravedad}] ${h.titulo} (${h.archivo}:${h.linea}). Escenario: ${h.escenario}.${h.prueba ? ` Prueba: ${h.prueba}.` : ''}`
}

function pCorregir(ctx, t, archivos, hallazgos) {
  const cuerpo = hallazgos.length
    ? `Hallazgos de la revisión adversarial:
${hallazgos.map(lineaDeHallazgo).join('\n')}

Con cada uno, corré su prueba. Si es real, arreglalo, dejá el test y buscá el mismo defecto en los caminos hermanos y en las piezas compartidas. Si no es real, borrá su test y contá por qué en descartados. Después corré el build y los tests rápidos enteros del repo, sin Docker y sin filtrar. Si la tarea tocó rutas, persistencia, autenticación o aislamiento, corré también los de integración y seguridad del área.`
    : 'No hay hallazgos que corregir: quien la programó ya corrió las suites. Confirmá con git status que los archivos de la tarea están ahí, sin cambios ajenos mezclados, y no vuelvas a correrlas.'
  return `Sos el agente que commitea la tarea ${t.id} de la etapa «${ctx.ETAPA}» (plan ${PLAN}). No la programaste ni la revisaste.

La tarea: ${t.titulo}. Sus archivos, en ${repoDe(t)}: ${archivos.join(', ')}.

${cuerpo}

Commiteá todo lo de la tarea, los archivos de arriba más los tests que quedaron, con la línea «Tarea: ${CLAVE}/${t.id}», y devolvé estado hecha con los commits. Si no queda en verde, no commitees: devolvé bloqueada, con el motivo y archivosSinCommit.

${COMUNES}`
}

function pIntegrar(ctx) {
  return `Sos el integrador de la etapa «${ctx.ETAPA}» (plan ${PLAN}). El contrato, el back y el front ya están commiteados.

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
- Cada diferencia real se arregla en el repo de su dueño, con su test. Corré solo lo que el arreglo toca, y commiteá con la línea «Tarea: ${CLAVE}/integracion».
- La puerta no la corrés vos: la corre el conductor después.
- Los pasos que el plan le deja al usuario en la integración (por ejemplo, cargar sus user-secrets) van en pasosDelUsuario, comando por comando y sin ningún valor: los valores los pega él.

${COMUNES}`
}

function pRevisar(ctx, repo) {
  const donde = repo === 'back' ? 'el back (la carpeta actual)' : `el front (${FRONT})`
  return `Sos el revisor del cierre de la etapa «${ctx.ETAPA}» (plan ${PLAN}) en ${donde}. Solo leés: no cambiás nada.

Los commits de la etapa son los que llevan la línea «Tarea: ${CLAVE}/». Listalos con git log --grep "Tarea: ${CLAVE}/" --format="%H %s" en ese repo y leé sus cambios.

Revisalos contra la «Lista de control» de .agents/skills/puerta/SKILL.md, que tiene 20 puntos. Cada punto que aplica a la etapa necesita su test, o una comprobación contra el tablero o el código, y tiene que pasar. Mirá también que ningún commit de la etapa haya deshecho lo de otro: lo de cada tarea sigue en el código de HEAD.

Devolvé solo problemas reales, con archivo y línea. En «punto» va el número de la lista o «pisados». Si un punto no aplica, no lo nombres. No inventes: una lista vacía es un resultado válido.`
}

function pCorregirCierre(ctx, repo, hallazgos) {
  const donde = repo === 'back' ? 'el back (la carpeta actual)' : `el front (${FRONT})`
  return `Sos el agente que corrige lo que encontró la revisión del cierre de la etapa «${ctx.ETAPA}» (plan ${PLAN}) en ${donde}. No programaste la etapa ni la revisaste.

Hallazgos:
${hallazgos.map(lineaDeHallazgo).join('\n')}

Con cada uno, confirmá que es real. Si lo es, arreglalo con un test que falle sin el arreglo, y buscá el mismo defecto en los caminos hermanos. Si no lo es, contá por qué en descartados. Corré lo que tocan los arreglos: el test, los de su área y, si cambiaste autenticación, persistencia base o la composición de la app, los tests rápidos enteros del repo. Commiteá con la línea «Tarea: ${CLAVE}/revision».

${COMUNES}`
}

function pPuerta(ctx, revision) {
  return `Corré la puerta de la etapa «${ctx.ETAPA}» (plan ${PLAN}) según la skill .agents/skills/puerta/SKILL.md, sobre copias del corte (el HEAD de main de cada repo), sin worktrees ni ramas.
- La lista de control, que es la revisión de código del cierre, ya la hizo otra fase: ${revision}. No la repitas: ponela en la tabla con ese resultado.
- La galería la mira el usuario. Generala y devolvé su ruta en galeria.
- No arregles nada ni borres las copias: devolvé sus rutas, porque los arreglos se traen a esas mismas copias. Al terminar, aspire stop.
- Ninguna corrida contra un proveedor real. Si un paso lo necesita, no lo corras y decilo en su fila.
- Cada fallo va con archivo, línea, la causa en una línea y su dueño (back o front). Antes de contarlo, descartá el entorno, como dice la skill.

${COMUNES}`
}

function pArreglarPuerta(ctx, repo, fallos) {
  const donde = repo === 'back' ? 'el back (la carpeta actual)' : `el front (${FRONT})`
  return `Sos el dueño de estos fallos de la puerta de la etapa «${ctx.ETAPA}» (plan ${PLAN}) en ${donde}. No programaste la etapa.

${fallos.map((f) => `- ${f.paso}: ${f.archivo}:${f.linea}. ${f.causa}`).join('\n')}

Arreglá cada uno en el checkout de siempre, no en la copia de la puerta, con su test, y buscá el mismo defecto en los caminos hermanos. Corré solo lo que toca el arreglo. Si un fallo era del entorno (un tiempo vencido que pasa al correrlo solo), contalo en descartados. Commiteá con la línea «Tarea: ${CLAVE}/puerta».

${COMUNES}`
}

function pRepetir(ctx, P, arreglos) {
  const commits = arreglos.flatMap((a) => (a && a.commits) || [])
  return `Los arreglos de la puerta de la etapa «${ctx.ETAPA}» (plan ${PLAN}) ya están commiteados en main: ${commits.join(', ') || 'ninguno'}.

Traelos a las copias de la puerta (back: ${P.copias.back}; front: ${P.copias.front}) con git -C <copia> fetch y checkout --detach del commit nuevo. Corré solo los pasos que esos arreglos tocan, según la skill .agents/skills/puerta/SKILL.md. La suite completa de un repo se repite solo si un arreglo tocó autenticación, persistencia base o la composición de la app.

Devolvé la tabla entera. Las filas que no corriste quedan como estaban:
${JSON.stringify(P.tabla)}
Al terminar, aspire stop y borrá las dos copias.

${COMUNES}`
}

function pCierre(ctx, resumen, puerta, copias) {
  return `Sos el que cierra la corrida del conductor en el plan ${PLAN}. Cambiás solo ese archivo y no agregás historial ni actas.
- En la tabla de estado del principio (Parte | Tareas | Estado), cada parte dice «Hecha» si todas sus tareas están hechas; si no, «Pendiente: <ids y motivo corto>».
- ${puerta ? `En la tabla de la puerta, el resultado de cada paso: ${JSON.stringify(puerta)}.` : 'La tabla de la puerta no se toca: la puerta no corrió.'}
- En «Cambios al contrato», una línea por cambio de esta corrida que todavía no esté: ${ctx.CAMBIOS.length ? ctx.CAMBIOS.join('; ') : 'ninguno'}.
Commiteá solo el plan, con git commit -m "docs(planes): ${ctx.ETAPA}, el estado después del conductor" -m "Tarea: ${CLAVE}/cierre" -- ${PLAN}. Si el plan no cambió, no commitees y devolvé commit vacío.
${copias ? `Después, si quedan las copias de la puerta (${copias}), corré aspire stop si algo quedó levantado y borralas.` : ''}

El estado de cada tarea:
${resumen.map((r) => `- ${r.id}: ${r.estado}${r.motivo ? `, ${r.motivo}` : ''}`).join('\n')}

${COMUNES}`
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

// ---------- Leer ----------

phase('Leer')
const L = await agent(pLeer(), { label: 'leer el plan', phase: 'Leer', schema: S_LEER })
if (!L) throw new Error('El agente que lee el plan no terminó')

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
const agentesEstimados = 1 + pendientes.reduce((n, t) => n + (t.riesgo.length ? 3 : 1), 0) + (MODO === 'programar' ? 2 : 8)
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
    agentesEstimados,
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

const ctx = {
  ETAPA: L.etapa,
  TEXTOS: L.textos,
  INTEGRACION: tareas.filter((t) => t.carril === 'integracion'),
  CAMBIOS: [],
  corrieron: 0,
  hechas() {
    const h = Object.values(R).filter((r) => r.estado === 'hecha').map((r) => (r.commits[0] ? `${r.id} (${r.commits[0]})` : r.id))
    return h.length ? h.join(', ') : 'ninguna'
  },
}
const PREGUNTAS = []
const PASOS = L.pasosDelUsuario.map((p) => p.paso)
let detenido = null

function anotarCambio(c, quien) {
  const linea = `${c} (${quien})`
  if (!ctx.CAMBIOS.includes(linea)) ctx.CAMBIOS.push(linea)
}

function juntar(t, r) {
  for (const c of r.cambiosAlContrato || []) anotarCambio(c, t.id)
  if (r.pregunta && !PREGUNTAS.some((p) => p.tarea === t.id && p.pregunta === r.pregunta)) PREGUNTAS.push({ tarea: t.id, pregunta: r.pregunta })
}

async function programarTarea(t, fase) {
  const revisa = t.riesgo.length > 0
  let r = null
  let falta = ''
  let vuelta = 0
  while (true) {
    r = await agent(pProgramar(ctx, t, revisa, falta, vuelta), { label: `${t.id} · programa${vuelta ? ` (${vuelta + 1})` : ''}`, phase: fase, schema: S_PROGRAMA })
    if (!r) return { estado: 'bloqueada', motivo: 'el agente que programaba no terminó', archivosSinCommit: ['(revisar git status)'] }
    juntar(t, r)
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
    let rev = await agent(pAdversarial(ctx, t, archivos), { label: `${t.id} · revisión adversarial`, phase: fase, schema: S_HALLAZGOS })
    if (!rev) rev = await agent(pAdversarial(ctx, t, archivos), { label: `${t.id} · revisión adversarial (2)`, phase: fase, schema: S_HALLAZGOS })
    if (!rev) return { estado: 'bloqueada', motivo: 'la revisión adversarial no terminó: el código quedó sin commitear', archivosSinCommit: archivos }
    hallazgos = rev.hallazgos || []
  }
  const opciones = { label: `${t.id} · ${hallazgos.length ? `corrige ${hallazgos.length} y commitea` : 'commitea'}`, phase: fase, schema: S_COMMIT }
  if (!hallazgos.length) opciones.effort = 'low'
  const c = await agent(pCorregir(ctx, t, archivos, hallazgos), opciones)
  if (!c) return { estado: 'bloqueada', motivo: 'el agente que cerraba la tarea no terminó', archivosSinCommit: archivos }
  juntar(t, c)
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
      log(`${t.id} · ${t.titulo}${t.riesgo.length ? ` (R: ${t.riesgo.join(', ')})` : ''}`)
      ctx.corrieron += 1
      const res = fin(t, await programarTarea(t, fase))
      log(`${t.id}: ${res.estado}${res.motivo ? ` · ${res.motivo}` : ''}`)
      if (res.estado === 'sin-cupo') detenido = `el cupo semanal llegó al ${TOPE_CUPO} %`
      if (res.estado !== 'hecha' && (res.archivosSinCommit || []).length) sucio = true
      if (PRESUPUESTO && PRESUPUESTO.total && PRESUPUESTO.remaining() < 250000) detenido = 'se acabó el presupuesto de la corrida'
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
    integracion = await agent(pIntegrar(ctx), { label: 'integrar', phase: 'Integrar', schema: S_INTEGRAR })
    if (integracion) {
      for (const c of integracion.cambiosAlContrato) anotarCambio(c, 'integración')
      for (const p of integracion.pasosDelUsuario) if (!PASOS.includes(p)) PASOS.push(p)
    }
  }
}

// ---------- Revisión del cierre y puerta ----------

let revision = null
let puerta = null
let copias = ''
const integro = !programa || (integracion && integracion.estado === 'hecha')
const puedeCerrar = cierra && !detenido && !faltan().length && integro
const esperaAlUsuario = pasosAntes.length > 0 && !USUARIO_LISTO

if (puedeCerrar && esperaAlUsuario) log(`La puerta espera al usuario: ${pasosAntes.join('; ')}. Relanzá con modo cerrar y usuarioListo: true.`)

if (puedeCerrar && !esperaAlUsuario) {
  phase('Revisión')
  revision = await pipeline(
    ['back', 'front'],
    (repo) => agent(pRevisar(ctx, repo), { label: `revisión del cierre · ${repo}`, phase: 'Revisión', schema: S_HALLAZGOS }),
    (rev, repo) => {
      if (!rev || !rev.hallazgos.length) return { repo, hallazgos: rev ? 0 : null, arreglo: null }
      return agent(pCorregirCierre(ctx, repo, rev.hallazgos), { label: `arreglos de la revisión · ${repo}`, phase: 'Revisión', schema: S_COMMIT })
        .then((c) => ({ repo, hallazgos: rev.hallazgos.length, arreglo: c }))
    },
  )
  const resumenRevision = revision
    .filter(Boolean)
    .map((r) => (r.hallazgos === null ? `${r.repo}: la revisión no terminó` : `${r.repo}: ${r.hallazgos} hallazgos${r.arreglo ? `, ${r.arreglo.arreglados.length} arreglados (${r.arreglo.estado})` : ''}`))
    .join('; ')

  phase('Puerta')
  puerta = await agent(pPuerta(ctx, resumenRevision), { label: 'puerta', phase: 'Puerta', schema: S_PUERTA })
  if (puerta) {
    copias = `${puerta.copias.back} y ${puerta.copias.front}`
    if (puerta.fallos.length) {
      const porDueno = {}
      for (const f of puerta.fallos) (porDueno[f.dueno] = porDueno[f.dueno] || []).push(f)
      const arreglos = await parallel(Object.keys(porDueno).map((repo) => () =>
        agent(pArreglarPuerta(ctx, repo, porDueno[repo]), { label: `arreglos de la puerta · ${repo}`, phase: 'Puerta', schema: S_COMMIT })))
      const otra = await agent(pRepetir(ctx, puerta, arreglos), { label: 'puerta · lo que tocan los arreglos', phase: 'Puerta', schema: S_PUERTA })
      if (otra) { puerta = Object.assign({}, otra, { corte: puerta.corte, copias: puerta.copias }); copias = '' }
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
  cierre = await agent(pCierre(ctx, resumen, puerta ? puerta.tabla : null, copias), { label: 'cierre del plan', phase: 'Cierre', schema: S_CIERRE, effort: 'low' })
}

const queFalta = faltan().map((t) => `${t.id}: ${R[t.id] ? `${R[t.id].estado}, ${R[t.id].motivo}` : 'sin correr'}`)
let resultado = 'puerta corrida'
if (!puerta) resultado = queFalta.length ? 'faltan tareas' : (esperaAlUsuario && cierra ? 'espera al usuario antes de la puerta' : 'programada, sin puerta')
if (puerta && puerta.fallos.length) resultado = 'puerta con fallos'

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
  cambiosAlContrato: ctx.CAMBIOS,
  integracion: integracion ? { estado: integracion.estado, diferencias: integracion.diferencias, motivo: integracion.motivo } : null,
  revision,
  puerta: puerta ? { tabla: puerta.tabla, fallos: puerta.fallos, galeria: puerta.galeria, corte: puerta.corte } : null,
  cierre,
  tokensDeSalida: PRESUPUESTO ? PRESUPUESTO.spent() : null,
}
