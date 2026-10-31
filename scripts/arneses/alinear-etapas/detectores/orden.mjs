import { leerEtapas, lineasDelCuerpo } from '../etapas.mjs';

export const nombre = 'orden';

/** La decisión 78: nada nombra, prueba ni dibuja algo de una etapa posterior. */
export async function detectar(ctx) {
  const hallazgos = [];
  for (const etapa of await leerEtapas(ctx)) {
    for (const l of lineasDelCuerpo(ctx.markdown, etapa.texto)) {
      if (l.miraAdelante) continue;
      const posteriores = new Set([...l.contenido.matchAll(/\bE(1[2-9]|2[0-5])\b/g)].map((m) => Number(m[1])).filter((n) => n > etapa.numero));
      for (const posterior of posteriores) {
        hallazgos.push(ctx.crear({
          archivo: etapa.archivo, linea: l.n, tipo: 'nombra-etapa-posterior', clave: `E${posterior}:${l.contenido.trim().slice(0, 60)}`,
          real: `E${etapa.numero} nombra la E${posterior}: ${l.contenido.trim().slice(0, 100)}`, accion: 'humano',
        }));
      }
    }
  }
  return hallazgos;
}
