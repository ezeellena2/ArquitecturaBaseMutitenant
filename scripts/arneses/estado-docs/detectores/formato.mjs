import { ESTADOS_DE_PASO, TOPE_DE_PALABRAS, clase, documentos, enPalabras, etapaDe, lineaDeEstado, palabraDe } from '../clases.mjs';

export const nombre = 'formato';

const SECCIONES_DE_FEATURE = ['Carpetas', 'Lo verifica'];
const SECCIONES_DE_DECISION = ['Contexto', 'Decisión'];

const faltantes = (ctx, texto, pedidas) => {
  const titulos = ctx.markdown.secciones(texto).map((s) => s.titulo);
  return pedidas.filter((p) => !titulos.some((t) => t.includes(p)));
};

function deEstado(ctx, archivo, estado) {
  if (!estado) {
    return ctx.crear({ archivo, linea: 1, tipo: 'sin-estado', clave: 'estado', real: `${archivo}: un documento de paso declara su estado debajo del título`, accion: 'humano' });
  }
  if (ESTADOS_DE_PASO.includes(palabraDe(estado))) return null;
  const real = `${archivo}: «${palabraDe(estado)}» no es un estado; van ${enPalabras(ESTADOS_DE_PASO)}`;
  return ctx.crear({ archivo, linea: estado.linea, tipo: 'estado-invalido', clave: 'estado', real, accion: 'humano' });
}

function deLargo(ctx, archivo, texto, estado) {
  if (etapaDe(archivo) === null || (estado && palabraDe(estado) === 'cerrado')) return null;
  const palabras = texto.split(/\s+/).filter(Boolean).length;
  if (palabras <= TOPE_DE_PALABRAS) return null;
  const real = `${archivo}: ${palabras} palabras; un plan o una spec tienen unas 4.000 como mucho`;
  return ctx.crear({ archivo, linea: 1, tipo: 'muy-largo', clave: 'palabras', real, accion: 'humano' });
}

function deSecciones(ctx, archivo, texto) {
  if (/^docs\/features\//.test(archivo)) {
    const faltan = faltantes(ctx, texto, SECCIONES_DE_FEATURE);
    if (faltan.length === 0) return null;
    return ctx.crear({ archivo, linea: 1, tipo: 'formato-feature', clave: 'secciones', real: `${archivo}: falta ${enPalabras(faltan)}`, accion: 'humano' });
  }
  return null;
}

function deDecision(ctx, archivo, texto, estado) {
  const faltan = [...(estado ? [] : ['Estado']), ...faltantes(ctx, texto, SECCIONES_DE_DECISION)];
  if (faltan.length === 0) return null;
  return ctx.crear({ archivo, linea: 1, tipo: 'formato-decision', clave: 'secciones', real: `${archivo}: falta ${enPalabras(faltan)}`, accion: 'humano' });
}

/** Cada documento está en una carpeta de la ficha y tiene la forma de su clase. */
export async function detectar(ctx) {
  const out = [];
  for (const archivo of documentos(ctx)) {
    const tipo = clase(archivo);
    if (tipo === null) {
      out.push(ctx.crear({ archivo, linea: 1, tipo: 'fuera-de-lugar', clave: 'carpeta', real: `${archivo}: no es ninguna de las clases de docs/rules/documentacion.md`, accion: 'humano' }));
      continue;
    }
    if (tipo === 'indice') continue;
    const texto = await ctx.leer(archivo);
    const estado = lineaDeEstado(texto);
    const hallazgos = tipo === 'de-paso'
      ? [deEstado(ctx, archivo, estado), deLargo(ctx, archivo, texto, estado)]
      : [tipo === 'decision' ? deDecision(ctx, archivo, texto, estado) : deSecciones(ctx, archivo, texto)];
    out.push(...hallazgos.filter(Boolean));
  }
  return out;
}
