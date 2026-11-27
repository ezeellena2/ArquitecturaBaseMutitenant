import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

// La verificación de una etapa (.agents/workflows/verificar-etapa.js) corre con agentes falsos: ningún test llama a un modelo.
const ruta = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '.agents', 'workflows', 'verificar-etapa.js');
const fuente = await readFile(ruta, 'utf8');
const AsyncFunction = Object.getPrototypeOf(async () => {}).constructor;
const CLAVE = 'prueba';
const COPIA_FRONT = 'C:/e2e-prueba/ArquitecturaBaseMutitenantFront';
const PASOS = ['Build y contrato', 'E2E real', 'Galería', 'Recorrido a 1440', 'Recorrido a 390'];
const PANTALLAS = 'Pantallas contra el tablero';

function lectura(extra = {}) {
  return {
    etapa: 'Etapa de prueba',
    numero: '12',
    plan: 'docs/plans/2026-11-08-etapa-prueba.md',
    spec: 'docs/plans/formularios/12-prueba.md',
    origen: 'backlog',
    donde: 'docs/backlog.md, «## Verificación de la etapa prueba»',
    seccion: '- **E2E real:** CASO-UNO.\n- **Galería** (scripts/capturas/etapa-12.json).',
    corte: { back: 'aaaaaaaa11', front: 'bbbbbbbb22' },
    estadoEnHojaDeRuta: 'Código listo el 2026-11-28; falta la verificación',
    galeria: 'scripts/capturas/etapa-12.json',
    quitarModulo: '',
    aMano: [],
    bloqueos: [],
    cupoSemanal: 40,
    notas: [],
    ...extra,
  };
}

const casos = (extra = {}) => ({ estado: 'hecha', commits: { back: [], front: ['cf1', 'cf2'] }, archivos: ['scripts/capturas/etapa-12.json'], galeria: 'scripts/capturas/etapa-12.json', aMano: [], archivosSinCommit: [], motivo: '', verificacion: 'node --test en verde', ...extra });
const paso = (id, extra = {}) => ({ paso: id, resultado: 'verde', numero: '10/10', compila: true, fallos: [], galeria: id === 'galeria' ? `${COPIA_FRONT}/docs/design/capturas/etapa-12` : '', aMano: [], salida: '.local/x.log', motivo: '', ...extra });
const fallo = (archivo, dueno, extra = {}) => ({ paso: 'E2E real', archivo, linea: 7, causa: `rompe ${archivo}`, dueno, ...extra });
const commit = (extra = {}) => ({ estado: 'hecha', commits: ['arr1'], archivos: [], archivosSinCommit: [], arreglados: ['uno (arr1)'], descartados: [], motivo: '', pregunta: '', cambiosAlContrato: [], verificacion: 'ok', ...extra });
const hallazgo = (titulo, repo = 'front') => ({ titulo, punto: '13', repo, archivo: `${titulo}.tsx`, linea: 4, escenario: `se ve ${titulo}`, regla: 'pantallas-y-ui', porque: 'la captura', gravedad: 'media' });
const idDe = { 'Build y contrato': 'build', 'E2E real': 'e2e', Galería: 'galeria', 'Recorrido a 1440': 'recorrido-1440', 'Recorrido a 390': 'recorrido-390', 'Quitar el módulo': 'quitar-modulo' };

/** Responde según la etiqueta; `propias` pisa las respuestas por defecto. */
function respondedor(propias = {}, lect = lectura()) {
  return async (label, prompt) => {
    if (label in propias) return typeof propias[label] === 'function' ? propias[label](prompt) : propias[label];
    if (label === 'leer la sección') return lect;
    if (label === 'escribir los casos') return casos();
    if (label === 'armar las copias') return { listas: true, motivo: '' };
    const p = label.match(/^paso · (.+)$/);
    if (p) return paso(idDe[p[1]]);
    if (label === `revisión · ${PANTALLAS}`) return { hallazgos: [], resumen: 'igual al tablero' };
    if (label.startsWith('arreglos · ')) return commit();
    if (label === 'repetir lo que tocan los arreglos') return { repetidos: [], motivo: '' };
    if (label === 'cierre') return { commits: ['c-cierre'], verificada: true, galeria: 'docs/design/capturas/etapa-12', copiasBorradas: true, motivo: '' };
    throw new Error(`etiqueta sin respuesta: ${label}`);
  };
}

