import assert from 'node:assert/strict';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
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

test('--detectar sale con 0 sin hallazgos y con 1 si el arnés no existe', async () => {
  const dir = await arnesDePrueba();
  const c = captura();
  assert.equal(await ejecutar(['prueba', '--detectar', '--repo', 'front', '--back', join(fixtures, 'back'), '--front', join(fixtures, 'front')], { dirArneses: dir, escribir: c.escribir, dirInformes: join(dir, '.i') }), 0);
  assert.equal(await ejecutar(['nada', '--detectar'], { dirArneses: dir, escribir: c.escribir }), 1);
});
