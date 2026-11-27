import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

// El conductor de etapas (.agents/workflows/etapa.js) corre con agentes falsos: ningún test llama a un modelo.
const ruta = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '.agents', 'workflows', 'etapa.js');
const fuente = await readFile(ruta, 'utf8');
const AsyncFunction = Object.getPrototypeOf(async () => {}).constructor;
const PLAN = 'docs/plans/2026-11-19-etapa-prueba.md';
const TRANSVERSALES = ['Arquitectura, clean code y SOLID', 'Lo que no está en la spec'];
const CAZADORES = ['Lo que se rompe al usarlo', 'Entradas hostiles e ingreso', 'Volumen y escala'];
// Los que corren siempre, sin esperar la lista de archivos de la etapa.
const DE_SIEMPRE = [...TRANSVERSALES, ...CAZADORES];
// Rutas de ejemplo que tocan cada tema de riesgo (y algunas fichas), para el agente que lista los archivos de la etapa.
const RUTA = {
  datos: 'src/X.Infrastructure/Persistence/Repositories/RoleRepository.cs',
  permisos: 'src/X.Api/Authorization/RolePolicy.cs',
  aislamiento: 'src/X.Api/Tenancy/TenantResolver.cs',
  servicio: 'src/X.Application/Services/Roles/RoleService.cs',
};
const archivosDe = (...lista) => ({ archivos: lista.map(([r, tareas, repo = 'back']) => ({ repo, ruta: r, tareas })), motivo: '' });

function tarea(id, carril, extra = {}) {
  return { id, titulo: `tarea ${id}`, carril, subcarril: carril, riesgo: [], espera: [], estado: 'pendiente', motivo: '', texto: `texto de ${id}`, area: '', migracion: false, archivos: [], ...extra };
}

function lectura(tareas, extra = {}) {
  return {
    etapa: 'Etapa de prueba',
    bloqueos: [],
    cupoSemanal: 50,
    tareas,
    textos: { contrato: 'chat del contrato', back: [{ subcarril: 'back', texto: 'chat del back' }], front: 'chat del front' },
    pasosDelUsuario: [],
    notas: [],
    ...extra,
  };
}

const programa = (id, extra = {}) => ({ estado: 'hecha', commits: [`c-${id}`], archivos: [`${id}.cs`], archivosSinCommit: [], falta: '', motivo: '', pregunta: '', cambiosAlContrato: [], verificacion: 'build y tests en verde', ...extra });
const commit = (id, extra = {}) => ({ estado: 'hecha', commits: [`c2-${id}`], archivos: [`${id}.cs`], archivosSinCommit: [], arreglados: [], descartados: [], motivo: '', pregunta: '', cambiosAlContrato: [], verificacion: 'ok', ...extra });
const tests = (hallazgos = []) => ({ tabla: [{ paso: 'Tests del back', resultado: 'ok', numero: '10/10' }], hallazgos, resumen: '' });
const ultima = (extra = {}) => ({ estado: 'hecha', commits: [], arreglados: [], alBacklog: [], archivosSinCommit: [], motivo: '', verificacion: 'ok', ...extra });
const hallazgoDe = (titulo, repo = 'back', extra = {}) => ({ titulo, punto: '4', repo, archivo: `${titulo}.cs`, linea: 3, escenario: `pasa ${titulo}`, regla: 'guardado', porque: `RAZON-${titulo}`, gravedad: 'media', ...extra });

/** Responde según la etiqueta; `propias` pisa las respuestas por defecto. */
function respondedor(lect, propias = {}) {
  return async (label, prompt) => {
    if (label in propias) return typeof propias[label] === 'function' ? propias[label](prompt) : propias[label];
    if (label === 'leer el plan') return lect;
    const programa_ = label.match(/^(\S+) · programa/);
    if (programa_) return programa(programa_[1]);
    const cierra = label.match(/^(\S+) · commitea/);
    if (cierra) return commit(cierra[1]);
    if (label === 'archivos de la etapa') return { archivos: [], motivo: '' };
    if (/^revisión · /.test(label)) return { hallazgos: [], resumen: 'sin hallazgos' };
    if (label === 'tests enteros') return tests();
    if (label.startsWith('arreglos · ')) return commit('arreglo');
    if (label === 'última revisión') return ultima();
    if (label === 'cierre') return { commits: ['c-cierre'], listas: ['prueba', 'a', 'b'], motivo: '' };
    throw new Error(`etiqueta sin respuesta: ${label}`);
  };
}

async function correr(responder, args) {
  const llamadas = [];
  const logs = [];
  const agent = async (prompt, opts = {}) => {
    llamadas.push({ label: opts.label, phase: opts.phase, effort: opts.effort, model: opts.model, prompt });
    return responder(opts.label ?? '', prompt, opts);
  };
  const parallel = (thunks) => Promise.all(thunks.map((t) => Promise.resolve().then(t).catch(() => null)));
  const pipeline = (items, ...etapas) => Promise.all(items.map(async (item, i) => {
    let r = item;
    for (const e of etapas) {
      try { r = await e(r, item, i); } catch { return null; }
    }
    return r;
  }));
  const budget = { total: null, spent: () => 0, remaining: () => Infinity };
  const cuerpo = fuente.replace(/^export const meta = /m, 'const meta = ');
  const fn = new AsyncFunction('agent', 'parallel', 'pipeline', 'phase', 'log', 'args', 'budget', cuerpo);
  const resultado = await fn(agent, parallel, pipeline, () => {}, (m) => logs.push(m), args, budget);
  return { resultado, llamadas, logs, etiquetas: llamadas.map((l) => l.label) };
}

const estadoDe = (resultado, id) => resultado.tareas.find((t) => t.id === id);
const promptDe = (llamadas, label) => llamadas.find((l) => l.label === label).prompt;

test('conductor: sin plan falla con el ejemplo, y un modo desconocido también', async () => {
  await assert.rejects(correr(respondedor(lectura([])), {}), /Falta args\.plan/);
  await assert.rejects(correr(respondedor(lectura([])), { plan: PLAN, modo: 'rapido' }), /Modo desconocido/);
});

test('conductor: planificar solo lee, con un agente, y devuelve los carriles y lo que va a costar', async () => {
  const lect = lectura([tarea('K1', 'contrato'), tarea('B1', 'back', { riesgo: ['datos'], archivos: [`${RUTA.datos} (nuevo)`] }), tarea('B2', 'back'), tarea('F1', 'front'), tarea('I1', 'integracion')]);
  const { resultado, etiquetas } = await correr(respondedor(lect), { plan: PLAN, modo: 'planificar' });
  assert.deepEqual(etiquetas, ['leer el plan']);
  assert.equal(resultado.clave, 'prueba');
  assert.deepEqual(resultado.carriles, { contrato: ['K1'], back: ['B1', 'B2'], front: ['F1'] });
  assert.deepEqual(resultado.carrilesBack.map((c) => c.tareas), [['B1'], ['B2']], 'sin área en común, cada una en su carril');
  // Las fichas que llevarían revisor salen del mapa de archivos de las tareas; capas-y-flujo la controlan sus tests.
  assert.deepEqual(resultado.reglasPrevistas, ['back/guardado', 'back/logs', 'back/persistencia-ef', 'back/tests']);
  // Leer y un agente por tarea pendiente; después, una vez: el que lista los archivos, el revisor de reglas del back (sus
  // 4 fichas van juntas), 2 transversales, 3 cazadores, el adversarial de «datos», los tests enteros, 2 arregladores, la
  // última revisión y el cierre.
  assert.equal(resultado.agentesEstimados, (1 + 4) + (1 + 1 + 2 + 3 + 1 + 1 + 2 + 1 + 1));
});

test('conductor: el cupo en 85 % o un bloqueo del plan frenan antes de programar', async () => {
  for (const extra of [{ cupoSemanal: 85 }, { bloqueos: ['la etapa anterior no cerró'] }]) {
    const { resultado, etiquetas } = await correr(respondedor(lectura([tarea('B1', 'back')], extra)), { plan: PLAN });
    assert.equal(resultado.resultado, 'no empezó');
    assert.equal(resultado.bloqueos.length, 1);
    assert.deepEqual(etiquetas, ['leer el plan']);
  }
});

