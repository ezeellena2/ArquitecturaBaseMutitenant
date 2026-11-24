#!/usr/bin/env node
/**
 * Los carriles del back del conductor (ADR 0071): cada carril programa en su propia copia de main, dentro de
 * .local/copias/, y entrega sus commits encima de main sin ramas ni worktrees.
 *
 *   node scripts/arneses/carril.mjs preparar <copia>     clona main en la copia, o la pone al día si ya existe
 *   node scripts/arneses/carril.mjs actualizar <copia>   trae a la copia lo último de main (solo avanza)
 *   node scripts/arneses/carril.mjs entregar <copia>     pone los commits de la copia encima de main, de a un carril por vez
 *   node scripts/arneses/carril.mjs fila "<comando>"     corre el comando cuando la fila pesada está libre
 *   node scripts/arneses/carril.mjs limpiar <prefijo>    borra las copias que empiezan así y ya entregaron todo
 *
 * Las rutas de las copias son relativas a la raíz de main. Sale 0 si anduvo; 2 si la copia no está lista (cambios
 * sin commitear, un rebase a medias o commits sin entregar); 3 si el rebase chocó (resolvelo en la copia y volvé a
 * entregar); 5 si lo de main rompió la verificación de la copia; 6 si main tiene cambios ajenos en esos archivos.
 */
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statfsSync, writeFileSync } from 'node:fs';
import { isAbsolute, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const DISCO_MINIMO = 15 * 1024 ** 3;
const CONFIG_QUE_SE_COPIA = /^(user\.name|user\.email|core\.autocrlf|core\.eol|core\.safecrlf|core\.longpaths|core\.hookspath)=/i;
const VERIFICAR = 'node scripts/arneses/verificar.mjs';

export class ErrorDeCarril extends Error {
  constructor(codigo, mensaje) {
    super(mensaje);
    this.codigo = codigo;
  }
}

function git(cwd, args, { permitirFallo = false } = {}) {
  const r = spawnSync('git', args, { cwd, encoding: 'utf8', windowsHide: true });
  const res = { codigo: r.status ?? 1, salida: (r.stdout || '').trim(), error: (r.stderr || '').trim() };
  if (!permitirFallo && res.codigo !== 0) throw new Error(`git ${args.join(' ')}: ${res.error || res.salida}`);
  return res;
}

export function raizDe(dir) {
  return git(dir, ['rev-parse', '--show-toplevel']).salida;
}

function rutaDeCopia(main, copia) {
  return isAbsolute(copia) ? copia : resolve(main, copia);
}

function mainDe(copia) {
  return resolve(git(copia, ['remote', 'get-url', 'origin']).salida);
}

function enRebase(copia) {
  const gitDir = resolve(copia, git(copia, ['rev-parse', '--git-dir']).salida);
  return existsSync(join(gitDir, 'rebase-merge')) || existsSync(join(gitDir, 'rebase-apply'));
}

function exigirLimpia(copia) {
  if (enRebase(copia)) {
    throw new ErrorDeCarril(2, 'La copia tiene un rebase a medias: resolvé los conflictos, git add, GIT_EDITOR=true git rebase --continue y volvé a entregar.');
  }
  const sucios = git(copia, ['status', '--porcelain']).salida;
  if (sucios) throw new ErrorDeCarril(2, `La copia tiene cambios sin commitear:\n${sucios}`);
}

function sinEntregar(copia) {
  return Number(git(copia, ['rev-list', '--count', 'origin/main..HEAD']).salida);
}

/** Clona main en la copia, con la configuración local que importa, o la pone al día si ya existe. */
export function preparar(main, copia) {
  const destino = rutaDeCopia(main, copia);
  if (existsSync(join(destino, '.git'))) return actualizar(destino);
  const disco = statfsSync(main);
  if (disco.bavail * disco.bsize < DISCO_MINIMO) {
    throw new ErrorDeCarril(1, `Quedan menos de 15 GB libres: no se crea otra copia. Borrá las viejas con limpiar.`);
  }
  mkdirSync(resolve(destino, '..'), { recursive: true });
  // La configuración va con el clone, así el checkout ya sale con los mismos finales de línea que main.
  const local = git(main, ['config', '--local', '--list'], { permitirFallo: true }).salida.split('\n').filter((l) => CONFIG_QUE_SE_COPIA.test(l));
  git(main, ['clone', '-q', ...local.flatMap((l) => ['-c', l]), main, destino]);
  return `Copia lista en ${destino}, en ${git(destino, ['log', '-1', '--format=%h %s']).salida}.`;
}

/** Trae a la copia lo último de main. Solo avanza: lo propio se entrega antes. */
export function actualizar(copia) {
  exigirLimpia(copia);
  git(copia, ['fetch', '-q', 'origin', 'main']);
  const propios = sinEntregar(copia);
  if (propios) throw new ErrorDeCarril(2, `La copia tiene ${propios} commit(s) sin entregar: corré entregar antes.`);
  git(copia, ['merge', '-q', '--ff-only', 'origin/main']);
  return `Copia al día con main: ${git(copia, ['log', '-1', '--format=%h %s']).salida}.`;
}

function vivo(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch (e) {
    return e.code === 'EPERM';
  }
}

const esperar = (ms) => new Promise((r) => setTimeout(r, ms));

/**
 * Un candado entre procesos: una carpeta en .local/copias/ que se crea de forma atómica. Si su dueño murió o pasó
 * el vencimiento, se toma igual.
 */
export async function conCandado(main, nombre, fn, { esperaMaxima = 3 * 3600_000, vence = 3 * 3600_000, cadaCuanto = 3000, avisar = () => {} } = {}) {
  const base = join(main, '.local', 'copias');
  mkdirSync(base, { recursive: true });
  const dir = join(base, `.candado-${nombre}`);
  const desde = Date.now();
  let avisado = false;
  for (;;) {
    try {
      mkdirSync(dir);
      writeFileSync(join(dir, 'dueno.json'), JSON.stringify({ pid: process.pid, desde: new Date().toISOString() }));
      break;
    } catch (e) {
      if (e.code !== 'EEXIST') throw e;
      let dueno = null;
      try { dueno = JSON.parse(readFileSync(join(dir, 'dueno.json'), 'utf8')); } catch { /* recién creada: todavía sin dueño */ }
      const viejo = dueno && (Date.now() - Date.parse(dueno.desde) > vence || !vivo(dueno.pid));
      if (viejo) { rmSync(dir, { recursive: true, force: true }); continue; }
      if (Date.now() - desde > esperaMaxima) throw new ErrorDeCarril(1, `La fila «${nombre}» sigue ocupada después de esperar: ${JSON.stringify(dueno)}`);
      if (!avisado) { avisar(`Esperando la fila «${nombre}» (la tiene el proceso ${dueno ? dueno.pid : '?'})...`); avisado = true; }
      await esperar(cadaCuanto);
    }
  }
  try {
    return await fn();
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
}

/**
 * Pone los commits de la copia encima de main. Si main avanzó, primero rebasa la copia y corre la verificación
 * con lo que entró; recién después main avanza con --ff-only. Un carril por vez.
 */
export async function entregar(copia, { verificar = VERIFICAR, avisar = () => {} } = {}) {
  exigirLimpia(copia);
  const main = mainDe(copia);
  const rama = git(main, ['rev-parse', '--abbrev-ref', 'HEAD']).salida;
  if (rama !== 'main') throw new ErrorDeCarril(6, `El checkout de main está en ${rama}: no se entrega.`);
  return conCandado(main, 'entrega', async () => {
    for (let intento = 1; intento <= 3; intento++) {
      git(copia, ['fetch', '-q', 'origin', 'main']);
      if (!sinEntregar(copia)) return { entregados: [], mensaje: 'No hay commits para entregar.' };
      const detras = Number(git(copia, ['rev-list', '--count', 'HEAD..origin/main']).salida);
      if (detras) {
        const r = git(copia, ['rebase', '-q', 'origin/main'], { permitirFallo: true });
        if (r.codigo !== 0) {
          const choques = git(copia, ['diff', '--name-only', '--diff-filter=U'], { permitirFallo: true }).salida;
          throw new ErrorDeCarril(3, `El rebase sobre main chocó en:\n${choques || r.error}\nResolvelo en la copia (git add, GIT_EDITOR=true git rebase --continue) y volvé a entregar.`);
        }
        avisar(`Entraron ${detras} commit(s) de main: verifico la copia antes de entregar.`);
        const v = spawnSync(verificar, { cwd: copia, shell: true, encoding: 'utf8', windowsHide: true });
        if (v.status !== 0) {
          throw new ErrorDeCarril(5, `Con lo que entró de main, la copia no pasa la verificación:\n${`${v.stdout || ''}${v.stderr || ''}`.trim().split('\n').slice(-40).join('\n')}`);
        }
      }
      const antes = git(main, ['rev-parse', 'HEAD']).salida;
      const nuevo = git(copia, ['rev-parse', 'HEAD']).salida;
      git(main, ['fetch', '-q', copia, 'HEAD']);
      const m = git(main, ['merge', '-q', '--ff-only', nuevo], { permitirFallo: true });
      if (m.codigo === 0) {
        const entregados = git(main, ['log', '--format=%h %s', `${antes}..${nuevo}`]).salida.split('\n').filter(Boolean);
        return { entregados, mensaje: `Entregado en main:\n${entregados.join('\n')}` };
      }
      if (/would be overwritten|serían sobrescritos|sobrescrit/i.test(m.error)) {
        throw new ErrorDeCarril(6, `main tiene cambios sin commitear de otra sesión en archivos que tocan estos commits:\n${m.error}`);
      }
      if (git(main, ['rev-parse', 'HEAD']).salida === antes) throw new Error(`git merge --ff-only en main: ${m.error}`);
      avisar('main avanzó mientras tanto: vuelvo a rebasar.');
    }
    throw new ErrorDeCarril(1, 'main siguió avanzando en tres intentos: volvé a entregar en un rato.');
  }, { esperaMaxima: 30 * 60_000, vence: 30 * 60_000, avisar });
}

/** Corre un comando pesado (los tests de integración) cuando nadie más lo está haciendo. */
export function enFila(main, comando, { avisar = () => {} } = {}) {
  return conCandado(main, 'pesada', () => new Promise((res) => {
    const p = spawn(comando, { cwd: process.cwd(), shell: true, stdio: 'inherit', windowsHide: true });
    p.on('exit', (codigo) => res(codigo ?? 1));
  }), { avisar });
}

/** Borra las copias de un prefijo. Una copia con commits sin entregar o cambios sin commitear no se borra, salvo forzar. */
export function limpiar(main, prefijo, { forzar = false } = {}) {
  if (!prefijo || prefijo.startsWith('.')) throw new ErrorDeCarril(1, 'Falta el prefijo de las copias.');
  const base = join(main, '.local', 'copias');
  if (!existsSync(base)) return { borradas: [], quedan: [] };
  const borradas = [];
  const quedan = [];
  for (const nombre of readdirSync(base)) {
    if (!nombre.startsWith(prefijo)) continue;
    const dir = join(base, nombre);
    if (!forzar && existsSync(join(dir, '.git'))) {
      try {
        exigirLimpia(dir);
        git(dir, ['fetch', '-q', 'origin', 'main']);
        const propios = sinEntregar(dir);
        if (propios) { quedan.push(`${nombre}: ${propios} commit(s) sin entregar`); continue; }
      } catch (e) {
        quedan.push(`${nombre}: ${e.message.split('\n')[0]}`);
        continue;
      }
    }
    rmSync(dir, { recursive: true, force: true, maxRetries: 5, retryDelay: 1000 });
    borradas.push(nombre);
  }
  return { borradas, quedan };
}

export async function ejecutar(argv, { cwd = process.cwd(), escribir = (s) => process.stdout.write(`${s}\n`) } = {}) {
  const [orden, ...resto] = argv;
  try {
    // Desde una copia también: su origin es la carpeta de main.
    const aca = raizDe(cwd);
    const origen = git(aca, ['remote', 'get-url', 'origin'], { permitirFallo: true }).salida;
    const main = origen && existsSync(join(origen, '.git')) ? resolve(origen) : aca;
    if (orden === 'preparar' && resto[0]) escribir(preparar(main, resto[0]));
    else if (orden === 'actualizar' && resto[0]) escribir(actualizar(rutaDeCopia(main, resto[0])));
    else if (orden === 'entregar' && resto[0]) {
      const i = resto.indexOf('--verificar');
      const r = await entregar(rutaDeCopia(main, resto[0]), { verificar: i >= 0 ? resto[i + 1] : VERIFICAR, avisar: escribir });
      escribir(r.mensaje);
    } else if (orden === 'fila' && resto.length) {
      return await enFila(main, resto.join(' '), { avisar: escribir });
    } else if (orden === 'limpiar' && resto[0]) {
      const r = limpiar(main, resto[0], { forzar: resto.includes('--forzar') });
      escribir(`Borradas: ${r.borradas.join(', ') || 'ninguna'}.${r.quedan.length ? `\nQuedan:\n${r.quedan.join('\n')}` : ''}`);
      return r.quedan.length ? 2 : 0;
    } else {
      escribir('Uso: carril.mjs preparar|actualizar|entregar <copia> · fila "<comando>" · limpiar <prefijo> [--forzar]');
      return 1;
    }
    return 0;
  } catch (e) {
    escribir(e.message);
    return e instanceof ErrorDeCarril ? e.codigo : 1;
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exitCode = await ejecutar(process.argv.slice(2));
}
