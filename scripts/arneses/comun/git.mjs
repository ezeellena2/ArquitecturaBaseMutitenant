import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const ejecutarArchivo = promisify(execFile);

/** Corre git en `cwd` y devuelve la salida; lanza si falla. */
export async function git(cwd, args, { maxBuffer = 256 * 1024 * 1024 } = {}) {
  const { stdout } = await ejecutarArchivo('git', ['-c', 'core.quotepath=false', ...args], { cwd, maxBuffer, windowsHide: true });
  return stdout;
}

/**
 * ¿La ruta (o algo dentro de ella, si es una carpeta) existió en la historia de este repo y fue borrada?
 * Una sola llamada a `git log --diff-filter=D`; si git no puede responder, nada figura como borrado.
 */
export function fabricaHistorial(raiz) {
  let borrados = null;
  const cargar = async () => {
    try {
      const salida = await git(raiz, ['log', '--diff-filter=D', '--name-only', '--format=']);
      return new Set(salida.split(/\r?\n/).filter(Boolean));
    } catch {
      return new Set();
    }
  };
  return async (ruta) => {
    borrados ??= await cargar();
    if (borrados.has(ruta)) return true;
    const prefijo = `${ruta.replace(/\/$/, '')}/`;
    for (const b of borrados) if (b.startsWith(prefijo)) return true;
    return false;
  };
}
