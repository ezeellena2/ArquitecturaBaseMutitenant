import { INDICE, PLAN, leerIndice } from '../etapas.mjs';

export const nombre = 'estados';

/** El plan de desarrollo y el índice de specs tienen que decir lo mismo de los tableros de cada etapa. */
export async function detectar(ctx) {
  if (!ctx.archivos.existe(PLAN)) return [];
  const estados = new Map();
  for (const { linea, celdas } of ctx.markdown.filasDeTabla(await ctx.leer(PLAN))) {
    const m = celdas[0]?.match(/^(\d{1,2}) · /);
    if (m) estados.set(Number(m[1]), { linea, estado: celdas[1] ?? '' });
  }

  const hallazgos = [];
  for (const fila of await leerIndice(ctx)) {
    const etapa = `E${fila.numero}`;
    const enPlan = estados.get(fila.numero);
    if (!enPlan) {
      hallazgos.push(ctx.crear({ archivo: PLAN, linea: 0, tipo: 'falta-en-el-plan', clave: etapa, real: `${etapa}: no tiene fila en el plan de desarrollo`, accion: 'humano' }));
      continue;
    }
    const indiceDibuja = fila.tramos.some((t) => t.etiqueta === 'Por dibujar');
    const planDibuja = /por dibujar/i.test(enPlan.estado);
    if (indiceDibuja === planDibuja) continue;
    const real = `${etapa}: ${INDICE} dice que ${indiceDibuja ? 'hay' : 'no hay'} tableros por dibujar, y el plan de desarrollo, lo contrario`;
    hallazgos.push(ctx.crear({ archivo: PLAN, linea: enPlan.linea, tipo: 'estado-distinto', clave: etapa, real, accion: 'humano' }));
  }
  return hallazgos;
}
