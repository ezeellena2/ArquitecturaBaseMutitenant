import { etapaDe, hallazgo, nombresNuevos } from '../etapa.mjs';
import { FUNCIONALIDAD_CLAVE, FUNCIONALIDAD_DEFINIDA, esClaveDeFuncionalidad, esDefinicionDeFuncionalidad } from '../extraer.mjs';

export const nombre = 'funcionalidades';
export const repos = ['back'];

/** Cada clave de funcionalidad nueva (un `IFeatureDefinitionContributor` o la `Key` de un `*Feature.cs`) está en la spec, entre comillas o backticks. */
export async function detectar(ctx) {
  const etapa = await etapaDe(ctx);
  const nuevas = new Map();
  const todas = [
    ...nombresNuevos(etapa.commits, esDefinicionDeFuncionalidad, FUNCIONALIDAD_DEFINIDA),
    ...nombresNuevos(etapa.commits, esClaveDeFuncionalidad, FUNCIONALIDAD_CLAVE),
  ];
  for (const n of todas) if (!nuevas.has(n.nombre)) nuevas.set(n.nombre, n);
  return [...nuevas.values()]
    .filter((n) => !etapa.spec.entreComillas(n.nombre))
    .map((n) => hallazgo(ctx, etapa, { tipo: 'funcionalidad', ...n }));
}
