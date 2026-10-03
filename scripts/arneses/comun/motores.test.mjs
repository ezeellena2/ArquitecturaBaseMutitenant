import assert from 'node:assert/strict';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { git } from './git.mjs';
import { guardia } from './guardia.mjs';
import { resolverEjecutable } from './proceso.mjs';
import * as claude from './motores/claude.mjs';
import * as codex from './motores/codex.mjs';
import { extraerResultado, validar } from './resultado.mjs';
import { carpetaDelWorktree, crearWorktree, nombreDeRama, quitarWorktree } from './worktree.mjs';

const aqui = dirname(fileURLToPath(import.meta.url));
const fakes = join(aqui, 'fakes');
const temporales = [];
after(() => Promise.all(temporales.map((t) => rm(t, { recursive: true, force: true }))));

async function repoTemporal(archivos) {
  // Cada test tiene su propia carpeta madre, así `../.arneses` (el worktree) no se comparte entre tests.
  const madre = await mkdtemp(join(tmpdir(), 'repo-arnes-'));
  temporales.push(madre);
  const dir = join(madre, 'repo');
  await mkdir(dir);
  await git(dir, ['init', '-q', '-b', 'main']);
  await git(dir, ['config', 'user.email', 'a@b.c']);
  await git(dir, ['config', 'user.name', 'Arnés']);
  await git(dir, ['config', 'core.autocrlf', 'false']);
  for (const [ruta, contenido] of Object.entries(archivos)) {
    await mkdir(dirname(join(dir, ruta)), { recursive: true });
    await writeFile(join(dir, ruta), contenido);
  }
  await git(dir, ['add', '-A']);
  await git(dir, ['commit', '-q', '-m', 'base']);
  return dir;
}

const permisos = { puedeCrear: ['docs/**/*.md', '**/AGENTS.md'], puedeBorrar: ['**/AGENTS.md'], nuncaToca: ['**/*.cs', 'tests/**'] };

test('worktree: rama y carpeta fuera del repo, desde HEAD, y se quita limpio', async () => {
  const raiz = await repoTemporal({ 'docs/a.md': 'uno\n' });
  await writeFile(join(raiz, 'docs/a.md'), 'a medio hacer\n');
  const carpeta = carpetaDelWorktree(raiz, 'realinear-docs', '20261002-2100');
  assert.match(carpeta.replaceAll('\\', '/'), /\/\.arneses\/repo-realinear-docs-20261002-2100$/);
  await crearWorktree(raiz, { rama: nombreDeRama('realinear-docs', '20261002-2100'), carpeta });
  assert.equal(await readFile(join(carpeta, 'docs/a.md'), 'utf8'), 'uno\n');
  assert.equal((await git(carpeta, ['branch', '--show-current'])).trim(), 'arnes/realinear-docs/20261002-2100');
  await mkdir(join(carpeta, '.local'), { recursive: true });
  await writeFile(join(carpeta, '.local', 'tarea.json'), '{}');
  assert.equal((await git(carpeta, ['status', '--porcelain'])).trim(), '');
  await quitarWorktree(raiz, carpeta);
  assert.ok(!existsSync(carpeta));
});

test('guardia: revierte lo tocado fuera de permisos y deja pasar lo permitido', async () => {
  const raiz = await repoTemporal({ 'src/X.cs': 'class X {}\n', 'docs/a.md': 'uno\n', 'src/AGENTS.md': 'viejo\n', 'tests/T.cs': 'class T {}\n', 'README.md': 'leeme\n' });
  await writeFile(join(raiz, 'src/X.cs'), 'class Y {}\n');
  await writeFile(join(raiz, 'docs/a.md'), 'dos\n');
  await writeFile(join(raiz, 'docs/nuevo.md'), 'nuevo\n');
  await mkdir(join(raiz, 'otro'), { recursive: true });
  await writeFile(join(raiz, 'otro/x.txt'), 'x');
  await rm(join(raiz, 'src/AGENTS.md'));
  await rm(join(raiz, 'README.md'));
  const g = await guardia(raiz, { permisos, excluir: [] });
  assert.equal(g.ok, false);
  assert.deepEqual(g.revertidos.map((r) => `${r.archivo}:${r.motivo}`).sort(), [
    'README.md:no está en puedeBorrar',
    'otro/x.txt:no está en puedeCrear',
    'src/X.cs:está en nuncaToca',
  ]);
  assert.equal(await readFile(join(raiz, 'src/X.cs'), 'utf8'), 'class X {}\n');
  assert.equal(await readFile(join(raiz, 'README.md'), 'utf8'), 'leeme\n');
  assert.ok(!existsSync(join(raiz, 'otro/x.txt')));
  assert.equal(await readFile(join(raiz, 'docs/a.md'), 'utf8'), 'dos\n');
  assert.ok(existsSync(join(raiz, 'docs/nuevo.md')));
  assert.ok(!existsSync(join(raiz, 'src/AGENTS.md')));
  const otra = await guardia(raiz, { permisos, excluir: [] });
  assert.equal(otra.ok, true);
});

