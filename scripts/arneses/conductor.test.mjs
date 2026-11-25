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
const PREMISAS = ['Textos y errores', 'Arquitectura, clean code y SOLID', 'Caché, cuotas y módulos', 'Seguridad y aislamiento', 'Lo que no está en la spec'];
const PANTALLAS = 'Pantallas contra el tablero';

function tarea(id, carril, extra = {}) {
  return { id, titulo: `tarea ${id}`, carril, subcarril: carril, riesgo: [], espera: [], estado: 'pendiente', motivo: '', texto: `texto de ${id}`, area: '', migracion: false, archivos: [], ...extra };
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
    if (/revisión adversarial|^revisión · /.test(label)) return { hallazgos: [], resumen: 'sin hallazgos' };
    if (label.startsWith('verifica · ')) return { veredictos: [] };
    const cierra = label.match(/^(\S+) · (corrige|commitea)/);
    if (cierra) return commit(cierra[1]);
    if (label === 'integrar') return integrar;
    if (/^back(-\d+)? · integración$/.test(label)) return commit('carril');
    if (label === 'puerta' || label === 'puerta · lo que tocan los arreglos') return puerta();
    if (label.startsWith('arreglos de')) return commit('arreglo');
    if (label === 'cierre del plan') return { commit: 'c-cierre', motivo: '' };
    throw new Error(`etiqueta sin respuesta: ${label}`);
  };
}

