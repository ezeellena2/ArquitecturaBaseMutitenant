import { readdir } from 'node:fs/promises';
import { join } from 'node:path';

const ignoradas = new Set(['bin', 'obj', 'node_modules', 'dist', '.git', '.local', 'TestResults', '.vs',
  '.artifacts', '.playwright-mcp', '.emails']);

/** Índice del árbol de archivos de un repo (rutas relativas con `/`). */
export async function indexarArchivos(raiz) {
  const archivos = [];
  const directorios = new Set();
  async function recorrer(abs, rel) {
    let entradas;
    try {
      entradas = await readdir(abs, { withFileTypes: true });
    } catch {
      return;
    }
    for (const e of entradas) {
      const r = rel ? `${rel}/${e.name}` : e.name;
      // `fixtures/` existe (los docs la nombran) pero no se mira adentro: son repos falsos de los tests de los arneses.
      if (e.isDirectory() && e.name === 'fixtures') {
        directorios.add(r);
        continue;
      }
      if (ignoradas.has(e.name)) continue;
      if (e.isDirectory()) {
        directorios.add(r);
        await recorrer(join(abs, e.name), r);
      } else {
        archivos.push(r);
      }
    }
  }
  await recorrer(raiz, '');
  const nombres = new Map();
  for (const a of archivos) {
    const n = a.slice(a.lastIndexOf('/') + 1);
    if (!nombres.has(n)) nombres.set(n, []);
    nombres.get(n).push(a);
  }
  const set = new Set(archivos);
  return {
    raiz,
    archivos,
    directorios,
    existe: (rel) => set.has(rel) || directorios.has(rel),
    esArchivo: (rel) => set.has(rel),
    esDirectorio: (rel) => directorios.has(rel),
    porNombre: (nombre) => nombres.get(nombre) ?? [],
  };
}