test('guardia: lo excluido solo se toca si es propio del arnés', async () => {
  const raiz = await repoTemporal({ 'docs/plans/p.md': 'plan\n', 'docs/ig.md': '[]\n' });
  await writeFile(join(raiz, 'docs/plans/p.md'), 'tocado\n');
  await writeFile(join(raiz, 'docs/ig.md'), '[1]\n');
  const g = await guardia(raiz, { permisos, excluir: ['docs/plans/**', 'docs/ig.md'], propios: ['docs/ig.md'] });
  assert.deepEqual(g.revertidos.map((r) => r.archivo), ['docs/plans/p.md']);
});

test('resultado: extrae el JSON de un bloque y valida contra el schema', async () => {
  const esquema = JSON.parse(await readFile(join(aqui, '..', 'realinear-docs', 'resultado.schema.json'), 'utf8'));
  const texto = 'Listo.\n```json\n{"resultados":[{"id":"a|b","resultado":"corregido","nota":""}]}\n```';
  const obj = extraerResultado(texto);
  assert.deepEqual(validar(obj, esquema), []);
  assert.deepEqual(extraerResultado('{"resultados":[]}'), { resultados: [] });
  assert.equal(extraerResultado('sin json'), null);
  assert.ok(validar({ resultados: [{ id: 'x', resultado: 'inventado', nota: '' }] }, esquema).length > 0);
  assert.ok(validar({ resultados: [{ id: 'x', resultado: 'humano' }] }, esquema).some((e) => /nota: falta/.test(e)));
  assert.ok(validar({ otro: 1 }, esquema).length > 0);
});

test('claude: argumentos del plan §7 y, sin aislar, sin los flags que evitan la config del usuario', () => {
  const base = { tarea: 'T', instrucciones: 'I.md', modelo: 'sonnet', esfuerzo: 'low', presupuestoUsd: 1.5 };
  const a = claude.argumentos(base);
  const par = (flag) => a[a.indexOf(flag) + 1];
  assert.equal(par('--model'), 'sonnet');
  assert.equal(par('--effort'), 'low');
  assert.equal(par('--max-budget-usd'), '1.5');
  assert.equal(par('--append-system-prompt-file'), 'I.md');
  assert.equal(par('--allowedTools'), 'Read Edit Write Glob Grep');
  assert.equal(par('--disallowedTools'), 'Bash');
  assert.equal(par('--permission-mode'), 'acceptEdits');
  assert.equal(par('--setting-sources'), 'project');
  assert.ok(a.includes('--strict-mcp-config') && a.includes('--no-session-persistence'));
  assert.equal(par('--output-format'), 'json');
  const sin = claude.argumentos({ ...base, aislar: false });
  assert.ok(!sin.includes('--setting-sources') && !sin.includes('--strict-mcp-config'));
});

test('codex: argumentos del plan §7 y --ignore-user-config solo si se aísla', () => {
  const base = { cwd: 'C:/wt', modelo: 'gpt-6.1-sol', esfuerzo: 'low', esquema: 'S.json', salida: 'R.json' };
  const a = codex.argumentos(base);
  const par = (flag) => a[a.indexOf(flag) + 1];
  assert.equal(a[0], 'exec');
  assert.equal(par('-C'), 'C:/wt');
  assert.equal(par('-m'), 'gpt-6.1-sol');
  assert.equal(par('-c'), 'model_reasoning_effort="low"');
  assert.equal(par('-s'), 'workspace-write');
  assert.ok(a.includes('--ignore-user-config') && a.includes('--ephemeral') && a.includes('--json'));
  assert.equal(par('--output-schema'), 'S.json');
  assert.equal(par('-o'), 'R.json');
  assert.equal(a.at(-1), '-');
  assert.ok(!codex.argumentos({ ...base, aislar: false }).includes('--ignore-user-config'));
});

