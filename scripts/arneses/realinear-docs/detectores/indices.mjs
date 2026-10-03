import { posix } from 'node:path';
import { enlaces, estaCerrada, filasDeTabla, marcasEtapa } from '../../comun/markdown.mjs';

export const nombre = 'indices';

/** Línea (1-based) de cada fila de tabla con sus enlaces, resueltos desde `archivo`. */
function filasConEnlaces(texto, archivo) {
  const porLinea = new Map();
  for (const e of enlaces(texto)) {
    if (!porLinea.has(e.linea)) porLinea.set(e.linea, []);
    porLinea.get(e.linea).push({ ...e, destino: e.ruta ? posix.normalize(posix.join(posix.dirname(archivo), e.ruta)) : archivo });
  }
  return filasDeTabla(texto).map((f) => ({ ...f, enlaces: porLinea.get(f.linea) ?? [] }));
}

function planeada(texto, linea, cerradas) {
  const marcas = marcasEtapa(texto.split(/\r?\n/)[linea - 1] ?? '');
  return marcas.length > 0 && !estaCerrada(marcas, cerradas);
}

/** Una fila cuyos enlaces apuntan todos a archivos que no existen y sin reemplazo: se borra. */
function filaHuerfana(ctx, archivo, texto, fila) {
  if (!fila.enlaces.length || planeada(texto, fila.linea, ctx.etapasCerradas)) return null;
  const faltan = fila.enlaces.every((e) => e.ruta && !ctx.archivos.existe(e.destino) && ctx.archivos.porNombre(posix.basename(e.destino)).length === 0);
  if (!faltan) return null;
  const linea = texto.split(/\r?\n/)[fila.linea - 1];
  const rotos = fila.enlaces.map((e) => e.ruta).join(', ');
  return ctx.crear({ archivo, linea: fila.linea, tipo: 'fila-huerfana', clave: fila.enlaces[0].destino, real: `la fila apunta a ${rotos}, que no existe`, accion: 'auto', parche: { tipo: 'borrar-linea', archivo, linea: fila.linea, de: linea } });
}

export async function detectar(ctx) {
  const out = [];
  const fichas = ctx.archivos.archivos.filter((a) => /^docs\/rules\/[^/]+\.md$/.test(a) && a !== 'docs/rules/README.md');

  if (ctx.archivos.esArchivo('docs/rules/README.md')) {
    const archivo = 'docs/rules/README.md';
    const texto = await ctx.leer(archivo);
    const filas = filasConEnlaces(texto, archivo);
    for (const f of filas) {
      const h = filaHuerfana(ctx, archivo, texto, f);
      if (h) out.push(h);
    }
    const enIndice = new Set(filas.flatMap((f) => f.enlaces.map((e) => e.destino)));
    const ultima = filas.at(-1)?.linea ?? 1;
    for (const ficha of fichas) {
      if (enIndice.has(ficha)) continue;
      const nombreFicha = posix.basename(ficha);
      out.push(ctx.crear({ archivo, linea: ultima, tipo: 'ficha-sin-fila', clave: nombreFicha, real: `${ficha} no tiene fila en docs/rules/README.md`, esperado: `| [${nombreFicha.replace(/\.md$/, '')}](${nombreFicha}) | <tema en una línea> |`, contexto: (await ctx.leer(ficha)).split(/\r?\n/).slice(0, 8).join('\n'), accion: 'modelo' }));
    }
  }

  if (ctx.archivos.esArchivo('docs/decisions/README.md')) {
    const archivo = 'docs/decisions/README.md';
    const texto = await ctx.leer(archivo);
    const numeros = new Set(filasDeTabla(texto).map((f) => f.celdas[0]?.match(/^\d{4}/)?.[0]).filter(Boolean));
    const ultima = filasDeTabla(texto).at(-1)?.linea ?? 1;
    for (const adr of ctx.archivos.archivos.filter((a) => /^docs\/decisions\/\d{4}-[^/]+\.md$/.test(a))) {
      const numero = posix.basename(adr).slice(0, 4);
      if (numeros.has(numero)) continue;
      out.push(ctx.crear({ archivo, linea: ultima, tipo: 'adr-sin-fila', clave: posix.basename(adr), real: `${adr} no tiene fila en el índice de decisiones`, esperado: `| ${numero} | <decisión en una línea> | <estado> |`, contexto: (await ctx.leer(adr)).split(/\r?\n/).slice(0, 12).join('\n'), accion: 'modelo' }));
    }
  }

  if (ctx.archivos.esArchivo('AGENTS.md')) {
    const archivo = 'AGENTS.md';
    const texto = await ctx.leer(archivo);
    const filas = filasConEnlaces(texto, archivo);
    for (const f of filas) {
      const h = filaHuerfana(ctx, archivo, texto, f);
      if (h) out.push(h);
    }
    const enGuia = new Set(enlaces(texto).map((e) => e.ruta && posix.normalize(e.ruta)));
    for (const ficha of fichas) {
      if (enGuia.has(ficha)) continue;
      out.push(ctx.crear({ archivo, linea: 0, tipo: 'ficha-sin-fila-guia', clave: posix.basename(ficha), real: `${ficha} no aparece en AGENTS.md`, accion: 'humano', pregunta: `¿${ficha} va en la tabla «si vas a…» de AGENTS.md o es a propósito que no esté?` }));
    }
  }
  return out;
}
