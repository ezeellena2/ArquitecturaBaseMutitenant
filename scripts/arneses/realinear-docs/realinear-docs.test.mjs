import assert from 'node:assert/strict';
import { cp, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { detectar, leerJson, listarArneses } from '../arnes.mjs';
import { parsearArbol } from '../comun/arbolmd.mjs';
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

// En los fixtures no hay historia de git: el "se borró en un commit" se simula con un nombre.
const historialFalso = async (_repo, ruta) => /ViejoService/.test(ruta);

async function correr(detectores, raices, soloRepo = 'back') {
  return detectar({ dirArneses, arnes: await arnesReal(detectores), raices, soloRepo, historial: historialFalso });
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
    'referencias|ruta-inexistente|modelo|docs/architecture/arbol.md|Domain/Futuro/',
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

test('punteros: crea, corrige y elimina los que no pertenecen a raíces del mapa', async () => {
  const hs = await correr(['punteros'], raicesFixture);
  assert.deepEqual(resumen(hs), [
    'punteros|puntero-claude-invalido|auto|src/Demo.Domain/CLAUDE.md|src/Demo.Domain/CLAUDE.md',
    'punteros|puntero-faltante|auto|src/Demo.Application/Services/AGENTS.md|src/Demo.Application/Services/AGENTS.md',
    'punteros|puntero-faltante|auto|src/Demo.Application/Services/CLAUDE.md|src/Demo.Application/Services/CLAUDE.md',
    'punteros|puntero-fuera-del-mapa|auto|src/Demo.Application/Extra/AGENTS.md|src/Demo.Application/Extra/AGENTS.md',
    'punteros|puntero-fuera-del-mapa|auto|src/Demo.Application/Extra/CLAUDE.md|src/Demo.Application/Extra/CLAUDE.md',
    'punteros|puntero-huerfano|auto|src/Demo.Application/Old/AGENTS.md|src/Demo.Application/Old/AGENTS.md',
    'punteros|puntero-huerfano|auto|src/Demo.Application/Old/CLAUDE.md|src/Demo.Application/Old/CLAUDE.md',
  ].sort());
  const crear = hs.find((h) => h.archivo === 'src/Demo.Application/Services/AGENTS.md');
  const lineas = crear.parche.contenido.trim().split('\n');
  assert.ok(lineas.length <= 3);
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

test('punteros: conserva la carpeta declarada aunque todavía sólo tenga punteros', async () => {
  const copia = await mkdtemp(join(tmpdir(), 'punteros-mapeados-'));
  temporales.push(copia);
  await cp(raicesFixture.back, copia, { recursive: true });
  const dir = 'src/Demo.Application/Empty';
  await mkdir(join(copia, dir), { recursive: true });
  await writeFile(join(copia, dir, 'AGENTS.md'), 'Modelos.\n');
  await writeFile(join(copia, dir, 'CLAUDE.md'), '@AGENTS.md\n');
  const arbol = join(copia, 'docs/architecture/arbol.md');
  await writeFile(arbol, (await readFile(arbol, 'utf8')) + `\n| \`${dir}/\` | modelos | guardado | — |\n`);
  const hs = await correr(['punteros'], { ...raicesFixture, back: copia });
  assert.ok(!hs.some(h => h.archivo.startsWith(dir + '/')), 'el mapa manda también cuando la carpeta no tiene código');
});

test('arbol: no pide carpetas cubiertas por una exclusión de descendientes', async () => {
  const copia = await mkdtemp(join(tmpdir(), 'arbol-excluidos-'));
  temporales.push(copia);
  await cp(raicesFixture.back, copia, { recursive: true });
  await mkdir(join(copia, '.agents/skills'), { recursive: true });
  await writeFile(join(copia, '.agents/skills/SKILL.md'), 'Una skill.');
  const hs = await correr(['arbol'], { ...raicesFixture, back: copia });
  assert.ok(!hs.some(h => h.real.startsWith('.agents')), 'una carpeta excluida no amplía el árbol');
});

test('arbol: el parser reconstruye las rutas de las secciones con ruta y no lee las demás', async () => {
  const texto = await readFile(join(fixtures, 'back', 'docs/architecture/arbol.md'), 'utf8');
  const secciones = parsearArbol(texto, [0, 1, 2]);
  assert.deepEqual(secciones.map((s) => s.raiz), ['', 'src/Demo.Application']);
  assert.deepEqual(secciones[0].entradas.map((e) => e.ruta), ['docs', 'docs/features', 'docs/features/roles.md', 'src', 'LICENSE']);
  assert.deepEqual(secciones[1].entradas.map((e) => e.ruta), [
    'src/Demo.Application/Services', 'src/Demo.Application/Services/Roles', 'src/Demo.Application/Services/Roles/RoleService.cs',
    'src/Demo.Application/Services/Roles/Helper.cs', 'src/Demo.Application/Services/Roles/ViejoService.cs',
    'src/Demo.Application/Services/Fantasma.cs', 'src/Demo.Application/Extra', 'src/Demo.Application/Extra/Cosa.cs',
    'src/Demo.Application/Old', 'src/Demo.Application/Old/Algo.cs',
  ]);
  const planeadas = secciones[1].entradas.filter((e) => e.planeada).map((e) => e.nombre);
  assert.deepEqual(planeadas, ['Extra/', 'Cosa.cs', 'Algo.cs']);
  assert.equal(parsearArbol(texto, [0, 1, 2, 7])[1].entradas.filter((e) => e.planeada).length, 1);
});

test('arbol: agregar o borrar archivos dentro de una carpeta no genera hallazgos', async () => {
  const hs = await correr(['arbol'], raicesFixture);
  assert.ok(hs.every(h => h.tipo.startsWith('carpeta-')));
});

test('simbolos: typo único (auto), inexistente y miembro que no está', async () => {
  const hs = await correr(['simbolos'], raicesFixture);
  assert.deepEqual(resumen(hs), [
    'simbolos|miembro-inexistente|modelo|docs/features/roles.md|RoleService.Borrar',
    'simbolos|simbolo-inexistente|auto|docs/features/roles.md|RolleService',
    'simbolos|simbolo-inexistente|modelo|docs/features/roles.md|RolFantasmaService',
  ].sort());
  assert.equal(hs.find((h) => h.accion === 'auto').parche.a, '`RoleService`');
});

test('simbolos: reconoce funciones locales TS y TSX y conserva los inexistentes', async () => {
  const copia = await mkdtemp(join(tmpdir(), 'simbolos-front-'));
  temporales.push(copia);
  await cp(join(fixtures, 'front'), copia, { recursive: true });
  await mkdir(join(copia, 'docs', 'features'), { recursive: true });
  await writeFile(join(copia, 'src', 'shared', 'LocalDialogs.tsx'), [
    'function OrganizationStatusDialog() { return null; }',
    'export const ExportedRecoveryDialog = () => null;',
  ].join('\n'));
  await writeFile(join(copia, 'src', 'shared', 'localRecovery.ts'), 'async function useLocalRecovery() {}\n');
  await writeFile(join(copia, 'src', 'shared', 'declarations.d.ts'), 'export declare function DeclarationOnlyDialog(): void;\n');
  await writeFile(join(copia, 'docs', 'features', 'locales.md'), [
    '# Símbolos locales',
    '`OrganizationStatusDialog`, `useLocalRecovery`, `ExportedRecoveryDialog` y `useAccess` existen.',
    '`MissingRecoveryDialog` y `DeclarationOnlyDialog` no están implementados.',
  ].join('\n'));
  const hs = await correr(['simbolos'], { back: raicesFixture.back, front: copia }, 'front');
  assert.deepEqual(resumen(hs.filter((h) => h.archivo === 'docs/features/locales.md')), [
    'simbolos|simbolo-inexistente|modelo|docs/features/locales.md|DeclarationOnlyDialog',
    'simbolos|simbolo-inexistente|modelo|docs/features/locales.md|MissingRecoveryDialog',
  ]);
});

test('catalogos: permisos, códigos y rutas mencionados que no existen, y grupos sin feature', async () => {
  const hs = await correr(['catalogos'], raicesFixture);
  const roles = 'docs/features/roles.md';
  assert.deepEqual(resumen(hs), [
    `catalogos|codigo-desconocido|modelo|${roles}|Roles.Role.NameReqired`,
    'catalogos|codigo-sin-feature|humano|docs/features|Companies',
    `catalogos|metodo-desconocido|modelo|${roles}|DELETE /api/roles`,
    `catalogos|permiso-desconocido|modelo|${roles}|company.members.reed`,
    `catalogos|permiso-desconocido|modelo|${roles}|roles.vew`,
    'catalogos|permiso-sin-feature|humano|docs/features|companies',
    `catalogos|ruta-desconocida|modelo|${roles}|POST /api/rolez`,
    'catalogos|ruta-sin-feature|humano|docs/features|companies',
  ].sort());
  assert.deepEqual(hs.find((h) => h.id.endsWith('|roles.vew')).candidatos, ['roles.view']);
  // Un permiso de empresa o de plataforma tiene dos puntos: también cuenta como documentado y se verifica.
  assert.deepEqual(hs.find((h) => h.id.endsWith('|company.members.reed')).candidatos, ['company.members.read']);
});

test('front: los mismos detectores sobre TS, con el mapa `src/` y el árbol `## src/`', async () => {
  const hs = await correr(undefined, raicesFixture, 'front');
  assert.deepEqual(resumen(hs), [
    'indices|ficha-sin-fila-guia|humano|AGENTS.md|otra.md',
    'indices|fila-huerfana|auto|AGENTS.md|docs/rules/viejo.md',
    'referencias|ruta-inexistente|modelo|docs/architecture/arnes.md|src/futuro/',
    'referencias|ruta-inexistente|modelo|docs/architecture/arbol.md|src/futuro/',
    'arbol|carpeta-nueva|modelo|docs/architecture/arbol.md|src/extra',
    'arbol|carpeta-nueva|modelo|docs/architecture/arbol.md|src/old',
    'arbol|carpeta-nueva|modelo|docs/architecture/arbol.md|src/test',
    'referencias|ruta-inexistente|modelo|docs/rules/formatos.md|src/shared/formater.ts',
    'simbolos|simbolo-inexistente|modelo|docs/rules/formatos.md|MoneyBannerPage',
    'simbolos|simbolo-inexistente|auto|docs/rules/formatos.md|useAccesss',
    'indices|ficha-sin-fila|modelo|docs/rules/README.md|otra.md',
    'indices|fila-huerfana|auto|docs/rules/README.md|docs/rules/viejo.md',
    'punteros|puntero-faltante|auto|src/areas/business/roles/api/AGENTS.md|src/areas/business/roles/api/AGENTS.md',
    'punteros|puntero-faltante|auto|src/areas/business/roles/api/CLAUDE.md|src/areas/business/roles/api/CLAUDE.md',
    'punteros|puntero-fuera-del-mapa|auto|src/extra/AGENTS.md|src/extra/AGENTS.md',
    'punteros|puntero-fuera-del-mapa|auto|src/extra/CLAUDE.md|src/extra/CLAUDE.md',
    'punteros|puntero-huerfano|auto|src/old/AGENTS.md|src/old/AGENTS.md',
    'punteros|puntero-huerfano|auto|src/old/CLAUDE.md|src/old/CLAUDE.md',
    'punteros|puntero-faltante|auto|src/shared/AGENTS.md|src/shared/AGENTS.md',
    'punteros|puntero-faltante|auto|src/shared/CLAUDE.md|src/shared/CLAUDE.md',
  ].sort());
  assert.equal(hs.find((h) => h.id.endsWith('useAccesss')).parche.a, '`useAccess`');
  // El catálogo (permisos, códigos y rutas del back) no corre sobre el front.
  assert.ok(!hs.some((h) => h.detector === 'catalogos'));
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
  assert.ok(autos.length >= 6);
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
