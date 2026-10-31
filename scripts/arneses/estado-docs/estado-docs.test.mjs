import assert from 'node:assert/strict';
import { test } from 'node:test';
import * as markdown from '../comun/markdown.mjs';
import * as deriva from './detectores/deriva.mjs';
import * as estados from './detectores/estados.mjs';
import * as formato from './detectores/formato.mjs';
import * as futuro from './detectores/futuro.mjs';
import * as huerfanos from './detectores/huerfanos.mjs';

const PLAN = 'docs/plans/2026-09-27-plan-de-desarrollo.md';
const planDeEtapas = ['| Etapa | Estado |', '|---|---|', '| 8 · WhatsApp | Cerrada |', '| 9 · Endurecimiento | Cerrada |', '| 10 · Operación | Pendiente |'].join('\n');

/** Un repo en memoria: `archivos` es { ruta: texto }, y `commits`, lo que devolvería git por carpeta. */
function contexto({ archivos = {}, cerradas = [8, 9], commits = {}, ultimoDelDoc = {} } = {}) {
  const todos = {
    'AGENTS.md': `[plan](${PLAN})`,
    [PLAN]: `# Hoja de ruta\n\n**Estado:** en curso\n\n[backlog](../backlog.md)\n\n${planDeEtapas}`,
    ...archivos,
  };
  const rutas = Object.keys(todos);
  return {
    repo: 'back',
    etapasCerradas: cerradas,
    markdown,
    archivos: { archivos: rutas, existe: (rel) => rutas.includes(rel) },
    leer: async (rel) => todos[rel],
    crear: (campos) => ({ ...campos }),
    log: async (rutasPedidas, desde) => (desde ? rutasPedidas.flatMap((r) => commits[r] ?? []) : [ultimoDelDoc[rutasPedidas[0]] ?? '']),
  };
}

const tipos = (hallazgos) => hallazgos.map((h) => h.tipo);
const plan = (etapa, estado) => ({ [`docs/plans/2026-10-16-etapa-${etapa}-algo.md`]: `# Etapa ${etapa}\n\n**Estado:** ${estado}\n` });

test('formato: un archivo fuera de las carpetas de la ficha no tiene lugar', async () => {
  const hallazgos = await formato.detectar(contexto({ archivos: { 'docs/reviews/acta.md': '# Acta' } }));
  assert.deepEqual(tipos(hallazgos), ['fuera-de-lugar']);
});

test('formato: los vivos no declaran estado y los índices no cuentan', async () => {
  const archivos = {
    'docs/rules/guardado.md': '# Guardado',
    'docs/rules/README.md': '# Fichas',
    'docs/plans/formularios/README.md': '# Specs',
    'docs/plans/formularios/plantilla.md': '# E<N>',
  };
  assert.deepEqual(await formato.detectar(contexto({ archivos })), []);
});

test('formato: un documento de paso sin estado o con una palabra de otra lista se marca', async () => {
  const sinEstado = await formato.detectar(contexto({ archivos: { 'docs/plans/2026-11-01-algo.md': '# Algo\n\nTexto' } }));
  assert.deepEqual(tipos(sinEstado), ['sin-estado']);
  const otra = await formato.detectar(contexto({ archivos: { 'docs/plans/2026-11-01-algo.md': '# Algo\n\n**Estado:** terminado' } }));
  assert.deepEqual(tipos(otra), ['estado-invalido']);
});

test('formato: el estado se lee aunque el título venga después de una aclaración', async () => {
  const archivos = { 'docs/plans/traspaso/anexo.md': '> Anexo.\n\n# Anexo\n\n**Estado:** insumo · se retira cuando termine\n' };
  assert.deepEqual(await formato.detectar(contexto({ archivos })), []);
});

test('formato: una ficha funcional termina en Carpetas y Lo verifica', async () => {
  const incompleta = await formato.detectar(contexto({ archivos: { 'docs/features/algo.md': '# Algo\n## Conducta\n## Carpetas' } }));
  assert.equal(incompleta.length, 1);
  assert.match(incompleta[0].real, /falta Lo verifica/);
  const completa = { 'docs/features/algo.md': '# Algo\n## Conducta\n## Carpetas\n## Lo verifica' };
  assert.deepEqual(await formato.detectar(contexto({ archivos: completa })), []);
});

