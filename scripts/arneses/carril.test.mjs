import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { actualizar, conCandado, entregar, ErrorDeCarril, ejecutar, limpiar, preparar } from './carril.mjs';

// Repos de git temporales: nada toca el repo real.
function git(cwd, ...args) {
  const r = spawnSync('git', args, { cwd, encoding: 'utf8' });
  if (r.status !== 0) throw new Error(`git ${args.join(' ')}: ${r.stderr}`);
  return r.stdout.trim();
}

function repoMain() {
  const dir = mkdtempSync(join(tmpdir(), 'carril-'));
  const main = join(dir, 'main');
  mkdirSync(main);
  git(main, 'init', '-q', '-b', 'main');
  git(main, 'config', 'user.name', 'Prueba');
  git(main, 'config', 'user.email', 'prueba@example.com');
  git(main, 'config', 'core.autocrlf', 'false');
  writeFileSync(join(main, 'a.txt'), 'uno\n');
  writeFileSync(join(main, 'b.txt'), 'dos\n');
  writeFileSync(join(main, '.gitignore'), '.local/\n');
  git(main, 'add', '.');
  git(main, 'commit', '-q', '-m', 'inicio');
  return { dir, main, borrar: () => rmSync(dir, { recursive: true, force: true }) };
}

function commitear(cwd, archivo, texto, mensaje) {
  writeFileSync(join(cwd, archivo), texto);
  git(cwd, 'add', archivo);
  git(cwd, 'commit', '-q', '-m', mensaje);
}

const OK = 'node -e "process.exit(0)"';
const MAL = 'node -e "console.log(\'falla FooTests.Bar\'); process.exit(1)"';

test('carril: preparar clona main en .local/copias con su configuración, y entregar lo pone encima de main', async () => {
  const r = repoMain();
  try {
    assert.match(preparar(r.main, '.local/copias/e-back-1'), /Copia lista/);
    const copia = join(r.main, '.local', 'copias', 'e-back-1');
    assert.equal(git(copia, 'config', 'user.email'), 'prueba@example.com');
    commitear(copia, 'c.txt', 'tres\n', 'feat: c');
    const res = await entregar(copia, { verificar: MAL });
    assert.equal(res.entregados.length, 1, 'sin nada nuevo en main no hace falta verificar');
    assert.match(res.entregados[0], /feat: c$/);
    assert.equal(git(r.main, 'log', '-1', '--format=%s'), 'feat: c');
    assert.equal(readFileSync(join(r.main, 'c.txt'), 'utf8'), 'tres\n', 'el árbol de main avanza');
    assert.equal((await entregar(copia, { verificar: OK })).entregados.length, 0);
  } finally {
    r.borrar();
  }
});

test('carril: dos carriles entregan uno encima del otro, y el segundo se verifica con lo del primero', async () => {
  const r = repoMain();
  try {
    preparar(r.main, '.local/copias/e-back-1');
    preparar(r.main, '.local/copias/e-back-2');
    const uno = join(r.main, '.local', 'copias', 'e-back-1');
    const dos = join(r.main, '.local', 'copias', 'e-back-2');
    commitear(uno, 'a.txt', 'uno cambiado\n', 'feat: uno');
    commitear(dos, 'b.txt', 'dos cambiado\n', 'feat: dos');
    await entregar(uno, { verificar: OK });
    const avisos = [];
    await assert.rejects(entregar(dos, { verificar: MAL, avisar: (s) => avisos.push(s) }), (e) => e instanceof ErrorDeCarril && e.codigo === 5 && /FooTests\.Bar/.test(e.message));
    assert.match(avisos.join('\n'), /Entraron 1 commit/);
    assert.equal(git(r.main, 'log', '-1', '--format=%s'), 'feat: uno', 'lo que no verifica no entra');
    const res = await entregar(dos, { verificar: OK });
    assert.deepEqual(git(r.main, 'log', '--format=%s').split('\n'), ['feat: dos', 'feat: uno', 'inicio']);
    assert.match(res.mensaje, /Entregado en main/);
    assert.match(actualizar(uno), /al día con main: .* feat: dos/);
  } finally {
    r.borrar();
  }
});

test('carril: un choque en el rebase devuelve los archivos, y con el rebase a medias no se entrega', async () => {
  const r = repoMain();
  try {
    preparar(r.main, '.local/copias/e-back-1');
    preparar(r.main, '.local/copias/e-back-2');
    const uno = join(r.main, '.local', 'copias', 'e-back-1');
    const dos = join(r.main, '.local', 'copias', 'e-back-2');
    commitear(uno, 'a.txt', 'de uno\n', 'feat: uno');
    commitear(dos, 'a.txt', 'de dos\n', 'feat: dos');
    await entregar(uno, { verificar: OK });
    await assert.rejects(entregar(dos, { verificar: OK }), (e) => e.codigo === 3 && /a\.txt/.test(e.message));
    await assert.rejects(entregar(dos, { verificar: OK }), (e) => e.codigo === 2 && /rebase a medias/.test(e.message));
    writeFileSync(join(dos, 'a.txt'), 'de uno y de dos\n');
    git(dos, 'add', 'a.txt');
    spawnSync('git', ['rebase', '--continue'], { cwd: dos, env: { ...process.env, GIT_EDITOR: 'true' } });
    await entregar(dos, { verificar: OK });
    assert.equal(readFileSync(join(r.main, 'a.txt'), 'utf8'), 'de uno y de dos\n');
  } finally {
    r.borrar();
  }
});

