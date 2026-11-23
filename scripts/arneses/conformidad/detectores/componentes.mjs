import { etapaDe, hallazgo } from '../etapa.mjs';
import { componenteNuevo } from '../extraer.mjs';

export const nombre = 'componentes';
export const repos = ['front'];

/** Un archivo nuevo en `src/shared/ui/` que la spec no nombra es un componente compartido que nadie pidió: un posible invento. */
export async function detectar(ctx) {
  const etapa = await etapaDe(ctx);
  const out = new Map();
  for (const commit of etapa.commits) {
    for (const archivo of commit.archivos) {
      const componente = componenteNuevo(archivo);
      if (!componente || out.has(componente) || etapa.spec.nombra(componente)) continue;
      out.set(componente, hallazgo(ctx, etapa, { tipo: 'componente', nombre: componente, commit, archivo: archivo.ruta, linea: 1 }));
    }
  }
  return [...out.values()];
}