test('conductor: el lector bloquea solo por cambios de otra sesión: lo de una corrida frenada del plan y los agentes de ListAgents no frenan', async () => {
  const { llamadas } = await correr(respondedor(lectura([tarea('B1', 'back')])), { plan: PLAN, modo: 'planificar' });
  const leer = promptDe(llamadas, 'leer el plan');
  assert.match(leer, /cambios sin commitear de otra sesión en los archivos que la etapa va a tocar/);
  assert.match(leer, /Tampoco bloquean los de una tarea de este mismo plan que dejó una corrida anterior frenada .*\.local\/copias\/prueba-back-\*\): van en notas, con la tarea y la copia, y esa tarea los continúa/);
  assert.match(leer, /Los agentes que lista ListAgents .* no son un bloqueo: un conductor frenado no sigue vivo/);
  assert.match(leer, /Las I no corren como tareas: las cubren la revisión, los arreglos, la última revisión y el cierre/);
  assert.match(leer, /esa no tiene el código listo según la hoja de ruta .*: su fila dice «Código listo», «Verificada» o «Cerrada»\. Que todavía corra su verificación no es un bloqueo/, 'la etapa siguiente no espera la verificación de la anterior');
  assert.match(leer, /la app levantada desde main: .*Uno que corre desde una copia fuera de main \(su línea de comando o su carpeta está en C:\/e2e-\* o en \.local\/, .*\) no bloquea, porque no mira el src de main: va en notas/, 'la verificación de otra etapa o el entorno de un ticket no frenan');
});

test('conductor: con topeCupo, el usuario deja pasar el 85 %, y los agentes miran ese tope', async () => {
  const lect = lectura([tarea('B1', 'back')], { cupoSemanal: 90 });
  const { resultado, llamadas } = await correr(respondedor(lect), { plan: PLAN, modo: 'programar', topeCupo: 100 });
  assert.equal(estadoDe(resultado, 'B1').estado, 'hecha');
  assert.match(promptDe(llamadas, 'B1 · programa'), /Con 100 % o más no empieces/);
  await assert.rejects(correr(respondedor(lect), { plan: PLAN, topeCupo: 120 }), /topeCupo va de 1 a 100/);
});

test('conductor: una espera del contrato hacia el back es un ciclo y no empieza', async () => {
  const lect = lectura([tarea('K1', 'contrato', { espera: ['B1'] }), tarea('B1', 'back')]);
  const { resultado, etiquetas } = await correr(respondedor(lect), { plan: PLAN });
  assert.match(resultado.bloqueos[0], /ciclo: .*K1.*B1/);
  assert.deepEqual(etiquetas, ['leer el plan']);
});

test('conductor: una tarea con riesgo commitea como una sin riesgo, sin revisión adversarial ni otro agente', async () => {
  const lect = lectura([tarea('B1', 'back', { riesgo: ['datos', 'aislamiento'], area: 'roles' }), tarea('B2', 'back', { area: 'roles' })]);
  const { resultado, llamadas, etiquetas } = await correr(respondedor(lect), { plan: PLAN, modo: 'programar' });
  assert.deepEqual(etiquetas.filter((e) => /^B/.test(e)), ['B1 · programa', 'B2 · programa']);
  assert.ok(!etiquetas.some((e) => /adversarial/i.test(e)), 'la revisión adversarial va en la fase 2, no en la tarea');
  for (const id of ['B1', 'B2']) {
    const prompt = promptDe(llamadas, `${id} · programa`);
    assert.match(prompt, new RegExp(`Commiteá cada parte coherente y verificada con la línea «Tarea: prueba/${id}»`));
    assert.match(prompt, /Código primero/);
    assert.doesNotMatch(prompt, /NO commitees|lleva revisión adversarial/);
    assert.match(prompt, /la revisión adversarial antes del commit: eso cambió el 2026-11-26 y no se hace/, 'lo que el plan pide por tarea quedó sin efecto');
    assert.match(prompt, /Tampoco corrés el E2E real, la galería ni el recorrido/);
  }
  assert.equal(estadoDe(resultado, 'B1').estado, 'hecha');
  assert.deepEqual(estadoDe(resultado, 'B1').commits, ['c-B1']);
  assert.equal(resultado.resultado, 'programada');
});

test('conductor: el back y el front corren a la vez, después de todo el contrato', async () => {
  let empezoElFront;
  const frontEmpezo = new Promise((r) => { empezoElFront = r; });
  const orden = [];
  const lect = lectura([tarea('K1', 'contrato'), tarea('B1', 'back'), tarea('F1', 'front')]);
  const resp = respondedor(lect, {
    'K1 · programa': () => { orden.push('K1'); return programa('K1'); },
    'B1 · programa': async () => {
      orden.push('B1');
      await Promise.race([frontEmpezo, new Promise((_, no) => setTimeout(() => no(new Error('el front no corrió a la vez')), 2000))]);
      return programa('B1');
    },
    'F1 · programa': () => { orden.push('F1'); empezoElFront(); return programa('F1'); },
  });
  const { resultado } = await correr(resp, { plan: PLAN, modo: 'programar' });
  assert.equal(orden[0], 'K1');
  assert.equal(estadoDe(resultado, 'B1').estado, 'hecha');
  assert.equal(estadoDe(resultado, 'F1').estado, 'hecha');
});

test('conductor: lo que espera a una tarea bloqueada no corre, el resto del carril sigue, y sin el código entero no se revisa', async () => {
  const lect = lectura([tarea('B1', 'back'), tarea('B2', 'back', { espera: ['B1'] }), tarea('B3', 'back'), tarea('F1', 'front', { espera: ['B1'] })]);
  const bloqueada = programa('B1', { estado: 'bloqueada', commits: [], pregunta: '¿La difusión cuenta como enviada?', motivo: 'decisión de producto' });
  const { resultado, etiquetas, llamadas } = await correr(respondedor(lect, { 'B1 · programa': bloqueada }), { plan: PLAN });
  assert.equal(estadoDe(resultado, 'B2').estado, 'sin-correr');
  assert.match(estadoDe(resultado, 'B2').motivo, /espera B1, que quedó bloqueada/);
  assert.equal(estadoDe(resultado, 'F1').estado, 'sin-correr');
  assert.equal(estadoDe(resultado, 'B3').estado, 'hecha');
  assert.deepEqual(resultado.preguntas, [{ tarea: 'B1', pregunta: '¿La difusión cuenta como enviada?' }]);
  assert.ok(!etiquetas.includes('archivos de la etapa') && !etiquetas.includes('tests enteros'), 'con tareas sin hacer no se revisa');
  assert.equal(resultado.resultado, 'faltan tareas');
  const cierre = promptDe(llamadas, 'cierre');
  assert.match(cierre, /La etapa no se cierra: faltan tareas de prueba/);
  assert.match(cierre, /«Pendiente: <ids y motivo corto>»/);
  assert.doesNotMatch(cierre, /git rm/);
});

test('conductor: una tarea que deja cambios sin commitear frena su carril', async () => {
  const lect = lectura([tarea('B1', 'back', { area: 'roles' }), tarea('B2', 'back', { area: 'roles' }), tarea('F1', 'front')]);
  const sucia = programa('B1', { estado: 'bloqueada', commits: [], archivosSinCommit: ['B1.cs'] });
  const { resultado } = await correr(respondedor(lect, { 'B1 · programa': sucia }), { plan: PLAN, modo: 'programar' });
  assert.match(estadoDe(resultado, 'B2').motivo, /sin commitear/);
  assert.equal(estadoDe(resultado, 'F1').estado, 'hecha', 'el otro repo sigue');
});

test('conductor: sin cupo no empieza ninguna tarea nueva', async () => {
  const lect = lectura([tarea('B1', 'back', { area: 'roles' }), tarea('B2', 'back', { area: 'roles' })]);
  const { resultado } = await correr(respondedor(lect, { 'B1 · programa': programa('B1', { estado: 'sin-cupo', commits: [] }) }), { plan: PLAN, modo: 'programar' });
  assert.equal(estadoDe(resultado, 'B2').estado, 'sin-correr');
  assert.match(resultado.detenido, /cupo/);
});

