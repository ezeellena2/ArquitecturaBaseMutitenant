/** Lo que comparten los detectores: de qué clase es cada documento y cómo se lee su estado. Solo lee. */

export const PLAN = 'docs/plans/2026-09-27-plan-de-desarrollo.md';
// «Unas 4.000 palabras como mucho» (AGENTS.md y la plantilla de specs).
export const TOPE_DE_PALABRAS = 4500;
export const ESTADOS_DE_PASO = ['pendiente', 'en curso', 'cerrado', 'insumo'];

const INDICE = /^docs\/(?:.+\/)?(?:README|plantilla)\.md$/;
const VIVO = /^docs\/(?:rules|architecture|features|guides|operations)\/[^/]+\.md$/;
const DECISION = /^docs\/decisions\/\d{4}-[^/]+\.md$/;
const DE_PASO = /^docs\/(?:plans\/.+|backlog)\.md$/;
const SPEC = /^docs\/plans\/formularios\/(\d\d)-(?!texto-diseno)/;
const PLAN_DE_ETAPA = /^docs\/plans\/\d{4}-\d\d-\d\d-etapa-(\d+)-/;

/** La clase sale de la carpeta: `indice`, `vivo`, `decision`, `de-paso`, o null si no tiene lugar. */
export function clase(archivo) {
  if (INDICE.test(archivo)) return 'indice';
  if (VIVO.test(archivo)) return 'vivo';
  if (DECISION.test(archivo)) return 'decision';
  if (DE_PASO.test(archivo)) return 'de-paso';
  return null;
}

export const documentos = (ctx) => ctx.archivos.archivos.filter((a) => /^docs\/.+\.md$/.test(a));

/** La línea `**Estado:** …` de las primeras líneas. Las decisiones viejas la escriben sin negrita. */
export function lineaDeEstado(texto) {
  const lineas = texto.split(/\r?\n/).slice(0, 10);
  for (let i = 0; i < lineas.length; i += 1) {
    const m = lineas[i].match(/^(?:\*\*)?Estado:(?:\*\*)?\s+\**(.+)$/);
    if (m) return { linea: i + 1, texto: m[1] };
  }
  return null;
}

/** La palabra del estado: lo que va antes del primer « · ». */
export const palabraDe = (estado) => estado.texto.split(' · ')[0].trim().replace(/\.$/, '');

/** El número de etapa de un plan o una spec; null en los demás. */
export function etapaDe(archivo) {
  const m = archivo.match(SPEC) ?? archivo.match(PLAN_DE_ETAPA);
  return m ? Number(m[1]) : null;
}

/** La tabla del plan de desarrollo: número → { linea, cerrada }. */
export async function leerEtapas(ctx) {
  const etapas = new Map();
  if (!ctx.archivos.existe(PLAN)) return etapas;
  for (const { linea, celdas } of ctx.markdown.filasDeTabla(await ctx.leer(PLAN))) {
    const m = celdas[0]?.match(/^(\d{1,2}) · /);
    if (m) etapas.set(Number(m[1]), { linea, cerrada: /^cerrada/i.test(celdas[1] ?? '') });
  }
  return etapas;
}

/** «a, b y c». */
export function enPalabras(lista) {
  if (lista.length < 2) return lista.join('');
  return `${lista.slice(0, -1).join(', ')} y ${lista.at(-1)}`;
}