async function correr(responder, args = { clave: CLAVE }) {
  const llamadas = [];
  const logs = [];
  const agent = async (prompt, opts = {}) => {
    llamadas.push({ label: opts.label, phase: opts.phase, effort: opts.effort, prompt });
    return responder(opts.label ?? '', prompt, opts);
  };
  const parallel = (thunks) => Promise.all(thunks.map((t) => Promise.resolve().then(t).catch(() => null)));
  const cuerpo = fuente.replace(/^export const meta = /m, 'const meta = ');
  const fn = new AsyncFunction('agent', 'parallel', 'phase', 'log', 'args', cuerpo);
  const resultado = await fn(agent, parallel, () => {}, (m) => logs.push(m), args);
  return { resultado, llamadas, logs, etiquetas: llamadas.map((l) => l.label) };
}

const promptDe = (llamadas, label) => llamadas.find((l) => l.label === label).prompt;
const filaDe = (resultado, titulo) => resultado.tabla.find((f) => f.paso === titulo);

test('verificar: los argumentos se validan antes de lanzar un agente', async () => {
  await assert.rejects(correr(respondedor(), {}), /Falta args\.plan .* o args\.clave/);
  await assert.rejects(correr(respondedor(), { clave: 'Catálogos 12' }), /minúsculas, números y guiones/);
  await assert.rejects(correr(respondedor(), { plan: 'docs/plans/2026-11-08-etapa-12-catalogos.md', clave: '13-campos' }), /no de 13-campos/);
  await assert.rejects(correr(respondedor(), { clave: CLAVE, corte: { back: 'HEAD' } }), /corte va con los dos commits/);
  await assert.rejects(correr(respondedor(), { clave: CLAVE, topeCupo: 0 }), /topeCupo va de 1 a 100/);
  const conPlan = await correr(respondedor(), { plan: 'docs\\plans\\2026-11-08-etapa-12-catalogos.md' });
  assert.equal(conPlan.resultado.clave, '12-catalogos', 'la clave sale del plan, aunque ya esté retirado');
  assert.match(promptDe(conPlan.llamadas, 'leer la sección'), /plan docs\/plans\/2026-11-08-etapa-12-catalogos\.md/);
});