test('conductor: una tarea a medias sigue con otro agente, hasta seis', async () => {
  const lect = lectura([tarea('B1', 'back')]);
  let vuelta = 0;
  const resp = respondedor(lect, {});
  const { resultado, llamadas } = await correr(async (label, prompt) => {
    if (/^B1 · programa/.test(label)) return programa('B1', { estado: 'parcial', falta: `falta el paso ${++vuelta}` });
    return resp(label, prompt);
  }, { plan: PLAN, modo: 'programar' });
  assert.deepEqual(llamadas.filter((l) => /^B1/.test(l.label)).map((l) => l.label), ['B1 · programa', 'B1 · programa (2)', 'B1 · programa (3)', 'B1 · programa (4)', 'B1 · programa (5)', 'B1 · programa (6)']);
  assert.match(llamadas[2].prompt, /falta el paso 1/);
  assert.equal(estadoDe(resultado, 'B1').estado, 'parcial');
});

test('conductor: lo hecho antes no se repite, y solo corre lo pedido', async () => {
  const lect = lectura([tarea('K1', 'contrato', { estado: 'hecha', motivo: 'c-viejo' }), tarea('B1', 'back'), tarea('B2', 'back'), tarea('F1', 'front')]);
  const { resultado, etiquetas } = await correr(respondedor(lect), { plan: PLAN, modo: 'programar', solo: ['B1'] });
  assert.ok(!etiquetas.includes('K1 · programa'));
  assert.equal(estadoDe(resultado, 'B1').estado, 'hecha');
  assert.match(estadoDe(resultado, 'B2').motivo, /fuera de esta corrida/);
  assert.match(estadoDe(resultado, 'F1').motivo, /fuera de esta corrida/);
  assert.equal(resultado.resultado, 'faltan tareas');
});

// ---------- Las cinco fases ----------

test('conductor: las cinco fases en orden: programar, revisión en paralelo, arreglos, última revisión y cierre', async () => {
  const lect = lectura([tarea('K1', 'contrato'), tarea('B1', 'back'), tarea('F1', 'front'), tarea('I1', 'integracion'), tarea('I3', 'integracion', { texto: 'el recorrido cubre un rol con un permiso ajeno' })]);
  const falla = { titulo: 'el 403 no muestra su texto', punto: 'Tests del front', repo: 'front', archivo: 'Roles.test.tsx', linea: 40, escenario: 'falta el texto del 403', regla: 'tests enteros', porque: 'expected «Sin permiso»', gravedad: 'alta' };
  const { resultado, etiquetas, llamadas } = await correr(respondedor(lect, {
    'archivos de la etapa': archivosDe([RUTA.servicio, ['prueba/B1']]),
    'revisión · Reglas · back': { hallazgos: [hallazgoDe('dos guardados')], resumen: '' },
    'tests enteros': tests([falla]),
  }), { plan: PLAN });
  const fase = (label) => llamadas.find((l) => l.label === label).phase;
  assert.equal(fase('leer el plan'), 'Programar');
  assert.equal(fase('K1 · programa'), 'Programar');
  for (const l of ['archivos de la etapa', 'tests enteros', 'revisión · Reglas · back', ...DE_SIEMPRE.map((t) => `revisión · ${t}`)]) assert.equal(fase(l), 'Revisión', l);
  assert.ok(!etiquetas.includes('revisión · Reglas · front'), 'la etapa no tocó fichas del front');
  assert.equal(fase('arreglos · back'), 'Arreglos');
  assert.equal(fase('arreglos · front'), 'Arreglos');
  assert.equal(fase('última revisión'), 'Última revisión');
  assert.equal(fase('cierre'), 'Cierre');
  const i = (e) => etiquetas.indexOf(e);
  assert.ok(i('F1 · programa') < i('archivos de la etapa') && i('tests enteros') < i('arreglos · back'));
  assert.ok(i('arreglos · front') < i('última revisión') && i('última revisión') < i('cierre'));
  assert.equal(etiquetas[etiquetas.length - 1], 'cierre');
  assert.ok(!etiquetas.some((e) => /^I\d · |integrar|^verifica · |puerta|Pantallas/.test(e)), 'sin integración, verificadores, puerta ni pantallas');
  assert.match(promptDe(llamadas, 'arreglos · front'), /el 403 no muestra su texto \(Roles\.test\.tsx:40\).*Lo vio: Tests enteros/);
  assert.match(promptDe(llamadas, 'arreglos · back'), /dos guardados \(dos guardados\.cs:3\).*Lo vio: Reglas · back/);
  assert.match(promptDe(llamadas, 'última revisión'), /back: 0 arreglados y 0 descartados de 1 \(hecha\)/);
  const cierre = promptDe(llamadas, 'cierre');
  assert.match(cierre, /Sos el que deja la etapa «Etapa de prueba» \(plan docs\/plans\/2026-11-19-etapa-prueba\.md\) con el código listo, el primer hito/);
  assert.match(cierre, /La verificación .* es el segundo hito y va aparte, con verificar-etapa\.js/);
  assert.match(cierre, /82087a6a/);
  assert.match(cierre, /git rm del plan/);
  assert.match(cierre, /la fila de la etapa dice «Código listo el <fecha>; falta la verificación»/);
  assert.doesNotMatch(cierre, /«Cerrada el <fecha>»/);
  assert.match(cierre, /ClosedStages en los dos repos/);
  assert.match(cierre, /estado-docs --detectar y realinear-docs --detectar, y dejalos en 0/);
  assert.match(cierre, /Los tests enteros: \[\{"paso":"Tests del back"/);
  assert.match(cierre, /carril\.mjs limpiar prueba-back/);
  assert.match(cierre, /la sección «## Verificación de la etapa prueba», al final: antes del git rm, la sección «Verificación de la etapa» del plan \(en los planes de antes del 2026-11-27, «Al cierre del bloque E12–E25»\) pasa ahí entera, para la verificación/, 'la verificación la lee del backlog');
  assert.match(cierre, /código listo el <fecha>: se retira su plan y su verificación pasa al backlog" -m "Tarea: prueba\/cierre"/);
  assert.match(cierre, /La documentación que nombra la sección «Revisión y cierre» del plan la escribís vos/);
  assert.doesNotMatch(cierre, /tabla de la puerta/);
  const delPlan = /De la sección «Revisión y cierre» de docs\/plans\/2026-11-19-etapa-prueba\.md: los riesgos que suma son ítems de tu lista de control/;
  for (const l of ['revisión · Reglas · back', ...DE_SIEMPRE.map((t) => `revisión · ${t}`), 'última revisión']) assert.match(promptDe(llamadas, l), delPlan, l);
  assert.equal(resultado.resultado, 'código listo');
  assert.equal(resultado.verificacion, "falta: Workflow({ scriptPath: '.agents/workflows/verificar-etapa.js', args: { clave: 'prueba' } })");
  assert.equal(resultado.revision.hallazgos, 2);
  assert.deepEqual(resultado.arreglos.map((a) => a.repo), ['back', 'front']);
});

test('conductor: en la revisión todo corre a la vez: reglas, cazadores, transversales, adversariales y los tests enteros', async () => {
  const esperados = ['tests enteros', ...DE_SIEMPRE.map((t) => `revisión · ${t}`), 'revisión · Reglas · back', 'revisión · Adversarial · datos'];
  const empezados = new Set();
  let todos;
  const estanTodos = new Promise((r) => { todos = r; });
  const base = respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })]), { 'archivos de la etapa': archivosDe([RUTA.datos, ['prueba/B1']]) });
  const { etiquetas } = await correr(async (label, prompt) => {
    if (esperados.includes(label)) {
      empezados.add(label);
      if (empezados.size === esperados.length) todos();
      await Promise.race([estanTodos, new Promise((_, no) => setTimeout(() => no(new Error(`${label} esperó solo`)), 2000))]);
    }
    return base(label, prompt);
  }, { plan: PLAN, modo: 'cerrar' });
  assert.equal(empezados.size, esperados.length);
  assert.ok(etiquetas.includes('última revisión'), 'nadie quedó esperando');
});

