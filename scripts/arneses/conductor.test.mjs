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

function tarea(id, carril, extra = {}) {
  return { id, titulo: `tarea ${id}`, carril, subcarril: carril, riesgo: [], espera: [], estado: 'pendiente', motivo: '', texto: `texto de ${id}`, ...extra };
}

function lectura(tareas, extra = {}) {
  return {
    etapa: 'Etapa de prueba',
    bloqueos: [],
    cupoSemanal: 50,
    tareas,
    textos: { contrato: 'chat del contrato', back: [{ subcarril: 'back', texto: 'chat del back' }], front: 'chat del front', integracion: 'chat de integración' },
    pasosDelUsuario: [],
    notas: [],
    ...extra,
  };
}

const programa = (id, extra = {}) => ({ estado: 'hecha', commits: [`c-${id}`], archivos: [`${id}.cs`], archivosSinCommit: [], falta: '', motivo: '', pregunta: '', cambiosAlContrato: [], verificacion: 'build y tests en verde', ...extra });
const commit = (id) => ({ estado: 'hecha', commits: [`c2-${id}`], archivos: [`${id}.cs`], archivosSinCommit: [], arreglados: [], descartados: [], motivo: '', pregunta: '', cambiosAlContrato: [], verificacion: 'ok' });
const integrar = { estado: 'hecha', commits: [], diferencias: [], pasosDelUsuario: [], cambiosAlContrato: [], archivosSinCommit: [], motivo: '', verificacion: 'contracts:check en verde' };
const puerta = (fallos = []) => ({ corte: { back: 'b1', front: 'f1' }, copias: { back: 'C:/e2e/back', front: 'C:/e2e/front' }, tabla: [{ paso: 'Back', resultado: 'ok', numero: '10/10' }], fallos, galeria: 'capturas/etapa-prueba', motivo: '' });

/** Responde según la etiqueta; `propias` pisa las respuestas por defecto. */
function respondedor(lect, propias = {}) {
  return async (label, prompt) => {
    if (label in propias) return typeof propias[label] === 'function' ? propias[label](prompt) : propias[label];
    if (label === 'leer el plan') return lect;
    const programa_ = label.match(/^(\S+) · programa/);
    if (programa_) return programa(programa_[1]);
    if (/revisión adversarial|revisión del cierre/.test(label)) return { hallazgos: [], resumen: 'sin hallazgos' };
    const cierra = label.match(/^(\S+) · (corrige|commitea)/);
    if (cierra) return commit(cierra[1]);
    if (label === 'integrar') return integrar;
    if (label === 'puerta' || label === 'puerta · lo que tocan los arreglos') return puerta();
    if (label.startsWith('arreglos de')) return commit('arreglo');
    if (label === 'cierre del plan') return { commit: 'c-cierre', motivo: '' };
    throw new Error(`etiqueta sin respuesta: ${label}`);
  };
}

