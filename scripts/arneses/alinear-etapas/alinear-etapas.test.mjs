import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import * as markdown from '../comun/markdown.mjs';
import * as decisiones from './detectores/decisiones.mjs';
import * as enlaces from './detectores/enlaces.mjs';
import * as estados from './detectores/estados.mjs';
import * as orden from './detectores/orden.mjs';
import * as permisos from './detectores/permisos.mjs';
import * as secciones from './detectores/secciones.mjs';
import * as tableros from './detectores/tableros.mjs';

const aqui = dirname(fileURLToPath(import.meta.url));
const HOJA = 'docs/plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md';
const INDICE = 'docs/plans/formularios/README.md';
const PLAN = 'docs/plans/2026-09-27-plan-de-desarrollo.md';

const hoja = [
  '## 2. Cómo encaja',
  '| Código | Etapa |',
  '| --- | --- |',
  '| `formrecords.create` | E14 |',
  '| `formrules.manage` | E21 |',
  '## 5. Decisiones de producto',
  '20. Algo. Deja de aplicar con la 35.',
  '26. Otra cosa. Reemplazada por la 61.',
  '35. Sin idioma.',
  '61. Avisos.',
  '## 6. Qué tomar',
  '## 7. Decisiones pendientes',
  'Ninguna.',
].join('\n');

/** Un repo en memoria: `archivos` es { ruta: texto }, y `lienzo`, los tableros que existen en el front. */
function contexto({ archivos = {}, lienzo = [], sinFront = false, cerradas = [] } = {}) {
  const todos = { [HOJA]: hoja, ...archivos };
  const rutas = Object.keys(todos);
  return {
    repo: 'back',
    etapasCerradas: cerradas,
    markdown,
    archivos: { archivos: rutas, existe: (rel) => rutas.includes(rel) },
    hermano: sinFront ? null : { existe: (rel) => lienzo.some((t) => rel === `docs/design/lienzo/${t}.dc.html`) },
    leer: async (rel) => todos[rel],
    crear: (campos) => ({ ...campos }),
  };
}

const spec = (numero, texto) => ({ [`docs/plans/formularios/${numero}-algo.md`]: texto });
const tipos = (hallazgos) => hallazgos.map((h) => h.tipo);
const completa = ['Qué leer', 'Pantallas', 'Contrato', 'Datos', 'Pasos', 'Qué verificar al arrancar', 'Cómo se cierra'].map((s) => `## ${s}`).join('\n');

test('secciones: una sola fila por etapa, con lo que le falta en el orden del formato', async () => {
  const hallazgos = await secciones.detectar(contexto({ archivos: spec(15, '# E15\n## Pantallas\n## Cómo se cierra') }));
  assert.equal(hallazgos.length, 1);
  assert.equal(hallazgos[0].tipo, 'faltan-secciones');
  assert.match(hallazgos[0].real, /E15: faltan Qué leer, Contrato, Datos, Pasos y Qué verificar al arrancar/);
});

test('secciones: una etapa con el formato completo no da nada', async () => {
  assert.deepEqual(await secciones.detectar(contexto({ archivos: spec(15, completa) })), []);
});

test('secciones: acepta los títulos numerados de las specs detalladas', async () => {
  const vieja = ['## 3. Modelo de datos', '## 5. Contrato borrador', '## 9. Archivos', '## 10. Pantallas (tableros)', '## 12. Puntos de contacto', '## 15. Cómo se cierra'].join('\n');
  const [hallazgo] = await secciones.detectar(contexto({ archivos: spec(14, vieja) }));
  assert.match(hallazgo.real, /E14: falta Qué leer$/);
});

test('secciones: no mira las etapas cerradas, los textos para Diseño ni el índice', async () => {
  const archivos = { ...spec(12, '# nada'), 'docs/plans/formularios/13-texto-diseno.md': '# nada', [INDICE]: '# nada' };
  assert.deepEqual(await secciones.detectar(contexto({ archivos, cerradas: [12] })), []);
});

const indice = (...filas) => ({ [INDICE]: ['| Etapa | Spec | Tableros |', '| --- | --- | --- |', ...filas].join('\n') });