test('conductor: un revisor de reglas por repo tocado, con cada ficha y sus archivos, que busca solo lo que tiene efecto, y lo salteado lo dice', async () => {
  const fichas = [
    'back/guardado', 'back/logs', 'back/persistencia-ef', 'back/tests',
    'front/accesibilidad', 'front/accesos-y-permisos', 'front/errores', 'front/estructura-y-features', 'front/formatos',
    'front/pantallas-y-ui', 'front/responsive', 'front/tests', 'front/textos-y-traducciones',
  ];
  const archivos = archivosDe(
    [RUTA.datos, ['prueba/B1']],
    ['docs/guides/una-guia.md', ['prueba/B1']],
    ['src/areas/business/roles/RolesPage.tsx', ['prueba/F1'], 'front'],
    ['src/areas/business/roles/RolesPage.test.tsx', ['prueba/F1'], 'front'],
  );
  const base = respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' }), tarea('F1', 'front', { estado: 'hecha' })]), { 'archivos de la etapa': archivos });
  const { llamadas, logs, resultado } = await correr(base, { plan: PLAN, modo: 'cerrar' });
  const deReglas = llamadas.filter((l) => /^revisión · Reglas? · /.test(l.label));
  assert.deepEqual(deReglas.map((l) => l.label), ['revisión · Reglas · back', 'revisión · Reglas · front'], 'uno por repo, no uno por ficha');
  const lista = llamadas.find((l) => l.label === 'archivos de la etapa');
  assert.equal(lista.effort, 'low');
  assert.match(lista.prompt, /git log --grep "Tarea: prueba\/" --name-only/);

  const back = promptDe(llamadas, 'revisión · Reglas · back');
  assert.equal(deReglas[0].effort, 'medium');
  assert.equal(deReglas[0].phase, 'Revisión');
  for (const f of ['guardado', 'logs', 'persistencia-ef', 'tests']) {
    assert.match(back, new RegExp(`^- docs/rules/${f}\\.md: src/X\\.Infrastructure/Persistence/Repositories/RoleRepository\\.cs$`, 'm'), f);
  }
  assert.doesNotMatch(back, /RolesPage|una-guia|capas-y-flujo/, 'solo sus fichas y sus archivos');
  assert.match(back, /git log --grep "Tarea: prueba\/" -p --format="%H %s" -- <esos archivos>/);
  assert.match(back, /Buscá solo incumplimientos con efecto: algo que una persona ve mal, un dato que se guarda mal, o una regla de seguridad o de convivencia de versiones/);
  assert.match(back, /Lo cosmético \(un color, un corte sm, un nombre\) no es un hallazgo, ni lo que ya controla un test, como la paridad de los textos de ResourceParityTests/);

  const front = promptDe(llamadas, 'revisión · Reglas · front');
  assert.match(front, /^- \.\.\/ArquitecturaBaseMutitenantFront\/docs\/rules\/accesibilidad\.md: src\/areas\/business\/roles\/RolesPage\.tsx$/m, 'los tests del front van solo a la ficha de tests');
  assert.match(front, /^- \.\.\/ArquitecturaBaseMutitenantFront\/docs\/rules\/tests\.md: src\/areas\/business\/roles\/RolesPage\.tsx, src\/areas\/business\/roles\/RolesPage\.test\.tsx$/m);
  assert.match(front, /los tokens de color de src\/theme-tokens\.test\.ts/);
  assert.doesNotMatch(front, /RoleRepository/);

  assert.match(promptDe(llamadas, 'revisión · Arquitectura, clean code y SOLID'), /revisar-arquitectura\.md/);
  assert.match(promptDe(llamadas, 'revisión · Lo que no está en la spec'), /node scripts\/arneses\/arnes\.mjs conformidad --detectar --plan docs\/plans\/2026-11-19-etapa-prueba\.md/);

  assert.ok(logs.includes('Revisión de reglas: back (guardado, logs, persistencia-ef, tests); front (accesibilidad, accesos-y-permisos, errores, estructura-y-features, formatos, pantallas-y-ui, responsive, tests, textos-y-traducciones)'));
  assert.ok(logs.some((m) => /^Sin revisor, porque un test la controla entera: back\/capas-y-flujo \(la controla .*ApplicationServicesTests.*back\/documentacion \(la controla los detectores de documentación/.test(m)));
  assert.ok(logs.some((m) => /^Sin revisor, porque la etapa no tocó sus archivos: back\/api-http, .*front\/telefonos$/.test(m)));
  assert.ok(logs.includes(`Corren 8 revisores y los tests enteros: ${[...DE_SIEMPRE, 'Reglas · back', 'Reglas · front', 'Adversarial · datos'].join(', ')}`));
  assert.deepEqual(resultado.revision.reparto.fichas.conRevisor.slice().sort(), fichas.slice().sort());
  assert.equal(resultado.revision.reparto.archivos, 4);
  assert.equal(resultado.revision.revisores.length, 2 + 3 + 2 + 1, 'transversales, cazadores, uno de reglas por repo y el adversarial de datos');
});

test('conductor: los tres cazadores de bugs corren siempre, en la revisión, con effort high y los pasos de una persona', async () => {
  const hallazgo = hallazgoDe('lo editado en otra página se pierde', 'front', { punto: 'Lo que se rompe al usarlo', archivo: 'CatalogEditor.tsx', linea: 88, gravedad: 'alta' });
  const { llamadas, resultado } = await correr(respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })]), {
    'revisión · Lo que se rompe al usarlo': { hallazgos: [hallazgo], resumen: '' },
  }), { plan: PLAN, modo: 'cerrar' });
  for (const c of CAZADORES) {
    const l = llamadas.find((x) => x.label === `revisión · ${c}`);
    assert.ok(l, `${c} corre aunque la etapa no toque ninguna ficha`);
    assert.equal(l.phase, 'Revisión', c);
    assert.equal(l.effort, 'high', c);
    assert.match(l.prompt, new RegExp(`Sos un cazador de bugs del tema «${c}»`));
    assert.match(l.prompt, /No es una auditoría de cumplimiento: no buscás si el código sigue una ficha, sino qué se rompe para una persona/);
    assert.match(l.prompt, /git log --grep "Tarea: prueba\/" --format="%H %s" en el back \(la carpeta actual\) y en el front \(\.\.\/ArquitecturaBaseMutitenantFront\)/);
    assert.match(l.prompt, /Cada hallazgo trae los pasos concretos de una persona y lo que pasa/);
    assert.match(l.prompt, /Antes de anotarlo, volvé a leer el código y confirmalo/);
  }
  assert.match(promptDe(llamadas, 'revisión · Lo que se rompe al usarlo'), /un campo obligatorio que el front no manda.*El conflicto 409.*Paginar, buscar o filtrar con cambios sin guardar.*El doble clic/s);
  assert.match(promptDe(llamadas, 'revisión · Entradas hostiles e ingreso'), /Archivos: .*un correo o un método revocado o desvinculado que sigue sirviendo para entrar.*Enumeración/s);
  assert.match(promptDe(llamadas, 'revisión · Volumen y escala'), /Lecturas que traen todo.*N\+1.*cuadráticos.*Colas y prioridades.*Locks y barreras que vencen/s);
  assert.match(promptDe(llamadas, 'arreglos · front'), /\[alta\] lo editado en otra página se pierde \(CatalogEditor\.tsx:88\).*Lo vio: Lo que se rompe al usarlo/);
  assert.deepEqual(resultado.revision.revisores.map((r) => r.titulo), DE_SIEMPRE, 'sin fichas ni temas de riesgo, quedan los transversales y los cazadores');
});