async function correr(responder, args) {
  const llamadas = [];
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
  assert.deepEqual(resultado.carrilesBack.map((c) => c.tareas), [['B1'], ['B2']], 'sin área en común, cada una en su carril');
  // La integración de cada carril (2) y el cierre: integrar, 5 revisores, 5 verificadores, 2 arreglos, la puerta, pantallas y su verificador, 2 arreglos, lo que tocan y el cierre.
  assert.equal(resultado.agentesEstimados, 1 + 1 + 3 + 1 + 1 + 2 + 20);
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
  const lect = lectura([tarea('B1', 'back', { riesgo: ['datos', 'aislamiento'], area: 'roles' }), tarea('B2', 'back', { area: 'roles' })]);
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

test('conductor: con la etapa completa integra, revisa por premisa, corre la puerta una vez y cierra', async () => {
  const lect = lectura([tarea('K1', 'contrato'), tarea('B1', 'back'), tarea('F1', 'front'), tarea('I1', 'integracion'), tarea('I3', 'integracion', { texto: 'el recorrido cubre un rol con un permiso ajeno' })]);
  const { resultado, etiquetas, llamadas } = await correr(respondedor(lect), { plan: PLAN });
  assert.deepEqual(etiquetas.slice(-9), ['integrar', ...PREMISAS.map((p) => `revisión · ${p}`), 'puerta', `revisión · ${PANTALLAS}`, 'cierre del plan']);
  assert.ok(!etiquetas.some((e) => e.startsWith('verifica · ')), 'sin hallazgos no hay nada que verificar');
  assert.equal(resultado.resultado, 'puerta corrida');
  assert.ok(!etiquetas.includes('I1 · programa'), 'la integración la hacen sus fases, no un carril');
  const integra = llamadas.find((l) => l.label === 'integrar').prompt;
  assert.match(integra, /I3 · tarea I3: el recorrido cubre un rol con un permiso ajeno/, 'el integrador deja listos los pasos del recorrido antes de la puerta');
  assert.match(integra, /recorrido-usuario\.mjs/);
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
  assert.deepEqual(etiquetas.slice(-5), ['puerta', `revisión · ${PANTALLAS}`, 'arreglos de la puerta · front', 'puerta · lo que tocan los arreglos', 'cierre del plan']);
  assert.equal(resultado.resultado, 'puerta corrida');
});

test('conductor: cerrar con tareas pendientes no corre la puerta ni gasta un cierre', async () => {
  const lect = lectura([tarea('B1', 'back', { estado: 'hecha' }), tarea('F1', 'front', { estado: 'bloqueada', motivo: 'faltan sus tableros' })]);
  const { resultado, etiquetas } = await correr(respondedor(lect), { plan: PLAN, modo: 'cerrar' });
  assert.deepEqual(etiquetas, ['leer el plan']);
  assert.equal(resultado.resultado, 'faltan tareas');
  assert.match(resultado.faltan[0], /F1: bloqueada, faltan sus tableros/);
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
  const prompt = (label) => llamadas.find((l) => l.label === label).prompt;
  const b1 = prompt('B1 · programa');
  assert.match(b1, /carril\.mjs preparar \.local\/copias\/prueba-back-\d/);
  assert.match(b1, /carril\.mjs entregar \.local\/copias\/prueba-back-\d/);
  assert.match(b1, /El mapa de la tarea .*Permissions\.cs, tests\/X\/PermissionTests\.cs \(nuevo\)/);
  assert.match(b1, /unos 80 pasos/);
  assert.match(b1, /node scripts\/arneses\/verificar\.mjs/);
  assert.match(b1, /Leé por partes/);
  assert.doesNotMatch(b1, /también los tests de integración y de seguridad de esa área/, 'la integración corre al cerrar el carril');
  assert.match(prompt('B2 · programa'), /NO commitees\. Dejá todo en el árbol de la copia/, 'con revisión adversarial no commitea ni entrega');
  assert.match(prompt('B2 · revisión adversarial'), /la copia \.local\/copias\/prueba-back-\d del back/);
  assert.match(prompt('B2 · commitea'), /carril\.mjs entregar/);
  const f1 = prompt('F1 · programa');
  assert.match(f1, /Trabajás en main/);
  assert.match(f1, /node scripts\/verificar\.mjs en el front/);
  assert.doesNotMatch(f1, /carril\.mjs/);
  assert.match(prompt('integrar'), /carril\.mjs limpiar prueba-back/);
  const solo = await correr(respondedor(lect), { plan: PLAN, modo: 'programar', carrilesBack: 1 });
  assert.match(solo.llamadas.find((l) => l.label === 'B1 · programa').prompt, /Trabajás en main/);
});

test('conductor: un carril espera a otro cuando el plan lo pide, y la integración corre una vez por carril', async () => {
  const orden = [];
  const lect = lectura([tarea('B1', 'back', { area: 'permisos' }), tarea('B2', 'back', { area: 'asistente', espera: ['B1'] }), tarea('F1', 'front')]);
  const resp = respondedor(lect, {
    'B1 · programa': async () => { await new Promise((r) => setTimeout(r, 30)); orden.push('B1'); return programa('B1'); },
    'B2 · programa': () => { orden.push('B2'); return programa('B2'); },
  });
  const { resultado, etiquetas, llamadas } = await correr(resp, { plan: PLAN, modo: 'programar' });
  assert.deepEqual(orden, ['B1', 'B2']);
  assert.deepEqual(etiquetas.filter((e) => / · integración$/.test(e)).sort(), ['back-1 · integración', 'back-2 · integración']);
  const integra = llamadas.find((l) => l.label === 'back-1 · integración').prompt;
  assert.match(integra, /verificar\.mjs --integracion --espacio/);
  assert.match(integra, /Tarea: prueba\/back-1/);
  assert.equal(resultado.integracionDeCarriles.length, 2);
  assert.ok(etiquetas.includes('integrar'));
});

test('conductor: si la integración de un carril queda en rojo, no se integra ni se cierra', async () => {
  const lect = lectura([tarea('B1', 'back', { area: 'permisos' }), tarea('F1', 'front')]);
  const mal = { ...commit('carril'), estado: 'bloqueada', motivo: 'RoleTests.Ajeno sigue en rojo' };
  const { resultado, etiquetas } = await correr(respondedor(lect, { 'back · integración': mal }), { plan: PLAN });
  assert.ok(!etiquetas.includes('integrar'));
  assert.ok(!etiquetas.includes('puerta'));
  assert.equal(resultado.resultado, 'faltan tareas');
  assert.match(resultado.faltan.join(' · '), /integración del carril back: bloqueada, RoleTests\.Ajeno sigue en rojo/);
});

test('conductor: modelos elige el modelo de cada rol, y sin modelos todos van con el de la sesión', async () => {
  const lect = lectura([tarea('B1', 'back'), tarea('F1', 'front')]);
  const { llamadas } = await correr(respondedor(lect), { plan: PLAN, modo: 'programar', modelos: { front: 'sonnet' } });
  assert.equal(llamadas.find((l) => l.label === 'F1 · programa').model, 'sonnet');
  assert.equal(llamadas.find((l) => l.label === 'B1 · programa').model, undefined);
  const base = respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })]));
  const cierre = await correr(async (label, prompt) => {
    if (label === 'revisión · Textos y errores') return { hallazgos: [hallazgoDe('TXT')], resumen: '' };
    if (label.startsWith('verifica · ')) return { veredictos: [{ numero: 1, confirmado: true, motivo: 'se ve' }] };
    return base(label, prompt);
  }, { plan: PLAN, modo: 'cerrar', modelos: { verifica: 'haiku' } });
  assert.equal(cierre.llamadas.find((l) => l.label === 'verifica · Textos y errores').model, 'haiku');
  assert.equal(cierre.llamadas.find((l) => l.label === 'revisión · Textos y errores').model, undefined);
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
  const b3 = llamadas.find((l) => l.label === 'B3 · programa').prompt;
  assert.match(b3, /git log --grep "Tarea: prueba\/B3" --stat y seguí desde ahí/);
  assert.match(b3, /faltan las listas hijas/);
});

