import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { mkdir, mkdtemp, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { ejecutar } from './arnes.mjs';

const fixtures = join(dirname(fileURLToPath(import.meta.url)), 'realinear-docs', 'fixtures');
const temporales = [];
after(() => Promise.all(temporales.map((t) => rm(t, { recursive: true, force: true }))));

async function arnesDePrueba() {
  const dir = await mkdtemp(join(tmpdir(), 'arneses-'));
  temporales.push(dir);
  await mkdir(join(dir, 'prueba', 'detectores'), { recursive: true });
  await mkdir(join(dir, '_oculto'), { recursive: true });
  await writeFile(join(dir, '_oculto', 'arnes.json'), JSON.stringify({ nombre: 'oculto' }));
  await writeFile(join(dir, 'prueba', 'arnes.json'), JSON.stringify({ nombre: 'prueba', descripcion: 'arnés de prueba', repos: ['back', 'front'], detectores: ['marca'], excluir: [] }));
  await writeFile(join(dir, 'prueba', 'detectores', 'marca.mjs'), `
export const nombre = 'marca';
export async function detectar(ctx) {
  const ok = ctx.archivos.existe('src/Demo.Application/Services/Roles/RoleService.cs');
  return ctx.repo === 'back' && ok ? [ctx.crear({ archivo: 'docs/features/roles.md', linea: 1, tipo: 'marca', clave: 'k', real: 'r', accion: 'humano' })] : [];
}`);
  return dir;
}

function captura() {
  const salida = [];
  return { salida, escribir: (t) => salida.push(t) };
}

test('sin argumentos lista los arneses e ignora las carpetas con _', async () => {
  const dir = await arnesDePrueba();
  const c = captura();
  const codigo = await ejecutar([], { dirArneses: dir, escribir: c.escribir });
  assert.equal(codigo, 0);
  assert.match(c.salida.join('\n'), /prueba\s+arnés de prueba/);
  assert.doesNotMatch(c.salida.join('\n'), /oculto/);
});

test('--detectar sobre los fixtures devuelve exactamente los hallazgos esperados (2)', async () => {
  const dir = await arnesDePrueba();
  const c = captura();
  const raiz = join(dir, '.informes');
  const codigo = await ejecutar(['prueba', '--detectar', '--back', join(fixtures, 'back'), '--front', join(fixtures, 'front')], { dirArneses: dir, escribir: c.escribir, dirInformes: raiz });
  assert.equal(codigo, 2);
  assert.match(c.salida.join('\n'), /humano: 1/);
});

test('--instalar copia a .claude las skills versionadas en .agents', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'skill-'));
  temporales.push(dir);
  for (const nombre of ['puerta', 'arnes']) {
    await mkdir(join(dir, '.agents', 'skills', nombre), { recursive: true });
    await writeFile(join(dir, '.agents', 'skills', nombre, 'SKILL.md'), `---\nname: ${nombre}\n---\n`);
  }
  await mkdir(join(dir, '.agents', 'skills', 'sin-skill'), { recursive: true });
  const c = captura();
  assert.equal(await ejecutar(['--instalar', '--back', dir], { escribir: c.escribir }), 0);
  for (const nombre of ['arnes', 'puerta']) {
    assert.equal(await readFile(join(dir, '.claude', 'skills', nombre, 'SKILL.md'), 'utf8'), `---\nname: ${nombre}\n---\n`);
  }
  assert.equal(existsSync(join(dir, '.claude', 'skills', 'sin-skill')), false);
  assert.match(c.salida.join('\n'), /Skills instaladas en .+: \/arnes, \/puerta/);
  assert.equal(await ejecutar(['--instalar', '--back', join(dir, 'nada')], { escribir: c.escribir }), 1);
});

test('cada skill versionada declara su nombre y su descripción', async () => {
  const raiz = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '.agents', 'skills');
  for (const carpeta of await readdir(raiz)) {
    if (!existsSync(join(raiz, carpeta, 'SKILL.md'))) continue;
    const skill = await readFile(join(raiz, carpeta, 'SKILL.md'), 'utf8');
    assert.match(skill, new RegExp(`^---\\r?\\nname: ${carpeta}\\r?\\ndescription: .+\\r?\\n---`), carpeta);
  }
});

test('la skill del repo declara su nombre y las opciones del CLI', async () => {
  const skill = await readFile(join(dirname(fileURLToPath(import.meta.url)), '..', '..', '.agents', 'skills', 'arnes', 'SKILL.md'), 'utf8');
  assert.match(skill, /^---\r?\nname: arnes\r?\n/);
  for (const opcion of ['--detectar', '--sin-modelo', '--motor', '--modelo', '--presupuesto', '--repo', '--fusionar']) assert.ok(skill.includes(opcion), opcion);
});

test('--detectar sale con 0 sin hallazgos y con 1 si el arnés no existe', async () => {
  const dir = await arnesDePrueba();
  const c = captura();
  assert.equal(await ejecutar(['prueba', '--detectar', '--repo', 'front', '--back', join(fixtures, 'back'), '--front', join(fixtures, 'front')], { dirArneses: dir, escribir: c.escribir, dirInformes: join(dir, '.i') }), 0);
  assert.equal(await ejecutar(['nada', '--detectar'], { dirArneses: dir, escribir: c.escribir }), 1);
});
