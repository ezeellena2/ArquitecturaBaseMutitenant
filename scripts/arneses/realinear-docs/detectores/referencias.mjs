import { posix } from 'node:path';
import { contextoDe } from '../../comun/hallazgos.mjs';
import { anclas, enlaces, estaCerrada, lineas, marcasEtapa, rutasEnBackticks } from '../../comun/markdown.mjs';
import { estiloDe, mapaDeCapas, resolverValor } from '../../comun/rutas.mjs';

export const nombre = 'referencias';

// Valores que parecen ruta y no lo son: carpetas generadas, secretos locales, extensiones sueltas, imports (@) y plantillas.
const ignorarRuta = /(^|\/)(\.local|bin|obj|node_modules|dist|\.git|wwwroot)(\/|$)|^appsettings|\.local\.json$|^\.env|\.\.\.|…|^@|[\\%]|^\.[\w.]+$|NNNN|AAAA|(^|\/)I?X(Service|Repository|Reader|Errors|Controller|Request)\b/;

/** Líneas cuya mención es de una etapa que no cerró (D3): la propia línea o el encabezado de su sección. */
function lineasPlaneadas(texto, cerradas) {
  const planeadas = new Set();
  let nivelPlaneado = null;
  for (const l of lineas(texto)) {
    const h = !l.codigo && l.contenido.match(/^(#{1,6})\s/);
    if (h) {
      const nivel = h[1].length;
      if (nivelPlaneado !== null && nivel <= nivelPlaneado) nivelPlaneado = null;
      const marcas = marcasEtapa(l.contenido);
      if (marcas.length && !estaCerrada(marcas, cerradas)) nivelPlaneado = nivel;
    }
    if (nivelPlaneado !== null) planeadas.add(l.n);
    const marcas = marcasEtapa(l.contenido);
    if (marcas.length && !estaCerrada(marcas, cerradas)) planeadas.add(l.n);
  }
  return planeadas;
}

// Nombres que se repiten en muchas carpetas: un único candidato no prueba que sea un renombre.
const nombreGenerico = /(^|\/)(README|AGENTS|CLAUDE|index|package|HarnessStage)\.[a-z]+$/;

function similares(ancla, existentes) {
  return [...existentes].filter((a) => a.includes(ancla.slice(0, 6)) || ancla.includes(a.slice(0, 6))).slice(0, 5);
}

export async function detectar(ctx) {
  const out = [];
  const capas = mapaDeCapas(ctx.archivos);
  const cacheAnclas = new Map();
  const anclasDe = async (ruta) => {
    if (!cacheAnclas.has(ruta)) cacheAnclas.set(ruta, anclas(await ctx.leer(ruta)));
    return cacheAnclas.get(ruta);
  };

  for (const archivo of ctx.archivos.archivos) {
    if (!archivo.endsWith('.md') || ctx.excluido(archivo)) continue;
    const texto = await ctx.leer(archivo);
    const planeadas = lineasPlaneadas(texto, ctx.etapasCerradas);
    const resolverCtx = { archivos: ctx.archivos, capas, origen: archivo, raiz: ctx.raiz, pares: ctx.pares, hermano: ctx.hermano };

    for (const e of enlaces(texto)) {
      if (planeadas.has(e.linea)) continue;
      const destino = e.ruta + (e.ancla ? `#${e.ancla}` : '');
      let rutaReal = archivo;
      if (e.ruta) {
        const r = resolverValor(e.ruta, { ...resolverCtx, estricto: true });
        if (r.estado === 'fuera') continue;
        if (r.estado === 'falta') {
          out.push(hallazgoFalta(ctx, { archivo, linea: e.linea, texto, valor: e.ruta, resultado: r, esEnlace: true, destino, ancla: e.ancla, capas }));
          continue;
        }
        rutaReal = r.ruta;
      }
      if (e.ancla && rutaReal.endsWith('.md') && ctx.archivos.esArchivo(rutaReal)) {
        const existentes = await anclasDe(rutaReal);
        if (!existentes.has(decodeURIComponent(e.ancla).toLowerCase())) {
          out.push(ctx.crear({
            archivo, linea: e.linea, tipo: 'ancla-inexistente', clave: e.ancla,
            real: `#${e.ancla} no es una sección de ${rutaReal}`, accion: 'modelo',
            candidatos: similares(e.ancla, existentes), contexto: contextoDe(texto, e.linea),
          }));
        }
      }
    }

    for (const b of rutasEnBackticks(texto)) {
      if (planeadas.has(b.linea) || ignorarRuta.test(b.valor)) continue;
      const r = resolverValor(b.valor, resolverCtx);
      if (r.estado !== 'falta') continue;
      out.push(hallazgoFalta(ctx, { archivo, linea: b.linea, texto, valor: b.valor, resultado: r, esEnlace: false, capas }));
    }
  }
  return out;
}

function hallazgoFalta(ctx, { archivo, linea, texto, valor, resultado, esEnlace, destino, ancla, capas }) {
  const base = { archivo, linea, tipo: 'ruta-inexistente', clave: valor, real: `${valor} no existe`, contexto: contextoDe(texto, linea) };
  const candidatos = resultado.candidatos;
  if (candidatos.length !== 1 || nombreGenerico.test(candidatos[0])) {
    return ctx.crear({ ...base, accion: 'modelo', candidatos: candidatos.slice(0, 5) });
  }
  const [candidato] = candidatos;
  if (esEnlace) {
    let nuevo = posix.relative(posix.dirname(archivo), candidato) || '.';
    if (valor.endsWith('/')) nuevo += '/';
    const aNuevo = nuevo + (ancla ? `#${ancla}` : '');
    return ctx.crear({ ...base, esperado: candidato, accion: 'auto', parche: { tipo: 'reemplazar', archivo, linea, de: `](${destino}`, a: `](${aNuevo}` } });
  }
  const nuevo = estiloDe(valor, candidato, { archivos: ctx.archivos, capas });
  return ctx.crear({ ...base, esperado: candidato, accion: 'auto', parche: { tipo: 'reemplazar', archivo, linea, de: `\`${valor}\``, a: `\`${nuevo}\`` } });
}