test('conductor: los tests enteros corren en copias de HEAD con la fila pesada y los detectores, sin E2E, galería ni recorrido', async () => {
  const { llamadas } = await correr(respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })])), { plan: PLAN, modo: 'cerrar' });
  const t = promptDe(llamadas, 'tests enteros');
  assert.match(t, /git clone --local \. \.local\/copias\/prueba-tests\/ArquitecturaBaseMutitenant y git clone --local \.\.\/ArquitecturaBaseMutitenantFront \.local\/copias\/prueba-tests\/ArquitecturaBaseMutitenantFront/);
  assert.match(t, /carril\.mjs fila "dotnet build ArquitecturaBaseMultitenant\.slnx" \(0 errores y 0 advertencias\)/);
  assert.match(t, /carril\.mjs fila "dotnet test --no-build" \(con Docker\)/);
  assert.match(t, /npm run build, npm run lint, npm test .* y npm run contracts:check/);
  assert.match(t, /realinear-docs --detectar, estado-docs --detectar y conformidad --detectar --plan docs\/plans\/2026-11-19-etapa-prueba\.md/);
  assert.match(t, /borrá \.local\/copias\/prueba-tests/);
  assert.match(t, /Sin E2E real, galería ni recorrido: van en la verificación de la etapa, el segundo hito/);
  assert.doesNotMatch(t, /test:e2e:real|capturar-etapa|recorrido-usuario/);
  assert.match(t, /Cada test que falla, cada advertencia del build y cada hallazgo de un detector es un hallazgo/);
});

test('conductor: los hallazgos repetidos se juntan antes de arreglar, y el arreglador comprueba cada uno', async () => {
  const base = respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })]), { 'archivos de la etapa': archivosDe([RUTA.datos, ['prueba/B1']]) });
  const repetido = { archivo: 'RoleRepository.cs', linea: 40 };
  const { llamadas, etiquetas, logs, resultado } = await correr(async (label, prompt) => {
    if (label === 'revisión · Lo que se rompe al usarlo') return { hallazgos: [hallazgoDe('se pierde lo editado', 'back', { ...repetido, regla: 'lo editado no se guarda' })], resumen: '' };
    if (label === 'revisión · Reglas · back') return { hallazgos: [hallazgoDe('dos guardados', 'back', repetido)], resumen: '' };
    if (label === 'tests enteros') return tests([hallazgoDe('el test de guardado falla', 'back', { ...repetido, regla: 'tests enteros' })]);
    return base(label, prompt);
  }, { plan: PLAN, modo: 'cerrar' });
  assert.ok(!etiquetas.some((e) => e.startsWith('verifica · ')), 'no hay fase de verificación');
  assert.equal(etiquetas.filter((e) => e.startsWith('arreglos · ')).length, 1, 'un arreglador por repo con hallazgos');
  const arreglo = promptDe(llamadas, 'arreglos · back');
  assert.equal(arreglo.match(/RoleRepository\.cs:40/g).length, 1, 'los repetidos llegan una sola vez');
  assert.match(arreglo, /\[media\] se pierde lo editado \(RoleRepository\.cs:40\).*Lo vio: Lo que se rompe al usarlo/);
  assert.match(arreglo, /también Reglas · back: dos guardados; también Tests enteros: el test de guardado falla/);
  assert.match(arreglo, /primero comprobá contra el código de HEAD que es real/);
  assert.match(arreglo, /va en descartados, con su motivo/);
  assert.match(arreglo, /«Tarea: prueba\/revision»/);
  assert.doesNotMatch(arreglo, /RAZON-/, 'el arreglador no recibe el razonamiento de los revisores');
  assert.ok(logs.includes('Se juntaron 2 hallazgos que repetían archivo y línea'));
  assert.equal(resultado.revision.hallazgos, 1);
  assert.equal(resultado.revision.juntados, 2);
});

test('conductor: sin hallazgos no hay arreglos, y la última revisión corre igual', async () => {
  const { etiquetas, llamadas, resultado } = await correr(respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })])), { plan: PLAN, modo: 'cerrar' });
  assert.ok(!etiquetas.some((e) => e.startsWith('arreglos · ')));
  const u = llamadas.find((l) => l.label === 'última revisión');
  assert.equal(u.effort, 'high');
  assert.match(u.prompt, /Los arreglos de la revisión: no hubo hallazgos\./);
  assert.match(u.prompt, /git log --grep "Tarea: prueba\/" --format="%H %s"/);
  assert.equal(resultado.resultado, 'código listo');
});

test('conductor: la última revisión arregla lo chico y manda lo demás al backlog, sin otra ronda', async () => {
  const grande = { que: 'partir RoleService', porQue: 'pasa la complejidad.', donde: 'RoleService.cs' };
  const { llamadas, etiquetas, resultado } = await correr(respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })]), {
    'última revisión': ultima({ arreglados: ['el 404 del camino hermano (c-u1)'], commits: ['c-u1'], alBacklog: [grande] }),
  }), { plan: PLAN, modo: 'cerrar' });
  const u = promptDe(llamadas, 'última revisión');
  assert.match(u, /Lo chico y seguro lo arreglás vos.*«Tarea: prueba\/ultima-revision»/);
  assert.match(u, /va en alBacklog \(que, porQue y donde\)\. No hay otra ronda/);
  assert.equal(etiquetas.filter((e) => e === 'última revisión').length, 1);
  assert.match(promptDe(llamadas, 'cierre'), /- \*\*partir RoleService\.\*\* pasa la complejidad\. Dónde: RoleService\.cs \(ultima-revision\)\./);
  assert.deepEqual(resultado.ultimaRevision.commits, ['c-u1']);
});

test('conductor: si un arreglo queda bloqueado o un revisor no termina, no se cierra: el cierre solo deja al día el plan', async () => {
  const base = respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })]));
  const casos = [
    { motivo: /los arreglos del back quedaron bloqueada: el test no se deja arreglar/, r: { 'revisión · Arquitectura, clean code y SOLID': { hallazgos: [hallazgoDe('A1')], resumen: '' }, 'arreglos · back': commit('arreglo', { estado: 'bloqueada', motivo: 'el test no se deja arreglar' }) } },
    { motivo: /no terminaron: Lo que no está en la spec/, r: { 'revisión · Lo que no está en la spec': null } },
    { motivo: /no terminaron: .*Tests enteros/, r: { 'tests enteros': null } },
    { motivo: /la última revisión no terminó/, r: { 'última revisión': null } },
  ];
  for (const caso of casos) {
    const { llamadas, resultado } = await correr(async (label, prompt) => (label in caso.r ? caso.r[label] : base(label, prompt)), { plan: PLAN, modo: 'cerrar' });
    const cierre = promptDe(llamadas, 'cierre');
    assert.match(cierre, /La etapa no se cierra: /);
    assert.match(cierre, caso.motivo);
    assert.doesNotMatch(cierre, /git rm/);
    assert.equal(resultado.resultado, 'revisada, sin cerrar');
    assert.match(resultado.pendiente.join(' · '), caso.motivo);
  }
  const sinCierre = await correr(respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })]), { cierre: null }), { plan: PLAN, modo: 'cerrar' });
  assert.equal(sinCierre.resultado.resultado, 'revisada, sin cerrar', 'sin el cierre no se da por cerrada');
});

// ---------- Temas de riesgo ----------

test('conductor: los temas de riesgo salen de los archivos de los commits: uno que no tocan no lleva revisor, aunque el plan lo marque', async () => {
  const lect = lectura([tarea('K1', 'contrato'), tarea('B1', 'back', { riesgo: ['datos', 'permisos'] }), tarea('B2', 'back'), tarea('F1', 'front')]);
  const archivos = archivosDe([RUTA.datos, ['prueba/B1']], [RUTA.servicio, ['prueba/B2']]);
  const { resultado, etiquetas, llamadas, logs } = await correr(respondedor(lect, { 'archivos de la etapa': archivos }), { plan: PLAN });
  assert.ok(etiquetas.includes('revisión · Adversarial · datos'));
  assert.ok(!etiquetas.includes('revisión · Adversarial · permisos'), 'los commits no tocan permisos');
  assert.ok(logs.includes('Revisión adversarial: datos'));
  const adversarial = llamadas.find((l) => l.label === 'revisión · Adversarial · datos');
  assert.equal(adversarial.phase, 'Revisión');
  assert.equal(adversarial.effort, 'high');
  assert.match(adversarial.prompt, /que tocan datos: B1 · tarea B1\./);
  assert.match(adversarial.prompt, /Los archivos que tocan datos: src\/X\.Infrastructure\/Persistence\/Repositories\/RoleRepository\.cs\./);
  assert.match(adversarial.prompt, /- Datos: la migración es portable/);
  assert.doesNotMatch(adversarial.prompt, /B2 · tarea B2/, 'solo las tareas de su tema');
  assert.doesNotMatch(adversarial.prompt, /build y tests en verde/, 'el revisor no recibe la conclusión de quien programó');
  assert.equal(resultado.revision.reparto.origen, 'archivos');
  assert.deepEqual(resultado.revision.reparto.temasDeRiesgo, ['datos']);

  // Si el agente que lista los archivos no termina, van los temas que marca el plan, y no se cierra.
  const sinLista = await correr(respondedor(lect, { 'archivos de la etapa': null }), { plan: PLAN });
  assert.ok(sinLista.etiquetas.includes('revisión · Adversarial · datos') && sinLista.etiquetas.includes('revisión · Adversarial · permisos'));
  assert.ok(!sinLista.etiquetas.some((e) => e.startsWith('revisión · Reglas · ')));
  assert.ok(sinLista.logs.some((m) => /no corre la revisión de reglas, y los temas de riesgo son los que marca el plan/.test(m)));
  assert.match(sinLista.resultado.pendiente.join(' · '), /Revisión de reglas \(no se supo qué archivos cambió la etapa\)/);
  for (const c of CAZADORES) assert.ok(sinLista.etiquetas.includes(`revisión · ${c}`), `${c} corre aunque no se sepa qué archivos cambió`);
  assert.equal(sinLista.resultado.resultado, 'revisada, sin cerrar');
});

