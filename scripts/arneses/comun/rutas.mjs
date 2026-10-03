import { existsSync } from 'node:fs';
import { posix, resolve } from 'node:path';

/**
 * Convención de arnes.md §3: `Application/X` = `src/<Proyecto>.Application/X`.
 * Se generaliza a todo proyecto de `src/` y `tests/`: `Api.IntegrationTests/X` = `tests/<Proyecto>.Api.IntegrationTests/X`.
 */
export function mapaDeCapas(archivos) {
  const out = new Map();
  for (const d of archivos.directorios) {
    const m = d.match(/^(src|tests)\/([^/]+)$/);
    if (!m) continue;
    const punto = m[2].indexOf('.');
    if (punto > 0) out.set(m[2].slice(punto + 1), d);
  }
  return out;
}

const tieneExtension = (v) => /\.[A-Za-z0-9]+$/.test(v.slice(v.lastIndexOf('/') + 1));

/**
 * Resuelve el valor de un backtick o de un enlace contra el repo. Devuelve:
 *  - { estado: 'existe', ruta }  — la ruta real (relativa a la raíz); puede estar en el repo hermano
 *  - { estado: 'fuera' }         — apunta fuera del par de repos (no se puede juzgar)
 *  - { estado: 'falta', candidatos, esDirectorio }
 * Con `estricto` (enlaces) solo cuenta la ruta relativa al archivo.
 */
export function resolverValor(valor, { archivos, capas, origen, raiz, estricto = false, pares = [], hermano = null }) {
  const limpio = valor.replace(/^\.\//, '');
  const esDir = limpio.endsWith('/') || !tieneExtension(limpio);
  let v = limpio.replace(/\/$/, '');
  const aqui = posix.dirname(origen);
  const relativo = posix.normalize(posix.join(aqui, v));

  if (!estricto && pares.includes(v.split('/')[0])) v = `../${v}`;
  if (estricto || v.startsWith('../')) {
    const desdeRaiz = posix.normalize(v);
    for (const r of estricto ? [relativo] : [desdeRaiz, relativo]) {
      if (r.startsWith('..')) {
        const fuera = resolverFuera(r, raiz, pares);
        if (fuera.estado !== 'falta') return fuera;
      } else if (archivos.existe(r)) return { estado: 'existe', ruta: r };
    }
    const r = estricto ? relativo : desdeRaiz;
    if (r.startsWith('..')) return resolverFuera(r, raiz, pares);
    return faltante(v, esDir, archivos);
  }

  if (archivos.existe(v)) return { estado: 'existe', ruta: v };
  if (archivos.existe(relativo)) return { estado: 'existe', ruta: relativo };
  const [primero, ...resto] = v.split('/');
  if (capas.has(primero)) {
    const r = [capas.get(primero), ...resto].join('/');
    if (archivos.existe(r)) return { estado: 'existe', ruta: r };
  }
  // `ArquitecturaBase/src/...`: un repo hermano que no es el par back/front; no se puede juzgar.
  if (raiz && v.includes('/') && !pares.includes(primero) && !archivos.esDirectorio(primero) && existsSync(resolve(raiz, '..', primero))) return { estado: 'fuera' };
  const enEste = porSufijo(v, esDir, archivos);
  if (enEste) return { estado: 'existe', ruta: enEste };
  if (hermano) {
    if (hermano.existe(v)) return { estado: 'existe', ruta: v };
    const enHermano = porSufijo(v, esDir, hermano);
    if (enHermano) return { estado: 'existe', ruta: enHermano };
  }
  return faltante(v, esDir, archivos);
}

function porSufijo(v, esDir, archivos) {
  if (esDir) {
    for (const d of archivos.directorios) if (d === v || d.endsWith(`/${v}`)) return d;
    return null;
  }
  const nombre = v.slice(v.lastIndexOf('/') + 1);
  return archivos.porNombre(nombre).find((p) => p === v || p.endsWith(`/${v}`)) ?? null;
}

function resolverFuera(relativo, raiz, pares) {
  const hermano = relativo.split('/')[1];
  if (!raiz || !pares.includes(hermano)) return { estado: 'fuera' };
  if (existsSync(resolve(raiz, relativo))) return { estado: 'existe', ruta: relativo };
  return existsSync(resolve(raiz, '..', hermano)) ? { estado: 'falta', candidatos: [], esDirectorio: !tieneExtension(relativo) } : { estado: 'fuera' };
}

function faltante(v, esDir, archivos) {
  const nombre = v.slice(v.lastIndexOf('/') + 1);
  const candidatos = esDir
    ? [...archivos.directorios].filter((d) => d === nombre || d.endsWith(`/${nombre}`))
    : archivos.porNombre(nombre);
  return { estado: 'falta', candidatos, esDirectorio: esDir };
}

/** Escribe `candidato` con el mismo estilo que el valor original (capa, raíz o cola). */
export function estiloDe(original, candidato, { archivos, capas }) {
  const barra = original.endsWith('/') ? '/' : '';
  const o = original.replace(/\/$/, '');
  const [primero] = o.split('/');
  for (const [capa, dir] of capas) {
    if (primero === capa && candidato.startsWith(`${dir}/`)) return `${capa}/${candidato.slice(dir.length + 1)}${barra}`;
  }
  if (o.includes('/') && archivos.esDirectorio(primero)) return `${candidato}${barra}`;
  const k = o.split('/').length;
  const partes = candidato.split('/');
  return `${partes.slice(-k).join('/')}${barra}`;
}
