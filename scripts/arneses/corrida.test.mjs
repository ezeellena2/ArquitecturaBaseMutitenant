import assert from 'node:assert/strict';
import { cp, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, sep } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { ejecutar } from './arnes.mjs';
import { git } from './comun/git.mjs';

const aqui = dirname(fileURLToPath(import.meta.url));
const fixtures = join(aqui, 'realinear-docs', 'fixtures');
const claudeFalso = [process.execPath, join(aqui, 'comun', 'fakes', 'claude-falso.mjs')];
const codexFalso = [process.execPath, join(aqui, 'comun', 'fakes', 'codex-falso.mjs')];
const noDebeLlamarse = [process.execPath, '-e', 'process.exit(99)'];
const historialFalso = async (_repo, ruta) => /ViejoService/.test(ruta);
const temporales = [];
after(async () => {
  for (const t of temporales) await rm(t, { recursive: true, force: true }).catch(() => {});
});

const ID_RUTA = 'referencias|back|docs/features/roles.md|ruta-inexistente|../rules/no-existe.md';
const ID_FUTURO = 'referencias|back|docs/architecture/arnes.md|ruta-inexistente|Domain/Futuro/';
const ID_CATALOGO = 'catalogos|back|docs/features/roles.md|permiso-desconocido|roles.vew';

/** Un repo git con el fixture del back, una copia del arnés (para poder escribir `ignorados.json`) y una carpeta de informes. */
async function entorno({ verificacion = ['node -e "process.exit(0)"'] } = {}) {
  const madre = await mkdtemp(join(tmpdir(), 'corrida-'));
  temporales.push(madre);
  const back = join(madre, 'repo');
  await cp(join(fixtures, 'back'), back, { recursive: true });
  await git(back, ['init', '-q', '-b', 'main']);
  await git(back, ['config', 'user.email', 'a@b.c']);
  await git(back, ['config', 'user.name', 'Arnés']);
  await git(back, ['config', 'core.autocrlf', 'false']);
  await git(back, ['add', '-A']);
  await git(back, ['commit', '-q', '-m', 'base']);
  const dirArneses = join(madre, 'arneses');
  await cp(aqui, dirArneses, { recursive: true, filter: (origen) => !origen.includes(`${sep}fixtures`) && !origen.endsWith('.test.mjs') });
  const manifiestoRuta = join(dirArneses, 'realinear-docs', 'arnes.json');
  const manifiesto = JSON.parse(await readFile(manifiestoRuta, 'utf8'));
  manifiesto.repos = ['back'];
  manifiesto.verificacion = { back: verificacion };
  await writeFile(manifiestoRuta, JSON.stringify(manifiesto));
  await writeFile(join(madre, 'escenario.json'), '{}');
  return { madre, back, dirArneses, dirInformes: join(madre, 'informes'), escenario: join(madre, 'escenario.json'), log: join(madre, 'log.jsonl') };
}

async function correr(e, extra = [], bins = { claude: claudeFalso, codex: codexFalso }, escenario = {}) {
  await writeFile(e.escenario, JSON.stringify(escenario));
  process.env.ARNES_FAKE_ESCENARIO = e.escenario;
  process.env.ARNES_FAKE_LOG = e.log;
  const salida = [];
  try {
    const codigo = await ejecutar(['realinear-docs', '--back', e.back, '--front', join(fixtures, 'front'), '--repo', 'back', ...extra], {
      dirArneses: e.dirArneses, dirInformes: e.dirInformes, escribir: (t) => salida.push(t), historial: historialFalso, bins, fecha: '20261002-2300',
    });
    return { codigo, salida: salida.join('\n') };
  } finally {
    delete process.env.ARNES_FAKE_ESCENARIO;
    delete process.env.ARNES_FAKE_LOG;
  }
}

const leerInforme = async (e) => JSON.parse(await readFile(join(e.dirInformes, 'realinear-docs', '20261002-2300-back.json'), 'utf8'));
const enRama = (e, ruta) => git(e.back, ['show', `arnes/realinear-docs/20261002-2300:${ruta}`]);

