import { mkdir, readFile, rmdir } from 'node:fs/promises';
import { existsSync, lstatSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';
import { git } from './git.mjs';

/** Carpeta del worktree, fuera del repo: `../.arneses/<repo>-<arnés>-<fecha>`. */
export function carpetaDelWorktree(raiz, arnes, fecha) {
  return join(dirname(resolve(raiz)), '.arneses', `${basename(resolve(raiz))}-${arnes}-${fecha}`);
}

export const nombreDeRama = (arnes, fecha) => `arnes/${arnes}/${fecha}`;

/** Crea la rama y el worktree desde HEAD (lo commiteado, no lo que está a medio hacer). */
export async function crearWorktree(raiz, { rama, carpeta }) {
  await mkdir(dirname(carpeta), { recursive: true });
  await git(raiz, ['worktree', 'add', '-b', rama, carpeta, 'HEAD']);
  // `.local/` guarda las tareas del modelo dentro del worktree sin ensuciarlo, aunque el repo no lo ignore.
  const excluir = (await git(carpeta, ['rev-parse', '--git-path', 'info/exclude'])).trim();
  const ruta = resolve(carpeta, excluir);
  await mkdir(dirname(ruta), { recursive: true });
  const actual = existsSync(ruta) ? await readFile(ruta, 'utf8') : '';
  if (!/^\.local\/$/m.test(actual)) {
    const { appendFile } = await import('node:fs/promises');
    await appendFile(ruta, `${actual.endsWith('\n') || !actual ? '' : '\n'}.local/\n`);
  }
  return carpeta;
}

/** Quita el worktree (y los enlaces a `node_modules`, sin tocar su destino). */
export async function quitarWorktree(raiz, carpeta) {
  const enlace = join(carpeta, 'node_modules');
  try {
    if (existsSync(enlace) && lstatSync(enlace).isSymbolicLink()) await rmdir(enlace);
  } catch {
    /* si no se puede quitar el enlace, git worktree remove lo intenta igual */
  }
  await git(raiz, ['worktree', 'remove', '--force', carpeta]);
  await git(raiz, ['worktree', 'prune']);
}

/** Para verificar el front en el worktree hace falta `node_modules`: se enlaza el del repo (una junction en Windows). */
export async function enlazarNodeModules(raiz, carpeta) {
  const origen = join(raiz, 'node_modules');
  const destino = join(carpeta, 'node_modules');
  if (!existsSync(origen) || existsSync(destino)) return false;
  const { symlink } = await import('node:fs/promises');
  await symlink(origen, destino, 'junction');
  return true;
}

/**
 * Los tests de arquitectura miran el repo hermano en `../<Repo>` y el worktree vive en `../.arneses/`:
 * se enlaza (junction) el hermano al lado del worktree mientras dura la verificación.
 * Devuelve los enlaces creados; `soltarEnlaces` los quita sin tocar el destino.
 */
export async function enlazarHermanos(carpeta, hermanos) {
  const { symlink } = await import('node:fs/promises');
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
