export const meta = {
  name: 'verificar-etapa',
  description: 'Verifica una etapa con el código listo (el segundo hito), sobre copias de su corte y sin worktrees ni ramas, mientras la siguiente se programa en main: a) prepara: lee su sección de verificación, escribe en main los casos del E2E, los pasos del recorrido y el JSON de la galería, y arma las copias; b) corre la integración y el contrato, el E2E real, la galería con las pantallas contra el tablero y el recorrido a 1440 y 390, cada paso entero una vez y de a uno por la fila pesada; c) arregla en main, por dueño; d) repite solo lo que tocan los arreglos; e) cierra: «verificada» o «verificación con fallos», con lo que falta',
  whenToUse: 'Cuando una etapa llegó a «Código listo» con el conductor (etapa.js) y hay que verificarla, a la vez que se programa la siguiente. args: { plan | clave, corte?: { back, front }, topeCupo?: 85 }. Guía: docs/guides/conducir-una-etapa.md, «La verificación»',
  phases: [
    { title: 'Preparar', detail: 'la sección de verificación, sus casos escritos en main y las copias del corte' },
    { title: 'Correr', detail: 'integración y contrato, E2E real, galería con las pantallas contra el tablero y recorrido a 1440 y 390, de a un paso por la fila pesada' },
    { title: 'Arreglos', detail: 'cada fallo, en main, por su dueño y con su test dirigido' },
    { title: 'Repetir', detail: 'solo los pasos que tocan los arreglos, en las mismas copias' },
    { title: 'Cierre', detail: 'la hoja de ruta, la tabla de resultados, la galería, el backlog y las copias borradas' },
  ],
}

// Se corre con Workflow({ scriptPath: '.agents/workflows/verificar-etapa.js', args: { clave: '12-catalogos' } }), o con
// { plan } (la ruta del plan, aunque ya esté retirado), sin copiarlo a .claude/. Es el segundo hito de una etapa
// (pedido del usuario del 2026-11-27): «Código listo» lo deja etapa.js, y esta verificación corre sobre una copia del
// corte mientras la etapa siguiente se programa en main. La guía es docs/guides/conducir-una-etapa.md y los tests,
// scripts/arneses/verificar-etapa.test.mjs (con agentes falsos).

function claveDe(plan) {
  return plan.split('/').pop().replace(/\.md$/, '').replace(/^\d{4}-\d{2}-\d{2}-etapa-/, '')
}

const A = typeof args === 'string' ? (/\.md$/.test(args) ? { plan: args } : { clave: args }) : (args || {})
const PLAN = A.plan ? String(A.plan).replace(/\\/g, '/') : ''
const CLAVE = A.clave ? String(A.clave) : (PLAN ? claveDe(PLAN) : '')
if (!CLAVE) throw new Error('Falta args.plan (la ruta del plan, aunque ya esté retirado) o args.clave, por ejemplo { clave: "12-catalogos" }')
if (!/^[a-z0-9][a-z0-9-]*$/.test(CLAVE)) throw new Error(`La clave «${CLAVE}» va como en los commits: minúsculas, números y guiones`)
if (A.plan && A.clave && claveDe(PLAN) !== CLAVE) throw new Error(`El plan ${PLAN} es de la clave ${claveDe(PLAN)}, no de ${CLAVE}`)
const HASH = /^[0-9a-f]{7,40}$/i
const CORTE = A.corte === undefined ? null : A.corte
if (CORTE !== null && !(CORTE && HASH.test(String(CORTE.back || '')) && HASH.test(String(CORTE.front || '')))) {
  throw new Error('corte va con los dos commits: { back, front }, cada uno un hash de git')
}
// Con este porcentaje del cupo semanal no se empieza (AGENTS.md, «Sesiones y cupo»). Solo el usuario lo sube.
const TOPE_CUPO = A.topeCupo === undefined ? 85 : A.topeCupo
if (!Number.isInteger(TOPE_CUPO) || TOPE_CUPO < 1 || TOPE_CUPO > 100) throw new Error('topeCupo va de 1 a 100.')

const FRONT = '../ArquitecturaBaseMutitenantFront'
// Ruta corta y con el nombre exacto de cada repo, hermanas: el E2E, contracts:check y los detectores buscan el otro al lado.
const RAIZ = `C:/e2e-${CLAVE}`
const COPIAS = { back: `${RAIZ}/ArquitecturaBaseMutitenant`, front: `${RAIZ}/ArquitecturaBaseMutitenantFront` }
const SALIDAS = `.local/verificacion/${CLAVE}`
const LINEA = `Tarea: ${CLAVE}/verificacion`

// ---------- Textos que reciben los agentes ----------

const LEER_POCO = `- Leé por partes: ubicá con grep -n y leé con Read (offset y limit) solo lo que necesitás, y no releas lo que ya leíste. Lo largo de una consola va a un archivo de ${SALIDAS}/ (en el back de siempre) y se lee por partes.`

function comunes() {
  return `Reglas de esta corrida, además de AGENTS.md:
- Las copias del corte son ${COPIAS.back} y ${COPIAS.front}: clones con git clone --local, en detached HEAD, sin ramas ni worktrees. En main (el back es la carpeta actual y el front, ${FRONT}) se está programando a la vez la etapa siguiente: sin stash, reset, cambio de rama ni push; tocá y commiteá solo tus rutas, con git commit -m "<tipo>(<área>): <qué>" -m "${LINEA}" -- <rutas>, y no reviertas nada que no hiciste. Con la app levantada de otra sesión, nadie edita el src del front.
${LEER_POCO}
- Ninguna corrida contra un proveedor real (Meta, IA, Gmail) ni con un teléfono: lo que lo necesite no se corre y va en aMano, «para probar a mano», nunca como aprobado. Nunca leas, copies ni muestres un secreto: de user-secrets, solo los nombres.
- No toques el lienzo (${FRONT}/docs/design/lienzo) ni dibujes pantallas.
- Un proceso se corta por PID, nunca con un filtro amplio. Lo que levantes (aspire, un Vite) lo apagás al terminar: aspire stop desde la copia, y lo demás por PID.
- Una decisión que cambia el producto (qué ve o puede hacer una persona, una pantalla, el modelo de accesos) no la tomás: devolvé la pregunta en motivo, con un ejemplo real y la opción que recomendás primero.`
}