test('carril: no entrega con cambios sin commitear en la copia ni pisa los de otra sesión en main', async () => {
  const r = repoMain();
  try {
    preparar(r.main, '.local/copias/e-back-1');
    const copia = join(r.main, '.local', 'copias', 'e-back-1');
    writeFileSync(join(copia, 'a.txt'), 'a medias\n');
    await assert.rejects(entregar(copia, { verificar: OK }), (e) => e.codigo === 2 && /sin commitear/.test(e.message));
    git(copia, 'add', 'a.txt');
    git(copia, 'commit', '-q', '-m', 'feat: a');
    writeFileSync(join(r.main, 'a.txt'), 'otra sesión\n');
    await assert.rejects(entregar(copia, { verificar: OK }), (e) => e.codigo === 6);
    assert.equal(readFileSync(join(r.main, 'a.txt'), 'utf8'), 'otra sesión\n', 'lo de la otra sesión sigue');
    assert.throws(() => actualizar(copia), (e) => e.codigo === 2 && /sin entregar/.test(e.message));
  } finally {
    r.borrar();
  }
});

test('carril: la fila deja pasar de a uno, y un candado de un proceso muerto se toma', async () => {
  const r = repoMain();
  try {
    const orden = [];
    let soltar;
    const ocupada = new Promise((res) => { soltar = res; });
    const primero = conCandado(r.main, 'pesada', async () => { orden.push('entra 1'); await ocupada; orden.push('sale 1'); }, { cadaCuanto: 20 });
    await new Promise((res) => setTimeout(res, 50));
    const segundo = conCandado(r.main, 'pesada', async () => { orden.push('entra 2'); }, { cadaCuanto: 20 });
    await new Promise((res) => setTimeout(res, 100));
    soltar();
    await Promise.all([primero, segundo]);
    assert.deepEqual(orden, ['entra 1', 'sale 1', 'entra 2']);

    const candado = join(r.main, '.local', 'copias', '.candado-pesada');
    mkdirSync(candado);
    writeFileSync(join(candado, 'dueno.json'), JSON.stringify({ pid: 999999, desde: new Date().toISOString() }));
    assert.equal(await conCandado(r.main, 'pesada', async () => 'tomado', { cadaCuanto: 20 }), 'tomado');
    assert.equal(existsSync(candado), false, 'se suelta al terminar');
  } finally {
    r.borrar();
  }
});

test('carril: limpiar borra solo lo entregado, y el CLI devuelve sus códigos', async () => {
  const r = repoMain();
  try {
    preparar(r.main, '.local/copias/e-back-1');
    preparar(r.main, '.local/copias/e-back-2');
    commitear(join(r.main, '.local', 'copias', 'e-back-2'), 'z.txt', 'z\n', 'feat: z');
    const res = limpiar(r.main, 'e-back');
    assert.deepEqual(res.borradas, ['e-back-1']);
    assert.match(res.quedan[0], /e-back-2: 1 commit\(s\) sin entregar/);
    const salida = [];
    assert.equal(await ejecutar(['limpiar', 'e-back'], { cwd: r.main, escribir: (s) => salida.push(s) }), 2);
    assert.equal(await ejecutar(['limpiar', 'e-back', '--forzar'], { cwd: r.main, escribir: (s) => salida.push(s) }), 0);
    assert.equal(existsSync(join(r.main, '.local', 'copias', 'e-back-2')), false);
    assert.equal(await ejecutar(['nada'], { cwd: r.main, escribir: (s) => salida.push(s) }), 1);
    assert.equal(await ejecutar(['fila', 'node -e "process.exit(4)"'], { cwd: r.main, escribir: (s) => salida.push(s) }), 4);
  } finally {
    r.borrar();
  }
});

test('carril: la fila corre el comando en --dir con el candado de main, e ignora un -- adelante', async () => {
  const r = repoMain();
  try {
    const otra = join(r.dir, 'copia-del-front');
    mkdirSync(otra);
    const salida = [];
    const escribir = (s) => salida.push(s);
    const crea = `node -e "require('fs').writeFileSync('aca.txt', 'x')"`;
    assert.equal(await ejecutar(['fila', '--dir', otra, crea], { cwd: r.main, escribir }), 0);
    assert.equal(existsSync(join(otra, 'aca.txt')), true, 'corre en la carpeta de --dir');
    assert.equal(existsSync(join(r.main, 'aca.txt')), false);
    assert.equal(existsSync(join(r.main, '.local', 'copias')), true, 'el candado es el de main');
    assert.equal(await ejecutar(['fila', '--', 'node', '-e', '"process.exit(3)"'], { cwd: r.main, escribir }), 3);
    assert.equal(await ejecutar(['fila', '--dir', join(r.dir, 'no-existe'), OK], { cwd: r.main, escribir }), 1);
    assert.match(salida.at(-1), /No existe la carpeta de --dir/);
    assert.equal(await ejecutar(['fila', '--dir', otra], { cwd: r.main, escribir }), 1);
    assert.match(salida.at(-1), /Falta el comando de la fila/);
  } finally {
    r.borrar();
  }
});