// ---------- Revisión por premisa ----------

const hallazgoDe = (titulo, repo = 'back') => ({ titulo, punto: '4', repo, archivo: `${titulo}.cs`, linea: 3, escenario: `pasa ${titulo}`, regla: 'guardado', porque: `RAZON-${titulo}`, gravedad: 'media' });

test('conductor: un revisor por premisa sobre los dos repos, cada uno con su verificador y su esfuerzo', async () => {
  const base = respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })]));
  const { llamadas, resultado } = await correr(async (label, prompt) => {
    const rev = label.match(/^revisión · (.+)$/);
    if (rev) return { hallazgos: [hallazgoDe(rev[1])], resumen: '' };
    if (label.startsWith('verifica · ')) return { veredictos: [{ numero: 1, confirmado: true, motivo: 'se ve en el código' }] };
    return base(label, prompt);
  }, { plan: PLAN, modo: 'cerrar' });
  const revisores = llamadas.filter((l) => /^revisión · /.test(l.label));
  const verificadores = llamadas.filter((l) => /^verifica · /.test(l.label));
  assert.deepEqual(revisores.map((l) => l.label), [...PREMISAS, PANTALLAS].map((p) => `revisión · ${p}`));
  assert.deepEqual(verificadores.map((l) => l.label).sort(), [...PREMISAS, PANTALLAS].map((p) => `verifica · ${p}`).sort());
  assert.deepEqual(revisores.map((l) => l.phase), ['Revisión', 'Revisión', 'Revisión', 'Revisión', 'Revisión', 'Puerta']);
  for (const l of [...revisores, ...verificadores]) assert.ok(l.effort, `${l.label} lleva su esfuerzo`);
  for (const l of revisores) {
    assert.match(l.prompt, /git log --grep "Tarea: prueba\/"/);
    assert.match(l.prompt, /ArquitecturaBaseMutitenantFront/, 'cada revisor ve los dos repos');
  }
  const de = (titulo) => revisores.find((l) => l.label === `revisión · ${titulo}`).prompt;
  assert.match(de('Textos y errores'), /14 \(cada error en pantalla tiene un solo dueño\)/);
  assert.doesNotMatch(de('Textos y errores'), /cache\.md|16 \(/, 'solo las reglas y los puntos de su premisa');
  assert.match(de('Arquitectura, clean code y SOLID'), /revisar-arquitectura\.md/);
  assert.match(de('Seguridad y aislamiento'), /16 \(lo que la pantalla ofrece/);
  assert.match(de('Lo que no está en la spec'), /node scripts\/arneses\/arnes\.mjs conformidad --detectar --plan docs\/plans\/2026-11-19-etapa-prueba\.md/);
  for (const l of verificadores) assert.doesNotMatch(l.prompt, /RAZON-/, 'el verificador no recibe el razonamiento del revisor');
  const arreglo = llamadas.filter((l) => l.label === 'arreglos de la revisión · back');
  assert.equal(arreglo.length, 1, 'un solo escritor por repo');
  for (const p of PREMISAS) assert.match(arreglo[0].prompt, new RegExp(`\\] ${p.replace(/[()]/g, '\\$&')} \\(`));
  assert.equal(resultado.revision.premisas.length, 6);
});

