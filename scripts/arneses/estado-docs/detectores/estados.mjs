import { PLAN, clase, documentos, etapaDe, leerEtapas, lineaDeEstado, palabraDe } from '../clases.mjs';

export const nombre = 'estados';

function etapasSinCerrar(ctx, etapas) {
  const out = [];
  for (const [numero, etapa] of etapas) {
    const enElArnes = ctx.etapasCerradas.includes(numero);
    if (etapa.cerrada === enElArnes) continue;
    const real = etapa.cerrada
      ? `E${numero}: el plan de desarrollo la da por cerrada y falta en ClosedStages, así que el arnés no exige lo marcado (E${numero})`
      : `E${numero}: está en ClosedStages y el plan de desarrollo no la da por cerrada`;
    out.push(ctx.crear({ archivo: PLAN, linea: etapa.linea, tipo: 'etapa-sin-cerrar', clave: `E${numero}`, real, accion: 'humano' }));
  }
  return out;
}

function deDocumento(ctx, archivo, estado, etapas) {
  const palabra = palabraDe(estado);
  if (palabra === 'insumo' && !estado.texto.includes('se retira cuando')) {
    return ctx.crear({ archivo, linea: estado.linea, tipo: 'insumo-sin-fin', clave: 'fin', real: `${archivo}: un insumo dice «se retira cuando …»`, accion: 'humano' });
  }
  const numero = etapaDe(archivo);
  const etapa = numero === null ? undefined : etapas.get(numero);
  if (etapa && etapa.cerrada !== (palabra === 'cerrado')) {
    const real = `${archivo}: dice «${palabra}» y el plan de desarrollo da la E${numero} por ${etapa.cerrada ? 'cerrada' : 'pendiente'}`;
    return ctx.crear({ archivo, linea: estado.linea, tipo: 'estado-distinto', clave: `E${numero}`, real, accion: 'humano' });
  }
  if (palabra !== 'cerrado') return null;
  const real = `${archivo}: terminó y sigue en el árbol`;
  const pregunta = `${real}. ¿Lo que sirve ya está en sus documentos vivos? Si es así, sacalo con \`git rm\`: queda en la historia de git.`;
  return ctx.crear({ archivo, linea: estado.linea, tipo: 'por-retirar', clave: 'retiro', real, pregunta, accion: 'humano' });
}

/** Lo que dice cada documento de paso, contra el plan de desarrollo y `ClosedStages`. */
export async function detectar(ctx) {
  const etapas = await leerEtapas(ctx);
  const out = etapasSinCerrar(ctx, etapas);
  for (const archivo of documentos(ctx)) {
    if (clase(archivo) !== 'de-paso') continue;
    const estado = lineaDeEstado(await ctx.leer(archivo));
    if (!estado) continue;
    const hallazgo = deDocumento(ctx, archivo, estado, etapas);
    if (hallazgo) out.push(hallazgo);
  }
  return out;
}