const escenarioFeliz = {
  ediciones: [{ archivo: 'docs/features/roles.md', reemplazar: ['[una ficha rota](../rules/no-existe.md) que no tiene reemplazo', 'una ficha que ya no existe'] }],
  resultados: [
    { id: ID_RUTA, resultado: 'corregido', nota: 'se sacó la mención' },
    { id: ID_FUTURO, resultado: 'descartado', nota: 'carpeta planeada' },
    { id: ID_CATALOGO, resultado: 'humano', nota: '¿roles.view o roles.manage?' },
  ],
};

test('corrida con Claude: autos, modelo, guardia, verificación y commit en la rama del arnés', async () => {
  const e = await entorno();
  const { codigo, salida } = await correr(e, [], undefined, escenarioFeliz);
  assert.equal(codigo, 0, salida);
  const info = await leerInforme(e);
  assert.equal(info.estado, 'commiteado');
  assert.equal(info.rama, 'arnes/realinear-docs/20261002-2300');
  assert.equal(info.motor, 'claude');
  assert.ok(info.autosAplicados >= 7);
  assert.ok(info.enviados >= 3);
  assert.equal(info.consumo.costoUsd, 0.0123);
  assert.equal(info.verificacion[0].codigo, 0);
  // Los autos y la corrección del modelo quedaron en el commit de la rama; main no se tocó.
  assert.equal((await enRama(e, 'src/Demo.Domain/CLAUDE.md')).trim(), '@AGENTS.md');
  assert.match(await enRama(e, 'docs/features/roles.md'), /una ficha que ya no existe/);
  assert.match(await enRama(e, 'src/Demo.Application/Services/AGENTS.md'), /Servicio y helpers/);
  assert.equal((await git(e.back, ['branch', '--show-current'])).trim(), 'main');
  assert.equal((await git(e.back, ['rev-list', '--count', 'main'])).trim(), '1');
  assert.equal((await git(e.back, ['rev-list', '--count', 'arnes/realinear-docs/20261002-2300'])).trim(), '2');
  const mensaje = await git(e.back, ['log', '-1', '--format=%B', 'arnes/realinear-docs/20261002-2300']);
  assert.match(mensaje, /^docs: realinear documentación con el código/);
  // Lo descartado quedó en ignorados.json (lo escribe el script) y lo escalado va a la lista humano.
  assert.match(await enRama(e, 'scripts/arneses/realinear-docs/ignorados.json').catch(() => ''), /^$|carpeta planeada/);
  const ignorados = JSON.parse(await readFile(join(e.dirArneses, 'realinear-docs', 'ignorados.json'), 'utf8'));
  assert.deepEqual(ignorados.map((i) => i.id), [ID_FUTURO]);
  assert.ok(info.hallazgos.some((h) => h.id === ID_CATALOGO && h.accion === 'humano' && h.pregunta === '¿roles.view o roles.manage?'));
  assert.ok(!existsSync(info.worktree ?? join(e.madre, '.arneses', 'repo-realinear-docs-20261002-2300')));
  const registro = (await readFile(e.log, 'utf8')).trim().split('\n').map((l) => JSON.parse(l));
  assert.equal(registro.length, 1);
  assert.ok(registro[0].args.includes('--strict-mcp-config'));
  assert.match(await readFile(join(e.dirInformes, 'realinear-docs', '20261002-2300-back.md'), 'utf8'), /costo US\$ 0\.0123/);
});