test('verificar: las cinco fases en orden, cada paso con su agente, y sin fallos no hay arreglos ni repetición', async () => {
  const { resultado, llamadas, etiquetas } = await correr(respondedor());
  const fase = (label) => llamadas.find((l) => l.label === label).phase;
  for (const l of ['leer la sección', 'escribir los casos', 'armar las copias']) assert.equal(fase(l), 'Preparar', l);
  for (const t of PASOS) assert.equal(fase(`paso · ${t}`), 'Correr', t);
  assert.equal(fase(`revisión · ${PANTALLAS}`), 'Correr');
  assert.equal(fase('cierre'), 'Cierre');
  const i = (e) => etiquetas.indexOf(e);
  assert.ok(i('leer la sección') < i('escribir los casos') && i('escribir los casos') < i('armar las copias'), 'los casos se escriben antes de armar las copias');
  assert.ok(i('armar las copias') < i('paso · Build y contrato'));
  assert.ok(i('paso · Build y contrato') < i('paso · E2E real') && i('paso · E2E real') < i('paso · Galería'));
  assert.ok(i('paso · Galería') < i(`revisión · ${PANTALLAS}`) && i('paso · Galería') < i('paso · Recorrido a 1440'));
  assert.ok(i('paso · Recorrido a 1440') < i('paso · Recorrido a 390'));
  assert.equal(etiquetas[etiquetas.length - 1], 'cierre');
  assert.ok(!etiquetas.some((e) => /^arreglos|^repetir|^verifica · |Quitar el módulo/.test(e)), 'sin fallos ni hallazgos, ni quitar el módulo que no se pide');
  assert.equal(resultado.resultado, 'verificada');
  assert.deepEqual(resultado.falta, []);
  assert.deepEqual(resultado.tabla.map((f) => `${f.paso}: ${f.resultado}`), [...PASOS.map((t) => `${t}: verde`), 'Quitar el módulo: no aplica']);
  assert.deepEqual(resultado.copias, { back: 'C:/e2e-prueba/ArquitecturaBaseMutitenant', front: COPIA_FRONT });
  const cierre = promptDe(llamadas, 'cierre');
  assert.match(cierre, /todos los pasos quedaron en verde/);
  assert.match(cierre, /pasa de «Código listo el …; falta la verificación» \(o de «Cerrada el …», como la dejaba el cierre de antes de los dos hitos\) a «Verificada el <fecha>»/);
  assert.match(cierre, /La tabla corta de resultados .* va en la spec de la etapa \(docs\/plans\/formularios\/12-prueba\.md\)/);
  assert.match(cierre, /La sección de verificación sale de docs\/backlog\.md, «## Verificación de la etapa prueba»/);
  assert.match(cierre, /La galería \(C:\/e2e-prueba\/ArquitecturaBaseMutitenantFront\/docs\/design\/capturas\/etapa-12\) pasa al front de main/);
  assert.match(cierre, /borrá C:\/e2e-prueba entera/);
  assert.match(cierre, /estado-docs --detectar y realinear-docs --detectar, y dejalos en 0/);
  assert.match(cierre, /git diff --quiet -- <archivo>/, 'no se lleva cambios de otra sesión');
  assert.match(cierre, /docs\(planes\): Etapa de prueba queda verificada el <fecha>" -m "Tarea: prueba\/verificacion"/);
});

test('verificar: la sección se busca en el plan, después en el backlog con sus dos nombres y por último en el plan retirado', async () => {
  const { llamadas } = await correr(respondedor());
  const leer = promptDe(llamadas, 'leer la sección');
  const a = leer.indexOf('a. En el plan, si sigue en el árbol');
  const b = leer.indexOf('b. En docs/backlog.md');
  const c = leer.indexOf('c. En el plan que retiró git');
  assert.ok(a > 0 && a < b && b < c, 'el orden de búsqueda');
  assert.match(leer, /«## Verificación de la etapa» \(hasta el 2026-11-27 se llamaba «## Al cierre del bloque E12–E25»\)/);
  assert.match(leer, /«## Verificación de la etapa prueba», donde la deja el cierre de etapa\.js, o «## Para después de la etapa prueba», donde la dejaba el cierre de antes de los dos hitos \(así la deja la E12\)/);
  assert.match(leer, /lo demás es para después y no cuenta/);
  assert.match(leer, /git log --diff-filter=D --format=%H -- "docs\/plans\/\*etapa-prueba\.md" y git show <commit>\^:<ruta>/);
  assert.match(leer, /git rev-parse HEAD en el back .* y en el front/, 'sin corte, el HEAD del momento');
  assert.match(leer, /Que en main se esté programando la etapa siguiente.* no es un bloqueo/);
  const casosPrompt = promptDe(llamadas, 'escribir los casos');
  assert.match(casosPrompt, /La sección de verificación, de docs\/backlog\.md, «## Verificación de la etapa prueba»:\n"""\n- \*\*E2E real:\*\* CASO-UNO\./);
  assert.match(casosPrompt, /solo en scripts\/ y con sus tests, sin tocar src\/ \(lo está programando la etapa siguiente\)/);
  assert.match(casosPrompt, /capturar-etapa\.mjs --casos <el JSON> --manifiesto/);
  assert.match(casosPrompt, /-m "Tarea: prueba\/verificacion" -- <rutas>/);

  const conCorte = await correr(respondedor(), { clave: CLAVE, corte: { back: 'abcdef1', front: '1234567' } });
  assert.match(promptDe(conCorte.llamadas, 'leer la sección'), /el que te pasaron, back abcdef1 y front 1234567/);

  for (const [origen, esperado] of [
    ['backlog', /si es «## Para después de la etapa …», salen solo sus líneas de la verificación y las demás se quedan/],
    ['plan', /el plan sigue en el árbol, así que solo se saca la sección/],
    ['plan-retirado', /el plan ya está retirado y la sección no está en el árbol, así que no hay nada que sacar/],
  ]) {
    const r = await correr(respondedor({}, lectura({ origen })));
    assert.match(promptDe(r.llamadas, 'cierre'), esperado, origen);
  }
});

test('verificar: las copias van en C:/e2e-<clave>, en el corte y sin ramas, con los casos encima', async () => {
  const { llamadas } = await correr(respondedor());
  const copias = promptDe(llamadas, 'armar las copias');
  assert.match(copias, /git clone --local del back .* en C:\/e2e-prueba\/ArquitecturaBaseMutitenant, y del front .* en C:\/e2e-prueba\/ArquitecturaBaseMutitenantFront/);
  assert.match(copias, /checkout -q --detach <corte>: back aaaaaaaa11 y front bbbbbbbb22/);
  assert.match(copias, /cherry-pick <commits>, en orden y sin ramas .* Back: ninguno\. Front: cf1, cf2\./);
  assert.match(copias, /npm ci/);
  assert.match(copias, /Sin worktrees ni ramas/);
});

test('verificar: cada paso pesado toma la fila por su cuenta, de a un comando, y la suelta antes del siguiente', async () => {
  const { llamadas } = await correr(respondedor());
  for (const t of PASOS) {
    const p = promptDe(llamadas, `paso · ${t}`);
    assert.match(p, /Nunca encadenes dos comandos en una misma llamada ni la tomes para esperar/, t);
    const comandos = [...p.matchAll(/carril\.mjs fila --dir (\S+) "([^"]*)"/g)].filter((m) => m[1] !== '<copia>');
    if (t === 'Build y contrato') {
      assert.equal(comandos.length, 1, 'el build, en su propia llamada a la fila');
      assert.equal(comandos[0][1], 'C:/e2e-prueba/ArquitecturaBaseMutitenant');
      assert.equal(comandos[0][2], 'dotnet build ArquitecturaBaseMultitenant.slnx');
      assert.doesNotMatch(p, /verificar\.mjs --integracion/, 'la suite de integración ya corrió en la fase 2 de etapa.js');
      assert.match(p, /La suite de integración no corre acá: ya corrió completa en la revisión del código listo/);
      assert.match(p, /npm run contracts:check, que es liviano y va sin fila/);
      continue;
    }
    assert.equal(comandos.length, 1, `${t}: una sola llamada a la fila`);
    assert.equal(comandos[0][1], COPIA_FRONT, t);
    assert.doesNotMatch(comandos[0][2], /&&|;/, `${t}: un solo comando adentro`);
  }
  assert.match(promptDe(llamadas, 'paso · E2E real'), /fila --dir C:\/e2e-prueba\/ArquitecturaBaseMutitenantFront "npm run test:e2e:real"/);
  assert.match(promptDe(llamadas, 'paso · Galería'), /"node scripts\/capturar-etapa\.mjs --casos scripts\/capturas\/etapa-12\.json"/);
  assert.match(promptDe(llamadas, 'paso · Recorrido a 1440'), /"node scripts\/recorrido-usuario\.mjs --no-build --width=1440"/);
  assert.match(promptDe(llamadas, 'paso · Recorrido a 390'), /"node scripts\/recorrido-usuario\.mjs --no-build --width=390"/);
});

test('verificar: los pasos van de a uno, y las pantallas contra el tablero corren a la vez que el recorrido', async () => {
  const activos = new Set();
  let maximo = 0;
  let empezoElRecorrido;
  const recorridoEmpezo = new Promise((r) => { empezoElRecorrido = r; });
  const medir = (id) => async () => {
    activos.add(id);
    maximo = Math.max(maximo, activos.size);
    if (id === 'recorrido-1440') empezoElRecorrido();
    await new Promise((r) => setTimeout(r, 10));
    activos.delete(id);
    return paso(id);
  };
  const resp = respondedor({
    'paso · Build y contrato': medir('build'),
    'paso · E2E real': medir('e2e'),
    'paso · Galería': medir('galeria'),
    'paso · Recorrido a 1440': medir('recorrido-1440'),
    'paso · Recorrido a 390': medir('recorrido-390'),
    [`revisión · ${PANTALLAS}`]: async () => {
      await Promise.race([recorridoEmpezo, new Promise((_, no) => setTimeout(() => no(new Error('el recorrido no corrió a la vez')), 2000))]);
      return { hallazgos: [], resumen: 'igual' };
    },
  });
  const { resultado, llamadas } = await correr(resp);
  assert.equal(maximo, 1, 'nunca dos pasos a la vez');
  assert.equal(resultado.pantallas.resumen, 'igual');
  assert.match(promptDe(llamadas, `revisión · ${PANTALLAS}`), /La galería acaba de quedar en C:\/e2e-prueba\/ArquitecturaBaseMutitenantFront\/docs\/design\/capturas\/etapa-12/);
});

test('verificar: cada fallo va a su dueño, lo de pantallas solo si el verificador lo confirma, y se repite solo lo que tocan los arreglos', async () => {
  const resp = respondedor({
    'paso · E2E real': paso('e2e', { resultado: 'con fallos', numero: '8/10', fallos: [fallo('Forms.cs', 'back'), fallo('test-e2e-real-forms.mjs', 'front')] }),
    [`revisión · ${PANTALLAS}`]: { hallazgos: [hallazgo('el botón va a la derecha'), hallazgo('un color fijo')], resumen: 'dos diferencias' },
    [`verifica · ${PANTALLAS}`]: { veredictos: [{ numero: 1, confirmado: true, motivo: 'se ve' }, { numero: 2, confirmado: false, motivo: 'ya estaba' }] },
    'arreglos · back': commit({ commits: ['ab1'] }),
    'arreglos · front': commit({ commits: ['af1', 'af2'] }),
    'repetir lo que tocan los arreglos': { repetidos: [paso('e2e', { numero: '10/10' }), paso('galeria')], motivo: '' },
  });
  const { resultado, llamadas, etiquetas } = await correr(resp);
  const i = (e) => etiquetas.indexOf(e);
  assert.ok(i('paso · Recorrido a 390') < i('arreglos · back') && i('arreglos · front') < i('repetir lo que tocan los arreglos'));
  assert.ok(i('repetir lo que tocan los arreglos') < i('cierre'));
  const back = promptDe(llamadas, 'arreglos · back');
  assert.match(back, /- E2E real: Forms\.cs:7\. rompe Forms\.cs/);
  assert.doesNotMatch(back, /test-e2e-real-forms|el botón/);
  assert.match(back, /en main, el checkout de siempre, no en la copia/);
  assert.match(back, /con la línea «Tarea: prueba\/verificacion»/);
  const front = promptDe(llamadas, 'arreglos · front');
  assert.match(front, /test-e2e-real-forms\.mjs:7/);
  assert.match(front, /confirmadas por un verificador:\n- \[media\] el botón va a la derecha/);
  assert.doesNotMatch(front, /un color fijo/, 'lo que el verificador no confirma no llega a un arreglo');
  const repetir = promptDe(llamadas, 'repetir lo que tocan los arreglos');
  assert.match(repetir, /- back: ab1\n- front: af1, af2/);
  assert.match(repetir, /cherry-pick <commits> de su repo, en orden y sin ramas/);
  assert.match(repetir, /No traigas el HEAD de main: trae lo de la etapa siguiente/);
  assert.match(repetir, /Lo que no tocan los arreglos no se repite/);
  assert.match(repetir, /- e2e \(E2E real\): con fallos, 8\/10/);
  assert.match(repetir, /fila --dir C:\/e2e-prueba\/ArquitecturaBaseMutitenant "dotnet build ArquitecturaBaseMultitenant\.slnx"/);
  assert.equal(resultado.resultado, 'verificada');
  assert.deepEqual(resultado.repetidos, ['e2e', 'galeria']);
  assert.equal(filaDe(resultado, 'E2E real').numero, '10/10');
  assert.equal(filaDe(resultado, 'E2E real').repetido, true);
  assert.equal(filaDe(resultado, 'Recorrido a 1440').repetido, false, 'lo que no tocaron no se repitió');
  assert.equal(resultado.pantallas.confirmados.length, 1);
  assert.deepEqual(resultado.pantallas.descartados.map((d) => d.titulo), ['un color fijo']);
});

test('verificar: con un paso que sigue fallando, la verificación queda con fallos y dice qué falta', async () => {
  const conFallos = paso('recorrido-390', { resultado: 'con fallos', numero: '120/127', fallos: [fallo('Roles.tsx', 'front', { paso: 'Recorrido a 390' })] });
  const resp = respondedor({
    'paso · Recorrido a 390': conFallos,
    'arreglos · front': commit({ estado: 'bloqueada', commits: [], pregunta: '¿El filtro va en una fila?' }),
    cierre: { commits: ['c-cierre'], verificada: false, galeria: '', copiasBorradas: true, motivo: '' },
  });
  const { resultado, llamadas, etiquetas } = await correr(resp);
  assert.equal(resultado.resultado, 'verificación con fallos');
  assert.ok(!etiquetas.includes('repetir lo que tocan los arreglos'), 'sin commits de arreglos no hay qué repetir');
  assert.match(resultado.falta.join(' · '), /Recorrido a 390: 1 fallos/);
  assert.match(resultado.falta.join(' · '), /los arreglos del front quedaron bloqueada, con una pregunta: ¿El filtro va en una fila\?/);
  const cierre = promptDe(llamadas, 'cierre');
  assert.match(cierre, /que quedó con fallos: Recorrido a 390/);
  assert.match(cierre, /suma «; la verificación del <fecha> quedó con fallos:» y lo que falta/);
  assert.match(cierre, /La sección de verificación .* no se toca: es lo que falta verificar/);
  assert.match(cierre, /borrá C:\/e2e-prueba entera/, 'las copias se borran igual');
  assert.match(cierre, /la verificación quedó con fallos" -m "Tarea: prueba\/verificacion"/);
});

test('verificar: si el back de la copia no compila, no corre ningún otro paso', async () => {
  const resp = respondedor({ 'paso · Build y contrato': paso('build', { resultado: 'con fallos', compila: false, fallos: [fallo('Program.cs', 'back', { paso: 'Build y contrato' })] }) });
  const { resultado, etiquetas } = await correr(resp);
  assert.ok(!etiquetas.some((e) => /^paso · (E2E|Galería|Recorrido)|^revisión · /.test(e)));
  assert.equal(filaDe(resultado, 'E2E real').resultado, 'no corrió');
  assert.match(filaDe(resultado, 'E2E real').motivo, /el back de la copia no quedó compilado/);
  assert.ok(etiquetas.includes('arreglos · back'));
  assert.equal(resultado.resultado, 'verificación con fallos');

  const sinCopias = await correr(respondedor({ 'armar las copias': { listas: false, motivo: 'quedan 9 GB' } }));
  assert.ok(!sinCopias.etiquetas.some((e) => e.startsWith('paso · ')), 'sin copias no corre ningún paso');
  assert.match(sinCopias.resultado.falta.join(' · '), /no corrió \(las copias del corte no quedaron listas: quedan 9 GB\)/);
  assert.ok(sinCopias.etiquetas.includes('cierre'), 'el cierre corre igual, para decir qué falta y borrar las copias');
});

test('verificar: sin galería no hay pantallas que comparar, y quitar el módulo corre solo si la sección lo pide', async () => {
  const sinGaleria = await correr(respondedor({ 'escribir los casos': casos({ galeria: '' }) }, lectura({ galeria: '' })));
  assert.ok(!sinGaleria.etiquetas.some((e) => /Galería|Pantallas/.test(e)));
  assert.equal(filaDe(sinGaleria.resultado, 'Galería').resultado, 'no aplica');
  assert.equal(sinGaleria.resultado.resultado, 'verificada');
  assert.match(promptDe(sinGaleria.llamadas, 'cierre'), /No hay galería que guardar/);

  const conModulo = await correr(respondedor({}, lectura({ quitarModulo: 'docs/guides/quitar-formularios.md' })));
  const quitar = promptDe(conModulo.llamadas, 'paso · Quitar el módulo');
  assert.match(quitar, /según docs\/guides\/quitar-formularios\.md, en una copia descartable aparte: git clone --local C:\/e2e-prueba\/ArquitecturaBaseMutitenant C:\/e2e-prueba\/quitar-modulo/);
  assert.match(quitar, /con --dir C:\/e2e-prueba\/quitar-modulo/);
  const i = (e) => conModulo.etiquetas.indexOf(e);
  assert.ok(i('paso · Recorrido a 390') < i('paso · Quitar el módulo'), 'después del recorrido, de a un paso');
});

test('verificar: un bloqueo o el cupo frenan antes de escribir nada, y lo de a mano va al backlog sin frenar', async () => {
  for (const extra of [{ bloqueos: ['Docker no responde'] }, { cupoSemanal: 85 }, { origen: 'ninguno' }]) {
    const { resultado, etiquetas } = await correr(respondedor({}, lectura(extra)));
    assert.equal(resultado.resultado, 'no empezó');
    assert.equal(resultado.bloqueos.length, 1);
    assert.deepEqual(etiquetas, ['leer la sección']);
  }
  const conTope = await correr(respondedor({}, lectura({ cupoSemanal: 90 })), { clave: CLAVE, topeCupo: 100 });
  assert.equal(conTope.resultado.resultado, 'verificada');

  const aMano = await correr(respondedor({ 'paso · Recorrido a 1440': paso('recorrido-1440', { aMano: ['el ingreso con Google'] }) }, lectura({ aMano: ['la calificación con el modelo real'] })));
  assert.equal(aMano.resultado.resultado, 'verificada', 'lo de a mano no frena la verificación');
  assert.deepEqual(aMano.resultado.aMano, ['la calificación con el modelo real', 'el ingreso con Google']);
  assert.match(promptDe(aMano.llamadas, 'cierre'), /«## Para después de la etapa prueba».*\n- \*\*Para probar a mano en la prueba integral del bloque:\*\* la calificación con el modelo real/);
});

test('verificar: verificada solo si el cierre lo deja escrito', async () => {
  const sinCierre = await correr(respondedor({ cierre: null }));
  assert.equal(sinCierre.resultado.resultado, 'verificación con fallos');
  assert.match(sinCierre.resultado.falta[0], /el cierre no terminó: .* las copias quedan en C:\/e2e-prueba/);
  const casosBloqueados = await correr(respondedor({ 'escribir los casos': casos({ estado: 'bloqueada', motivo: 'falta el tablero FRM-X' }) }));
  assert.match(casosBloqueados.resultado.falta.join(' · '), /los casos de la etapa: falta el tablero FRM-X/);
});

test('verificar: las fases de meta son exactamente las que usa el script', () => {
  const meta = [...fuente.match(/phases: \[([\s\S]*?)\n  \],/)[1].matchAll(/title: '([^']+)'/g)].map((m) => m[1]);
  const usadas = new Set([...fuente.matchAll(/phase(?:\(|: )'([^']+)'/g)].map((m) => m[1]));
  assert.deepEqual([...usadas].sort(), [...meta].sort());
  assert.deepEqual(meta, ['Preparar', 'Correr', 'Arreglos', 'Repetir', 'Cierre']);
});