async function correr(responder, args) {
  const llamadas = [];
  const agent = async (prompt, opts = {}) => {
    llamadas.push({ label: opts.label, phase: opts.phase, effort: opts.effort, prompt });
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
  const resultado = await fn(agent, parallel, pipeline, () => {}, () => {}, args, budget);
  return { resultado, llamadas, etiquetas: llamadas.map((l) => l.label) };
}

const estadoDe = (resultado, id) => resultado.tareas.find((t) => t.id === id);

test('conductor: sin plan falla con el ejemplo, y un modo desconocido también', async () => {
  await assert.rejects(correr(respondedor(lectura([])), {}), /Falta args\.plan/);
  await assert.rejects(correr(respondedor(lectura([])), { plan: PLAN, modo: 'rapido' }), /Modo desconocido/);
});

test('conductor: planificar solo lee, con un agente, y devuelve los carriles y lo que va a costar', async () => {
  const lect = lectura([tarea('K1', 'contrato'), tarea('B1', 'back', { riesgo: ['datos'] }), tarea('B2', 'back'), tarea('F1', 'front'), tarea('I1', 'integracion')]);
  const { resultado, etiquetas } = await correr(respondedor(lect), { plan: PLAN, modo: 'planificar' });
  assert.deepEqual(etiquetas, ['leer el plan']);
  assert.equal(resultado.clave, 'prueba');
  assert.deepEqual(resultado.carriles, { contrato: ['K1'], back: ['B1', 'B2'], front: ['F1'] });
  assert.equal(resultado.agentesEstimados, 1 + 1 + 3 + 1 + 1 + 8);
});

test('conductor: el cupo en 85 % o un bloqueo del plan frenan antes de programar', async () => {
  for (const extra of [{ cupoSemanal: 85 }, { bloqueos: ['la puerta de la etapa anterior no cerró'] }]) {
    const { resultado, etiquetas } = await correr(respondedor(lectura([tarea('B1', 'back')], extra)), { plan: PLAN });
    assert.equal(resultado.resultado, 'no empezó');
    assert.equal(resultado.bloqueos.length, 1);
    assert.deepEqual(etiquetas, ['leer el plan']);
  }
});

test('conductor: una espera del contrato hacia el back es un ciclo y no empieza', async () => {
  const lect = lectura([tarea('K1', 'contrato', { espera: ['B1'] }), tarea('B1', 'back')]);
  const { resultado, etiquetas } = await correr(respondedor(lect), { plan: PLAN });
  assert.match(resultado.bloqueos[0], /ciclo: .*K1.*B1/);
  assert.deepEqual(etiquetas, ['leer el plan']);
});

test('conductor: una tarea con riesgo pasa por programa, revisión adversarial y commit; sin riesgo, solo programa', async () => {
  const lect = lectura([tarea('B1', 'back', { riesgo: ['datos', 'aislamiento'] }), tarea('B2', 'back')]);
  const { llamadas, etiquetas } = await correr(respondedor(lect), { plan: PLAN, modo: 'programar' });
  assert.deepEqual(etiquetas.filter((e) => /^B/.test(e)), ['B1 · programa', 'B1 · revisión adversarial', 'B1 · commitea', 'B2 · programa']);
  const prog = llamadas.find((l) => l.label === 'B1 · programa');
  assert.match(prog.prompt, /NO commitees/);
  const adversarial = llamadas.find((l) => l.label === 'B1 · revisión adversarial');
  assert.match(adversarial.prompt, /- Datos:.*\n- Aislamiento:/);
  assert.doesNotMatch(adversarial.prompt, /build y tests en verde/, 'el revisor no recibe la conclusión de quien programó');
  assert.equal(llamadas.find((l) => l.label === 'B1 · commitea').effort, 'low');
  assert.match(llamadas.find((l) => l.label === 'B2 · programa').prompt, /Tarea: prueba\/B2/);
});

test('conductor: con hallazgos, otro agente corrige y commitea con el esfuerzo de la sesión', async () => {
  const lect = lectura([tarea('B1', 'back', { riesgo: ['permisos'] })]);
  const hallazgo = { titulo: 'un permiso ajeno pasa', punto: 'permisos', archivo: 'RoleService.cs', linea: 12, escenario: 'falta un permiso', prueba: 'RoleTests.Ajeno', gravedad: 'alta' };
  const { llamadas } = await correr(respondedor(lect, { 'B1 · revisión adversarial': { hallazgos: [hallazgo], resumen: '' } }), { plan: PLAN, modo: 'programar' });
  const corrige = llamadas.find((l) => l.label === 'B1 · corrige 1 y commitea');
  assert.ok(corrige);
  assert.equal(corrige.effort, undefined);
  assert.match(corrige.prompt, /\[alta\] un permiso ajeno pasa \(RoleService\.cs:12\)/);
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

test('conductor: lo que espera a una tarea bloqueada no corre, y el resto del carril sigue', async () => {
  const lect = lectura([tarea('B1', 'back'), tarea('B2', 'back', { espera: ['B1'] }), tarea('B3', 'back'), tarea('F1', 'front', { espera: ['B1'] })]);
  const bloqueada = programa('B1', { estado: 'bloqueada', commits: [], pregunta: '¿La difusión cuenta como enviada?', motivo: 'decisión de producto' });
  const { resultado, etiquetas } = await correr(respondedor(lect, { 'B1 · programa': bloqueada }), { plan: PLAN, modo: 'programar' });
  assert.equal(estadoDe(resultado, 'B2').estado, 'sin-correr');
  assert.match(estadoDe(resultado, 'B2').motivo, /espera B1, que quedó bloqueada/);
  assert.equal(estadoDe(resultado, 'F1').estado, 'sin-correr');
  assert.equal(estadoDe(resultado, 'B3').estado, 'hecha');
  assert.deepEqual(resultado.preguntas, [{ tarea: 'B1', pregunta: '¿La difusión cuenta como enviada?' }]);
  assert.ok(!etiquetas.includes('integrar'), 'con tareas sin hacer no se integra');
});

test('conductor: una tarea que deja cambios sin commitear frena su carril', async () => {
  const lect = lectura([tarea('B1', 'back'), tarea('B2', 'back'), tarea('F1', 'front')]);
  const sucia = programa('B1', { estado: 'bloqueada', commits: [], archivosSinCommit: ['B1.cs'] });
  const { resultado } = await correr(respondedor(lect, { 'B1 · programa': sucia }), { plan: PLAN, modo: 'programar' });
  assert.match(estadoDe(resultado, 'B2').motivo, /sin commitear/);
  assert.equal(estadoDe(resultado, 'F1').estado, 'hecha', 'el otro repo sigue');
});

test('conductor: sin cupo no empieza ninguna tarea nueva', async () => {
  const lect = lectura([tarea('B1', 'back'), tarea('B2', 'back')]);
  const { resultado } = await correr(respondedor(lect, { 'B1 · programa': programa('B1', { estado: 'sin-cupo', commits: [] }) }), { plan: PLAN, modo: 'programar' });
  assert.equal(estadoDe(resultado, 'B2').estado, 'sin-correr');
  assert.match(resultado.detenido, /cupo/);
});

test('conductor: una tarea a medias sigue con otro agente, hasta tres', async () => {
  const lect = lectura([tarea('B1', 'back')]);
  let vuelta = 0;
  const resp = respondedor(lect, {});
  const { resultado, llamadas } = await correr(async (label, prompt) => {
    if (/^B1 · programa/.test(label)) return programa('B1', { estado: 'parcial', falta: `falta el paso ${++vuelta}` });
    return resp(label, prompt);
  }, { plan: PLAN, modo: 'programar' });
  assert.deepEqual(llamadas.filter((l) => /^B1/.test(l.label)).map((l) => l.label), ['B1 · programa', 'B1 · programa (2)', 'B1 · programa (3)']);
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

test('conductor: con la etapa completa integra, revisa los dos repos, corre la puerta una vez y cierra', async () => {
  const lect = lectura([tarea('K1', 'contrato'), tarea('B1', 'back'), tarea('F1', 'front'), tarea('I1', 'integracion')]);
  const { resultado, etiquetas } = await correr(respondedor(lect), { plan: PLAN });
  assert.deepEqual(etiquetas.slice(-5), ['integrar', 'revisión del cierre · back', 'revisión del cierre · front', 'puerta', 'cierre del plan']);
  assert.equal(resultado.resultado, 'puerta corrida');
  assert.ok(!etiquetas.includes('I1 · programa'), 'la integración la hacen sus fases, no un carril');
});

test('conductor: con pasos del usuario antes de la puerta, frena antes y los devuelve', async () => {
  const lect = lectura([tarea('B1', 'back')], { pasosDelUsuario: [{ paso: 'cargar los user-secrets de B9', cuando: 'antes-de-la-puerta' }] });
  const { resultado, etiquetas } = await correr(respondedor(lect), { plan: PLAN });
  assert.ok(!etiquetas.includes('puerta'));
  assert.equal(resultado.resultado, 'espera al usuario antes de la puerta');
  assert.deepEqual(resultado.pasosAntesDeLaPuerta, ['cargar los user-secrets de B9']);
  const despues = await correr(respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })], { pasosDelUsuario: lect.pasosDelUsuario })), { plan: PLAN, modo: 'cerrar', usuarioListo: true });
  assert.ok(despues.etiquetas.includes('puerta'));
  assert.ok(!despues.etiquetas.includes('integrar'), 'cerrar no vuelve a integrar');
});

