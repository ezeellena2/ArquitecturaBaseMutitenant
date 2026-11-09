import { appendFile, mkdir, readFile, rm, rmdir, symlink } from 'node:fs/promises';
import { existsSync, lstatSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';
import { git } from './git.mjs';

/**
 * Carpeta de la copia del corte, dentro del repo y fuera del control de versiones:
 * `.local/arneses/copias/<arnés>-<fecha>`. Sin worktrees ni ramas (AGENTS.md, «Todo en `main`»).
 */
export function carpetaDeLaCopia(raiz, arnes, fecha) {
  return join(resolve(raiz), '.local', 'arneses', 'copias', `${arnes}-${fecha}`);
}

/** La configuración que la copia hereda del repo: quién commitea y cómo se guardan los fines de línea. */
const heredada = ['user.name', 'user.email', 'core.autocrlf'];

/**
 * Copia local de HEAD (lo commiteado, no lo que está a medio hacer), como `/puerta`: `git clone --local` y
 * `checkout --detach`. No suma ramas ni worktrees al repo; lo que se commitea queda en la copia.
 */
export async function crearCopia(raiz, carpeta) {
  const corte = (await git(raiz, ['rev-parse', 'HEAD'])).trim();
  // La copia es un repo dentro de `.local/` del repo de trabajo: aunque este no lo ignore, git no la ve ni la suma.
  await excluirLocal(raiz);
  await mkdir(dirname(carpeta), { recursive: true });
  await git(dirname(carpeta), ['clone', '-q', '--local', '--no-checkout', resolve(raiz), carpeta]);
  for (const clave of heredada) {
    const valor = (await git(raiz, ['config', '--get', clave]).catch(() => '')).trim();
    if (valor) await git(carpeta, ['config', clave, valor]);
  }
  await git(carpeta, ['checkout', '-q', '--detach', corte]);
  // `.local/` guarda las tareas del modelo dentro de la copia sin ensuciarla, aunque el repo no lo ignore.
  await excluirLocal(carpeta);
  return { carpeta, corte };
}

/** Suma `.local/` al `info/exclude` del repo, que es local y no se versiona. */
async function excluirLocal(repo) {
  const excluir = resolve(repo, (await git(repo, ['rev-parse', '--git-path', 'info/exclude'])).trim());
  await mkdir(dirname(excluir), { recursive: true });
  const actual = existsSync(excluir) ? await readFile(excluir, 'utf8') : '';
  if (!/^\.local\/$/m.test(actual)) await appendFile(excluir, `${actual.endsWith('\n') || !actual ? '' : '\n'}.local/\n`);
}

/**
 * Trae a la rama actual del repo el commit de la copia, solo si es un avance rápido (`--ff-only`) y sin crear una rama:
 * lo busca con `fetch` (queda en FETCH_HEAD) y lo fusiona. Si el repo avanzó desde el corte, falla y no toca nada.
 */
export async function fusionarCopia(raiz, carpeta) {
  await git(raiz, ['fetch', '-q', '--no-tags', resolve(carpeta), 'HEAD']);
  await git(raiz, ['merge', '-q', '--ff-only', 'FETCH_HEAD']);
}

/** Quita la copia: primero el enlace a `node_modules`, sin tocar su destino. */
export async function quitarCopia(carpeta) {
  await soltarEnlaces([join(carpeta, 'node_modules')]);
  await rm(carpeta, { recursive: true, force: true, maxRetries: 3 });
}

/** Para verificar el front en la copia hace falta `node_modules`: se enlaza el del repo (una junction en Windows). */
export async function enlazarNodeModules(raiz, carpeta) {
  const origen = join(raiz, 'node_modules');
  const destino = join(carpeta, 'node_modules');
  if (!existsSync(origen) || existsSync(destino)) return false;
  await symlink(origen, destino, 'junction');
  return true;
}

/**
 * Los tests de arquitectura miran el repo hermano en `../<Repo>`: se lo enlaza (junction) al lado de la copia mientras
 * dura la verificación. Devuelve los enlaces creados; `soltarEnlaces` los quita sin tocar el destino.
 */
export async function enlazarHermanos(carpeta, hermanos) {
  const creados = [];
  for (const origen of hermanos.filter((h) => h && existsSync(h))) {
    const destino = join(dirname(carpeta), basename(origen));
    if (existsSync(destino)) continue;
    await symlink(origen, destino, 'junction');
    creados.push(destino);
  }
  return creados;
}

export async function soltarEnlaces(enlaces) {
  for (const e of enlaces) {
    try {
      if (lstatSync(e).isSymbolicLink()) await rmdir(e);
    } catch {
      /* ya no está */
    }
  }
}
