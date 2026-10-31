import { posix } from 'node:path';
import { HOJA, INDICE, leerEtapas } from '../etapas.mjs';

export const nombre = 'enlaces';

/** Los enlaces de las specs, del índice y de la hoja de ruta. `docs/plans/**` queda afuera de `realinear-docs`. */
export async function detectar(ctx) {
  const etapas = await leerEtapas(ctx);
  const documentos = [...etapas.map((e) => e.archivo), INDICE, HOJA].filter((a) => ctx.archivos.existe(a));
  const hallazgos = [];
  for (const archivo of documentos) {
    const texto = await ctx.leer(archivo);
    for (const { linea, ruta, ancla } of ctx.markdown.enlaces(texto)) {
      const destino = ruta ? posix.normalize(posix.join(posix.dirname(archivo), ruta)) : archivo;
      if (destino.startsWith('..')) continue; // Otro repo: ArquitecturaBase es referencia y no siempre está.
      const base = { archivo, linea, clave: `${ruta}#${ancla}`, accion: 'humano' };
      if (!ctx.archivos.existe(destino)) {
        hallazgos.push(ctx.crear({ ...base, tipo: 'enlace-roto', real: `${archivo}: ${ruta} no existe` }));
        continue;
      }
      if (!ancla || !destino.endsWith('.md')) continue;
      const anclas = ctx.markdown.anclas(ruta ? await ctx.leer(destino) : texto);
      if (!anclas.has(ancla)) hallazgos.push(ctx.crear({ ...base, tipo: 'ancla-rota', real: `${archivo}: ${destino} no tiene el título #${ancla}` }));
    }
  }
  return hallazgos;
}