test('formato: una decisión lleva estado, contexto y decisión, en las dos formas del estado', async () => {
  const buena = '# 0001\n\nEstado: aceptada el 2026-10-01.\n\n## Contexto\n## Decisión';
  const tambien = '# 0002\n\n**Estado:** reemplazada por la 0003.\n\n## Contexto\n## Decisión';
  assert.deepEqual(await formato.detectar(contexto({ archivos: { 'docs/decisions/0001-uno.md': buena, 'docs/decisions/0002-dos.md': tambien } })), []);
  const mala = await formato.detectar(contexto({ archivos: { 'docs/decisions/0003-tres.md': '# 0003\n\n## Contexto' } }));
  assert.deepEqual(tipos(mala), ['formato-decision']);
  assert.match(mala[0].real, /falta Estado y Decisión/);
});

test('formato: una spec o un plan de etapa largos se marcan, y el plan general no', async () => {
  const largo = Array(4600).fill('palabra').join(' ');
  const archivos = {
    'docs/plans/formularios/15-algo.md': `# E15\n\n**Estado:** pendiente\n\n${largo}`,
    'docs/plans/2026-10-12-hoja-de-ruta.md': `# Hoja\n\n**Estado:** en curso\n\n${largo}`,
  };
  const hallazgos = await formato.detectar(contexto({ archivos }));
  assert.deepEqual(hallazgos.map((h) => h.archivo), ['docs/plans/formularios/15-algo.md']);
  assert.equal(hallazgos[0].tipo, 'muy-largo');
});

test('estados: un plan que dice pendiente de una etapa cerrada se marca, y al revés también', async () => {
  const hallazgos = await estados.detectar(contexto({ archivos: { ...plan(9, 'pendiente'), ...plan(10, 'cerrado') } }));
  assert.deepEqual(tipos(hallazgos).filter((t) => t === 'estado-distinto').length, 2);
});

test('estados: el plan de una etapa cerrada que quedó en el árbol se pide retirar', async () => {
  const hallazgos = await estados.detectar(contexto({ archivos: plan(9, 'cerrado') }));
  assert.deepEqual(tipos(hallazgos), ['por-retirar']);
  assert.match(hallazgos[0].pregunta, /git rm/);
});

test('estados: el texto para Diseño no sigue el estado de la etapa', async () => {
  const archivos = { 'docs/plans/formularios/15-texto-diseno.md': '# Texto\n\n**Estado:** pendiente\n' };
  assert.deepEqual(await estados.detectar(contexto({ archivos })), []);
});

test('estados: una etapa cerrada en el plan tiene que estar en ClosedStages, y al revés', async () => {
  const falta = await estados.detectar(contexto({ cerradas: [8] }));
  assert.deepEqual(tipos(falta), ['etapa-sin-cerrar']);
  assert.match(falta[0].real, /E9/);
  const sobra = await estados.detectar(contexto({ cerradas: [8, 9, 10] }));
  assert.deepEqual(tipos(sobra), ['etapa-sin-cerrar']);
  assert.match(sobra[0].real, /E10/);
});

test('estados: un insumo dice cuándo se retira', async () => {
  const sinFin = await estados.detectar(contexto({ archivos: { 'docs/plans/x.md': '# X\n\n**Estado:** insumo · material\n' } }));
  assert.deepEqual(tipos(sinFin), ['insumo-sin-fin']);
  const conFin = await estados.detectar(contexto({ archivos: { 'docs/plans/x.md': '# X\n\n**Estado:** insumo · se retira cuando termine\n' } }));
  assert.deepEqual(conFin, []);
});

test('futuro: un vivo no habla en futuro de una etapa cerrada', async () => {
  const archivos = { 'docs/features/inicio.md': '# Inicio\n\nEl número llega con la Etapa 9.\n' };
  const hallazgos = await futuro.detectar(contexto({ archivos }));
  assert.deepEqual(tipos(hallazgos), ['futuro-vencido']);
  assert.equal(hallazgos[0].linea, 3);
});

test('futuro: también en plural', async () => {
  const archivos = { 'docs/rules/validacion.md': '# Validación\n\nLas reglas fiscales llegan en E9.\n' };
  assert.deepEqual(tipos(await futuro.detectar(contexto({ archivos }))), ['futuro-vencido']);
});

