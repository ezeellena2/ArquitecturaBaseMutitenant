import { etapaDe, hallazgo, nombresNuevos } from '../etapa.mjs';
import { PERMISO, esCatalogoDePermisos } from '../extraer.mjs';

export const nombre = 'permisos';
export const repos = ['back'];

/** Cada permiso que suma un catálogo de `Domain/Authorization/*Permissions.cs` está en la spec. */
export async function detectar(ctx) {
  const etapa = await etapaDe(ctx);
  return nombresNuevos(etapa.commits, esCatalogoDePermisos, PERMISO)
    .filter((n) => !etapa.spec.nombra(n.nombre))
    .map((n) => hallazgo(ctx, etapa, { tipo: 'permiso', ...n }));
}
