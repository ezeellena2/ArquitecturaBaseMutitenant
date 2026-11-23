import { etapaDe, hallazgo, nombresNuevos } from '../etapa.mjs';
import { ERROR, esCodigoDeError } from '../extraer.mjs';

export const nombre = 'errores';
export const repos = ['back'];

/** Cada código `Area.Entidad.Motivo` que suma un `*Errors.cs` está, entero, en la spec. */
export async function detectar(ctx) {
  const etapa = await etapaDe(ctx);
  return nombresNuevos(etapa.commits, esCodigoDeError, ERROR)
    .filter((n) => !etapa.spec.nombra(n.nombre))
    .map((n) => hallazgo(ctx, etapa, { tipo: 'error', ...n }));
}
