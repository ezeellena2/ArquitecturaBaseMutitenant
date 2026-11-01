/** Lo que comparten los detectores: dónde viven las etapas y cómo se leen. Solo lee. */

export const CARPETA = 'docs/plans/formularios';
export const INDICE = `${CARPETA}/README.md`;
export const HOJA = 'docs/plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md';
export const PLAN = 'docs/plans/2026-09-27-plan-de-desarrollo.md';
export const LIENZO = 'docs/design/lienzo';

// Lo único que puede nombrar una etapa posterior (decisión 78): lo que dice qué queda afuera o qué se revisa después.
const MIRA_ADELANTE = /Fuera de esta etapa|No entra|Requisitos|Puntos de contacto|Qué verificar|Relación con|Referencia/;
const ESPEC = new RegExp(`^${CARPETA}/(\\d\\d)-(?!texto-diseno)[^/]+\\.md$`);
const ETIQUETAS = /(Aprobados|A revisar|Se redibujan|Por dibujar):/g;

/** Una spec que no entra en 4.000 palabras sigue en `NN-parte-<tema>.md`: es la misma etapa y se controla igual. */
export const esParte = (archivo) => new RegExp(`^${CARPETA}/\\d\\d-parte-`).test(archivo);
export const esTablero = (nombre) => /^(?:M-)?(?:Plat-)?(?:FRM|WA)-[A-Za-z0-9-]+$/.test(nombre);
export const esCodigoDeAlla = (nombre) => /^[A-Z]{2,3}\d+(?:-[A-Z])?$/.test(nombre);

/** «a, b y c». */
export function enPalabras(lista) {
  if (lista.length < 2) return lista.join('');
  return `${lista.slice(0, -1).join(', ')} y ${lista.at(-1)}`;
}

/** La spec de cada etapa que falta programar, en orden. */
export async function leerEtapas(ctx) {
  const etapas = [];
  for (const archivo of ctx.archivos.archivos) {
    const m = archivo.match(ESPEC);
    if (!m) continue;
    const numero = Number(m[1]);
    if (ctx.etapasCerradas.includes(numero)) continue;
    etapas.push({ numero, archivo, texto: await ctx.leer(archivo) });
  }
  return etapas.sort((a, b) => a.numero - b.numero);
}

/** Las líneas de una spec, con `miraAdelante` si ahí se puede nombrar una etapa posterior. */
export function lineasDelCuerpo(markdown, texto) {
  let zona = false;
  return markdown.lineas(texto).map((l) => {
    const titulo = !l.codigo && l.contenido.match(/^#{1,6}\s+(.*)$/);
    if (titulo) zona = MIRA_ADELANTE.test(titulo[1]);
    else if (/^\*\*No entra/.test(l.contenido)) zona = true;
    else if (/^\*\*Entra/.test(l.contenido)) zona = false;
    return { ...l, miraAdelante: zona || /^\*\*Estado|Se planifica/.test(l.contenido) };
  });
}

function tramosDeTableros(texto) {
  const marcas = [...texto.matchAll(ETIQUETAS)];
  return marcas.map((m, i) => {
    const tramo = texto.slice(m.index + m[0].length, marcas[i + 1]?.index ?? texto.length);
    return { etiqueta: m[1], nombres: [...tramo.matchAll(/`([^`]+)`/g)].map((n) => n[1]) };
  });
}

/** Las filas del índice de specs: `| E15 · Nombre | spec | Aprobados: … A revisar: … Por dibujar: … |`. */
export async function leerIndice(ctx) {
  if (!ctx.archivos.existe(INDICE)) return [];
  const filas = [];
  for (const { linea, celdas } of ctx.markdown.filasDeTabla(await ctx.leer(INDICE))) {
    const m = celdas[0]?.match(/^E(\d\d) · /);
    if (!m || ctx.etapasCerradas.includes(Number(m[1]))) continue;
    filas.push({ numero: Number(m[1]), linea, tramos: tramosDeTableros(celdas[2] ?? '') });
  }
  return filas;
}
