import { HOJA, leerEtapas, lineasDelCuerpo } from '../etapas.mjs';

export const nombre = 'permisos';

async function tablaDePermisos(ctx) {
  const tabla = new Map();
  for (const { celdas } of ctx.markdown.filasDeTabla(await ctx.leer(HOJA))) {
    const codigo = celdas[0]?.match(/^`([a-z]+\.[a-z]+)`$/);
    const etapa = celdas[1]?.match(/^E(\d\d)$/);
    if (codigo && etapa) tabla.set(codigo[1], Number(etapa[1]));
  }
  return tabla;
}

/** Cada permiso que nombra una spec está en la tabla de la hoja de ruta y ya nació en esa etapa o antes. */
export async function detectar(ctx) {
  const tabla = await tablaDePermisos(ctx);
  const hallazgos = [];
  for (const etapa of await leerEtapas(ctx)) {
    const vistos = new Set();
    for (const l of lineasDelCuerpo(ctx.markdown, etapa.texto)) {
      for (const [, permiso] of l.contenido.matchAll(/`(form[a-z]*\.[a-z]+)`/g)) {
        if (vistos.has(permiso)) continue;
        const nace = tabla.get(permiso);
        const base = { archivo: etapa.archivo, linea: l.n, clave: `E${etapa.numero}:${permiso}`, accion: 'humano' };
        if (nace === undefined) {
          vistos.add(permiso);
          hallazgos.push(ctx.crear({ ...base, tipo: 'permiso-desconocido', real: `E${etapa.numero}: ${permiso} no está en la tabla de permisos de la hoja de ruta` }));
        } else if (nace > etapa.numero && !l.miraAdelante) {
          vistos.add(permiso);
          hallazgos.push(ctx.crear({ ...base, tipo: 'permiso-antes-de-su-etapa', real: `E${etapa.numero}: ${permiso} nace en la E${nace}` }));
        }
      }
    }
  }
  return hallazgos;
}
