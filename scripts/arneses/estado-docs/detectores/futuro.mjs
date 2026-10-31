import { clase, documentos, leerEtapas } from '../clases.mjs';

export const nombre = 'futuro';

const ETAPA = String.raw`(?:Etapa\s+|E)(\d+)b?`;
const ANUNCIOS = [
  new RegExp(String.raw`\b(?:llegan?|llegarán?|nacen?|nacerán?|se suman?|se sumarán?|se implementan?|se implementarán?|se agregan?|se agregarán?|quedan? para|quedarán? para|entrarán?|estarán?|hasta)\s+(?:con\s+|en\s+|para\s+)?(?:la\s+)?${ETAPA}`, 'giu'),
  new RegExp(String.raw`${ETAPA},?\s+todavía\s+planificad[ao]`, 'giu'),
];

/** Un documento vivo cuenta lo que existe hoy: no anuncia lo que ya pasó. */
export async function detectar(ctx) {
  const etapas = await leerEtapas(ctx);
  const cerradas = new Set([...ctx.etapasCerradas, ...[...etapas].filter(([, e]) => e.cerrada).map(([n]) => n)]);
  const out = [];
  for (const archivo of documentos(ctx)) {
    if (clase(archivo) !== 'vivo') continue;
    for (const l of ctx.markdown.lineas(await ctx.leer(archivo))) {
      if (l.codigo) continue;
      for (const patron of ANUNCIOS) {
        for (const m of l.contenido.matchAll(patron)) {
          if (!cerradas.has(Number(m[1]))) continue;
          out.push(ctx.crear({ archivo, linea: l.n, tipo: 'futuro-vencido', clave: m[0], real: `${archivo}: «${m[0]}», y esa etapa ya cerró`, accion: 'humano' }));
        }
      }
    }
  }
  return out;
}
