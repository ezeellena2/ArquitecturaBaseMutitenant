import { enPalabras, esParte, leerEtapas } from '../etapas.mjs';

export const nombre = 'secciones';

// El formato de docs/plans/formularios/plantilla.md. Las specs detalladas llaman distinto a dos de sus partes.
const FORMATO = [
  ['Qué leer', /^Qué leer/],
  ['Pantallas', /^Pantallas/],
  ['Contrato', /^Contrato/],
  ['Datos', /^(?:Datos|Modelo de datos)/],
  ['Pasos', /^(?:Pasos|Archivos)/],
  ['Qué verificar al arrancar', /^(?:Qué verificar|Puntos de contacto)/],
  ['Cómo se cierra', /^Cómo se cierra/],
];

/** Una fila por etapa con lo que le falta para programarse sin adivinar. */
export async function detectar(ctx) {
  const hallazgos = [];
  const etapas = await leerEtapas(ctx);
  for (const etapa of etapas.filter((e) => !esParte(e.archivo))) {
    const propias = etapas.filter((e) => e.numero === etapa.numero);
    const titulos = propias.flatMap((e) => ctx.markdown.secciones(e.texto)).map((s) => s.titulo.replace(/^\d+[a-z]?\.\s*/, ''));
    const faltan = FORMATO.filter(([, patron]) => !titulos.some((t) => patron.test(t))).map(([seccion]) => seccion);
    if (!faltan.length) continue;
    const real = `E${etapa.numero}: ${faltan.length === 1 ? 'falta' : 'faltan'} ${enPalabras(faltan)}`;
    hallazgos.push(ctx.crear({ archivo: etapa.archivo, linea: 1, tipo: 'faltan-secciones', clave: `E${etapa.numero}`, real, accion: 'humano' }));
  }
  return hallazgos;
}