// La fila pesada de carril.mjs, de a un comando por llamada: entre un paso y otro la toma un carril de la etapa
// siguiente, si la está esperando. Corre desde el back de siempre, así el candado es el mismo que usan los carriles.
function fila(dir, comando) {
  return `node scripts/arneses/carril.mjs fila --dir ${dir} "${comando}"`
}

const FILA = `La fila pesada: cada comando pesado va en su propia llamada, desde la carpeta del back de siempre (la actual): ${fila('<copia>', '<comando>')}. Espera su turno con los carriles de la etapa siguiente, corre el comando en <copia> y suelta la fila al terminar. Nunca encadenes dos comandos en una misma llamada ni la tomes para esperar: entre un paso y otro, un carril puede entrar. Corré cada llamada en segundo plano, con la salida en un archivo de ${SALIDAS}/, y esperá a que termine sin consultar el avance.`

// ---------- Los pasos de la fase b ----------
// Cada paso corre entero una sola vez (AGENTS.md, «Velocidad y cierre liviano»), con su propio agente y su propia
// llamada a la fila. Los del front usan el back de la copia ya compilado por el primero.

const PASOS = [
  {
    id: 'integracion',
    titulo: 'Integración y contrato',
    corre: () => `1. En la copia del back (cd ${COPIAS.back}): node scripts/arneses/verificar.mjs --integracion. Compila la solución y corre los tests de integración, y espera él solo su turno en la fila pesada: no lo envuelvas en otra llamada a la fila.
2. En la copia del front (cd ${COPIAS.front}): npm run contracts:check, que es liviano y va sin fila.`,
    pasaSi: 'el build tiene 0 errores y 0 advertencias, la integración 0 fallidos y contracts:check sale 0. En compila, false si el build de la copia del back falló: sin él no corre ningún otro paso',
  },
  {
    id: 'e2e',
    titulo: 'E2E real',
    corre: () => fila(COPIAS.front, 'npm run test:e2e:real'),
    pasaSi: 'hay 0 fallidos. Arranca su propio AppHost con el back de la copia, ya compilado: si dice que hay otro AppHost en ejecución, no es un fallo del código; va en motivo, con resultado «no corrió»',
  },
  {
    id: 'galeria',
    titulo: 'Galería',
    corre: (c) => `La galería fotografía la app servida en http://localtest.me:5192 (docs/rules/tests.md del front, «Galería»): levantá en ese puerto el Vite de la copia del front, en segundo plano, y apagalo por PID al terminar. La captura va por la fila: ${fila(COPIAS.front, `node scripts/capturar-etapa.mjs --casos ${c.galeria}`)}.`,
    pasaSi: 'cada caso queda capturado al lado de su tablero, a 1440, 390 y 320, sin desplazarse de costado. En galeria, la ruta absoluta de la carpeta de la copia con las capturas lado a lado (docs/design/capturas/etapa-<etapa>/)',
  },
  {
    id: 'recorrido-1440',
    titulo: 'Recorrido a 1440',
    corre: () => fila(COPIAS.front, 'node scripts/recorrido-usuario.mjs --no-build --width=1440'),
    pasaSi: 'todas las comprobaciones están en verde (skill /recorrido). Lo que el script no automatiza, como el ingreso con Google o un WhatsApp real, va en aMano',
  },
  {
    id: 'recorrido-390',
    titulo: 'Recorrido a 390',
    corre: () => fila(COPIAS.front, 'node scripts/recorrido-usuario.mjs --no-build --width=390'),
    pasaSi: 'todas las comprobaciones están en verde (skill /recorrido). Lo que el script no automatiza va en aMano',
  },
  {
    id: 'quitar-modulo',
    titulo: 'Quitar el módulo',
    corre: (c) => `Una sola vez y con la base vacía, según ${c.quitarModulo}, en una copia descartable aparte: git clone --local ${COPIAS.back} ${RAIZ}/quitar-modulo (sin ramas). Cada comando pesado (el build, los tests y el ingreso) va en su propia llamada a la fila, con --dir ${RAIZ}/quitar-modulo. Al terminar, borrá esa copia.`,
    pasaSi: 'el núcleo, el build, los tests y el ingreso quedan en verde sin el módulo',
  },
]

const PANTALLAS = {
  titulo: 'Pantallas contra el tablero',
  puntos: '10 (botones y avisos) y 13 (cada pantalla se compara con su tablero, con textos ciertos en todos los estados)',
  reglas: 'docs/rules/pantallas-y-ui.md, responsive.md, accesibilidad.md y paginado-y-listados.md del front',
}

// ---------- Esquemas ----------

const TEXTO = { type: 'string' }
const LISTA = { type: 'array', items: { type: 'string' } }
const CORTE_ESQUEMA = { type: 'object', properties: { back: TEXTO, front: TEXTO }, required: ['back', 'front'] }
const POR_REPO = { type: 'object', properties: { back: LISTA, front: LISTA }, required: ['back', 'front'] }