test('conductor: lo que el verificador no confirma no llega al arreglo', async () => {
  const base = respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })]));
  const { llamadas, etiquetas, resultado } = await correr(async (label, prompt) => {
    if (label === 'revisión · Textos y errores') return { hallazgos: [hallazgoDe('TXT', 'front')], resumen: '' };
    if (label === 'revisión · Arquitectura, clean code y SOLID') return { hallazgos: [hallazgoDe('A1'), hallazgoDe('A2')], resumen: '' };
    if (label === 'revisión · Seguridad y aislamiento') return { hallazgos: [hallazgoDe('S1')], resumen: '' };
    if (label === 'verifica · Textos y errores') return { veredictos: [{ numero: 1, confirmado: false, motivo: 'el texto sale del resx' }] };
    if (label === 'verifica · Arquitectura, clean code y SOLID') return { veredictos: [{ numero: 1, confirmado: false, motivo: 'otra pieza lo resuelve' }, { numero: 2, confirmado: true, motivo: 'reescribe las opciones' }] };
    if (label === 'verifica · Seguridad y aislamiento') return null;
    return base(label, prompt);
  }, { plan: PLAN, modo: 'cerrar' });
  assert.ok(!etiquetas.includes('arreglos de la revisión · front'), 'sin confirmados en el front no hay arreglo');
  assert.ok(!etiquetas.includes('verifica · Caché, cuotas y módulos'), 'sin hallazgos no se verifica');
  const arreglo = llamadas.find((l) => l.label === 'arreglos de la revisión · back').prompt;
  assert.match(arreglo, /A2 \(A2\.cs:3\).*Lo confirmó un verificador: reescribe las opciones/);
  assert.doesNotMatch(arreglo, /A1|S1|TXT/);
  const premisa = (id) => resultado.revision.premisas.find((p) => p.id === id);
  assert.deepEqual(premisa('textos').descartados, [{ titulo: 'TXT', motivo: 'el texto sale del resx' }]);
  assert.equal(premisa('arquitectura').confirmados, 1);
  assert.equal(premisa('seguridad').confirmados, null, 'una verificación que no terminó no confirma nada');
});

test('conductor: el revisor de pantallas corre después de la galería, y sus arreglos van con los de la puerta', async () => {
  const fallo = { paso: 'Tests del back', archivo: 'RoleTests.cs', linea: 8, causa: 'falta el 404', dueno: 'back' };
  const base = respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })]), { puerta: puerta([fallo]) });
  const { llamadas, etiquetas } = await correr(async (label, prompt) => {
    if (label === `revisión · ${PANTALLAS}`) return { hallazgos: [hallazgoDe('el filtro no es el de shared', 'front')], resumen: '' };
    if (label === `verifica · ${PANTALLAS}`) return { veredictos: [{ numero: 1, confirmado: true, motivo: 'la captura lo muestra' }] };
    return base(label, prompt);
  }, { plan: PLAN, modo: 'cerrar' });
  const i = (e) => etiquetas.indexOf(e);
  assert.ok(i('puerta') < i(`revisión · ${PANTALLAS}`) && i(`revisión · ${PANTALLAS}`) < i(`verifica · ${PANTALLAS}`));
  assert.ok(i(`verifica · ${PANTALLAS}`) < i('arreglos de la puerta · front'));
  assert.ok(i('arreglos de la puerta · back') < i('puerta · lo que tocan los arreglos'));
  assert.ok(i('arreglos de la puerta · front') < i('puerta · lo que tocan los arreglos'));
  assert.equal(etiquetas.filter((e) => e === 'arreglos de la puerta · front').length, 1);
  assert.ok(!etiquetas.includes('arreglos de la revisión · front'));
  const pantallas = llamadas.find((l) => l.label === `revisión · ${PANTALLAS}`);
  assert.equal(pantallas.phase, 'Puerta');
  assert.match(pantallas.prompt, /capturas\/etapa-prueba/);
  assert.match(llamadas.find((l) => l.label === 'arreglos de la puerta · front').prompt, /el filtro no es el de shared/);
  assert.match(llamadas.find((l) => l.label === 'arreglos de la puerta · back').prompt, /RoleTests\.cs:8\. falta el 404/);
  assert.match(llamadas.find((l) => l.label === 'puerta · lo que tocan los arreglos').prompt, /sus casos de la galería/);

  const sinGaleria = await correr(respondedor(lectura([tarea('B1', 'back', { estado: 'hecha' })]), { puerta: { ...puerta(), galeria: '' } }), { plan: PLAN, modo: 'cerrar' });
  assert.ok(!sinGaleria.etiquetas.includes(`revisión · ${PANTALLAS}`), 'sin galería no hay pantallas que mirar');
});