test('corrida con Codex: mismo informe, instrucciones por stdin y consumo sin costo', async () => {
  const e = await entorno();
  const { codigo, salida } = await correr(e, ['--motor', 'codex'], undefined, escenarioFeliz);
  assert.equal(codigo, 0, salida);
  const info = await leerInforme(e);
  assert.equal(info.estado, 'commiteado');
  assert.equal(info.motor, 'codex');
  assert.deepEqual(info.consumo, { tokensEntrada: 150, cacheLectura: 100, tokensSalida: 20, tokensRazonamiento: 5, costoUsd: null });
  assert.match(await enRama(e, 'docs/features/roles.md'), /una ficha que ya no existe/);
  const registro = JSON.parse((await readFile(e.log, 'utf8')).trim());
  assert.match(registro.stdin, /^# Instrucciones: realinear/);
  assert.ok(registro.args.includes('--ignore-user-config'));
  assert.match(await readFile(join(e.dirInformes, 'realinear-docs', '20261002-2300-back.md'), 'utf8'), /costo n\/d/);
});

test('guardia: si el modelo toca código, se revierte, la corrida falla y no se commitea', async () => {
  const e = await entorno();
  const escenario = { ediciones: [{ archivo: 'src/Demo.Domain/Authorization/Permissions.cs', agregar: '// tocado\n' }, { archivo: 'docs/features/roles.md', agregar: '\nmás\n' }], resultados: [] };
  const { codigo } = await correr(e, [], undefined, escenario);
  assert.equal(codigo, 1);
  const info = await leerInforme(e);
  assert.match(info.estado, /^falló: la guardia revirtió 1 archivo/);
  assert.deepEqual(info.revertidos.map((r) => r.archivo), ['src/Demo.Domain/Authorization/Permissions.cs']);
  assert.equal((await git(e.back, ['rev-list', '--count', 'arnes/realinear-docs/20261002-2300'])).trim(), '1');
  assert.ok(info.worktreeConservado);
  assert.doesNotMatch(await readFile(join(info.worktree, 'src/Demo.Domain/Authorization/Permissions.cs'), 'utf8'), /tocado/);
});

test('verificación: si falla un comando no se commitea y el worktree queda para mirar', async () => {
  const e = await entorno({ verificacion: ['node -e "process.exit(0)"', 'node -e "console.error(\'rojo\'); process.exit(3)"'] });
  const { codigo } = await correr(e, ['--sin-modelo']);
  assert.equal(codigo, 1);
  const info = await leerInforme(e);
  assert.match(info.estado, /^falló: la verificación falló: node -e/);
  assert.deepEqual(info.verificacion.map((v) => v.codigo), [0, 3]);
  assert.match(info.verificacion[1].salida, /rojo/);
  assert.ok(existsSync(info.worktree));
});

test('--sin-modelo: arregla lo mecánico y no llama al motor', async () => {
  const e = await entorno();
  const { codigo, salida } = await correr(e, ['--sin-modelo'], { claude: noDebeLlamarse, codex: noDebeLlamarse });
  assert.equal(codigo, 0, salida);
  const info = await leerInforme(e);
  assert.equal(info.estado, 'commiteado');
  assert.equal(info.enviados, 0);
  assert.equal(info.consumo, undefined);
  assert.ok(info.hallazgos.filter((h) => h.accion === 'humano').length > 0);
});

test('un motor que falla no deja nada commiteado', async () => {
  const e = await entorno();
  const { codigo } = await correr(e, [], undefined, { error: true });
  assert.equal(codigo, 1);
  const info = await leerInforme(e);
  assert.match(info.estado, /^falló: el motor claude falló: error_max_budget_usd/);
  assert.equal(info.consumo.costoUsd, 1.5);
});

test('--fusionar: con la verificación en verde, main avanza solo con --ff-only', async () => {
  const e = await entorno();
  const { codigo, salida } = await correr(e, ['--sin-modelo', '--fusionar']);
  assert.equal(codigo, 0, salida);
  const info = await leerInforme(e);
  assert.equal(info.estado, 'fusionado');
  assert.equal((await git(e.back, ['rev-list', '--count', 'main'])).trim(), '2');
  assert.equal((await readFile(join(e.back, 'src/Demo.Domain/CLAUDE.md'), 'utf8')).trim(), '@AGENTS.md');
});

test('sin cambios: no deja rama ni worktree', async () => {
  const e = await entorno();
  const manifiestoRuta = join(e.dirArneses, 'realinear-docs', 'arnes.json');
  const m = JSON.parse(await readFile(manifiestoRuta, 'utf8'));
  m.detectores = ['catalogos'];
  await writeFile(manifiestoRuta, JSON.stringify(m));
  const { codigo } = await correr(e, ['--sin-modelo']);
  assert.equal(codigo, 0);
  const info = await leerInforme(e);
  assert.equal(info.estado, 'sin cambios');
  assert.equal((await git(e.back, ['branch', '--list', 'arnes/*'])).trim(), '');
  await mkdir(join(e.madre, 'x'), { recursive: true });
});