const S_LEER = {
  type: 'object',
  properties: {
    etapa: TEXTO,
    numero: TEXTO,
    plan: TEXTO,
    spec: TEXTO,
    origen: { type: 'string', enum: ['plan', 'backlog', 'plan-retirado', 'ninguno'] },
    donde: TEXTO,
    seccion: TEXTO,
    corte: CORTE_ESQUEMA,
    estadoEnHojaDeRuta: TEXTO,
    galeria: TEXTO,
    quitarModulo: TEXTO,
    aMano: LISTA,
    bloqueos: LISTA,
    cupoSemanal: { type: ['number', 'null'] },
    notas: LISTA,
  },
  required: ['etapa', 'numero', 'plan', 'spec', 'origen', 'donde', 'seccion', 'corte', 'estadoEnHojaDeRuta', 'galeria', 'quitarModulo', 'aMano', 'bloqueos', 'cupoSemanal', 'notas'],
}

const S_CASOS = {
  type: 'object',
  properties: {
    estado: { type: 'string', enum: ['hecha', 'bloqueada'] },
    commits: POR_REPO,
    archivos: LISTA,
    galeria: TEXTO,
    aMano: LISTA,
    archivosSinCommit: LISTA,
    motivo: TEXTO,
    verificacion: TEXTO,
  },
  required: ['estado', 'commits', 'archivos', 'galeria', 'aMano', 'archivosSinCommit', 'motivo', 'verificacion'],
}

const S_COPIAS = { type: 'object', properties: { listas: { type: 'boolean' }, motivo: TEXTO }, required: ['listas', 'motivo'] }

const FALLO = {
  type: 'object',
  properties: { paso: TEXTO, archivo: TEXTO, linea: { type: 'integer' }, causa: TEXTO, dueno: { type: 'string', enum: ['back', 'front'] } },
  required: ['paso', 'archivo', 'linea', 'causa', 'dueno'],
}

const PASO_PROPIEDADES = {
  paso: TEXTO,
  resultado: { type: 'string', enum: ['verde', 'con fallos', 'no corrió'] },
  numero: TEXTO,
  compila: { type: 'boolean' },
  fallos: { type: 'array', items: FALLO },
  galeria: TEXTO,
  aMano: LISTA,
  salida: TEXTO,
  motivo: TEXTO,
}
const PASO_REQUERIDOS = ['paso', 'resultado', 'numero', 'compila', 'fallos', 'galeria', 'aMano', 'salida', 'motivo']
const S_PASO = { type: 'object', properties: PASO_PROPIEDADES, required: PASO_REQUERIDOS }