test('futuro: también en un título, con la b de una etapa', async () => {
  const archivos = { 'docs/operations/configuracion.md': '# Conf\n\n## Asistente (E8b, todavía planificada)\n' };
  assert.deepEqual(tipos(await futuro.detectar(contexto({ archivos }))), ['futuro-vencido']);
});

test('futuro: lo anunciado de una etapa abierta, lo pasado y los bloques de código no cuentan', async () => {
  const archivos = {
    'docs/rules/algo.md': '# Algo\n\nLlega con la Etapa 10.\n\nLlegó con la Etapa 9.\n\n```\nllega con la Etapa 9\n```\n\nLa marca `Algo` (E9) es solo una marca.\n',
  };
  assert.deepEqual(await futuro.detectar(contexto({ archivos })), []);
});

test('futuro: los planes y las decisiones no se miran: cuentan lo que era cierto ese día', async () => {
  const archivos = {
    'docs/plans/2026-10-16-algo.md': '# Plan\n\n**Estado:** cerrado\n\nLlega con la Etapa 9.\n',
    'docs/decisions/0001-uno.md': '# 0001\n\nLlega con la Etapa 9.\n',
  };
  assert.deepEqual(await futuro.detectar(contexto({ archivos })), []);
});

test('deriva: una ficha funcional cuyo código cambió después se marca, con los commits', async () => {
  const hallazgos = await deriva.detectar(contexto({
    archivos: {
      'docs/features/roles.md': '# Roles',
      'src/App/Roles/AGENTS.md': 'Área: [roles](../../../docs/features/roles.md).',
    },
    ultimoDelDoc: { 'docs/features/roles.md': '2026-10-10' },
    commits: { 'src/App/Roles': ['2026-10-12 feat(roles): algo nuevo', '2026-10-13 fix(roles): un arreglo'] },
  }));
  assert.deepEqual(tipos(hallazgos), ['codigo-cambio']);
  assert.match(hallazgos[0].real, /2 commits/);
  assert.match(hallazgos[0].pregunta, /Revisado/);
});

test('deriva: formato, tests y documentación no cambian lo que hace el área', async () => {
  const hallazgos = await deriva.detectar(contexto({
    archivos: {
      'docs/features/roles.md': '# Roles',
      'src/App/Roles/AGENTS.md': '[roles](../../../docs/features/roles.md)',
    },
    ultimoDelDoc: { 'docs/features/roles.md': '2026-10-10' },
    commits: { 'src/App/Roles': ['2026-10-12 style(formato): sin lógica', '2026-10-13 test(roles): más casos', '2026-10-14 docs: punteros', '2026-10-14 chore: ordenar'] },
  }));
  assert.deepEqual(hallazgos, []);
});

test('deriva: «Revisado» cuenta desde esa fecha si es más nueva que el último commit', async () => {
  const pedidas = [];
  const ctx = contexto({
    archivos: {
      'docs/features/roles.md': '# Roles\n\n**Revisado:** 2026-10-20\n',
      'src/App/Roles/AGENTS.md': '[roles](../../../docs/features/roles.md)',
    },
    ultimoDelDoc: { 'docs/features/roles.md': '2026-10-10' },
  });
  const original = ctx.log;
  ctx.log = async (rutas, desde) => {
    if (desde) pedidas.push(desde);
    return original(rutas, desde);
  };
  await deriva.detectar(ctx);
  assert.deepEqual(pedidas, ['2026-10-20']);
});

test('huerfanos: un documento que nadie enlaza no se encuentra', async () => {
  const archivos = { 'docs/guides/nueva.md': '# Nueva', 'docs/rules/README.md': '# Fichas\n\n[guardado](guardado.md)', 'docs/rules/guardado.md': '# Guardado' };
  const hallazgos = await huerfanos.detectar(contexto({ archivos }));
  assert.deepEqual(hallazgos.map((h) => h.archivo), ['docs/guides/nueva.md']);
});

test('huerfanos: alcanza con un AGENTS.md, y lo cerrado ya lo pide «estados»', async () => {
  const archivos = {
    'docs/guides/nueva.md': '# Nueva',
    'src/Algo/AGENTS.md': '[guía](../../docs/guides/nueva.md)',
    'docs/plans/2026-10-16-viejo.md': '# Viejo\n\n**Estado:** cerrado\n',
  };
  assert.deepEqual(await huerfanos.detectar(contexto({ archivos })), []);
});
