import assert from 'node:assert/strict';
import { cp, mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { detectar, leerJson, listarArneses } from '../arnes.mjs';
import { aplicarParches } from '../comun/parches.mjs';

const aqui = dirname(fileURLToPath(import.meta.url));
const dirArneses = join(aqui, '..');
const fixtures = join(aqui, 'fixtures');
const temporales = [];
after(() => Promise.all(temporales.map((t) => rm(t, { recursive: true, force: true }))));

async function arnesReal(detectores) {
  const manifiesto = await leerJson(join(aqui, 'arnes.json'));
  return { carpeta: 'realinear-docs', manifiesto: { ...manifiesto, detectores: detectores ?? manifiesto.detectores } };
}

async function correr(detectores, raices, soloRepo = 'back') {
  return detectar({ dirArneses, arnes: await arnesReal(detectores), raices, soloRepo });
}

const resumen = (hs) => hs.map((h) => `${h.detector}|${h.tipo}|${h.accion}|${h.archivo}|${h.id.split('|').at(-1)}`).sort();
const raicesFixture = { back: join(fixtures, 'back'), front: join(fixtures, 'front') };

test('el manifiesto de realinear-docs se lista y apunta a detectores que existen', async () => {
  const arneses = await listarArneses(dirArneses);
  const a = arneses.find((x) => x.carpeta === 'realinear-docs');
  assert.ok(a);
  for (const d of a.manifiesto.detectores) {
    const modulo = await import(`./detectores/${d}.mjs`);
    assert.equal(modulo.nombre, d);
  }
});

test('referencias: rutas, anclas y renombres del fixture', async () => {
  const hs = await correr(['referencias'], raicesFixture);
  assert.deepEqual(resumen(hs), [
    'referencias|ancla-inexistente|modelo|docs/features/roles.md|no-esta',
    'referencias|ruta-inexistente|auto|docs/features/roles.md|src/Demo.Application/Services/RoleService.cs',
    'referencias|ruta-inexistente|modelo|AGENTS.md|docs/rules/inexistente.md',
    'referencias|ruta-inexistente|modelo|docs/architecture/arnes.md|Domain/Futuro/',
    'referencias|ruta-inexistente|modelo|docs/features/roles.md|../rules/no-existe.md',
    'referencias|ruta-inexistente|modelo|docs/features/roles.md|Services/Helper.cs',
    'referencias|ruta-inexistente|modelo|docs/rules/README.md|viejo.md',
  ].sort());
  const auto = hs.find((h) => h.accion === 'auto');
  assert.equal(auto.esperado, 'src/Demo.Application/Services/Roles/RoleService.cs');
  assert.equal(auto.parche.a, '`src/Demo.Application/Services/Roles/RoleService.cs`');
});

test('referencias: lo planeado (E#) y lo excluido no son errores', async () => {
  const hs = await correr(['referencias'], raicesFixture);
  assert.ok(!hs.some((h) => h.archivo.startsWith('docs/plans/')));
  assert.ok(!hs.some((h) => /agregar-un-area|futuro\.md|RoleService\.cs$/.test(h.real) && h.accion !== 'auto'));
});

test('punteros: crea, corrige, borra huérfanos y pregunta por los que no están en el mapa', async () => {
  const hs = await correr(['punteros'], raicesFixture);
  assert.deepEqual(resumen(hs), [
    'punteros|puntero-claude-invalido|auto|src/Demo.Domain/CLAUDE.md|src/Demo.Domain/CLAUDE.md',
    'punteros|puntero-faltante|auto|src/Demo.Application/Services/AGENTS.md|src/Demo.Application/Services/AGENTS.md',
    'punteros|puntero-faltante|auto|src/Demo.Application/Services/CLAUDE.md|src/Demo.Application/Services/CLAUDE.md',
    'punteros|puntero-fuera-del-mapa|modelo|src/Demo.Application/Extra/AGENTS.md|src/Demo.Application/Extra',
    'punteros|puntero-huerfano|auto|src/Demo.Application/Old/AGENTS.md|src/Demo.Application/Old/AGENTS.md',
    'punteros|puntero-huerfano|auto|src/Demo.Application/Old/CLAUDE.md|src/Demo.Application/Old/CLAUDE.md',
  ].sort());
  const crear = hs.find((h) => h.archivo === 'src/Demo.Application/Services/AGENTS.md');
  const lineas = crear.parche.contenido.trim().split('\n');
  assert.ok(lineas.length <= 8);
  assert.match(crear.parche.contenido, /Servicio y helpers/);
  assert.match(crear.parche.contenido, /docs\/rules\/guardado\.md/);
});

test('indices: filas de archivos que no existen y fichas sin fila', async () => {
  const hs = await correr(['indices'], raicesFixture);
  assert.deepEqual(resumen(hs), [
    'indices|adr-sin-fila|modelo|docs/decisions/README.md|0002-segunda.md',
    'indices|ficha-sin-fila|modelo|docs/rules/README.md|nueva.md',
    'indices|ficha-sin-fila-guia|humano|AGENTS.md|nueva.md',
    'indices|fila-huerfana|auto|AGENTS.md|docs/rules/inexistente.md',
    'indices|fila-huerfana|auto|docs/rules/README.md|docs/rules/viejo.md',
  ].sort());
});

test('el runner deja una sola respuesta por línea: el auto de indices gana al modelo de referencias', async () => {
  const hs = await correr(undefined, raicesFixture);
  const enLinea = hs.filter((h) => h.archivo === 'docs/rules/README.md' && h.linea === 6 && h.tipo !== 'ficha-sin-fila');
  assert.deepEqual(enLinea.map((h) => `${h.detector}:${h.accion}`), ['indices:auto']);
});

test('cada auto del fixture queda arreglado y no queda ningún auto', async () => {
  const copia = await mkdtemp(join(tmpdir(), 'fix-back-'));
  temporales.push(copia);
  await cp(join(fixtures, 'back'), copia, { recursive: true });
  const raices = { back: copia, front: join(fixtures, 'front') };
  const antes = await correr(undefined, raices);
  const autos = antes.filter((h) => h.accion === 'auto');
  assert.ok(autos.length >= 7);
  const manifiesto = (await arnesReal()).manifiesto;
  const r = await aplicarParches(copia, autos.map((h) => h.parche), manifiesto.permisos);
  assert.deepEqual(r.fallidos, []);
  const despues = await correr(undefined, raices);
  assert.deepEqual(despues.filter((h) => h.accion === 'auto'), []);
  assert.match(await readFile(join(copia, 'docs/features/roles.md'), 'utf8'), /`src\/Demo\.Application\/Services\/Roles\/RoleService\.cs` pasó/);
  assert.equal(await readFile(join(copia, 'src/Demo.Domain/CLAUDE.md'), 'utf8'), '@AGENTS.md\n');
  assert.doesNotMatch(await readFile(join(copia, 'docs/rules/README.md'), 'utf8'), /viejo/);
});

test('parches: verifica el texto, respeta permisos y preserva CRLF', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'parches-'));
  temporales.push(dir);
  const { writeFile, mkdir } = await import('node:fs/promises');
  await mkdir(join(dir, 'docs'), { recursive: true });
  await writeFile(join(dir, 'docs', 'a.md'), 'uno\r\ndos\r\ntres\r\n');
  await writeFile(join(dir, 'Codigo.cs'), 'class A {}\n');
  const permisos = { puedeCrear: ['docs/**/*.md'], puedeBorrar: ['**/AGENTS.md'], nuncaToca: ['**/*.cs'] };
  const r = await aplicarParches(dir, [
    { tipo: 'reemplazar', archivo: 'docs/a.md', linea: 2, de: 'dos', a: 'DOS' },
    { tipo: 'borrar-linea', archivo: 'docs/a.md', linea: 3, de: 'tres' },
    { tipo: 'insertar-linea', archivo: 'docs/a.md', despues: 1, texto: 'medio' },
    { tipo: 'reemplazar', archivo: 'docs/a.md', linea: 1, de: 'no está', a: 'x' },
    { tipo: 'reemplazar', archivo: 'Codigo.cs', linea: 1, de: 'A', a: 'B' },
    { tipo: 'crear-archivo', archivo: 'docs/nuevo.md', contenido: 'hola\n' },
    { tipo: 'crear-archivo', archivo: 'otra/cosa.txt', contenido: 'x' },
    { tipo: 'borrar-archivo', archivo: 'docs/a.md' },
  ], permisos);
  assert.equal(r.aplicados.length, 4);
  assert.deepEqual(r.fallidos.map((f) => f.motivo.split(':')[0]).sort(), ['no coincide', 'sin permiso', 'sin permiso', 'sin permiso']);
  assert.equal(await readFile(join(dir, 'docs', 'a.md'), 'utf8'), 'uno\r\nmedio\r\nDOS\r\n');
  assert.equal(await readFile(join(dir, 'Codigo.cs'), 'utf8'), 'class A {}\n');
  assert.equal(await readFile(join(dir, 'docs', 'nuevo.md'), 'utf8'), 'hola\n');
});