test('conductor: sin temas de riesgo no hay revisión adversarial, y lo dice', async () => {
  const lect = lectura([tarea('B1', 'back', { estado: 'hecha' })]);
  const { etiquetas, logs } = await correr(respondedor(lect, { 'archivos de la etapa': archivosDe([RUTA.servicio, ['prueba/B1']]) }), { plan: PLAN, modo: 'cerrar' });
  assert.ok(!etiquetas.some((e) => /Adversarial/.test(e)));
  assert.ok(logs.includes('Los archivos de la etapa no tocan ningún tema de riesgo: no va revisión adversarial'));
});

test('conductor: lo que encuentra un adversarial lo arregla el arreglador de su repo', async () => {
  const lect = lectura([tarea('B1', 'back', { estado: 'hecha', riesgo: ['permisos'] }), tarea('B2', 'back', { estado: 'hecha', riesgo: ['permisos', 'aislamiento'] }), tarea('F1', 'front', { estado: 'hecha' })]);
  const hallazgo = { titulo: 'un permiso ajeno pasa', punto: 'permisos', repo: 'back', archivo: 'RoleService.cs', linea: 12, escenario: 'falta un permiso', regla: 'permisos', porque: 'RAZON-permiso', gravedad: 'alta' };
  const { llamadas } = await correr(respondedor(lect, {
    'archivos de la etapa': archivosDe([RUTA.permisos, ['prueba/B1', 'prueba/B2']], [RUTA.aislamiento, ['prueba/B2']]),
    'revisión · Adversarial · permisos': { hallazgos: [hallazgo], resumen: '' },
  }), { plan: PLAN, modo: 'cerrar' });
  const revisor = promptDe(llamadas, 'revisión · Adversarial · permisos');
  assert.match(revisor, /romper lo que programaron estas tareas, que tocan permisos: B1 · tarea B1; B2 · tarea B2\./);
  assert.match(revisor, /- Permisos: cada ruta declara \[Access\(\.\.\.\)\] o \[PublicSite\].*A quien le falta un permiso recibe 403/);
  assert.match(promptDe(llamadas, 'revisión · Adversarial · aislamiento'), /que tocan aislamiento: B2 · tarea B2\./, 'una premisa por tema');
  const arreglo = llamadas.find((l) => l.label === 'arreglos · back');
  assert.equal(arreglo.effort, undefined, 'el arreglador va con el esfuerzo de la sesión');
  assert.match(arreglo.prompt, /\[alta\] un permiso ajeno pasa \(RoleService\.cs:12\).*Lo vio: Adversarial · permisos/);
});

test('conductor: modelos elige el modelo de cada rol, y sin modelos todos van con el de la sesión', async () => {
  const lect = lectura([tarea('B1', 'back'), tarea('F1', 'front')]);
  const { llamadas } = await correr(respondedor(lect), { plan: PLAN, modo: 'programar', modelos: { front: 'sonnet' } });
  assert.equal(llamadas.find((l) => l.label === 'F1 · programa').model, 'sonnet');
  assert.equal(llamadas.find((l) => l.label === 'B1 · programa').model, undefined);
  const cierre = await correr(respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })])), { plan: PLAN, modo: 'cerrar', modelos: { revisa: 'haiku' } });
  assert.equal(cierre.llamadas.find((l) => l.label === 'revisión · Arquitectura, clean code y SOLID').model, 'haiku');
  assert.equal(cierre.llamadas.find((l) => l.label === 'revisión · Volumen y escala').model, 'haiku', 'los cazadores son revisores');
  assert.equal(cierre.llamadas.find((l) => l.label === 'tests enteros').model, undefined);
  assert.equal(cierre.llamadas.find((l) => l.label === 'última revisión').model, undefined);
});

// ---------- Carriles del back (ADR 0071) ----------

test('conductor: el back va en carriles por área, con las migraciones juntas, y cada carril trabaja en su copia', async () => {
  const lect = lectura([
    tarea('B1', 'back', { area: 'permisos', riesgo: ['permisos'] }),
    tarea('B2', 'back', { area: 'asistente', migracion: true, riesgo: ['datos'] }),
    tarea('B3', 'back', { area: 'permisos' }),
    tarea('B4', 'back', { area: 'identidad', migracion: true }),
    tarea('B5', 'back', { area: 'whatsapp' }),
  ]);
  const { resultado } = await correr(respondedor(lect), { plan: PLAN, modo: 'planificar' });
  assert.deepEqual(resultado.carrilesBack, [
    { nombre: 'back-1', copia: '.local/copias/prueba-back-1', tareas: ['B1', 'B3', 'B5'] },
    { nombre: 'back-2', copia: '.local/copias/prueba-back-2', tareas: ['B2', 'B4'] },
  ]);
  const uno = await correr(respondedor(lect), { plan: PLAN, modo: 'planificar', carrilesBack: 1 });
  assert.deepEqual(uno.resultado.carrilesBack, [{ nombre: 'back', copia: null, tareas: ['B1', 'B2', 'B3', 'B4', 'B5'] }]);
  await assert.rejects(correr(respondedor(lect), { plan: PLAN, carrilesBack: 5 }), /carrilesBack va de 1 a 4/);
});

test('conductor: en una copia se prepara, se entrega y se lee poco; en main, como siempre', async () => {
  const lect = lectura([
    tarea('B1', 'back', { area: 'permisos', archivos: ['src/Domain/Authorization/Permissions.cs', 'tests/X/PermissionTests.cs (nuevo)'] }),
    tarea('B2', 'back', { area: 'asistente', riesgo: ['aislamiento'] }),
    tarea('F1', 'front'),
  ]);
  const { llamadas } = await correr(respondedor(lect), { plan: PLAN, modo: 'programar' });
  const b1 = promptDe(llamadas, 'B1 · programa');
  assert.match(b1, /carril\.mjs preparar \.local\/copias\/prueba-back-\d/);
  assert.match(b1, /carril\.mjs entregar \.local\/copias\/prueba-back-\d/);
  assert.match(b1, /El mapa de la tarea .*Permissions\.cs, tests\/X\/PermissionTests\.cs \(nuevo\)/);
  assert.match(b1, /unos 80 pasos/);
  assert.match(b1, /node scripts\/arneses\/verificar\.mjs/);
  assert.match(b1, /Leé por partes/);
  assert.match(b1, /la integración y la seguridad no los corrés: corren una vez, con todo el código de la etapa, en la revisión/);
  const b2 = promptDe(llamadas, 'B2 · programa');
  assert.match(b2, /carril\.mjs entregar \.local\/copias\/prueba-back-\d/, 'con riesgo también commitea y entrega');
  assert.doesNotMatch(b2, /NO commitees/);
  assert.ok(!llamadas.some((l) => /^B2 · (revisión adversarial|commitea)/.test(l.label)), 'ni un revisor ni otro agente que commitee');
  const f1 = promptDe(llamadas, 'F1 · programa');
  assert.match(f1, /Trabajás en main/);
  assert.match(f1, /node scripts\/verificar\.mjs en el front/);
  assert.doesNotMatch(f1, /carril\.mjs/);
  assert.match(promptDe(llamadas, 'cierre'), /carril\.mjs limpiar prueba-back/, 'el cierre borra las copias de los carriles');
  const solo = await correr(respondedor(lect), { plan: PLAN, modo: 'programar', carrilesBack: 1 });
  assert.match(solo.llamadas.find((l) => l.label === 'B1 · programa').prompt, /Trabajás en main/);
});

