#!/usr/bin/env node
/**
 * CLI único de los arneses ejecutables.
 *   node scripts/arneses/arnes.mjs                          lista los arneses
 *   node scripts/arneses/arnes.mjs <arnés> --detectar       0 tokens: detecta y escribe el informe
 *   node scripts/arneses/arnes.mjs <arnés> [opciones]       corrida completa (ver README.md)
 */
import { readFile, readdir } from 'node:fs/promises';
import { join, dirname } from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';
import { existsSync } from 'node:fs';
import { indexarArchivos } from './comun/archivos.mjs';
import { coincide } from './comun/glob.mjs';
import { contar, crearHallazgo, filtrarIgnorados, ordenar } from './comun/hallazgos.mjs';
import { escribirInforme, fechaCorrida, resumenMarkdown } from './comun/informe.mjs';
import * as markdown from './comun/markdown.mjs';
import { cargarRepo, raizBackPorDefecto, raizFrontPorDefecto } from './comun/repos.mjs';

const aqui = dirname(fileURLToPath(import.meta.url));

export function parsearArgumentos(argv) {
  const opciones = { _: [] };
  const conValor = new Set(['repo', 'motor', 'modelo', 'presupuesto', 'back', 'front']);
  for (let i = 0; i < argv.length; i += 1) {
    const a = argv[i];
    if (a.startsWith('--')) {
      const nombre = a.slice(2);
      opciones[nombre] = conValor.has(nombre) ? argv[++i] : true;
    } else opciones._.push(a);
  }
  return opciones;
}

export async function listarArneses(dirArneses) {
  const entradas = await readdir(dirArneses, { withFileTypes: true });
  const out = [];
  for (const e of entradas) {
    if (!e.isDirectory() || e.name.startsWith('_')) continue;
    const manifiesto = await leerJson(join(dirArneses, e.name, 'arnes.json'));
    if (manifiesto) out.push({ carpeta: e.name, manifiesto });
  }
  return out;
}

export async function leerJson(ruta, porDefecto = null) {
  try {
    return JSON.parse(await readFile(ruta, 'utf8'));
  } catch {
    return porDefecto;
  }
}

/** Corre los detectores del arnés sobre cada repo y devuelve los hallazgos sin ignorados, ordenados. */
export async function detectar({ dirArneses, arnes, raices, soloRepo }) {
  const { manifiesto } = arnes;
  const ignorados = (await leerJson(join(dirArneses, arnes.carpeta, 'ignorados.json'), [])) ?? [];
  const todos = [];
  for (const nombre of manifiesto.repos ?? ['back']) {
    if (soloRepo && soloRepo !== nombre) continue;
    const raiz = raices[nombre];
    if (!raiz || !existsSync(raiz)) continue;
    const repo = await cargarRepo(nombre, raiz);
    const archivos = await indexarArchivos(raiz);
    const excluido = (rel) => coincide(rel, manifiesto.excluir ?? []);
    const ctx = {
      repo: nombre,
      raiz,
      repoInfo: repo,
      archivos,
      markdown,
      etapasCerradas: repo.etapasCerradas,
      manifiesto,
      raices,
      excluido,
      leer: (rel) => readFile(join(raiz, rel), 'utf8'),
      crear: (campos) => crearHallazgo({ ...campos, repo: nombre, detector: ctx.detectorActual }),
    };
    for (const nombreDetector of manifiesto.detectores ?? []) {
      const modulo = await import(pathToFileURL(join(dirArneses, arnes.carpeta, 'detectores', `${nombreDetector}.mjs`)).href);
      if (modulo.repos && !modulo.repos.includes(nombre)) continue;
      ctx.detectorActual = modulo.nombre ?? nombreDetector;
      const encontrados = await modulo.detectar(ctx);
      todos.push(...encontrados.filter((h) => !excluido(h.archivo)));
    }
  }
  return ordenar(filtrarIgnorados(todos, ignorados));
}

export async function ejecutar(argv, opts = {}) {
  const escribir = opts.escribir ?? ((t) => process.stdout.write(`${t}\n`));
  const dirArneses = opts.dirArneses ?? aqui;
  const args = parsearArgumentos(argv);
  try {
    const arneses = await listarArneses(dirArneses);
    if (args._.length === 0 && !args.instalar) {
      escribir('Arneses disponibles:');
      for (const a of arneses) escribir(`  ${a.carpeta.padEnd(18)} ${a.manifiesto.descripcion ?? ''}`);
      return 0;
    }
    const arnes = arneses.find((a) => a.carpeta === args._[0]);
    if (!arnes) {
      escribir(`No existe el arnés «${args._[0]}».`);
      return 1;
    }
    const back = args.back ?? raizBackPorDefecto();
    const raices = { back, front: args.front ?? raizFrontPorDefecto(back) };
    const dirInformes = opts.dirInformes ?? join(back, '.local', 'arneses');
    if (args.detectar) {
      const hallazgos = await detectar({ dirArneses, arnes, raices, soloRepo: args.repo });
      const info = { arnes: arnes.carpeta, fecha: fechaCorrida(), modo: 'detectar', hallazgos, contadores: contar(hallazgos) };
      const rutas = await escribirInforme(dirInformes, info);
      const c = info.contadores;
      escribir(`auto: ${c.auto} · modelo: ${c.modelo} · humano: ${c.humano}`);
      escribir(`Informe: ${rutas.md}`);
      return hallazgos.length ? 2 : 0;
    }
    escribir('La corrida completa todavía no está disponible; usá --detectar.');
    return 1;
  } catch (e) {
    escribir(`Falló: ${e.stack ?? e}`);
    return 1;
  }
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  process.exitCode = await ejecutar(process.argv.slice(2));
}

export { resumenMarkdown };