test('motor claude con un ejecutable falso: resultado, tokens y costo', async () => {
  const cwd = await mkdtemp(join(tmpdir(), 'motor-'));
  temporales.push(cwd);
  const log = join(cwd, 'log.jsonl');
  const escenario = join(cwd, 'escenario.json');
  await writeFile(join(cwd, 'a.md'), 'uno\n');
  await writeFile(join(cwd, 'I.md'), 'instrucciones');
  await writeFile(escenario, JSON.stringify({ ediciones: [{ archivo: 'a.md', reemplazar: ['uno', 'dos'] }], resultados: [{ id: 'x', resultado: 'corregido', nota: '' }] }));
  process.env.ARNES_FAKE_ESCENARIO = escenario;
  process.env.ARNES_FAKE_LOG = log;
  const r = await claude.ejecutar({ tarea: 'Hacelo', instrucciones: join(cwd, 'I.md'), modelo: 'sonnet', esfuerzo: 'low', presupuestoUsd: 1, cwd, bin: [process.execPath, join(fakes, 'claude-falso.mjs')] });
  assert.equal(r.ok, true);
  assert.deepEqual(r.resultado, { resultados: [{ id: 'x', resultado: 'corregido', nota: '' }] });
  assert.deepEqual(r.consumo, { tokensEntrada: 162, entradaSinCache: 12, cacheLectura: 100, cacheEscritura: 50, tokensSalida: 30, costoUsd: 0.0123 });
  assert.equal(await readFile(join(cwd, 'a.md'), 'utf8'), 'dos\n');
  const registro = JSON.parse((await readFile(log, 'utf8')).trim());
  assert.equal(registro.args[0], '-p');
  assert.ok(registro.args.includes('--strict-mcp-config'));
  await writeFile(escenario, JSON.stringify({ error: true }));
  const mala = await claude.ejecutar({ tarea: 'x', instrucciones: join(cwd, 'I.md'), modelo: 'sonnet', esfuerzo: 'low', presupuestoUsd: 1, cwd, bin: [process.execPath, join(fakes, 'claude-falso.mjs')] });
  assert.equal(mala.ok, false);
  assert.equal(mala.estado, 'error_max_budget_usd');
  delete process.env.ARNES_FAKE_ESCENARIO;
  delete process.env.ARNES_FAKE_LOG;
});

test('motor codex con un ejecutable falso: instrucciones por stdin, resultado y tokens sin costo', async () => {
  const cwd = await mkdtemp(join(tmpdir(), 'motor-'));
  temporales.push(cwd);
  const log = join(cwd, 'log.jsonl');
  const escenario = join(cwd, 'escenario.json');
  await writeFile(escenario, JSON.stringify({ ediciones: [{ archivo: 'nuevo.md', crear: 'hola\n' }], resultados: [{ id: 'y', resultado: 'creado', nota: 'ok' }] }));
  process.env.ARNES_FAKE_ESCENARIO = escenario;
  process.env.ARNES_FAKE_LOG = log;
  const salida = join(cwd, 'resultado.json');
  const r = await codex.ejecutar({ tarea: 'Hacelo', instruccionesTexto: 'INSTRUCCIONES', modelo: 'gpt-6.1-sol', esfuerzo: 'low', cwd, esquema: 'S.json', salida, bin: [process.execPath, join(fakes, 'codex-falso.mjs')] });
  assert.equal(r.ok, true);
  assert.deepEqual(r.resultado.resultados[0].id, 'y');
  assert.deepEqual(r.consumo, { tokensEntrada: 150, cacheLectura: 100, tokensSalida: 20, tokensRazonamiento: 5, costoUsd: null });
  assert.ok(existsSync(join(cwd, 'nuevo.md')));
  const registro = JSON.parse((await readFile(log, 'utf8')).trim());
  assert.match(registro.stdin, /^INSTRUCCIONES/);
  delete process.env.ARNES_FAKE_ESCENARIO;
  delete process.env.ARNES_FAKE_LOG;
});

test('proceso: en Windows resuelve los shims .cmd de npm a su ejecutable real', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'shims-'));
  temporales.push(dir);
  await writeFile(join(dir, 'uno.cmd'), '@ECHO off\r\n"%dp0%\\node_modules\\x\\bin\\uno.exe"   %*\r\n');
  await writeFile(join(dir, 'dos.cmd'), '@ECHO off\r\n  SET "_prog=node"\r\n"%_prog%"  "%dp0%\\node_modules\\y\\bin\\dos.js" %*\r\n');
  const entorno = { PATH: dir };
  assert.deepEqual(resolverEjecutable('uno', { entorno, plataforma: 'win32' }), [`${dir}\\node_modules\\x\\bin\\uno.exe`]);
  assert.deepEqual(resolverEjecutable('dos', { entorno, plataforma: 'win32' }), [process.execPath, `${dir}\\node_modules\\y\\bin\\dos.js`]);
  assert.deepEqual(resolverEjecutable('uno', { plataforma: 'linux' }), ['uno']);
});