test('conductor: un fallo de la puerta vuelve a su dueño y se corre solo lo que toca el arreglo', async () => {
  const lect = lectura([tarea('B1', 'back', { estado: 'hecha' })]);
  const fallo = { paso: 'Tests del front', archivo: 'Roles.test.tsx', linea: 40, causa: 'falta el texto del 403', dueno: 'front' };
  const { resultado, etiquetas } = await correr(respondedor(lect, { puerta: puerta([fallo]) }), { plan: PLAN, modo: 'cerrar' });
  assert.deepEqual(etiquetas.slice(-4), ['puerta', 'arreglos de la puerta · front', 'puerta · lo que tocan los arreglos', 'cierre del plan']);
  assert.equal(resultado.resultado, 'puerta corrida');
});

test('conductor: cerrar con tareas pendientes no corre la puerta ni gasta un cierre', async () => {
  const lect = lectura([tarea('B1', 'back', { estado: 'hecha' }), tarea('F1', 'front', { estado: 'bloqueada', motivo: 'faltan sus tableros' })]);
  const { resultado, etiquetas } = await correr(respondedor(lect), { plan: PLAN, modo: 'cerrar' });
  assert.deepEqual(etiquetas, ['leer el plan']);
  assert.equal(resultado.resultado, 'faltan tareas');
  assert.match(resultado.faltan[0], /F1: bloqueada, faltan sus tableros/);
});

test('conductor: las fases de meta son exactamente las que usa el script', () => {
  const meta = [...fuente.match(/phases: \[([\s\S]*?)\n  \],/)[1].matchAll(/title: '([^']+)'/g)].map((m) => m[1]);
  const usadas = new Set([...fuente.matchAll(/phase(?:\(|: )'([^']+)'/g)].map((m) => m[1]));
  for (const f of fuente.matchAll(/correrCarril\([a-z]+, '([^']+)'\)/g)) usadas.add(f[1]);
  assert.deepEqual([...usadas].sort(), [...meta].sort());
});
