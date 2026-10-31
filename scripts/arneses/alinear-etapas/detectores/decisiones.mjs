import { HOJA, leerEtapas, lineasDelCuerpo } from '../etapas.mjs';

export const nombre = 'decisiones';

const CITA = /\((?:decisi[oó]n(?:es)? )?(\d{1,3}(?:(?:, | y | a )\d{1,3})*)\)/g;
const ES_UN_LIMITE = 200; // «(200)» o «(30 y 300)» son topes, no decisiones.

function leerDecisiones(ctx, hoja) {
  const seccion = ctx.markdown.secciones(hoja).find((s) => /^5\. Decisiones/.test(s.titulo));
  const decisiones = new Map();
  for (const l of seccion?.lineas ?? []) {
    const m = l.contenido.match(/^(\d{1,3})\. (.*)$/);
    if (m) decisiones.set(Number(m[1]), /Reemplazada por|Deja de aplicar/.test(m[2]));
  }
  return decisiones;
}

/** Las citas a decisiones existen y siguen vigentes, y no queda ninguna por decidir. */
export async function detectar(ctx) {
  const hoja = await ctx.leer(HOJA);
  const decisiones = leerDecisiones(ctx, hoja);
  const hallazgos = [];

  const pendientes = ctx.markdown.secciones(hoja).find((s) => /^7\. Decisiones pendientes/.test(s.titulo));
  if (pendientes && !pendientes.lineas.some((l) => l.contenido.trim() === 'Ninguna.')) {
    hallazgos.push(ctx.crear({ archivo: HOJA, linea: pendientes.linea, tipo: 'decisiones-pendientes', clave: 'seccion-7', real: 'La §7 de la hoja de ruta tiene decisiones sin tomar', accion: 'humano' }));
  }

  for (const etapa of await leerEtapas(ctx)) {
    for (const l of lineasDelCuerpo(ctx.markdown, etapa.texto)) {
      const enumera = l.contenido.includes('(1)'); // «(1) … (2) … (3)» son pasos, no citas.
      for (const cita of l.contenido.matchAll(CITA)) {
        const numeros = cita[1].split(/, | y | a /).map(Number);
        if (numeros.some((n) => n >= ES_UN_LIMITE)) continue;
        for (const n of numeros) {
          if (enumera && n < 10) continue;
          const base = { archivo: etapa.archivo, linea: l.n, clave: `E${etapa.numero}:${n}:${l.contenido.trim().slice(0, 60)}`, accion: 'humano' };
          if (!decisiones.has(n)) hallazgos.push(ctx.crear({ ...base, tipo: 'decision-inexistente', real: `E${etapa.numero}: cita la decisión ${n}, que no existe` }));
          else if (decisiones.get(n) && !/reemplaz/i.test(l.contenido)) hallazgos.push(ctx.crear({ ...base, tipo: 'decision-reemplazada', real: `E${etapa.numero}: cita la decisión ${n}, que ya no rige` }));
        }
      }
    }
  }
  return hallazgos;
}