test('conductor: un carril espera a otro cuando el plan lo pide, y no hay integración aparte', async () => {
  const orden = [];
  const lect = lectura([tarea('B1', 'back', { area: 'permisos' }), tarea('B2', 'back', { area: 'asistente', espera: ['B1'] }), tarea('F1', 'front')]);
  const resp = respondedor(lect, {
    'B1 · programa': async () => { await new Promise((r) => setTimeout(r, 30)); orden.push('B1'); return programa('B1'); },
    'B2 · programa': () => { orden.push('B2'); return programa('B2'); },
  });
  const { etiquetas, llamadas } = await correr(resp, { plan: PLAN, modo: 'programar' });
  assert.deepEqual(orden, ['B1', 'B2']);
  assert.match(promptDe(llamadas, 'B2 · programa'), /Ya están commiteadas: .*B1 \(c-B1\)/);
  assert.ok(!etiquetas.some((e) => / · integración$|^integrar$/.test(e)), 'los tests enteros corren una vez, en la revisión');
});

test('conductor: los carriles se reparten por lo que falta, y una tarea a medias sigue lo que dejó la corrida anterior', async () => {
  const lect = lectura([
    tarea('B1', 'back', { area: 'permisos', estado: 'hecha' }),
    tarea('B2', 'back', { area: 'permisos', estado: 'hecha', riesgo: ['permisos'] }),
    tarea('B3', 'back', { area: 'permisos', motivo: 'quedó a medias: faltan las listas hijas' }),
    tarea('B4', 'back', { area: 'identidad' }),
    tarea('B5', 'back', { area: 'whatsapp' }),
  ]);
  const { resultado } = await correr(respondedor(lect), { plan: PLAN, modo: 'planificar' });
  assert.deepEqual(resultado.carrilesBack.map((c) => c.tareas), [['B1', 'B2', 'B3', 'B5'], ['B4']], 'lo hecho no pesa');
  const { llamadas } = await correr(respondedor(lect), { plan: PLAN, modo: 'programar' });
  const b3 = promptDe(llamadas, 'B3 · programa');
  assert.match(b3, /git log --grep "Tarea: prueba\/B3" --stat y seguí desde ahí/);
  assert.match(b3, /faltan las listas hijas/);
});

// ---------- Lo que frena antes de revisar ----------

test('conductor: con pasos del usuario antes de revisar, frena antes y los devuelve', async () => {
  const lect = lectura([tarea('B1', 'back')], { pasosDelUsuario: [{ paso: 'cargar los user-secrets de B9', cuando: 'antes-de-revisar', soloMirar: false }] });
  const { resultado, etiquetas } = await correr(respondedor(lect), { plan: PLAN });
  assert.ok(!etiquetas.includes('tests enteros'));
  assert.equal(resultado.resultado, 'espera al usuario antes de revisar');
  assert.deepEqual(resultado.pasosAntesDeRevisar, ['cargar los user-secrets de B9']);
  const despues = await correr(respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })], { pasosDelUsuario: lect.pasosDelUsuario })), { plan: PLAN, modo: 'cerrar', usuarioListo: true });
  assert.ok(despues.etiquetas.includes('tests enteros'));
  assert.ok(!despues.etiquetas.some((e) => / · programa$/.test(e)), 'cerrar no vuelve a programar');
});

test('conductor: cerrar con tareas pendientes no revisa ni gasta un cierre', async () => {
  const lect = lectura([tarea('B1', 'back', { estado: 'hecha' }), tarea('F1', 'front', { estado: 'bloqueada', motivo: 'faltan sus tableros' })]);
  const { resultado, etiquetas } = await correr(respondedor(lect), { plan: PLAN, modo: 'cerrar' });
  assert.deepEqual(etiquetas, ['leer el plan']);
  assert.equal(resultado.resultado, 'faltan tareas');
  assert.match(resultado.faltan[0], /F1: bloqueada, faltan sus tableros/);
});

// ---------- Para después ----------