test('tableros: lo aprobado, a revisar o a redibujar tiene que estar en el lienzo', async () => {
  const fila = '| E12 · Algo | [x](x.md) | Aprobados: `FRM-Uno`. A revisar: `FRM-Dos`. Se redibujan: `FRM-Tres`. |';
  const hallazgos = await tableros.detectar(contexto({ archivos: indice(fila), lienzo: ['FRM-Uno', 'FRM-Tres'] }));
  assert.deepEqual(tipos(hallazgos), ['tablero-no-existe']);
  assert.match(hallazgos[0].real, /FRM-Dos/);
});

test('tableros: lo que figura por dibujar y ya existe es un estado viejo', async () => {
  const archivos = { ...indice('| E17 · Algo | [x](x.md) | Por dibujar: `FRM-Copiar`. |'), 'docs/plans/formularios/17-texto-diseno.md': 'x' };
  const hallazgos = await tableros.detectar(contexto({ archivos, lienzo: ['FRM-Copiar'] }));
  assert.deepEqual(tipos(hallazgos), ['tablero-ya-existe']);
});

test('tableros: un código de ArquitecturaBase por dibujar todavía no tiene nombre de tablero', async () => {
  const archivos = { ...indice('| E16 · Algo | [x](x.md) | Por dibujar: `CE4-A` y `DOC7-A`. |'), 'docs/plans/formularios/16-texto-diseno.md': 'x' };
  const hallazgos = await tableros.detectar(contexto({ archivos }));
  assert.deepEqual(tipos(hallazgos), ['tablero-sin-nombre']);
  assert.match(hallazgos[0].real, /CE4-A y DOC7-A/);
});

test('tableros: una etapa con algo por dibujar necesita su texto para Diseño', async () => {
  const hallazgos = await tableros.detectar(contexto({ archivos: indice('| E19 · Algo | [x](x.md) | Por dibujar: `FRM-Nuevo`. |') }));
  assert.deepEqual(tipos(hallazgos), ['falta-texto-diseno']);
});

test('tableros: una fila sin etiquetas no se puede controlar', async () => {
  const hallazgos = await tableros.detectar(contexto({ archivos: indice('| E12 · Algo | [x](x.md) | Están casi todos |') }));
  assert.deepEqual(tipos(hallazgos), ['fila-sin-etiqueta']);
});

test('tableros: sin el repo del front no opina sobre lo que existe', async () => {
  const fila = '| E12 · Algo | [x](x.md) | Aprobados: `FRM-Uno`. |';
  assert.deepEqual(await tableros.detectar(contexto({ archivos: indice(fila), sinFront: true })), []);
});

test('tableros: una etapa cerrada no se mira', async () => {
  const fila = '| E12 · Algo | [x](x.md) | Aprobados: `FRM-Uno`. |';
  assert.deepEqual(await tableros.detectar(contexto({ archivos: indice(fila), cerradas: [12] })), []);
});

test('permisos: uno que no está en la tabla de la hoja de ruta', async () => {
  const hallazgos = await permisos.detectar(contexto({ archivos: spec(15, 'Pide `formrecords.delete`.') }));
  assert.deepEqual(tipos(hallazgos), ['permiso-desconocido']);
});

test('permisos: uno que nace en una etapa posterior', async () => {
  const hallazgos = await permisos.detectar(contexto({ archivos: spec(15, 'Pide `formrules.manage`.') }));
  assert.deepEqual(tipos(hallazgos), ['permiso-antes-de-su-etapa']);
  assert.match(hallazgos[0].real, /nace en la E21/);
});

test('permisos: nombrarlo en «Fuera de esta etapa» no cuenta', async () => {
  const texto = 'Pide `formrecords.create`.\n## Fuera de esta etapa\n- `formrules.manage`, de la E21.';
  assert.deepEqual(await permisos.detectar(contexto({ archivos: spec(15, texto) })), []);
});

test('decisiones: cita una que no existe', async () => {
  const hallazgos = await decisiones.detectar(contexto({ archivos: spec(15, 'Va así (99).') }));
  assert.deepEqual(tipos(hallazgos), ['decision-inexistente']);
});

test('decisiones: cita una reemplazada, salvo que la línea diga que la reemplaza', async () => {
  const hallazgos = await decisiones.detectar(contexto({ archivos: spec(15, 'Va así (26).\nEs nueva, que reemplaza la anterior (61 y 26).') }));
  assert.deepEqual(tipos(hallazgos), ['decision-reemplazada']);
  assert.equal(hallazgos[0].linea, 1);
});

