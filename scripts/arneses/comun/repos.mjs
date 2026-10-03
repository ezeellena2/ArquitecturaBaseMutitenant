import { readFile, readdir } from 'node:fs/promises';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const aqui = dirname(fileURLToPath(import.meta.url));

/** Raíz del back: el repo que contiene scripts/arneses. */
export function raizBackPorDefecto() {
  return resolve(aqui, '..', '..', '..');
}

/** El front es el repo hermano `<back>Front`. */
export function raizFrontPorDefecto(back = raizBackPorDefecto()) {
  return join(dirname(back), `${basename(back)}Front`);
}

export function parseEtapasCerradas(texto) {
  const m = texto.match(/ClosedStages\s*=\s*\[(?<e>[^\]]*)\]/);
  if (!m) return [];
  return m.groups.e.split(',').map((x) => Number.parseInt(x.trim(), 10)).filter(Number.isInteger);
}

async function leerSiExiste(ruta) {
  try {
    return await readFile(ruta, 'utf8');
  } catch {
    return null;
  }
}

/** Lee ClosedStages del repo: `src/test/HarnessStage.ts` (front) o `tests/<proyecto>/HarnessStage.cs` (back). */
export async function leerEtapasCerradas(raiz) {
  const ts = await leerSiExiste(join(raiz, 'src', 'test', 'HarnessStage.ts'));
  if (ts !== null) return parseEtapasCerradas(ts);
  const tests = await readdir(join(raiz, 'tests')).catch(() => []);
  for (const carpeta of tests) {
    const cs = await leerSiExiste(join(raiz, 'tests', carpeta, 'HarnessStage.cs'));
    if (cs !== null) return parseEtapasCerradas(cs);
  }
  return [];
}

/** @returns {Promise<{ nombre: 'back'|'front', raiz: string, etapasCerradas: number[] }>} */
export async function cargarRepo(nombre, raiz) {
  return { nombre, raiz, etapasCerradas: await leerEtapasCerradas(raiz) };
}