// ---------- Para después ----------

test('conductor: lo de para después se junta de cada agente y el cierre lo escribe en el backlog', async () => {
  const lect = lectura([tarea('B1', 'back'), tarea('F1', 'front')]);
  const filtro = { que: 'un filtro por fecha', porQue: 'la lista crece rápido.', donde: 'Roles.tsx' };
  const { resultado, llamadas } = await correr(respondedor(lect, {
    'B1 · programa': programa('B1', { paraDespues: [filtro] }),
    'F1 · programa': programa('F1', { paraDespues: [filtro, { que: 'exportar a Excel', porQue: 'lo pidió un cliente.', donde: 'spec §4' }], pregunta: '¿Sin roles, se muestra el vacío?', captura: '.local/vacio.png' }),
    integrar: { ...integrar, paraDespues: [{ que: 'una columna de la empresa', porQue: 'el contrato ya la trae.', donde: 'RoleResponse' }] },
  }), { plan: PLAN });
  assert.match(llamadas.find((l) => l.label === 'B1 · programa').prompt, /Nunca agregás una tabla, una columna, un filtro, un combo, una ventana emergente, una ruta ni un permiso/);
  assert.match(llamadas.find((l) => l.label === 'B1 · programa').prompt, /paraDespues \(que, porQue y donde/);
  assert.deepEqual(resultado.paraDespues.map((p) => `${p.tarea}: ${p.que}`), ['B1: un filtro por fecha', 'F1: exportar a Excel', 'integracion: una columna de la empresa']);
  assert.deepEqual(resultado.preguntas, [{ tarea: 'F1', pregunta: '¿Sin roles, se muestra el vacío?', captura: '.local/vacio.png' }]);
  const cierre = llamadas.find((l) => l.label === 'cierre del plan').prompt;
  assert.match(cierre, /docs\/backlog\.md, la sección «## Para después de la etapa prueba»/);
  assert.match(cierre, /- \*\*un filtro por fecha\.\*\* la lista crece rápido\. Dónde: Roles\.tsx \(B1\)\./);
  assert.match(cierre, /git diff --quiet -- docs\/backlog\.md/, 'no se lleva cambios de otra sesión');

  const sinNada = await correr(respondedor(lectura([tarea('B1', 'back')])), { plan: PLAN });
  assert.doesNotMatch(sinNada.llamadas.find((l) => l.label === 'cierre del plan').prompt, /backlog/);
  assert.deepEqual(sinNada.resultado.paraDespues, []);
});

// ---------- Varios planes seguidos ----------

const PA = 'docs/plans/2026-11-20-etapa-a.md';
const PB = 'docs/plans/2026-11-21-etapa-b.md';

/** Con varios planes, cada etiqueta lleva la clave de su plan adelante: «a · leer el plan». */
function respondedorDePlanes(porClave) {
  return async (label, prompt, opts) => {
    const m = label.match(/^([a-z0-9-]+) · (.*)$/);
    if (!m || !porClave[m[1]]) throw new Error(`etiqueta sin plan: ${label}`);
    return porClave[m[1]](m[2], prompt, opts);
  };
}

test('conductor: planes sigue con el siguiente cuando la puerta pasa en verde, y devuelve cada plan', async () => {
  const resp = respondedorDePlanes({ a: respondedor(lectura([tarea('B1', 'back')])), b: respondedor(lectura([tarea('B1', 'back')])) });
  const { resultado, etiquetas, llamadas } = await correr(resp, { planes: [PA, PB] });
  assert.equal(resultado.resultado, 'todos con la puerta en verde');
  assert.deepEqual(resultado.planes.map((p) => `${p.clave}: ${p.resultado}`), ['a: puerta corrida', 'b: puerta corrida']);
  assert.equal(resultado.frenoEn, null);
  assert.ok(etiquetas.indexOf('a · cierre del plan') < etiquetas.indexOf('b · leer el plan'), 'uno después del otro');
  assert.doesNotMatch(llamadas.find((l) => l.label === 'a · leer el plan').prompt, /sigue a/);
  assert.match(llamadas.find((l) => l.label === 'b · leer el plan').prompt, /sigue a docs\/plans\/2026-11-20-etapa-a\.md .*no es un bloqueo/);
});

test('conductor: planes frena con la puerta en rojo, una pregunta abierta o un paso del usuario', async () => {
  const fallo = { paso: 'E2E real', archivo: 'roles.spec.ts', linea: 5, causa: 'no carga', dueno: 'front' };
  const casos = [
    { motivo: /la puerta no pasó en verde: puerta con fallos/, a: respondedor(lectura([tarea('B1', 'back')]), { puerta: puerta([fallo]), 'puerta · lo que tocan los arreglos': puerta([fallo]) }) },
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
  const lectorCaido = await correr(respondedorDePlanes({ a: respondedor(lectura([tarea('B1', 'back')])), b: respondedor(lectura([]), { 'leer el plan': null }) }), { planes: [PA, PB] });
  assert.equal(lectorCaido.resultado.planes[0].resultado, 'puerta corrida', 'lo del plan anterior no se pierde');
  assert.match(lectorCaido.resultado.frenoEn.porque[0], /no empezó: El agente que lee el plan docs\/plans\/2026-11-21-etapa-b\.md no terminó/);
  const soloMirar = respondedor(lectura([tarea('B1', 'back')], { pasosDelUsuario: [{ paso: 'mirar la galería', cuando: 'despues', soloMirar: true }] }));
  const sigue = await correr(respondedorDePlanes({ a: soloMirar, b: respondedor(lectura([tarea('B1', 'back')])) }), { planes: [PA, PB] });
  assert.equal(sigue.resultado.resultado, 'todos con la puerta en verde', 'mirar la galería no frena');
});

test('conductor: plan solo sigue como antes, y los argumentos de planes se validan', async () => {
  const { resultado, etiquetas } = await correr(respondedor(lectura([tarea('B1', 'back')])), { plan: PLAN });
  assert.equal(resultado.resultado, 'puerta corrida');
  assert.equal(resultado.planes, undefined, 'con plan devuelve el plan, no la cadena');
  assert.ok(etiquetas.includes('leer el plan'), 'sin la clave adelante');
  await assert.rejects(correr(respondedor(lectura([])), { plan: PLAN, planes: [PA] }), /plan o planes/);
  await assert.rejects(correr(respondedor(lectura([])), { planes: [PA, PB], modo: 'programar' }), /Con planes, el modo es todo o planificar/);
  await assert.rejects(correr(respondedor(lectura([])), { planes: [PA, PB], solo: ['B1'] }), /solo va con un plan/);
  await assert.rejects(correr(respondedor(lectura([])), { planes: [PA, PA] }), /repetido/);
  const lect = lectura([tarea('B1', 'back')]);
  const plan = await correr(respondedorDePlanes({ a: respondedor(lect), b: respondedor(lect) }), { planes: [PA, PB], modo: 'planificar' });
  assert.deepEqual(plan.etiquetas, ['a · leer el plan', 'b · leer el plan']);
  assert.equal(plan.resultado.resultado, 'planificados');
  assert.equal(plan.resultado.agentesEstimados, 2 * (1 + 1 + 1 + 20));
});

test('conductor: las fases de meta son exactamente las que usa el script', () => {
  const meta = [...fuente.match(/phases: \[([\s\S]*?)\n  \],/)[1].matchAll(/title: '([^']+)'/g)].map((m) => m[1]);
  const usadas = new Set([...fuente.matchAll(/phase(?:\(|: )'([^']+)'/g)].map((m) => m[1]));
  for (const f of fuente.matchAll(/correrCarril\([a-z]+, '([^']+)'\)/g)) usadas.add(f[1]);
  assert.deepEqual([...usadas].sort(), [...meta].sort());
});
