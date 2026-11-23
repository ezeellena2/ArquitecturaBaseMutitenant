import { etapaDe, hallazgo, nombresNuevos } from '../etapa.mjs';
import { RUTA_FRONT, esCodigoDelFront } from '../extraer.mjs';

export const nombre = 'rutas-front';
export const repos = ['front'];

/** Cada camino nuevo del router (`path: "/org/…"`) está en la spec, con cualquier nombre en sus parámetros. */
export async function detectar(ctx) {
  const etapa = await etapaDe(ctx);
  return nombresNuevos(etapa.commits, esCodigoDelFront, RUTA_FRONT)
    .filter((n) => n.nombre.length > 1 && !/^\/api(\/|$)/.test(n.nombre) && !etapa.spec.rutaFront(n.nombre))
    .map((n) => hallazgo(ctx, etapa, { tipo: 'ruta-front', ...n }));
}