test('decisiones: los números que no son decisiones no cuentan', async () => {
  const texto = 'Tope de opciones (200).\nHasta (30 y 300) caracteres.\nPasos: (1) uno, (2) dos y (3) tres, con (9) al final.';
  assert.deepEqual(await decisiones.detectar(contexto({ archivos: spec(15, texto) })), []);
});

test('decisiones: la §7 de la hoja de ruta tiene que estar vacía', async () => {
  const pendiente = hoja.replace('Ninguna.', '- **P1 · Algo.**');
  const hallazgos = await decisiones.detectar(contexto({ archivos: { [HOJA]: pendiente } }));
  assert.deepEqual(tipos(hallazgos), ['decisiones-pendientes']);
});

test('enlaces: uno roto y un ancla rota, en una spec', async () => {
  const archivos = {
    ...spec(15, '[roto](no-existe.md) y [ancla](destino.md#nada) y [bien](destino.md#si)'),
    'docs/plans/formularios/destino.md': '## Si',
  };
  assert.deepEqual(tipos(await enlaces.detectar(contexto({ archivos }))), ['enlace-roto', 'ancla-rota']);
});

test('enlaces: lo que sale del repo no se mira, y un ancla del mismo archivo sí', async () => {
  const hallazgos = await enlaces.detectar(contexto({ archivos: spec(15, '[afuera](../../../../Otro/x.md) y [acá](#nada)\n## Algo') }));
  assert.deepEqual(tipos(hallazgos), ['ancla-rota']);
});

test('orden: una etapa nombra una posterior en su cuerpo', async () => {
  const hallazgos = await orden.detectar(contexto({ archivos: spec(14, '## Pantallas\nUsa lo de la E21.') }));
  assert.deepEqual(tipos(hallazgos), ['nombra-etapa-posterior']);
  assert.match(hallazgos[0].real, /E14 nombra la E21/);
});

test('orden: las etapas anteriores y las secciones que miran adelante no cuentan', async () => {
  const texto = [
    '**Estado:** va después de la E20.',
    '## Alcance', 'Usa lo de la E12.',
    '**No entra:**', '- reglas (E21);',
    '**Entra:**', '- preguntas;',
    '## Fuera de esta etapa', '- Corregir (E21).',
    '## Requisitos previos', 'La E22.',
    '## Qué verificar al arrancar', 'Lo que deje la E23.',
  ].join('\n');
  assert.deepEqual(await orden.detectar(contexto({ archivos: spec(14, texto) })), []);
});

test('estados: el plan de desarrollo y el índice dicen cosas distintas', async () => {
  const archivos = {
    ...indice('| E15 · Algo | [x](x.md) | A revisar: `FRM-Uno`. |', '| E16 · Algo | [x](x.md) | Por dibujar: `FRM-Dos`. |', '| E17 · Algo | [x](x.md) | Aprobados: `FRM-Tres`. |'),
    [PLAN]: ['| Etapa | Estado |', '|---|---|', '| 15 · Algo | Pendiente; tableros por dibujar |', '| 16 · Algo | Pendiente; tableros aprobados |'].join('\n'),
  };
  const hallazgos = await estados.detectar(contexto({ archivos }));
  assert.deepEqual(tipos(hallazgos), ['estado-distinto', 'estado-distinto', 'falta-en-el-plan']);
});

test('estados: si dicen lo mismo no da nada', async () => {
  const archivos = {
    ...indice('| E16 · Algo | [x](x.md) | Por dibujar: `FRM-Dos`. |'),
    [PLAN]: '| 16 · Algo | Pendiente; tableros por dibujar |',
  };
  assert.deepEqual(await estados.detectar(contexto({ archivos })), []);
});

test('el manifiesto nombra exactamente los detectores de la carpeta y no toca nada', async () => {
  const manifiesto = JSON.parse(await readFile(join(aqui, 'arnes.json'), 'utf8'));
  const archivos = (await readdir(join(aqui, 'detectores'))).map((a) => a.replace('.mjs', '')).sort();
  assert.deepEqual([...manifiesto.detectores].sort(), archivos);
  assert.deepEqual(manifiesto.permisos.nuncaToca, ['**/*']);
});