test('conductor: lo de para después se junta de cada agente y el cierre lo escribe en el backlog', async () => {
  const lect = lectura([tarea('B1', 'back'), tarea('F1', 'front')]);
  const filtro = { que: 'un filtro por fecha', porQue: 'la lista crece rápido.', donde: 'Roles.tsx' };
  const { resultado, llamadas } = await correr(respondedor(lect, {
    'B1 · programa': programa('B1', { paraDespues: [filtro] }),
    'F1 · programa': programa('F1', { paraDespues: [filtro, { que: 'exportar a Excel', porQue: 'lo pidió un cliente.', donde: 'spec §4' }], pregunta: '¿Sin roles, se muestra el vacío?', captura: '.local/vacio.png' }),
  }), { plan: PLAN });
  assert.match(promptDe(llamadas, 'B1 · programa'), /Nunca agregás una tabla, una columna, un filtro, un combo, una ventana emergente, una ruta ni un permiso/);
  assert.match(promptDe(llamadas, 'B1 · programa'), /paraDespues \(que, porQue y donde/);
  assert.deepEqual(resultado.paraDespues.map((p) => `${p.tarea}: ${p.que}`), ['B1: un filtro por fecha', 'F1: exportar a Excel']);
  assert.deepEqual(resultado.preguntas, [{ tarea: 'F1', pregunta: '¿Sin roles, se muestra el vacío?', captura: '.local/vacio.png' }]);
  const cierre = promptDe(llamadas, 'cierre');
  assert.match(cierre, /La etapa no se cierra: preguntas abiertas para el usuario: 1/, 'con una pregunta abierta no se cierra');
  assert.match(cierre, /docs\/backlog\.md, la sección «## Para después de la etapa prueba»/);
  assert.match(cierre, /- \*\*un filtro por fecha\.\*\* la lista crece rápido\. Dónde: Roles\.tsx \(B1\)\./);
  assert.match(cierre, /git diff --quiet -- docs\/backlog\.md/, 'no se lleva cambios de otra sesión');

  const sinNada = await correr(respondedor(lectura([tarea('B1', 'back')])), { plan: PLAN });
  const cierreSinNada = promptDe(sinNada.llamadas, 'cierre');
  assert.match(cierreSinNada, /«## Verificación de la etapa prueba», al final: antes del git rm, .* pasa ahí entera, para la verificación\./);
  assert.doesNotMatch(cierreSinNada, /- \*\*|Para después de la etapa/, 'sin nada para después, solo la sección de verificación');
  assert.deepEqual(sinNada.resultado.paraDespues, []);
});

// ---------- Varios planes juntos ----------

const PA = 'docs/plans/2026-11-20-etapa-a.md';
const PB = 'docs/plans/2026-11-21-etapa-b.md';

/** Con varios planes, la fase 1 lleva la clave de su plan adelante («a · leer el plan»); las fases 2 a 5, no. */
function respondedorDePlanes(porClave, juntas = respondedor(lectura([]))) {
  return async (label, prompt, opts) => {
    const m = label.match(/^([a-z0-9-]+) · (.*)$/);
    if (m && porClave[m[1]]) return porClave[m[1]](m[2], prompt, opts);
    return juntas(label, prompt, opts);
  };
}

test('conductor: con planes, la fase 1 corre para cada uno en orden, y de la 2 a la 5 una sola vez, juntas', async () => {
  const resp = respondedorDePlanes({ a: respondedor(lectura([tarea('B1', 'back')])), b: respondedor(lectura([tarea('B1', 'back')])) });
  const { resultado, etiquetas, llamadas } = await correr(resp, { planes: [PA, PB] });
  assert.equal(resultado.resultado, 'código listo');
  assert.deepEqual(resultado.planes.map((p) => `${p.clave}: ${p.resultado}`), ['a: código listo', 'b: código listo']);
  assert.equal(resultado.frenoEn, null);
  const i = (e) => etiquetas.indexOf(e);
  assert.ok(i('a · B1 · programa') < i('b · leer el plan'), 'uno después del otro');
  assert.ok(i('b · B1 · programa') < i('tests enteros'));
  for (const unaVez of ['archivos de la etapa', 'tests enteros', 'última revisión', 'cierre']) assert.equal(etiquetas.filter((e) => e === unaVez).length, 1, unaVez);
  assert.doesNotMatch(promptDe(llamadas, 'a · leer el plan'), /sigue a/);
  assert.match(promptDe(llamadas, 'b · leer el plan'), /sigue a docs\/plans\/2026-11-20-etapa-a\.md .*no es un bloqueo/);
  assert.match(promptDe(llamadas, 'archivos de la etapa'), /git log --grep "Tarea: a\/" --grep "Tarea: b\/" --name-only/);
  assert.match(promptDe(llamadas, 'tests enteros'), /conformidad --detectar --plan docs\/plans\/2026-11-20-etapa-a\.md y conformidad --detectar --plan docs\/plans\/2026-11-21-etapa-b\.md/);
  assert.match(promptDe(llamadas, 'última revisión'), /las etapas «Etapa de prueba» \(plan docs\/plans\/2026-11-20-etapa-a\.md\), «Etapa de prueba» \(plan docs\/plans\/2026-11-21-etapa-b\.md\), que se revisan y se cierran juntas/);
  const cierre = promptDe(llamadas, 'cierre');
  assert.match(cierre, /Plan docs\/plans\/2026-11-20-etapa-a\.md .*\n[\s\S]*Plan docs\/plans\/2026-11-21-etapa-b\.md/);
  assert.match(cierre, /código listo el <fecha>: se retiran sus planes y sus verificaciones pasan al backlog/);
  assert.match(cierre, /carril\.mjs limpiar a-back y node scripts\/arneses\/carril\.mjs limpiar b-back/);
});

test('conductor: planes frena con una pregunta abierta, un paso del usuario o un bloqueo, y no programa el siguiente', async () => {
  const casos = [
    { motivo: /preguntas abiertas para el usuario: 1/, a: respondedor(lectura([tarea('B1', 'back')]), { 'B1 · programa': programa('B1', { pregunta: '¿Se muestra el vacío?' }) }) },
    { motivo: /el usuario tiene que: probar el envío con Meta/, a: respondedor(lectura([tarea('B1', 'back')], { pasosDelUsuario: [{ paso: 'probar el envío con Meta', cuando: 'despues', soloMirar: false }] })) },
    { motivo: /el usuario tiene que: B9: tarea B9/, a: respondedor(lectura([tarea('B1', 'back'), tarea('B9', 'back', { estado: 'del-usuario' })])) },
    { motivo: /no empezó: la app está levantada/, a: respondedor(lectura([tarea('B1', 'back')], { bloqueos: ['la app está levantada'] })) },
  ];
  for (const caso of casos) {
    const resp = respondedorDePlanes({ a: caso.a, b: respondedor(lectura([tarea('B1', 'back')])) });
    const { resultado, etiquetas } = await correr(resp, { planes: [PA, PB] });
    assert.equal(resultado.resultado, 'frenó');
    assert.equal(resultado.frenoEn.clave, 'a');
    assert.match(resultado.frenoEn.porque.join(' · '), caso.motivo);
    assert.deepEqual(resultado.sinCorrer, [PB]);
    assert.ok(!etiquetas.some((e) => e.startsWith('b · ')), 'el plan siguiente no corre');
  }
  const soloMirar = respondedor(lectura([tarea('B1', 'back')], { pasosDelUsuario: [{ paso: 'la prueba integral del bloque', cuando: 'despues', soloMirar: true }] }));
  const sigue = await correr(respondedorDePlanes({ a: soloMirar, b: respondedor(lectura([tarea('B1', 'back')])) }), { planes: [PA, PB] });
  assert.equal(sigue.resultado.resultado, 'código listo', 'mirar no frena');
});

test('conductor: con planes, si el siguiente no termina su código no se revisa nada, y el cierre deja al día los dos', async () => {
  const b = respondedor(lectura([tarea('B1', 'back')]), { 'B1 · programa': programa('B1', { estado: 'bloqueada', commits: [], motivo: 'falta el tablero' }) });
  const { resultado, etiquetas, llamadas } = await correr(respondedorDePlanes({ a: respondedor(lectura([tarea('B1', 'back')])), b }), { planes: [PA, PB] });
  assert.equal(resultado.frenoEn.clave, 'b');
  assert.ok(!etiquetas.includes('tests enteros') && !etiquetas.includes('archivos de la etapa'), 'la revisión espera el código de todas');
  assert.deepEqual(resultado.planes.map((p) => `${p.clave}: ${p.resultado}`), ['a: sin revisar', 'b: faltan tareas']);
  const cierre = promptDe(llamadas, 'cierre');
  assert.match(cierre, /La etapa no se cierra: faltan tareas de b/);
  assert.match(cierre, /Plan docs\/plans\/2026-11-20-etapa-a\.md[\s\S]*Plan docs\/plans\/2026-11-21-etapa-b\.md/);

  const lectorCaido = await correr(respondedorDePlanes({ a: respondedor(lectura([tarea('B1', 'back')])), b: respondedor(lectura([]), { 'leer el plan': null }) }), { planes: [PA, PB] });
  assert.equal(lectorCaido.resultado.planes[0].resultado, 'código listo', 'lo del plan anterior se revisa y se cierra');
  assert.match(lectorCaido.resultado.frenoEn.porque[0], /no empezó: El agente que lee el plan docs\/plans\/2026-11-21-etapa-b\.md no terminó/);
});

test('conductor: plan solo devuelve el plan, y los argumentos de planes se validan', async () => {
  const { resultado, etiquetas } = await correr(respondedor(lectura([tarea('B1', 'back')])), { plan: PLAN });
  assert.equal(resultado.resultado, 'código listo');
  assert.equal(resultado.planes, undefined, 'con plan devuelve el plan, no la cadena');
  assert.ok(etiquetas.includes('leer el plan'), 'sin la clave adelante');
  await assert.rejects(correr(respondedor(lectura([])), { plan: PLAN, planes: [PA] }), /plan o planes/);
  await assert.rejects(correr(respondedor(lectura([])), { planes: [PA, PB], solo: ['B1'] }), /solo va con un plan/);
  await assert.rejects(correr(respondedor(lectura([])), { planes: [PA, PA] }), /repetido/);
  const lect = lectura([tarea('B1', 'back')]);
  const programar = await correr(respondedorDePlanes({ a: respondedor(lect), b: respondedor(lect) }), { planes: [PA, PB], modo: 'programar' });
  assert.equal(programar.resultado.resultado, 'programadas');
  assert.ok(!programar.etiquetas.includes('tests enteros'));
  const plan = await correr(respondedorDePlanes({ a: respondedor(lect), b: respondedor(lect) }), { planes: [PA, PB], modo: 'planificar' });
  assert.deepEqual(plan.etiquetas, ['a · leer el plan', 'b · leer el plan']);
  assert.equal(plan.resultado.resultado, 'planificados');
  // Cada plan, leer y su tarea; las fases 2 a 5, una vez: el que lista, 2 transversales, 3 cazadores, los tests, 2
  // arreglos, la última y el cierre.
  assert.equal(plan.resultado.agentesEstimados, 2 * (1 + 1) + (1 + 2 + 3 + 1 + 2 + 1 + 1));
});

test('conductor: las fases de meta son exactamente las que usa el script', () => {
  const meta = [...fuente.match(/phases: \[([\s\S]*?)\n  \],/)[1].matchAll(/title: '([^']+)'/g)].map((m) => m[1]);
  const usadas = new Set([...fuente.matchAll(/phase(?:\(|: )'([^']+)'/g)].map((m) => m[1]));
  assert.deepEqual([...usadas].sort(), [...meta].sort());
  assert.deepEqual(meta, ['Programar', 'Revisión', 'Arreglos', 'Última revisión', 'Cierre']);
});