const S_REPETIR = {
  type: 'object',
  properties: { repetidos: { type: 'array', items: { type: 'object', properties: PASO_PROPIEDADES, required: PASO_REQUERIDOS } }, motivo: TEXTO },
  required: ['repetidos', 'motivo'],
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

const S_CIERRE = {
  type: 'object',
  properties: { commits: LISTA, verificada: { type: 'boolean' }, galeria: TEXTO, copiasBorradas: { type: 'boolean' }, motivo: TEXTO },
  required: ['commits', 'verificada', 'galeria', 'copiasBorradas', 'motivo'],
}

// ---------- Prompts de la fase a ----------

function pLeer() {
  const corte = CORTE
    ? `el que te pasaron, back ${CORTE.back} y front ${CORTE.front}: confirmá con git cat-file -e en cada repo que existen, y devolvé el hash entero`
    : `el HEAD del momento de cada repo, con el hash entero: git rev-parse HEAD en el back (la carpeta actual) y en el front (${FRONT})`
  return `Sos el primer paso de la verificación de la etapa con clave «${CLAVE}»${PLAN ? ` (plan ${PLAN})` : ''}: el segundo hito, después de «Código listo». Solo leés y corrés comandos de lectura (git log, git show, git rev-parse, grep, ls, docker info, df, listar procesos): no cambiás nada.

Devolvé:
1. etapa: su nombre, como lo dice el título de su plan o su fila en la hoja de ruta (docs/plans/2026-09-27-plan-de-desarrollo.md); numero: su número si tiene (por ejemplo «12»), o cadena vacía; plan: la ruta de su plan (${PLAN || `docs/plans/<fecha>-etapa-${CLAVE}.md`}), siga o no en el árbol; spec: la spec que nombra.
2. La sección de verificación de la etapa. Buscala en este orden y quedate con la primera que aparezca:
   a. En el plan, si sigue en el árbol: «## Verificación de la etapa» (hasta el 2026-11-27 se llamaba «## Al cierre del bloque E12–E25»). origen: plan.
   b. En docs/backlog.md: «## Verificación de la etapa ${CLAVE}», donde la deja el cierre de etapa.js, o «## Para después de la etapa ${CLAVE}», donde la dejaba el cierre de antes de los dos hitos (así la deja la E12). En esa segunda, la sección es solo lo de la verificación (el E2E real, el recorrido, la galería, lo que pida un teléfono o un proveedor real y quitar el módulo); lo demás es para después y no cuenta. origen: backlog.
   c. En el plan que retiró git: git log --diff-filter=D --format=%H -- "docs/plans/*etapa-${CLAVE}.md" y git show <commit>^:<ruta>, la misma sección que en a. origen: plan-retirado.
   Si no está en ninguno, origen: ninguno. En donde, el archivo y el título de la sección (o el commit y la ruta, si es del plan retirado); en seccion, su texto literal.
3. corte: ${corte}.
4. estadoEnHojaDeRuta: la celda de estado de la fila de la etapa en la hoja de ruta, literal.
5. galeria: el JSON de la galería que nombra la sección, con su ruta desde la raíz del front (por ejemplo scripts/capturas/etapa-12.json), o cadena vacía si la sección no pide galería. quitarModulo: la guía con que la sección pide quitar el módulo (por ejemplo docs/guides/quitar-formularios.md), o cadena vacía si no lo pide o dice que no corresponde. aMano: lo que la sección pide y la verificación no corre, una línea por cosa: una corrida con un proveedor real (IA, Meta), un teléfono o lo que hace el usuario.
6. bloqueos, uno por línea, o la lista vacía:
   - origen ninguno: no hay qué verificar;
   - la fila de la hoja de ruta no dice que la etapa tiene el código listo («Código listo el …», o «Cerrada el …» como la dejaba el cierre de antes de los dos hitos), o ya dice «Verificada»;
   - Docker no responde (docker info): la integración y el E2E lo necesitan;
   - quedan menos de 15 GB libres en el disco C: (las copias, con sus builds y node_modules, ocupan unos 6 GB);
   - un AppHost o un Vite levantados (procesos aspire, dcp o node con vite en su línea de comando): el E2E, la galería y el recorrido usan sus mismos puertos.
   Que en main se esté programando la etapa siguiente, o que haya copias de sus carriles en .local/copias/ o de una verificación anterior en ${RAIZ}, no es un bloqueo: la verificación arma sus propias copias.
7. cupoSemanal: si tenés la herramienta mcp__ccd_session_mgmt__get_usage (buscala con ToolSearch), el porcentaje usado del límite semanal de todos los modelos; si no, null.
8. notas: lo que conviene que sepa quien lanzó la verificación, en pocas líneas.`
}

function pCasos(L) {
  return `Sos el que escribe en main los casos de la verificación de la etapa «${L.etapa}» (clave ${CLAVE}), antes de que corran sobre las copias de su corte. No programaste la etapa.

La sección de verificación, de ${L.donde}:
"""
${L.seccion}
"""

De ahí escribís, en el front (${FRONT}), solo en scripts/ y con sus tests, sin tocar src/ (lo está programando la etapa siguiente):
- los casos del E2E real: su script y su enganche en scripts/test-e2e-real.test.mjs, como los de Acciones del asistente (scripts/test-e2e-real-acciones.mjs);
- los pasos del recorrido: su script y su enganche en scripts/recorrido-usuario.mjs, con su test, como scripts/recorrido-usuario-acciones.mjs;
- el JSON de la galería${L.galeria ? ` (${L.galeria})` : ''}: los tableros que nombra la sección, como scripts/capturas/etapa-acciones-del-asistente.json. Un estado que necesita algo nuevo en src/ va a pending, con su reason.
Si un caso del E2E necesita su preparación en el back, va solo en tools/ArquitecturaBaseMultitenant.RealE2ESetup. Lo que ya escribieron las tareas de la etapa o una verificación anterior se completa, sin rehacerlo: mirá git log --grep "Tarea: ${CLAVE}/" -- scripts/ en el front. Lo que pide un proveedor real, un teléfono o al usuario no se escribe: va en aMano.

Verificá solo lo que escribiste, sin levantar nada: node --test de los .test.mjs que tocaste y node scripts/capturar-etapa.mjs --casos <el JSON> --manifiesto, que lo valida sin fotografiar. El E2E, la galería y el recorrido corren después, en las copias.

Devolvé: estado hecha, o bloqueada con el motivo; commits, los hashes cortos de cada repo en orden (back y front); archivos; galeria, la ruta del JSON desde la raíz del front, o cadena vacía si la etapa no trae pantallas; aMano; archivosSinCommit; motivo; y en verificacion, lo que corriste y su resultado, en una línea.

${comunes()}`
}

function pCopias(L, casos) {
  const commits = casos && casos.commits ? casos.commits : { back: [], front: [] }
  const traer = commits.back.length || commits.front.length
    ? `3. Los casos de la verificación ya están en main, después del corte: traelos con git -C <copia> cherry-pick <commits>, en orden y sin ramas (la copia sigue en detached HEAD). Back: ${commits.back.join(', ') || 'ninguno'}. Front: ${commits.front.join(', ') || 'ninguno'}. Si uno choca, git cherry-pick --abort, traé solo sus archivos con git -C <copia> checkout <commit> -- <archivos> y decilo en motivo.`
    : '3. No hay casos nuevos que traer de main.'
  return `Sos el que arma las copias del corte para la verificación de la etapa «${L.etapa}» (clave ${CLAVE}). Sin worktrees ni ramas (AGENTS.md, «Todo en main, también la puerta»), y sin tocar main.

1. Si ${RAIZ} ya existe, es de una verificación anterior: aspire stop desde su copia del back si algo quedó levantado, y borrala entera (solo esa carpeta).
2. git clone --local del back (la carpeta actual, con la ruta absoluta que da pwd -W) en ${COPIAS.back}, y del front (${FRONT}) en ${COPIAS.front}. Después, git -C <copia> checkout -q --detach <corte>: back ${L.corte.back} y front ${L.corte.front}.
${traer}
4. En la copia del front, npm ci: un node_modules enlazado rompe la letra Inter en las capturas. El back no se compila acá: lo compila el primer paso, en la fila pesada.

Devolvé listas: true si las dos copias quedaron en su corte, con los casos encima y con npm ci hecho; y en motivo, lo que no salió.`
}

// ---------- Prompts de la fase b ----------

function pPaso(P, c) {
  return `Sos el que corre el paso «${P.titulo}» de la verificación de la etapa «${c.etapa}» (clave ${CLAVE}), sobre las copias del corte: back ${COPIAS.back} y front ${COPIAS.front} (corte ${c.corte.back} y ${c.corte.front}, con los casos de la verificación encima). No arreglás nada: cada fallo vuelve con su dueño y lo arregla otro agente, en main.

Qué corrés, entero y una sola vez:
${P.corre(c)}

${FILA}

Pasa si ${P.pasaSi}.

Leé solo el resumen (totales, passed y failed, # pass y # fail) y, de lo que falla, sus líneas. Antes de contar un fallo, descartá el entorno: un test que se pasó de tiempo se corre una vez más, solo, con su propia llamada a la fila; si así pasa, era la carga de la PC y no cuenta.

Devolvé: paso «${P.id}»; resultado verde, con fallos o no corrió; numero (por ejemplo «41/41»); compila (${P.id === 'integracion' ? 'false si el build de la copia del back falló' : 'true'}); fallos, cada uno con su paso, archivo y línea, la causa en una línea y su dueño (back o front); galeria (${P.id === 'galeria' ? 'la carpeta de las capturas lado a lado' : 'cadena vacía'}); aMano, lo que el paso no automatiza; salida, dónde quedó el log; y motivo.

${comunes()}`
}

function pPantallas(c, galeria) {
  return `Sos el revisor de la premisa «${PANTALLAS.titulo}» en la verificación de la etapa «${c.etapa}» (clave ${CLAVE}). Solo leés: no cambiás nada, no compilás ni corrés tests.

La galería acaba de quedar en ${galeria}: cada pantalla nueva o cambiada de la etapa, al lado de su tablero. Abrí cada imagen con Read y compará la captura con su tablero.
- Mirá las piezas compartidas (que se use la de shared/ y no una propia), las ventanas emergentes, los colores (los tokens del tema, nada fijo), los filtros, los botones y avisos, y los estados: cargando, vacío, con error, sin permiso y con la organización suspendida.
- Cada diferencia va con el archivo y la línea del front que la causa: buscalos en los commits de la etapa, con git log --grep "Tarea: ${CLAVE}/" en ${FRONT}.

De la «Lista de control» de .agents/skills/puerta/SKILL.md, mirás los puntos ${PANTALLAS.puntos}. Las reglas son ${PANTALLAS.reglas}. Lo demás lo miran otros: no lo nombres.

Devolvé solo diferencias reales. En repo, front (o back, si la causa es un texto o un dato del back); archivo y línea; en punto, el número de la lista o la regla; en escenario, qué se ve y qué dice el tablero, en una línea; en regla, la que se rompe; y en porque, la captura que lo muestra. Una lista vacía es un resultado válido: no inventes.`
}

function pVerificarPantallas(c, galeria, hallazgos) {
  const afirmaciones = hallazgos.map((h, i) => `${i + 1}. [${h.repo}] ${h.archivo}:${h.linea}: ${h.titulo}. Escenario: ${h.escenario}.${h.regla ? ` Regla: ${h.regla}.` : ''}`).join('\n')
  return `Sos el verificador de la premisa «${PANTALLAS.titulo}» en la verificación de la etapa «${c.etapa}» (clave ${CLAVE}). Otro agente comparó la galería con los tableros y afirma lo de abajo. No sabés cómo llegó a cada afirmación: tu trabajo es intentar refutarla contra las capturas lado a lado de ${galeria} y el código de HEAD del front (${FRONT}). Solo leés: no cambiás nada.

${afirmaciones}

Por cada una devolvé su número, confirmado y el motivo en una línea. Confirmado es true solo si lo ves en la captura y en el código, y el código es de la etapa (sus commits llevan la línea «Tarea: ${CLAVE}/»). Si ya estaba antes de la etapa, si el tablero no lo pide o si dudás, false con lo que viste.`
}

// ---------- Prompts de las fases c a e ----------

function lineaDeFallo(f) {
  return `- ${f.paso}: ${f.archivo}:${f.linea}. ${f.causa}`
}

function lineaDeHallazgo(h) {
  return `- [${h.gravedad}] ${h.titulo} (${h.archivo}:${h.linea}). Escenario: ${h.escenario}.${h.regla ? ` Regla: ${h.regla}.` : ''}`
}

function dondeDe(repo) {
  return repo === 'back' ? 'el back (la carpeta actual)' : `el front (${FRONT})`
}

function pArreglar(c, repo, fallos, hallazgos) {
  const deLosPasos = fallos.length ? `Fallos de los pasos:\n${fallos.map(lineaDeFallo).join('\n')}\n` : ''
  const dePantallas = hallazgos.length ? `${deLosPasos ? '\n' : ''}Diferencias con el tablero que vio el revisor de pantallas en la galería, confirmadas por un verificador:\n${hallazgos.map(lineaDeHallazgo).join('\n')}\n` : ''
  return `Sos el dueño de lo que encontró la verificación de la etapa «${c.etapa}» (clave ${CLAVE}) en ${dondeDe(repo)}. No programaste la etapa ni corriste la verificación.

${deLosPasos}${dePantallas}
Arreglá cada uno en main, el checkout de siempre, no en la copia de la verificación: con un test dirigido que falle sin el arreglo, buscando el mismo defecto en los caminos hermanos, y corré solo ese test y el build (en el front, tsc -b y el lint de lo que tocaste). Si main no compila por el trabajo a medias de la etapa siguiente, probá en una copia de HEAD dentro de .local/ y borrala al terminar. Un test del E2E o del recorrido que falla se arregla en su causa, no en el test, salvo que el test esté mal. Si un fallo era del entorno o no es real, va en descartados, con su motivo. Commiteá cada arreglo coherente con la línea «${LINEA}».

En arreglados, una línea por cosa arreglada, con su commit.

${comunes()}`
}

function lineaDePaso(r) {
  return `${r.id} (${r.paso}): ${r.resultado}${r.numero ? `, ${r.numero}` : ''}`
}

function pRepetir(c, arreglos, tabla) {
  return `Los arreglos de la verificación de la etapa «${c.etapa}» (clave ${CLAVE}) ya están commiteados en main:
${arreglos.map((a) => `- ${a.repo}: ${a.commits.join(', ')}`).join('\n')}

Traelos a las copias de la verificación (back ${COPIAS.back}, front ${COPIAS.front}) con git -C <copia> fetch -q origin y git -C <copia> cherry-pick <commits> de su repo, en orden y sin ramas: la copia sigue en detached HEAD. No traigas el HEAD de main: trae lo de la etapa siguiente. Si un cherry-pick choca, git cherry-pick --abort y decilo en motivo. Si un arreglo tocó el back, la copia del back se vuelve a compilar: ${fila(COPIAS.back, 'dotnet build ArquitecturaBaseMultitenant.slnx')}.

Corré solo los pasos que esos arreglos tocan, según la skill .agents/skills/puerta/SKILL.md: un paso con fallos que un arreglo tocó se repite siempre; en el E2E y el recorrido, solo sus casos si el script lo permite; si un arreglo cambió una pantalla, sus casos de la galería (capturar-etapa.mjs --id <ids>) y nada más de ella. La suite completa de un repo se repite solo si un arreglo tocó autenticación, persistencia base o la composición de la app. Lo que no tocan los arreglos no se repite.

${FILA}

La tabla de hoy:
${tabla.map((r) => `- ${lineaDePaso(r)}`).join('\n')}

Devolvé en repetidos solo los pasos que corriste, cada uno con su paso (el id de la tabla), su resultado, su número y sus fallos. En motivo, lo que no se pudo traer o correr.

${comunes()}`
}

function lineaParaDespues(texto) {
  return `- ${texto}`
}

function pCierre(c, K) {
  const tabla = JSON.stringify(K.tabla)
  const compartidos = 'Antes de tocar docs/backlog.md, la hoja de ruta o la spec, mirá git diff --quiet -- <archivo>: si tiene cambios sin commitear de otra sesión (el conductor de la etapa siguiente cierra en los mismos archivos), no lo toques y decilo en motivo.'
  const galeria = K.galeria
    ? `- La galería (${K.galeria}) pasa al front de main, al mismo lugar (docs/design/capturas/etapa-<etapa>/), y se commitea en el front con git commit -m "docs(capturas): galería de la etapa ${c.etapa}, <n> pantallas al lado de su tablero" -m "${LINEA}" -- <la carpeta>. En galeria, su ruta en main.`
    : '- No hay galería que guardar: en galeria, cadena vacía.'
  const paraDespues = K.paraDespues.length
    ? `- En docs/backlog.md, la sección «## Para después de la etapa ${CLAVE}», al final si todavía no está, con una línea por cosa que no esté ya, así:\n${K.paraDespues.map(lineaParaDespues).join('\n')}`
    : ''
  const copias = `- Las copias: aspire stop desde ${COPIAS.back} si algo quedó levantado, y borrá ${RAIZ} entera. En copiasBorradas, si quedó borrada.`
  const comun = `${galeria}
${paraDespues ? `${paraDespues}\n` : ''}${copias}
- Después corré node scripts/arneses/arnes.mjs estado-docs --detectar y realinear-docs --detectar, y dejalos en 0.
${compartidos}`
  if (!K.verificada) {
    return `Sos el que deja al día la verificación de la etapa «${c.etapa}» (clave ${CLAVE}), que quedó con fallos: ${K.falta.join('; ')}. La fecha es la de hoy (date +%F). Cambiás solo lo que dice abajo, sin historial ni actas.

La tabla de la verificación: ${tabla}.

- En la hoja de ruta (docs/plans/2026-09-27-plan-de-desarrollo.md), la fila de la etapa sigue en «Código listo el …» (o «Cerrada el …», si la dejó así el cierre de antes de los dos hitos) y suma «; la verificación del <fecha> quedó con fallos:» y lo que falta, en pocas palabras.
- La sección de verificación (${c.donde}) no se toca: es lo que falta verificar.
${comun}
Commiteá en el back con git commit -m "docs(planes): ${c.etapa}, la verificación quedó con fallos" -m "${LINEA}" -- <rutas>. Devolvé los commits, verificada false, galeria, copiasBorradas y motivo.`
  }
  return `Sos el que cierra la verificación de la etapa «${c.etapa}» (clave ${CLAVE}): todos los pasos quedaron en verde. La fecha es la de hoy (date +%F). Cambiás solo lo que dice abajo, sin historial ni actas y en el tono de cada documento.

La tabla de la verificación: ${tabla}.

- En la hoja de ruta (docs/plans/2026-09-27-plan-de-desarrollo.md), la fila de la etapa pasa de «Código listo el …; falta la verificación» (o de «Cerrada el …», como la dejaba el cierre de antes de los dos hitos) a «Verificada el <fecha>», y conserva lo que trajo.
- La tabla corta de resultados (paso, resultado y número) va en la spec de la etapa (${c.spec || 'la que nombra su plan'}), en su sección del cierre, al lado de la de los tests enteros.
- La sección de verificación sale de ${c.donde}${c.origen === 'backlog' ? ': si es «## Para después de la etapa …», salen solo sus líneas de la verificación y las demás se quedan' : (c.origen === 'plan' ? ': el plan sigue en el árbol, así que solo se saca la sección' : ': el plan ya está retirado y la sección no está en el árbol, así que no hay nada que sacar')}.
${comun}
Commiteá en el back con git commit -m "docs(planes): ${c.etapa} queda verificada el <fecha>" -m "${LINEA}" -- <rutas>. Devolvé los commits, verificada true, galeria, copiasBorradas y motivo.`
}

// ---------- Ayudas ----------

function porRepo(lista, campo) {
  const g = {}
  for (const x of lista) if (x && (x[campo] === 'back' || x[campo] === 'front')) (g[x[campo]] = g[x[campo]] || []).push(x)
  return g
}

function sinCorrer(P, motivo, resultado = 'no corrió') {
  return { paso: P.id, resultado, numero: '', compila: true, fallos: [], galeria: '', aMano: [], salida: '', motivo }
}

const NO_APLICA = 'no aplica'

async function correrPaso(P, c) {
  const r = await agent(pPaso(P, c), { label: `paso · ${P.titulo}`, phase: 'Correr', schema: S_PASO })
  if (!r) return sinCorrer(P, 'el agente que corría el paso no terminó')
  return Object.assign({}, r, { paso: P.id, fallos: (r.fallos || []).map((f) => Object.assign({}, f, { paso: f.paso || P.titulo })) })
}

/** Las pantallas contra el tablero: un revisor sobre la galería y un verificador que intenta refutar lo que vio. */
async function revisarPantallas(c, galeria) {
  const base = { titulo: PANTALLAS.titulo }
  if (!galeria) return Object.assign(base, { hallazgos: 0, confirmados: [], descartados: [], resumen: 'no hubo galería que comparar' })
  const rev = await agent(pPantallas(c, galeria), { label: `revisión · ${PANTALLAS.titulo}`, phase: 'Correr', schema: S_PREMISA, effort: 'medium' })
  if (!rev) return Object.assign(base, { hallazgos: null, confirmados: null, descartados: [], resumen: 'la revisión no terminó' })
  const hallazgos = rev.hallazgos || []
  if (!hallazgos.length) return Object.assign(base, { hallazgos: 0, confirmados: [], descartados: [], resumen: rev.resumen })
  const v = await agent(pVerificarPantallas(c, galeria, hallazgos), { label: `verifica · ${PANTALLAS.titulo}`, phase: 'Correr', schema: S_VERIFICA, effort: 'medium' })
  if (!v) return Object.assign(base, { hallazgos: hallazgos.length, confirmados: null, descartados: [], resumen: 'la verificación no terminó: sus hallazgos no llegan a un arreglo' })
  const confirmados = []
  const descartados = []
  hallazgos.forEach((h, i) => {
    const veredicto = (v.veredictos || []).find((x) => x.numero === i + 1)
    if (veredicto && veredicto.confirmado === true) confirmados.push(Object.assign({}, h, { confirmacion: veredicto.motivo }))
    else descartados.push({ titulo: h.titulo, motivo: veredicto ? veredicto.motivo : 'el verificador no lo contestó' })
  })
  return Object.assign(base, { hallazgos: hallazgos.length, confirmados, descartados, resumen: rev.resumen })
}

// ---------- a. Preparar ----------

phase('Preparar')
const L = await agent(pLeer(), { label: 'leer la sección', phase: 'Preparar', schema: S_LEER, effort: 'low' })
if (!L) throw new Error(`El agente que lee la sección de verificación de ${CLAVE} no terminó`)
const bloqueos = L.bloqueos.slice()
if (L.origen === 'ninguno' && !bloqueos.length) bloqueos.push(`No hay sección de verificación de ${CLAVE}: ni en el plan, ni en el backlog, ni en el plan retirado`)
if (typeof L.cupoSemanal === 'number' && L.cupoSemanal >= TOPE_CUPO) bloqueos.push(`El cupo semanal va en ${L.cupoSemanal} %: con ${TOPE_CUPO} % no se empieza a verificar`)
if (bloqueos.length) return { etapa: L.etapa, clave: CLAVE, resultado: 'no empezó', bloqueos, notas: L.notas }

const C = { etapa: L.etapa, corte: L.corte, galeria: L.galeria, quitarModulo: L.quitarModulo, spec: L.spec, donde: L.donde, origen: L.origen }
log(`Verificación de ${L.etapa}: la sección sale de ${L.donde}; el corte es ${L.corte.back.slice(0, 8)} (back) y ${L.corte.front.slice(0, 8)} (front)`)

const casos = await agent(pCasos(L), { label: 'escribir los casos', phase: 'Preparar', schema: S_CASOS })
if (casos && casos.galeria) C.galeria = casos.galeria
const copias = await agent(pCopias(L, casos), { label: 'armar las copias', phase: 'Preparar', schema: S_COPIAS, effort: 'low' })
const copiasListas = Boolean(copias && copias.listas)
if (!copiasListas) log(`Las copias del corte no quedaron listas: ${copias ? copias.motivo : 'el agente no terminó'}`)

// ---------- b. Correr ----------

phase('Correr')
const R = {}
const P_ = Object.fromEntries(PASOS.map((P) => [P.id, P]))
if (!C.galeria) R.galeria = sinCorrer(P_.galeria, 'la etapa no trae galería', NO_APLICA)
if (!C.quitarModulo) R['quitar-modulo'] = sinCorrer(P_['quitar-modulo'], 'la sección no pide quitar el módulo', NO_APLICA)
let pantallas = null
let carpetaDeLaGaleria = ''

if (!copiasListas) {
  for (const P of PASOS) if (!R[P.id]) R[P.id] = sinCorrer(P, `las copias del corte no quedaron listas: ${copias ? copias.motivo : 'el agente no terminó'}`)
} else {
  R.integracion = await correrPaso(P_.integracion, C)
  // Sin el back de la copia compilado no corre nada más: el E2E y el recorrido arrancan el AppHost con --no-build.
  if (R.integracion.compila === false || R.integracion.resultado === 'no corrió') {
    for (const P of PASOS) if (!R[P.id]) R[P.id] = sinCorrer(P, 'el back de la copia no quedó compilado: el primer paso no lo dejó')
  } else {
    R.e2e = await correrPaso(P_.e2e, C)
    if (!R.galeria) {
      R.galeria = await correrPaso(P_.galeria, C)
      carpetaDeLaGaleria = R.galeria.galeria || ''
    }
    // Las pantallas contra el tablero leen la galería sin la fila: corren a la vez que el recorrido.
    await parallel([
      async () => { pantallas = await revisarPantallas(C, carpetaDeLaGaleria) },
      async () => {
        for (const id of ['recorrido-1440', 'recorrido-390', 'quitar-modulo']) if (!R[id]) R[id] = await correrPaso(P_[id], C)
      },
    ])
    for (const P of PASOS) if (!R[P.id]) R[P.id] = sinCorrer(P, 'el conductor de la verificación falló en este paso')
  }
}
if (!pantallas) pantallas = { titulo: PANTALLAS.titulo, hallazgos: 0, confirmados: [], descartados: [], resumen: 'no corrió: no hubo galería' }

const tablaDe = () => PASOS.map((P) => ({ id: P.id, paso: P.titulo, resultado: R[P.id].resultado, numero: R[P.id].numero || '' }))

// ---------- c. Arreglos ----------

const fallos = PASOS.flatMap((P) => (R[P.id].fallos || []))
const fallosPorDueno = porRepo(fallos, 'dueno')
const pantallasPorRepo = porRepo(pantallas.confirmados || [], 'repo')
const duenos = ['back', 'front'].filter((repo) => fallosPorDueno[repo] || pantallasPorRepo[repo])
let arreglos = []
if (duenos.length) {
  phase('Arreglos')
  const hechos = await parallel(duenos.map((repo) => () =>
    agent(pArreglar(C, repo, fallosPorDueno[repo] || [], pantallasPorRepo[repo] || []), { label: `arreglos · ${repo}`, phase: 'Arreglos', schema: S_COMMIT })))
  arreglos = duenos.map((repo, i) => {
    const a = hechos[i]
    const cuantos = (fallosPorDueno[repo] || []).length + (pantallasPorRepo[repo] || []).length
    return a
      ? { repo, aArreglar: cuantos, estado: a.estado, arreglados: a.arreglados, descartados: a.descartados, commits: a.commits, pregunta: a.pregunta, motivo: a.motivo }
      : { repo, aArreglar: cuantos, estado: 'no terminó', arreglados: [], descartados: [], commits: [], pregunta: '', motivo: '' }
  })
}

// ---------- d. Repetir solo lo que tocan los arreglos ----------

let repetidos = []
const conCommits = arreglos.filter((a) => a.commits.length)
if (conCommits.length && copiasListas) {
  phase('Repetir')
  const rep = await agent(pRepetir(C, conCommits, tablaDe()), { label: 'repetir lo que tocan los arreglos', phase: 'Repetir', schema: S_REPETIR })
  if (!rep) log('El agente que repetía lo que tocan los arreglos no terminó: la tabla queda como antes de los arreglos')
  for (const fila_ of (rep && rep.repetidos) || []) {
    if (!P_[fila_.paso]) continue
    R[fila_.paso] = Object.assign({}, fila_, { repetido: true })
    repetidos.push(fila_.paso)
  }
}

// ---------- e. Cierre ----------

const falta = []
for (const P of PASOS) {
  const r = R[P.id]
  if (r.resultado === 'con fallos') falta.push(`${P.titulo}: ${r.fallos.length || 'con'} fallos${r.motivo ? ` (${r.motivo})` : ''}`)
  else if (r.resultado === 'no corrió') falta.push(`${P.titulo}: no corrió (${r.motivo})`)
}
if (!casos) falta.push('los casos de la etapa: el agente que los escribía no terminó')
else if (casos.estado !== 'hecha') falta.push(`los casos de la etapa: ${casos.motivo}`)
if (pantallas.confirmados === null) falta.push(`${PANTALLAS.titulo}: ${pantallas.resumen}`)
for (const a of arreglos) if (a.estado !== 'hecha') falta.push(`los arreglos del ${a.repo} quedaron ${a.estado}${a.pregunta ? `, con una pregunta: ${a.pregunta}` : (a.motivo ? `: ${a.motivo}` : '')}`)

const aMano = [...new Set([].concat(L.aMano, casos ? casos.aMano : [], ...PASOS.map((P) => R[P.id].aMano || [])))]
const paraDespues = aMano.map((x) => `**Para probar a mano en la prueba integral del bloque:** ${x}`)
  .concat(arreglos.flatMap((a) => a.descartados.map((d) => `**${d.titulo}.** Lo descartó el arreglo de la verificación: ${d.porque} (${a.repo}).`)))

phase('Cierre')
const K = { verificada: !falta.length, falta, tabla: tablaDe(), galeria: carpetaDeLaGaleria, paraDespues }
const cierre = await agent(pCierre(C, K), { label: 'cierre', phase: 'Cierre', schema: S_CIERRE, effort: 'medium' })
if (!cierre) falta.push(`el cierre no terminó: la hoja de ruta y el backlog siguen como estaban, y las copias quedan en ${RAIZ}`)
else if (K.verificada && cierre.verificada !== true) falta.push(`el cierre no dejó la etapa verificada: ${cierre.motivo}`)
if (cierre && cierre.copiasBorradas === false) log(`Las copias no se borraron: ${RAIZ}. ${cierre.motivo}`)

const resultado = !falta.length ? 'verificada' : 'verificación con fallos'
log(resultado === 'verificada' ? `${L.etapa}: verificada` : `${L.etapa}: verificación con fallos. Falta: ${falta.join('; ')}`)

return {
  etapa: L.etapa,
  clave: CLAVE,
  resultado,
  falta,
  corte: L.corte,
  copias: COPIAS,
  seccion: { origen: L.origen, donde: L.donde },
  casos: casos ? { estado: casos.estado, commits: casos.commits, galeria: casos.galeria, motivo: casos.motivo } : null,
  tabla: PASOS.map((P) => ({ paso: P.titulo, resultado: R[P.id].resultado, numero: R[P.id].numero || '', repetido: Boolean(R[P.id].repetido), motivo: R[P.id].motivo || '' })),
  fallos: PASOS.flatMap((P) => R[P.id].fallos || []),
  pantallas,
  arreglos,
  repetidos,
  aMano,
  cierre,
}
